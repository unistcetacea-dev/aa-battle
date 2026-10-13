using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 // 트리거 모듈 2단계: 상태(상태이상·상태변화·빈사) · 체력 · 날씨 · 장소(지형·트릭룸·중력) · 랭크.
 // 상태 모듈(「…일 때」)은 지금 성립하는가(Holds), 사건 모듈(「…되었을 때」)은 StateWatch가 전후 값을 비교해 낸다.
 // 확정 01·02(체력: 현재 값 / 위에서 아래로 넘을 때만), 16·17(실제 랭크 변화), 21(강한 비는 비에 포함, 눈=싸라기눈, 날씨부정이면 불성립),
 // 23(날씨 자연 종료는 변화 아님), 24(지형은 깔려 있으면 성립), 27(중력 등 전개 중 / 시작·해제는 별도), 47(아군 = 출전, 갱신은 「되었을 때」 아님)
 public static partial class TriggerModules {
  static readonly string[] StatNames = { "", "공격", "방어", "특공", "특방", "속도", "명중", "회피" };
  static readonly Dictionary<string, string> StatAlias = new Dictionary<string, string> { { "공", "공격" }, { "방", "방어" }, { "특", "특공" }, { "속", "속도" }, { "스피드", "속도" } };
  static readonly Dictionary<string, string> WeatherAlias = new Dictionary<string, string> { { "모래날림", "모래바람" }, { "설경", "눈" }, { "싸라기눈(설경)", "싸라기눈" }, { "(강한) 비", "비" }, { "(강한)비", "비" }, { "맑음", "쾌청" } };
  static readonly string[] Rooms = { "트릭룸", "중력" };

  public static IEnumerable<string> KnownStates() {
   return Mechanics.Statuses.Where(x => x != "정상").Concat(StateEffects.Table.Select(x => x.Name)).Concat(new[] { "빈사", "상태이상" }).Distinct();
  }
  static bool IsStatus(string name) { return name == "상태이상" || Mechanics.Statuses.Contains(name) && name != "정상"; }
  static string[] StateNames(string raw) {
   var names = Regex.Matches(raw ?? "", "「([^」]+)」").Cast<Match>().SelectMany(m => Regex.Split(m.Groups[1].Value, @"[/()（）]")).Select(x => x.Trim()).Where(x => x.Length > 0).Distinct().ToArray();
   return names.Length > 0 && names.All(x => KnownStates().Contains(x)) ? names : null;
  }
  static int[] StatList(string raw) {
   string t = (raw ?? "").Trim('「', '」', ' ');
   if (t.Length == 0 || t == "능력" || t == "능력치" || t == "능력 랭크") return new int[0];
   if (t == "전능력치" || t == "전능력") return new[] { 1, 2, 3, 4, 5 };
   var list = new List<int>();
   foreach (string part in t.Split('/', '・', '·', ' ')) {
    string name = part.Trim(); if (name.Length == 0) continue;
    string alias; if (StatAlias.TryGetValue(name, out alias)) name = alias;
    int index = Array.IndexOf(StatNames, name);
    if (index <= 0) return null;
    if (!list.Contains(index)) list.Add(index);
   }
   return list.Count > 0 ? list.ToArray() : null;
  }
  static string Weather(string raw) { string t = raw.Trim(); string alias; if (WeatherAlias.TryGetValue(t, out alias)) t = alias; return Mechanics.Weathers.Contains(t) && t != "없음" ? t : null; }
  static string Subject(string raw) {
   string t = Regex.Replace(raw ?? "", @"\s+", "");
   if (t.StartsWith("자신과상대")) return "자신과 상대";
   if (t.StartsWith("상대")) return "상대";
   if (t.StartsWith("아군") || t.StartsWith("현재아군")) return "아군";
   return "자신";
  }
  static string Owner(string subject) { return subject == "상대" ? "상대가" : subject == "아군" ? "아군이" : subject == "자신과 상대" ? "자신과 상대가" : "자신이"; }
  static string Of(string subject) { return subject == "상대" ? "상대의" : subject == "아군" ? "아군의" : "자신의"; }
  static string Category(string[] names) { if (names.Length == 1 && names[0] == "상태이상") return ""; return names.All(IsStatus) ? "상태이상" : names.All(x => !IsStatus(x) && x != "빈사") ? "상태변화" : "상태"; }
  static string Bracket(IEnumerable<string> names) { return string.Concat(names.Select(x => "「" + x + "」")); }

  // ===== 수집 원문 → 구조 =====
  static bool TryParseState(string t, out TriggerSpec spec) {
   spec = null; Match m;
   m = Regex.Match(t, @"^(?<s>자신|아군|상대)의\s*「(?<a>공격|방어|특공|특방|속도)」(?:이|가)\s*「(?<b>공격|방어|특공|특방|속도)」\s*(?:(?:보다\s*)?(?<low>이하일)|보다\s*(?<high>높을))\s*때$");
   if (m.Success) {
    spec = new TriggerSpec { Module = "능력치 비교", Subject = m.Groups["s"].Value,
     Stats = new[] { Array.IndexOf(StatNames, m.Groups["a"].Value), Array.IndexOf(StatNames, m.Groups["b"].Value) },
     Op = m.Groups["high"].Success ? "높음" : "이하" };
    return true;
   }
   // 빈사(쓰러졌을 때)
   m = Regex.Match(t, @"^(?:(?<s>자신|상대)(?:이|가)\s*)?(?:「빈사」\s*(?:상태)?(?:가|이)\s*(?:됐을|되었을)\s*때|쓰러졌을\s*때)$");
   if (m.Success) { spec = new TriggerSpec { Module = "상태 됨", Subject = m.Groups["s"].Success ? m.Groups["s"].Value : "자신", Names = new[] { "빈사" } }; return true; }
   // 상태(있음 / 됨 / 해제)
   m = Regex.Match(t, @"^(?:현재\s*)?(?:(?<s>자신과\s*상대|자신|상대|아군)(?:이|가|의)\s*)?(?<n>(?:「[^」]+」\s*)+)\s*(?:상태이상|상태변화|상태)?\s*(?<v>일\s*때|이\s*있을\s*때|에\s*걸렸을\s*때|(?:가|이)\s*(?:됐을|되었을)\s*때|(?:가|이)\s*(?:해제됐을|해제되었을|풀렸을)\s*때)$");
   var stateNames = m.Success ? StateNames(m.Groups["n"].Value) : null;
   if (stateNames != null) {
    var names = stateNames;
    string v = m.Groups["v"].Value.Replace(" ", "");
    string module = v == "일때" || v == "이있을때" ? "상태" : v.Contains("해제") || v.Contains("풀렸") ? "상태 해제" : "상태 됨";
    if (v == "에걸렸을때") module = "상태 됨";
    string subject = m.Groups["s"].Success ? Subject(m.Groups["s"].Value) : "자신";
    if (module != "상태" && subject == "자신과 상대") return false;
    if (module == "상태 해제" && names.Contains("빈사")) return false;
    spec = new TriggerSpec { Module = module, Subject = subject, Names = names };
    return true;
   }
   // 회복
   m = Regex.Match(t, @"^(?:(?<s>자신|상대)(?:의|이|가)\s*)?체력(?:을|이)\s*회복했을\s*때$");
   if (m.Success) { spec = new TriggerSpec { Module = "회복", Subject = m.Groups["s"].Success ? m.Groups["s"].Value : "자신" }; return true; }
   // 체력(현재 / 변화)
   m = Regex.Match(t, @"^(?:(?<s>자신|상대)(?:의)?\s*)?체력이\s*(?:「)?(?:(?<t>\d+)\s*/\s*(?<b>\d+)|(?<half>절반))(?:」)?\s*(?<o>이하|이상)?\s*(?<v>일\s*때|이\s*됐을\s*때|이\s*되었을\s*때|가\s*됐을\s*때|가\s*되었을\s*때)$");
   if (m.Success) {
    string v = m.Groups["v"].Value.Replace(" ", "");
    bool became = v != "일때";
    string op = m.Groups["o"].Success ? m.Groups["o"].Value : "이하";
    if (became && op == "이상") return false;
    if (!m.Groups["o"].Success && !became) op = "이하";
    int top = m.Groups["half"].Success ? 1 : int.Parse(m.Groups["t"].Value), bottom = m.Groups["half"].Success ? 2 : int.Parse(m.Groups["b"].Value);
    if (bottom <= 0 || top <= 0 || top > bottom) return false;
    spec = new TriggerSpec { Module = became ? "체력 변화" : "체력", Subject = m.Groups["s"].Success ? m.Groups["s"].Value : "자신", Op = op, Top = top, Bottom = bottom };
    return true;
   }
   m = Regex.Match(t, @"^(?:(?<s>자신|상대)(?:의)?\s*)?체력이\s*(?<v>가득일\s*때|가득하면|최대일\s*때|가득하지\s*않을\s*때|가득이\s*아닐\s*때|가득하지\s*않(?:아졌을|게\s*됐을|게\s*되었을)\s*때|가득해졌을\s*때|「?1」?이\s*(?:됐을|되었을)\s*때|감소했을\s*때)$");
   if (m.Success) {
    string v = m.Groups["v"].Value.Replace(" ", "").Replace("「", "").Replace("」", "");
    string module = "체력", op;
    if (v == "가득일때" || v == "가득하면" || v == "최대일때") op = "가득";
    else if (v == "가득하지않을때" || v == "가득이아닐때") op = "가득 아님";
    else if (v == "가득해졌을때") { module = "체력 변화"; op = "가득"; }
    else if (v.StartsWith("1이")) { module = "체력 변화"; op = "1"; }
    else if (v == "감소했을때") { module = "체력 변화"; op = "감소"; }
    else { module = "체력 변화"; op = "가득 아님"; }
    spec = new TriggerSpec { Module = module, Subject = m.Groups["s"].Success ? m.Groups["s"].Value : "자신", Op = op };
    return true;
   }
   // 날씨
   m = Regex.Match(t, @"^(?:필드의\s*)?(?:날씨|기후)가\s*(?:변화했을|바뀌었을|변했을)\s*때$");
   if (m.Success) { spec = new TriggerSpec { Module = "날씨 변화" }; return true; }
   if (Regex.IsMatch(t, @"^(?:필드에\s*)?날씨가\s*있을\s*때$")) { spec = new TriggerSpec { Module = "날씨" }; return true; }
   m = Regex.Match(t, @"^(?:필드의\s*|필드가\s*)?(?:(?:날씨|기후)(?:가|는)\s*)?「(?<w>[^」]+)」\s*(?<v>일\s*때|(?:이|가)\s*(?:됐을|되었을)\s*때|(?:으로|로)\s*바뀌었을\s*때)$");
   if (m.Success && (t.Contains("날씨") || t.Contains("기후") || Weather(m.Groups["w"].Value) != null && !t.StartsWith("「"))) {
    string w = Weather(m.Groups["w"].Value);
    if (w == null) return false;
    spec = new TriggerSpec { Module = m.Groups["v"].Value.StartsWith("일") ? "날씨" : "날씨 변화", Place = w };
    return true;
   }
   // 장소(지형·트릭룸·중력)
   m = Regex.Match(t, @"^(?:(?:필드|장소)(?:가|에)\s*)?「(?<p>[^」]+)」\s*(?<v>일\s*때|이\s*전개되어\s*있을\s*때|가\s*전개되어\s*있을\s*때|(?:이|가)\s*(?:됐을|되었을)\s*때|(?:으로|로)\s*변했을\s*때|(?:이|가)\s*전개되었을\s*때|(?:이|가)\s*해제되었을\s*때)$");
   if (m.Success) {
    string p = m.Groups["p"].Value.Trim(), v = m.Groups["v"].Value.Replace(" ", "");
    bool terrain = EffectModules.TerrainNames.Contains(p), room = Rooms.Contains(p);
    if (!terrain && !room) return false;
    bool now = v == "일때" || v.Contains("전개되어있을");
    if (terrain && (v.Contains("전개") || v.Contains("해제"))) return false;
    spec = new TriggerSpec { Module = now ? "장소" : "장소 변화", Place = p, Release = v.Contains("해제") };
    return true;
   }
   // 랭크(실제 변화)
   m = Regex.Match(t, @"^(?<by>상대(?:에\s*의해|에게)\s*)?(?:(?<s>자신|상대)(?:의)?\s*)?(?<st>「[^」]+」|능력(?:\s*랭크)?|능력치|[가-힣]+)\s*(?:의\s*랭크)?(?:이|가)\s*(?<big>크게\s*)?(?<v>올랐을|올라갔을|상승했을|내려갔을|내렸을|떨어졌을|하락했을|감소했을|감소\(저하\)했을|저하했을)\s*때$");
   if (m.Success) {
    var stats = StatList(m.Groups["st"].Value);
    if (stats == null) return false;
    string v = m.Groups["v"].Value;
    spec = new TriggerSpec { Module = "랭크", Subject = m.Groups["s"].Success ? m.Groups["s"].Value : "자신", Stats = stats, Rise = v.StartsWith("올") || v.StartsWith("상승"), Big = m.Groups["big"].Success, ByOpponent = m.Groups["by"].Success };
    if (spec.ByOpponent && (spec.Subject != "자신" || spec.Rise)) return false;
    return true;
   }
   return false;
  }

  // ===== 표준 문장 =====
  static string CanonicalState(TriggerSpec s) {
   switch (s.Module) {
    case "종족치 등급": return Of(s.Subject) + " 「" + s.Names[0] + "」 종족치가 「" + s.Names[1] + "」 " + s.Op + "일 때";
    case "턴 조건": return TurnConditions.Canonical(s);
    case "능력치 비교": return Of(s.Subject) + " 「" + StatNames[s.Stats[0]] + "」" + Particle(StatNames[s.Stats[0]], "이", "가") + " 「" + StatNames[s.Stats[1]] + "」" + (s.Op == "높음" ? "보다 높을 때" : " 이하일 때");
    case "상태": return Owner(s.Subject) + " " + Bracket(s.Names) + (Category(s.Names).Length > 0 ? " " + Category(s.Names) : "") + "일 때";
    case "상태 됨":
     if (s.Names.Length == 1 && s.Names[0] == "빈사") return Owner(s.Subject) + " 「빈사」 상태가 되었을 때";
     string category = Category(s.Names);
     if (category.Length == 0) return Owner(s.Subject) + " 「상태이상」이 되었을 때";
     return Owner(s.Subject) + " " + Bracket(s.Names) + " " + category + (category == "상태이상" ? "이" : "가") + " 되었을 때";
    case "상태 해제": return Of(s.Subject) + " " + Bracket(s.Names) + " 상태가 해제되었을 때";
    case "체력":
     if (s.Op == "가득") return Of(s.Subject) + " 체력이 가득일 때";
     if (s.Op == "가득 아님") return Of(s.Subject) + " 체력이 가득하지 않을 때";
     return Of(s.Subject) + " 체력이 " + s.Top + "/" + s.Bottom + " " + s.Op + "일 때";
    case "체력 변화":
     if (s.Op == "가득") return Of(s.Subject) + " 체력이 가득해졌을 때";
     if (s.Op == "가득 아님") return Of(s.Subject) + " 체력이 가득하지 않게 되었을 때";
     if (s.Op == "1") return Of(s.Subject) + " 체력이 「1」이 되었을 때";
     if (s.Op == "감소") return Of(s.Subject) + " 체력이 감소했을 때";
     return Of(s.Subject) + " 체력이 " + s.Top + "/" + s.Bottom + " 이하가 되었을 때";
    case "회복": return Owner(s.Subject) + " 체력을 회복했을 때";
    case "날씨": return s.Place.Length == 0 ? "날씨가 있을 때" : "날씨가 「" + s.Place + "」일 때";
    case "날씨 변화": return s.Place.Length == 0 ? "날씨가 변화했을 때" : "날씨가 「" + s.Place + "」" + Particle(s.Place, "이", "가") + " 되었을 때";
    case "장소": return Rooms.Contains(s.Place) ? "「" + s.Place + "」" + Particle(s.Place, "이", "가") + " 전개되어 있을 때" : "「" + s.Place + "」일 때";
    case "장소 변화":
     if (Rooms.Contains(s.Place)) return "「" + s.Place + "」" + Particle(s.Place, "이", "가") + (s.Release ? " 해제되었을 때" : " 전개되었을 때");
     return "「" + s.Place + "」" + Particle(s.Place, "이", "가") + " 되었을 때";
    case "랭크":
     string stat = s.Stats.Length == 0 ? "능력" : s.Stats.SequenceEqual(new[] { 1, 2, 3, 4, 5 }) ? "「전능력치」" : "「" + string.Join("/", s.Stats.Select(i => StatNames[i])) + "」";
     return (s.ByOpponent ? "상대에 의해 " : "") + Of(s.Subject) + " " + stat + Particle(stat, "이", "가") + " " + (s.Rise ? (s.Big ? "크게 올랐을 때" : "올랐을 때") : "내려갔을 때");
   }
   return CanonicalCondition(s);
  }

  // ===== 상태 조건(지금 성립하는가) =====
  static bool SelfSubject(string subject) { return subject == "자신" || subject == "아군"; }
  public static IEnumerable<string> ActiveStates(Fighter f) {
   if (f == null) yield break;
   if (f.HP <= 0) { yield return "빈사"; yield break; }
   if (f.Status != "정상") yield return f.Status;
   foreach (var rule in StateEffects.Table) if (rule.Category != "상태이상" && Mechanics.HasCondition(f, rule.Name)) yield return rule.Name;
  }
  static bool HasState(Fighter f, string[] names) {
   if (f == null) return false;
   var active = ActiveStates(f).ToList();
   return names.Any(n => n == "상태이상" ? f.HP > 0 && f.Status != "정상" : active.Contains(n));
  }
  static bool WeatherIs(string actual, string wanted) {
   if (actual == wanted) return true;
   if (wanted == "비") return actual == "강한 비";
   if (wanted == "쾌청") return actual == "큰가뭄";
   if (wanted == "눈") return actual == "싸라기눈";
   if (wanted == "싸라기눈") return actual == "눈";
   return false;
  }
  public static bool Holds(TriggerSpec s, Fighter owner, Fighter opponent, Rules rules) {
   if (owner == null || rules == null) return false;
   if (AbaddonTriggers.IsMine(s)) return AbaddonTriggers.Holds(s, owner, opponent, rules);
   var target = s.Subject == "상대" ? opponent : owner;
   switch (s.Module) {
    case "종족치 등급": return SpeciesGradeTrigger.Holds(s, target);
    case "턴 조건": return TurnConditions.Holds(s, owner, rules);
    case "능력치 비교":
     if (target == null || !target.OnField || target.HP <= 0) return false;
     var other = ReferenceEquals(target, owner) ? opponent ?? target : owner;
     return CompareStats(target, other, s, rules);
    case "상태":
     if (s.Subject == "자신과 상대") return HasState(owner, s.Names) && HasState(opponent, s.Names);
     return target != null && (s.Names.Contains("빈사") || target.HP > 0) && HasState(target, s.Names);
    case "체력":
     if (target == null || target.HP <= 0) return false;
     int max = Mechanics.MaxHP(target, (ReferenceEquals(target, owner) ? opponent : owner) ?? target, rules);
     if (s.Op == "가득") return target.HP >= max;
     if (s.Op == "가득 아님") return target.HP < max;
     if (s.Op == "이상") return (long)target.HP * s.Bottom >= (long)max * s.Top;
     return (long)target.HP * s.Bottom <= (long)max * s.Top;
    case "날씨": {
     string w = Mechanics.Weather(owner, opponent ?? owner, rules);
     return s.Place.Length == 0 ? w != "없음" : WeatherIs(w, s.Place);
    }
    case "장소":
     if (s.Place == "트릭룸") return rules.TrickRoom > 0;
     if (s.Place == "중력") return rules.Gravity > 0;
     return rules.Terrain == s.Place;
   }
   return HoldsCondition(s, owner, opponent, rules);
  }
  // 비교 조건이 능력치 배율 자체를 바꾸어도 자기 조건을 재귀 평가하지 않는다.
  [ThreadStatic] static HashSet<TriggerSpec> comparingStats;
  static bool CompareStats(Fighter target, Fighter other, TriggerSpec spec, Rules rules) {
   if (comparingStats == null) comparingStats = new HashSet<TriggerSpec>();
   if (!comparingStats.Add(spec)) return false;
   try {
    int left = Engine.Effective(target, other, spec.Stats[0], rules);
    int right = Engine.Effective(target, other, spec.Stats[1], rules);
    return spec.Op == "높음" ? left > right : left <= right;
   } finally { comparingStats.Remove(spec); }
  }

  // ===== 사건 대조 =====
  static bool MatchesState(TriggerSpec s, TriggerFact f) {
   bool self = SelfSubject(s.Subject);
   switch (s.Module) {
    case "상태 됨":
     return f.Kind == "상태 됨" && f.Self == self && (s.Names.Contains(f.Method) || s.Names.Contains("상태이상") && IsStatus(f.Method) && f.Method != "상태이상");
    case "상태 해제":
     return f.Kind == "상태 해제" && f.Self == self && (s.Names.Contains(f.Method) || s.Names.Contains("상태이상") && IsStatus(f.Method));
    case "체력 변화":
     if (f.Kind != "체력" || f.Self != self || f.Max <= 0) return false;
     if (s.Op == "가득") return f.Before < f.Max && f.After2 >= f.Max;
     if (s.Op == "가득 아님") return f.Before >= f.Max && f.After2 < f.Max;
     if (s.Op == "1") return f.Before != 1 && f.After2 == 1;
     if (s.Op == "감소") return f.After2 < f.Before;
     return (long)f.Before * s.Bottom > (long)f.Max * s.Top && (long)f.After2 * s.Bottom <= (long)f.Max * s.Top && f.After2 > 0;
    case "회복": return f.Kind == "회복" && f.Self == self;
    case "날씨 변화": return f.Kind == "날씨" && (s.Place.Length == 0 || WeatherIs(f.Method, s.Place));
    case "장소 변화": return f.Kind == "장소" && f.Method == s.Place + (Rooms.Contains(s.Place) ? (s.Release ? " 해제" : " 전개") : "");
    case "랭크":
     if (f.Kind != "랭크" || f.Self != self || f.Delta == 0) return false;
     if (s.Rise != f.Delta > 0) return false;
     if (s.Big && Math.Abs(f.Delta) < 2) return false;
     if (s.ByOpponent && !f.ByOpponent) return false;
     return s.Stats.Length == 0 || s.Stats.Contains(f.Stat);
   }
   return MatchesCondition(s, f);
  }
 }

 // 상태·체력·랭크·날씨·장소의 전후 비교. 바뀐 것이 있으면 사건을 낸다(원인과 관계없이 한 곳에서 판정)
 public static class StateWatch {
  static int[] Stages(Fighter f) { return new[] { 0, f.Stages[1], f.Stages[2], f.Stages[3], f.Stages[4], f.Stages[5], f.AccuracyStage, f.EvasionStage }; }
  static string Place(Rules r) { return r.Weather + "|" + r.Terrain + "|" + (r.TrickRoom > 0 ? 1 : 0) + "|" + (r.Gravity > 0 ? 1 : 0); }
  // 등장할 때 지금 값으로 맞춘다(이미 걸린 상태로 나와도 「되었을 때」가 아니다)
  public static void Reset(Fighter f) {
   if (f == null) return;
   var c = f.Conditions;
   c.WatchStates = string.Join("|", TriggerModules.ActiveStates(f));
   c.WatchHP = f.HP;
   c.WatchStages = string.Join(",", Stages(f));
   AbaddonTriggers.ResetTypes(f);
  }
  // 효과 범위를 시작할 때 비교 기준이 없는 포켓몬·장소는 지금 값으로 맞춘다
  public static void Prime(Fighter[] fighters, Rules rules) {
   foreach (var f in fighters ?? new Fighter[0]) if (f != null && f.Data != null && (f.Conditions.WatchHP < 0 || f.Conditions.WatchStates == null || f.Conditions.WatchStages == null)) Reset(f);
   if (rules != null && rules.WatchPlace == null) rules.WatchPlace = Place(rules);
  }
  public static void CheckCurrent(Action<string> log) {
   var scope = EffectScope.Current;
   if (scope == null || log == null) return;
   Check(scope.Fighters, scope.Rules, log);
  }
  public static void Check(Fighter[] fighters, Rules rules, Action<string> log) {
   if (fighters == null || fighters.Length != 2 || log == null) return;
   for (int i = 0; i < 2; i++) {
    var f = fighters[i]; var other = fighters[1 - i];
    if (f == null || f.Data == null) continue;
    var c = f.Conditions;
    if (c.WatchHP < 0 || c.WatchStates == null || c.WatchStages == null) { Reset(f); continue; }
    var before = c.WatchStates.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
    var now = TriggerModules.ActiveStates(f).ToArray();
    int hpBefore = c.WatchHP;
    var stagesBefore = c.WatchStages.Split(',').Select(int.Parse).ToArray();
    var stagesNow = Stages(f);
    string typesBefore = c.WatchTypes;
    Reset(f);
    int max = rules == null ? 0 : Mechanics.MaxHP(f, other ?? f, rules);
    if (f.HP != hpBefore) TriggerModules.Raise(new TriggerFact { Kind = "체력", Before = hpBefore, After2 = f.HP, Max = max }, f, other, f.Data.name + "의 HP " + hpBefore + "→" + f.HP, log);
    foreach (string name in now.Where(x => !before.Contains(x))) TriggerModules.Raise(new TriggerFact { Kind = "상태 됨", Method = name }, f, other, f.Data.name + ": 「" + name + "」 상태가 되었다", log);
    if (f.HP > 0) foreach (string name in before.Where(x => !now.Contains(x) && x != "빈사")) TriggerModules.Raise(new TriggerFact { Kind = "상태 해제", Method = name }, f, other, f.Data.name + ": 「" + name + "」 상태가 해제되었다", log);
    for (int k = 1; k < stagesNow.Length && k < stagesBefore.Length; k++)
     if (stagesNow[k] != stagesBefore[k]) TriggerModules.Raise(new TriggerFact { Kind = "랭크", Stat = k, Delta = stagesNow[k] - stagesBefore[k], ByOpponent = c.StageByOpponent }, f, other, f.Data.name + "의 랭크 변화 (" + k + ": " + stagesBefore[k] + "→" + stagesNow[k] + ")", log);
    AbaddonTriggers.TypesChanged(f, other, typesBefore, log);
   }
   if (rules == null) return;
   string place = Place(rules);
   if (rules.WatchPlace == null) { rules.WatchPlace = place; return; }
   if (rules.WatchPlace == place) return;
   var old = rules.WatchPlace.Split('|');
   rules.WatchPlace = place;
   var a = fighters[0]; var b = fighters[1];
   if (old[0] != rules.Weather && rules.Weather != "없음") TriggerModules.Raise(new TriggerFact { Kind = "날씨", Method = rules.Weather }, a, b, "날씨가 「" + rules.Weather + "」가 되었다", log);
   if (old[1] != rules.Terrain && rules.Terrain != "없음") TriggerModules.Raise(new TriggerFact { Kind = "장소", Method = rules.Terrain }, a, b, "「" + rules.Terrain + "」가 되었다", log);
   if (old[2] != (rules.TrickRoom > 0 ? "1" : "0")) TriggerModules.Raise(new TriggerFact { Kind = "장소", Method = "트릭룸" + (rules.TrickRoom > 0 ? " 전개" : " 해제") }, a, b, "「트릭룸」" + (rules.TrickRoom > 0 ? " 전개" : " 해제"), log);
   if (old[3] != (rules.Gravity > 0 ? "1" : "0")) TriggerModules.Raise(new TriggerFact { Kind = "장소", Method = "중력" + (rules.Gravity > 0 ? " 전개" : " 해제") }, a, b, "「중력」" + (rules.Gravity > 0 ? " 전개" : " 해제"), log);
  }
 }
}
