using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 // 기술 효과 모듈: 기술 설명 문장(Move.effect)과 태그에서 읽는다
 //  드레인   「준 대미지의 N/M만큼 HP를 회복한다」(타격마다, 대타에 준 대미지 포함)
 //  조이기   「N～M T 동안 상대를 「조이기」 상태로 한다」(기간이 없으면 4～5T)
 //  모으기   태그 「모으기」: 1T째 준비(「X」로 모습을 감춘다 / 1T째 능력 상승), 2T째 자동으로 공격
 //  숨기     「하늘 위」「땅속」「물속」「불꽃속」「어둠속」: 닿는 기술만 맞는다(위력 2배는 기술 문장 「상대가 「X」 상태일 때」)
 //  급소     「급소에 맞기 쉽다(C+N)」 · 「반드시 급소에 맞는다」(태그 「확정급소」)
 //  방어 관통 「상대의 「방어」 기술을 관통한다」(태그 「방어관통」)
 //  필중     「날씨가 「비」일 때, 「필중」 효과가 부여된다」 · 「「독」 타입이 내보내면 「필중」 효과가 부여된다」
 //  조건 위력 「상대가 「X」 상태(도중·중)일 때, 위력이 강화(N배)된다」
 //  실패     꿈먹기(잠듦이 아니면) · 수상한촉수(장소에 나온 첫 T가 아니면)
 // 소지품·포텐셜용 계속 효과(표준 문장)
 //  드레인 강화      자신의 「드레인」 기술의 회복량을 N배로 한다.
 //  조이기 지속      자신이 거는 「조이기」의 지속 T를 NT로 한다.
 //  조이기 대미지    자신이 거는 「조이기」의 대미지를 최대 HP의 1/N로 한다.
 //  모으기 생략      자신의 「모으기」 기술의 모으기를 생략한다.
 //  연속 사용 강화   자신이 같은 기술을 연속으로 내보낼 때마다 그 기술의 위력을 N배씩 강화한다(최대 M배).
 //  접촉 반응 무시   자신의 「접촉」 기술에 반응하는 효과를 받지 않는다.
 // 사건 효과(표준 문장)
 //  능력 하락 복구   자신의 내려간 능력을 원래대로 되돌린다.
 //  다음 T 우선도    다음 T 자신의 기술 우선도를 N 올린다.
 //  소지품 이동      자신의 소지품을 상대에게 옮긴다.
 public static class MoveModules {
  static readonly Regex DrainBoost = new Regex(@"^자신의 「드레인」 기술의 회복량을 (?<n>\d+(?:\.\d+)?)배로 한다$");
  static readonly Regex BindTurns = new Regex(@"^자신이 거는 「조이기」의 지속 T를 (?<n>\d+)T로 한다$");
  static readonly Regex BindDamage = new Regex(@"^자신이 거는 「조이기」의 대미지를 최대 HP의 1/(?<n>\d+)로 한다$");
  static readonly Regex Repeat = new Regex(@"^자신이 같은 기술을 연속으로 내보낼 때마다 그 기술의 위력을 (?<s>\d+(?:\.\d+)?)배씩 강화한다\(최대 (?<m>\d+(?:\.\d+)?)배\)$");
  static readonly Regex NextPriority = new Regex(@"^다음 T 자신의 기술 우선도를 (?<n>[1-7]) 올린다$");
  const string SkipCharge = "자신의 「모으기」 기술의 모으기를 생략한다";
  const string ContactGuard = "자신의 「접촉」 기술에 반응하는 효과를 받지 않는다";
  const string RestoreDrops = "자신의 내려간 능력을 원래대로 되돌린다";
  const string GiveItem = "자신의 소지품을 상대에게 옮긴다";

  // 숨는 곳 → 닿는 기술(값은 위력 배율)
  static readonly Dictionary<string, Dictionary<string, double>> Reach = new Dictionary<string, Dictionary<string, double>> {
   { "하늘 위", new Dictionary<string, double> { { "번개", 1 }, { "폭풍", 1 }, { "회오리", 1 }, { "용오름", 1 }, { "스카이업퍼", 1 }, { "사우전드애로우", 1 }, { "떨어뜨리기", 1 }, { "프리폴", 1 } } },
   { "땅속", new Dictionary<string, double> { { "지진", 1 }, { "매그니튜드", 1 }, { "땅가르기", 1 } } },
   { "물속", new Dictionary<string, double> { { "파도타기", 1 }, { "바다회오리", 1 } } },
   { "불꽃속", new Dictionary<string, double>() },
   { "어둠속", new Dictionary<string, double>() },
   { "숨음", new Dictionary<string, double>() }
  };
  public static IEnumerable<string> HidePlaces { get { return Reach.Keys; } }

  static string Clean(string text) { return Regex.Replace(PotentialParts.Clean(text ?? ""), @"\s+", " ").Trim().TrimEnd('.', '。', '．').Trim(); }
  // 기술 설명은 전각 문장부호를 쓴다
  static string Plain(Move m) {
   string t = m == null ? "" : m.effect ?? "";
   t = t.Replace('（', '(').Replace('）', ')').Replace('，', ',').Replace('．', '.').Replace('＋', '+').Replace('：', ':').Replace('～', '~');
   return Regex.Replace(t, @"\s+", " ");
  }
  static IEnumerable<string> Parts(Move m) { return Plain(m).Split('.').Select(x => x.Trim()).Where(x => x.Length > 0); }
  static bool Tagged(Move m, string tag) { return m != null && (m.tags ?? new string[0]).Contains(tag); }

  // ===== 표준 문장 =====
  public static string Normalize(string text) {
   string t = Clean(text);
   if (t.Length == 0) return null;
   Match m;
   if (DrainBoost.IsMatch(t) || BindTurns.IsMatch(t) || BindDamage.IsMatch(t) || Repeat.IsMatch(t) || NextPriority.IsMatch(t)) return t + ".";
   if (t == SkipCharge || t == ContactGuard || t == RestoreDrops || t == GiveItem) return t + ".";
   // 수집 원문(아바돈 소지품)
   m = Regex.Match(t, @"^체력을\s*흡수하는\s*기술의\s*회복량이\s*(?<p>\d+)\s*%\s*증가한다$");
   if (m.Success) return "자신의 「드레인」 기술의 회복량을 " + (1 + int.Parse(m.Groups["p"].Value) / 100.0).ToString("0.##") + "배로 한다.";
   m = Regex.Match(t, @"^(?:기술로\s*발생하는\s*)?「조이기」\s*상태의\s*지속\s*시간이\s*언제나\s*(?<n>\d+)\s*T\s*(?:이|가)\s*된다$");
   if (m.Success) return "자신이 거는 「조이기」의 지속 T를 " + m.Groups["n"].Value + "T로 한다.";
   m = Regex.Match(t, @"^「조이기」\s*상태에\s*의한\s*(?:데미지|대미지)가\s*「?1/(?<n>\d+)」?\s*(?:으로|로)\s*증가한다$");
   if (m.Success) return "자신이 거는 「조이기」의 대미지를 최대 HP의 1/" + m.Groups["n"].Value + "로 한다.";
   m = Regex.Match(t, @"^이전\s*턴과\s*같은\s*기술을\s*내보낸\s*횟수에\s*비례해서\s*기술의\s*위력이\s*강화된다\.?\s*\(\s*(?<a>\d+(?:\.\d+)?)\s*[～~]\s*(?<b>\d+(?:\.\d+)?)\s*배\s*\)$");
   if (m.Success) return "자신이 같은 기술을 연속으로 내보낼 때마다 그 기술의 위력을 " + (double.Parse(m.Groups["a"].Value) - 1).ToString("0.##") + "배씩 강화한다(최대 " + m.Groups["b"].Value + "배).";
   if (Regex.IsMatch(t, @"^(?:자신의\s*)?「접촉\s*기술」에\s*반응하는\s*효과를\s*받지\s*않는다$")) return ContactGuard + ".";
   if (Regex.IsMatch(t, @"^(?:자신의\s*)?「모으기\s*기술」의\s*모으기를\s*생략한다$")) return SkipCharge + ".";
   if (Regex.IsMatch(t, @"^(?:자신의\s*)?능력의\s*감소를\s*원래대로\s*되돌린다$")) return RestoreDrops + ".";
   m = Regex.Match(t, @"^다음\s*T\s*(?:자신이\s*)?내보내는\s*기술의\s*우선도를\s*\+(?<n>[1-7])\s*한다$");
   if (m.Success) return "다음 T 자신의 기술 우선도를 " + m.Groups["n"].Value + " 올린다.";
   return null;
  }
  public static string Classify(string sentence) {
   string s = Clean(sentence);
   if (DrainBoost.IsMatch(s)) return "드레인 강화";
   if (BindTurns.IsMatch(s)) return "조이기 지속";
   if (BindDamage.IsMatch(s)) return "조이기 대미지";
   if (Repeat.IsMatch(s)) return "연속 사용 강화";
   if (NextPriority.IsMatch(s)) return "다음 T 우선도";
   if (s == SkipCharge) return "모으기 생략";
   if (s == ContactGuard) return "접촉 반응 무시";
   if (s == RestoreDrops) return "능력 하락 복구";
   if (s == GiveItem) return "소지품 이동";
   return "";
  }

  static IEnumerable<string> Lines(Fighter f, Fighter other, string keyword) {
   if (f == null) return new string[0];
   var rules = EffectScope.Current == null ? new Rules() : EffectScope.Current.Rules;
   return PotentialModifiers.Sentences(f, other, rules, keyword).Select(Clean);
  }

  // ===== 드레인 =====
  public static bool TryDrain(Move m, out int top, out int bottom) {
   top = bottom = 0;
   var x = Regex.Match(Plain(m), @"준 대미지의 (\d+)/(\d+)만큼 HP를 회복한다");
   if (!x.Success) return false;
   top = int.Parse(x.Groups[1].Value); bottom = int.Parse(x.Groups[2].Value);
   return bottom > 0;
  }
  public static void Drain(Fighter a, Fighter b, Move m, Rules r, int dealt, List<string> log) {
   int top, bottom;
   if (a == null || a.HP <= 0 || dealt <= 0 || !TryDrain(m, out top, out bottom)) return;
   double boost = 1;
   foreach (string line in Lines(a, b, "회복량")) { var x = DrainBoost.Match(line); if (x.Success) boost *= double.Parse(x.Groups["n"].Value); }
   int amount = Math.Max(1, (int)Math.Floor(dealt * top / (double)bottom * boost + 1e-9));
   int gain = Mechanics.Heal(a, b, r, amount, m.name + " 흡수" + (boost != 1 ? " ×" + boost : ""), log);
   if (gain > 0) AnsweredTriggers.Drained(a, b, dealt, gain, log.Add);
  }

  // ===== 조이기 =====
  public static bool TryBind(Move m, out int min, out int max) {
   min = max = 0;
   string t = Plain(m);
   if (!Tagged(m, "조이기") || !t.Contains("「조이기」 상태로 한다")) return false;
   var x = Regex.Match(t, @"(\d+)\s*~\s*(\d+)\s*T");
   if (x.Success) { min = int.Parse(x.Groups[1].Value); max = int.Parse(x.Groups[2].Value); return true; }
   x = Regex.Match(t, @"(\d+)\s*T 동안");
   if (x.Success) { min = max = int.Parse(x.Groups[1].Value); return true; }
   min = 4; max = 5;
   return true;
  }
  public static void Bind(Fighter a, Fighter b, Move m, Rules r, Func<double> rng, List<string> log) {
   int min, max;
   if (a == null || b == null || b.HP <= 0 || b.Conditions.Bound > 0 || !TryBind(m, out min, out max)) return;
   rng = rng ?? (() => .5);
   int turns = min + Math.Min(max - min, (int)(rng() * (max - min + 1)));
   foreach (string line in Lines(a, b, "조이기")) { var x = BindTurns.Match(line); if (x.Success) turns = int.Parse(x.Groups["n"].Value); }
   int divisor = 8;
   foreach (string line in Lines(a, b, "조이기")) { var x = BindDamage.Match(line); if (x.Success) divisor = Math.Min(divisor, int.Parse(x.Groups["n"].Value)); }
   if (!Mechanics.ApplyActionState(b, "조이기", turns, log)) return;
   b.Conditions.BindDivisor = divisor;
   log.Add(b.Data.name + ": 「조이기」 " + turns + "T" + (divisor != 8 ? " (대미지 1/" + divisor + ")" : ""));
  }

  // ===== 실패 조건 =====
  public static string FailReason(Fighter a, Fighter b, Move m) {
   string t = Plain(m);
   if (t.Contains("상대가 「잠듦」 상태가 아니면 실패한다") && b != null && b.Status != "잠듦") return "상대가 잠들어 있지 않아 실패했다.";
   if (t.Contains("「필드에 나온 후 최초」가 아니면 실패한다") && a != null && a.Conditions.FieldTurn > 1) return "장소에 나온 첫 T가 아니라 실패했다.";
   return "";
  }

  // ===== 모으기 =====
  public static bool Charges(Move m) { return Tagged(m, "모으기"); }
  public static string HidePlace(Move m) {
   string t = Plain(m);
   var x = Regex.Match(t, @"「([^」]+)」로 모습을 감추");
   if (x.Success && Reach.ContainsKey(x.Groups[1].Value)) return x.Groups[1].Value;
   return t.Contains("모습을 감추") ? "숨음" : "";
  }
  static bool Carries(Move m) { return Plain(m).Contains("상대와 함께"); }
  static string Family(string weather) { return Mechanics.Sunny(weather) ? "쾌청" : Mechanics.Rain(weather) ? "비" : weather ?? ""; }
  // 「날씨가 「쾌청(큰가뭄)」일 때, …」 문장들(날씨 계열, 문장)
  static IEnumerable<KeyValuePair<string, string>> WeatherParts(Move m) {
   foreach (string part in Parts(m)) {
    var x = Regex.Match(part, @"날씨가 「(?<w>[^」(]+)(?:\([^)]*\))?」일 때");
    if (x.Success) yield return new KeyValuePair<string, string>(x.Groups["w"].Value.Trim(), part);
   }
  }
  static string ActiveWeather(Fighter a, Fighter b, Rules r) {
   string w = Mechanics.Weather(a, b ?? a, r);
   return MiscModules.IgnoresWeather(a, w, r) ? "" : Family(w);
  }
  public static double PowerFactor(Fighter a, Fighter b, Move m, Rules r) {
   double v = 1;
   if (a == null || m == null) return v;
   // 숨은 상대에게 닿는 기술
   Dictionary<string, double> reach; double factor;
   if (b != null && !string.IsNullOrEmpty(b.Conditions.Hidden) && Reach.TryGetValue(b.Conditions.Hidden, out reach) && reach.TryGetValue(m.name, out factor)) v *= factor;
   // 「상대가 「X」 상태일 때, 위력이 강화(N배)된다」
   foreach (string part in Parts(m)) {
    var x = Regex.Match(part, @"^상대가 「(?<s>[^」]+)」\s*(?:상태|도중|중)?일 때\s*,\s*위력이 강화\((?<n>\d+(?:\.\d+)?)배\)된다$");
    if (x.Success && InState(b, x.Groups["s"].Value)) v *= double.Parse(x.Groups["n"].Value);
   }
   // 날씨 위력(솔라빔 등)
   string w = ActiveWeather(a, b, r);
   foreach (var part in WeatherParts(m)) {
    if (part.Key != w) continue;
    var x = Regex.Match(part.Value, @"위력이 (?:강화|약화)\((?<n>\d+(?:\.\d+)?)배\)");
    if (x.Success) v *= double.Parse(x.Groups["n"].Value);
   }
   // 연속 사용 강화(메트로놈)
   if (a.Conditions.RepeatMove == m.name && a.Conditions.RepeatCount > 0)
    foreach (string line in Lines(a, b, "연속")) {
     var x = Repeat.Match(line);
     if (x.Success) v *= Math.Min(double.Parse(x.Groups["m"].Value), 1 + double.Parse(x.Groups["s"].Value) * a.Conditions.RepeatCount);
    }
   return v;
  }
  // ===== 급소·방어 관통·필중 =====
  public static int CritBonus(Move m) { var x = Regex.Match(Plain(m), @"급소에 맞기 쉽다\s*\(\s*C\s*\+\s*(\d)\s*\)"); return x.Success ? int.Parse(x.Groups[1].Value) : 0; }
  public static bool AlwaysCrit(Move m) { return m != null && (Plain(m).Contains("반드시 급소에 맞는다") || Tagged(m, "확정급소")); }
  public static bool PiercesProtect(Move m) { return m != null && (Plain(m).Contains("「방어」 기술을 관통한다") || Tagged(m, "방어관통")); }
  public static bool SureHit(Fighter a, Fighter b, Move m, Rules r) {
   if (a == null || m == null) return false;
   string w = ActiveWeather(a, b, r);
   if (WeatherParts(m).Any(x => x.Key == w && x.Value.Contains("「필중」"))) return true;
   foreach (string part in Parts(m)) {
    var x = Regex.Match(part, @"^「(?<t>[^」]+)」 타입이 내보내면 「필중」 효과가 부여된다$");
    if (x.Success && Mechanics.Has(a, x.Groups["t"].Value)) return true;
   }
   return false;
  }
  static bool InState(Fighter f, string name) {
   if (f == null) return false;
   return f.Status == name || name == "상태이상" && f.Status != "정상" || f.Conditions.Hidden == name || Mechanics.HasCondition(f, name);
  }

  // 이 기술 행동이 모으기 T인가(true면 행동은 여기서 끝난다). 2T째는 모으기 상태를 풀고 false
  public static bool Charge(Fighter a, Fighter b, Move m, Rules r, List<string> log) {
   if (a == null || !Charges(m)) return false;
   var c = a.Conditions;
   if (c.ChargeMove == m.name) { Release(a, b); return false; }
   string w = ActiveWeather(a, b, r);
   if (WeatherParts(m).Any(x => x.Key == w && x.Value.Contains("모으기") && x.Value.Contains("생략"))) { log.Add(a.Data.name + ": 「" + w + "」로 모으기를 생략했다"); return false; }
   if (Lines(a, b, "모으기").Any(x => x == SkipCharge)) {
    log.Add(a.Data.name + ": 모으기를 생략했다");
    ConsumeItem(a, SkipCharge, log);
    return false;
   }
   c.ChargeMove = m.name;
   string place = HidePlace(m);
   c.Hidden = place;
   if (Carries(m) && b != null && b.HP > 0 && b.OnField && b.Conditions.Substitute == 0) { b.Conditions.Hidden = "하늘 위"; b.Conditions.HeldInSky = true; }
   log.Add(a.Data.name + ": " + m.name + "의 힘을 모으고 있다" + (place.Length == 0 ? "" : place == "숨음" ? " — 모습을 감췄다" : " — 「" + place + "」로 모습을 감췄다") + (b != null && b.Conditions.HeldInSky ? " (" + b.Data.name + "도 함께)" : ""));
   if (m.category != "변화") foreach (var stage in ChargeStages(m)) Mechanics.ChangeStage(a, b, stage.Key, stage.Value, r, m.name, log);
   return true;
  }
  // 1T째 능력 상승: 「1T째에 자신의 「특공」을 올리고」 · 「명중 후」가 없는 「방어+1」
  static IEnumerable<KeyValuePair<int, int>> ChargeStages(Move m) {
   string[] names = { "", "공격", "방어", "특공", "특방", "속도" };
   foreach (string part in Parts(m)) {
    var x = Regex.Match(part, @"1T째에 자신의 「(?<s>[^」]+)」을 올리고");
    if (x.Success) { int i = Array.IndexOf(names, x.Groups["s"].Value); if (i > 0) yield return new KeyValuePair<int, int>(i, 1); continue; }
    if (part.Contains("명중 후")) continue;
    x = Regex.Match(part, @"^「(?<s>[^」+]+)\+(?<n>\d)」$");
    if (!x.Success) continue;
    foreach (string s in x.Groups["s"].Value.Split('/')) { int i = Array.IndexOf(names, s); if (i > 0) yield return new KeyValuePair<int, int>(i, int.Parse(x.Groups["n"].Value)); }
   }
  }
  // 모으기 끝(공격 T 시작·행동 실패·퇴장): 함께 데려간 상대도 내려온다
  public static void Release(Fighter a, Fighter b) {
   if (a == null) return;
   a.Conditions.ChargeMove = ""; a.Conditions.Hidden = "";
   if (b != null && b.Conditions.HeldInSky) { b.Conditions.HeldInSky = false; b.Conditions.Hidden = ""; }
  }
  // 숨은 상대에게 닿는가(노가드·마음의눈은 닿는다)
  public static bool Reaches(Fighter a, Fighter b, Move m, int side) {
   if (b == null || string.IsNullOrEmpty(b.Conditions.Hidden)) return true;
   if (Mechanics.Ability(a, "노가드") || Mechanics.Ability(b, "노가드") || side >= 0 && b.Conditions.MindReaderSide == side) return true;
   Dictionary<string, double> reach;
   return Reach.TryGetValue(b.Conditions.Hidden, out reach) && reach.ContainsKey(m.name);
  }
  // 1번만인 소지품이 그 계속 효과를 줬다면(다른 출처가 없을 때) 소비
  static void ConsumeItem(Fighter f, string sentence, List<string> log) {
   var list = f.Data.potentials ?? new Potential[0];
   bool other = list.Any(p => p != null && p.id != ItemRuntime.Slot && Clean(p.description).EndsWith(sentence));
   bool item = list.Any(p => p != null && p.id == ItemRuntime.Slot && p.uses == "1/시" && Clean(p.description) == sentence);
   if (item && !other) ItemRuntime.Lose(f, "소비", log.Add);
  }

  // ===== 연속 사용 =====
  public static void Record(Fighter a, Move m) {
   var c = a.Conditions;
   if (c.RepeatMove == m.name) c.RepeatCount++;
   else { c.RepeatMove = m.name; c.RepeatCount = 0; }
  }

  // ===== 접촉 반응 무시 =====
  public static bool IgnoresContact(Fighter user) { return user != null && Lines(user, null, "접촉").Any(x => x == ContactGuard); }

  // ===== 장소를 떠날 때 =====
  public static void Leave(Fighter a) {
   var c = a.Conditions;
   if (a.FieldOpponent != null && a.FieldOpponent.Conditions.HeldInSky && !string.IsNullOrEmpty(c.ChargeMove)) { a.FieldOpponent.Conditions.HeldInSky = false; a.FieldOpponent.Conditions.Hidden = ""; }
   c.ChargeMove = ""; c.Hidden = ""; c.HeldInSky = false;
   c.RepeatMove = ""; c.RepeatCount = 0; c.NextPriority = 0; c.BindDivisor = 0;
  }

  // ===== 사건 효과 실행기 =====
  public static void RegisterExecutors() {
   PotentialExecutors.Register("능력 하락 복구",
    c => Clean(c.Line) == RestoreDrops,
    c => {
     var f = c.Owner;
     if (f == null || f.HP <= 0) return EffectOutcome.Failed;
     bool any = false;
     for (int i = 1; i < f.Stages.Length; i++) if (f.Stages[i] < 0) { f.Stages[i] = 0; any = true; }
     if (f.AccuracyStage < 0) { f.AccuracyStage = 0; any = true; }
     if (f.EvasionStage < 0) { f.EvasionStage = 0; any = true; }
     if (!any) return EffectOutcome.Failed;
     c.Say(f.Data.name + ": 내려간 능력이 원래대로 돌아왔다 (" + c.Source.Label + ")");
     return EffectOutcome.Success;
    });
   PotentialExecutors.Register("다음 T 우선도",
    c => NextPriority.IsMatch(Clean(c.Line)),
    c => {
     var f = c.Owner;
     if (f == null || f.HP <= 0) return EffectOutcome.Failed;
     int n = int.Parse(NextPriority.Match(Clean(c.Line)).Groups["n"].Value);
     f.Conditions.NextPriority += n;
     c.Say(f.Data.name + ": 다음 T 기술 우선도 +" + n + " (" + c.Source.Label + ")");
     return EffectOutcome.Success;
    });
   PotentialExecutors.Register("소지품 이동",
    c => Clean(c.Line) == GiveItem,
    c => {
     var f = c.Owner; var other = c.Opponent;
     if (f == null || other == null || other.HP <= 0 || string.IsNullOrEmpty(f.Data.item) || !string.IsNullOrEmpty(other.Data.item)) return EffectOutcome.Failed;
     string item = f.Data.item;
     f.Data.item = ""; ItemRuntime.Sync(f);
     other.Data.item = item; ItemRuntime.Sync(other);
     c.Say(f.Data.name + "의 「" + item + "」이 " + other.Data.name + "에게 옮겨졌다");
     return EffectOutcome.Success;
    });
  }
 }
}
