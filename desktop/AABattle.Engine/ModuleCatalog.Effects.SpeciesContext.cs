using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle
{
  public static partial class ModuleCatalog
  {
    // 편집 대상 포켓몬의 종족치에 맞추는 입력 범위. UI는 이 콜백을 공통으로 호출한다.
    static int[] CoreSpeciesRange(PokemonRecord pokemon, IDictionary<string, string> values)
    {
      int current = 0;
      var match = Regex.Match(values["대상"], "「([^」]+)」");
      int index = match.Success ? EffectModules.StatIndex(match.Groups[1].Value) : -1;
      if (pokemon != null && pokemon.stats != null)
      {
        int[] stats = { pokemon.stats.hp, pokemon.stats.attack, pokemon.stats.defense,
          pokemon.stats.spAttack, pokemon.stats.spDefense, pokemon.stats.speed };
        if (index >= 0 && index < stats.Length) current = stats[index];
      }
      string grade;
      if (values.TryGetValue("등급", out grade))
        return current > 0 ? SpeciesStatRanks.GradeRange(current, grade) : SpeciesStatRanks.LabelRange(grade);
      string ranks;
      if (values.TryGetValue("랭크", out ranks) && current > 0)
        return SpeciesStatRanks.RaisedRange(current, int.Parse(ranks, System.Globalization.CultureInfo.InvariantCulture));
      return new[] { 1, 999 };
    }

    static ModuleSlot CoreSpeciesValue()
    {
      var slot = CoreNumber("수치", ModuleSlotKind.StatValue, "110");
      slot.ContextRange = CoreSpeciesRange;
      return slot;
    }
  }
}
