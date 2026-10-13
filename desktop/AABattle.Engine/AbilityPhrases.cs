using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 // 특성 도감 문장의 같은 뜻 변형 → 트리거·효과 모듈 표준 문장, 그리고 특성 정리에서 나온 새 계속 효과 모듈(포텐셜에도 쓴다)
 //  급소 무효       자신은 급소에 맞지 않는다.
 //  반드시 버팀     자신은 빈사가 되는 공격을 HP 1로 버틴다.
 //  간접 대미지 무효 자신은 공격 이외의 대미지를 받지 않는다.
 //  능력 하락 무효   자신은 상대에 의해 (「X」이) 능력이 내려가지 않는다.
 //  설치물 무시     자신은 설치물의 영향을 받지 않는다.
 //  특성 관통       자신의 기술은 상대의 〔방호 특성〕을 관통한다.
 //  타입 추가       자신의 타입에 「X」를 추가한다.
 //  조건 우선도     자신의 「조건」 기술의 우선도를 +N한다.
 public static partial class AbilityPhrases {
  public const string NoCritical = "자신은 급소에 맞지 않는다";
  public const string Sturdy = "자신은 빈사가 되는 공격을 HP 1로 버틴다";
  public const string MagicGuard = "자신은 공격 이외의 대미지를 받지 않는다";
  public const string HazardProof = "자신은 설치물의 영향을 받지 않는다";
  public const string Pierce = "자신의 기술은 상대의 〔방호 특성〕을 관통한다";
  public const string ExtraStage = "자신의 능력 변화량을 1 늘린다";
  public const string ReverseStage = "자신에 대한 능력 변화를 반대로 한다";
  public const string ReflectStage = "상대에 의한 능력 하락을 상대에게 되돌린다";
  public const string NoWeather = "날씨의 효과를 무효화한다";
  public const string StatusProtectPierce = "자신의 「변화」 기술이 상대의 「방어」 기술을 관통한다";
  public const string PoisonPierce = "자신이 거는 「독」「맹독」은 타입 면역을 관통한다";
  public const string SuppressAbility = "상대의 〔특성〕을 무효화한다";
  static readonly Dictionary<string, string> ContinuousRules = new Dictionary<string, string> {
   { ExtraStage, "능력 변화량 추가" }, { ReverseStage, "능력 변화 반전" }, { ReflectStage, "능력 하락 반사" },
   { NoWeather, "날씨 효과 무효" }, { StatusProtectPierce, "변화기술 방어 관통" },
   { PoisonPierce, "독 타입 면역 관통" }, { SuppressAbility, "상대 특성 무효" }
  };
  static readonly Regex DropImmune = new Regex(@"^자신은 상대에 의해 (?:(?<s>「[^」]+」)이|능력이) 내려가지 않는다$");
  static readonly Regex AddType = new Regex(@"^자신의 타입에 「(?<t>[^」]+)」를 추가한다$");
  static readonly Regex Priority = new Regex(@"^자신의 (?<c>(?:「[^」]+」)+) 기술의 우선도를 \+(?<n>\d)한다$");
  static readonly Regex WeatherDamage = new Regex(@"^자신은 (?<w>(?:「[^」]+」)+)의 대미지를 받지 않는다$");
  static readonly Regex StatusPower = new Regex(@"^(?<s>(?:「[^」]+」)+)의 기술 위력 저하 효과를 무효화한다$");
  static readonly string[] WeatherNames = { "쾌청", "큰가뭄", "비", "강한 비", "모래바람", "싸라기눈", "눈", "난기류" };
  static string[] QuotedNames(string text) {
   return Regex.Matches(text, "「([^」]+)」").Cast<Match>().SelectMany(x => x.Groups[1].Value.Split('/')).Select(x => x.Trim()).ToArray();
  }
  static bool WeatherDamageRule(string text) {
   var m = WeatherDamage.Match(text);
   return m.Success && QuotedNames(m.Groups["w"].Value).All(WeatherNames.Contains);
  }

  static string Clean(string text) { return Regex.Replace(PotentialParts.Clean(text ?? ""), @"\s+", " ").Trim().TrimEnd('.', '。', '．').Trim(); }
  static string Stat(string s) { return s == "공" ? "공격" : s == "방" ? "방어" : s == "속" ? "속도" : s; }

  // ===== 도감 문장 → 표준 문장(문장 하나씩) =====
  public static string Rewrite(string text) {
   string t = (text ?? "").Trim();
   // 「쾌청(큰가뭄)」「비(강한 비)」 → 「쾌청」「비」, 날씨 이름
   t = Regex.Replace(t, @"「(쾌청|비)\([^)]*\)」", "「$1」");
   t = Regex.Replace(t, @"날씨가 (맑음|싸라기눈|모래바람)일 때", m => "날씨가 「" + (m.Groups[1].Value == "맑음" ? "쾌청" : m.Groups[1].Value) + "」일 때");
   t = Regex.Replace(t, @"필드가 「([^」]+)」 상태일 때", "「$1」일 때");
   t = Regex.Replace(t, @"(?:자신은\s*)?(?<s>「[^」]+」)\s*상태가\s*되지\s*않고,\s*", "자신은 ${s}상태가 되지 않는다. ");
   // 급소 보정: 「…급소에 맞기 쉬워진다. (C+N)」
   t = Regex.Replace(t, @"급소에 맞기 쉬워진다\.?\s*\(\s*[CＣ]\s*\+\s*(\d)\s*\)\.?", "급소에 맞기 쉬워진다(C+$1).");
   var result = new List<string>();
   foreach (string raw in Regex.Split(t, @"(?<=[.。])\s+").Select(x => x.Trim()).Where(x => x.Length > 0)) result.AddRange(Sentence(raw));
   return string.Join(" ", result);
  }
  static IEnumerable<string> Sentence(string raw) {
   string s = raw.TrimEnd('.', '。').Trim();
   string remaining = RewriteRemaining(s);
   if (remaining != null) { yield return remaining + "."; yield break; }
   Match m;
   // 흡수: 「X」 기술을 받으면 흡수해서 「S」이 오른다 / 체력을 1/N 회복한다
   m = Regex.Match(s, @"^(?:상대로부터의 )?「(?<x>[^」]+)」\s*(?:타입의\s*)?(?:기술|타입 기술)을 (?:받으면|끌어들여) 흡수(?:해서|하여|하고|한다)\s*,?\s*(?<r>.*)$");
   if (m.Success) {
    string x = m.Groups["x"].Value, r = m.Groups["r"].Value.Trim();
    yield return "상대의 「" + x + "」 기술을 무효화한다.";
    if (r.Length == 0) yield break;
    var heal = Regex.Match(r, @"^체력을 1/(?<n>\d+)\s*회복한다$");
    var up = Regex.Match(r, @"^「(?<s>[^」]+)」(?:이|가) (?<d>크게 )?오른다$");
    if (heal.Success) yield return "상대의 「" + x + "」 기술을 무효화했을 때, 자신의 체력을 최대 HP의 1/" + heal.Groups["n"].Value + " 회복한다.";
    else if (up.Success) yield return "상대의 「" + x + "」 기술을 무효화했을 때, 자신의 「" + up.Groups["s"].Value + "」이 " + up.Groups["d"].Value + "오른다.";
    else yield return r + ".";
    yield break;
   }
   // 「이 〔특성〕이 발동했을 때, …」·「특성 발동 시, …」 → 직전 무효화 문장의 사건(Fold가 합친다)
   m = Regex.Match(s, @"^(?:이 〔특성〕이 발동했을 때|특성 발동 시)\s*,\s*(?<r>.+)$");
   if (m.Success) { yield return "〔발동〕 " + Effect(m.Groups["r"].Value) + "."; yield break; }
   // 트리거 절 + 효과
   var parts = Regex.Match(s, @"^(?<c>.+?(?:때|면|시))\s*,\s*(?<r>.+)$");
   if (!parts.Success) parts = Regex.Match(s, @"^(?<c>(?:「?접촉\s*기술」?|「접촉」 기술)을 받(?:으면|았을 때))\s+(?<r>.+)$");
   if (parts.Success) {
    string c = Trigger(parts.Groups["c"].Value), r = parts.Groups["r"].Value;
    if (c != null) { yield return c + ", " + Effect(r) + "."; yield break; }
   }
   yield return Effect(s) + ".";
  }
  // 「상대의 「X」 기술을 무효화한다. 〔발동〕 B」 → 무효화 + 「상대의 「X」 기술을 무효화했을 때, B」
  public static string Fold(string text) {
   var list = Regex.Split(text, @"(?<=[.。])\s+").Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
   for (int i = 1; i < list.Count; i++) {
    if (!list[i].StartsWith("〔발동〕 ")) continue;
    var g = Regex.Match(list[i - 1], @"^상대(?:로부터)?의 (?<x>(?:「[^」]+」)+)\s*(?:타입\s*)?(?:기술|공격)을 (?:무효화한다|끌어들여 흡수한다)\.?$");
    string body = list[i].Substring("〔발동〕 ".Length);
    list[i] = g.Success ? "상대의 " + g.Groups["x"].Value + " 기술을 무효화했을 때, " + body : body;
    if (g.Success) list[i - 1] = "상대의 " + g.Groups["x"].Value + " 기술을 무효화한다.";
   }
   return string.Join(" ", list);
  }
  static string Trigger(string c) {
   c = c.Trim();
   Match m;
   if (Regex.IsMatch(c, @"^(?:「?접촉\s*기술」?|「접촉」 기술)을 (?:받으면|받았을 때)$")) return "상대의 「접촉」 기술을 받았을 때";
   if (Regex.IsMatch(c, @"^「비접촉」 기술을 받았을 때$")) return null;
   if ((m = Regex.Match(c, @"^(?:자신의 )?(?:HP|체력)가 1/(?<n>\d) ?이하가 (?:됐을 때|되었을 때|되면)$")).Success) return "자신의 체력이 1/" + m.Groups["n"].Value + " 이하가 되었을 때";
   if ((m = Regex.Match(c, @"^(?:자신의 )?(?:HP|체력)이? ?가? ?1/(?<n>\d) ?(?<o>이하|이상)일 때$")).Success) return "자신의 체력이 1/" + m.Groups["n"].Value + " " + m.Groups["o"].Value + "일 때";
   if (Regex.IsMatch(c, @"^(?:자신의 )?(?:체력이 가득일 때|체력이 가득할 때|HP가 「가득」일 때)$")) return "자신의 체력이 가득일 때";
   if (Regex.IsMatch(c, @"^(?:상대의 공격기술을 받으면|「공격」 기술을 받았을 때|상대의 공격을 받았을 때)$")) return "상대의 공격을 받았을 때";
   if ((m = Regex.Match(c, @"^「(?<x>물리|특수)\s*(?:기술)?」\s*(?:기술)?을 (?:받으면|받았을 때)$")).Success) return "상대의 「" + m.Groups["x"].Value + "」 기술을 받았을 때";
   if ((m = Regex.Match(c, @"^「(?<x>물리|특수)기술」을 명중시키면$")).Success) return "자신의 「" + m.Groups["x"].Value + "」 기술이 명중했을 때";
   if (c == "상대를 쓰러트리면" || c == "상대를 쓰러뜨리면") return "상대를 쓰러뜨렸을 때";
   if ((m = Regex.Match(c, @"^(?<s>혼란|화상)상태일 때$")).Success) return "자신이 「" + m.Groups["s"].Value + "」 상태일 때";
   return c;
  }
  static string Effect(string r) {
   string s = r.Trim().TrimEnd('.', '。').Trim();
   string remaining = RewriteRemaining(s);
   if (remaining != null) return remaining;
   Match m;
   m = Regex.Match(s, @"^(?:자신은\s*)?(?<w>(?:「[^」]+」\s*)+)의\s*대미지를\s*받지\s*않는다$");
   if (m.Success && QuotedNames(m.Groups["w"].Value).All(WeatherNames.Contains)) return "자신은 " + string.Concat(QuotedNames(m.Groups["w"].Value).Select(x => "「" + x + "」")) + "의 대미지를 받지 않는다";
   if (Regex.IsMatch(s, @"^(?:상대의 기술이 급소에 맞지 않게 된다|상대의 공격이 급소에 맞지 않는다|급소 ?무효)$")) return NoCritical;
   if (Regex.IsMatch(s, @"^(?:반드시 버틴다|「빈사」 상태가 되지 않는다)$")) return Sturdy;
   if (Regex.IsMatch(s, @"^(?:상대의 공격 이외의 대미지를 (?:무효화한다|무시한다)|상대의 공격 이외로 대미지를 받지 않는다)$")) return MagicGuard;
   if (Regex.IsMatch(s, @"^(?:자신의 능력이 감소\(저하\)하지 않는다|상대에 의해 능력이 감소\(저하\)하지 않는다|상대에 의한 능력 감소\(저하\) 효과를 무효화한다|상대로부터의 능력감소\(저하\)를 (?:무효화한다|받지 않는다))$")) return "자신은 상대에 의해 능력이 내려가지 않는다";
   if ((m = Regex.Match(s, @"^자신의 (?<s>공|방|명중|속|특공|특방)이 감소\(저하\)하지 않는다$")).Success) return "자신은 상대에 의해 「" + Stat(m.Groups["s"].Value) + "」이 내려가지 않는다";
   if (Regex.IsMatch(s, @"^설치기술의 (?:효과를 무시한다|영향을 받지 않는다)$")) return HazardProof;
   if (Regex.IsMatch(s, @"^(?:상대의 방호특성을 무시한다|방호특성 무효|자신의 기술이 상대의 〔방호 특성〕을 관통한다)$")) return Pierce;
   if ((m = Regex.Match(s, @"^자신의 타입에 「?(?<t>[^」\s]+?)」?(?:을|를) 추가한다$")).Success) return "자신의 타입에 「" + m.Groups["t"].Value + "」를 추가한다";
   if (s == "강제교대 무효") return "자신은 강제교대되지 않는다";
   if ((m = Regex.Match(s, @"^상대(?:에게 HP의|의 체력을) 1/(?<n>\d+)(?:만큼 대미지를 준다| ?감소한다)$")).Success) return "상대에게 최대 HP의 1/" + m.Groups["n"].Value + " 대미지를 준다";
   if ((m = Regex.Match(s, @"^(?:자신의 )?HP를 1/(?<n>\d+)만큼 회복한다$")).Success) return "자신의 체력을 최대 HP의 1/" + m.Groups["n"].Value + " 회복한다";
   if ((m = Regex.Match(s, @"^「(?<x>[^」]+)」 기술로 받는 대미지가 (?<n>\d+(?:\.\d+)?)배가 된다$")).Success) return "자신이 받는 「" + m.Groups["x"].Value + "」 기술의 대미지를 " + (double.Parse(m.Groups["n"].Value) >= 1 ? "강화" : "약화") + "(" + m.Groups["n"].Value + "배)한다";
   if ((m = Regex.Match(s, @"^(?:자신이 )?「(?<x>[^」]+)」\s*(?:상태)?(?:가|이) 되지 않는다$")).Success) return "자신은 「" + m.Groups["x"].Value + "」상태가 되지 않는다";
   if ((m = Regex.Match(s, @"^(?<n>\d+)할로 (?<r>(?:상대를 )?「[^」]+」\s*상태로 한다)$")).Success) return m.Groups["n"].Value + "할로 " + (m.Groups["r"].Value.StartsWith("상대") ? m.Groups["r"].Value : "상대를 " + m.Groups["r"].Value);
   if ((m = Regex.Match(s, @"^자신의 「(?<s>[^」]+)」 종족치(?:를|가) 「?(?<v>[A-Z+]+)」?(?:로 한다|가 된다)$")).Success) return "자신의 「" + m.Groups["s"].Value + "」 종족치를 「" + m.Groups["v"].Value + "」로 한다";
   if ((m = Regex.Match(s, @"^자신의 「(?<c>[^」]+)」 (?:타입 )?기술의 우선도를 [+＋](?<n>\d)한다$")).Success) return "자신의 「" + m.Groups["c"].Value + "」 기술의 우선도를 +" + m.Groups["n"].Value + "한다";
   if ((m = Regex.Match(s, @"^(?<n>\d+)T 동안 (?:날씨를 「(?<w>[^」]+)」(?:으로|로) 한다|전체 필드를 「(?<f>[^」]+)」 상태로 한다)$")).Success)
    return m.Groups["w"].Success ? m.Groups["n"].Value + "T동안 날씨를 「" + m.Groups["w"].Value + "」" + EffectModules.Ro(m.Groups["w"].Value) + " 한다" : m.Groups["n"].Value + "T동안 지형을 「" + m.Groups["f"].Value + "」" + EffectModules.Ro(m.Groups["f"].Value) + " 한다";
   return s;
  }

  // ===== 새 계속 효과 모듈 =====
  public static string Normalize(string text) {
   string t = Clean(text);
   if (ClassifyRemaining(t).Length > 0) return t + ".";
   if (ContinuousRules.ContainsKey(t) || t == NoCritical || t == Sturdy || t == MagicGuard || t == HazardProof || t == Pierce || DropImmune.IsMatch(t) || AddType.IsMatch(t) || Priority.IsMatch(t) || WeatherDamageRule(t) || StatusPower.IsMatch(t)) return t + ".";
   return null;
  }
  public static string Classify(string sentence) {
   string s = Clean(sentence);
   string remaining = ClassifyRemaining(s);
   if (remaining.Length > 0) return remaining;
   string module;
   if (ContinuousRules.TryGetValue(s, out module)) return module;
   if (s == NoCritical) return "급소 무효";
   if (s == Sturdy) return "반드시 버팀";
   if (s == MagicGuard) return "간접 대미지 무효";
   if (s == HazardProof) return "설치물 무시";
   if (s == Pierce) return "특성 관통";
   if (WeatherDamageRule(s)) return "날씨 대미지 무효";
   if (StatusPower.IsMatch(s)) return "상태이상 위력 저하 무효";
   if (DropImmune.IsMatch(s)) return "능력 하락 무효";
   if (AddType.IsMatch(s)) return "타입 추가";
   if (Priority.IsMatch(s)) return "조건 우선도";
   return "";
  }
  static IEnumerable<string> Lines(Fighter f, Fighter other, string keyword) {
   if (f == null) return new string[0];
   var rules = EffectScope.Current == null ? new Rules() : EffectScope.Current.Rules;
   return PotentialModifiers.Sentences(f, other, rules, keyword).Select(Clean);
  }
  static bool HasRule(Fighter owner, Fighter other, Rules rules, string text, bool global = false) {
   return PotentialModifiers.Sentences(owner, other, rules, text, null, global).Any(x => Clean(x) == text);
  }
  public static int StageAmount(Fighter target, Fighter source, Rules rules, int amount) {
   if (HasRule(target, source, rules, ReverseStage)) amount = -amount;
   if (HasRule(target, source, rules, ExtraStage)) amount += Math.Sign(amount);
   return amount;
  }
  public static bool ReflectsStage(Fighter target, Fighter source, Rules rules, List<string> log) {
   if (!HasRule(target, source, rules, ReflectStage)) return false;
   foreach (var p in (target.Data.potentials ?? new Potential[0]).Where(p => p != null && (p.description ?? "").Contains(ReflectStage))) {
    if (!PotentialModifiers.Continuous(p, target, source, rules) || AbilityModules.Ignored(p, target, source)) continue;
    log.Add(target.Data.name + (p.id == AbilityModules.Slot ? "의 〔" + AbilityRuntime.Name(target) + "〕" : "의 『" + p.name + "』") + ": 능력 하락 반사");
    if (p.id == AbilityModules.Slot) AnsweredTriggers.AbilityActivated(target, AbilityRuntime.Name(target), log.Add);
    break;
   }
   return true;
  }
  // 조건부 날씨 무효는 무효화 전 날씨로 조건을 판정한다.
  [ThreadStatic] static bool checkingWeather;
  public static bool NegatesWeather(Fighter a, Fighter b, Rules rules) {
   if (checkingWeather) return false;
   checkingWeather = true;
   try { return HasRule(a, b, rules, NoWeather, true) || HasRule(b, a, rules, NoWeather, true); }
   finally { checkingWeather = false; }
  }
  public static bool PiercesProtect(Fighter a, Fighter b, Move move, Rules rules) {
   return move != null && move.category == "변화" && HasRule(a, b, rules, StatusProtectPierce);
  }
  public static bool PiercesStatusType(Fighter source, Fighter target, string status, Rules rules) {
   return (status == "독" || status == "맹독") && HasRule(source, target, rules, PoisonPierce);
  }
  public static bool SuppressesAbilities(Fighter owner, Fighter other, bool abilityOnly = false) {
   var rules = EffectScope.Current == null ? new Rules() : EffectScope.Current.Rules;
   // 제거된 슬롯도 원래 문장으로 조회한다. 양쪽 판정이 끝날 때까지 상태를 바꾸지 않는다.
   var potentials = AbilityModules.UnsuppressedPotentials(owner);
   if (abilityOnly) potentials = potentials.Where(p => p != null && p.id == AbilityModules.Slot);
   return PotentialModifiers.Sentences(owner, other, rules, SuppressAbility, potentials, true).Any(x => Clean(x) == SuppressAbility);
  }
  public static bool NoCrit(Fighter target, Fighter attacker) { return Lines(target, attacker, "급소").Any(x => x == NoCritical); }
  public static bool Endures(Fighter target, Fighter attacker) { return Lines(target, attacker, "버틴다").Any(x => x == Sturdy); }
  public static bool IgnoresIndirect(Fighter f) { return Lines(f, null, "이외").Any(x => x == MagicGuard); }
  public static bool IgnoresHazards(Fighter f) { return Lines(f, null, "설치물").Any(x => x == HazardProof); }
  public static bool IgnoresWeatherDamage(Fighter f, string weather) {
   return Lines(f, null, "대미지").Any(x => WeatherDamageRule(x) && QuotedNames(WeatherDamage.Match(x).Groups["w"].Value).Contains(weather));
  }
  public static bool IgnoresStatusPowerPenalty(Fighter f, string status) {
   return Lines(f, null, "위력 저하").Any(x => StatusPower.IsMatch(x) && QuotedNames(StatusPower.Match(x).Groups["s"].Value).Contains(status));
  }
  public static bool BlocksOhko(Fighter target, Fighter attacker) {
   return Lines(target, attacker, "무효").Any(x => x == "상대의 「일격필살」 기술을 무효화한다");
  }
  public static bool FloatsFromAbility(Fighter f) {
   return f != null && f.OnField && (f.Data.potentials ?? new Potential[0]).Any(p => p != null && p.id == AbilityModules.Slot && (p.description ?? "").Contains("자신은 공중에 떠오른다"));
  }
  [ThreadStatic] static bool piercing;
  public static bool PiercesAbility(Fighter f) {
   if (f == null || piercing) return false;
   piercing = true;
   try { return Lines(f, null, "관통").Any(x => x == Pierce); } finally { piercing = false; }
  }
  public static bool BlocksDrop(Fighter target, int stat) {
   string[] names = { "", "공격", "방어", "특공", "특방", "속도", "명중", "회피" };
   foreach (string line in Lines(target, null, "내려가지")) {
    var m = DropImmune.Match(line);
    if (!m.Success) continue;
    if (!m.Groups["s"].Success) return true;
    if (stat >= 0 && stat < names.Length && m.Groups["s"].Value.Trim('「', '」').Split('/').Contains(names[stat])) return true;
   }
   return false;
  }
  public static int PriorityBonus(Fighter a, Move m) {
   int total = 0;
   foreach (string line in Lines(a, null, "우선도")) {
    var x = Priority.Match(line); MoveCondition c;
    if (x.Success && MoveCondition.TryParse(x.Groups["c"].Value, out c) && c.Matches(m, a, 1)) total += int.Parse(x.Groups["n"].Value);
   }
   return total;
  }
  // 타입 추가: 배틀 포켓몬의 타입에 붙였다 뗀다(AbilityModules.Sync가 부른다)
  public static void SyncTypes(Fighter f) {
   if (f == null || f.Data == null) return;
   var c = f.Conditions;
   var types = (f.Data.types ?? new string[0]).ToList();
   foreach (string old in c.AddedTypes ?? new string[0]) types.Remove(old);
   var added = new List<string>();
   foreach (var p in (f.Data.potentials ?? new Potential[0]).Where(p => p != null && p.id == AbilityModules.Slot))
    foreach (Match m in Regex.Matches(p.description ?? "", @"자신의 타입에 「([^」]+)」를 추가한다")) if (!types.Contains(m.Groups[1].Value)) { types.Add(m.Groups[1].Value); added.Add(m.Groups[1].Value); }
   f.Data.types = types.ToArray();
   c.AddedTypes = added.ToArray();
  }
 }
}
