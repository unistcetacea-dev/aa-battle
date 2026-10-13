using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 // 오레마스 전용 포텐셜 유형이 쓰는 새 효과 문장(표준 문장 · 정규화 · 분류 · 도우미).
 //  추격 발동 배율   자신이 「추격」 효과로 주는 대미지를 강화(N배)한다.
 //  상태변화 전체 해제  상대의(자신의) 모든 상태변화를 해제한다.
 // 사건으로 얻는 필중·방어 관통·방호 특성 관통은 기존 표준 문장(AbilityMoveModules)을 그대로 쓰고,
 // 실행기(PotentialEvents.Exclusive.cs)가 이번 행동 동안의 효과(ActiveModifier)로 건다.
 public static class ExclusiveModules {
  static readonly Regex PursuitScale = new Regex(@"^자신이 「추격」 효과로 주는 대미지를 강화\((?<n>[0-9.]+)배\)한다$");
  static readonly Regex ReleaseAll = new Regex(@"^(?<t>자신|상대)의 모든 상태변화를 해제한다$");

  public static string PursuitSentence(string rate) { return "자신이 「추격」 효과로 주는 대미지를 강화(" + rate + "배)한다."; }

  static string Clean(string text) {
   return Regex.Replace(PotentialParts.Clean(text ?? ""), @"\s+", " ").Trim().TrimEnd('.', '。', '．').Trim();
  }

  // 수집 원문 변형 → 표준 문장. 아니면 null
  public static string Normalize(string text) {
   string s = Clean(text);
   if (s.Length == 0) return null;
   // 「추격」효과의 발동 시, 기술의 데미지를 2배로 한다
   var m = Regex.Match(s, @"^「추격」\s*효과의?\s*발동\s*시\s*,?\s*(?:자신의\s*)?기술의\s*(?:대미지|데미지)(?:를|가)\s*(?<n>[0-9]+(?:\.[0-9]+)?)배(?:로\s*한다|가\s*된다)$");
   if (m.Success) return PursuitSentence(m.Groups["n"].Value);
   if (PursuitScale.IsMatch(s)) return s + ".";
   m = Regex.Match(s, @"^(?:(?<t>자신|상대)의\s*)?(?:「상태변화」|모든\s*상태변화)(?:를|을)\s*해제한다$");
   if (m.Success) return (m.Groups["t"].Success ? m.Groups["t"].Value : "상대") + "의 모든 상태변화를 해제한다.";
   return null;
  }

  public static string Classify(string sentence) {
   string s = Clean(sentence);
   if (PursuitScale.IsMatch(s)) return "추격 발동 배율";
   if (ReleaseAll.IsMatch(s)) return "상태변화 전체 해제";
   return "";
  }

  public static bool IsPursuitScale(string sentence) { return PursuitScale.IsMatch(Clean(sentence)); }
  public static bool TryPursuitScale(string sentence, out double rate) {
   rate = 1;
   var m = PursuitScale.Match(Clean(sentence));
   return m.Success && double.TryParse(m.Groups["n"].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out rate);
  }
  public static bool TryReleaseAll(string sentence, out string target) {
   var m = ReleaseAll.Match(Clean(sentence));
   target = m.Success ? m.Groups["t"].Value : "";
   return m.Success;
  }

  // 이 포켓몬이 이번 추격에 쓸 수 있는 「추격 발동 배율」(선행입력이면 선언한 것만)
  public static double PursuitRate(Fighter owner, string[] declared) {
   double rate = 1;
   if (owner == null || owner.Data == null) return rate;
   foreach (var p in (owner.Data.potentials ?? new Potential[0]).Where(x => x != null && (x.description ?? "").Contains("「추격」 효과로 주는"))) {
    if ((p.activation ?? "").Trim() == "선행" && !(declared ?? new string[0]).Contains(p.name)) continue;
    string condition, effect;
    PotentialLibrary.Split(p.description ?? "", out condition, out effect);
    foreach (string line in EffectNormalizer.NormalizeLines(effect.Length > 0 ? effect : p.description ?? "").Replace("\r", "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
     double value;
     if (TryPursuitScale(line, out value)) rate *= value;
    }
   }
   return rate;
  }

  // 상대(또는 자신)의 상태변화를 모두 푼다. 대타출동·소란·행동불능·숨은 상태는 상태변화로 보지 않는다
  static readonly string[] Released = {
   "혼란", "도발", "풀죽음", "씨뿌리기", "침묵", "망각", "트집", "회복봉인", "사슬묶기", "검은눈빛", "조이기", "방어", "버티기", "저주",
   "작아지기", "꿰뚫어보기", "하품", "기충전", "매직코트", "악몽", "분진", "전자부유", "텔레키네시스", "금제", "예민해지기", "날따름",
   "순서미루기", "봉인", "앙코르", "악인", "침수", "송전", "가열", "냉각", "충전", "폭탄", "요격", "분노", "마음의눈", "도우미", "웅크리기",
   "아쿠아링", "뿌리박기", "헤롱헤롱", "동면", "죽음의 카운트", "먹이를 문"
  };
  public static bool ReleaseAllConditions(Fighter f, List<string> log) {
   if (f == null || f.HP <= 0) return false;
   bool any = false;
   foreach (string name in Released) {
    if (!Mechanics.HasCondition(f, name)) continue;
    Mechanics.ClearCondition(f, name);
    any = true;
    if (log != null) log.Add(f.Data.name + ": 「" + name + "」 상태가 해제됐다");
   }
   return any;
  }
 }
}
