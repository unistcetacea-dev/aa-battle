using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 public static partial class AbilityPhrases {
  public const string MatchMoveType = "자신의 타입을 내보내는 기술의 타입으로 한다";
  public const string NoRecoil = "자신의 기술의 반동 대미지를 받지 않는다";
  static readonly Regex MoveType = new Regex(@"^자신의 (?<c>(?:「[^」]+」)*)\s*기술의 타입을 「(?<t>[^」]+)」(?:으로|로) 한다$");
  static readonly Regex WeatherLock = new Regex(@"^날씨를 「(?<w>큰가뭄|강한 비|난기류)」(?:으로|로) 고정한다$");
  static string RewriteRemaining(string s) {
   Match m;
   m = Regex.Match(s, @"^(?<s>자신|상대)의 〔특성〕을 〔(?<a>[^〕]+)〕(?:으로|로) 변경한다$");
   if (m.Success) return m.Groups["s"].Value + "의 특성을 「" + m.Groups["a"].Value + "」" + EffectModules.Ro(m.Groups["a"].Value) + " 한다";
   m = Regex.Match(s, @"^(?<n>헤롱헤롱이나 도발) 상태가 되지 않는다$");
   if (m.Success) return "자신은 「헤롱헤롱/도발」 상태가 되지 않는다";
   if (s == "자신이 기술을 내보낼 때,자신의 타입이 내보내는 기술과 같은 타입으로 변화한다" ||
       Regex.IsMatch(s, @"^자신이 기술을 내보낼 때,\s*자신의 타입이 내보내는 기술과 같은 타입으로 변화한다$")) return MatchMoveType;
   m = Regex.Match(s, @"^자신의\s*(?<c>(?:「[^」]+」\s*)*)\s*(?:타입\s*)?기술(?:을|이)\s*「(?<t>[^」]+)」\s*타입(?:으로|로|이)\s*(?:변경한다|된다)$");
   MoveCondition condition;
   if (m.Success && MoveCondition.TryParse(m.Groups["c"].Value, out condition) && MoveCondition.TypeNames.Contains(m.Groups["t"].Value))
    return "자신의 " + (condition.Empty ? "" : condition.Text + " ") + "기술의 타입을 「" + m.Groups["t"].Value + "」" + EffectModules.Ro(m.Groups["t"].Value) + " 한다";
   if (s == "자신의 기술의 반동 대미지를 무효화한다") return NoRecoil;
   return null;
  }
  static string ClassifyRemaining(string s) {
   if (s == MatchMoveType) return "기술 타입으로 변화";
   if (s == NoRecoil) return "기술 반동 무효";
   if (MoveType.IsMatch(s) && MoveCondition.TypeNames.Contains(MoveType.Match(s).Groups["t"].Value)) return "기술 타입 변경";
   if (WeatherLock.IsMatch(s)) return "날씨 고정";
   return "";
  }
  public static Move ChangeMoveType(Fighter a, Fighter b, Move move, Rules rules) {
   if (a == null || move == null) return move;
   foreach (string line in PotentialModifiers.Sentences(a, b, rules, "기술")) {
    var match = MoveType.Match(Clean(line)); MoveCondition condition;
    if (!match.Success || !MoveCondition.TryParse(match.Groups["c"].Value, out condition) || !condition.Matches(move, a, 1)) continue;
    move = move.Copy(); move.types = new[] { match.Groups["t"].Value };
    // 한 기술은 최초로 성립한 변경 모듈 하나로 판정한다.
    break;
   }
   return move;
  }
  public static void ChangeUserType(Fighter a, Fighter b, Move move, Rules rules, List<string> log) {
   if (!PotentialModifiers.Sentences(a, b, rules, "내보내는 기술의 타입").Any(x => Clean(x) == MatchMoveType)) return;
   if (a.Data.types.SequenceEqual(move.types)) return;
   if (a.Conditions.OriginalTypes == null) a.Conditions.OriginalTypes = a.Data.types.ToArray();
   a.Data.types = move.types.ToArray();
   log.Add(a.Data.name + ": 기술의 타입으로 변화 (" + string.Join("/", move.types) + ")");
  }
  public static bool UsesMoveType(Fighter a, Fighter b, Rules rules) {
   return PotentialModifiers.Sentences(a, b, rules, "내보내는 기술의 타입").Any(x => Clean(x) == MatchMoveType);
  }
  public static bool IgnoresMoveRecoil(Fighter a, Rules rules, Move move = null) {
   return PotentialModifiers.Sentences(a, null, rules, "반동").Any(x => Clean(x) == NoRecoil) || AbilityMoveModules.IgnoresRecoil(a, move, rules);
  }
  public static bool HasWeatherLock(Fighter a, Fighter b, Rules rules) {
   return PotentialModifiers.Sentences(a, b, rules, "고정", null, true).Concat(PotentialModifiers.Sentences(b, a, rules, "고정", null, true)).Any(x => WeatherLock.IsMatch(Clean(x)));
  }
  public static void RefreshWeatherLock(Fighter[] fighters, Rules rules, List<string> log) {
   var locks = new List<Tuple<string, string>>();
   foreach (int side in Enumerable.Range(0, 2))
    foreach (string line in PotentialModifiers.Sentences(fighters[side], fighters[1 - side], rules, "고정", null, true)) {
     var match = WeatherLock.Match(Clean(line));
     if (match.Success) locks.Add(Tuple.Create(Mechanics.MemberKey(fighters[side].Data) + ":" + match.Groups["w"].Value, match.Groups["w"].Value));
    }
   var changed = locks.Where(x => !rules.WeatherLockMembers.Contains(x.Item1)).ToList();
   if (changed.Count > 1) {
    var rng = EffectScope.Current == null ? (Func<double>)(() => .5) : EffectScope.Current.Rng;
    var order = Mechanics.SpeedOrder(fighters, rules, rng);
    changed = changed.OrderBy(x => Array.FindIndex(order, side => x.Item1.StartsWith(Mechanics.MemberKey(fighters[side].Data) + ":", StringComparison.Ordinal))).ToList();
   }
   var chosen = changed.LastOrDefault() ?? locks.FirstOrDefault(x => x.Item1 == rules.WeatherLockOwner) ?? locks.LastOrDefault();
   if (chosen != null) {
    if (rules.Weather != chosen.Item2) log.Add("날씨 고정: " + chosen.Item2);
    rules.Weather = chosen.Item2; rules.WeatherTurns = 0; rules.WeatherLockOwner = chosen.Item1;
   } else if (rules.WeatherLockOwner.Length > 0) {
    log.Add("날씨 고정이 풀렸다"); rules.Weather = "없음"; rules.WeatherTurns = 0; rules.WeatherLockOwner = "";
   }
   rules.WeatherLockMembers = locks.Select(x => x.Item1).ToArray();
  }
 }
}
