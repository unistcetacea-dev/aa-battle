// 실행 모듈의 입력 틀과 공용 빈칸 데이터. 배틀 판정에는 관여하지 않는다.
using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 public enum ModuleKind { TriggerEvent, TriggerState, Effect, TriggerLabel }
 public enum ModuleSlotKind {
  Subject, MoveCondition, Stats, Rank, Multiplier, Denominator, State, Weather, Terrain, Room,
  Hazard, Item, Ability, Move, Type, Turns, Probability, Count, HpRatio, Comparison, Text,
  Boolean, TrainerAptitude, Grade, Trigger, StatValue, Timing
 }
 public sealed class ModuleSlot {
  public string Id, Name, Default;
  public ModuleSlotKind Kind;
  public string[] Choices;
  public decimal Minimum = 0, Maximum = 999;
  public int Decimals;
  public bool Multiple;
  // true면 선택지에 있는 값만 읽는다(인접한 빈칸 사이를 가르는 +/- 같은 짧은 값).
  public bool Strict;
  public Func<PokemonRecord,IDictionary<string,string>,int[]> ContextRange;
  public ModuleSlot(string id, ModuleSlotKind kind, string defaultValue, params string[] choices) {
   Id = Name = id; Kind = kind; Default = defaultValue; Choices = choices ?? new string[0];
   if (kind == ModuleSlotKind.Multiplier) { Decimals = 2; Maximum = 100; }
   if (kind == ModuleSlotKind.Denominator) Minimum = 1;
   if (kind == ModuleSlotKind.Rank) { Minimum = -6; Maximum = 6; }
  }
  public bool Numeric {
   get { return new[] { ModuleSlotKind.Rank, ModuleSlotKind.Multiplier, ModuleSlotKind.Denominator,
    ModuleSlotKind.Turns, ModuleSlotKind.Count, ModuleSlotKind.StatValue }.Contains(Kind); }
  }
  public string[] GetChoices(Database db = null) {
   if (Choices.Length > 0) return Choices.ToArray();
   IEnumerable<string> values;
   switch (Kind) {
    case ModuleSlotKind.Subject: values = new[] { "자신", "상대", "아군", "아군 1체", "아군 전체", "자신과 상대" }; break;
    case ModuleSlotKind.Type: values = MoveCondition.TypeNames; break;
    case ModuleSlotKind.Stats: values = new[] { "체력", "공격", "방어", "특공", "특방", "속도", "명중", "회피", "C", "공/특공", "방/특방", "전능력치", "임의의 능력", "랜덤한 능력치", "가장 높은 능력", "가장 낮은 능력" }; break;
    case ModuleSlotKind.State: values = TriggerModules.KnownStates(); break;
    case ModuleSlotKind.Weather: values = Mechanics.Weathers; break;
    case ModuleSlotKind.Terrain: values = EffectModules.TerrainNames; break;
    case ModuleSlotKind.Room: values = new[] { "트릭룸", "중력" }; break;
    case ModuleSlotKind.Hazard: values = new[] { "스텔스록", "압정뿌리기", "독압정", "끈적끈적네트" }; break;
    case ModuleSlotKind.Ability: values = AbilityCatalog.All.Select(x => x.name); break;
    case ModuleSlotKind.Move: values = db == null ? new string[0] : db.moves.Select(x => x.name); break;
    case ModuleSlotKind.Item: values = ItemCatalog.All.Select(x => x.Name); break;
    case ModuleSlotKind.Probability: values = EffectNormalizer.ProbabilityChoices; break;
    case ModuleSlotKind.Comparison: values = new[] { "이상", "이하", "높음", "낮음" }; break;
    case ModuleSlotKind.Boolean: values = new[] { "예", "아니오" }; break;
    case ModuleSlotKind.TrainerAptitude: values = new[] { "지시", "육성", "통솔", "능력" }; break;
    case ModuleSlotKind.Grade: values = SpeciesStatRanks.Labels; break;
    case ModuleSlotKind.HpRatio: values = new[] { "1/2", "1/3", "1/4", "1/8", "가득", "가득하지 않음" }; break;
    case ModuleSlotKind.Timing: values = new[] { TriggerStructures.TurnStartLabel, TriggerStructures.TurnLastLabel, TriggerStructures.TurnEndLabel }; break;
    case ModuleSlotKind.MoveCondition:
     values = MoveCondition.TypeNames.Concat(new[] { "물리", "특수", "공격", "변화", "접촉", "비접촉", "타입일치", "타입불일치", "효과가굉장", "효과가별로", "효과보통", "위력 40 이하", "우선도 +", "소리", "탄/볼", "칼/검", "기술:몸통박치기" }); break;
    default: values = new string[0]; break;
   }
   return new[] { Default }.Concat(values).Where(x => x != null).Distinct().ToArray();
  }
  internal bool Valid(string value) {
   if (!Numeric) return true;
   decimal number;
   return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out number)
    && number >= Minimum && number <= Maximum && (Decimals > 0 || number == decimal.Truncate(number));
  }
 }
 public sealed class ModuleDefinition {
  public string Id, Name, Family, Template, Expected;
  public ModuleKind Kind;
  public bool AllowDuration;
  public ModuleSlot[] Slots;
  public Func<IDictionary<string,string>,string> Formatter;
  public Func<string,Dictionary<string,string>> Reader;
  public ModuleDefinition(string id, string name, ModuleKind kind, string family, string template, params ModuleSlot[] slots) {
   Id = id; Name = name; Kind = kind; Family = family; Template = template; Slots = slots; Expected = name;
  }
  public Dictionary<string,string> Defaults() { return Slots.ToDictionary(x => x.Id, x => x.Default); }
  public string Fill(IDictionary<string,string> values = null) {
   var resolved = Defaults();
   if (values != null) foreach (var pair in values) {
    if (!resolved.ContainsKey(pair.Key)) throw new ArgumentException("알 수 없는 빈칸: " + pair.Key);
    resolved[pair.Key] = pair.Value ?? "";
   }
   foreach (var slot in Slots)
    if (!slot.Valid(resolved[slot.Id])) throw new ArgumentException(slot.Name + " 값이 범위를 벗어났습니다.");
   string result = Formatter == null ? Regex.Replace(Template, @"\{([^{}]+)\}",
    m => resolved[m.Groups[1].Value]) : Formatter(resolved);
   return Regex.Replace(result ?? "", @"[ \t]+", " ").Trim();
  }
  public bool TryRead(string text, out Dictionary<string,string> values) {
   values = null;
   if (Reader != null) {
    var read = Reader(text ?? "");
    if (read == null) return false;
    var result = Defaults();
    foreach (var pair in read) {
     if (!result.ContainsKey(pair.Key)) return false;
     result[pair.Key] = pair.Value;
    }
    if (Slots.Any(s => !s.Valid(result[s.Id]))) return false;
    values = result; return true;
   }
   if (!BuildTemplateRegex()) return false;
   var match = templateRegex.Match((text ?? "").Trim());
   if (!match.Success) return false;
   var parsed = Defaults();
   for (int i = 0; i < Slots.Length; i++) {
    if (templateSeen.Contains(Slots[i].Id)) parsed[Slots[i].Id] = match.Groups["v" + i].Value.Trim();
    if (!Slots[i].Valid(parsed[Slots[i].Id])) return false;
   }
   if (Fill(parsed) != (text ?? "").Trim()) return false;
   values = parsed; return true;
  }

  // 문장 틀 → 정규식은 한 번만 만든다(편집기가 모든 모듈을 차례로 읽어 보기 때문).
  Regex templateRegex;
  HashSet<string> templateSeen;
  bool templateBuilt, templateBroken;
  bool BuildTemplateRegex() {
   if (templateBuilt) return !templateBroken;
   templateBuilt = true;
   string pattern = "^";
   int position = 0;
   var seen = new HashSet<string>();
   foreach (Match token in Regex.Matches(Template, @"\{([^{}]+)\}")) {
    pattern += Regex.Escape(Template.Substring(position, token.Index - position)).Replace(@"\ ", @"\s*");
    string id = token.Groups[1].Value;
    int index = Array.FindIndex(Slots, s => s.Id == id);
    if (index < 0) { templateBroken = true; return false; }
    string group = "v" + index;
    pattern += seen.Add(id) ? "(?<" + group + ">.*?)" : @"\k<" + group + ">";
    position = token.Index + token.Length;
   }
   pattern += Regex.Escape(Template.Substring(position)).Replace(@"\ ", @"\s*") + "$";
   templateRegex = new Regex(pattern, RegexOptions.Singleline);
   templateSeen = seen;
   return true;
  }
 }
 public static partial class ModuleCatalog {
  static readonly Lazy<ModuleDefinition[]> entries = new Lazy<ModuleDefinition[]>(Build);
  public static ModuleDefinition[] All { get { return entries.Value.ToArray(); } }
  static ModuleDefinition[] Build() {
   var list = new List<ModuleDefinition>();
   RegisterTriggers(list); RegisterTriggerLabels(list); RegisterCoreEffects(list); RegisterAbilityEffects(list);
   if (list.GroupBy(x => x.Id).Any(x => x.Count() > 1)) throw new InvalidOperationException("중복 모듈 ID");
   foreach (var module in list.Where(x => x.Kind == ModuleKind.Effect)) AttachModifiers(module);
   return list.ToArray();
  }
  static void AttachModifiers(ModuleDefinition module) {
   var original = new ModuleDefinition(module.Id, module.Name, module.Kind, module.Family, module.Template, module.Slots) {
    Expected = module.Expected, Formatter = module.Formatter, Reader = module.Reader
   };
   var probability = S("발동확률", ModuleSlotKind.Probability, "없음",
    "없음", "드물게", "저확률", "중확률", "고확률", "1할", "2할", "3할", "4할", "5할", "6할", "7할", "8할", "9할", "10할");
   var common = new List<ModuleSlot> { probability };
   if (module.AllowDuration) common.Add(S("효과기간", ModuleSlotKind.Turns, "없음",
    "없음", "T종료시까지", "1T동안", "2T동안", "3T동안", "4T동안", "5T동안"));
   // 기간은 숫자뿐 아니라 기존 문장 접두어 선택값이다.
   foreach (var slot in common.Where(x => x.Id == "효과기간")) slot.Kind = ModuleSlotKind.Text;
   module.Slots = original.Slots.Concat(common).ToArray();
   module.Template = "{발동확률} " + (module.AllowDuration ? "{효과기간} " : "") + original.Template;
   module.Formatter = values => {
    string body = original.Fill(original.Slots.ToDictionary(x => x.Id, x => values[x.Id]));
    if (module.AllowDuration && values["효과기간"] != "없음") body = values["효과기간"] + " " + body;
    string chance = values["발동확률"];
    return chance.EndsWith("할", StringComparison.Ordinal) ? chance + "로 " + body : EffectNormalizer.WithProbability(chance, body);
   };
   module.Reader = text => {
    string chance, body;
    if (!EffectNormalizer.TryProbabilityPrefix(text, out chance, out body)) { chance = "없음"; body = text; }
    string duration = "없음";
    if (module.AllowDuration) {
     var match = Regex.Match(body ?? "", @"^(?<duration>T\s*종료\s*시\s*까지|\d+\s*T\s*(?:동안|간))\s+");
     if (match.Success) { duration = match.Groups["duration"].Value; body = body.Substring(match.Length); }
    }
    Dictionary<string,string> values;
    if (!original.TryRead(body, out values)) return null;
    values["발동확률"] = chance;
    if (module.AllowDuration) values["효과기간"] = duration;
    return values;
   };
  }
  static partial void RegisterTriggers(List<ModuleDefinition> modules);
  static partial void RegisterCoreEffects(List<ModuleDefinition> modules);
  static partial void RegisterAbilityEffects(List<ModuleDefinition> modules);
  static ModuleSlot S(string id, ModuleSlotKind kind, string value, params string[] choices) {
   return new ModuleSlot(id, kind, value, choices);
  }
  public static ModuleDefinition Find(string id) { return entries.Value.FirstOrDefault(x => x.Id == id); }
  public static bool TryRead(ModuleKind kind, string text, out ModuleDefinition module, out Dictionary<string,string> values) {
   foreach (var candidate in entries.Value.Where(x => x.Kind == kind))
    if (candidate.TryRead(text, out values)) { module = candidate; return true; }
   module = null; values = null; return false;
  }
 }
}
