using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 // G 방호(확정 28): 포텐셜의 기술 무효화·반사(방호)와 회피(방호 아님), 방호 관통.
 // 포켓몬의 방호 관통은 포켓몬 포텐셜의 방호만 뚫는다. 트레이너 포텐셜의 방호(무효화·반사·『버텨라！』)는
 // 관통이 트레이너 포텐셜에서 나왔거나 「트레이너 방호」를 명시했을 때만 뚫린다. 회피는 어떤 관통에도 뚫리지 않는다.
 public static class PotentialProtection {
  static readonly Regex Guard = new Regex(@"^상대의 (?<c>(?:「[^」]+」)*)\s*기술을 (?:(?<once>1번만) )?(?:(?<p>드물게|저확률로|중확률로) )?(?<v>무효화|회피|반사)한다$");
  static readonly Regex Pierce = new Regex(@"^자신의 (?<c>(?:「[^」]+」)*)\s*기술이 상대의 (?<tr>트레이너 )?방호 포텐셜을 무시한다$");

  static string Clean(string text) { return Regex.Replace(PotentialParts.Clean(text ?? "").Trim().TrimEnd('.', '。', '．').Trim(), @"\s+", " "); }
  static double? Rate(string word) { return word == "드물게" ? .10 : word == "저확률로" ? .20 : word == "중확률로" ? .50 : (double?)null; }

  // 「상대의 「조건」 기술을 (확률) 무효화/회피/반사한다」. kind = 무효화·회피·반사
  public static bool TryGuard(string text, out string kind, out MoveCondition condition, out double? rate) {
   bool once;
   return TryGuard(text, out kind, out condition, out rate, out once);
  }
  // once: 「1번만」(특성의 1/시 방호. 쓴 기록은 포켓몬이 들고 있고, 사용 횟수 복구 효과로 지운다)
  public static bool TryGuard(string text, out string kind, out MoveCondition condition, out double? rate, out bool once) {
   kind = ""; condition = new MoveCondition(); rate = null; once = false;
   var m = Guard.Match(Clean(text));
   if (!m.Success || !MoveCondition.TryParse(m.Groups["c"].Value, out condition)) return false;
   kind = m.Groups["v"].Value; rate = Rate(m.Groups["p"].Value); once = m.Groups["once"].Success;
   return true;
  }
  public static bool TryPierce(string text, out MoveCondition condition, out bool trainer) {
   condition = new MoveCondition(); trainer = false;
   var m = Pierce.Match(Clean(text));
   if (!m.Success || !MoveCondition.TryParse(m.Groups["c"].Value, out condition)) return false;
   trainer = m.Groups["tr"].Success; return true;
  }
  public static bool TryPierce(string text, out MoveCondition condition) { bool trainer; return TryPierce(text, out condition, out trainer); }
  public static bool IsAnalysisImmunity(string text) { return Clean(text) == "자신에 대한 데이터 해석을 무효화한다"; }
  public static string Canonical(MoveCondition condition, string probability, string kind) {
   return "상대의 " + (condition == null || condition.Empty ? "" : condition.Text + " ") + "기술을 " + (string.IsNullOrEmpty(probability) ? "" : probability + " ") + kind + "한다.";
  }
  public static string Canonical(MoveCondition condition, string probability, bool evade) { return Canonical(condition, probability, evade ? "회피" : "무효화"); }

  sealed class Line { public string Text; public bool Trainer; }

  // 포켓몬 쪽: 계속 성립하는 포텐셜 문장 + 사건으로 걸린 것. 트레이너 쪽: 장소에 있는 동안 성립하는 트레이너 고유 포텐셜
  static IEnumerable<Line> Lines(Fighter owner, Fighter opponent, Rules rules, string keyword) {
   var all = owner.Data == null ? new Potential[0] : owner.Data.potentials ?? new Potential[0];
   // 전용 포텐셜은 포켓몬에 달려 있어도 트레이너 포텐셜이다(트레이너 방호·관통)
   var ordinary = all.Where(p => p == null || p.id != TeamRules.ExclusiveSlot).ToList();
   var exclusive = all.Where(p => p != null && p.id == TeamRules.ExclusiveSlot).ToList();
   foreach (string s in PotentialModifiers.Sentences(owner, opponent, rules, keyword, ordinary)) yield return new Line { Text = s };
   if (exclusive.Count > 0)
    foreach (string s in PotentialModifiers.Sentences(owner, opponent, rules, keyword, exclusive)) yield return new Line { Text = s, Trainer = true };
   foreach (var m in owner.Conditions.Modifiers ?? new List<ActiveModifier>()) if (m != null && (m.Text ?? "").Contains(keyword)) yield return new Line { Text = m.Text, Trainer = m.Trainer };
   var trainer = TrainerOf(owner);
   if (trainer == null) yield break;
   foreach (var p in TrainerTriggers.Matching(trainer, TriggerStructures.AlwaysLabel).Concat(TrainerTriggers.Matching(trainer, TriggerStructures.FieldLabel)).Distinct()) {
    if ((p.effect ?? "").IndexOf(keyword, StringComparison.Ordinal) < 0) continue;
    foreach (string s in Regex.Split(EffectNormalizer.NormalizeLines(p.effect ?? "").Replace("\r", ""), @"\n|(?<=[.．。])\s+").Where(x => x.Trim().Length > 0)) yield return new Line { Text = s.Trim(), Trainer = true };
   }
  }

  static TrainerRecord TrainerOf(Fighter f) {
   var scope = EffectScope.Current;
   if (scope == null || scope.States == null || f == null) return null;
   int side = Array.IndexOf(scope.Fighters, f);
   return side < 0 || side >= scope.States.Length || scope.States[side] == null ? null : scope.States[side].Trainer;
  }

  public static bool AnalysisBlocked(Fighter target, Fighter analyst, Rules rules) {
   return target != null && Lines(target, analyst, rules ?? new Rules(), "해석").Any(x => IsAnalysisImmunity(x.Text));
  }

  // 공격 측의 관통: (포켓몬 방호를 뚫는가, 트레이너 방호를 뚫는가)
  static void Piercing(Fighter a, Fighter b, Move move, Rules rules, double type, out bool pokemon, out bool trainer) {
   pokemon = trainer = false;
   foreach (var line in Lines(a, b, rules, "방호")) {
    MoveCondition c; bool explicitTrainer;
    if (!TryPierce(line.Text, out c, out explicitTrainer) || !c.Matches(move, a, type)) continue;
    pokemon = true;
    if (line.Trainer || explicitTrainer) trainer = true;
   }
  }
  // 『버텨라！』 같은 트레이너 방호를 뚫는가
  public static bool PiercesTrainer(Database db, Fighter a, Fighter b, Move move, Rules rules) {
   bool pokemon, trainer;
   Piercing(a, b, move, rules, Mechanics.TypeEffect(db, a, b, move, rules), out pokemon, out trainer);
   return trainer;
  }

  // 행동 시점(명중 판정 직전) 판정. 막았으면 (회피|무효|반사, 출처 문장), 아니면 null
  public static KeyValuePair<string, string>? Check(Database db, Fighter a, Fighter b, Move move, Rules rules, Func<double> rng, bool sureHit) {
   if (a == null || b == null || move == null || b.HP <= 0) return null;
   double type = Mechanics.TypeEffect(db, a, b, move, rules);
   bool piercePokemon, pierceTrainer;
   Piercing(a, b, move, rules, type, out piercePokemon, out pierceTrainer);
   var seen = new HashSet<string>();
   foreach (var line in Lines(b, a, rules, "무효").Concat(Lines(b, a, rules, "회피")).Concat(Lines(b, a, rules, "반사"))) {
    if (!seen.Add((line.Trainer ? "T:" : "P:") + line.Text)) continue;
    string kind; MoveCondition c; double? rate; bool once;
    if (!TryGuard(line.Text, out kind, out c, out rate, out once)) continue;
    MoveCondition.MatchTarget = b;
    try { if (!c.Matches(move, a, type)) continue; } finally { MoveCondition.MatchTarget = null; }
    if (once && b.Conditions.GuardUsed.Contains(line.Text)) continue;                  // 1번만: 이미 썼다
    if (kind == "회피" && sureHit) continue;                                         // 필중 기술은 회피하지 못한다
    if (kind != "회피" && (line.Trainer ? pierceTrainer : piercePokemon)) continue;   // 방호(무효화·반사) 관통
    if (rate.HasValue && (rng ?? (() => .5))() >= rate.Value) continue;
    if (once) b.Conditions.GuardUsed.Add(line.Text);
    return new KeyValuePair<string, string>(kind == "회피" ? "회피" : kind == "반사" ? "반사" : "무효", (line.Trainer ? "트레이너 " : "") + line.Text);
   }
   return null;
  }
 }
}
