using System;
using System.Linq;

namespace AABattle
{
  public static partial class PotentialQueue
  {
    internal static partial class BuiltIn
    {
      public static void RegisterExclusiveMore()
      {
        PotentialExecutors.Register("버서크 추가공격", c => ExclusiveModules.IsBerserkRepeat(c.Line), c =>
        {
          var scope = EffectScope.Current;
          var owner = c.Owner;
          if (scope == null || owner == null || c.Event.Move == null)
            return EffectOutcome.Failed;
          string marker = "berserk-repeat:" + c.Source.Key;
          var permit = owner.Conditions.Modifiers.FirstOrDefault(x => x != null && x.Key == marker);
          if (permit == null)
            return EffectOutcome.Failed;
          owner.Conditions.Modifiers.Remove(permit);
          int side = Array.IndexOf(scope.Fighters, owner);
          if (side < 0)
            side = Mechanics.PartySide(owner, c.Rules ?? new Rules());
          if (side < 0)
            return EffectOutcome.Failed;
          scope.ExtraActions.Add(new ExtraActionGrant
          {
            Owner = owner,
            Side = side,
            First = true,
            FixedMove = c.Event.Move.Copy(),
            Source = c.Source.Label
          });
          owner.Conditions.PendingAction = true;
          c.Say(owner.Data.name + ": 방금 사용한 공격으로 추가공격을 얻었다");
          return EffectOutcome.Success;
        });
        PotentialExecutors.Register("현재 HP 소비", c => ExclusiveModules.IsCurrentHpCost(c.Line), c =>
        {
          var owner = c.Owner;
          if (owner == null || owner.HP <= 1)
            return EffectOutcome.Failed;
          int amount = owner.HP - owner.HP / 2;
          Mechanics.Hurt(owner, c.Opponent, c.Rules ?? new Rules(), amount, c.Source.Label, c.Log, false);
          owner.Conditions.Modifiers.Add(new ActiveModifier { Key = "berserk-repeat:" + c.Source.Key, Text = "버서크 추가공격", Source = c.Source.Label, Turns = -1 });
          return EffectOutcome.Success;
        });
      }
    }
  }
}
