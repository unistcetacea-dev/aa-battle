using System;
using System.Linq;
using System.Collections.Generic;

namespace AABattle
{
  // 마검의 기능확장을 턴 최후의 추가행동 큐에 연결한다.
  public static class MakenTurnLast
  {
    public static void Register()
    {
      PotentialExecutors.Register("마검 턴 최후 기능확장", CanGrant, Grant);
    }

    public static void Process(Fighter[] fighters, List<ActionSlot> slots, int next, List<string> log, ref bool processed)
    {
      if (processed || fighters == null || slots == null || log == null)
        return;
      processed = true;
      var events = fighters.Where(x => x != null && x.HP > 0 && x.OnField
          && (x.Conditions.TurnFacts ?? new List<TriggerFact>()).Any(f => f != null
            && f.Kind == "등장" && f.Self && f.Method == "통상교대"))
        .Select(owner => new PotentialEvent
        {
          Owner = owner,
          Other = fighters.FirstOrDefault(x => x != null && !ReferenceEquals(x, owner)),
          Fact = new TriggerFact { Kind = "타이밍", Method = TriggerStructures.TurnLastLabel },
          Label = ExclusiveTriggerModules.NormalSwitchLastSentence,
          Message = owner.Data.name + " · " + TriggerStructures.TurnLastLabel,
          Log = log.Add,
          Announce = true,
          Spec = clause => ExclusiveTriggerModules.IsNormalSwitchLast(TriggerModules.Parse(TriggerStructures.Normalize(clause))),
          PendingText = (source, extra) => "『" + source.Name + "』 통상교대로 장소에 나온 T의 최후 트리거 충족 · 효과 판정 대기: " + source.Description
        }).ToArray();
      PotentialQueue.Raise(events);
      ExtraActionRuntime.Pull(slots, next, log);
    }

    static bool CanGrant(EffectCall call)
    {
      string subject;
      string[] moves;
      return call != null && call.Owner != null && call.Event != null
        && ExclusiveTriggerModules.MatchesNormalSwitchLast(call.Event.Fact, call.Owner)
        && FunctionExtensions.TryGrant(call.Line, out subject, out moves);
    }

    static EffectOutcome Grant(EffectCall call)
    {
      string subject;
      string[] moves;
      if (call.Owner == null || call.Owner.HP <= 0
        || !FunctionExtensions.TryGrant(call.Line, out subject, out moves))
        return EffectOutcome.Failed;
      var scope = EffectScope.Current;
      int side = scope == null ? -1 : Array.IndexOf(scope.Fighters, call.Owner);
      if (side < 0)
        return EffectOutcome.Failed;

      foreach (string move in moves)
        if (!call.Owner.Conditions.ExtensionMoves.Contains(move))
          call.Owner.Conditions.ExtensionMoves.Add(move);
      scope.ExtraActions.Add(new ExtraActionGrant
      {
        Owner = call.Owner,
        Side = side,
        First = true,
        Condition = new MoveCondition { Names = moves.ToList() },
        Source = call.Source.Label
      });
      call.Say(call.Owner.Data.name + ": 턴 최후에 기능확장 추가행동을 등록했다");
      return EffectOutcome.Success;
    }
  }
}
