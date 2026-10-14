using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 // 상태·대미지에 관한 계속 효과 모듈(특성·소지품·포텐셜 공용 표준 문장)
 //  기술 고정        자신은 장소에 나와서 최초로 내보낸 기술만 내보낼 수 있다.   (구애 계열과 같은 기술 고정)
 //  잠듦 기간 절반   자신의 「잠듦」 상태의 지속 T를 절반으로 한다.               (올림, 최소 1)
 //  간접 빈사 방지   자신은 공격 이외의 대미지로 빈사가 되지 않는다.             (HP 1이 남는다)
 //  확률 버팀        자신은 빈사가 되는 공격을 (드물게/저확률로/중확률로) HP 1로 버틴다.
 //  상태 대미지 면역 자신은 「독/맹독」의 대미지를 받지 않는다.                   (독·맹독·화상·동상)
 //  드레인 반전      자신에 대한 상대의 드레인 효과를 반전한다.                   (드레인한 쪽이 대미지를 받는다)
 //  오라 반전        〔오라〕의 효과를 반전한다.                                   (필드 전원 위력 배율)
 //  상태이상 면역    자신은 「상태이상」상태가 되지 않는다.                       (기존 상태 면역 모듈 확장)
 public static class AbilityStateModules {
  public const string MoveLock = "자신은 장소에 나와서 최초로 내보낸 기술만 내보낼 수 있다";
  public const string SleepHalf = "자신의 「잠듦」 상태의 지속 T를 절반으로 한다";
  public const string CursePierce = "자신이 건 「저주」의 대미지는 무효화되지 않는다";
  static readonly Regex CursePierceRaw = new Regex(@"^상대의 「저주」\s*대미지가 무효화되지 않는다$");
  public const string SurviveIndirect = "자신은 공격 이외의 대미지로 빈사가 되지 않는다";
  public const string ReverseDrain = "자신에 대한 상대의 드레인 효과를 반전한다";
  public const string AuraBreak = "〔오라〕의 효과를 반전한다";
  static readonly Regex SwitchTrap = new Regex(@"^(?<w>접지한 |「[^」]+」 타입인 )?상대는 「통상교대」를 할 수 없다$");
  static readonly Regex Chance = new Regex(@"^자신은 빈사가 되는 공격을 (?<p>드물게|저확률로|중확률로) HP 1로 버틴다(?: \(남은 HP 비례\))?$");
  static readonly Regex StatusDamage = new Regex(@"^자신은 (?<s>(?:「[^」]+」)+)의 대미지를 받지 않는다$");
  static readonly string[] DamageStatuses = { "독", "맹독", "화상", "동상" };

  static string Clean(string text) {
   return Regex.Replace(PotentialParts.Clean(text ?? ""), @"\s+", " ").Trim().TrimEnd('.', '。', '．').Trim();
  }
  static string[] Names(string raw) {
   return Regex.Matches(raw ?? "", "「([^」]+)」").Cast<Match>().SelectMany(m => m.Groups[1].Value.Split('/')).Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
  }
  static IEnumerable<string> Own(Fighter f, Fighter other, Rules rules, string keyword) {
   return f == null ? new string[0] : PotentialModifiers.Sentences(f, other, rules ?? new Rules(), keyword).Select(Clean);
  }
  static double Rate(string word) {
   return word == "드물게" ? .10 : word == "저확률로" ? .20 : .50;
  }

  public static string Normalize(string text) {
   string s = Clean(text);
   if (s.Length == 0) return null;
   if (CursePierceRaw.IsMatch(s)) return CursePierce + ".";
   if (Classify(s).Length > 0) return s + ".";
   return null;
  }
  public static string Classify(string sentence) {
   string s = Clean(sentence);
   if (s == MoveLock) return "기술 고정";
   if (s == SleepHalf) return "잠듦 기간 절반";
   if (s == SurviveIndirect) return "간접 대미지 빈사 방지";
   if (s == CursePierce) return "저주 대미지 관통";
   if (s == ReverseDrain) return "드레인 반전";
   if (s == AuraBreak) return "오라 반전";
   if (Chance.IsMatch(s)) return "확률 버팀";
   var trap = SwitchTrap.Match(s);
   if (trap.Success && (!trap.Groups["w"].Value.StartsWith("「") || Names(trap.Groups["w"].Value).All(MoveCondition.TypeNames.Contains))) return "통상교대 금지";
   var m = StatusDamage.Match(s);
   if (m.Success && Names(m.Groups["s"].Value).All(DamageStatuses.Contains)) return "상태이상 대미지 면역";
   return "";
  }

  // ===== 판정 =====
  public static bool MoveLocked(Fighter f) {
   return f != null && f.OnField && Own(f, null, EffectScope.Current == null ? null : EffectScope.Current.Rules, "최초로").Any(x => x == MoveLock);
  }
  // 잠듦 지속 T(올림 절반, 최소 1)
  public static int SleepTurns(Fighter target, int turns, Rules rules) {
   if (turns <= 1 || !Own(target, null, rules, "잠듦").Any(x => x == SleepHalf)) return turns;
   return Math.Max(1, (turns + 1) / 2);
  }
  public static bool SurvivesIndirect(Fighter f) {
   return f != null && Own(f, null, EffectScope.Current == null ? null : EffectScope.Current.Rules, "빈사가 되지").Any(x => x == SurviveIndirect);
  }
  // 반드시 버티는 것 말고 확률로 버티는 것. 공격 한 번마다 굴린다
  public static bool EndureChance(Fighter target, Fighter attacker, Rules rules, Func<double> rng) {
   foreach (string line in Own(target, attacker, rules, "버틴다")) {
    var m = Chance.Match(line);
    if (m.Success && (rng ?? (() => 1))() < (line.Contains("남은 HP 비례")
     ? HpScaledChance.Probability(Rate(m.Groups["p"].Value), target.HP, Mechanics.MaxHP(target, attacker, rules))
     : Rate(m.Groups["p"].Value))) return true;
   }
   return false;
  }
  // 저주 대미지가 무효화되지 않는가: 저주를 건 쪽이 CursePierce 문장을 가지면 간접 대미지 면역을 건너뛴다
  public static bool PiercesCurse(Fighter victim, string reason) {
   if (victim == null || reason != "저주" || !victim.Conditions.Curse) return false;
   var source = victim.Conditions.CurseSource;
   if (source == null || source.Data == null) return false;
   var rules = EffectScope.Current == null ? null : EffectScope.Current.Rules;
   return Own(source, victim, rules, "저주").Any(x => x == CursePierce);
  }
  // 사유(독·맹독·화상·동상)의 대미지를 받지 않는가
  public static bool IgnoresDamageSource(Fighter f, string reason) {
   if (f == null || string.IsNullOrEmpty(reason)) return false;
   foreach (string line in Own(f, null, EffectScope.Current == null ? null : EffectScope.Current.Rules, "대미지를 받지")) {
    var m = StatusDamage.Match(line);
    if (!m.Success) continue;
    foreach (string name in Names(m.Groups["s"].Value)) if (DamageStatuses.Contains(name) && (reason == name || reason.StartsWith(name + " "))) return true;
   }
   return false;
  }
  // 도주·탈피: 통상교대를 막는 상태(검은눈빛·조이기·붙잡힘)에서도 반드시 교대할 수 있다
  public const string AlwaysSwitch = "반드시 아군과 교대할 수 있다";
  public static bool AlwaysSwitchable(Fighter f) {
   return f != null && Own(f, null, EffectScope.Current == null ? null : EffectScope.Current.Rules, "반드시 아군과").Any(x => x == AlwaysSwitch);
  }
  // 개미지옥(접지한 상대)·그림자밟기(상대 전원)·자력(강철 타입 상대): 상대가 통상교대(『돌아와！』 포함)를 할 수 없다
  public static string SwitchTrapReason(Fighter holder, Fighter user) {
   if (holder == null || user == null || holder.HP <= 0 || !holder.OnField) return "";
   var rules = EffectScope.Current == null ? new Rules() : EffectScope.Current.Rules;
   foreach (string line in Own(holder, user, rules, "통상교대")) {
    var m = SwitchTrap.Match(line);
    if (!m.Success) continue;
    string who = m.Groups["w"].Value;
    if (who.StartsWith("접지") && !Mechanics.Grounded(user, rules)) continue;
    if (who.StartsWith("「") && !Names(who).Any(t => Mechanics.Has(user, t))) continue;
    return "상대의 효과로 통상교대를 할 수 없다.";
   }
   return "";
  }
  public static bool ReversesDrain(Fighter drained, Fighter drainer, Rules rules) {
   return drained != null && Own(drained, drainer, rules, "드레인").Any(x => x == ReverseDrain);
  }
  // 〔오라〕의 효과를 반전하는 포켓몬이 장소에 있는가(양쪽 모두 영향)
  public static bool AurasReversed(Fighter a, Fighter b, Rules rules) {
   return Own(a, b, rules, "오라").Any(x => x == AuraBreak) || Own(b, a, rules, "오라").Any(x => x == AuraBreak);
  }
 }
}
