using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AABattle {
 // 트리거 없는 종족치 수치 가산은 장소에 나올 때 한 번 적용하고 퇴장 시 원본으로 복원한다.
 public static class SpeciesValueModules {
  public static string Normalize(string text) {
   if (string.IsNullOrWhiteSpace(text) || !text.Contains("종족치"))
    return null;
   var match = Regex.Match(text.Trim().TrimEnd('.', '。').Trim(), @"^(?<subject>자신|상대)의\s*(?<stat>전종족치|「[^」]+」)\s*종족치를\s*(?<value>\d+)\s*올린다$");
   return match.Success ? match.Groups["subject"].Value + "의 " + match.Groups["stat"].Value + " 종족치를 " + match.Groups["value"].Value + " 올린다" : null;
  }

  public static bool TryParse(string text, out SpeciesEffect effect) {
   effect = null;
   var match = Regex.Match(text ?? "", @"^(?<subject>자신|상대)의\s*(?<stat>전종족치|「(?<name>[^」]+)」)\s*종족치를\s*(?<value>\d+)\s*올린다$");
   if (!match.Success)
    return false;
   int[] stats;
   if (match.Groups["stat"].Value == "전종족치")
    stats = new[] { 0, 1, 2, 3, 4, 5 };
   else {
    string name = match.Groups["name"].Value;
    int index = Array.IndexOf(Engine.Names, name);
    if (index < 0 || index > 5)
     return false;
    stats = new[] { index };
   }
   effect = new SpeciesEffect { Subject = match.Groups["subject"].Value, Stats = stats, Operation = "가산", Value = int.Parse(match.Groups["value"].Value) };
   return true;
  }

  public static int Add(int current, int amount) { return current + amount; }

  public static void ApplyPassive(Fighter fighter, Action<string> log) {
   if (fighter == null || fighter.Data == null || fighter.HP <= 0 || fighter.Data.@base == null || fighter.Data.@base.Length < 6)
    return;
   var effects = new List<SpeciesEffect>();
   foreach (var potential in fighter.Data.potentials ?? new Potential[0]) {
    if (potential == null)
     continue;
    if (!string.IsNullOrWhiteSpace(potential.uses) || (potential.activation ?? "").Trim() == "선행")
     continue;
    var clauses = TriggerStructures.Clauses(potential).Select(TriggerStructures.Normalize).Where(x => x.Length > 0).ToArray();
    if (clauses.Any(x => x != TriggerStructures.AlwaysLabel && x != TriggerStructures.FieldLabel))
     continue;

    string condition, effect;
    PotentialLibrary.Split(potential.description ?? "", out condition, out effect);
    string normalized = EffectNormalizer.NormalizeLines(effect.Length > 0 ? effect : potential.description ?? "");
    foreach (string line in normalized.Replace("\r", "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
     SpeciesEffect species;
     if (EffectModules.TrySpecies(line.Trim(), out species) && species.Subject == "자신" && species.Operation == "가산")
      effects.Add(species);
    }
   }

   if (effects.Count == 0)
    return;

   bool changed = false;
   foreach (var effect in effects) {
    if (effect.Value == 0)
     continue;
    foreach (int index in effect.Stats) {
     if (index < 0 || index >= fighter.Data.@base.Length)
      continue;
     if (!changed && fighter.Conditions.OriginalBase == null)
      fighter.Conditions.OriginalBase = (int[])fighter.Data.@base.Clone();
     int before = fighter.Data.@base[index];
     fighter.Data.@base[index] += effect.Value;
     changed = true;
     if (log != null)
      log(fighter.Data.name + ": " + Engine.Names[index] + " 종족치 " + before + "→" + fighter.Data.@base[index] + " (상시 종족치 가산)");
    }
   }

   if (changed && effects.Any(x => x.Stats.Contains(0)))
    fighter.HP = Math.Min(fighter.HP, Engine.Stats(fighter, fighter.Data.level, new Rules())[0]);
  }
 }
}
