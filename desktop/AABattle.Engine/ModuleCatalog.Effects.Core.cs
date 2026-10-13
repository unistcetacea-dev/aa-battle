using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle
{
  public static partial class ModuleCatalog
  {
    static readonly HashSet<string> coreEffectIds = new HashSet<string>();
    public static string[] CoreEffectIds { get { var all = All; return coreEffectIds.ToArray(); } }

    // 문장 조사만 가변 처리한다. 판정·정규화·실행에는 관여하지 않는다.
    static ModuleDefinition CoreAdd(List<ModuleDefinition> modules, string id, string name,
      string expected, string family, string template, params ModuleSlot[] slots)
    {
      var defaults = slots.ToDictionary(x => x.Id, x => x.Default);
      Func<IDictionary<string, string>, string> fill = values =>
      {
        string result = Regex.Replace(template, @"\{([^{}]+)\}", m => values[m.Groups[1].Value]);
        result = Regex.Replace(result, @"@(ro|object|subject):([^@]+)@", m =>
        {
          string value = values[m.Groups[2].Value];
          if (m.Groups[1].Value == "ro") return EffectModules.Ro(value);
          char ch = value.Reverse().FirstOrDefault(c => c >= '가' && c <= '힣');
          bool batchim = ch == 0 || (ch - '가') % 28 != 0;
          return m.Groups[1].Value == "object" ? (batchim ? "을" : "를") : (batchim ? "이" : "가");
        });
        return Regex.Replace(result, @"\s+", " ").Trim();
      };
      string display = Regex.Replace(template, @"@(ro|object|subject):([^@]+)@", m =>
      {
        string value = defaults[m.Groups[2].Value];
        if (m.Groups[1].Value == "ro") return EffectModules.Ro(value);
        char ch = value.Reverse().FirstOrDefault(c => c >= '가' && c <= '힣');
        bool batchim = ch == 0 || (ch - '가') % 28 != 0;
        return m.Groups[1].Value == "object" ? (batchim ? "을" : "를") : (batchim ? "이" : "가");
      });
      // 선택지가 있는 빈칸은 먼저 선택지 값으로만 읽고(strict), 안 맞으면 느슨하게 읽는다.
      var strictRegex = CoreTemplateRegex(template, slots, true);
      var regex = CoreTemplateRegex(template, slots, false);
      var module = new ModuleDefinition(id, name, ModuleKind.Effect, family, display, slots)
      {
        Expected = expected,
        AllowDuration = expected == "수치 배율" || expected == "상태 부여" || expected == "장소 전개" || family == "방호·회피" || family == "타입·상성" || id == "effect.analysis.immune",
        Formatter = fill,
        Reader = text =>
        {
          foreach (var candidate in new[] { strictRegex, regex })
          {
            var match = candidate.Match(text ?? "");
            if (!match.Success) continue;
            var values = slots.ToDictionary(x => x.Id, x => match.Groups[x.Id].Value.Trim());
            if (fill(values) == text) return values;
          }
          return null;
        }
      };
      modules.Add(module);
      coreEffectIds.Add(id);
      return module;
    }

    static Regex CoreTemplateRegex(string template, ModuleSlot[] slots, bool preferChoices)
    {
      string pattern = Regex.Escape(template);
      // 인접한 빈칸이 서로 글자를 가로채지 않도록 종류가 정해진 빈칸은 모양을 제한한다.
      foreach (var slot in slots)
        pattern = pattern.Replace(Regex.Escape("{" + slot.Id + "}"), "(?<" + slot.Id + ">" + CoreSlotPattern(slot, preferChoices) + ")");
      pattern = Regex.Replace(pattern, @"@(ro|object|subject):([^@]+)@", m =>
        m.Groups[1].Value == "ro" ? "(?:으로|로)" : m.Groups[1].Value == "object" ? "(?:을|를)" : "(?:이|가)");
      // 빈 기술 조건의 공백은 Fill에서 한 칸으로 줄어든다.
      pattern = pattern.Replace(@"\ ", @"\s*");
      return new Regex("^" + pattern + "$", RegexOptions.CultureInvariant);
    }

    static string CoreSlotPattern(ModuleSlot slot, bool preferChoices)
    {
      if (slot.Numeric)
        return slot.Decimals > 0 ? @"[0-9]+(?:\.[0-9]+)?" : @"[0-9]+";
      if (slot.Kind == ModuleSlotKind.MoveCondition)
        return @"(?:「[^」]+」\s*)*";
      if (slot.Kind == ModuleSlotKind.Stats)
        return @"[^+\-{}「」]+?";
      bool closed = slot.Strict || slot.Kind == ModuleSlotKind.Timing || preferChoices;
      if (closed && slot.Choices.Length > 0 && !slot.Multiple)
        return "(?:" + string.Join("|", slot.Choices.OrderByDescending(x => x.Length).Select(Regex.Escape)) + ")";
      return ".*?";
    }
    static ModuleSlot CoreNumber(string id, ModuleSlotKind kind, string value, int max = 999)
    {
      return new ModuleSlot(id, kind, value) { Minimum = 1, Maximum = max };
    }

    static partial void RegisterCoreEffects(List<ModuleDefinition> modules)
    {
      RegisterCoreStats(modules);
      RegisterCorePlaces(modules);
      RegisterCoreTypes(modules);
      RegisterCoreActions(modules);
      RegisterCoreMoveEffects(modules);
      RegisterMoveItemEffects(modules);
      RegisterRoleEffects(modules);
      RegisterExclusiveEffects(modules);
    }

    static void RegisterCoreStats(List<ModuleDefinition> modules)
    {
      var subjects = new[] { "자신", "상대", "아군", "아군 1체", "아군 전체" };
      var rankTargets = new[] { "자신", "상대", "아군", "아군 1체", "아군 전체" }
        .SelectMany(s => new[] { "「공격」", "「방어」", "「특공」", "「특방」", "「속도」", "「명중」", "「회피」", "「C」", "임의의 능력", "랜덤한 능력치", "가장 높은 능력", "가장 낮은 능력", "전능력치" }.Select(t => s + "의 " + t)).ToArray();
      foreach (string direction in new[] { "raise", "lower" })
      {
        bool down = direction == "lower";
        for (int rank = 1; rank <= 3; rank++)
        {
          string degree = rank == 3 ? "매우 크게 " : rank == 2 ? "크게 " : "";
          CoreAdd(modules, "effect.rank." + direction + rank, "능력 " + rank + "랭크 " + (down ? "하락" : "상승"), "능력 랭크 변화", "능력 랭크",
            "{대상}@" + (down ? "object" : "subject") + ":대상@ " + degree + (down ? "내린다." : "오른다."), S("대상", ModuleSlotKind.Text, "자신의 「공격」", rankTargets));
        }
      }
      foreach (string action in new[] { "강화", "약화" })
      {
        var scalarTargets = subjects.SelectMany(s => new[] { "「공격」", "「방어」", "「특공」", "「특방」", "「속도」", "「명중」", "「회피」", "전능력치", "가장 높은 능력", "가장 낮은 능력" }.Select(t => s + "의 " + t)).ToArray();
        CoreAdd(modules, "effect.scalar.stats." + action, "능력 " + action, "수치 배율", "수치 배율", "{대상}@object:대상@ " + action + "({배율}배)한다.",
          S("대상", ModuleSlotKind.Text, "자신의 「공격」", scalarTargets), S("배율", ModuleSlotKind.Multiplier, action == "강화" ? "1.5" : "0.5"));
        foreach (string stage in new[] { "위력", "명중" })
          CoreAdd(modules, "effect.scalar.move." + stage + action, "기술 " + stage + " " + action, "수치 배율", "수치 배율", "{주체}의 {조건} 기술의 " + stage + (stage == "위력" ? "을" : "을") + " " + action + "({배율}배)한다.",
            S("주체", ModuleSlotKind.Subject, "자신", subjects), S("조건", ModuleSlotKind.MoveCondition, ""), S("배율", ModuleSlotKind.Multiplier, "1.5"));
        foreach (string dir in new[] { "주는", "받는" })
          CoreAdd(modules, "effect.scalar.damage." + dir + action, dir + " 대미지 " + action, "수치 배율", "수치 배율", "{주체}@subject:주체@ " + dir + " {조건} 기술의 대미지를 " + action + "({배율}배)한다.",
            S("주체", ModuleSlotKind.Subject, "자신", subjects), S("조건", ModuleSlotKind.MoveCondition, ""), S("배율", ModuleSlotKind.Multiplier, "1.5"));
        CoreAdd(modules, "effect.scalar.damage.plain." + action, "대미지 " + action, "수치 배율", "수치 배율", "{주체}@subject:주체@ {방향} 대미지를 " + action + "({배율}배)한다.", S("주체", ModuleSlotKind.Subject, "자신", subjects), S("방향", ModuleSlotKind.Text, "주는", "주는", "받는"), S("배율", ModuleSlotKind.Multiplier, "1.5"));
      }
      foreach (string action in new[] { "회복", "소비", "대미지", "대미지 보호" })
        CoreAdd(modules, "effect.hp." + action, "최대 HP 비율 " + action, "최대 HP의 1/N", "체력", action.StartsWith("대미지") ? "{주체}에게 최대 HP의 1/{분모} 대미지를 준다." + (action.EndsWith("보호") ? " 이 대미지로는 빈사로 할 수 없다." : "") : "{주체}의 체력을 최대 HP의 1/{분모} " + action + "한다.", S("주체", ModuleSlotKind.Subject, action.StartsWith("대미지") ? "상대" : "자신", action == "소비" ? new[] { "자신" } : subjects), CoreNumber("분모", ModuleSlotKind.Denominator, "4"));
      var speciesTargets = new[] { "자신", "상대" }.SelectMany(s => new[] { "체력", "공격", "방어", "특공", "특방", "속도", "공/방", "공/특공", "방/특방", "공/방/특공" }.Select(t => s + "의 「" + t + "」").Concat(new[] { s + "의 전종족치" })).ToArray();
      var target = S("대상", ModuleSlotKind.Text, "자신의 「공격」", speciesTargets);
      foreach (string extreme in new[] { "높은", "낮은" })
        CoreAdd(modules, "effect.species.copy." + extreme, "가장 " + extreme + " 종족치 복사", "종족치 변경", "종족치", "{대상} 종족치를 가장 " + extreme + " 종족치로 한다.", target);
      foreach (string verb in new[] { "와 같게 한다", "까지 올린다" })
        CoreAdd(modules, "effect.species.copy." + verb, "다른 종족치 " + verb, "종족치 변경", "종족치", "{대상} 종족치를 자신의 「{원본}」 종족치" + verb + ".", target, S("원본", ModuleSlotKind.Stats, "방어", "체력", "공격", "방어", "특공", "특방", "속도"));
      CoreAdd(modules, "effect.species.value", "종족치 수치 지정", "종족치 변경", "종족치", "{대상} 종족치를 「{수치}」로 한다.", target, CoreNumber("수치", ModuleSlotKind.StatValue, "110"));
      CoreAdd(modules, "effect.species.value.add", "종족치 수치 가산", "종족치 변경", "종족치", "{대상} 종족치를 {수치} 올린다.", target, CoreNumber("수치", ModuleSlotKind.StatValue, "50"));
      CoreAdd(modules, "effect.species.raise", "종족치 랭크 상승", "종족치 변경", "종족치", "{대상} 종족치를 「{수치}」로 하여 {랭크}랭크 올린다.", target, CoreSpeciesValue(), CoreNumber("랭크", ModuleSlotKind.Rank, "1", 2));
      CoreAdd(modules, "effect.species.raise.auto", "종족치 자동 랭크 상승", "종족치 변경", "종족치", "{대상} 종족치를 {랭크}랭크 올린다.", target, CoreNumber("랭크", ModuleSlotKind.Rank, "1", 2));
      foreach (string verb in new[] { "로 한다", "까지 올린다", "까지 내린다" })
      {
        CoreAdd(modules, "effect.species.grade." + verb, "종족치 등급 " + verb, "종족치 변경", "종족치", "{대상} 종족치를 「{등급}」" + verb + ".", target, S("등급", ModuleSlotKind.Grade, "A", SpeciesStatRanks.Labels));
        CoreAdd(modules, "effect.species.grade.value." + verb, "등급 범위 내 종족치 " + verb, "종족치 변경", "종족치", "{대상} 종족치를 「{등급}」 범위의 「{수치}」" + verb + ".", target, S("등급", ModuleSlotKind.Grade, "A", SpeciesStatRanks.Labels), CoreSpeciesValue());
      }
      CoreAdd(modules, "effect.species.trainer", "트레이너 자질까지 종족치 상승", "종족치 변경", "종족치", "{대상} 종족치를 트레이너 「{자질}」 등급까지 올린다.", target, S("자질", ModuleSlotKind.TrainerAptitude, "육성", "지시", "육성", "통솔", "능력"));
    }
  }
}





