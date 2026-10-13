using System;

namespace AABattle
{
  public static partial class PotentialQueue
  {
    internal static partial class BuiltIn
    {
      public static void RegisterExclusiveMore()
      {
        PotentialExecutors.Register("현재 HP 소비", c => ExclusiveModules.IsCurrentHpCost(c.Line), c =>
        {
          var owner = c.Owner;
          if (owner == null || owner.HP <= 1)
            return EffectOutcome.Failed;
          int amount = owner.HP / 2;
          Mechanics.Hurt(owner, c.Opponent, c.Rules ?? new Rules(), amount, c.Source.Label, c.Log, false);
          return EffectOutcome.Success;
        });
      }
    }
  }
}
