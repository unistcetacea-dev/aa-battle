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
 //  조건 위력 「상대가 「X」 상태(도중·중)일 때, 위력이 강화(N배)된다」 · 「전체 필드가 「X」 상태일 때, …」
 //  대미지 계산 「상대의 「방어」 능력치를 반감해서」 · 「자신과 상대의 「X」 능력치를 반전해」(공격 쪽 능력치를 상대에게서, 방어 쪽을 자신에게서)
 //            「상대의 「공격」 능력치로」(공격 쪽 능력치를 상대에게서) · 「상대의 「방어/특방/회피」의 상승(강화)을 관통한다」
 //  상성      「상대가 「X」 타입일 때, 「효과가 굉장」해진다」 · 「상대가 자신과 같은 타입일 때, …」 · 「타입 상성에 의해 무효화되지 않는다」
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
  public const string ClearField = "전체 필드의 특수한 상태를 해제한다";
  public const string Analyze = "상대의 데이터를 해석한다";
  public const string ClearSubsAndHazards = "전체 필드의 대타출동과 설치물을 해제한다";
  public const string Swallow = "자신의 비축하기 상태를 해제해 그 횟수에 따라 HP를 회복한다";
  static readonly Regex Screens = new Regex(@"^(?<s>자진|적진)의 「벽」을 해제한다$");
  static readonly Regex ResetStages = new Regex(@"^(?<s>상대|자신|전원)의 능력 변화를 초기화한다$");

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
   if (t == SkipCharge || t == ContactGuard || t == RestoreDrops || t == GiveItem || t == ClearField || Screens.IsMatch(t) || ResetStages.IsMatch(t) || t == Analyze || t == ClearSubsAndHazards || t == Swallow) return t + ".";
   m = Regex.Match(t, @"^(?<s>상대|자신|필드의 전원)의 「능력변화」를 「초기화」한다$");
   if (m.Success) return (m.Groups["s"].Value == "필드의 전원" ? "전원" : m.Groups["s"].Value) + "의 능력 변화를 초기화한다.";
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
   if (s == ClearField) return "장 해제";
   if (Screens.IsMatch(s)) return "벽 해제";
   if (s == Analyze) return "데이터 해석";
   if (s == ClearSubsAndHazards) return "대타·설치물 해제";
   if (s == Swallow) return "비축 회복";
   if (ResetStages.IsMatch(s)) return "능력 변화 초기화";
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

  // ===== 반동: 「준 대미지의 N/M만큼 반동 대미지를 받는다」(돌머리·매직가드는 받지 않는다) =====
  public static void Recoil(Fighter a, Fighter b, Move m, Rules r, int dealt, List<string> log) {
   if (a == null || a.HP <= 0 || dealt <= 0 || AbilityPhrases.IgnoresMoveRecoil(a, r)) return;
   var x = Regex.Match(Plain(m), @"준 대미지의 (\d+)/(\d+)만큼 반동 대미지를 받는다");
   if (!x.Success) return;
   Mechanics.Hurt(a, b, r, Math.Max(1, dealt * int.Parse(x.Groups[1].Value) / int.Parse(x.Groups[2].Value)), m.name + " 반동", log);
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
  public static string FailReason(Fighter a, Fighter b, Move m) { return FailModules.Reason(a, b, m, EffectScope.Current == null ? new Rules() : EffectScope.Current.Rules); }

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
    var x = Regex.Match(part, @"^상대가 「(?<s>[^」]+)」\s*(?:상태|도중|중)?일 때\s*,\s*위력이 강화\((?<n>\d+(?:\.\d+)?)배\)(?:된다|되고 「필중」 효과가 부여된다)$");
    if (x.Success && InState(b, x.Groups["s"].Value)) v *= double.Parse(x.Groups["n"].Value);
   }
   // 자신·이번 T 조건
   foreach (string part in Parts(m)) {
    var x = Regex.Match(part, @"^(?<c>.+?)\s*,\s*위력이 강화\((?<n>\d+(?:\.\d+)?)배\)된다$");
    if (!x.Success) continue;
    string c = x.Groups["c"].Value; bool holds = false;
    var y = Regex.Match(c, @"^(?:자신이 )?「(?<s>[^」]+)」 상태일 때$");
    if (c == "상대보다 먼저 내보낼 때") holds = b != null && b.Conditions.ActionOrder == 0;
    else if (c == "기술을 내보내기 전에 상대의 공격을 받으면") holds = (a.Conditions.TurnFacts ?? new List<TriggerFact>()).Any(f => f.Kind == "명중" && f.Received && f.Move != null && f.Move.category != "변화");
    else if (c == "자신의 HP가 「가득」일 때") holds = r != null && a.HP >= Mechanics.MaxHP(a, b ?? a, r);
    else if (c == "상대가 「방어」 기술을 내보내고 있으면") holds = b != null && b.Conditions.Protect;
    else if (y.Success) holds = InState(a, y.Groups["s"].Value);
    if (holds) v *= double.Parse(x.Groups["n"].Value);
   }
   // 물놀이(불꽃)·흙놀이(전기) 약화(실기 0.33배)
   if (r != null && r.WaterSport > 0 && (m.types ?? new string[0]).Contains("불꽃")) v *= .33;
   if (r != null && r.MudSport > 0 && (m.types ?? new string[0]).Contains("전기")) v *= .33;
   // 「전체 필드가 「X」 상태일 때, 위력이 강화(N배)된다」
   foreach (string part in Parts(m)) {
    var x = Regex.Match(part, @"^전체 필드가 「(?<s>[^」]+)」 상태일 때\s*,\s*위력이 강화\((?<n>\d+(?:\.\d+)?)배\)된다$");
    if (x.Success && r != null && r.Terrain == x.Groups["s"].Value) v *= double.Parse(x.Groups["n"].Value);
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
    x = Regex.Match(part, @"^상대가 「(?<s>[^」]+)」 상태일 때\s*,\s*위력이 강화\([^)]*\)되고 「필중」 효과가 부여된다$");
    if (x.Success && InState(b, x.Groups["s"].Value)) return true;
   }
   return false;
  }
  static bool InState(Fighter f, string name) {
   if (f == null) return false;
   if (name.Contains("/")) return name.Split('/').Any(x => InState(f, x));
   return f.Status == name || name == "상태이상" && f.Status != "정상" || f.Conditions.Hidden == name || Mechanics.HasCondition(f, name);
  }

  // ===== 방호 특성 관통 · 벽 깨기 · 고정 대미지 =====
  public static bool PiercesAbility(Move m) { return m != null && (Plain(m).Contains("〔방호 특성〕을 관통한다") || Tagged(m, "특성관통")); }
  // 「적진의 「벽」을 해제한 후 기술을 내보낸다」: 첫 타 전에
  public static void BreakScreens(Fighter a, Fighter b, Move m, Rules r, int side, List<string> log) {
   if (m == null || !Plain(m).Contains("적진의 「벽」을 해제한 후 기술을 내보낸다")) return;
   var target = r.Sides[1 - Math.Max(0, Math.Min(1, side))];
   if (target.Reflect == 0 && target.LightScreen == 0) return;
   target.Reflect = target.LightScreen = 0;
   log.Add(m.name + ": 상대 진영의 벽을 깨뜨렸다");
  }
  // 고정 대미지(타입 상성은 무효만 따른다). 16개 난수 자리, 고정 대미지 기술이 아니면 null
  public static int[] FixedRolls(Fighter a, Fighter b, Move m, Rules r) {
   if (a == null || b == null || m == null || m.category == "변화") return null;
   foreach (string part in Parts(m)) {
    Match x;
    int value = -1;
    if ((x = Regex.Match(part, @"^상대에게 현재 HP의 1/(?<n>\d+)만큼 고정 대미지를 준다$")).Success) value = Math.Max(1, b.HP / int.Parse(x.Groups["n"].Value));
    else if ((x = Regex.Match(part, @"^상대에게 HP의 1/(?<n>\d+)만큼 고정 대미지를 준다$")).Success) value = Math.Max(1, Mechanics.MaxHP(b, a, r) / int.Parse(x.Groups["n"].Value));
    else if ((x = Regex.Match(part, @"^상대에게 (?<n>\d+)만큼 고정 대미지를 준다$")).Success) value = int.Parse(x.Groups["n"].Value);
    else if (Regex.IsMatch(part, @"상대에게 자신의 남은 HP만큼 고정 대미지를 준다$")) value = a.HP;
    else if ((x = Regex.Match(part, @"^상대로부터 받은 (?:「(?<c>물리|특수)」 )?대미지의 (?<n>\d+(?:\.\d+)?)배만큼 고정 대미지를 준다$")).Success) {
     int taken = x.Groups["c"].Value == "물리" ? a.Conditions.TakenPhysical : x.Groups["c"].Value == "특수" ? a.Conditions.TakenSpecial : a.Conditions.TakenPhysical + a.Conditions.TakenSpecial;
     value = (int)Math.Floor(taken * double.Parse(x.Groups["n"].Value));
    }
    else if (part == "자신의 레벨의 0.5~1.5배 사이의 고정 대미지를 준다" || part == "자신의 레벨의 0.5～1.5배 사이의 고정 대미지를 준다")
     return Enumerable.Range(0, 16).Select(i => Math.Max(1, (int)Math.Floor(a.Data.level * (50 + i * 100 / 15.0) / 100))).ToArray();
    if (value >= 0) return Enumerable.Repeat(value, 16).ToArray();
   }
   return null;
  }

  // ===== 대미지 계산 문장 =====
  public static bool HalvesDefense(Move m) { return Plain(m).Contains("「방어」 능력치를 반감해서 대미지를 계산한다"); }
  public static bool SwapsStats(Move m) { return Plain(m).Contains("능력치를 반전해 대미지를 계산한다"); }
  public static bool FoeAttack(Move m) { return Parts(m).Any(x => Regex.IsMatch(x, @"^상대의 「(?:공격|특공)」 능력치로 대미지를 계산한다$")); }
  public static bool IgnoresRaise(Move m, string stat) {
   foreach (string part in Parts(m)) {
    var x = Regex.Match(part, @"^상대의 「(?<s>[^」]+)」의 상승\(강화\)을 관통한다$");
    if (x.Success && x.Groups["s"].Value.Split('/').Contains(stat)) return true;
   }
   return false;
  }
  // 대미지 식의 공격 쪽·방어 쪽 능력치
  public static double AttackStat(Fighter a, Fighter b, Move m, int i, Rules r, int attackerSide, int defenderSide) {
   return SwapsStats(m) || FoeAttack(m) ? Engine.Effective(b, a, i, r, 1, defenderSide) : Engine.Effective(a, b, i, r, 1, attackerSide);
  }
  public static double DefenseStat(Fighter a, Fighter b, Move m, int i, Rules r, int attackerSide, int defenderSide) {
   var rules = r;
   if (IgnoresRaise(m, i == 2 ? "방어" : i == 4 ? "특방" : i == 1 ? "공격" : i == 3 ? "특공" : "속도")) { rules = r.Copy(); rules.IgnoreDefenderRaise = true; }
   double v = SwapsStats(m) ? Engine.Effective(a, b, i, rules, 2, attackerSide) : Engine.Effective(b, a, i, rules, 2, defenderSide);
   return HalvesDefense(m) ? Math.Max(1, Math.Floor(v / 2)) : v;
  }
  // 타입별 배율 덮어쓰기(Mechanics.TypeEffect의 방어 타입 하나마다)
  public static double TypeOverride(Fighter a, Move m, string defenseType, double k) {
   if (m == null) return k;
   string seer = a == null ? "" : a.Conditions.Foresight;
   var types = m.types ?? new string[0];
   if (k == 0 && seer.Length > 0 && (seer == "미라클아이" ? defenseType == "악" && types.Contains("에스퍼") : defenseType == "고스트" && (types.Contains("노말") || types.Contains("격투")))) k = 1;
   foreach (string part in Parts(m)) {
    var x = Regex.Match(part, @"^상대가 (?<t>(?:「[^」]+」)+) 타입일 때\s*,\s*「효과가 굉장」해진다$");
    if (x.Success && Regex.Matches(x.Groups["t"].Value, "「([^」]+)」").Cast<Match>().Any(t => t.Groups[1].Value == defenseType)) k = 2;
    if (Regex.IsMatch(part, @"^상대가 자신과 같은 타입일 때\s*,\s*「효과가 굉장」해진다$") && a != null && (a.Data.types ?? new string[0]).Contains(defenseType)) k = 2;
    if (part == "타입 상성에 의해 무효화되지 않는다" && k == 0) k = 1;
   }
   return k;
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
   if (Plain(m).Contains("「" + b.Conditions.Hidden + "」 상태인 상대에게도 명중한다")) return true;
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
   c.ProtectMove = ""; c.Enduring = false; c.SoloChain = 0;
   c.Minimized = c.FocusEnergy = c.MagicCoat = c.DestinyBond = c.Nightmare = c.Grudge = c.Powder = false; c.Foresight = "";
   c.Yawn = c.MagnetRise = c.Telekinesis = c.Embargo = c.Stockpile = c.LaserFocus = c.RolledPower = 0; c.FollowMe = c.Quash = false;
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
   // 장 해제(사용자 결정): 양쪽 진영의 벽·설치물과 지형·중력·룸이 모두 사라진다
   PotentialExecutors.Register("장 해제",
    c => Clean(c.Line) == ClearField,
    c => {
     var r = c.Rules ?? new Rules();
     bool any = r.Terrain != "없음" || r.Gravity > 0 || r.TrickRoom > 0;
     foreach (var side in r.Sides) {
      any |= side.HasHazards() || side.Reflect > 0 || side.LightScreen > 0;
      side.ClearHazards(); side.Reflect = 0; side.LightScreen = 0;
     }
     foreach (var side in r.Sides) { any |= side.Safeguard > 0 || side.Mist > 0; side.Safeguard = side.Mist = 0; }
     r.Terrain = "없음"; r.TerrainTurns = 0; r.Gravity = 0; r.TrickRoom = 0;
     c.Say(any ? "전체 필드의 특수한 상태가 사라졌다 (" + c.Source.Name + ")" : "해제할 특수한 상태가 없다");
     return any ? EffectOutcome.Success : EffectOutcome.Failed;
    });
   PotentialExecutors.Register("데이터 해석 실행",
    c => Clean(c.Line) == Analyze,
    c => {
     var target = c.Opponent;
     if (target == null || target.HP <= 0) return EffectOutcome.Failed;
     if (PotentialProtection.AnalysisBlocked(target, c.Owner, c.Rules)) { c.Say(target.Data.name + ": 데이터 해석을 무효화했다"); return EffectOutcome.Failed; }
     var state = BattleRecords.StateOf(c.Owner);
     if (BattleRecords.IsAnalyzed(state, target)) { c.Say(target.Data.name + ": 이미 해석한 상대다"); return EffectOutcome.Success; }
     c.Say(c.Source.Name + " · " + target.Data.name + "의 데이터를 해석한다.");
     if (state != null) BattleRecords.RecordAnalysis(state, c.Owner, new[] { c.Owner }, target, c.Say); else RuntimeTriggers.Analyzed(c.Owner, new[] { c.Owner }, target, c.Say);
     return EffectOutcome.Success;
    });
   PotentialExecutors.Register("대타·설치물 해제",
    c => Clean(c.Line) == ClearSubsAndHazards,
    c => {
     var r = c.Rules ?? new Rules();
     foreach (var side in r.Sides) side.ClearHazards();
     foreach (var f in new[] { c.Owner, c.Opponent }) if (f != null) f.Conditions.Substitute = 0;
     c.Say("전체 필드의 대타출동·설치물이 사라졌다");
     return EffectOutcome.Success;
    });
   // 꿀꺽: 1회 1/4, 2회 1/2, 3회 가득. 비축한 방어·특방 상승도 되돌린다(실기)
   PotentialExecutors.Register("비축 회복",
    c => Clean(c.Line) == Swallow,
    c => {
     var f = c.Owner; int n = f == null ? 0 : f.Conditions.Stockpile;
     if (n <= 0) { c.Say("비축하기가 없어 실패했다"); return EffectOutcome.Failed; }
     int max = Mechanics.MaxHP(f, c.Opponent ?? f, c.Rules ?? new Rules());
     Mechanics.Heal(f, c.Opponent ?? f, c.Rules ?? new Rules(), n >= 3 ? max : n == 2 ? max / 2 : max / 4, "꿀꺽", c.Log ?? new List<string>());
     f.Stages[2] = Math.Max(-6, f.Stages[2] - n); f.Stages[4] = Math.Max(-6, f.Stages[4] - n); f.Conditions.Stockpile = 0;
     return EffectOutcome.Success;
    });
   PotentialExecutors.Register("벽 해제",
    c => Screens.IsMatch(Clean(c.Line)),
    c => {
     var r = c.Rules ?? new Rules();
     int own = Mechanics.PartySide(c.Owner, r); if (own < 0) own = 0;
     int side = Screens.Match(Clean(c.Line)).Groups["s"].Value == "자진" ? own : 1 - own;
     if (r.Sides[side].Reflect == 0 && r.Sides[side].LightScreen == 0) return EffectOutcome.Failed;
     r.Sides[side].Reflect = r.Sides[side].LightScreen = 0;
     c.Say((side + 1) + "팀 진영의 벽이 사라졌다 (" + c.Source.Name + ")");
     return EffectOutcome.Success;
    });
   PotentialExecutors.Register("능력 변화 초기화",
    c => ResetStages.IsMatch(Clean(c.Line)),
    c => {
     string who = ResetStages.Match(Clean(c.Line)).Groups["s"].Value;
     var targets = who == "전원" ? new[] { c.Owner, c.Opponent } : new[] { who == "상대" ? c.Opponent : c.Owner };
     bool any = false;
     foreach (var f in targets.Where(x => x != null && x.HP > 0)) {
      if (f.Stages.Skip(1).All(x => x == 0) && f.AccuracyStage == 0 && f.EvasionStage == 0) continue;
      f.Stages = new int[f.Stages.Length]; f.AccuracyStage = f.EvasionStage = 0; any = true;
      c.Say(f.Data.name + ": 능력 변화가 원래대로 돌아왔다 (" + c.Source.Name + ")");
     }
     return any ? EffectOutcome.Success : EffectOutcome.Failed;
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
