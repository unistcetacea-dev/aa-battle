using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AABattle {
 // F4b 타입·상성(확정 30): 복수 타입 기술의 상성은 각 타입 배율의 곱, 자속은 하나라도 일치하면 1회.
 // 포텐셜 모듈(표준 문장):
 //  타입 내성 관통   자신의 「C」 기술이 상대의(「X」 타입의) 타입 내성을 관통한다.   반감·무효 → 1배(사용자 결정)
 //  타입 상성 무시   자신의 「C」 기술이 상대의 타입 상성을 무시한다.               항상 1배
 //  상성 무효 관통   자신의 「C」 기술이 타입 상성에 의한 무효를 관통한다.          0배 → 1배
 //  자속 배율       자신의 「C」 기술의 타입 일치 배율을 「N배」로 한다.
 //  타입 추가       자신의 「C」 기술에 「X」 타입을 추가해 유리한 상성으로 판정한다.  타입별 배율 중 가장 유리한 것
 //  유리한 상성     자신의 기술을 유리한 상성으로 판정한다.
 //  상성 지정       (「X」 타입에게) 자신/상대의 「C」 기술의 상성을 「효과가 굉장/효과가 별로/효과 보통」으로 한다.
 //  타입 변경       자신/상대의 타입을 「X」로 한다(할 수 있다).
 public static class TypeRules {
  const string Owner = @"^(?:자신(?:의|이\s*내보내는)\s*)?(?<c>.*?)\s*";
  static readonly Regex Resist = new Regex(@"^자신의 (?<c>(?:「[^」]+」)*)\s*기술이 (?:상대의|「(?<vs>[^」]+)」 타입의) 타입 내성을 관통한다$");
  static readonly Regex IgnoreAll = new Regex(@"^자신의 (?<c>(?:「[^」]+」)*)\s*기술이 상대의 타입 상성을 무시한다$");
  static readonly Regex Immune = new Regex(@"^자신의 (?<c>(?:「[^」]+」)*)\s*기술이 타입 상성에 의한 무효를 관통한다$");
  static readonly Regex ImmuneTaken = new Regex(@"^상대의 (?<c>(?:「[^」]+」)*)\s*기술이 타입 상성에 의한 무효를 관통한다$");
  static readonly Regex Stab = new Regex(@"^자신의 (?<c>(?:「[^」]+」)*)\s*기술의 타입 일치 배율을 「(?<v>[0-9.]+)배」로 한다$");
  static readonly Regex GhostImmune = new Regex(@"^자신의 (?<c>(?:「[^」]+」)*)\s*기술이 「고스트」 타입의 무효를 관통한다$");
  static readonly Regex AddType = new Regex(@"^자신의 (?<c>(?:「[^」]+」)*)\s*기술에 「(?<t>[^」]+)」 타입을 추가해 유리한 상성으로 판정(?:한다|할 수 있다)$");
  static readonly Regex Rating = new Regex(@"^(?:「(?<vs>[^」]+)」 타입에게 )?(?<s>자신|상대)의 (?<c>(?:「[^」]+」)*)\s*기술의 상성을 「(?<r>효과가 굉장|효과가 별로|효과 보통)」(?:으로|로) 한다$");
  static readonly Regex TypeChange = new Regex(@"^(?<s>자신|상대)의 타입을 「(?<t>[^」]+)」(?:으로|로) (?:한다|할 수 있다)$");
  const string Favorable = "자신의 기술을 유리한 상성으로 판정한다";
  public const string InverseField = "필드에 있는 전원의 타입 상성을 반전한다";
  public const string NeutralField = "필드에 있는 전원의 기술의 타입 상성을 「효과 보통」으로 한다";

  static string Clean(string text) { return Regex.Replace(PotentialParts.Clean(text ?? "").Trim().TrimEnd('.', '。', '．').Trim(), @"\s+", " "); }
  static bool Types(string value) { return value.Split('/').All(x => MoveCondition.TypeNames.Contains(x.Trim())); }
  static string Subject(MoveCondition c) { return "자신의 " + (c.Empty ? "" : c.Text + " "); }

  // 수집 원문의 기술 조건: 괄호 밖 「기술」과 조사를 떼고 MoveCondition으로 읽는다
  static bool Condition(string raw, out MoveCondition c) {
   string t = Regex.Replace(raw ?? "", "기술(?![^「]*」)", " ");
   t = Regex.Replace(t, @"(?<=^|\s|」)(?:이|가|에|을|를|의)(?=\s|$|「)", " ");
   return MoveCondition.TryParse(Regex.Replace(t, @"\s+", " ").Trim(), out c);
  }

  // ===== 정규화(수집 원문 변형 → 표준 문장) =====
  public static string Normalize(string text) {
   string t = Regex.Replace(text ?? "", @"\s+", " ").Trim().TrimEnd('.', '。', '．').Trim();
   if (!t.Contains("타입") && !t.Contains("상성")) return null;
   if (Clean(t) == InverseField) return InverseField + ".";
   if (Regex.IsMatch(Clean(t), @"^필드에 있는 전원의 기술의 타입\s*상성이 「등배」가 된다$")) return NeutralField + ".";
   if (Classify(t).Length > 0) return Clean(t) + ".";
   Match m; MoveCondition c;
   m = Regex.Match(t, Owner + @"(?:상대의\s*|「(?<vs>[^」]+)」\s*타입의\s*)?「?타입\s*내성」?(?:을|를)\s*(?:관통|무시)한다$");
   if (m.Success && Condition(m.Groups["c"].Value, out c) && (!m.Groups["vs"].Success || Types(m.Groups["vs"].Value)))
    return Subject(c) + "기술이 " + (m.Groups["vs"].Success ? "「" + m.Groups["vs"].Value + "」 타입의" : "상대의") + " 타입 내성을 관통한다.";
   m = Regex.Match(t, Owner + @"(?:상대의\s*)?「?타입\s*상성」?(?:을|를)\s*무시한다$");
   if (m.Success && Condition(m.Groups["c"].Value, out c)) return Subject(c) + "기술이 상대의 타입 상성을 무시한다.";
   m = Regex.Match(t, @"^상대의\s*(?<c>.*?)\s*(?:기술)?(?:이|가)\s*타입\s*상성(?:으로|에\s*의해)\s*무효화되지\s*않(?:게\s*된다|는다)$");
   if (m.Success && Condition(m.Groups["c"].Value, out c)) return "상대의 " + (c.Empty ? "" : c.Text + " ") + "기술이 타입 상성에 의한 무효를 관통한다.";
   m = Regex.Match(t, Owner + @"타입\s*상성에\s*의해\s*무시되지\s*않는다$");
   if (m.Success && Condition(m.Groups["c"].Value, out c)) return Subject(c) + "기술이 타입 상성에 의한 무효를 관통한다.";
   m = Regex.Match(t, Owner + @"「노말/격투」\s*기술이\s*「고스트」\s*타입의\s*무효를\s*관통한다$");
   if (m.Success) return "자신의 「노말/격투」 기술이 「고스트」 타입의 무효를 관통한다.";
   m = Regex.Match(t, Owner + @"「?타입\s*일치\s*배율」?(?:을|를|이|가)\s*(?:강화한다\.?\s*\(\s*1\.5\s*배\s*=+>\s*(?<v>[0-9.]+)\s*배\s*\)|강화\s*\(\s*(?<v>[0-9.]+)\s*배\s*\)\s*한다|「?(?<v>[0-9.]+)\s*배」?\s*(?:로|가)\s*(?:변경한다|한다|된다))$");
   if (m.Success && Condition(m.Groups["c"].Value, out c)) {
    double v = double.Parse(m.Groups["v"].Value, CultureInfo.InvariantCulture);
    return Subject(c) + "기술의 타입 일치 배율을 「" + v.ToString("0.##", CultureInfo.InvariantCulture) + "배」로 한다.";
   }
   m = Regex.Match(t, Owner + @"「(?<t>[^」]+)」\s*타입을\s*(?:추가|부여)(?:해|하여|하고)\s*,?\s*유리한\s*(?:상성|타입)으로\s*(?:판정|계산)(?<can>할\s*수\s*있다|한다)$");
   if (m.Success && Types(m.Groups["t"].Value)) {
    string raw = Regex.Replace(m.Groups["c"].Value, @"\s*(?:에|이|가)$", "");
    if (Condition(raw, out c)) return Subject(c) + "기술에 「" + m.Groups["t"].Value + "」 타입을 추가해 유리한 상성으로 판정" + (m.Groups["can"].Value.StartsWith("할") ? "할 수 있다." : "한다.");
   }
   if (Regex.IsMatch(t, @"^(?:자신의\s*기술을\s*)?유리한\s*상성으로\s*판정한다$")) return Favorable + ".";
   m = Regex.Match(t, @"^(?:「(?<vs>[^」]+)」\s*타입(?:에게|에 대해|에 대하여)\s*,?\s*)?(?<s>자신|상대)의\s*(?<c>(?:「[^」]+」\s*)*)\s*기술(?:의\s*상성)?(?:이|가)\s*「?(?<r>효과가?\s*(?:매우\s*)?(?:굉장|별로|보통))」?(?:이|가)?\s*(?:된다|해진다)$");
   if (m.Success && MoveCondition.TryParse(m.Groups["c"].Value, out c)) {
    string r = m.Groups["r"].Value.Replace(" ", "");
    r = r.Contains("굉장") ? "효과가 굉장" : r.Contains("별로") ? "효과가 별로" : "효과 보통";
    return (m.Groups["vs"].Success ? "「" + m.Groups["vs"].Value + "」 타입에게 " : "") + m.Groups["s"].Value + "의 " + (c.Empty ? "" : c.Text + " ") + "기술의 상성을 「" + r + "」" + EffectModules.Ro(r) + " 한다.";
   }
   m = Regex.Match(t, @"^(?<s>자신|상대)(?:를|을)\s*「(?<t>[^」]+)」\s*타입으로\s*(?<v>한다|할\s*수\s*있다)$");
   if (!m.Success) m = Regex.Match(t, @"^(?<s>자신|상대)의\s*타입을\s*「(?<t>[^」]+)」\s*(?:으로|로)\s*(?<v>한다|할\s*수\s*있다|변화시킨다|바꾼다)$");
   if (m.Success && Types(m.Groups["t"].Value)) return m.Groups["s"].Value + "의 타입을 「" + m.Groups["t"].Value + "」" + EffectModules.Ro(m.Groups["t"].Value) + (m.Groups["v"].Value.StartsWith("할") ? " 할 수 있다." : " 한다.");
   return null;
  }

  // 편집기 「타입·상성」 모듈의 표준 문장
  public static readonly string[] EditorTargets = { "타입 내성 관통", "타입 상성 무시", "상성 무효 관통", "자속 배율", "타입 추가(유리한 상성)", "상대의 타입 변경", "자신의 타입 변경" };
  public static string Canonical(string target, MoveCondition c, string type, double rate) {
   c = c ?? new MoveCondition();
   switch (target) {
    case "타입 내성 관통": return Subject(c) + "기술이 상대의 타입 내성을 관통한다.";
    case "타입 상성 무시": return Subject(c) + "기술이 상대의 타입 상성을 무시한다.";
    case "상성 무효 관통": return Subject(c) + "기술이 타입 상성에 의한 무효를 관통한다.";
    case "자속 배율": return Subject(c) + "기술의 타입 일치 배율을 「" + rate.ToString("0.##", CultureInfo.InvariantCulture) + "배」로 한다.";
    case "타입 추가(유리한 상성)": return Subject(c) + "기술에 「" + type + "」 타입을 추가해 유리한 상성으로 판정한다.";
    case "상대의 타입 변경": return "상대의 타입을 「" + type + "」" + EffectModules.Ro(type) + " 한다.";
    case "자신의 타입 변경": return "자신의 타입을 「" + type + "」" + EffectModules.Ro(type) + " 한다.";
   }
   return "";
  }

  public static string Classify(string sentence) {
   string s = Clean(sentence);
   if (!s.Contains("타입") && !s.Contains("상성")) return "";
   if (s == InverseField) return "필드 타입 상성 반전";
   if (s == NeutralField) return "필드 타입 상성 중립";
   if (Resist.IsMatch(s)) return "타입 내성 관통";
   if (IgnoreAll.IsMatch(s)) return "타입 상성 무시";
   if (Immune.IsMatch(s) || ImmuneTaken.IsMatch(s)) return "상성 무효 관통";
   if (GhostImmune.IsMatch(s)) return "고스트 타입 무효 관통";
   if (Stab.IsMatch(s)) return "자속 배율";
   if (AddType.IsMatch(s) && Types(AddType.Match(s).Groups["t"].Value)) return "타입 추가";
   if (s == Favorable) return "유리한 상성";
   if (Rating.IsMatch(s)) return "상성 지정";
   if (TypeChange.IsMatch(s) && Types(TypeChange.Match(s).Groups["t"].Value)) return "타입 변경";
   return "";
  }

  public static bool TryTypeChange(string text, out string subject, out string[] types) {
   var m = TypeChange.Match(Clean(text));
   subject = m.Success ? m.Groups["s"].Value : "";
   types = m.Success ? m.Groups["t"].Value.Split('/').Select(x => x.Trim()).ToArray() : new string[0];
   return m.Success && types.All(MoveCondition.TypeNames.Contains);
  }

  // 계속 성립하는 포텐셜 문장 + 사건으로 걸린 것(ConditionState.Modifiers)
  static IEnumerable<string> Lines(Fighter owner, Fighter opponent, Rules rules) {
   if (owner == null) yield break;
   foreach (string s in PotentialModifiers.Sentences(owner, opponent, rules ?? new Rules(), "타입")) yield return Clean(s);
   foreach (string s in PotentialModifiers.Sentences(owner, opponent, rules ?? new Rules(), "상성")) if (!s.Contains("타입")) yield return Clean(s);
   foreach (var m in owner.Conditions.Modifiers ?? new List<ActiveModifier>())
    if (m != null && ((m.Text ?? "").Contains("타입") || (m.Text ?? "").Contains("상성"))) yield return Clean(m.Text);
  }

  static bool Applies(Match x, Move m, Fighter a) { MoveCondition c; return x.Success && MoveCondition.TryParse(x.Groups["c"].Value, out c) && c.Matches(m, a, 1); }

  // 공격 측 포텐셜이 바꾸는 상성 계산 방식
  public sealed class Profile {
   public List<string> Added = new List<string>();
   public bool Favorable, IgnoreAll, PierceImmune, PierceAll, IgnoreGhostImmune, Inverse;
   public List<string> PierceTypes = new List<string>();
  }

  public static Profile For(Fighter a, Fighter b, Move m, Rules r) {
   var p = new Profile();
   if (a == null || m == null) return p;
   // 필드 전원 효과는 장소의 양쪽 모두에게 작용한다. 복수 보유해도 반전은 한 번이다.
   foreach (string line in Lines(a, b, r).Concat(Lines(b, a, r))) {
    if (line == InverseField) p.Inverse = true;
    if (line == NeutralField) p.IgnoreAll = true;
   }
   foreach (string line in Lines(a, b, r)) {
    Match x;
    if ((x = AddType.Match(line)).Success && Applies(x, m, a)) { foreach (string t in x.Groups["t"].Value.Split('/')) if (!p.Added.Contains(t)) p.Added.Add(t); p.Favorable = true; }
    else if (GhostImmune.IsMatch(line) && Applies(GhostImmune.Match(line), m, a)) p.IgnoreGhostImmune = true;
    else if (line == Favorable) p.Favorable = true;
    else if ((x = IgnoreAll.Match(line)).Success && Applies(x, m, a)) p.IgnoreAll = true;
    else if ((x = Immune.Match(line)).Success && Applies(x, m, a)) p.PierceImmune = true;
    else if ((x = Resist.Match(line)).Success && Applies(x, m, a)) { if (x.Groups["vs"].Success) p.PierceTypes.AddRange(x.Groups["vs"].Value.Split('/')); else p.PierceAll = true; }
   }
   // 받는 쪽의 「상대의 기술이 타입 상성에 의한 무효를 관통한다」(겨냥표적)
   foreach (string line in Lines(b, a, r)) { var x = ImmuneTaken.Match(line); if (Applies(x, m, a)) p.PierceImmune = true; }
   return p;
  }

  // 상성 반전은 단일 타입 조합마다 적용하고 그 뒤 복수 타입 배율을 곱한다.
  public static double Matchup(Profile profile, double factor) {
   if (profile == null || !profile.Inverse) return factor;
   return factor == 0 ? 2 : 1 / factor;
  }

  // 판정에 쓰는 기술 타입(타입 추가 포함). 자속도 이 목록으로 본다
  public static string[] MoveTypes(Fighter a, Fighter b, Move m, Rules r) {
   var types = (m == null ? new string[0] : m.types ?? new string[0]).ToList();
   foreach (string t in For(a, b, m, r).Added) if (!types.Contains(t)) types.Add(t);
   return types.ToArray();
  }

  // 상성 지정(기본 상성 계산 뒤). 무효(0)는 그대로 둔다
  public static double Adjust(Fighter a, Fighter b, Move m, Rules r, double type) {
   if (a == null || b == null || m == null || type <= 0) return type;
   double result = type;
   foreach (var pair in new[] { new { owner = a, other = b, side = "자신" }, new { owner = b, other = a, side = "상대" } })
    foreach (string line in Lines(pair.owner, pair.other, r)) {
     var x = Rating.Match(line); MoveCondition c;
     if (!x.Success || x.Groups["s"].Value != pair.side || !MoveCondition.TryParse(x.Groups["c"].Value, out c) || !c.Matches(m, a, type)) continue;
     if (x.Groups["vs"].Success && !x.Groups["vs"].Value.Split('/').Any(v => (b.Data.types ?? new string[0]).Contains(v))) continue;
     string rating = x.Groups["r"].Value;
     result = rating == "효과가 굉장" ? 2 : rating == "효과가 별로" ? .5 : 1;
    }
   return result;
  }

  // 자속 배율 지정(타입이 일치할 때만)
  public static double StabOverride(Fighter a, Fighter b, Move m, Rules r, double stab) {
   if (a == null || m == null || stab <= 1) return stab;
   foreach (string line in Lines(a, b, r)) {
    var x = Stab.Match(line);
    if (Applies(x, m, a)) return double.Parse(x.Groups["v"].Value, CultureInfo.InvariantCulture);
   }
   return stab;
  }
 }
}
