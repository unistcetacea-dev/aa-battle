using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace AABattle {
 // 트리거 모듈(효과 모듈과 같은 방식): 수집 원문 → 표준 문장(Normalize) → 구조(TriggerSpec) → 배틀 사건(TriggerFact)과 대조.
 // 1단계 모듈: 등장 · 교대 · 퇴장 · 타이밍 · 피격 · 명중 · 급소 · 쓰러뜨림 · 기술 사용
 // 사용자 결정: 「내보낼 때」(기술 직전)와 「내보냈을 때」(기술 직후)는 다른 타이밍, 「명중 시」와 「공격이 성공했을 때」는 같다.
 public sealed class TriggerSpec {
  public string Module = "";
  public bool Opponent;                                // 주체가 상대(상대가 나왔을 때·상대가 기술을 내보낼 때)
  public bool IncludeForced;                           // 강제교대도 퇴장에 포함
  public string Method = "";                           // 등장·교대 방식 / 타이밍 라벨
  public MoveCondition Condition = new MoveCondition();
  public bool Attack;                                  // 「공격」(공격기술 한정)과 「기술」(변화기술 포함)
  public bool After;                                   // 「내보냈을 때」
  public bool Ain;                                     // 상대가 아인종
  public string[] Types = new string[0];               // 쓰러뜨린 상대 / 교대 상대 아군의 타입
  public int Every, FieldTurn;                         // 「N」배수 T / 장소에 나와 「N」T째
  public string Inner = "";                            // 「…」가 성립한 T 종료 시
  public bool NextTurn;                                // 「…」가 성립한 다음 T 종료 시(지난 T의 사건)
  // 2단계(TriggerStates): 상태 이름 · 체력 비교 · 날씨/장소 이름 · 랭크
  public string Subject = "자신";                      // 자신 · 상대 · 아군(출전 = 자신) · 자신과 상대
  public string[] Names = new string[0];
  public string Op = "";                               // 체력: 가득 · 가득 아님 · 이하 · 이상 · 1 · 감소
  public int Top = 1, Bottom = 1;
  public string Place = "";
  public bool Release, Rise, Big;
  public int[] Stats = new int[0];
  public bool ByOpponent;                              // 상대에 의한 랭크 변화
  public bool IsEvent { get { return TriggerModules.EventModules.Contains(Module); } }
 }

 // 배틀 사건 하나를 한 포켓몬의 관점에서 본 것. 이번 T 기록(ConditionState.TurnFacts)에도 그대로 남긴다
 public sealed class TriggerFact {
  public string Kind = "";        // 등장 · 교대 · 퇴장 · 타이밍 · 명중 · 쓰러뜨림 · 기술 사용
  public bool Self;               // 관점 포켓몬이 사건의 주체(나온 쪽·교대한 쪽·기술을 쓴 쪽)
  public bool Received;           // 관점 포켓몬이 기술을 받은 쪽
  public string Method = "";      // 등장 방식(선발·죽어내밀기·통상교대·임의교대·강제교대) / 교대(임의·강제) / 타이밍 라벨
  public Move Move;
  public Fighter User;            // 기술을 쓴 쪽(자속 판정)
  public bool Damaged, Substitute, Critical, After;
  public string Item = "";        // 소지품 소실 사건의 소지품 이름
  public double TypeEffect = 1;
  public string[] OtherTypes = new string[0];   // 쓰러뜨린 상대의 타입
  public string[] PartnerTypes = new string[0]; // 교대한 상대 아군의 타입
  public bool OtherAin;                         // 상대(공격한 쪽·쓰러뜨린 쪽)가 아인종
  public int Before, After2, Max, Stat, Delta;
  public bool ByOpponent;  // 체력 변화(전·후·최대) / 랭크 변화(능력 번호·변화량)
  public TriggerFact For(bool self, bool received) { var copy = (TriggerFact)MemberwiseClone(); copy.Self = self; copy.Received = received; return copy; }
 }

 public static partial class TriggerModules {
  public static readonly string[] EventModules = { "등장", "교대", "퇴장", "타이밍", "피격", "명중", "급소", "쓰러뜨림", "기술 사용", "상태 됨", "상태 해제", "체력 변화", "회복", "날씨 변화", "장소 변화", "랭크", "특성 발동", "빗나감", "먼저 행동함", "무효화", "배틀 시작", "지령", "해석", "피격·무효화", "대미지", "행동 선택", "방어 성공", "간접 대미지", "드레인", "해석당함", "소지품 소실", "급소 받음", "필드 빈사", "나무열매 사용" };
  public static readonly string[] StateModules = { "행동 선후", "상태", "체력", "날씨", "장소", "상시", "PT 참가", "적진 타입", "트레이너 자질", "플레이버", "여기다", "해석 상태", "부여", "포텐셜 보유", "소지품", "특성", "생존", "빈사 수", "진영", "공격하지 않음", "능력 변화 합", "능력치 비교", "또는", "속도 비교", "첫 행동 전", "공중", "같은 타입", "최후 아님", "PT 특성" };
  public static readonly string[] EntryMethods = { "선발", "죽어내밀기", "임의교대", "통상교대", "강제교대", "선발 또는 죽어내밀기" };
  static readonly ConcurrentDictionary<string, TriggerSpec> specs = new ConcurrentDictionary<string, TriggerSpec>();
  static readonly TriggerSpec None = new TriggerSpec();

  static string Clean(string text) {
   string t = Regex.Replace(PotentialParts.Clean(text ?? ""), @"\s+", " ").Trim().TrimEnd('.', '。', '．').Trim();
   return Regex.Replace(t, @"^자동\s*", "");
  }
  static bool AllTypes(string value) { return value.Length > 0 && value.Split('/').All(x => MoveCondition.TypeNames.Contains(x.Trim())); }
  static string[] TypeList(string raw) {
   var list = Regex.Matches(raw ?? "", "「([^」]+)」").Cast<Match>().SelectMany(m => m.Groups[1].Value.Split('/')).Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
   return list.Length > 0 && list.All(MoveCondition.TypeNames.Contains) ? list : null;
  }
  static string Particle(string word, string withBatchim, string without) {
   if (string.IsNullOrEmpty(word)) return without;
   char ch = word[word.Length - 1];
   if (ch == '」' && word.Length > 1) ch = word[word.Length - 2];
   return ch >= '가' && ch <= '힣' && (ch - '가') % 28 != 0 ? withBatchim : without;
  }

  // 수집 원문의 기술 조건: 괄호 밖 「기술」·조사를 떼고 우선도·위력 표기를 괄호로 바꿔 MoveCondition으로 읽는다
  static bool Cond(string raw, out MoveCondition c) {
   string t = raw ?? "";
   t = Regex.Replace(t, @"(?<!「)우선도\s*(?:가\s*)?「?\+\s*1?」?\s*(?:이상)?\s*(?:의|인)?", "「우선도 +」");
   t = Regex.Replace(t, @"(?<!「)위력\s*:?\s*「?(\d+)」?\s*(이하|이상)\s*(?:의|인)?", "「위력 $1 $2」");
   t = Regex.Replace(t, "(?:접촉)?기술(?![^「]*」)", m => m.Value.StartsWith("접촉") ? "「접촉」" : " ");
   t = Regex.Replace(t, @"(?<=^|\s|」)(?:이|가|에|을|를|의|등의|등|자신의|자신이)(?=\s|$|「)", " ");
   t = Regex.Replace(t, @"\s+", " ").Trim();
   return MoveCondition.TryParse(t, out c);
  }
  static string CondText(MoveCondition c) { return c == null || c.Empty ? "" : c.Text + " "; }

  // ===== 표준 문장 =====
  public static string Canonical(TriggerSpec s) {
   if (s.Module == "소지품 소실") return CanonicalItemLoss(s);
   switch (s.Module) {
    case "등장":
     if (s.Types.Length > 0) return "「" + string.Join("/", s.Types) + "」 타입 아군과 교대해 장소에 나올 때";
     return (s.Opponent ? "상대가 " : "") + (s.Method.Length > 0 ? s.Method + "로 " : "") + "장소에 " + (s.Opponent ? "나왔을 때" : "나올 때");
    case "교대":
     if (s.Opponent) return "상대가 " + (s.Method == "임의" ? "임의교대할 때" : "교대할 때");
     return s.Types.Length > 0 ? "「" + string.Join("/", s.Types) + "」 타입 아군과 교대할 때" : TriggerStructures.SwitchLabel;
    case "퇴장": return (s.Opponent ? "상대가 " : "") + (s.IncludeForced ? "강제교대를 포함해 " : "") + "장소를 떠날 때";
    case "타이밍":
     if (s.Inner.Length > 0) return "「" + s.Inner + "」가 성립한 " + (s.NextTurn ? "다음 " : "") + "T 종료 시";
     if (s.FieldTurn > 0) return "장소에 나와 「" + s.FieldTurn + "」T째 종료 시";
     return (s.Every > 0 ? "「" + s.Every + "」배수 " : "") + s.Method;
    case "피격": return (s.Ain ? "아인종 " : "") + "상대의 " + CondText(s.Condition) + (s.Attack ? "공격을" : "기술을") + " 받았을 때";
    case "명중": return "자신의 " + CondText(s.Condition) + (s.Attack ? "공격이" : "기술이") + " 명중했을 때";
    case "급소": return "자신의 " + CondText(s.Condition) + "기술이 급소에 맞았을 때";
    case "쓰러뜨림":
     if (s.Ain) return "아인종 포켓몬을 쓰러뜨렸을 때";
     if (s.Types.Length > 0) return "「" + string.Join("/", s.Types) + "」 타입 포켓몬을 쓰러뜨렸을 때";
     return s.Condition.Empty ? TriggerStructures.DefeatLabel : "자신의 " + s.Condition.Text + " 기술로 상대를 쓰러뜨렸을 때";
    case "기술 사용": return (s.Opponent ? "상대가 " : "") + CondText(s.Condition) + "기술을 " + (s.After ? "내보냈을 때" : "내보낼 때");
   }
   return CanonicalState(s);
  }

  // ===== 수집 원문 → 구조 =====
  public static bool TryParse(string text, out TriggerSpec spec) {
   spec = null;
   string t = Clean(text);
   if (t.Length == 0) return false;
   if (TryParseItemLoss(t, out spec)) return true;
   if (TryParseAbilityState(t, out spec)) return true;
   if (TryParseState(t, out spec)) return true;
   if (TryParseCondition(t, out spec)) return true;
   Match m; MoveCondition c;
   // 타이밍
   m = Regex.Match(t, @"^(?:매\s*)?(?:「?(?<n>\d+)」?\s*(?:의\s*)?배수\s*(?:의\s*)?)?(?:T|턴)\s*(?:의\s*)?(?<k>개시|시작|종료|최후|마지막)\s*(?:시|시에|에)?$");
   if (m.Success) {
    string k = m.Groups["k"].Value;
    spec = new TriggerSpec { Module = "타이밍", Method = k == "개시" || k == "시작" ? TriggerStructures.TurnStartLabel : k == "종료" ? TriggerStructures.TurnEndLabel : TriggerStructures.TurnLastLabel, Every = m.Groups["n"].Success ? int.Parse(m.Groups["n"].Value) : 0 };
    return true;
   }
   m = Regex.Match(t, @"^(?:자신이\s*)?(?:필드|장소)에\s*(?:나와|나온)\s*(?:「?(?<n>\d+)」?\s*(?:T|턴)\s*째\s*(?:의\s*)?|(?:T|턴)\s*(?:의\s*)?)?종료\s*시(?:에)?$");
   if (m.Success) { spec = new TriggerSpec { Module = "타이밍", Method = TriggerStructures.TurnEndLabel, FieldTurn = m.Groups["n"].Success ? int.Parse(m.Groups["n"].Value) : 1 }; return spec.FieldTurn > 0; }
   m = Regex.Match(t, @"^「(?<inner>.+)」가\s*성립한\s*(?<next>다음\s*)?T\s*종료\s*시$");
   if (m.Success) { TriggerSpec inner; if (!TryParse(m.Groups["inner"].Value, out inner) || !inner.IsEvent || inner.Module == "타이밍") return false; spec = new TriggerSpec { Module = "타이밍", Method = TriggerStructures.TurnEndLabel, Inner = Canonical(inner), NextTurn = m.Groups["next"].Success }; return true; }
   m = Regex.Match(t, @"^(?<inner>.+?)\s*(?:T|턴)\s*(?:의\s*)?종료\s*시(?:에)?$");
   if (m.Success) {
    string inner = InnerEvent(m.Groups["inner"].Value.Trim().TrimEnd(','));
    TriggerSpec innerSpec;
    if (inner != null && TryParse(inner, out innerSpec) && innerSpec.IsEvent && innerSpec.Module != "타이밍") { spec = new TriggerSpec { Module = "타이밍", Method = TriggerStructures.TurnEndLabel, Inner = Canonical(innerSpec) }; return true; }
    return false;
   }
   // 쌍방 선택(확정 13·14: 선택 시점) → 기존 선택 사건 문장
   if (Regex.IsMatch(t, @"^서로\s*(?:「)?필중(?:」)?\s*기술을\s*(?:내보낼|선택했을)\s*때$") || Regex.IsMatch(t, @"^서로\s*공격(?:기)?을\s*(?:내보낼|선택했을)\s*때$")) return false;
   // 등장
   m = Regex.Match(t, @"^(?:(?<s>상대가|자신이|자신의\s*포켓몬이)\s*)?(?:「?(?<m>선발\s*또는\s*죽어내밀기|선발\(죽어내밀기\)|\(선발\)죽어내밀기|선발|죽어내밀기|임의\s*교대|통상\s*교대|강제\s*교대|교대)」?\s*(?:로|으로)\s*|(?<sw>아군과\s*(?:임의\s*)?교대해\s*)|(?<t>(?:「[^」]+」\s*)+)\s*(?:타입\s*아군|타입|인\s*아군|타입인\s*아군)과\s*교대해\s*)?(?:(?:필드|장소)\s*에\s*)?(?:나올\s*때|나왔을\s*때(?:에)?|나오면|나와|나온\s*때|등장했을\s*때|등장할\s*때)$");
   if (m.Success && (m.Groups["m"].Success || m.Groups["sw"].Success || m.Groups["t"].Success || Regex.IsMatch(t, "필드|장소"))) {
    bool opponent = m.Groups["s"].Value == "상대가";
    string method = Regex.Replace(m.Groups["m"].Value, @"\s+", "");
    if (method == "선발또는죽어내밀기" || method == "선발(죽어내밀기)" || method == "(선발)죽어내밀기") method = "선발 또는 죽어내밀기";
    if (method == "교대" || m.Groups["sw"].Success) method = "임의교대";
    var types = m.Groups["t"].Success ? TypeList(m.Groups["t"].Value) : new string[0];
    if (types == null || types.Length > 0 && opponent) return false;
    spec = new TriggerSpec { Module = "등장", Opponent = opponent, Method = types.Length > 0 ? "" : method, Types = types };
    return true;
   }
   // 교대(나가는 쪽)
   m = Regex.Match(t, @"^(?:(?<s>상대가)\s*)?(?:아군과\s*)?(?<v>임의\s*)?교대할\s*때$");
   if (!m.Success) m = Regex.Match(t, @"^(?<v>임의\s*)교대\s*시$");
   if (m.Success) { bool opp = m.Groups["s"].Success; if (!opp && !m.Groups["v"].Success && !t.Contains("아군과")) return false; spec = new TriggerSpec { Module = "교대", Opponent = opp, Method = opp && !m.Groups["v"].Success ? "" : "임의" }; return true; }
   m = Regex.Match(t, @"^(?:(?<t1>(?:「[^」]+」\s*)+)\s*(?:타입\s*아군|타입|인\s*아군|타입인\s*아군)|아군\s*(?<t2>(?:「[^」]+」\s*)+))과\s*교대할\s*때$");
   if (m.Success) { var types = TypeList(m.Groups["t1"].Success ? m.Groups["t1"].Value : m.Groups["t2"].Value); if (types == null) return false; spec = new TriggerSpec { Module = "교대", Method = "임의", Types = types }; return true; }
   // 퇴장
   m = Regex.Match(t, @"^(?:(?<s>상대가)\s*)?(?<forced>강제\s*교대를\s*포함해\s*)?(?:필드|장소|장)(?:를|을)\s*떠날\s*때$");
   if (m.Success) { spec = new TriggerSpec { Module = "퇴장", Opponent = m.Groups["s"].Success, IncludeForced = m.Groups["forced"].Success }; return true; }
   // 쓰러뜨림
   m = Regex.Match(t, @"^(?:자신(?:이|의)\s*)?(?:(?<c>.*?)\s*(?:기술)?\s*(?:으로|로)\s*)?(?:상대|(?<ain>「아인」|아인(?:종)?(?:\s*포켓몬)?)|(?<t>(?:「[^」]+」\s*,?\s*)+)\s*(?:타입의?\s*포켓몬|타입|포켓몬)?)\s*(?:을|를)\s*쓰러(?:뜨|트)렸을\s*때$");
   if (m.Success) {
    c = new MoveCondition();
    if (m.Groups["c"].Success && m.Groups["c"].Value.Trim().Length > 0 && !Cond(m.Groups["c"].Value, out c)) return false;
    if (m.Groups["ain"].Success) { spec = new TriggerSpec { Module = "쓰러뜨림", Ain = true }; return true; }
    var types = m.Groups["t"].Success ? TypeList(m.Groups["t"].Value) : new string[0];
    if (types == null) return false;
    spec = new TriggerSpec { Module = "쓰러뜨림", Types = types, Condition = c };
    return true;
   }
   // 급소
   m = Regex.Match(t, @"^(?:자신의\s*)?(?<c>.*?)\s*(?:기술|공격)(?:이|가)\s*급소에\s*(?:맞았을|명중했을)\s*때$");
   if (m.Success && Cond(m.Groups["c"].Value, out c)) { spec = new TriggerSpec { Module = "급소", Condition = c }; return true; }
   // 피격(상대의 기술을 받음 · 상대의 기술이 자신에게 명중)
   m = Regex.Match(t, @"^(?:자신이\s*)?(?<ain>아인(?:종)?(?:의|\s*상대의)\s*)?(?:상대(?:의|에게서|로부터)?\s*)?(?<c>.*?)\s*(?<o>접촉공격|공격기술|공격|기술)?\s*(?:을|를)\s*(?:받았을\s*때|받으면|맞았을\s*때|당했을\s*때)$");
   if (!m.Success) m = Regex.Match(t, @"^(?<ain>아인(?:종)?의\s*)?상대의\s*(?<c>.*?)\s*(?<o>공격|기술)?(?:이|가)\s*(?:자신에게\s*)?명중했을\s*때$");
   if (m.Success && !Regex.IsMatch(m.Groups["c"].Value, "지령|『|체력|대미지|데미지|반동")) {
    string o = m.Groups["o"].Value, raw = m.Groups["c"].Value;
    if (o.Length == 0 && !Regex.IsMatch(raw, "^(?:「[^」]+」\\s*)+(?:등의)?$")) return false;
    if (o == "접촉공격") raw += "「접촉」";
    if (!Cond(raw, out c)) return false;
    bool attack = o == "공격" || o == "접촉공격" || o == "공격기술" || c.Category == "공격";
    if (c.Category == "공격") c.Category = "";
    spec = new TriggerSpec { Module = "피격", Condition = c, Attack = attack, Ain = m.Groups["ain"].Success };
    return true;
   }
   // 명중(= 공격 성공)
   m = Regex.Match(t, @"^(?:자신(?:의|이)\s*)?(?<c>.*?)\s*(?<o>공격|기술)?\s*(?:이|가|의)?\s*(?:상대에게\s*)?(?:명중했을\s*때|명중\s*시|명중한\s*때|성공했을\s*때)$");
   if (m.Success && !t.StartsWith("상대")) {
    string o = m.Groups["o"].Value, raw = m.Groups["c"].Value;
    if (o.Length == 0 && !Regex.IsMatch(raw, "^(?:「[^」]+」\\s*)+(?:등의)?$")) return false;
    if (!Cond(raw, out c)) return false;
    bool attack = o == "공격" || c.Category == "공격";
    if (c.Category == "공격") c.Category = "";
    spec = new TriggerSpec { Module = "명중", Condition = c, Attack = attack };
    return true;
   }
   // 기술 사용(내보낼 때 = 직전 / 내보냈을 때 = 직후)
   m = Regex.Match(t, @"^(?:(?<s>자신|상대)(?:이|가|의)\s*)?(?<pre>기술\s*(?=「))?(?<c>.*?)\s*(?<o>공격기술|공격기|변화기술|변화기|기술)?\s*(?:을|를)\s*(?<v>내보낼\s*때|사용할\s*때|내보냈을\s*때|사용했을\s*때|사용\s*시)$");
   if (!m.Success) m = Regex.Match(t, @"^(?:(?<s>자신|상대)(?:이|가|의)\s*)?(?<pre>기술\s*(?=「))?(?<c>(?:「[^」]+」\s*)+)\s*(?<v>사용\s*시)$");
   if (m.Success && !Regex.IsMatch(m.Groups["c"].Value, "나무열매|소지품|지령|『|아군|상대|같은|나중")) {
    string o = m.Groups["o"].Value, raw = m.Groups["c"].Value;
    if (o.Length == 0 && !Regex.IsMatch(raw, "^(?:「[^」]+」\\s*)+(?:등의)?$")) return false;
    if (!Cond(raw, out c)) return false;
    if (o.StartsWith("공격")) c.Category = "공격";
    if (o.StartsWith("변화")) c.Category = "변화";
    string v = m.Groups["v"].Value.Replace(" ", "");
    spec = new TriggerSpec { Module = "기술 사용", Opponent = m.Groups["s"].Value == "상대", Condition = c, After = v == "내보냈을때" || v == "사용했을때" };
    return true;
   }
   return false;
  }

  // 「X한 T 종료시」의 X(관형형) → 사건 문장
  static string InnerEvent(string phrase) {
   string p = phrase.Trim();
   if (p.Length == 0) return null;
   p = Regex.Replace(p, @"(?:필드|장소)에\s*나온$", "장소에 나왔을 때");
   p = Regex.Replace(p, @"(?<=로|으로)\s*나온$", " 장소에 나왔을 때");
   p = Regex.Replace(p, @"받은$", "받았을 때");
   p = Regex.Replace(p, @"명중한$", "명중했을 때");
   p = Regex.Replace(p, @"(?:내보낸|사용한)$", "내보냈을 때");
   p = Regex.Replace(p, @"쓰러(?:뜨|트)린$", "쓰러뜨렸을 때");
   p = Regex.Replace(p, @"교대한$", "교대할 때");
   p = Regex.Replace(p, @"해제된$", "해제되었을 때");
   p = Regex.Replace(p, @"(?<=(?:가|이))\s*된$", " 되었을 때");
   if (!p.EndsWith("때")) return null;
   if (Regex.IsMatch(p, @"^공격을\s*받았을\s*때$")) p = "상대의 " + p;
   return p;
  }

  public static TriggerSpec Parse(string clause) {
   return specs.GetOrAdd(clause ?? "", key => { TriggerSpec s; return TryParse(key, out s) ? s : None; });
  }
  public static bool IsModule(string clause) { return Parse(clause).Module.Length > 0; }

  // 수집 원문의 같은 뜻 변형 → 표준 문장. 모듈이 아니면 null
  public static string Normalize(string text) {
   string t = Clean(text);
   if (Regex.IsMatch(t, @"^서로\s*(?:「)?필중(?:」)?\s*기술을\s*(?:내보낼|선택했을)\s*때$")) return "서로 필중 기술을 선택했을 때";
   if (Regex.IsMatch(t, @"^서로\s*공격(?:기)?을\s*(?:내보낼|선택했을)\s*때$")) return "서로 공격기를 선택했을 때";
   TriggerSpec s;
   return TryParse(t, out s) ? Canonical(s) : null;
  }
  public static string Classify(string text) { var s = Parse(TriggerStructures.Normalize(text)); return s.Module; }

  // ===== 사건 대조 =====
  static readonly string[] Voluntary = { "통상교대", "임의교대" };
  static bool EntryMethod(string spec, string fact) {
   if (spec.Length == 0) return fact != "강제교대";
   if (spec == "임의교대") return Voluntary.Contains(fact);
   if (spec == "선발 또는 죽어내밀기") return fact == "선발" || fact == "죽어내밀기";
   return spec == fact;
  }
  public static bool Matches(TriggerSpec s, TriggerFact f, Fighter owner) {
   if (s == null || f == null || s.Module.Length == 0) return false;
   if (s.Module == "소지품 소실") return MatchesItemLoss(s, f);
   var move = f.Move;
   switch (s.Module) {
    case "등장":
     if (f.Kind != "등장" || s.Opponent == f.Self) return false;
     if (s.Types.Length > 0) return Voluntary.Contains(f.Method) && f.PartnerTypes.Any(s.Types.Contains);
     return EntryMethod(s.Method, f.Method);
    case "교대":
     if (f.Kind != "교대" || s.Opponent == f.Self) return false;
     if (s.Method == "임의" && f.Method != "임의") return false;
     return s.Types.Length == 0 || f.PartnerTypes.Any(s.Types.Contains);
    case "퇴장": return f.Kind == "퇴장" && s.Opponent != f.Self && (s.IncludeForced || f.Method != "강제");
    case "타이밍":
     if (f.Kind != "타이밍" || f.Method != s.Method) return false;
     var rules = EffectScope.Current == null ? null : EffectScope.Current.Rules;
     if (s.Every > 0 && (rules == null || rules.Turn <= 0 || rules.Turn % s.Every != 0)) return false;
     if (s.FieldTurn > 0 && (owner == null || owner.Conditions.FieldTurn != s.FieldTurn)) return false;
     if (s.Inner.Length > 0) { var inner = Parse(s.Inner); return owner != null && ((s.NextTurn ? owner.Conditions.PrevTurnFacts : owner.Conditions.TurnFacts) ?? new List<TriggerFact>()).Any(x => Matches(inner, x, owner)); }
     return true;
    case "피격":
     if (f.Kind != "명중" || !f.Received || move == null) return false;
     if (s.Ain && !f.OtherAin) return false;
     if (s.Attack && move.category == "변화") return false;
     if (move.category != "변화" && !f.Damaged && !(s.Condition.Tags.Contains("접촉") && !f.Substitute)) return false;
     if (s.Condition.Tags.Contains("접촉") && MoveModules.IgnoresContact(f.User)) return false;
     return s.Condition.Matches(move, f.User, f.TypeEffect);
    case "명중":
     if (f.Kind != "명중" || !f.Self || move == null) return false;
     if (s.Attack && move.category == "변화") return false;
     return s.Condition.Matches(move, f.User, f.TypeEffect);
    case "급소": return f.Kind == "명중" && f.Self && f.Critical && move != null && s.Condition.Matches(move, f.User, f.TypeEffect);
    case "쓰러뜨림":
     if (f.Kind != "쓰러뜨림" || !f.Self) return false;
     if (s.Ain) return f.OtherAin;
     if (s.Types.Length > 0 && !f.OtherTypes.Any(s.Types.Contains)) return false;
     return s.Condition.Empty || move != null && s.Condition.Matches(move, f.User, f.TypeEffect);
    case "기술 사용":
     return f.Kind == "기술 사용" && s.After == f.After && s.Opponent != f.Self && move != null && s.Condition.Matches(move, f.User, 1);
   }
   return MatchesState(s, f);
  }

  // ===== 사건 발생 =====
  // actor: 사건의 주체(나온 쪽·교대한 쪽·기술을 쓴 쪽), other: 반대편(받는 쪽). 양쪽 관점으로 이번 T 기록을 남기고 맞는 포텐셜을 발동한다
  public static void Raise(TriggerFact fact, Fighter actor, Fighter other, string message, Action<string> log) {
   if (fact == null) return;
   bool received = fact.Kind == "명중";
   if (actor != null) Observe(actor, fact.For(true, false), message, log);
   if (other != null && !ReferenceEquals(other, actor)) Observe(other, fact.For(false, received), message, log);
  }
  public static void Observe(Fighter owner, TriggerFact fact, string message, Action<string> log) {
   if (owner == null || owner.Data == null) return;
   if (fact.Kind != "타이밍") {
    if (owner.Conditions.TurnFacts == null) owner.Conditions.TurnFacts = new List<TriggerFact>();
    if (owner.Conditions.TurnFacts.Count < 64) owner.Conditions.TurnFacts.Add(fact);
   }
   var e = EventFor(owner, fact, message, log);
   if (e != null) { if (PotentialQueue.Active) PotentialQueue.Raise(e); else LogPending(e); }
   // 트레이너 고유는 장소에 있는 자기 포켓몬의 관점으로 같은 사건을 받는다
   var state = BattleRecords.StateOf(owner);
   if (state != null && state.Trainer != null && owner.OnField) TrainerTriggers.Raise(state.Trainer, fact, owner, message, log);
  }
  // 이 사건에 맞는 포텐셜이 있으면 큐 사건을 만든다(없으면 null — 로그를 남기지 않는다)
  public static PotentialEvent EventFor(Fighter owner, TriggerFact fact, string message, Action<string> log) {
   if (owner == null || owner.Data == null || log == null) return null;
   if (owner.HP <= 0 && fact.Kind != "퇴장" && !(fact.Kind == "상태 됨" && fact.Method == "빈사")) return null;   // 빈사로 장소를 떠날 때·빈사가 되었을 때는 발동
   Func<string, bool> spec = clause => Matches(Parse(TriggerStructures.Normalize(clause)), fact, owner);
   string label = (owner.Data.potentials ?? new Potential[0]).Where(p => p != null).SelectMany(TriggerStructures.Clauses).FirstOrDefault(spec);
   if (label == null) return null;
   return new PotentialEvent { Owner = owner, Spec = spec, Label = TriggerStructures.Normalize(label), Move = fact.Move, Fact = fact, Message = message ?? "", Log = log, Announce = true, PendingText = (source, extra) => "『" + source.Name + "』 " + TriggerStructures.Normalize(label) + " 트리거 충족" + (extra ? " · 추가 조건 확인 필요" : "") + " · 효과 판정 대기: " + source.Description };
  }
  // 효과 실행 범위 밖: 예전처럼 사건과 대기 중인 포텐셜만 기록
  public static void LogPending(PotentialEvent e) {
   e.Log(e.Message);
   foreach (var p in (e.Owner.Data.potentials ?? new Potential[0]).Where(p => p != null && TriggerStructures.Clauses(p).Any(e.Matches)))
    e.Log("『" + p.name + "』 " + e.Label + " 트리거 충족 — " + e.Message + " · 효과 판정 대기: " + p.description);
  }

  // 추가 조건 절(확정 48: 기본 AND): 사건이 아닌 절은 지금 성립하면 통과
  public static bool Holds(string clause, Fighter owner, Fighter opponent, Rules rules) {
   string normalized = TriggerStructures.Normalize(clause);
   if (normalized == TriggerStructures.AlwaysLabel) return true;
   if (normalized == TriggerStructures.FieldLabel) return owner != null && owner.HP > 0 && owner.OnField;
   var spec = Parse(normalized);
   if (spec.IsEvent) return false;
   if (StateModules.Contains(spec.Module)) return Holds(spec, owner, opponent, rules ?? new Rules());
   return AnsweredTriggers.StateHolds(clause, owner, opponent, rules ?? new Rules());
  }
  // 등장 계열 사건 둘(「자신(상대)이 장소에 나왔을 때」)은 OR
  public static bool Alternative(string matched, string other) {
   var a = Parse(TriggerStructures.Normalize(matched)); var b = Parse(TriggerStructures.Normalize(other));
   return a.Module == "등장" && b.Module == "등장";
  }

  // 등장 방식 이름
  public static string EntryMethodName(EntryReason reason) {
   switch (reason) {
    case EntryReason.Lead: return "선발";
    case EntryReason.Replacement: return "죽어내밀기";
    case EntryReason.NormalSwitch: case EntryReason.Return: return "통상교대";
    case EntryReason.Forced: return "강제교대";
   }
   return "임의교대";
  }
  public static bool IsAin(Fighter f) { return f != null && f.Data != null && f.Data.ain; }
 }
}
