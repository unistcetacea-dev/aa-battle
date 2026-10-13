using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 // 이름으로 상태(상태이상·상태변화)를 푼다. 효과 모듈 「상태 치유」: 「자신의 「X」「Y」 상태를 치유한다」(「상태이상」은 상태이상 전체)
 public static class StateCure {
  // 사용자 결정: 「치유」는 상태이상(혼란은 예외로 포함), 그 밖의 상태변화를 푸는 것은 「해제」
  static readonly Regex Pattern = new Regex(@"^(?<t>자신|상대)의 (?<n>(?:「[^」]+」)+) 상태를 (?<v>치유|해제)한다$");
  static bool Curable(string name) { return name == "상태이상" || name == "혼란" || Mechanics.Statuses.Contains(name) && name != "정상"; }

  public static string Normalize(string text) {
   string t = Regex.Replace(PotentialParts.Clean(text ?? ""), @"\s+", " ").Trim().TrimEnd('.', '。').Trim();
   var m = Regex.Match(t, @"^(?:(?<t>자신|상대)의\s*)?(?<n>(?:「[^」]+」\s*(?:또는|이나|나|,)?\s*)+)\s*(?:상태|상태이상|상태변화)?(?:을|를)\s*(?:치유한다|회복한다|해제한다)$");
   if (!m.Success) return null;
   var names = Regex.Matches(m.Groups["n"].Value, "「([^」]+)」").Cast<Match>().SelectMany(x => Regex.Split(x.Groups[1].Value, @"[/()（）]")).Select(x => x.Trim()).Where(x => x.Length > 0).Distinct().ToArray();
   if (names.Length == 0 || !names.All(Known)) return null;
   string target = m.Groups["t"].Success ? m.Groups["t"].Value : "자신";
   var cure = names.Where(Curable).ToArray(); var release = names.Where(x => !Curable(x)).ToArray();
   string result = cure.Length > 0 ? target + "의 " + string.Concat(cure.Select(x => "「" + x + "」")) + " 상태를 치유한다." : "";
   if (release.Length > 0) result += (result.Length > 0 ? " " : "") + target + "의 " + string.Concat(release.Select(x => "「" + x + "」")) + " 상태를 해제한다.";
   return result;
  }
  static bool Known(string name) { return name == "상태이상" || Mechanics.Statuses.Contains(name) && name != "정상" || StateEffects.Table.Any(x => x.Name == name); }
  public static bool TryParse(string text, out string target, out string[] names, out bool release) {
   var m = Pattern.Match(Regex.Replace(PotentialParts.Clean(text ?? ""), @"\s+", " ").Trim().TrimEnd('.', '。').Trim());
   target = m.Success ? m.Groups["t"].Value : "";
   names = m.Success ? Regex.Matches(m.Groups["n"].Value, "「([^」]+)」").Cast<Match>().Select(x => x.Groups[1].Value).ToArray() : new string[0];
   release = m.Success && m.Groups["v"].Value == "해제";
   return m.Success && names.Length > 0 && (release ? names.All(x => !Curable(x)) : names.All(Curable));
  }

  // 지정한 상태를 푼다. 실제로 풀린 것이 있으면 true
  public static bool Cure(Fighter f, IEnumerable<string> names, List<string> log) {
   if (f == null || f.HP <= 0) return false;
   bool cured = false;
   foreach (string name in names) {
    if ((name == "상태이상" || name == f.Status || name == "독" && f.Status == "맹독") && f.Status != "정상") {
     if (log != null) log.Add(f.Data.name + ": 「" + f.Status + "」 치유");
     f.Status = "정상"; f.Conditions.ToxicStage = 1; f.Conditions.SleepFromRest = false; cured = true;
     continue;
    }
    if (Mechanics.HasCondition(f, name)) { Mechanics.ClearCondition(f, name); if (log != null) log.Add(f.Data.name + ": 「" + name + "」 상태가 풀렸다"); cured = true; }
   }
   return cured;
  }
 }

 public static partial class Mechanics {
  // HasCondition과 같은 이름표로 상태변화를 푼다
  public static void ClearCondition(Fighter f, string name) {
   var c = f.Conditions;
   switch (name) {
    case "혼란": c.Confusion = 0; break;
    case "도발": c.Taunt = 0; break;
    case "풀죽음": c.Flinch = false; break;
    case "씨뿌리기": c.Seed = false; break;
    case "침묵": c.Silenced = 0; break;
    case "망각": c.Forgotten = 0; break;
    case "트집": c.Tormented = false; break;
    case "회복봉인": c.HealBlock = 0; break;
    case "사슬묶기": c.Disabled = 0; c.DisabledMove = ""; break;
    case "검은눈빛": c.MeanLook = false; break;
    case "조이기": c.Bound = 0; break;
    case "앙코르": c.EncoreTurns = 0; c.EncoreMove = ""; break;
    case "악인": c.Dark = false; break;
    case "침수": c.Soaked = false; break;
    case "송전": c.Electrified = false; break;
    case "가열": c.Heated = false; break;
    case "냉각": c.Chilled = false; break;
    case "충전": c.Charged = false; break;
    case "폭탄": c.Bomb = false; break;
    case "요격": c.Intercept = 0; break;
    case "분노": c.Rage = 0; break;
    case "마음의눈": c.MindReaderSide = -1; break;
    case "도우미": c.HelpingHand = false; break;
    case "웅크리기": c.Curled = false; break;
    case "아쿠아링": c.AquaRing = false; break;
    case "뿌리박기": c.Ingrain = false; break;
    case "헤롱헤롱": c.AttractSide = -1; break;
    case "동면": c.Hibernate = 0; break;
    case "죽음의 카운트": c.DeathCount = 0; break;
    case "먹이를 문": c.Gulping = false; break;
   }
  }
 }
}
