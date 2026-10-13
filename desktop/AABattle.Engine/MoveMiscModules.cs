using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 // 기술 설명에서 나온 효과 모듈과 상태(아픔나누기·소란피기·참기·날씨별 회복/랭크)
 //  체력 배분        필드의 전원의 현재 HP를 더한 뒤 똑같이 배분한다.        (홀수는 내림, 각자 최대 HP까지)
 //  날씨별 회복      자신의 체력을 날씨에 따라 최대 HP의 1/4·1/2·2/3 회복한다.  (악천후·없음·쾌청 순)
 //  날씨별 능력 상승 자신의 「X」이 1단계 오르고, 날씨가 「쾌청」일 때는 2단계 오른다.
 //  상태 「소란」     지속 T 동안 아무도 잠들지 않고 잠든 포켓몬은 깬다. 기술 소란피기는 같은 기술을 자동으로 낸다(난동부리기와 같은 행동 고정).
 //  상태 「참기」     N T 동안 받은 대미지를 모았다가 그 다음 행동에서 N배의 고정 대미지로 돌려준다. 모은 것이 없으면 실패.
 public static class MoveMiscModules {
  public const string PainSplit = "필드의 전원의 현재 HP를 더한 뒤 똑같이 배분한다";
  public const string KindPainSplit = "체력 배분";
  public const string KindWeatherHeal = "날씨별 회복";
  public const string KindWeatherRank = "날씨별 능력 상승";

  static readonly Regex WeatherHeal = new Regex(@"^자신의 체력을 날씨에 따라 최대 HP의 (?<bad>\d+/\d+)·(?<mid>\d+/\d+)·(?<sun>\d+/\d+) 회복한다$");
  static readonly Regex WeatherRank = new Regex(@"^자신의 「(?<s>[^」]+)」이 (?<a>[1-3])단계 오르고, 날씨가 「(?<w>[^」]+)」일 때는 (?<b>[1-3])단계 오른다$");
  // 기술 설명 원문
  static readonly Regex HealSource = new Regex(@"날씨에 따라 회복량이 변화한다\s*[.．]?\s*[(（]\s*(?<a>\d+/\d+)\s*[,，]\s*(?<b>\d+/\d+)\s*[,，]\s*(?<c>\d+/\d+)\s*[)）]");
  static readonly Regex RankSource = new Regex(@"^날씨가 「(?<w>[^」(]+)(?:\([^)]*\))?」일 때\s*,\s*「(?<s>[^」]+?)\+\s*(?<n>\d)」$");
  static readonly Regex PierceSource = new Regex(@"(?<n>(?:「[^」]+」)+)\s*상태[(（]기술[)）]를 관통한다");
  static readonly Regex WeatherPierceSource = new Regex(@"날씨가 「(?<w>[^」(（]+)(?:[(（][^)）]*[)）])?」일 때\s*[,，]\s*상대의 「방어」 기술을 관통한다");
  static readonly Regex UproarSource = new Regex(@"소란피워 공격한다");
  static readonly Regex BideSource = new Regex(@"(?<t>\d+)T\s*동안\s*상대로부터\s*받은\s*대미지의\s*(?<r>\d+(?:\.\d+)?)배만큼\s*고정\s*대미지를\s*준다");

  static string Clean(string s) { return Regex.Replace(PotentialParts.Clean(s ?? ""), @"\s+", " ").Trim().TrimEnd('.', '。', '．').Trim(); }

  // ===== 표준 문장 =====
  public static string Normalize(string text) {
   string s = Clean(text);
   return s == PainSplit || WeatherHeal.IsMatch(s) || WeatherRank.IsMatch(s) ? s + "." : null;
  }
  public static string Classify(string text) {
   string s = Clean(text);
   return s == PainSplit ? KindPainSplit : WeatherHeal.IsMatch(s) ? KindWeatherHeal : WeatherRank.IsMatch(s) ? KindWeatherRank : "";
  }

  // ===== 기술 설명 → 표준 문장(MoveRuntime.Read가 부른다). 읽었으면 true =====
  public static bool ReadMove(Move m, string s, List<string> into) {
   if (m == null) return false;
   if (s == PainSplit) { into.Add(PainSplit + "."); return true; }
   if (PierceSource.IsMatch(s)) return true;   // 방어·대타출동·매직코트 관통: PiercesState가 읽는다
   if (UproarSource.IsMatch(s) || s == "그 동안은 아무도 잠들지 않게 된다") return true;   // 소란: BeginUproar와 상태 「소란」
   var heal = HealSource.Match(m.effect ?? "");
   if (s == "날씨에 따라 회복량이 변화한다" && heal.Success) { into.Add("자신의 체력을 날씨에 따라 최대 HP의 " + heal.Groups["a"].Value + "·" + heal.Groups["b"].Value + "·" + heal.Groups["c"].Value + " 회복한다."); return true; }
   if (s == "HP를 회복한다" && (m.effect ?? "").Contains("날씨에 따라 회복량이")) return true;
   var rank = RankSource.Match(s);
   if (rank.Success && m.category == "변화") return MergeWeatherRank(rank, into);
   return false;
  }
  // 「자신의 X이 오른다」 + 「날씨가 W일 때, X+N」 → 하나의 날씨별 문장
  static bool MergeWeatherRank(Match rank, List<string> into) {
   string stats = rank.Groups["s"].Value.Trim();
   for (int i = 0; i < into.Count; i++) {
    var x = Regex.Match(into[i], @"^자신의 「" + Regex.Escape(stats) + @"」이 (?<d>매우 크게 |크게 )?오른다\.$");
    if (!x.Success) continue;
    int basic = x.Groups["d"].Value.StartsWith("매우") ? 3 : x.Groups["d"].Value.StartsWith("크게") ? 2 : 1;
    int boosted = int.Parse(rank.Groups["n"].Value);
    if (boosted < 1 || boosted > 3) return false;
    into[i] = "자신의 「" + stats + "」이 " + basic + "단계 오르고, 날씨가 「" + rank.Groups["w"].Value.Trim() + "」일 때는 " + boosted + "단계 오른다.";
    return true;
   }
   return false;
  }

  // ===== 날씨 =====
  static string ActiveWeather(Fighter a, Fighter b, Rules r) {
   string w = Mechanics.Weather(a, b ?? a, r);
   return MiscModules.IgnoresWeather(a, w, r) ? "없음" : w;
  }
  static bool WeatherIs(string actual, string wanted) {
   if (wanted == "쾌청") return Mechanics.Sunny(actual);
   if (wanted == "비") return Mechanics.Rain(actual);
   return actual == wanted;
  }
  static int Amount(int max, string fraction) {
   var part = fraction.Split('/');
   int n = int.Parse(part[0]), d = Math.Max(1, int.Parse(part[1]));
   return (int)Math.Floor(max * (double)n / d);
  }

  // ===== 방어·대타출동·매직코트 관통(기술 설명 「…상태(기술)를 관통한다」) =====
  public static bool PiercesState(Move m, string state) {
   if (m == null || string.IsNullOrEmpty(m.effect)) return false;
   foreach (Match x in PierceSource.Matches(m.effect))
    if (x.Groups["n"].Value.Contains("「" + state + "」")) return true;
   return false;
  }
  // 「날씨가 W일 때, 상대의 「방어」 기술을 관통한다」(마그마어퍼): 조건이 있으면 태그만으로 관통하지 않는다
  public static bool HasWeatherPierce(Move m) { return m != null && WeatherPierceSource.IsMatch(m.effect ?? ""); }
  public static bool WeatherPierces(Fighter a, Fighter b, Move m, Rules r) {
   if (m == null || a == null) return false;
   var x = WeatherPierceSource.Match(m.effect ?? "");
   return x.Success && WeatherIs(ActiveWeather(a, b, r ?? new Rules()), x.Groups["w"].Value.Trim());
  }

  // ===== 소란 =====
  public static bool IsUproar(Move m) { return m != null && UproarSource.IsMatch(m.effect ?? ""); }
  static bool UproarOn(Fighter f) { return f != null && f.HP > 0 && f.Conditions.UproarTurns > 0; }
  static Fighter FieldFoe(Fighter f) {
   var scope = EffectScope.Current;
   var current = scope == null ? null : scope.Opponent(f);
   return current ?? f.FieldOpponent;
  }
  // 소란이 이어지는 동안 양쪽 모두 잠들지 않는다(잠자기 포함)
  public static string UproarBlock(Fighter target) {
   return target != null && (UproarOn(target) || UproarOn(FieldFoe(target))) ? "소란: 잠들 수 없다" : "";
  }
  public static void WakeByUproar(Fighter f, List<string> log) {
   if (f == null || f.Status != "잠듦" || UproarBlock(f).Length == 0) return;
   f.Status = "정상";
   f.Conditions.SleepFromRest = false;
   if (log != null) log.Add(f.Data.name + ": 소란에 잠에서 깼다.");
  }
  static void Start(Fighter a, string moveName, int turns, double roll, List<string> log) {
   var c = a.Conditions;
   c.UproarTurns = turns > 0 ? turns : 2 + (int)(roll * 4);   // 2~5T
   c.UproarMove = moveName;
   if (log != null) log.Add(a.Data.name + ": 소란 " + c.UproarTurns + "T");
   WakeByUproar(a, log);
   WakeByUproar(FieldFoe(a), log);
  }
  // 기술을 내기 시작할 때(난동부리기와 같은 시점)
  public static void BeginUproar(Fighter a, Fighter b, Move m, Func<double> rng, List<string> log) {
   if (a == null || !IsUproar(m) || a.Conditions.UproarTurns > 0) return;
   Start(a, m.name, 0, rng == null ? .5 : rng(), log);
  }
  // 상태 「소란」을 문장으로 걸 때(자동 행동은 없다)
  public static bool GrantUproar(Fighter target, int turns, double roll, List<string> log) {
   if (target == null || target.HP <= 0) return false;
   Start(target, "", turns, roll, log);
   return true;
  }

  // ===== 참기 =====
  public static bool IsBide(Move m) { return BideSource.IsMatch(m == null ? "" : m.effect ?? ""); }
  public static bool BideInfo(Move m, out int span, out double rate) {
   span = 0; rate = 0;
   var x = BideSource.Match(m == null ? "" : m.effect ?? "");
   if (!x.Success) return false;
   span = int.Parse(x.Groups["t"].Value);
   rate = double.Parse(x.Groups["r"].Value, System.Globalization.CultureInfo.InvariantCulture);
   return true;
  }
  // 모은 대미지의 N배(고정 대미지). 모은 것이 없으면 0
  public static int BideDamage(Fighter a, Move m) {
   int span; double rate;
   if (a == null || !BideInfo(m, out span, out rate)) return -1;
   return (int)Math.Floor(a.Conditions.BideStored * rate);
  }
  // 받은 대미지를 모은다(기다리는 동안만)
  public static void Store(Fighter target, int damage) {
   if (target == null || damage <= 0) return;
   var c = target.Conditions;
   if (c.BideMove.Length > 0 && c.BideTurns > 0) c.BideStored += damage;
  }
  // 참기 기술의 행동: 처음에는 모으기 시작, 기다리는 동안은 행동이 여기서 끝난다(true). 마지막 행동은 false(공격 경로)
  public static bool Bide(Fighter a, Fighter b, Move m, List<string> log) {
   int span; double rate;
   if (a == null || !BideInfo(m, out span, out rate)) return false;
   var c = a.Conditions;
   if (c.BideMove.Length == 0) {
    c.BideMove = m.name;
    c.BideTurns = span;
    c.BideStored = 0;
    log.Add(a.Data.name + ": " + m.name + " — " + span + "T 동안 받는 대미지를 모은다");
    return true;
   }
   if (c.BideTurns > 0 && --c.BideTurns > 0) {
    log.Add(a.Data.name + ": " + m.name + " — 참고 있다 (모은 대미지 " + c.BideStored + ")");
    return true;
   }
   if (c.BideStored <= 0) {
    log.Add(a.Data.name + "의 " + m.name + " — 실패했다: 받은 대미지가 없다");
    ClearBide(a);
    return true;
   }
   log.Add(a.Data.name + ": " + m.name + " — 모은 대미지 " + c.BideStored + "의 " + rate.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "배를 돌려준다");
   return false;
  }
  static void ClearBide(Fighter a) {
   a.Conditions.BideMove = "";
   a.Conditions.BideTurns = 0;
   a.Conditions.BideStored = 0;
  }
  // 상태 「참기」를 문장으로 걸 때(기술 참기의 자동 행동으로 이어진다)
  public static bool GrantBide(Fighter target, int turns, List<string> log) {
   if (target == null || target.HP <= 0) return false;
   var c = target.Conditions;
   c.BideMove = "참기";
   c.BideTurns = turns > 0 ? turns : 2;
   c.BideStored = 0;
   if (log != null) log.Add(target.Data.name + ": 참기 " + c.BideTurns + "T");
   return true;
  }

  // ===== 자동 행동(행동 고정) =====
  public static string AutoMove(Fighter a) {
   var c = a.Conditions;
   if (c.UproarTurns > 0 && c.UproarMove.Length > 0) return c.UproarMove;
   return c.BideMove;
  }
  public static string AutoLabel(Fighter a) { return a.Conditions.UproarTurns > 0 && a.Conditions.UproarMove.Length > 0 ? "소란" : "참기"; }

  // 기술 하나를 끝낸 뒤(AfterMove)
  public static void AfterMove(Fighter a, Move m, ConditionState used, List<string> log) {
   var c = a.Conditions;
   if (used.UproarTurns > 0 && used.UproarMove.Length > 0 && c.UproarTurns > 0 && --c.UproarTurns == 0) {
    c.UproarMove = "";
    log.Add(a.Data.name + ": 소란이 가라앉았다.");
   }
   if (c.BideMove.Length > 0 && c.BideTurns <= 0 && IsBide(m)) ClearBide(a);
  }
  // 행동하지 못했을 때: 소란은 끝나고, 참기는 한 번 센다
  public static void ActionFailed(Fighter a, List<string> log) {
   var c = a.Conditions;
   if (c.UproarTurns > 0 && c.UproarMove.Length > 0) {
    c.UproarTurns = 0;
    c.UproarMove = "";
    log.Add(a.Data.name + ": 소란이 가라앉았다.");
   }
   if (c.BideMove.Length > 0 && (c.BideTurns <= 0 || --c.BideTurns <= 0)) {
    ClearBide(a);
    log.Add(a.Data.name + ": 참기가 풀렸다.");
   }
  }
  // T 종료: 문장으로 건 소란(자동 행동 없음)은 여기서 센다
  public static void Tick(Fighter f, List<string> log) {
   var c = f.Conditions;
   if (c.UproarTurns > 0 && c.UproarMove.Length == 0 && --c.UproarTurns == 0) log.Add(f.Data.name + ": 소란이 가라앉았다.");
  }
  public static void Leave(Fighter f) {
   f.Conditions.UproarTurns = 0;
   f.Conditions.UproarMove = "";
   ClearBide(f);
  }

  // ===== 실행기 =====
  public static void Register() {
   PotentialExecutors.Register(KindPainSplit, c => Clean(c.Line) == PainSplit, SplitPain);
   PotentialExecutors.Register(KindWeatherHeal, c => WeatherHeal.IsMatch(Clean(c.Line)), HealByWeather);
   PotentialExecutors.Register(KindWeatherRank, c => WeatherRank.IsMatch(Clean(c.Line)), RaiseByWeather);
  }

  static EffectOutcome SplitPain(EffectCall call) {
   var a = call.Owner;
   var b = call.Opponent;
   if (a == null || b == null || a.HP <= 0 || b.HP <= 0) return EffectOutcome.Failed;
   var rules = call.Rules ?? new Rules();
   int each = (a.HP + b.HP) / 2;   // 홀수의 나머지 1은 버린다
   int aBefore = a.HP, bBefore = b.HP;
   a.HP = Math.Max(1, Math.Min(each, Mechanics.MaxHP(a, b, rules)));
   b.HP = Math.Max(1, Math.Min(each, Mechanics.MaxHP(b, a, rules)));
   call.Say(a.Data.name + " " + aBefore + " → " + a.HP + " / " + b.Data.name + " " + bBefore + " → " + b.HP + " (체력을 나눴다)");
   return EffectOutcome.Success;
  }

  static EffectOutcome HealByWeather(EffectCall call) {
   var a = call.Owner;
   if (a == null || a.HP <= 0) return EffectOutcome.Failed;
   var x = WeatherHeal.Match(Clean(call.Line));
   var rules = call.Rules ?? new Rules();
   var foe = call.Opponent ?? a;
   string weather = ActiveWeather(a, foe, rules);
   string group = Mechanics.Sunny(weather) ? "sun" : weather == "없음" || weather == "난기류" ? "mid" : "bad";
   int amount = Amount(Mechanics.MaxHP(a, foe, rules), x.Groups[group].Value);
   int gain = Mechanics.Heal(a, foe, rules, amount, call.Source.Name, call.Log);
   return gain > 0 ? EffectOutcome.Success : EffectOutcome.Failed;
  }

  static EffectOutcome RaiseByWeather(EffectCall call) {
   var x = WeatherRank.Match(Clean(call.Line));
   var rules = call.Rules ?? new Rules();
   string weather = ActiveWeather(call.Owner, call.Opponent, rules);
   int steps = int.Parse(WeatherIs(weather, x.Groups["w"].Value.Trim()) ? x.Groups["b"].Value : x.Groups["a"].Value);
   string line = EffectNormalizer.Normalize(MoveRuntime.Rank("자신", x.Groups["s"].Value, "+", steps) ?? "");
   var inner = new EffectCall { Source = call.Source, Event = call.Event, Line = line, Rules = call.Rules, Rng = call.Rng, Log = call.Log };
   var executor = PotentialExecutors.Find(inner);
   return executor == null ? EffectOutcome.Pending : executor.Run(inner);
  }
 }
}
