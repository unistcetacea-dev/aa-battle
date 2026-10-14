using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace AABattle
{
  // 상태 조건만으로 정해지는 일회성 효과는 해당 턴이 시작될 때 큐에 올린다.
  public static class TurnStateEffects
  {
    static readonly ConditionalWeakTable<Fighter, Potential[]> OriginalPotentialLists = new ConditionalWeakTable<Fighter, Potential[]>();

    public static void Run(Fighter[] fighters, Rules rules, Action<string> log)
    {
      if (fighters == null || fighters.Length != 2 || rules == null)
        return;

      for (int side = 0; side < fighters.Length; side++)
      {
        var owner = fighters[side];
        var other = fighters[1 - side];
        if (owner == null || owner.Data == null || owner.HP <= 0 || !owner.OnField)
          continue;
        var activeLines = new HashSet<string>(StringComparer.Ordinal);

        var queued = new List<Potential>();
        int scalarIndex = 0;
        foreach (var potential in (owner.Data.potentials ?? new Potential[0]).Where(x => x != null))
        {
          string trigger, effect;
          PotentialLibrary.Split(potential.description ?? "", out trigger, out effect);
          if (trigger.Length == 0 || effect.Length == 0)
            continue;
          var clauses = TriggerStructures.Clauses(potential).ToArray();
          bool hasTurnClause = clauses.Any(clause =>
          {
            TriggerSpec turn;
            return TurnConditions.TryParse(TriggerStructures.Normalize(clause), out turn);
          });
          bool holds = hasTurnClause && clauses.All(clause =>
          {
            TriggerSpec turn;
            return TurnConditions.TryParse(TriggerStructures.Normalize(clause), out turn)
              ? TurnConditions.Holds(turn, owner, rules)
              : TriggerModules.Holds(clause, owner, other, rules);
          });
          if (!holds)
            continue;

          string normalizedEffect = EffectNormalizer.NormalizeLines(effect);
          var lines = System.Text.RegularExpressions.Regex.Split(normalizedEffect, @"\r?\n|(?<=[.．。])\s+")
            .Where(line => line.Trim().Length > 0).ToArray();
          foreach (string line in lines)
            activeLines.Add(PotentialParts.Clean(line.Trim().TrimEnd('.', '．', '。')));
          var scalarLines = lines.Where(line =>
          {
            ScalarEffect scalar;
            return (line.Contains("배") || line.Contains("배율")) &&
              (EffectModules.TryScalar(line.Trim(), out scalar) || AbilityCriticalFieldModules.TryFieldScalar(line.Trim(), out scalar));
          }).ToArray();
          foreach (string line in scalarLines)
            PotentialModifiers.Add(owner, new ActiveModifier
            {
              Key = PotentialKeys.For(owner.Data, potential) + "#turn#" + rules.Turn + "#" + scalarIndex++,
              Text = line.Trim(),
              Source = "『" + potential.name + "』",
              Turns = 1
            });
          var otherLines = lines.Except(scalarLines).ToArray();
          if (otherLines.Length == 0)
            continue;

          // 큐에는 비배율 줄만 넣고, 원본 턴 조건은 명시 trigger로 보존한다.
          queued.Add(new Potential
          {
            id = potential.id,
            name = potential.name,
            description = string.Join("\n", otherLines),
            activation = potential.activation,
            uses = potential.uses,
            triggers = (potential.triggers ?? new string[0]).Concat(clauses.Select(TriggerStructures.Normalize)).Distinct().ToArray(),
            unlocks = potential.unlocks == null ? null : (string[])potential.unlocks.Clone()
          });
        }

        if (queued.Count == 0)
          continue;

        var originalPotentials = owner.Data.potentials;
        try
        {
          owner.Data.potentials = queued.ToArray();
          PotentialQueue.Raise(new PotentialEvent
          {
            Owner = owner,
            Other = other,
            Label = "턴 조건",
            Message = owner.Data.name + "의 턴 조건이 성립했다",
            Log = log,
            Spec = clause =>
            {
              TriggerSpec turn;
              return TurnConditions.TryParse(TriggerStructures.Normalize(clause), out turn) && TurnConditions.Holds(turn, owner, rules);
            }
          });
        }
        finally
        {
          owner.Data.potentials = originalPotentials;
        }

        // 기존 모으기 실행기가 읽는 상시 문장으로 해당 턴 효과를 노출한다.
        var chargeSkip = activeLines.FirstOrDefault(ChargeSkipModules.IsSkip);
        if (chargeSkip != null && !OriginalPotentialLists.TryGetValue(owner, out _))
        {
          OriginalPotentialLists.Add(owner, originalPotentials);
          owner.Data.potentials = (originalPotentials ?? new Potential[0]).Concat(new[]
          {
            new Potential
            {
              id = "종족 ①",
              name = "턴 조건 임시 모으기 생략",
              description = chargeSkip + ".",
              triggers = new[] { TriggerStructures.AlwaysLabel }
            }
          }).ToArray();
        }
      }
    }

    public static void Restore(Fighter[] fighters)
    {
      foreach (var fighter in fighters ?? new Fighter[0])
      {
        if (fighter == null || fighter.Data == null) continue;
        Potential[] original;
        if (!OriginalPotentialLists.TryGetValue(fighter, out original)) continue;
        fighter.Data.potentials = original;
        OriginalPotentialLists.Remove(fighter);
      }
    }
  }
}
