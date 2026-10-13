// 트레이너 포텐셜 이력 기록과 재발동 (유대 포텐셜)
using System;
using System.Linq;
using System.Collections.Generic;

namespace AABattle
{
  public static partial class PotentialQueue
  {
    // 이 포텐셜이 속한 진영의 트레이너 상태
    public static TrainerBattleState StateOf(EffectScope scope, PotentialInstance source)
    {
      if (scope == null || scope.States == null)
        return null;
      int side = SideOf(scope, source);
      return side < 0 || side >= scope.States.Length ? null : scope.States[side];
    }

    // 트레이너 포텐셜이 발동하면 그 포켓몬의 이력에 남긴다
    static void RecordTrainerUse(EffectScope scope, PotentialInstance source)
    {
      if (!source.IsTrainerPotential || source.Owner == null)
        return;
      BondModules.Record(StateOf(scope, source), source, source.Owner);
    }

    // 이력의 포텐셜을 target을 대상으로 한 번 더 발동한다. 트리거·확률·사용 횟수는 보지 않는다
    public static void Replay(EffectScope scope, PotentialInstance template, Fighter target, PotentialEvent origin, List<string> log)
    {
      if (scope == null || template == null || target == null)
        return;
      var source = new PotentialInstance
      {
        Owner = target,
        Trainer = template.Trainer,
        Pokemon = template.Pokemon,
        Record = template.Record,
        Key = template.Key,
        Name = template.Name,
        Slot = template.Slot,
        Uses = template.Uses,
        Activation = template.Activation,
        Effect = template.Effect,
        Description = template.Description,
        Clauses = template.Clauses
      };
      var e = new PotentialEvent
      {
        Owner = target,
        Trainer = template.Trainer,
        Other = OpponentOf(target),
        Label = "재발동",
        Message = "재발동",
        Log = null
      };
      e.Id = ++nextEvent;
      var parent = current;
      e.ChainId = parent == null ? e.Id : parent.ChainId;
      e.Depth = parent == null ? 0 : parent.Depth + 1;
      if (e.Depth > MaxDepth)
      {
        log.Add("포텐셜 연쇄가 " + MaxDepth + "단계를 넘어 중단했다 — " + template.Name);
        return;
      }

      var saved = current;
      current = new Frame
      {
        ChainId = e.ChainId,
        Depth = e.Depth,
        ChainFired = parent == null ? new HashSet<string>() : parent.ChainFired,
        Parent = saved
      };
      try
      {
        var plan = BuildPlan(scope, e, source, false);
        log.Add(source.Label + " 재발동 · 효과 적용: " + source.Effect);
        foreach (var step in plan.Steps)
        {
          if (step.Executor == null)
          {
            log.Add(source.Label + " 효과 판정 대기: " + step.Line);
            continue;
          }

          var lines = new List<string>();
          step.Executor.Run(new EffectCall
          {
            Source = source,
            Event = e,
            Line = step.Line,
            Rules = scope.Rules,
            Rng = scope.Rng,
            Log = lines
          });
          log.AddRange(lines);
        }
      }
      finally
      {
        current = saved;
      }

      BondModules.Record(StateOf(scope, source), source, target);
    }
  }
}
