using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle
{
  public static class SpeciesGradeTrigger
  {
    static readonly string[] Stats = { "체력", "공격", "방어", "특공", "특방", "속도" };
    static readonly string[] Grades = { "E", "D", "C", "B", "A", "AA", "AAA", "S" };

    public static int StatIndex(string stat)
    {
      return Array.IndexOf(Stats, stat);
    }

    internal static bool TryParse(string text, out TriggerSpec spec)
    {
      spec = null;
      var match = Regex.Match(text ?? "", @"^(?<s>자신|상대|아군)의\s*「(?<st>체력|공격|방어|특공|특방|속도)」\s*종족치가\s*「(?<g>E|D|C|B|A|AA|AAA|S)」\s*(?<op>이상|이하)일\s*때$");
      if (!match.Success)
        return false;

      spec = new TriggerSpec
      {
        Module = "종족치 등급",
        Subject = match.Groups["s"].Value,
        Names = new[] { match.Groups["st"].Value, match.Groups["g"].Value },
        Op = match.Groups["op"].Value
      };
      return true;
    }

    internal static bool TryParseSpeciesGrade(string text, out TriggerSpec spec)
    {
      return TryParse(text, out spec);
    }

    internal static bool Holds(TriggerSpec spec, Fighter target)
    {
      if (spec == null || target == null || target.Data == null)
        return false;
      int index = StatIndex(spec.Names[0]);
      if (index < 0 || target.Data.@base == null || target.Data.@base.Length <= index)
        return false;

      int value = target.Data.@base[index];
      string grade = spec.Names[1];
      int[] range = SpeciesStatRanks.LabelRange(grade + (grade == "S" ? "" : spec.Op == "이하" ? "+" : "-"));
      return spec.Op == "이하" ? value <= range[1] : value >= range[0];
    }

  }
}

namespace AABattle
{
  public static partial class ModuleCatalog
  {
    static void RegisterTriggerSpeciesGrades(List<ModuleDefinition> modules)
    {
      var subject = S("주체", ModuleSlotKind.Subject, "상대", "자신", "상대", "아군");
      var stat = S("능력", ModuleSlotKind.Stats, "속도", "체력", "공격", "방어", "특공", "특방", "속도");
      var grade = S("등급", ModuleSlotKind.Grade, "B", "E", "D", "C", "B", "A", "AA", "AAA", "S");
      var comparison = S("비교", ModuleSlotKind.Comparison, "이하", "이상", "이하");
      T(modules, "species.grade", "종족치 등급", "종족치 등급 비교", "종족치",
        "{주체}의 「{능력}」 종족치가 「{등급}」 {비교}일 때",
        () => new TriggerSpec { Module = "종족치 등급", Subject = "상대", Names = new[] { "속도", "B" }, Op = "이하" },
        TB(subject, (s, v) => { s.Subject = v; s.Opponent = v == "상대"; }, s => s.Subject),
        TB(stat, (s, v) => s.Names[0] = v, s => s.Names[0]),
        TB(grade, (s, v) => s.Names[1] = v, s => s.Names[1]),
        TB(comparison, (s, v) => s.Op = v, s => s.Op));
    }
  }
}
