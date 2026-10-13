using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace AABattle {
 // 계속 효과. 특성 이름 대신 같은 표준 문장을 포텐셜에서도 판정한다.
 public static class AbilityCriticalFieldModules {
  public const string CriticalRankZero = "상대의 기술의 급소 랭크 보정을 0으로 한다";
  public const string FieldAll = "필드에 있는 전원";
  public const string FieldOthers = "자신을 제외한 전원";

  static string Clean(string text) {
   return Regex.Replace(PotentialParts.Clean(text ?? ""), @"\s+", " ").Trim().TrimEnd('.', '。', '．').Trim();
  }

  public static bool IsCriticalRankZero(string text) {
   return Clean(text) == CriticalRankZero;
  }

  public static string Normalize(string text) {
   string s = Clean(text);
   if (IsCriticalRankZero(s) || Regex.IsMatch(s, @"^상대의 「C」가 「±0」이 된다$")) return CriticalRankZero + ".";
   var match = Regex.Match(s, @"^(?<who>필드에 있는 전원|자신을 제외한 전원)(?<tail>의 .+)$");
   if (!match.Success) return null;
   string tail = match.Groups["tail"].Value;
   // 도감의 타입명과 「기술을 강화」는 기존 위력 모듈로 표현한다.
   tail = Regex.Replace(tail, @"(?<t>악|페어리)기술", "「${t}」 기술");
   tail = tail.Replace("「페어리」「악」", "「페어리/악」");
   tail = Regex.Replace(tail, @"기술을 (?=강화|약화)", "기술의 위력을 ");
   tail = Regex.Replace(tail, @"(강화|약화)(\([^)]*\))시킨다$", "$1$2한다");
   string normalized = EffectModules.Normalize("자신" + tail);
   ScalarEffect scalar;
   if (normalized == null || !EffectModules.TryScalar(normalized, out scalar)) return null;
   return match.Groups["who"].Value + normalized.Substring("자신".Length);
  }

  public static bool TryFieldScalar(string text, out ScalarEffect effect) {
   effect = null;
   var match = Regex.Match(Clean(text), @"^(?<who>필드에 있는 전원|자신을 제외한 전원)(?<tail>의 .+)$");
   if (!match.Success || !EffectModules.TryScalar("자신" + match.Groups["tail"].Value, out effect)) return false;
   effect.Subject = match.Groups["who"].Value;
   effect.Text = text;
   return true;
  }

  public static string Classify(string text) {
   if (IsCriticalRankZero(text)) return "급소 랭크 보정 0";
   ScalarEffect scalar;
   return TryFieldScalar(text, out scalar) ? "필드 전원 수치 배율" : "";
  }

  public static bool HasCriticalRankZero(Fighter target, Fighter attacker, Rules rules) {
   return PotentialModifiers.Sentences(target, attacker, rules, "급소 랭크").Any(IsCriticalRankZero);
  }

  public static bool FieldSubject(string subject) {
   return subject == FieldAll || subject == FieldOthers;
  }

  public static bool AppliesTo(ScalarEffect scalar, Fighter owner, Fighter holder) {
   return holder != null && holder.OnField && holder.HP > 0 &&
    (scalar.Subject == FieldAll || scalar.Subject == FieldOthers && !ReferenceEquals(owner, holder));
  }
 }
}
