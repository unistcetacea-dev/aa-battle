using System;
using System.Linq;

namespace AABattle
{
  // PT 참가 상태의 대기 멤버가 가진 확률 데이터·소지품 해석 포텐셜을 T 개시 시 처리한다.
  public static class ObserverChanceRolls
  {
    public static void Run(Database db, Fighter[] field, Rules rules, TrainerBattleState[] states, Func<double> rng, Action<string> log)
    {
      if (field == null || field.Length != 2 || rules == null || states == null || states.Length < 2 || rng == null)
        return;

      for (int side = 0; side < 2; side++)
      {
        var roster = rules.Parties[side] ?? new[] { field[side] };
        foreach (var observer in roster.Where(x => x != null && x.HP > 0 && !Mechanics.SameMember(x, field[side])))
        {
          var eligible = (observer.Data.potentials ?? new Potential[0]).Where(p => p != null &&
            TriggerStructures.Clauses(p).Any(c => TriggerStructures.Normalize(c) == TriggerStructures.ObserverLabel) &&
            IsAnalysisEffect(p.description)).ToArray();
          if (eligible.Length == 0)
            continue;

          Potential[] original = observer.Data.potentials;
          int originalSide = observer.FieldSide;
          Fighter originalOpponent = observer.FieldOpponent;
          try
          {
            // 대기 멤버에게도 큐의 확률·중확률 보장·선택·사용 제한을 그대로 적용한다.
            observer.Data.potentials = eligible;
            observer.FieldSide = side;
            observer.FieldOpponent = field[1 - side];
            PotentialQueue.Raise(new PotentialEvent
            {
              Owner = observer,
              Other = field[1 - side],
              Label = TriggerStructures.ObserverLabel,
              Message = observer.Data.name + "의 PT 참가 포텐셜을 판정한다",
              Log = log,
              Spec = clause => TriggerStructures.Normalize(clause) == TriggerStructures.ObserverLabel
            });
          }
          finally
          {
            observer.Data.potentials = original;
            observer.FieldSide = originalSide;
            observer.FieldOpponent = originalOpponent;
          }
        }
      }
    }

    static bool IsAnalysisEffect(string description)
    {
      string trigger, effect;
      PotentialLibrary.Split(description ?? "", out trigger, out effect);
      if (effect.Length == 0)
        effect = description ?? "";
      foreach (string line in EffectNormalizer.NormalizeLines(effect).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
      {
        string probability, body;
        string value = EffectNormalizer.TryProbabilityPrefix(line.Trim(), out probability, out body) ? body : line;
        string normalized = EffectNormalizer.Normalize(value);
        if (MoveModules.Classify(normalized) == "데이터 해석" || AbilityItemModules.Classify(normalized) == "소지품 해석")
          return true;
      }
      return false;
    }
  }
}
