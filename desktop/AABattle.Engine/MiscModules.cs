using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 // 소지품 정리에서 나온 효과 모듈(포텐셜에도 그대로 쓴다)
 //  급소 보정   자신의 「조건」 기술의 급소 랭크를 +N한다.            (계속)
 //  지속 연장   자신이 전개하는 「쾌청」…의 지속 T를 +N한다.          (계속, 날씨·지형·벽)
 //  날씨 무시   자신은 「쾌청」「비」의 영향을 받지 않는다.            (계속, 위력 보정을 받지 않는다)
 //  우선도 -0.5 자신의 기술의 우선도를 -0.5한다.                     (계속, 같은 우선도끼리에서만 나중)
 //  특성 변경   자신/상대의 특성을 「X」로 한다.                       (사건, 기간 가능, 교대하면 원래대로)
 //  소지품 소실 자신의 소지품이 부서진다.                             (사건)
 public static class MiscModules {
  static readonly Regex Crit = new Regex(@"^자신의 (?<c>(?:「[^」]+」)*)\s*기술의 급소 랭크를 \+(?<n>\d)한다$");
  static readonly Regex Extend = new Regex(@"^자신이 전개하는 (?<n>(?:「[^」]+」)+)의 지속 T를 \+(?<v>\d+)한다$");
  static readonly Regex Ignore = new Regex(@"^자신은 (?<n>(?:「[^」]+」)+)의 영향을 받지 않는다$");
  static readonly Regex Ability = new Regex(@"^(?<s>자신|상대)의 특성을 「(?<a>[^」]+)」(?:으로|로) (?:한다|할 수 있다)$");
  const string Lag = "자신의 기술의 우선도를 -0.5한다";
  const string Break = "자신의 소지품이 부서진다";
  static readonly string[] Screens = { "리플렉터", "빛의장막" };

  static string Clean(string text) { return Regex.Replace(PotentialParts.Clean(text ?? ""), @"\s+", " ").Trim().TrimEnd('.', '。', '．').Trim(); }
  static string[] Names(string raw) { return Regex.Matches(raw ?? "", "「([^」]+)」").Cast<Match>().Select(m => m.Groups[1].Value.Trim()).Where(x => x.Length > 0).Distinct().ToArray(); }
  static bool Cond(string raw, out MoveCondition c) {
   string t = Regex.Replace(raw ?? "", @"(?:으로|로)\s*내보내는", " ");
   t = Regex.Replace(t, "기술(?![^「]*」)", " ");
   t = Regex.Replace(t, @"(?<=^|\s|」)(?:이|가|의|등의)(?=\s|$|「)", " ");
   return MoveCondition.TryParse(Regex.Replace(t, @"\s+", " ").Trim(), out c);
  }
  static string CondText(MoveCondition c) { return c.Empty ? "" : c.Text + " "; }

  public static string Normalize(string text) {
   string t = Clean(text);
   if (t.Length == 0) return null;
   Match m; MoveCondition c;
   m = Regex.Match(t, @"^(?:자신의\s*|소지한\s*포켓몬의\s*)?(?<c>.*?)\s*기술이\s*급소에\s*맞기\s*(?:쉬워진다|쉽다)\.?\s*[(（]\s*C\s*\+\s*(?<n>\d)\s*[)）]$");
   if (m.Success && Cond(m.Groups["c"].Value, out c)) return "자신의 " + CondText(c) + "기술의 급소 랭크를 +" + m.Groups["n"].Value + "한다.";
   m = Regex.Match(t, @"^자신의\s*(?<c>.*?)\s*기술의\s*급소\s*랭크를\s*\+(?<n>\d)\s*한다$");
   if (m.Success && Cond(m.Groups["c"].Value, out c)) return "자신의 " + CondText(c) + "기술의 급소 랭크를 +" + m.Groups["n"].Value + "한다.";
   m = Regex.Match(t, @"^(?:자신의\s*기술,\s*특성에\s*의한|자신이\s*전개하는)\s*(?<n>(?:「[^」]+」\s*)+)\s*(?:상태)?(?:등의\s*기술)?의\s*지속\s*T\s*수?를\s*\+(?<v>\d+)\s*한다$");
   if (m.Success) {
    var names = Names(m.Groups["n"].Value).SelectMany(x => x == "필드" ? EffectModules.TerrainNames : x == "리플렉터" && !m.Groups["n"].Value.Contains("빛의장막") && t.Contains("등의") ? Screens : new[] { x }).Distinct().ToArray();
    if (names.All(Extendable)) return "자신이 전개하는 " + string.Concat(names.Select(x => "「" + x + "」")) + "의 지속 T를 +" + m.Groups["v"].Value + "한다.";
   }
   m = Regex.Match(t, @"^(?:자신은\s*)?(?<n>(?:「[^」]+」\s*,?\s*)+|쾌청,\s*비)\s*의\s*영향을\s*받지\s*않는다$");
   if (m.Success) {
    var names = m.Groups["n"].Value.Contains("「") ? Names(m.Groups["n"].Value) : new[] { "쾌청", "비" };
    if (names.All(x => Mechanics.Weathers.Contains(x) && x != "없음")) return "자신은 " + string.Concat(names.Select(x => "「" + x + "」")) + "의 영향을 받지 않는다.";
   }
   if (Regex.IsMatch(t, @"^(?:자신의\s*기술의\s*)?우선도를\s*-\s*0\.5\s*한다$")) return Lag + ".";
   m = Regex.Match(t, @"^(?<s>자진의\s*포켓몬|자신|상대)의\s*특성을\s*「(?<a>[^」]+)」\s*(?:으로|로)\s*(?<v>변경한다|변화시킨다|바꾼다|한다|변경할\s*수\s*있다|할\s*수\s*있다)$");
   if (m.Success) { string s = m.Groups["s"].Value.StartsWith("상대") ? "상대" : "자신"; string a = m.Groups["a"].Value.Trim(); return s + "의 특성을 「" + a + "」" + EffectModules.Ro(a) + (m.Groups["v"].Value.Contains("있다") ? " 할 수 있다." : " 한다."); }
   if (Regex.IsMatch(t, @"^(?:자신의\s*소지품이\s*|소지품이\s*)?부서진다$")) return Break + ".";
   return null;
  }
  static bool Extendable(string x) { return Mechanics.Weathers.Contains(x) && x != "없음" || EffectModules.TerrainNames.Contains(x) || Screens.Contains(x); }

  public static string Classify(string sentence) {
   string s = Clean(sentence);
   if (Crit.IsMatch(s)) return "급소 보정";
   if (Extend.IsMatch(s)) return "지속 연장";
   if (Ignore.IsMatch(s)) return "날씨 무시";
   if (s == Lag) return "우선도 -0.5";
   if (Ability.IsMatch(s)) return "특성 변경";
   if (s == Break) return "소지품 소실";
   return "";
  }
  public static bool TryAbility(string text, out string subject, out string ability) {
   var m = Ability.Match(Clean(text));
   subject = m.Success ? m.Groups["s"].Value : ""; ability = m.Success ? m.Groups["a"].Value : "";
   return m.Success;
  }
  public static bool IsBreak(string text) { return Clean(text) == Break; }

  // ===== 계속 효과 판정 =====
  static IEnumerable<string> Lines(Fighter f, Fighter other, Rules rules, string keyword) { return f == null ? new string[0] : PotentialModifiers.Sentences(f, other, rules ?? new Rules(), keyword).Select(Clean); }
  public static int CritBonus(Fighter a, Fighter b, Move move, Rules rules) {
   int total = 0;
   foreach (string line in Lines(a, b, rules, "급소")) {
    var m = Crit.Match(line); MoveCondition c;
    if (m.Success && MoveCondition.TryParse(m.Groups["c"].Value, out c) && c.Matches(move, a, 1)) total += int.Parse(m.Groups["n"].Value);
   }
   return total;
  }
  public static int ExtendTurns(Fighter source, string what, int turns) {
   if (source == null || turns <= 0) return turns;
   foreach (string line in Lines(source, null, null, "지속")) {
    var m = Extend.Match(line);
    if (m.Success && Names(m.Groups["n"].Value).Contains(what)) turns += int.Parse(m.Groups["v"].Value);
   }
   return turns;
  }
  public static bool IgnoresWeather(Fighter f, string weather, Rules rules) {
   string family = Mechanics.Sunny(weather) ? "쾌청" : Mechanics.Rain(weather) ? "비" : weather;
   return Lines(f, null, rules, "영향").Any(line => { var m = Ignore.Match(line); return m.Success && Names(m.Groups["n"].Value).Contains(family); });
  }
  public static bool Lags(Fighter f, Fighter other, Rules rules, Move move = null) {
   if (f == null || f.HP <= 0) return false;
   return Lines(f, other, rules, "우선도").Any(x => x == Lag || move != null && AbilityMoveModules.LagsMove(x, f, move));
  }

  // ===== 특성 변경(교대하면·기간이 끝나면 원래대로) =====
  public static bool ChangeAbility(Fighter target, string ability, int turns, List<string> log, string source, bool refresh = true) {
   if (target == null || target.HP <= 0 || AbilityRuntime.Name(target) == (ability ?? "")) return false;
   if (AbilityChangeModules.CannotChange(target)) {
    if (log != null) log.Add(target.Data.name + ": 특성은 변화되지 않는다 (" + source + ")");
    return false;
   }
   var c = target.Conditions;
   if (c.AbilityBefore == null) c.AbilityBefore = AbilityRuntime.Name(target);
   target.CurrentAbility = ability; target.AbilityChanged = true;
   AbilityModules.Sync(target);
   c.AbilityTurns = turns;
   if (log != null) log.Add(target.Data.name + ": 특성이 「" + ability + "」" + EffectModules.Ro(ability) + " 바뀌었다 (" + source + ")");
   if (refresh) AbilityChangeModules.Refresh(log);
   return true;
  }
  public static void RestoreAbility(Fighter target, List<string> log) {
   var c = target.Conditions;
   if (c.AbilityBefore == null) return;
   target.CurrentAbility = c.AbilityBefore; target.AbilityChanged = c.AbilityBefore != (target.Data.ability ?? "");
   c.AbilityBefore = null; c.AbilityTurns = 0;
   AbilityModules.Sync(target);
   if (log != null) log.Add(target.Data.name + ": 특성이 「" + AbilityRuntime.Name(target) + "」" + EffectModules.Ro(AbilityRuntime.Name(target)) + " 돌아왔다");
  }
  public static void TickAbility(Fighter f, List<string> log) {
   if (f == null || f.Conditions.AbilityTurns <= 0) return;
   if (--f.Conditions.AbilityTurns == 0) RestoreAbility(f, log);
  }
 }
}
