// 전용 포텐셜 유형이 쓰는 실행기: 사건으로 얻는 필중·방어 관통·방호 특성 관통, 추격 발동 배율, 상태변화 전체 해제
using System;
using System.Linq;
using System.Collections.Generic;

namespace AABattle
{
  public static partial class PotentialQueue
  {
    internal static partial class BuiltIn
    {
      public static void RegisterExclusive()
      {
        ExclusiveBorrowModules.Register();
        ForcedCounterModules.Register();
        // 사건으로 얻는 기술 판정(필중·방어 관통·방호 특성 관통): 기술 사건이면 이번 행동만, 아니면 필드를 떠날 때까지
        PotentialExecutors.Register("기술 판정 부여", c => AbilityMoveModules.IsEventGrant(c.Line), c =>
        {
          var owner = c.Owner;
          if (owner == null || owner.HP <= 0)
            return EffectOutcome.Failed;
          bool action = ActionEvent(c.Event);
          string text = c.Line.Trim();
          PotentialModifiers.Add(owner, new ActiveModifier { Key = c.Source.Key, Text = text, Source = c.Source.Label, Turns = -1, Action = action, Trainer = c.Source.IsTrainerPotential });
          c.Say(owner.Data.name + ": " + text + (action ? " (이번 행동)" : " (필드를 떠날 때까지)"));
          return EffectOutcome.Success;
        });
        // 추격 발동 배율은 추격이 실제로 일어날 때 PursuitTurn이 읽는다. 여기서는 준비만 알린다
        PotentialExecutors.Register("추격 발동 배율", c => ExclusiveModules.IsPursuitScale(c.Line), c =>
        {
          c.Say(c.Owner.Data.name + ": 추격 효과가 발동하면 대미지가 늘어난다");
          return EffectOutcome.Success;
        });
        PotentialExecutors.Register("상태변화 전체 해제", c =>
        {
          string target;
          return ExclusiveModules.TryReleaseAll(c.Line, out target);
        }, c =>
        {
          string target;
          ExclusiveModules.TryReleaseAll(c.Line, out target);
          var victim = target == "상대" ? c.Opponent : c.Owner;
          var lines = new List<string>();
          bool any = ExclusiveModules.ReleaseAllConditions(victim, lines);
          foreach (string line in lines)
            c.Say(line);
          if (!any)
            c.Say(c.Line + " — 해제할 상태변화가 없다");
          return any ? EffectOutcome.Success : EffectOutcome.Failed;
        });
      }
    }
  }
}
