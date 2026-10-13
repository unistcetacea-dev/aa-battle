// 턴 진행과 행동 루프
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;

namespace AABattle
{
  public static partial class Engine
  {
    static TurnResult ResolveTurn(Database db, Fighter[] input, BattleCommand[] commands, Rules original, TrainerBattleState[] trainerInput, Func<double> rng, Func<SwitchRequest, Fighter> chooseSwitch)
    {
      if (input == null || input.Length != 2 || commands == null || commands.Length != 2)
        throw new Exception("양쪽 행동을 모두 선택하세요.");
      var states = (trainerInput ?? new[]
      {
        new TrainerBattleState(),
        new TrainerBattleState()
      }

      ).Select(x => (x ?? new TrainerBattleState()).Copy()).ToArray();
      var result = new TurnResult
      {
        Fighters = input.Select(p => p.Copy()).ToArray(),
        Rules = original.Copy(),
        Trainers = states
      };
      var f = result.Fighters;
      var chosenActors = f.ToArray();
      var r = result.Rules;
      EffectScope.Begin(f, r, rng, states, db);
      int[] deployed =
      {
        -1,
        -1
      };
      bool[] returnAway =
      {
        false,
        false
      }, deniedSwitch =
      {
        false,
        false
      };
      for (int i = 0; i < 2; i++)
      {
        f[i].OnField = true;
        TrainerAccessRules.ApplyTrainerPassives(f[i], states[i].Trainer, states[i], result.Log.Add);
      }

      foreach (var fighter in f)
        Mechanics.RefreshRollout(db, fighter, result.Log);
      commands = commands.Select(x => x ?? new BattleCommand()).ToArray();
      for (int i = 0; i < 2; i++)
        if (Mechanics.AutoAction(f[i]))
        {
          var prior = commands[i];
          commands[i] = new BattleCommand
          {
            Move = Mechanics.ActionMove(db, f[i], prior.Move),
            Party = prior.Party,
            Trainer = prior.Trainer
          };
          result.Log.Add(f[i].Data.name + ": " + Mechanics.AutoActionLabel(f[i]) + "의 " + Mechanics.AutoMoveName(f[i]) + " 자동 행동 — 지정 명령 무시");
        }

      Mechanics.SetParties(r, f, Enumerable.Range(0, 2).Select(i => (IEnumerable<Fighter>)((commands[i].Party ?? new Fighter[0]).Length > 0 ? commands[i].Party : r.Parties[i]).Where(x => x != null).Select(x => x.Copy()).ToArray()).ToArray());
      foreach (var fighter in f)
        Mechanics.RefreshEncore(db, fighter, result.Log);
      AbilityRuntime.RefreshSuppression(f, result.Log.Add);
      EnterAbilities(f, r, rng, i => !f[i].Conditions.AbilityEntryApplied, result.Log);
      var fresh = new bool[2];
      for (int i = 0; i < 2; i++)
      {
        fresh[i] = !f[i].Conditions.EntryTriggered;
        f[i].Conditions.EntryTriggered = true;
      }

      for (int i = 0; i < 2; i++)
        if (fresh[i])
          EntryTriggers.Enter(f[i], f[1 - i], r, rng, f[i].Conditions.PendingEntry, result.Log.Add);
      for (int i = 0; i < 2; i++)
        if (fresh[1 - i])
          RuntimeTriggers.OpponentEntered(f[i], f[1 - i], r, result.Log.Add);
      var pursuit = PursuitRuntime.Begin(db, f, commands, r, states, result.Log);
      var returnTransfers = new BatonSnapshot[2];
      for (int i = 0; i < 2; i++)
      {
        var c = commands[i] ?? new BattleCommand();
        string form = c.Form ?? "", order = c.Order ?? "";
        if (c.SwitchIn != null)
          TrainerAccessRules.ApplyTrainerPassives(c.SwitchIn, states[i].Trainer, states[i], result.Log.Add);
        string switchBlock;
        if (c.IsSwitch && !SwitchEffectRuntime.CanSwitch(f[1 - i], f[i], form.Contains("돌아와") ? SwitchReason.Return : SwitchReason.Normal, out switchBlock))
        {
          deniedSwitch[i] = true;
          result.Log.Add(f[i].Data.name + ": " + switchBlock);
          continue;
        }

        if (c.IsSwitch && form.Length > 0 && !form.Contains("돌아와"))
          throw new Exception("교대 행동에는 『돌아와!』만 함께 선언할 수 있습니다.");
        if (!c.IsSwitch && form.Contains("돌아와"))
          throw new Exception("『돌아와!』는 교대 행동과 함께 선언하세요.");
        if (c.IsSwitch && order.Length > 0)
          throw new Exception("지령은 기술 선택과 함께 선언하세요.");
        if (!c.IsSwitch && c.Move == null)
          throw new Exception("기술 또는 교대 행동을 선택하세요.");
        if (form.Length > 0)
        {
          if (!TrainerAccessRules.CanUseForm(form, states[i].Trainer, states[i], f[i].Data.potentials))
            throw new Exception("트레이너의 지시 등급으로 " + form + "을 사용할 수 없습니다.");
          if (states[i].UsedForms.Contains(form))
            throw new Exception(form + "은 이미 사용했습니다.");
          states[i].UsedForms.Add(form);
          result.Log.Add("TRAINER " + (i + 1) + " — 『" + form + "』 선언!");
        }

        if (order.Length > 0)
        {
          if (!TrainerAccessRules.CanUseOrder(order, states[i].Trainer, states[i], f[i].Data.potentials))
            throw new Exception("트레이너의 지시 등급으로 " + order + "을 사용할 수 없습니다.");
          if (states[i].UsedOrders.Contains(order))
            throw new Exception(order + "은 이미 사용했습니다.");
          states[i].UsedOrders.Add(order);
          result.Log.Add("TRAINER " + (i + 1) + " — 『" + order + "』 지령!");
          deployed[i] = ApplyOrder(f[i], f[1 - i], order, r, result.Log);
          if (deployed[i] == -2)
            c.Move = null;
        }

        foreach (string potential in c.PreInputs ?? new string[0])
        {
          var definition = (f[i].Data.potentials ?? new Potential[0]).FirstOrDefault(x => x != null && x.name == potential);
          string key = PotentialKeys.ForName(f[i].Data, potential), uses = definition == null ? "" : definition.uses;
          if (!states[i].Usage.CanUse(key, uses))
            throw new Exception("『" + potential + "』은 이미 사용했습니다.");
          states[i].Usage.Use(key, uses);
          states[i].Usage.Declared.Add(key);
          result.Log.Add(f[i].Data.name + " — 선행입력 『" + potential + "』 선언!" + ((definition == null ? "" : definition.description) ?? ""));
        }

        if (order.Length > 0)
        {
          RuntimeTriggers.DirectiveReceived(f[i], (c.Party ?? new Fighter[0]).Where(x => x != null && !Mechanics.SameMember(x, f[i])).Concat(new[] { f[i] }), order, result.Log.Add);
          TrainerTriggers.Fire(states[i].Trainer, TriggerStructures.AllyOrderLabel, order, result.Log.Add);
        }
      }

      foreach (var x in f)
        if (x.OnField && x.HP > 0)
          x.Conditions.FieldTurn++;
      for (int k = 0; k < 2; k++)
      {
        var nc = f[k].Conditions;
        if (nc.NextPriority != 0)
        {
          nc.PriorityModifier += nc.NextPriority;
          nc.PriorityModifierTurns = 1;
          nc.NextPriority = 0;
        }

        f[k].Conditions.ActionOrder = 0;
        f[k].Conditions.TakenPhysical = f[k].Conditions.TakenSpecial = 0;
        f[k].Conditions.Selected = commands[k].IsSwitch || commands[k].Move == null ? "" : commands[k].Move.category ?? "";
        f[k].Conditions.PendingAction = !commands[k].IsSwitch && commands[k].Move != null;
      }

      RuntimeTriggers.Timing(f, states, TriggerStructures.TurnStartLabel, result.Log.Add);
      AnsweredTriggers.Selected(f, commands, result.Log.Add);
      int entryStart = result.Log.Count;
      for (int i = 0; i < 2; i++)
        if (commands[i].IsSwitch && !deniedSwitch[i] && !pursuit.Deferred(i) && (commands[i].Form ?? "").Contains("돌아와"))
          PursuitReturn(f, commands, r, rng, result, returnAway, returnTransfers, i);
      AbilityRuntime.RefreshSuppression(f, result.Log.Add);
      for (int i = 0; i < 2; i++)
        if (commands[i].IsSwitch && !deniedSwitch[i] && !pursuit.Deferred(i) && !((commands[i].Form ?? "").Contains("돌아와")))
          SwitchNow(db, f, states, r, rng, result, i, commands[i].SwitchIn, SwitchReason.Normal, SwitchTiming.MidTurn, "통상 교대");
      AbilityRuntime.RefreshSuppression(f, result.Log.Add);
      EnterAbilities(f, r, rng, i => result.Switched[i] && !((commands[i].Form ?? "").Contains("돌아와")), result.Log);
      ResolveTriggeredSwitches(db, f, commands, states, r, rng, result, entryStart, SwitchTiming.MidTurn, chooseSwitch);
      StateWatch.Check(f, r, result.Log.Add);
      BattleRecords.UpdateHere(f, r, states, result.Log.Add);
      var movers = Enumerable.Range(0, 2).Where(i => !commands[i].IsSwitch && commands[i].Move != null).ToArray();
      PriorityEffectRuntime.PrimeTurn(f, commands, states, result.Log.Add);
      if (movers.Length == 2)
      {
        int tier0 = PotentialModifiers.FirstTier(f[0], f[1], r), tier1 = PotentialModifiers.FirstTier(f[1], f[0], r);
        f[0].Conditions.ActFirst = f[1].Conditions.ActFirst = 0;
        if (tier0 != tier1 || tier0 > 0)
        {
          int lead = tier0 != tier1 ? (tier0 > tier1 ? 0 : 1) : Effective(f[0], f[1], 5, r, 0, 0) != Effective(f[1], f[0], 5, r, 0, 1) ? (Effective(f[0], f[1], 5, r, 0, 0) > Effective(f[1], f[0], 5, r, 0, 1) ? 0 : 1) : rng() < .5 ? 0 : 1;
          result.Log.Add(f[lead].Data.name + ": 포텐셜로 " + (Math.Max(tier0, tier1) == 2 ? "가장 먼저" : "상대보다 먼저") + " 행동한다" + (tier0 == tier1 ? " (같은 등급: 속도 순)" : ""));
          movers = new[]
          {
            lead,
            1 - lead
          };
        }
        else
        {
          double priority0 = Mechanics.Priority(f[0], Mechanics.ActionMove(db, f[0], commands[0].Move), f[1], states[0].Trainer, states[1].Trainer) - (MiscModules.Lags(f[0], f[1], r, Mechanics.ActionMove(db, f[0], commands[0].Move)) ? .5 : 0), priority1 = Mechanics.Priority(f[1], Mechanics.ActionMove(db, f[1], commands[1].Move), f[0], states[1].Trainer, states[0].Trainer) - (MiscModules.Lags(f[1], f[0], r, Mechanics.ActionMove(db, f[1], commands[1].Move)) ? .5 : 0);
          int delta = priority0.CompareTo(priority1);
          if (delta == 0)
          {
            delta = Effective(f[0], f[1], 5, r, 0, 0).CompareTo(Effective(f[1], f[0], 5, r, 0, 1));
            if (r.TrickRoom > 0)
              delta = -delta;
          }

          int first = delta > 0 ? 0 : delta < 0 ? 1 : rng() < .5 ? 0 : 1;
          movers = new[]
          {
            first,
            1 - first
          };
        }
      }

      var originalCommands = commands.ToArray();
      var slots = pursuit.Order(ExtraActionRuntime.Build(movers, f, r));
      Fighter usedBy = null;
      Move usedMove = null;
      Action flushUsed = () =>
      {
        if (usedBy == null)
          return;
        var lastUser = usedBy;
        var lastMove = usedMove;
        usedBy = null;
        usedMove = null;
        RuntimeTriggers.MoveUsed(lastUser, lastMove, result.Log.Add);
      };
      Action watch = () => StateWatch.Check(f, r, result.Log.Add);
      int actionCounter = 0;
      Fighter lastActor = null;
      Action flushActed = () =>
      {
        if (lastActor == null)
          return;
        var actor = lastActor;
        lastActor = null;
        var waiting = ReferenceEquals(f[0], actor) ? f[1] : ReferenceEquals(f[1], actor) ? f[0] : null;
        if (waiting != null && waiting.HP > 0 && waiting.Conditions.PendingAction && waiting.Conditions.ActionOrder == 0)
          TriggerModules.Raise(new TriggerFact { Kind = "먼저 행동함" }, actor, waiting, actor.Data.name + "이 먼저 행동했다", result.Log.Add);
      };
      for (int qi = 0;; qi++)
      {
        pursuit.Complete(f, side => SwitchNow(db, f, states, r, rng, result, side, commands[side].SwitchIn, SwitchReason.Normal, SwitchTiming.MidTurn, "통상 교대"), side => PursuitReturn(f, commands, r, rng, result, returnAway, returnTransfers, side), result.Log);
        flushUsed();
        flushActed();
        watch();
        ExtraActionRuntime.Pull(slots, qi, result.Log);
        if (qi >= slots.Count)
          break;
        var slot = slots[qi];
        int i = slot.Side;
        pursuit.BeginSlot(i, slot.Extra != null, f, result.Log);
        commands[i] = originalCommands[i];
        if (slot.Extra != null)
        {
          if (!ExtraActionRuntime.Prepare(db, slot, f, originalCommands[i], r, result.Log))
            continue;
          commands[i] = slot.Command;
        }
        else if (f[i] == null || f[i].HP <= 0 || !ReferenceEquals(f[i], chosenActors[i]))
          continue;
        AbilityRuntime.RefreshSuppression(f, result.Log.Add);
        var a = f[i];
        var b = f[1 - i];
        EffectScope.Current.ExtraPowerOwner = a;
        EffectScope.Current.ExtraPowerSentence = slot.Extra != null && slot.Extra.PowerRate != 1 ? "자신의 기술의 위력을 약화(" + slot.Extra.PowerRate.ToString(System.Globalization.CultureInfo.InvariantCulture) + "배)한다." : "";
        Mechanics.RefreshRollout(db, a, result.Log);
        Mechanics.RefreshEncore(db, a, result.Log);
        var m = Mechanics.ActionMove(db, a, commands[i].Move);
        if (commands[i].Extension && m != null && ReferenceEquals(m, commands[i].Move))
        {
          m = m.Copy();
          m.extension = true;
        }

        m = AbilityMoveModules.Replace(db, a, b, m, r, rng, result.Log);
        int actionStart = result.Log.Count;
        if (a.HP <= 0)
          continue;
        actionCounter++;
        if (a.Conditions.ActionOrder == 0)
          a.Conditions.ActionOrder = actionCounter;
        a.Conditions.PendingAction = false;
        a.Conditions.DestinyBond = false;
        a.Conditions.Acting = true;
        a.Conditions.ActingMove = m;
        lastActor = a;
        if (a.Conditions.HeldInSky)
        {
          result.Log.Add(a.Data.name + ": 「하늘 위」에 붙잡혀 기술을 내보낼 수 없다!");
          continue;
        }

        if (!Mechanics.BeforeMove(a, b, m, r, rng, result.Log))
        {
          a.Conditions.Acting = false;
          MoveModules.Release(a, b);
          a.Conditions.RepeatMove = "";
          Mechanics.ClearRollout(a, result.Log);
          Mechanics.FinishRampage(a, rng, result.Log);
          MoveMiscModules.ActionFailed(a, result.Log);
          ResolveTriggeredSwitches(db, f, commands, states, r, rng, result, actionStart, SwitchTiming.MidTurn, chooseSwitch);
          continue;
        }

        Mechanics.BeginRollout(a, m);
        Mechanics.BeginRampage(a, m, result.Log);
        MoveMiscModules.BeginUproar(a, b, m, rng, result.Log);
        if (a.Conditions.Forgotten > 0)
          result.Log.Add(a.Data.name + ": 망각으로 발버둥을 내보낸다.");
        RuntimeTriggers.BeforeMove(a, m, result.Log.Add);
        if (ItemRuntime.Choice(a) && !m.extension && a.Conditions.ChoiceMove.Length == 0)
          a.Conditions.ChoiceMove = m.name;
        if (m.extension && ItemRuntime.Name(a) == "기술머신(" + m.name + ")")
          ItemRuntime.Lose(a, "소비", result.Log.Add);
        usedBy = a;
        usedMove = m;
        FieldEffectRuntime.BeforeMove(r, f, m, a.Data.name + "이 " + m.name + "을 내보냄", rng, result.Log.Add);
        if (MoveModules.Charge(a, b, m, r, result.Log))
        {
          usedBy = null;
          usedMove = null;
          Mechanics.RecordMove(a, m);
          ResolveTriggeredSwitches(db, f, commands, states, r, rng, result, actionStart, SwitchTiming.MidTurn, chooseSwitch);
          continue;
        }

        if (MoveMiscModules.Bide(a, b, m, result.Log))
        {
          usedBy = null;
          usedMove = null;
          Mechanics.RecordMove(a, m);
          ResolveTriggeredSwitches(db, f, commands, states, r, rng, result, actionStart, SwitchTiming.MidTurn, chooseSwitch);
          continue;
        }

        MoveModules.Record(a, m);
        a.Conditions.PiercingAbility = MoveModules.PiercesAbility(m);
        m = Mechanics.JudgedMove(a, m, b, r);
        usedMove = m;
        AbilityPhrases.ChangeUserType(a, b, m, r, result.Log);
        IceWallRules.BreakByFire(m, r, 1 - i, result.Log);
        var used = a.Conditions.Copy();
        bool pivotGrant = SwitchEffectRuntime.TriggeredPivotGrant(a, result.Log.Skip(actionStart)) || SwitchEffectRuntime.TriggeredPivotGrant(states[i].Trainer, result.Log.Skip(actionStart));
        if (!ProtectModules.SharesChain(m))
          a.Conditions.ProtectChain = 0;
        bool target = Mechanics.TargetsOpponent(m);
        string defendingForm = commands[1 - i].Form ?? "";
        if (target && !b.OnField)
        {
          result.Log.Add(a.Data.name + "의 " + m.name + " — 상대가 볼 안에 있어 실패했다.");
          Mechanics.AfterMove(a, m, used, result.Log, rng);
          ResolveTriggeredSwitches(db, f, commands, states, r, rng, result, actionStart, SwitchTiming.MidTurn, chooseSwitch);
          continue;
        }

        if (Mechanics.IsDelayedMove(m))
        {
          Mechanics.ScheduleDelayedMove(a, b, m, r, i, result.Log);
          Mechanics.RecordMove(a, m);
          Mechanics.AfterMove(a, m, used, result.Log, rng);
          ResolveTriggeredSwitches(db, f, commands, states, r, rng, result, actionStart, SwitchTiming.MidTurn, chooseSwitch);
          continue;
        }

        if (target && ((defendingForm.Contains("물러나") && m.category == "물리") || (defendingForm.Contains("피해라") && m.category == "특수")))
        {
          result.Log.Add(b.Data.name + "은 『" + defendingForm + "』로 공격을 회피했다!");
          Mechanics.AfterMove(a, m, used, result.Log, rng);
          ResolveTriggeredSwitches(db, f, commands, states, r, rng, result, actionStart, SwitchTiming.MidTurn, chooseSwitch);
          continue;
        }

        string reason = target ? Mechanics.Block(db, a, b, m, r) : "";
        if (reason.Length > 0)
        {
          result.Log.Add(a.Data.name + "의 " + m.name + " — " + reason);
          if (reason == ProtectModules.Blocked)
            ProtectModules.React(db, b, a, m, r, rng, result.Log);
          if (reason == StateTicks.Reflected)
            MoveRuntime.Status(b, a, m, r, rng, result.Log, 1 - i);
          MoveRuntime.Crash(a, b, m, r, rng, result.Log);
          Mechanics.Absorb(a, b, reason, r, result.Log);
          RuntimeTriggers.Nullified(b, a, m, reason, result.Log.Add);
          FieldEffectRuntime.Fire(r, TriggerStructures.ReceivedOrNullifiedLabel, f, b.Data.name + "이 " + m.name + "을 무효화", rng, result.Log.Add);
          AnsweredTriggers.DefensiveBlocked(b, a, m, reason, result.Log.Add);
          Mechanics.AfterMove(a, m, used, result.Log, rng);
          ResolveTriggeredSwitches(db, f, commands, states, r, rng, result, actionStart, SwitchTiming.MidTurn, chooseSwitch);
          continue;
        }

        string moveFail = MoveModules.FailReason(a, b, m);
        if (moveFail.Length == 0)
          moveFail = ProtectModules.ChainFail(a, m, rng);
        if (moveFail.Length == 0)
          moveFail = StateTicks.Powder(a, b, m, r, result.Log);
        if (moveFail.Length == 0)
          moveFail = VariablePower.Roll(a, b, m, r, rng, result.Log);
        if (moveFail.Length > 0)
        {
          result.Log.Add(a.Data.name + "의 " + m.name + " — " + moveFail);
          Mechanics.AfterMove(a, m, used, result.Log, rng);
          ResolveTriggeredSwitches(db, f, commands, states, r, rng, result, actionStart, SwitchTiming.MidTurn, chooseSwitch);
          continue;
        }

        if (target && b.Conditions.Substitute > 0 && m.category == "변화" && !Mechanics.BypassSub(a, m))
        {
          result.Log.Add("대타가 " + m.name + "을 막았다.");
          Mechanics.AfterMove(a, m, used, result.Log, rng);
          ResolveTriggeredSwitches(db, f, commands, states, r, rng, result, actionStart, SwitchTiming.MidTurn, chooseSwitch);
          continue;
        }

        if (target && !MoveModules.Reaches(a, b, m, i))
        {
          result.Log.Add(a.Data.name + "의 " + m.name + "! 그러나 " + b.Data.name + "은 「" + b.Conditions.Hidden + "」에 있어 맞지 않았다!");
          MoveRuntime.Crash(a, b, m, r, rng, result.Log);
          TriggerModules.Raise(new TriggerFact { Kind = "빗나감", Move = m, User = a }, a, b, a.Data.name + "의 " + m.name + "이 빗나갔다", result.Log.Add);
          Mechanics.AfterMove(a, m, used, result.Log, rng);
          ResolveTriggeredSwitches(db, f, commands, states, r, rng, result, actionStart, SwitchTiming.MidTurn, chooseSwitch);
          continue;
        }

        bool sureHit = (commands[i].Form ?? "").Contains("맞춰라") && !Mechanics.Ohko(m) || Mechanics.SureHit(b, m, target ? i : -1);
        if (target)
        {
          var guard = PotentialProtection.Check(db, a, b, m, r, rng, sureHit);
          if (guard != null)
          {
            if (guard.Value.Key == "반사")
            {
              result.Log.Add(b.Data.name + "이 " + a.Data.name + "의 " + m.name + "을 반사했다! (" + guard.Value.Value + ")");
              RuntimeTriggers.Nullified(b, a, m, "방호 포텐셜", result.Log.Add);
              if (m.category == "변화")
                Mechanics.StatusMove(b, a, m, r, rng, result.Log, 1 - i);
              else
              {
                var back = Calculate(db, b, a, m, r, 1 - i, false);
                int amount = back.Rolls[Math.Min(15, (int)(rng() * 16))];
                if (amount > 0 && a.HP > 0)
                {
                  int before = a.HP;
                  a.HP = Math.Max(0, a.HP - amount);
                  result.Log.Add(b.Data.name + "의 반사된 " + m.name + "! " + a.Data.name + "에게 " + (before - a.HP) + " 대미지.");
                  RuntimeTriggers.Damaged(a, before - a.HP, b.Data.name + "의 반사", result.Log.Add);
                  if (a.HP == 0)
                    result.Log.Add(a.Data.name + "은 쓰러졌다!");
                }
              }
            }
            else if (guard.Value.Key == "회피")
              result.Log.Add(b.Data.name + "은 " + a.Data.name + "의 " + m.name + "을 회피했다! (포텐셜: " + guard.Value.Value + ")");
            else
            {
              result.Log.Add(a.Data.name + "의 " + m.name + " — " + b.Data.name + "의 방호 포텐셜로 무효화됐다 (" + guard.Value.Value + ")");
              RuntimeTriggers.Nullified(b, a, m, "방호 포텐셜", result.Log.Add);
            }

            Mechanics.AfterMove(a, m, used, result.Log, rng);
            ResolveTriggeredSwitches(db, f, commands, states, r, rng, result, actionStart, SwitchTiming.MidTurn, chooseSwitch);
            continue;
          }
        }

        if (!sureHit && rng() * 100 >= Mechanics.Accuracy(a, b, m, r, target ? i : -1))
        {
          result.Log.Add(a.Data.name + "의 " + m.name + "! 그러나 빗나갔다!");
          MoveRuntime.Crash(a, b, m, r, rng, result.Log);
          a.Conditions.RepeatMove = "";
          TriggerModules.Raise(new TriggerFact { Kind = "빗나감", Move = m, User = a }, a, b, a.Data.name + "의 " + m.name + "이 빗나갔다", result.Log.Add);
          Mechanics.AfterMove(a, m, used, result.Log, rng);
          ResolveTriggeredSwitches(db, f, commands, states, r, rng, result, actionStart, SwitchTiming.MidTurn, chooseSwitch);
          continue;
        }

        if (target)
          Mechanics.MindReaderHit(b, i, result.Log);
        if (m.category == "변화")
        {
          if (target)
            RuntimeTriggers.Hit(a, b, m, 0, false, false, 1, result.Log.Add);
          if (MoveRuntime.Status(a, b, m, r, rng, result.Log, i))
            Mechanics.RecordMove(a, m);
          Mechanics.AfterMove(a, m, used, result.Log, rng);
          ResolveTriggeredSwitches(db, f, commands, states, r, rng, result, actionStart, SwitchTiming.MidTurn, chooseSwitch);
          if (f[i] == a && a.HP > 0 && (SwitchEffectRuntime.IsPivotMove(m) || pivotGrant))
            RequestSwitch(db, f, commands, states, r, rng, result, i, SwitchReason.Pivot, SwitchTiming.MidTurn, m.name, chooseSwitch);
          continue;
        }

        bool multi = Mechanics.MultiHit(m), directDefeat = false;
        int count = AbilityMoveModules.HitCount(a, b, m, r, rng, result.Log), hits = 0, totalBody = 0;
        for (int hit = 1; hit <= count && a.HP > 0 && b.HP > 0; hit++)
        {
          if (hit > 1 && Mechanics.TripleHit(m) && !sureHit && rng() * 100 >= Mechanics.Accuracy(a, b, m, r, i))
          {
            result.Log.Add(a.Data.name + "의 " + m.name + "! " + hit + "타가 빗나갔다!");
            break;
          }

          if (hit == 1)
            MoveModules.BreakScreens(a, b, m, r, i, result.Log);
          var hitMove = Mechanics.HitMove(m, hit);
          var hitRules = r.Copy();
          hitRules.Critical = Mechanics.HitCritical(a, hitMove, r, rng, b) && !StateTicks.NoCrit(b, r) && !(AbilityPhrases.NoCrit(b, a) && !Mechanics.BypassAbility(a));
          pursuit.Boost(hitRules, a, b, hitMove);
          var d = Calculate(db, a, b, hitMove, hitRules, i, false, true);
          var onceItems = ItemRuntime.OnceScalars(a, b, hitMove, d.Type, hitRules);
          if (!d.Supported)
          {
            result.Log.Add(m.name + ": 가변 위력 — 수동 판정.");
            break;
          }

          int damage = Mechanics.Ohko(m) ? b.HP : d.Rolls[Math.Min(15, (int)(rng() * 16))], bodyDamage = 0, subDealt = 0;
          bool sub = b.Conditions.Substitute > 0 && !Mechanics.BypassSub(a, m);
          if (sub)
          {
            subDealt = Math.Min(damage, b.Conditions.Substitute);
            b.Conditions.Substitute = Math.Max(0, b.Conditions.Substitute - damage);
            result.Log.Add(m.name + ": 대타에 " + damage + " 대미지. 남은 대타 HP " + b.Conditions.Substitute);
          }
          else
          {
            int hpBefore = b.HP;
            if (defendingForm.Contains("버텨라") && !PotentialProtection.PiercesTrainer(db, a, b, hitMove, r))
            {
              if (!Mechanics.Ohko(m))
                damage = (int)Math.Floor(damage / 2.0);
              if (damage >= b.HP)
                damage = Math.Max(0, b.HP - 1);
              result.Log.Add(b.Data.name + "은 『" + defendingForm + "』로 대미지를 반감하고 버텼다!");
            }
            else if ((multi || Mechanics.Ohko(m)) && damage >= b.HP && ItemRuntime.Name(b) == "기합의띠" && !b.Conditions.FocusSashUsed && b.HP == Stats(b, a.Data.level, r)[0])
            {
              damage = Math.Max(0, b.HP - 1);
              b.Conditions.FocusSashUsed = true;
              result.Log.Add(b.Data.name + "은 기합의띠를 소모해 HP 1을 남겼다!");
            }
            else if (damage >= b.HP && ItemRuntime.Holds(b, "기합의머리띠") && rng() < .1)
            {
              damage = Math.Max(0, b.HP - 1);
              result.Log.Add(b.Data.name + "은 「기합의머리띠」로 HP 1을 남겼다!");
            }
            else if (damage >= b.HP && AbilityPhrases.Endures(b, a) && !Mechanics.BypassAbility(a))
            {
              damage = Math.Max(0, b.HP - 1);
              result.Log.Add(b.Data.name + "은 「" + AbilityRuntime.Name(b) + "」으로 HP 1을 남겼다!");
              AnsweredTriggers.AbilityActivated(b, AbilityRuntime.Name(b), result.Log.Add);
            }
            else if (damage >= b.HP && b.Conditions.Enduring)
            {
              damage = Math.Max(0, b.HP - 1);
              result.Log.Add(b.Data.name + "은 「버티기」로 HP 1을 남겼다!");
            }
            else if (damage >= b.HP && AbilityRuntime.Endures(a, b, r))
            {
              damage = Math.Max(0, b.HP - 1);
              result.Log.Add(b.Data.name + "은 「" + AbilityRuntime.Name(b) + "」으로 HP 1을 남겼다!");
              AnsweredTriggers.AbilityActivated(b, AbilityRuntime.Name(b), result.Log.Add);
            }

            b.HP = Math.Max(0, b.HP - damage);
            bodyDamage = hpBefore - b.HP;
            if (bodyDamage > 0)
            {
              b.Conditions.TimesHit++;
              b.Conditions.LastDamageTaken = bodyDamage;
            }

            if (hitMove.category == "물리")
              b.Conditions.TakenPhysical += bodyDamage;
            else if (hitMove.category == "특수")
              b.Conditions.TakenSpecial += bodyDamage;
            MoveMiscModules.Store(b, bodyDamage);
            result.Log.Add(a.Data.name + "의 " + m.name + "! " + b.Data.name + "에게 " + damage + " 대미지." + ((hitRules.Critical && !AbilityRuntime.BlocksCritical(a, b)) ? " 급소!" : "") + (d.Type > 1 ? " 효과가 굉장했다!" : d.Type < 1 ? " 효과가 별로다." : ""));
            RuntimeTriggers.Damaged(b, bodyDamage, a.Data.name + "의 " + m.name, result.Log.Add);
            if (bodyDamage > 0)
              FieldEffectRuntime.Fire(r, TriggerStructures.DamagedLabel, f, b.Data.name + "이 " + bodyDamage + " 대미지를 받음", rng, result.Log.Add);
            AnsweredTriggers.HpChanged(b, a, r, hpBefore, b.HP, a.Data.name + "의 " + m.name, result.Log.Add);
            if (b.HP == 0)
            {
              result.Log.Add(b.Data.name + "은 쓰러졌다!");
              StateTicks.Fainted(a, b, m, result.Log);
              DefeatTriggers.ByMove(a, b, m, hpBefore, damage, result.Log.Add);
              FieldEffectRuntime.Fire(r, TriggerStructures.DefeatLabel, f, a.Data.name + "이 " + b.Data.name + "을 쓰러뜨림", rng, result.Log.Add);
            }
          }

          hits++;
          totalBody += bodyDamage;
          directDefeat |= bodyDamage > 0 && b.HP == 0;
          if (damage > 0)
            Mechanics.AfterHit(a, b, hitMove, r, rng, result.Log, sub, i, bodyDamage);
          RuntimeTriggers.Received(b, a, hitMove, bodyDamage, result.Log.Add);
          if (bodyDamage > 0)
            FieldEffectRuntime.Fire(r, TriggerStructures.ReceivedLabel, f, b.Data.name + "이 공격을 받음", rng, result.Log.Add);
          foreach (var holder in onceItems)
            if (!(sub && ReferenceEquals(holder, b)))
              ItemRuntime.Lose(holder, "소비", result.Log.Add);
          RuntimeTriggers.Hit(a, b, hitMove, bodyDamage, sub, hitRules.Critical && !AbilityRuntime.BlocksCritical(a, b), d.Type, result.Log.Add);
          if (damage > 0 && !sub)
            SecondaryEffects.Apply(a, b, hitMove, d.Type, r, rng, result.Log);
          if (damage > 0 && !sub)
            MoveRuntime.PerHit(a, b, hitMove, r, rng, result.Log);
          MoveModules.Drain(a, b, hitMove, r, sub ? subDealt : bodyDamage, result.Log);
          RuntimeTriggers.AttackSucceeded(a, b, hitMove, result.Log.Add);
        }

        if (multi)
          result.Log.Add(hits + "회 맞았다!");
        if (hits == 0)
        {
          Mechanics.AfterMove(a, m, used, result.Log, rng);
          ResolveTriggeredSwitches(db, f, commands, states, r, rng, result, actionStart, SwitchTiming.MidTurn, chooseSwitch);
          continue;
        }

        Mechanics.AfterDamage(a, b, m, r, totalBody, result.Log);
        if (totalBody > 0)
          MoveModules.Bind(a, b, m, r, rng, result.Log);
        MoveRuntime.AfterHits(a, b, m, r, rng, result.Log);
        MoveModules.Recoil(a, b, m, r, totalBody, result.Log);
        if (totalBody > 0 && a.HP > 0 && ItemRuntime.Holds(a, "생명의구슬"))
        {
          int lifeLoss = Math.Max(1, Mechanics.MaxHP(a, b, r) / 10);
          a.HP = Math.Max(0, a.HP - lifeLoss);
          result.Log.Add(a.Data.name + ": 「생명의구슬」 반동 -" + lifeLoss);
          StateWatch.Check(f, r, result.Log.Add);
        }

        FieldEffectRuntime.Fire(r, TriggerStructures.AttackSuccessLabel, f, a.Data.name + "의 공격 성공", rng, result.Log.Add);
        if (totalBody > 0)
          TrainerTriggers.Fire(states[1 - i].Trainer, TriggerStructures.DamagedLabel, a.Data.name + "의 " + m.name + "으로 " + totalBody + " 대미지", result.Log.Add);
        AbilityRuntime.RefreshSuppression(f, result.Log.Add);
        Mechanics.RecordMove(a, m);
        if (m.name == "발버둥")
          Mechanics.StruggleRecoil(a, b, r, result.Log);
        Mechanics.AfterMove(a, m, used, result.Log, rng, true);
        ResolveTriggeredSwitches(db, f, commands, states, r, rng, result, actionStart, SwitchTiming.MidTurn, chooseSwitch);
        if (f[i] == a && a.HP > 0 && (SwitchEffectRuntime.IsPivotMove(m) || pivotGrant))
          RequestSwitch(db, f, commands, states, r, rng, result, i, SwitchReason.Pivot, SwitchTiming.MidTurn, m.name, chooseSwitch);
      }

      flushUsed();
      flushActed();
      watch();
      for (int k = 0; k < 2; k++)
        commands[k] = originalCommands[k];
      RuntimeTriggers.Timing(f, states, TriggerStructures.TurnLastLabel, result.Log.Add);
      for (int i = 0; i < 2; i++)
        if (returnAway[i])
        {
          f[i] = commands[i].SwitchIn.Copy();
          f[i].OnField = true;
          BatonModules.Apply(returnTransfers[i], f[i], result.Log);
          result.Fighters = f;
          result.Switched[i] = true;
          result.Switches.Add(new SwitchRecord { Side = i, Timing = SwitchTiming.TurnEnd, Reason = SwitchReason.Return, Source = "『돌아와!』", Outgoing = result.SwitchedOut[i], Incoming = f[i].Copy() });
          Mechanics.SetParties(r, f);
          AbilityRuntime.RefreshSuppression(f, result.Log.Add);
          Mechanics.EnterField(db, f[i], f[1 - i], i, r, rng, result.Log);
          EntryTriggers.Enter(f[i], f[1 - i], r, rng, EntryReason.Return, result.Log.Add, result.SwitchedOut[i]);
          RuntimeTriggers.OpponentEntered(f[1 - i], f[i], r, result.Log.Add);
          FieldEffectRuntime.Fire(r, TriggerStructures.EntryLabel, f, f[i].Data.name + "이 장소에 나옴", rng, result.Log.Add);
          FieldEffectRuntime.Fire(r, TriggerStructures.OpponentEntryLabel, f, f[i].Data.name + "이 장소에 나옴", rng, result.Log.Add);
          result.Log.Add("『돌아와!』 — 턴 마지막 교대 시점에 " + f[i].Data.name + " 등장!");
        }

      AbilityRuntime.RefreshSuppression(f, result.Log.Add);
      EnterAbilities(f, r, rng, i => result.Switched[i] && (commands[i].Form ?? "").Contains("돌아와"), result.Log);
      foreach (var fighter in f)
        Mechanics.RefreshEncore(db, fighter, result.Log);
      int turnEndStart = result.Log.Count;
      Mechanics.EndTurn(f, r, result.Log, rng);
      AbilityRuntime.RefreshSuppression(f, result.Log.Add);
      StateTicks.EndTurn(f, r, result.Log, rng);
      StateWatch.Check(f, r, result.Log.Add);
      foreach (int i in Mechanics.SpeedOrder(f, r, rng))
        AbilityRuntime.EndTurn(f[i], f[1 - i], r, result.Log);
      for (int i = 0; i < 2; i++)
        if (!result.Switched[i])
        {
          if (deployed[i] >= 0)
            f[i].Multipliers[deployed[i]] /= 2;
          else if (deployed[i] == -3)
            f[i].AccuracyMultiplier /= 2;
          else if (deployed[i] == -4)
            f[i].EvasionMultiplier /= 2;
        }

      RuntimeTriggers.TurnEnd(f, r, rng, result.Log.Add);
      FieldEffectRuntime.Fire(r, TriggerStructures.TurnEndLabel, f, "턴 종료", rng, result.Log.Add);
      StateWatch.Check(f, r, result.Log.Add);
      foreach (var x in f)
      {
        x.Conditions.PrevTurnFacts = new List<TriggerFact>(x.Conditions.TurnFacts);
        x.Conditions.TurnFacts.Clear();
        x.Conditions.Acting = false;
      }

      ResolveTriggeredSwitches(db, f, commands, states, r, rng, result, turnEndStart, SwitchTiming.TurnEnd, chooseSwitch);
      int delayedStart = result.Log.Count;
      Mechanics.ResolveDelayedMoves(db, f, r, rng, result.Log, (side, attack, damage, defeated) =>
      {
        if (damage > 0)
          TrainerTriggers.Fire(states[1 - side].Trainer, TriggerStructures.DamagedLabel, attack.SourceName + "의 " + attack.Move.name + "으로 " + damage + " 대미지", result.Log.Add);
      });
      ResolveTriggeredSwitches(db, f, commands, states, r, rng, result, delayedStart, SwitchTiming.TurnEnd, chooseSwitch);
      AbilityRuntime.RefreshSuppression(f, result.Log.Add);
      RememberFainted(f, r);
      BattleRecords.UpdateHere(f, r, states, result.Log.Add);
      r.Turn++;
      return result;
    }
  }
}
