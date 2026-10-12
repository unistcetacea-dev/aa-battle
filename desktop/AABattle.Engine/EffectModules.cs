using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 // 편집기 구조화 효과 모듈의 표준 문장과 엔진 해석을 한곳에 둔다.
 // 수집 원문의 같은 뜻 변형은 EffectModules.Normalize가 표준 문장으로 바꾼다(EffectNormalizer.Normalize가 호출).
 // 실행기와 포텐셜 도감은 표준 문장만 해석한다. 해석하지 못한 문장은 "효과 판정 대기"로 남는다.

 // 기술 조건: 「불꽃/얼음」「물리」「타입 불일치」「효과가 굉장」「단일」「위력 40 이하」「접촉」「기술:베놈쇼크」
 public sealed class MoveCondition {
  public static readonly string[] TypeNames = { "노말", "불꽃", "물", "풀", "전기", "얼음", "격투", "독", "땅", "비행", "에스퍼", "벌레", "바위", "고스트", "드래곤", "악", "강철", "페어리" };
  public List<string> Types = new List<string>();
  public string Category = "";       // 물리 · 특수 · 변화 · 공격(물리+특수)
  public string Stab = "";           // 타입 일치 · 타입 불일치
  public string Effectiveness = "";  // 효과가 굉장(매우 포함) · 효과가 별로(매우 포함) · 효과 보통
  public string Range = "";          // 단일 · 전체
  public int PowerMax = -1, PowerMin = -1;
  public bool PriorityPositive;      // 「우선도 +」 선공기
  public List<string> Tags = new List<string>();
  public List<string> Names = new List<string>();
  public bool Placeholder;           // 빈 괄호 「」·「○」: 타입 자리 표시(실행하지 않음)
  public bool Empty { get { return Types.Count == 0 && Category == "" && Stab == "" && Effectiveness == "" && Range == "" && PowerMax < 0 && PowerMin < 0 && !PriorityPositive && Tags.Count == 0 && Names.Count == 0 && !Placeholder; } }

  public string Text {
   get {
    var parts = new List<string>();
    if (Placeholder) parts.Add("「○」");
    if (Types.Count > 0) parts.Add("「" + string.Join("/", Types) + "」");
    if (Category.Length > 0) parts.Add("「" + Category + "」");
    if (Stab.Length > 0) parts.Add("「" + Stab + "」");
    if (Effectiveness.Length > 0) parts.Add("「" + Effectiveness + "」");
    if (Range.Length > 0) parts.Add("「" + Range + "」");
    if (PowerMax >= 0) parts.Add("「위력 " + PowerMax + " 이하」");
    if (PowerMin >= 0) parts.Add("「위력 " + PowerMin + " 이상」");
    if (PriorityPositive) parts.Add("「우선도 +」");
    parts.AddRange(Tags.Select(x => "「" + x + "」"));
    parts.AddRange(Names.Select(x => "「기술:" + x + "」"));
    return string.Concat(parts);
   }
  }

  static string TypeName(string value) { value = value.Trim(); if (value == "버레") return "벌레"; if (value == "에스파") return "에스퍼"; return value; }

  // 조건 문자열(괄호 토큰 + 허용된 수식어)을 읽는다. 모르는 말이 섞이면 false
  public static bool TryParse(string raw, out MoveCondition condition) {
   condition = new MoveCondition();
   string text = (raw ?? "").Trim();
   var tokens = Regex.Matches(text, "「([^」]*)」").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
   string outside = Regex.Replace(text, "「[^」]*」", " ");
   outside = Regex.Replace(outside, @"\s+", " ").Trim();
   foreach (string word in outside.Split(' ').Where(x => x.Length > 0)) {
    string w = word.Replace(" ", "");
    if (w == "타입불일치인" || w == "타입불일치") condition.Stab = "타입 불일치";
    else if (w == "타입일치인" || w == "타입일치") condition.Stab = "타입 일치";
    else if (w == "타입" || w == "불일치인" || w == "불일치") { if (w.StartsWith("불일치")) condition.Stab = "타입 불일치"; }
    else if (new[] { "한", "인", "의", "등의", "효과가", "부여된", "있는", "효과를", "가진", "효과", "계통의", "계통" }.Contains(w)) { }
    else if (!condition.Apply(w.TrimEnd('의'), false)) return false;
   }
   if (Regex.IsMatch(outside.Replace(" ", ""), "타입불일치")) condition.Stab = "타입 불일치";
   foreach (string token in tokens) condition.Apply(token, true);
   return true;
  }

  // 조건 토큰 하나를 반영한다. bracketed=false(괄호 밖 단어)면 태그로 받지 않고 모르는 말이면 false
  public bool Apply(string token, bool bracketed) {
   string t = (token ?? "").Trim(), compact = t.Replace(" ", "");
   if (bracketed && (compact.Length == 0 || compact == "○" || compact.Trim('?').Length == 0)) { Placeholder = true; return true; }
   if (compact.Length == 0) return !bracketed;
   if (compact.StartsWith("기술:") && bracketed) { Names.Add(t.Substring(t.IndexOf(':') + 1).Trim()); return true; }
   string core = compact.EndsWith("기술") && compact.Length > 2 ? compact.Substring(0, compact.Length - 2) : compact;
   var parts = core.Split('/').Select(TypeName).ToArray();
   if (parts.All(x => TypeNames.Contains(x))) { foreach (var x in parts) if (!Types.Contains(x)) Types.Add(x); return true; }
   if (core == "물리") { Category = "물리"; return true; }
   if (core == "특수") { Category = "특수"; return true; }
   if (core == "변화") { Category = "변화"; return true; }
   if (core == "공격") { Category = "공격"; return true; }
   if (core == "타입일치") { Stab = "타입 일치"; return true; }
   if (core == "타입불일치") { Stab = "타입 불일치"; return true; }
   if (core.StartsWith("효과")) {
    if (core.Contains("굉장")) { Effectiveness = "효과가 굉장"; return true; }
    if (core.Contains("별로")) { Effectiveness = "효과가 별로"; return true; }
    if (core.Contains("보통")) { Effectiveness = "효과 보통"; return true; }
   }
   if (core == "단일" || core == "단일공격") { Range = "단일"; if (core == "단일공격") Category = "공격"; return true; }
   if (core == "전체" || core == "전체공격") { Range = "전체"; if (core == "전체공격") Category = "공격"; return true; }
   var power = Regex.Match(core, @"^위력:?(?<n>\d+)(?<dir>이하|이상)$");
   if (power.Success) { int n = int.Parse(power.Groups["n"].Value); if (power.Groups["dir"].Value == "이하") PowerMax = n; else PowerMin = n; return true; }
   if (core == "우선도+" || core == "우선도가+" || core == "선공") { PriorityPositive = true; return true; }
   if (!bracketed) return false;
   Tags.Add(core);
   return true;
  }

  // 기술·사용자·실제 상성 배율로 조건을 판정한다
  public bool Matches(Move move, Fighter user, double typeEffect) {
   if (move == null || Placeholder) return false;
   var types = move.types ?? new string[0];
   if (Types.Count > 0 && !types.Any(Types.Contains)) return false;
   if (Category == "공격" && move.category == "변화") return false;
   if (Category.Length > 0 && Category != "공격" && move.category != Category) return false;
   bool stab = user != null && types.Any(t => (user.Data.types ?? new string[0]).Contains(t));
   if (Stab == "타입 일치" && !stab) return false;
   if (Stab == "타입 불일치" && stab) return false;
   if (Effectiveness == "효과가 굉장" && !(typeEffect > 1)) return false;
   if (Effectiveness == "효과가 별로" && !(typeEffect > 0 && typeEffect < 1)) return false;
   if (Effectiveness == "효과 보통" && typeEffect != 1) return false;
   bool spread = (move.target ?? "").Contains("전체") || (move.tags ?? new string[0]).Contains("전체");
   if (Range == "전체" && !spread) return false;
   if (Range == "단일" && spread) return false;
   if (PowerMax >= 0 && !(move.power > 0 && move.power <= PowerMax)) return false;
   if (PowerMin >= 0 && move.power < PowerMin) return false;
   if (PriorityPositive && move.priority <= 0) return false;
   foreach (string tag in Tags) if (!(move.tags ?? new string[0]).Contains(tag) && !(move.effect ?? "").Contains(tag) && move.name != tag) return false;
   if (Names.Count > 0 && !Names.Contains(move.name)) return false;
   return true;
  }
 }

 // 수치 배율: 대상(자신/상대/아군) · 단계(능력치/위력/주는 대미지/받는 대미지/명중) · 기술 조건 · 강화·약화 배율
 public sealed class ScalarEffect {
  public string Subject = "자신";
  public string Stage = "능력치";      // 능력치 · 위력 · 주는 대미지 · 받는 대미지 · 명중
  public int[] Stats = new int[0];     // 능력치 단계: Engine.Names 색인(1~5)
  public bool HighestStat, LowestStat;
  public MoveCondition Condition = new MoveCondition();
  public double Rate = 1;
  public string Text = "";
 }

 // 종족치 변경: 지정 등급으로 변경 · 지정 등급까지 상승·하락 · N랭크 상승 · 가장 높은/낮은 종족치 복사 · 다른 종족치 복사 · 직접 수치 · 트레이너 자질 등급까지
 public sealed class SpeciesEffect {
  public string Subject = "자신";
  public int[] Stats = new int[0];     // 0(체력)~5
  public string Operation = "";        // 변경 · 상승 · 하락 · 랭크 · 최고 복사 · 최저 복사 · 복사 · 수치 · 트레이너
  public string Grade = "";
  public int Value = -1, Ranks, Source = -1;
  public string Aptitude = "";         // 지시 · 육성 · 통솔 · 능력
 }

 // 최대 HP의 1/N: 회복 · 대미지(간접) · 소비(대가)
 public sealed class FractionEffect {
  public string Target = "자신", Action = "회복";
  public int Denominator = 1;
  public bool CanFaint = true;
 }

 // 능력 랭크 변화
 public sealed class RankEffect {
  public string Subject = "자신";
  public string Mode = "지정";         // 지정 · 임의 · 랜덤 · 최고 · 최저 · 전능력치
  public int[] Stats = new int[0];     // 1~5 능력, 6 명중, 7 회피, 8 C(급소)
  public int Amount;
 }

 public static class EffectModules {
  // 정규식 캐시(기본 15개)를 넘는 패턴을 쓰므로 늘린다
  static EffectModules() { if (Regex.CacheSize < 256) Regex.CacheSize = 256; }
  static Regex stateNames;
  static Regex StateNames {
   get {
    if (stateNames == null) {
     var names = StateEffects.Table.SelectMany(r => new[] { r.Name }.Concat(r.Aliases)).Distinct().OrderByDescending(x => x.Length).Select(Regex.Escape);
     stateNames = new Regex(@"(?<=^|[\s,을를])(?<!「)(?<n>" + string.Join("|", names) + @")(?=\s*상태(?:로|가))", RegexOptions.Compiled);
    }
    return stateNames;
   }
  }
  const string Subject = @"(?<s>자신|상대|아군(?:\s*1체|\s*전체)?)";
  const string Rate = @"(?<r>[0-9]+(?:\.[0-9]+)?)";

  public static int StatIndex(string name) {
   switch ((name ?? "").Replace(" ", "")) {
    case "체력": case "HP": return 0;
    case "공격": case "공": return 1;
    case "방어": case "방": return 2;
    case "특공": return 3;
    case "특방": return 4;
    case "속도": case "속": case "스피드": return 5;
    case "명중": return 6;
    case "회피": return 7;
    case "C": case "급소": return 8;
    default: return -1;
   }
  }
  static int[] StatList(string token) {
   var list = token.Split('/').Select(StatIndex).ToArray();
   return list.All(x => x >= 0) ? list : null;
  }
  static string F(double value) { return value.ToString("0.##", CultureInfo.InvariantCulture); }
  static string Subj(string s) { s = Regex.Replace(s ?? "자신", @"\s+", " ").Trim(); return s.Length == 0 ? "자신" : s; }
  static string SubjectParticle(string s) { return s.EndsWith("신") || s.EndsWith("군") || s.EndsWith("체") ? "이" : "가"; }

  // 배율 동사 → (강화/약화, 배율)
  static bool TryVerb(string verb, out string action, out double rate) {
   action = ""; rate = 0;
   string v = verb.Replace(" ", "");
   Match m;
   if (Regex.IsMatch(v, @"^반감(한다|시킨다|된다)$")) { action = "약화"; rate = .5; return true; }
   if (Regex.IsMatch(v, @"^배화(한다|된다)$")) { action = "강화"; rate = 2; return true; }
   m = Regex.Match(v, @"^(?<a>강화|약화|완화|저하|감소)\(" + Rate + @"배\)(한다|시킨다|된다)$");
   if (m.Success) { rate = double.Parse(m.Groups["r"].Value, CultureInfo.InvariantCulture); action = m.Groups["a"].Value == "강화" ? "강화" : "약화"; return rate > 0; }
   m = Regex.Match(v, "^" + Rate + @"배(가된다|로한다|로된다|가된다)$");
   if (m.Success) { rate = double.Parse(m.Groups["r"].Value, CultureInfo.InvariantCulture); action = rate >= 1 ? "강화" : "약화"; return rate > 0; }
   return false;
  }

  static string Scalar(string target, string action, double rate) { return EffectNormalizer.Canonical(target, action, rate); }
  static string Cond(MoveCondition c) { return c.Empty ? "" : c.Text + " "; }

  // 수집 원문 변형 → 표준 문장. 바꿀 수 없으면 null
  static readonly string[] StatWords = { "공격", "방어", "특공", "특방", "속도", "명중", "회피" };
  static bool AllStats(string value) { return value.Split('/').All(x => StatIndex(x) >= 0); }
  static bool AllTypes(string value) { return value.Split('/').All(x => MoveCondition.TypeNames.Contains(x.Trim())); }
  // 표기 통일: 각주 표시 제거, 『타입』→「타입」, 이어 붙은 능력·타입 괄호 병합, 전능력→전능력치, 괄호 없는 상태 이름
  public static string Prepare(string value) {
   string text = (value ?? "").Trim();
   text = Regex.Replace(text, @"\s*\.?\s*\*\s*\d+\s*$", "");
   text = text.TrimEnd('.', '。', '．').Trim();
   text = Regex.Replace(text, "『([^』]+)』", m => AllTypes(m.Groups[1].Value) ? "「" + m.Groups[1].Value + "」" : m.Value);
   text = Regex.Replace(text, "「([^」]+)」\\s*「([^」]+)」(?:\\s*「([^」]+)」)?", m => {
    var items = new[] { m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value }.Where(x => x.Length > 0).ToArray();
    return items.All(AllStats) || items.All(AllTypes) ? "「" + string.Join("/", items) + "」" : m.Value;
   });
   text = Regex.Replace(text, "전능력(?!치)", "전능력치");
   text = Regex.Replace(text, @"「위력\s*:?\s*(\d+)」\s*(이하|이상)(?:인|의)?", "「위력 $1 $2」");
   text = Regex.Replace(text, @"위력\s*「(\d+)」\s*(이하|이상)(?:인|의)?", "「위력 $1 $2」");
   text = Regex.Replace(text, @"[.．。]\s*(이 대미지로는 빈사로 할 수 없다)", " $1");
   text = Regex.Replace(text, @"우선도가\s*「\+」\s*인", "「우선도 +」");
   text = StateNames.Replace(text, m => "「" + m.Groups["n"].Value + "」");
   return text;
  }

  public static string Normalize(string clean) {
   var activation = Activation.Match((clean ?? "").Trim());
   if (activation.Success) { string inner = Normalize(clean.Trim().Substring(activation.Length)); return inner == null ? null : activation.Groups[1].Value + " " + inner; }
   string text = Prepare(clean);
   if (text.Length == 0) return null;
   // 기간 접두어는 떼었다가 다시 붙인다
   string prefix = "";
   var duration = Regex.Match(text, @"^(?:(?<n>\d+)\s*T\s*(?:간|동안)|(?<end>T\s*종료\s*시\s*까지))\s*,?\s*");
   if (duration.Success && StateEffects.Parse(text) != null) return text + ".";
   if (duration.Success) { prefix = duration.Groups["end"].Success ? "T종료시까지 " : duration.Groups["n"].Value + "T동안 "; text = text.Substring(duration.Length).Trim(); }
   string body = NormalizeBody(text);
   return body == null ? null : prefix + body;
  }

  static string NormalizeBody(string text) {
   Match m; string action; double rate; MoveCondition c;
   // 「상대 기술의」처럼 조사가 빠진 주어
   text = Regex.Replace(text, @"^(자신|상대)\s+(?=기술의|「)", "$1의 ");
   // 「X」의 위력/대미지(괄호 하나가 기술 조건)
   m = Regex.Match(text, "^" + Subject + @"의\s*(?<c>「[^」]+」|공격|물리기술|특수기술)의\s*(?<k>위력|대미지|데미지)(?:을|를|이)\s*(?<v>.+)$");
   if (m.Success && TryVerb(m.Groups["v"].Value, out action, out rate) && MoveCondition.TryParse(m.Groups["c"].Value.StartsWith("「") ? m.Groups["c"].Value : "「" + m.Groups["c"].Value + "」", out c)) {
    string s = Subj(m.Groups["s"].Value);
    return m.Groups["k"].Value == "위력" ? Scalar(s + "의 " + Cond(c) + "기술의 위력", action, rate) : Scalar(s + SubjectParticle(s) + " 주는 " + c.Text + " 기술의 대미지", action, rate);
   }
   // 상대의 (조건)기술로 받는 대미지 → 자신이 받는
   m = Regex.Match(text, @"^상대의\s*(?<c>.*?)\s*기술로\s*받는\s*(?:대미지|데미지)(?:을|를)\s*(?<v>.+)$");
   if (m.Success && TryVerb(m.Groups["v"].Value, out action, out rate) && MoveCondition.TryParse(m.Groups["c"].Value, out c))
    return Scalar("자신이 받는 " + (c.Empty ? "대미지" : c.Text + " 기술의 대미지"), action, rate);
   // 능력치 배율(괄호 없는 능력 이름·주어 생략·명중/회피 포함)
   m = Regex.Match(text, "^(?:" + Subject + @"의\s*)?(?<st>「[^」]+」|전능력치|가장 높은 능력|가장 낮은 능력|공격|방어|특공|특방|속도|명중|회피)(?:을|를)\s*(?<v>(?:강화|약화|저하)\([0-9.]+배\)(?:한다|시킨다))$");
   if (m.Success && TryVerb(m.Groups["v"].Value, out action, out rate)) {
    string st = m.Groups["st"].Value;
    if (st.StartsWith("「") && !AllStats(st.Trim('「', '」'))) st = null;
    else if (StatWords.Contains(st)) st = "「" + st + "」";
    if (st != null) return Scalar(Subj(m.Groups["s"].Value) + "의 " + st, action, rate);
   }
   // 능력 랭크(주어 생략: 오르면 자신, 내리면 상대)
   m = Regex.Match(text, @"^랜덤(?:으로|하게)\s*(?<s>자신|상대)의\s*능력(?:치)?(?:을|를)\s*(?<d>매우 크게|크게)?\s*(?<dir>올린다|내린다)$");
   if (m.Success) return EffectNormalizer.CanonicalRank(m.Groups["s"].Value + "의 랜덤한 능력치", Amount(m.Groups["d"].Value, m.Groups["dir"].Value));
   m = Regex.Match(text, "^(?:" + Subject + @"의\s*)?(?<what>「[^」]+」|임의의 능력|랜덤한 능력치|가장 높은 능력|가장 낮은 능력|전능력치|공격|방어|특공|특방|속도|명중|회피)(?:이|가|을|를)\s*(?<d>매우 크게|크게)?\s*(?<dir>오른다|올린다|내린다)$");
   if (m.Success && (!m.Groups["what"].Value.StartsWith("「") || AllStats(m.Groups["what"].Value.Trim('「', '」')))) {
    string dir = m.Groups["dir"].Value;
    string s = m.Groups["s"].Success && m.Groups["s"].Value.Length > 0 ? Subj(m.Groups["s"].Value) : dir == "내린다" ? "상대" : "자신";
    string what = StatWords.Contains(m.Groups["what"].Value) ? "「" + m.Groups["what"].Value + "」" : m.Groups["what"].Value;
    return EffectNormalizer.CanonicalRank(s + "의 " + what, Amount(m.Groups["d"].Value, dir));
   }
   // 위력
   m = Regex.Match(text, "^(?:" + Subject + @"의\s*)?(?<c>.*?)\s*기술의\s*위력(?:을|이)\s*(?<v>.+)$");
   if (m.Success && TryVerb(m.Groups["v"].Value, out action, out rate) && MoveCondition.TryParse(m.Groups["c"].Value, out c))
    return Scalar(Subj(m.Groups["s"].Value) + "의 " + Cond(c) + "기술의 위력", action, rate);
   m = Regex.Match(text, "^" + Subject + @"의\s*(?<c>「[^」]+」)의\s*위력(?:을|이)\s*(?<v>.+)$");
   if (m.Success && TryVerb(m.Groups["v"].Value, out action, out rate) && MoveCondition.TryParse(m.Groups["c"].Value, out c))
    return Scalar(Subj(m.Groups["s"].Value) + "의 " + Cond(c) + "기술의 위력", action, rate);
   // 주는 대미지: 「자신/상대의 (조건) 기술의 대미지」
   m = Regex.Match(text, "^" + Subject + @"의\s*(?<c>.*?)\s*기술의\s*(?:대미지|데미지)(?:을|를|가|이)\s*(?<v>.+)$");
   if (m.Success && TryVerb(m.Groups["v"].Value, out action, out rate) && MoveCondition.TryParse(m.Groups["c"].Value, out c)) {
    string s = Subj(m.Groups["s"].Value);
    return Scalar(s + SubjectParticle(s) + " 주는 " + (c.Empty ? "대미지" : c.Text + " 기술의 대미지"), action, rate);
   }
   // 받는 대미지: 「(조건) 기술로 받는 대미지」
   m = Regex.Match(text, "^(?:" + Subject + @"(?:이|가)\s*)?(?<c>.*?)\s*기술로\s*받는\s*(?:대미지|데미지)(?:을|를)\s*(?<v>.+)$");
   if (m.Success && TryVerb(m.Groups["v"].Value, out action, out rate) && MoveCondition.TryParse(m.Groups["c"].Value, out c)) {
    string s = Subj(m.Groups["s"].Value);
    return Scalar(s + SubjectParticle(s) + " 받는 " + (c.Empty ? "대미지" : c.Text + " 기술의 대미지"), action, rate);
   }
   // 명중
   m = Regex.Match(text, "^" + Subject + @"의\s*(?<c>.*?)\s*기술의\s*명중률?(?:을|를|이)\s*(?<v>.+)$");
   if (m.Success && TryVerb(m.Groups["v"].Value, out action, out rate) && MoveCondition.TryParse(m.Groups["c"].Value, out c))
    return Scalar(Subj(m.Groups["s"].Value) + "의 " + Cond(c) + "기술의 명중", action, rate);
   // 능력치 배율의 「N배로 한다 / N배가 된다」
   m = Regex.Match(text, "^(?:" + Subject + @"의\s*)?(?<st>「[^」]+」|전능력치|공격|방어|특공|특방|속도)(?:을|를|이|가)\s*(?<v>" + Rate + @"배(?:로 한다|가 된다))$");
   if (m.Success && TryVerb(m.Groups["v"].Value, out action, out rate) && (m.Groups["st"].Value == "전능력치" || StatList(m.Groups["st"].Value.Trim('「', '」')) != null)) {
    string st = m.Groups["st"].Value.StartsWith("「") || m.Groups["st"].Value == "전능력치" ? m.Groups["st"].Value : "「" + m.Groups["st"].Value + "」";
    return Scalar(Subj(m.Groups["s"].Value) + "의 " + st, action, rate);
   }
   // F4b 타입·상성
   string typed = TypeRules.Normalize(text);
   if (typed != null) return typed;
   // G 방호·회피·관통
   string guard = NormalizeGuard(text);
   if (guard != null) return guard;
   // F4b 추가행동
   string extra = NormalizeExtra(text);
   if (extra != null) return extra;
   // F4a 장소·교대 모듈
   string place = NormalizePlace(text);
   if (place != null) return place;
   // 종족치
   string species = NormalizeSpecies(text);
   if (species != null) return species;
   // 최대 HP의 1/N
   string fraction = NormalizeFraction(text);
   if (fraction != null) return fraction;
   // 주어 없는 능력 랭크: 「「공격」이 오른다」 → 자신
   m = Regex.Match(text, @"^(?<st>「[^」]+」)(?:이|가)\s*(?<d>(?:매우\s*)?크게\s*)?오른다$");
   if (m.Success && StatList(m.Groups["st"].Value.Trim('「', '」')) != null) return "자신의 " + m.Groups["st"].Value + "이 " + m.Groups["d"].Value.Trim() + (m.Groups["d"].Value.Trim().Length > 0 ? " " : "") + "오른다.";
   return null;
  }

  // 받침이 있으면 「으로」, 없거나 ㄹ 받침이면 「로」
  public static string Ro(string word) { if (string.IsNullOrEmpty(word)) return "로"; char ch = word[word.Length - 1]; if (ch < '가' || ch > '힣') return "로"; int last = (ch - '가') % 28; return last == 0 || last == 8 ? "로" : "으로"; }
  public static readonly string[] TerrainNames = { "그래스필드", "일렉트릭필드", "미스트필드", "사이코필드" };
  static readonly string[] MajorStatuses = { "독", "맹독", "마비", "화상", "얼음", "잠듦", "동상" };

  // 장소 전개·설치물·강제교대·부유·상태이상 면역 변형 → 표준 문장
  static string NormalizePlace(string text) {
   Match m;
   string optional = @"(?<opt>한다|할\s*수\s*있다)";
   string Opt(Match x) { return x.Groups["opt"].Value.Contains("있다") ? "할 수 있다." : "한다."; }
   m = Regex.Match(text, @"^날씨를\s*(?:(?<n>\d+)\s*T\s*(?:간|동안)\s*)?「(?<w>[^」]+)」\s*(?:로|으로)\s*" + optional + "$");
   if (m.Success && Mechanics.Weathers.Contains(m.Groups["w"].Value)) return (m.Groups["n"].Success ? m.Groups["n"].Value + "T동안 " : "") + "날씨를 「" + m.Groups["w"].Value + "」" + Ro(m.Groups["w"].Value) + " " + Opt(m);
   m = Regex.Match(text, @"^(?:필드|지형)를\s*(?:(?<n>\d+)\s*T\s*(?:간|동안)\s*)?「(?<t>[^」]+)」\s*(?:상태)?\s*(?:로|으로)\s*" + optional + "$");
   if (!m.Success) m = Regex.Match(text, @"^(?:필드에\s*)?(?:(?<n>\d+)\s*T\s*(?:간|동안)\s*)?「(?<t>[^」]+)」(?:을|를)\s*전개(?<opt>한다|할\s*수\s*있다)$");
   if (m.Success) {
    string t = m.Groups["t"].Value, n = m.Groups["n"].Success ? m.Groups["n"].Value + "T동안 " : "";
    string opt = m.Groups["opt"].Value.Contains("있다") ? "할 수 있다." : "한다.";
    if (TerrainNames.Contains(t)) return n + "지형을 「" + t + "」" + Ro(t) + " " + opt;
    if (t == "중력" || t == "트릭룸") return n + "「" + t + "」을 전개" + opt;
   }
   m = Regex.Match(text, @"^적진에\s*「(?<h>스텔스록|압정뿌리기|압정|독압정|끈적끈적네트)」(?:을|를)\s*설치" + optional + "$");
   if (m.Success) { string h = m.Groups["h"].Value == "압정" ? "압정뿌리기" : m.Groups["h"].Value; return "적진에 「" + h + "」을 설치" + Opt(m); }
   if (Regex.IsMatch(text, @"^자(?:진|신의\s*진영)의\s*설치물을\s*(?:해제|제거|철거)한다$")) return "자진의 설치물을 제거한다.";
   m = Regex.Match(text, @"^(?:상대를\s*)?강제교대\s*(?:시킨다|한다|(?<opt>시킬\s*수\s*있다|할\s*수\s*있다))$");
   if (m.Success) return "상대를 강제교대시" + (m.Groups["opt"].Success ? "킬 수 있다." : "킨다.");
   if (Regex.IsMatch(text, @"^(?:자신은\s*)?(?:상대(?:로부터의|에\s*의해서?)\s*)?(?:「강제교대」|강제교대)\s*(?:되지\s*않는다|를\s*받지\s*않는다|가\s*되지\s*않는다)$")) return "자신은 강제교대되지 않는다.";
   if (Regex.IsMatch(text, @"^(?:자신은\s*)?공중에\s*떠오른다$")) return "자신은 공중에 떠오른다.";
   m = Regex.Match(text, @"^(?:자신은\s*)?(?<w>가장\s*먼저|상대보다\s*먼저|먼저)\s*행동(?<opt>한다|할\s*수\s*있다)$");
   if (m.Success) return "자신은 " + (m.Groups["w"].Value.StartsWith("가장") ? "가장" : "상대보다") + " 먼저 행동" + (m.Groups["opt"].Value.Contains("있다") ? "할 수 있다." : "한다.");
   m = Regex.Match(text, @"^(?:자신은\s*)?「(?<s>[^」]+)」\s*상태(?:가|에)\s*(?:되지|걸리지)\s*않는다$");
   if (m.Success && m.Groups["s"].Value.Split('/').All(x => MajorStatuses.Contains(x.Trim()))) return "자신은 「" + m.Groups["s"].Value + "」상태가 되지 않는다.";
   return null;
  }

  // 방호(무효화)·회피·방호 관통·데이터 해석 무효 변형 → 표준 문장. 확률은 문장 안(공격마다 판정)으로 옮긴다
  // 「자동」「선행」「1/자동/」 같은 발동 방식 접두어는 떼었다가 다시 붙인다
  static readonly Regex Activation = new Regex(@"^((?:\d+/)?(?:자동|선행|T시)(?:/\S+)?)\s+");
  public static string NormalizeGuard(string clean) {
   var activation = Activation.Match((clean ?? "").Trim());
   if (activation.Success) { string inner = NormalizeGuard(clean.Trim().Substring(activation.Length)); return inner == null ? null : activation.Groups[1].Value + " " + inner; }
   string text = Prepare(clean);
   string prefix = "";
   var duration = Regex.Match(text, @"^(?:(?<n>\d+)\s*T\s*(?:간|동안)|(?<end>T\s*종료\s*시\s*까지))\s*,?\s*");
   if (duration.Success) { prefix = duration.Groups["end"].Success ? "T종료시까지 " : duration.Groups["n"].Value + "T동안 "; text = text.Substring(duration.Length).Trim(); }
   if (Regex.IsMatch(text, @"^(?:자신에\s*대한|상대로부터의|상대의)\s*(?:「데이터\s*해석」|데이터\s*해석)을\s*무효화한다$")) return prefix + "자신에 대한 데이터 해석을 무효화한다.";
   var pierce = Regex.Match(text, @"^(?:자신의\s*(?<c>(?:「[^」]+」\s*)*)\s*기술이\s*)?상대(?:의)?\s*(?<tr>트레이너의?\s*)?(?:『방호\s*포텐셜』|방호\s*포텐셜)을\s*(?:무시|무효화|관통)한다$");
   if (pierce.Success) { MoveCondition pc; if (!MoveCondition.TryParse(pierce.Groups["c"].Value, out pc)) return null; return prefix + "자신의 " + (pc.Empty ? "" : pc.Text + " ") + "기술이 상대의 " + (pierce.Groups["tr"].Success ? "트레이너 " : "") + "방호 포텐셜을 무시한다."; }
   string probability = "";
   string body; string label;
   if (EffectNormalizer.TryProbabilityPrefix(text, out label, out body)) { probability = label == "드물게" ? "드물게" : label + "로"; text = body.Trim(); }
   var m = Regex.Match(text, @"^(?:상대(?:의)?\s*)?(?<c>.*?)\s*(?<o>기술|공격)?\s*(?:을|를)\s*(?<p>드물게|저확률로|중확률로)?\s*(?<v>무효화|회피|반사)한다$");
   if (!m.Success) return null;
   string raw = m.Groups["c"].Value.Trim();
   if (!m.Groups["o"].Success && !raw.Contains("기술") && !Regex.IsMatch(raw, "^(?:「[^」]*」)+$")) return null;
   if (raw.Contains("「데이터") || raw.Contains("포텐셜") || raw.Contains("『")) return null;
   MoveCondition c;
   string tokens = Regex.Replace(raw, @"(?<=」)\s*기술$|(?<=」)(?:의)?$", "");
   if (!MoveCondition.TryParse(tokens, out c)) return null;
   if (m.Groups["o"].Value == "공격" && c.Category.Length == 0) c.Category = "공격";
   if (c.Tags.Any(t => t.Contains("데이터") || t.Contains("해석") || t.Contains("추가행동") || t.Contains("포텐셜"))) return null;
   if (m.Groups["p"].Success) probability = m.Groups["p"].Value;
   return prefix + PotentialProtection.Canonical(c, probability, m.Groups["v"].Value);
  }

  // 추가행동 변형 → 「「N」배수 T, 가장 먼저/마지막에 「조건」 기술에 의한 추가행동을 얻는다」. 지령에 의한 추가행동권은 다른 규칙이라 바꾸지 않는다
  static string NormalizeExtra(string text) {
   string t = Regex.Replace(text, @"추가\s*행동권?", "추가행동");
   if (!t.Contains("추가행동") || t.Contains("지령")) return null;
   var m = Regex.Match(t, @"^(?:「?(?<n>\d+)」?\s*(?:의\s*)?배수\s*(?:의\s*)?T\s*,?\s*)?(?:(?<pos>가장\s*먼저|가장\s*마지막에|제일\s*마지막에|T의\s*최후에|최후에)\s*,?\s*)?(?:(?:기술\s*)?(?<c>(?:「[^」]+」\s*)+)(?:등의\s*)?(?:기술)?(?:에\s*의한|로)\s*)?추가행동을\s*얻는다$");
   if (!m.Success) return null;
   MoveCondition c = new MoveCondition();
   if (m.Groups["c"].Success && !MoveCondition.TryParse(m.Groups["c"].Value, out c)) return null;
   if (m.Groups["c"].Success && c.Tags.Count > 0 && Regex.IsMatch(m.Groups["c"].Value, @"^「[^」/]+」$") && !text.Contains("기술")) return null;
   if (m.Groups["c"].Success && Regex.IsMatch(text, @"기술\s*「[^」]+」에\s*의한")) { c = new MoveCondition(); c.Names.Add(Regex.Match(text, @"기술\s*「(?<v>[^」]+)」").Groups["v"].Value); }
   int every = m.Groups["n"].Success ? int.Parse(m.Groups["n"].Value) : 0;
   return ExtraActionRuntime.Canonical(every, m.Groups["pos"].Value.Contains("먼저"), c, false);
  }

  // 장소 전개: kind = 날씨·지형·중력·트릭룸
  public static bool TryPlace(string text, out string kind, out string value) {
   kind = value = "";
   var m = Regex.Match(Clean(text), @"^(?<k>날씨|지형)을?를?\s*「(?<v>[^」]+)」(?:으로|로)\s*(?:한다|할 수 있다)$");
   if (m.Success) { kind = m.Groups["k"].Value; value = m.Groups["v"].Value; return kind == "날씨" ? Mechanics.Weathers.Contains(value) : TerrainNames.Contains(value); }
   m = Regex.Match(Clean(text), @"^「(?<v>중력|트릭룸)」을\s*전개(?:한다|할 수 있다)$");
   if (m.Success) { kind = value = m.Groups["v"].Value; return true; }
   return false;
  }
  public static bool TryHazard(string text, out bool install, out string name) {
   install = false; name = "";
   string t = Clean(text);
   if (t == "자진의 설치물을 제거한다") return true;
   var m = Regex.Match(t, @"^적진에 「(?<h>[^」]+)」을 설치(?:한다|할 수 있다)$");
   if (!m.Success || !Mechanics.HazardNames.Contains(m.Groups["h"].Value)) return false;
   install = true; name = m.Groups["h"].Value; return true;
  }
  public static bool IsForcedSwitch(string text) { return Regex.IsMatch(Clean(text), @"^상대를 강제교대시(?:킨다|킬 수 있다)$"); }
  public static bool IsForcedSwitchImmunity(string text) { return Clean(text) == "자신은 강제교대되지 않는다"; }
  public static int ActFirstTier(string text) { var m = Regex.Match(Clean(text), @"^자신은 (?<w>가장|상대보다) 먼저 행동(?:한다|할 수 있다)$"); return !m.Success ? 0 : m.Groups["w"].Value == "가장" ? 2 : 1; }
  public static bool IsActFirst(string text) { return ActFirstTier(text) > 0; }
  public static bool IsFloating(string text) { return Clean(text) == "자신은 공중에 떠오른다"; }
  public static bool TryStatusImmunity(string text, out string[] statuses) {
   var m = Regex.Match(Clean(text), @"^자신은 「(?<s>[^」]+)」상태가 되지 않는다$");
   statuses = m.Success ? m.Groups["s"].Value.Split('/').Select(x => x.Trim()).ToArray() : new string[0];
   return m.Success && statuses.All(MajorStatuses.Contains);
  }
  static string Clean(string text) { return Regex.Replace(PotentialParts.Clean(text ?? "").Trim().TrimEnd('.', '。', '．').Trim(), @"\s+", " "); }

  static int Amount(string degree, string direction) { int n = degree == "매우 크게" ? 3 : degree == "크게" ? 2 : 1; return direction == "내린다" ? -n : n; }

  static string SpeciesTarget(string subject, string stats) {
   string s = Subj(subject);
   string st = stats.Trim();
   if (st.Replace(" ", "") == "전종족치" || st.Replace(" ", "") == "전") return s + "의 전종족치";
   return s + "의 " + (st.StartsWith("「") ? st : "「" + st + "」");
  }

  static string NormalizeSpecies(string text) {
   if (!text.Contains("종족치")) return null;
   const string Who = @"(?:(?<s>자신|상대)의\s*)?";
   const string What = @"(?<st>「[^」]+」|전\s*종족치)";
   Match m;
   m = Regex.Match(text, "^" + Who + What + @"(?:의)?\s*종족치(?:를|을)\s*「(?<g>[A-S][A-S]*[+-]?)」\s*(?:로|으로)\s*한다$");
   if (m.Success) return SpeciesTarget(m.Groups["s"].Value, m.Groups["st"].Value) + " 종족치를 「" + m.Groups["g"].Value + "」로 한다.";
   m = Regex.Match(text, "^" + Who + What + @"(?:의)?\s*종족치(?:를|을)\s*「(?<g>[A-S][A-S]*[+-]?)」\s*까지\s*올린다$");
   if (m.Success) return SpeciesTarget(m.Groups["s"].Value, m.Groups["st"].Value) + " 종족치를 「" + m.Groups["g"].Value + "」까지 올린다.";
   m = Regex.Match(text, "^" + Who + What + @"(?:의)?\s*종족치(?:를|을|가|이)\s*(?<n>[12])\s*랭크\s*(?:올린다|오른다)$");
   if (m.Success) return SpeciesTarget(m.Groups["s"].Value, m.Groups["st"].Value) + " 종족치를 " + m.Groups["n"].Value + "랭크 올린다.";
   m = Regex.Match(text, "^" + Who + @"(?<st>「[^」]+」)(?:의\s*종족치)?(?:을|를)\s*가장\s*(?<h>높은|낮은)\s*종족치(?:와\s*동랭크)?로\s*한다$");
   if (m.Success) return SpeciesTarget(m.Groups["s"].Value, m.Groups["st"].Value) + " 종족치를 가장 " + m.Groups["h"].Value + " 종족치로 한다.";
   m = Regex.Match(text, "^" + Who + What + @"(?:의)?\s*종족치(?:를|을)\s*(?:자신의\s*)?(?<src>「[^」]+」)(?:의)?\s*종족치와\s*(?:동랭크까지\s*올린다|같게\s*한다|동랭크로\s*한다)$");
   if (m.Success) return SpeciesTarget(m.Groups["s"].Value, m.Groups["st"].Value) + " 종족치를 자신의 " + m.Groups["src"].Value + " 종족치와 같게 한다.";
   m = Regex.Match(text, "^" + Who + What + @"(?:의)?\s*종족치(?:를|을|가)\s*(?:아군\s*)?트레이너의?\s*「(?<a>지시|육성|통솔|능력)」(?:과|와)\s*(?:같은\s*랭크|동랭크)(?:까지\s*올린다|로\s*한다|가\s*된다)$");
   if (m.Success) return SpeciesTarget(m.Groups["s"].Value, m.Groups["st"].Value) + " 종족치를 트레이너 「" + m.Groups["a"].Value + "」 등급까지 올린다.";
   return null;
  }

  static string NormalizeFraction(string text) {
   Match m;
   string cure = "";
   var withCure = Regex.Match(text, @"^(?<head>.+?회복)(?:해|하고|하여)\s*,?\s*상태이상을\s*(?:치유|회복|해제)\s*(?:한다|시킨다)$");
   if (withCure.Success) { text = withCure.Groups["head"].Value + "한다"; cure = "#"; }
   m = Regex.Match(text, @"^(?:(?<t>자신|아군|상대)(?:의|은|는)\s*)?(?:(?:최대|전체)\s*)?체력(?:의|을|를|이|가)?\s*(?:최대\s*HP의\s*)?1/(?<n>\d+)(?:을|를|만큼)?\s*회복(?:한다|시킨다)$");
   if (m.Success) {
    string t = Subj(m.Groups["t"].Value);
    string result = t + "의 체력을 최대 HP의 1/" + m.Groups["n"].Value + " 회복한다.";
    return cure.Length > 0 ? result + " " + t + "의 상태이상을 치유한다." : result;
   }
   if (cure.Length > 0) return null;
   m = Regex.Match(text, @"^(?<t>자신|상대)의\s*체력(?:을|를)\s*1/(?<n>\d+)\s*감소(?:시킨다|한다)$");
   if (m.Success) return m.Groups["t"].Value + "에게 최대 HP의 1/" + m.Groups["n"].Value + " 대미지를 준다.";
   m = Regex.Match(text, @"^(?<t>상대|자신)에게\s*(?:(?:최대|전체)\s*)?(?:체력|HP)의\s*1/(?<n>\d+)의\s*(?:「[^」]*」\s*)?(?:대미지|데미지)를\s*준다$");
   if (m.Success) return m.Groups["t"].Value + "에게 최대 HP의 1/" + m.Groups["n"].Value + " 대미지를 준다.";
   return null;
  }

  // ===== 표준 문장 해석 =====

  public static bool TryScalar(string text, out ScalarEffect effect) {
   effect = null;
   string target, action; double rate;
   if (!EffectStructures.TryScalar(text, out target, out action, out rate)) return false;
   target = Regex.Replace(target.Trim(), @"\s+", " ");
   var e = new ScalarEffect { Rate = action == "강화" ? rate : rate, Text = text };
   if (action == "약화" && rate > 1) e.Rate = 1 / rate;
   Match m;
   MoveCondition c;
   if ((m = Regex.Match(target, "^" + Subject + @"의\s*(?<c>.*?)\s*기술의\s*(?<stage>위력|명중)$")).Success && MoveCondition.TryParse(m.Groups["c"].Value, out c)) { e.Subject = Subj(m.Groups["s"].Value); e.Stage = m.Groups["stage"].Value; e.Condition = c; effect = e; return true; }
   if ((m = Regex.Match(target, "^" + Subject + @"(?:이|가)\s*(?<dir>주는|받는)\s*(?:(?<c>.*?)\s*기술의\s*)?대미지$")).Success && MoveCondition.TryParse(m.Groups["c"].Value, out c)) { e.Subject = Subj(m.Groups["s"].Value); e.Stage = m.Groups["dir"].Value + " 대미지"; e.Condition = c; effect = e; return true; }
   if ((m = Regex.Match(target, "^" + Subject + @"의\s*(?<st>「[^」]+」|전능력치|가장 높은 능력|가장 낮은 능력|공격|방어|특공|특방|속도|명중|회피)$")).Success) {
    e.Subject = Subj(m.Groups["s"].Value); e.Stage = "능력치";
    string st = m.Groups["st"].Value;
    if (st == "전능력치") e.Stats = new[] { 1, 2, 3, 4, 5 };
    else if (st == "가장 높은 능력") e.HighestStat = true;
    else if (st == "가장 낮은 능력") e.LowestStat = true;
    else { var list = StatList(st.Trim('「', '」')); if (list == null || list.Any(x => x < 1 || x > 7)) return false; e.Stats = list; }
    effect = e; return true;
   }
   return false;
  }

  public static bool TrySpecies(string text, out SpeciesEffect effect) {
   effect = null;
   string t = PotentialParts.Clean(text ?? "").Trim().TrimEnd('.', '。').Trim();
   var m = Regex.Match(t, @"^(?<s>자신|상대)의\s*(?:(?<all>전종족치)|「(?<st>[^」]+)」)\s*종족치를\s*(?<rest>.+)$");
   if (!m.Success) { m = Regex.Match(t, @"^(?<s>자신|상대)의\s*(?<all>전종족치)를\s*(?<rest>.+)$"); if (!m.Success) return false; }
   var e = new SpeciesEffect { Subject = m.Groups["s"].Value };
   if (m.Groups["all"].Success && m.Groups["all"].Value.Length > 0) e.Stats = new[] { 0, 1, 2, 3, 4, 5 };
   else { var list = StatList(m.Groups["st"].Value); if (list == null || list.Any(x => x > 5)) return false; e.Stats = list; }
   string rest = m.Groups["rest"].Value.Trim();
   Match r;
   if ((r = Regex.Match(rest, @"^「(?<g>[A-S][A-S]*[+-]?)」\s*범위의\s*「(?<v>\d+)」(?<op>로 한다|까지 올린다|까지 내린다)$")).Success) { e.Grade = r.Groups["g"].Value; e.Value = int.Parse(r.Groups["v"].Value); e.Operation = r.Groups["op"].Value == "로 한다" ? "변경" : r.Groups["op"].Value.Contains("올린") ? "상승" : "하락"; }
   else if ((r = Regex.Match(rest, @"^「(?<g>[A-S][A-S]*[+-]?)」(?<op>로 한다|까지 올린다|까지 내린다)$")).Success) { e.Grade = r.Groups["g"].Value; e.Operation = r.Groups["op"].Value == "로 한다" ? "변경" : r.Groups["op"].Value.Contains("올린") ? "상승" : "하락"; }
   else if ((r = Regex.Match(rest, @"^「(?<v>\d+)」로 하여 (?<n>[12])랭크 올린다$")).Success) { e.Operation = "랭크"; e.Ranks = int.Parse(r.Groups["n"].Value); }
   else if ((r = Regex.Match(rest, @"^(?<n>[12])랭크 올린다$")).Success) { e.Operation = "랭크"; e.Ranks = int.Parse(r.Groups["n"].Value); }
   else if ((r = Regex.Match(rest, @"^「(?<v>\d+)」로 한다$")).Success) { e.Operation = "수치"; e.Value = int.Parse(r.Groups["v"].Value); }
   else if ((r = Regex.Match(rest, @"^가장 (?<h>높은|낮은) 종족치로 한다$")).Success) e.Operation = r.Groups["h"].Value == "높은" ? "최고 복사" : "최저 복사";
   else if ((r = Regex.Match(rest, @"^자신의 「(?<src>[^」]+)」 종족치와 같게 한다$")).Success) { e.Operation = "복사"; e.Source = StatIndex(r.Groups["src"].Value); if (e.Source < 0 || e.Source > 5) return false; }
   else if ((r = Regex.Match(rest, @"^트레이너 「(?<a>지시|육성|통솔|능력)」 등급까지 올린다$")).Success) { e.Operation = "트레이너"; e.Aptitude = r.Groups["a"].Value; }
   else return false;
   if (e.Grade.Length > 0) { try { SpeciesStatRanks.LabelRange(e.Grade); } catch { return false; } }
   effect = e; return true;
  }

  public static bool TryFraction(string text, out FractionEffect effect) {
   effect = null;
   string t = PotentialParts.Clean(text ?? "").Trim();
   bool canFaint = !t.Contains("빈사로 할 수 없다");
   t = Regex.Replace(t, @"\s*이 대미지로는 빈사로 할 수 없다\.?$", "").Trim().TrimEnd('.', '。').Trim();
   Match m;
   if ((m = Regex.Match(t, @"^(?<t>자신|상대|아군 1체|아군 전체|아군)의 체력을 최대 HP의 1/(?<n>\d+) 회복한다$")).Success) { effect = new FractionEffect { Target = m.Groups["t"].Value, Action = "회복", Denominator = int.Parse(m.Groups["n"].Value) }; }
   else if ((m = Regex.Match(t, @"^(?<t>자신|상대|아군 1체|아군 전체|아군)에게 최대 HP의 1/(?<n>\d+) 대미지를 준다$")).Success) { effect = new FractionEffect { Target = m.Groups["t"].Value, Action = "대미지", Denominator = int.Parse(m.Groups["n"].Value), CanFaint = canFaint }; }
   else if ((m = Regex.Match(t, @"^(?:자신의\s*)?(?:최대\s*)?체력(?:의|을|를)?\s*(?:최대 HP의\s*)?1/(?<n>\d+)(?:을|를|만큼)?\s*소비(?:해서|해|하여|하고|한다)$")).Success) { effect = new FractionEffect { Target = "자신", Action = "소비", Denominator = int.Parse(m.Groups["n"].Value) }; }
   else {
    string target, action; int n;
    if (!EffectStructures.TryFraction(t, out target, out action, out n)) return false;
    effect = new FractionEffect { Target = target, Action = action, Denominator = n, CanFaint = canFaint };
   }
   return effect.Denominator > 0;
  }

  public static bool TryCure(string text, out string target) {
   var m = Regex.Match(PotentialParts.Clean(text ?? "").Trim().TrimEnd('.', '。').Trim(), @"^(?<t>자신|상대|아군 1체|아군 전체|아군)의 상태이상을 치유한다$");
   target = m.Success ? m.Groups["t"].Value : "";
   return m.Success;
  }

  public static bool TryRank(string text, out RankEffect effect) {
   effect = null;
   string t = PotentialParts.Clean(text ?? "").Trim().TrimEnd('.', '。').Trim();
   var m = Regex.Match(t, "^" + Subject + @"의\s*(?<what>「[^」]+」|임의의 능력|랜덤한 능력치|가장 높은 능력|가장 낮은 능력|전능력치)(?:이|가|을|를)\s*(?<d>매우 크게|크게)?\s*(?<dir>오른다|내린다)$");
   if (!m.Success) return false;
   var e = new RankEffect { Subject = Subj(m.Groups["s"].Value) };
   int amount = m.Groups["d"].Value == "매우 크게" ? 3 : m.Groups["d"].Value == "크게" ? 2 : 1;
   e.Amount = m.Groups["dir"].Value == "내린다" ? -amount : amount;
   string what = m.Groups["what"].Value;
   if (what == "임의의 능력") e.Mode = "임의";
   else if (what == "랜덤한 능력치") e.Mode = "랜덤";
   else if (what == "가장 높은 능력") e.Mode = "최고";
   else if (what == "가장 낮은 능력") e.Mode = "최저";
   else if (what == "전능력치") { e.Mode = "전능력치"; e.Stats = new[] { 1, 2, 3, 4, 5 }; }
   else { var list = StatList(what.Trim('「', '」')); if (list == null || list.Any(x => x < 1)) return false; e.Stats = list; }
   effect = e; return true;
  }

  // 포텐셜 도감·실행기 공용: 표준 문장의 모듈 이름. 모듈이 아니면 ""
  static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> classified = new System.Collections.Concurrent.ConcurrentDictionary<string, string>();
  public static string Classify(string text) { return classified.GetOrAdd(text ?? "", ClassifyUncached); }
  static string ClassifyUncached(string text) {
   string value = EffectNormalizer.Normalize(text ?? "");
   string probability, body;
   if (EffectNormalizer.TryProbabilityPrefix(value, out probability, out body)) value = body;
   value = Regex.Replace(Prepare(value), @"^(?:\d+\s*T\s*(?:간|동안)|T\s*종료\s*시\s*까지)\s*,?\s*", "");
   value = Activation.Replace(value, "");
   var sentences = Regex.Split(value, @"(?<=[.．。])\s+").Where(x => x.Trim().Length > 0).ToArray();
   if (sentences.Length == 0) return "";
   var kinds = sentences.Select(ClassifySentence).ToArray();
   return kinds.Any(x => x.Length == 0) ? "" : string.Join(" + ", kinds.Distinct());
  }
  static string ClassifySentence(string s) {
   ScalarEffect scalar; SpeciesEffect species; FractionEffect fraction; RankEffect rank; string target;
   string typed = TypeRules.Classify(s);
   if (typed.Length > 0) return typed;
   if (TryScalar(s, out scalar)) return "수치 배율";
   if (TrySpecies(s, out species)) return "종족치 변경";
   if (TryFraction(s, out fraction)) return "최대 HP의 1/N";
   if (TryCure(s, out target)) return "상태이상 치유";
   if (TryRank(s, out rank)) return "능력 랭크 변화";
   string kind, value; bool install; string[] statuses;
   if (TryPlace(s, out kind, out value)) return "장소 전개";
   if (TryHazard(s, out install, out value)) return "설치물";
   if (IsForcedSwitch(s)) return "강제교대";
   if (IsForcedSwitchImmunity(s)) return "강제교대 무효";
   if (IsFloating(s)) return "부유";
   if (IsActFirst(s)) return "먼저 행동";
   MoveCondition guardCondition; double? guardRate;
   string guardKind;
   if (PotentialProtection.TryGuard(s, out guardKind, out guardCondition, out guardRate)) return guardKind == "회피" ? "회피" : guardKind == "반사" ? "기술 반사" : "기술 무효화";
   if (PotentialProtection.TryPierce(s, out guardCondition)) return "방호 관통";
   if (PotentialProtection.IsAnalysisImmunity(s)) return "데이터 해석 무효";
   ExtraActionGrant extra; int every;
   if (ExtraActionRuntime.TryParse(s, out extra, out every)) return "추가행동";
   if (TryStatusImmunity(s, out statuses)) return "상태이상 면역";
   if (EffectStructures.IsPriority(s)) return "우선도 변화";
   if (EffectStructures.IsStatIgnore(s)) return "능력 상승·강화 무시";
   if (EffectStructures.IsSwitchRestriction(s)) return "교대 제한";
   if (EffectStructures.IsSwitchEffect(s)) return "교대 효과";
   if (EffectStructures.IsPivotGrant(s)) return "유턴 효과";
   if (EffectStructures.IsAnalysisPermission(s)) return "데이터 해석";
   var state = StateEffects.Parse(s);
   if (state != null && StateEffects.Find(state.State) != null) return "상태 부여";
   return "";
  }
 }
}
