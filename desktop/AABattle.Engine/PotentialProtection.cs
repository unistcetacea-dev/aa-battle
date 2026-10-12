using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 // G 방호(확정 28): 포켓몬 포텐셜의 기술 무효화(방호)·회피(방호 아님)와 방호 관통.
 // 방호 관통은 포켓몬 포텐셜의 방호만 뚫고 회피·방어 기술·방호 특성·트레이너 방호는 뚫지 않는다.
 public static class PotentialProtection {
  static readonly Regex Guard = new Regex(@"^상대의 (?<c>(?:「[^」]+」)*)\s*기술을 (?:(?<p>드물게|저확률로|중확률로) )?(?<v>무효화|회피)한다$");
  static readonly Regex Pierce = new Regex(@"^자신의 (?<c>(?:「[^」]+」)*)\s*기술이 상대의 방호 포텐셜을 무시한다$");

  static string Clean(string text) { return Regex.Replace(PotentialParts.Clean(text ?? "").Trim().TrimEnd('.', '。', '．').Trim(), @"\s+", " "); }
  static double? Rate(string word) { return word == "드물게" ? .10 : word == "저확률로" ? .20 : word == "중확률로" ? .50 : (double?)null; }

  // 「상대의 「조건」 기술을 (확률) 무효화/회피한다」
  public static bool TryGuard(string text, out bool evade, out MoveCondition condition, out double? rate) {
   evade = false; condition = new MoveCondition(); rate = null;
   var m = Guard.Match(Clean(text));
   if (!m.Success || !MoveCondition.TryParse(m.Groups["c"].Value, out condition)) return false;
   evade = m.Groups["v"].Value == "회피"; rate = Rate(m.Groups["p"].Value);
   return true;
  }
  public static bool TryPierce(string text, out MoveCondition condition) {
   condition = new MoveCondition();
   var m = Pierce.Match(Clean(text));
   return m.Success && MoveCondition.TryParse(m.Groups["c"].Value, out condition);
  }
  public static bool IsAnalysisImmunity(string text) { return Clean(text) == "자신에 대한 데이터 해석을 무효화한다"; }
  public static string Canonical(MoveCondition condition, string probability, bool evade) {
   return "상대의 " + (condition == null || condition.Empty ? "" : condition.Text + " ") + "기술을 " + (string.IsNullOrEmpty(probability) ? "" : probability + " ") + (evade ? "회피" : "무효화") + "한다.";
  }

  // 계속 성립하는 포텐셜 문장 + 사건으로 걸린 방호(ConditionState.Modifiers)
  static IEnumerable<string> Sentences(Fighter owner, Fighter opponent, Rules rules, string keyword) {
   foreach (string s in PotentialModifiers.Sentences(owner, opponent, rules, keyword)) yield return s;
   foreach (var m in owner.Conditions.Modifiers ?? new List<ActiveModifier>()) if (m != null && (m.Text ?? "").Contains(keyword)) yield return m.Text;
  }

  public static bool AnalysisBlocked(Fighter target, Fighter analyst, Rules rules) {
   return target != null && Sentences(target, analyst, rules ?? new Rules(), "해석").Any(IsAnalysisImmunity);
  }

  // 행동 시점(명중 판정 직전) 판정. 막았으면 ("회피"|"무효", 출처 문장), 아니면 null
  public static KeyValuePair<string, string>? Check(Database db, Fighter a, Fighter b, Move move, Rules rules, Func<double> rng, bool sureHit) {
   if (a == null || b == null || move == null || b.HP <= 0) return null;
   double type = Mechanics.TypeEffect(db, a, b, move, rules);
   bool pierce = Sentences(a, b, rules, "방호").Any(s => { MoveCondition c; return TryPierce(s, out c) && c.Matches(move, a, type); });
   foreach (string s in Sentences(b, a, rules, "무효").Concat(Sentences(b, a, rules, "회피")).Where(x => x.Contains("무효화") || x.Contains("회피")).Distinct()) {
    bool evade; MoveCondition c; double? rate;
    if (!TryGuard(s, out evade, out c, out rate) || !c.Matches(move, a, type)) continue;
    if (evade && sureHit) continue;          // 필중 기술은 회피하지 못한다
    if (!evade && pierce) continue;          // 방호 관통: 포켓몬 포텐셜의 무효화(방호)만 뚫는다
    if (rate.HasValue && (rng ?? (() => .5))() >= rate.Value) continue;
    return new KeyValuePair<string, string>(evade ? "회피" : "무효", s);
   }
   return null;
  }
 }
}
