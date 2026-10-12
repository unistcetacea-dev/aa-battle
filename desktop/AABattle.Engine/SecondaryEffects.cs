using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 // 추가효과 모듈: 「자신의 「조건」 기술에 「N할:상태」의 추가효과를 부여한다」(계속 효과).
 // 공격이 맞으면(대타에 막히지 않고 상대가 남아 있으면) 타격마다 그 확률로 상대에게 상태를 건다.
 // 풀죽음은 행동권이 없는 상대에게도 걸린다(사용자 결정: 의미가 없을 뿐, 턴 끝에 풀린다)
 public static class SecondaryEffects {
  static readonly Regex Pattern = new Regex(@"^자신의 (?<c>(?:「[^」]+」)*)\s*(?<o>공격|기술)에 (?<e>(?:「\d+할:[^」]+」)+)의 추가효과를 부여한다$");

  static string Clean(string text) { return Regex.Replace(PotentialParts.Clean(text ?? ""), @"\s+", " ").Trim().TrimEnd('.', '。', '．').Trim(); }
  static bool KnownState(string name) { return Mechanics.Statuses.Contains(name) && name != "정상" || StateEffects.Find(name) != null; }

  public static string Normalize(string text) {
   string t = Clean(text);
   if (!t.Contains("할")) return null;
   var m = Regex.Match(t, @"^(?:자신의|아군의)\s*(?<c>.*?)\s*(?<o>공격|기술)?\s*(?:에|에게)\s*(?<e>(?:「\s*\d+\s*할\s*[:：]\s*[^」]+」\s*,?\s*)+)\s*(?:의\s*추가\s*효과)?(?:를|을)\s*부여한다$");
   if (!m.Success) return null;
   string raw = Regex.Replace(m.Groups["c"].Value, "기술(?![^「]*」)", " ");
   MoveCondition c;
   if (!MoveCondition.TryParse(Regex.Replace(raw, @"\s+", " ").Trim(), out c)) return null;
   var effects = Regex.Matches(m.Groups["e"].Value, @"「\s*(\d+)\s*할\s*[:：]\s*([^」]+)」").Cast<Match>().Select(x => new { n = int.Parse(x.Groups[1].Value), s = x.Groups[2].Value.Trim() }).ToList();
   if (effects.Count == 0 || effects.Any(x => x.n <= 0 || x.n > 10 || !KnownState(x.s))) return null;
   bool attack = m.Groups["o"].Value == "공격" || c.Category == "공격";
   if (c.Category == "공격") c.Category = "";
   return "자신의 " + (c.Empty ? "" : c.Text + " ") + (attack ? "공격" : "기술") + "에 " + string.Concat(effects.Select(x => "「" + x.n + "할:" + x.s + "」")) + "의 추가효과를 부여한다.";
  }
  public static bool IsModule(string sentence) { return Pattern.IsMatch(Clean(sentence)); }

  // 공격이 맞은 뒤(타격마다) 호출
  public static void Apply(Fighter a, Fighter b, Move move, double typeEffect, Rules rules, Func<double> rng, List<string> log) {
   if (a == null || b == null || move == null || move.category == "변화" || b.HP <= 0 || a.HP <= 0) return;
   rng = rng ?? (() => .5);
   foreach (string line in PotentialModifiers.Sentences(a, b, rules ?? new Rules(), "추가효과")) {
    var m = Pattern.Match(Clean(line)); MoveCondition c;
    if (!m.Success || !MoveCondition.TryParse(m.Groups["c"].Value, out c) || !c.Matches(move, a, typeEffect)) continue;
    foreach (Match e in Regex.Matches(m.Groups["e"].Value, @"「(\d+)할:([^」]+)」")) {
     string state = e.Groups[2].Value;
     if (rng() >= int.Parse(e.Groups[1].Value) / 10.0) continue;
     log.Add(a.Data.name + "의 추가효과 「" + e.Groups[1].Value + "할:" + state + "」");
     StateEffects.Apply("상대를 「" + state + "」상태로 한다.", a, b, rules ?? new Rules(), rng, log, "추가효과");
    }
   }
  }
 }
}
