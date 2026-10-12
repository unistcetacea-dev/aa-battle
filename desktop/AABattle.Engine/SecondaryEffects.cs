using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 // 추가효과 모듈: 「자신의 「조건」 기술에 「N할:상태」의 추가효과를 부여한다」(계속 효과). 「N할:공격-1」 같은 랭크도 같은 모듈(−는 상대, +는 자신).
 // 기술 자체의 「N할:X」도 같은 Roll을 쓴다(MoveRuntime)
 // 공격이 맞으면(대타에 막히지 않고 상대가 남아 있으면) 타격마다 그 확률로 상대에게 상태를 건다.
 // 풀죽음은 행동권이 없는 상대에게도 걸린다(사용자 결정: 의미가 없을 뿐, 턴 끝에 풀린다)
 public static class SecondaryEffects {
  static readonly Regex Pattern = new Regex(@"^자신의 (?<c>(?:「[^」]+」)*)\s*(?<o>공격|기술)에 (?<e>(?:「\d+할:[^」]+」)+)의 추가효과를 부여한다$");

  static string Clean(string text) { return Regex.Replace(PotentialParts.Clean(text ?? ""), @"\s+", " ").Trim().TrimEnd('.', '。', '．').Trim(); }
  static bool KnownState(string name) { return Mechanics.Statuses.Contains(name) && name != "정상" || StateEffects.Find(name) != null; }
  // 「N할:X」의 X: 상태(「마비/화상/얼음」은 그중 하나) 또는 랭크(「공격-1」「방어/특방+1」「전능력치+1」)
  static readonly Regex RankToken = new Regex(@"^(?<s>[^+\-]+)(?<d>[+\-])(?<n>\d)$");
  static bool TryRank(string body, out string stats, out string sign, out int amount) {
   stats = sign = ""; amount = 0;
   var m = RankToken.Match(Regex.Replace(body ?? "", @"\s+", ""));
   if (!m.Success) return false;
   stats = m.Groups["s"].Value; sign = m.Groups["d"].Value; amount = int.Parse(m.Groups["n"].Value);
   return MoveRuntime.Rank("자신", stats, sign, amount) != null && EffectModules.Classify(MoveRuntime.Rank("자신", stats, sign, amount)).Length > 0;
  }
  static bool Known(string body) { string a, b; int n; return TryRank(body, out a, out b, out n) || body.Split('/').All(x => KnownState(x.Trim())); }

  public static string Normalize(string text) {
   string t = Clean(text);
   if (!t.Contains("할")) return null;
   var m = Regex.Match(t, @"^(?:자신의|아군의)\s*(?<c>.*?)\s*(?<o>공격|기술)?\s*(?:에|에게)\s*(?<e>(?:「\s*\d+\s*할\s*[:：]\s*[^」]+」\s*,?\s*)+)\s*(?:의\s*추가\s*효과)?(?:를|을)\s*부여한다$");
   if (!m.Success) return null;
   string raw = Regex.Replace(m.Groups["c"].Value, "기술(?![^「]*」)", " ");
   MoveCondition c;
   if (!MoveCondition.TryParse(Regex.Replace(raw, @"\s+", " ").Trim(), out c)) return null;
   var effects = Regex.Matches(m.Groups["e"].Value, @"「\s*(\d+)\s*할\s*[:：]\s*([^」]+)」").Cast<Match>().Select(x => new { n = int.Parse(x.Groups[1].Value), s = Regex.Replace(x.Groups[2].Value, @"\s+", "") }).ToList();
   if (effects.Count == 0 || effects.Any(x => x.n <= 0 || x.n > 10 || !Known(x.s))) return null;
   bool attack = m.Groups["o"].Value == "공격" || c.Category == "공격";
   if (c.Category == "공격") c.Category = "";
   return "자신의 " + (c.Empty ? "" : c.Text + " ") + (attack ? "공격" : "기술") + "에 " + string.Concat(effects.Select(x => "「" + x.n + "할:" + x.s + "」")) + "의 추가효과를 부여한다.";
  }
  public static bool IsModule(string sentence) { return Pattern.IsMatch(Clean(sentence)); }

  // 「N할:X」 하나를 굴린다. self: null = 전부, true = 자신에게 거는 것(+랭크)만, false = 상대에게 거는 것만
  public static bool Roll(Fighter a, Fighter b, string token, string label, Rules rules, Func<double> rng, List<string> log, bool? self) {
   var m = Regex.Match(token ?? "", @"「\s*(?<n>\d+)\s*할\s*:\s*(?<x>[^」]+)」");
   if (!m.Success) return false;
   string body = Regex.Replace(m.Groups["x"].Value, @"\s+", "");
   string stats, sign; int amount;
   bool rank = TryRank(body, out stats, out sign, out amount);
   bool toSelf = rank && sign == "+";
   if (self.HasValue && self.Value != toSelf) return false;
   var target = toSelf ? a : b;
   if (a == null || target == null || target.HP <= 0) return false;
   rng = rng ?? (() => .5);
   int tenths = int.Parse(m.Groups["n"].Value);
   if (StateTicks.Rainbow(a, rules ?? new Rules())) tenths = Math.Min(10, tenths * 2);   // 무지개: 추가효과 확률 2배
   if (tenths < 10 && rng() >= tenths / 10.0) return false;
   log.Add(a.Data.name + "의 " + label + " 「" + tenths + "할:" + body + "」");
   if (rank) return MoveRuntime.Run(a, b, null, MoveRuntime.Rank(toSelf ? "자신" : "상대", stats, sign, amount), rules ?? new Rules(), rng, log) == EffectOutcome.Success;
   var choices = body.Split('/').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
   string state = choices[Math.Min(choices.Length - 1, (int)(rng() * choices.Length))];
   if (!KnownState(state)) { log.Add(label + ": 「" + state + "」 상태는 아직 자동 적용되지 않는다."); return false; }
   return StateEffects.Apply("상대를 「" + state + "」상태로 한다.", a, b, rules ?? new Rules(), rng, log, label) == EffectOutcome.Success;
  }

  // 공격이 맞은 뒤(타격마다) 호출
  public static void Apply(Fighter a, Fighter b, Move move, double typeEffect, Rules rules, Func<double> rng, List<string> log) {
   if (a == null || b == null || move == null || move.category == "변화" || b.HP <= 0 || a.HP <= 0) return;
   rng = rng ?? (() => .5);
   foreach (string line in PotentialModifiers.Sentences(a, b, rules ?? new Rules(), "추가효과")) {
    var m = Pattern.Match(Clean(line)); MoveCondition c;
    if (!m.Success || !MoveCondition.TryParse(m.Groups["c"].Value, out c) || !c.Matches(move, a, typeEffect)) continue;
    foreach (Match e in Regex.Matches(m.Groups["e"].Value, @"「\d+할:[^」]+」")) Roll(a, b, e.Value, "추가효과", rules, rng, log, null);
   }
  }
 }
}
