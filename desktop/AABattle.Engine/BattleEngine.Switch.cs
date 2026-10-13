// 교대 요청·실행과 퇴장·등장 처리
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
    // 포텐셜 강제교대(확정 26): 무작위 멤버, 교대할 멤버가 없거나 강제교대되지 않으면 실패
    static void RememberFainted(Fighter[] fighters, Rules rules)
    {
      for (int side = 0; side < 2; side++)
      {
        var member = fighters[side];
        if (member == null || member.HP > 0 || !member.OnField)
          continue;
        rules.LastFaintedAbility[side] = AbilityRuntime.Name(member);
        rules.LastFaintedMember[side] = Mechanics.MemberKey(member.Data);
      }
    }

    static void ResolveForcedSwitches(Database db, Fighter[] fighters, BattleCommand[] commands, TrainerBattleState[] states, Rules rules, Func<double> rng, TurnResult result, SwitchTiming timing)
    {
      var scope = EffectScope.Current;
      if (scope == null || scope.ForcedSwitches.Count == 0)
        return;
      var pending = scope.ForcedSwitches.ToList();
      scope.ForcedSwitches.Clear();
      foreach (var request in pending)
      {
        int side = request.Key;
        if (side < 0 || side > 1 || fighters[side] == null || fighters[side].HP <= 0 || !fighters[side].OnField)
          continue;
        if (PotentialModifiers.ForcedSwitchImmune(fighters[side], fighters[1 - side], rules))
        {
          result.Log.Add(fighters[side].Data.name + "은 강제교대되지 않는다 (" + request.Value + ")");
          continue;
        }

        var candidates = SwitchCandidates(fighters[side], commands[side]);
        if (candidates.Length == 0)
        {
          result.Log.Add(request.Value + " — " + fighters[side].Data.name + ": 교대할 수 있는 멤버가 없어 강제교대 실패");
          continue;
        }

        var incoming = candidates[Math.Min(candidates.Length - 1, (int)((rng ?? (() => .5))() * candidates.Length))];
        SwitchNow(db, fighters, states, rules, rng, result, side, incoming, SwitchReason.Forced, timing, request.Value + " 강제교대");
      }
    }

    static void ResolveTriggeredSwitches(Database db, Fighter[] fighters, BattleCommand[] commands, TrainerBattleState[] states, Rules rules, Func<double> rng, TurnResult result, int logStart, SwitchTiming timing, Func<SwitchRequest, Fighter> chooseSwitch)
    {
      ResolveForcedSwitches(db, fighters, commands, states, rules, rng, result, timing);
      var lines = result.Log.Skip(Math.Max(0, logStart)).ToArray();
      for (int side = 0; side < 2; side++)
      {
        Fighter owner = fighters[side];
        if (owner == null || owner.HP <= 0 || !owner.OnField)
          continue;
        BattleCommand command = commands[side] ?? new BattleCommand();
        foreach (var potential in SwitchEffectRuntime.TriggeredSwitches(owner, lines))
        {
          string key = PotentialKeys.For(owner.Data, potential);
          bool declared = (command.PreInputs ?? new string[0]).Contains(potential.name);
          if (!declared && !states[side].Usage.CanUse(key, potential.uses))
            continue;
          string source = "『" + potential.name + "』";
          if (RequestSwitch(db, fighters, commands, states, rules, rng, result, side, SwitchReason.Potential, timing, source, chooseSwitch))
          {
            if (!declared)
              states[side].Usage.Use(key, potential.uses);
            break;
          }
        }

        if (fighters[side] != owner || fighters[side].HP <= 0 || !fighters[side].OnField)
          continue;
        foreach (var potential in SwitchEffectRuntime.TriggeredTrainerSwitches(states[side].Trainer, lines))
        {
          string key = PotentialKeys.For(states[side].Trainer, potential);
          if (!states[side].Usage.CanUse(key, potential.uses))
            continue;
          string source = "TRAINER 『" + potential.name + "』";
          if (RequestSwitch(db, fighters, commands, states, rules, rng, result, side, SwitchReason.Potential, timing, source, chooseSwitch))
          {
            states[side].Usage.Use(key, potential.uses);
            break;
          }
        }
      }
    }

    static Fighter[] SwitchCandidates(Fighter current, BattleCommand command)
    {
      var seen = new HashSet<string>();
      return (command == null ? new Fighter[0] : (command.Party ?? new Fighter[0])).Where(x => x != null && x.Data != null && x.HP > 0 && current != null && !Mechanics.SameMember(x, current) && seen.Add(Mechanics.MemberKey(x.Data))).Select(x => x.Copy()).ToArray();
    }

    static bool RequestSwitch(Database db, Fighter[] fighters, BattleCommand[] commands, TrainerBattleState[] states, Rules rules, Func<double> rng, TurnResult result, int side, SwitchReason reason, SwitchTiming timing, string source, Func<SwitchRequest, Fighter> chooseSwitch)
    {
      if (side < 0 || side > 1 || fighters[side] == null || fighters[side].HP <= 0 || !fighters[side].OnField)
        return false;
      string switchBlock;
      if (!SwitchEffectRuntime.CanSwitch(fighters[1 - side], fighters[side], reason, out switchBlock))
      {
        result.Log.Add(fighters[side].Data.name + "의 " + source + " — " + switchBlock);
        return false;
      }

      var candidates = SwitchCandidates(fighters[side], commands[side]);
      if (candidates.Length == 0)
      {
        result.Log.Add(fighters[side].Data.name + "의 " + source + " — 교대할 수 있는 아군이 없다.");
        return false;
      }

      var request = new SwitchRequest
      {
        Side = side,
        Timing = timing,
        Source = source,
        Outgoing = fighters[side].Copy(),
        Candidates = candidates
      };
      Fighter selected = chooseSwitch == null ? null : chooseSwitch(request);
      if (selected == null)
      {
        result.Log.Add(fighters[side].Data.name + "의 " + source + " — 교대를 취소했다.");
        return false;
      }

      var incoming = candidates.FirstOrDefault(x => Mechanics.SameMember(x, selected));
      if (incoming == null)
      {
        result.Log.Add(fighters[side].Data.name + "의 " + source + " — 교대 대상이 유효하지 않아 취소했다.");
        return false;
      }

      SwitchNow(db, fighters, states, rules, rng, result, side, incoming, reason, timing, source);
      return true;
    }

    static void SwitchNow(Database db, Fighter[] fighters, TrainerBattleState[] states, Rules rules, Func<double> rng, TurnResult result, int side, Fighter incoming, SwitchReason reason, SwitchTiming timing, string source)
    {
      DeferredDamage.Flush(db, fighters[side], fighters, rules, rng, result.Log, "교대 전");
      Fighter outgoing = fighters[side].Copy();
      RuntimeTriggers.Switch(fighters[side], incoming, reason, result.Log.Add);
      var transfer = BatonModules.Capture(fighters[side], fighters[1 - side], rules, reason, incoming, result.Log);
      AbilityRuntime.Leave(fighters[side], fighters[1 - side], rules, reason, result.Log);
      Mechanics.LeaveField(fighters[side], result.Log, rng);
      outgoing = fighters[side].Copy();
      fighters[side].OnField = false;
      result.SwitchedOut[side] = outgoing;
      fighters[side] = incoming.Copy();
      fighters[side].OnField = true;
      BatonModules.Apply(transfer, fighters[side], result.Log);
      result.Fighters = fighters;
      result.Switched[side] = true;
      result.Switches.Add(new SwitchRecord { Side = side, Timing = timing, Reason = reason, Source = source, Outgoing = outgoing, Incoming = fighters[side].Copy() });
      Mechanics.SetParties(rules, fighters);
      AbilityRuntime.RefreshSuppression(fighters, result.Log.Add);
      Mechanics.EnterField(db, fighters[side], fighters[1 - side], side, rules, rng, result.Log);
      EntryTriggers.Enter(fighters[side], fighters[1 - side], rules, rng, reason == SwitchReason.Normal ? EntryReason.NormalSwitch : reason == SwitchReason.Pivot ? EntryReason.Pivot : reason == SwitchReason.Forced ? EntryReason.Forced : EntryReason.Potential, result.Log.Add, outgoing);
      if (timing == SwitchTiming.TurnEnd)
        fighters[side].Conditions.FieldTurn = 0;
      RuntimeTriggers.OpponentEntered(fighters[1 - side], fighters[side], rules, result.Log.Add);
      FieldEffectRuntime.Fire(rules, TriggerStructures.EntryLabel, fighters, fighters[side].Data.name + "이 장소에 나옴", rng, result.Log.Add);
      FieldEffectRuntime.Fire(rules, TriggerStructures.OpponentEntryLabel, fighters, fighters[side].Data.name + "이 장소에 나옴", rng, result.Log.Add);
      AbilityRuntime.RefreshSuppression(fighters, result.Log.Add);
      AbilityRuntime.Enter(fighters[side], fighters[1 - side], rules, result.Log.Add);
      result.Log.Add(source + " — " + (timing == SwitchTiming.TurnEnd ? "턴 마지막" : "턴 중간") + "에 " + fighters[side].Data.name + " 등장!");
    }

    static void EnterAbilities(Fighter[] fighters, Rules rules, Func<double> rng, Func<int, bool> include, List<string> log)
    {
      var sides = Enumerable.Range(0, 2).Where(include).ToArray();
      if (sides.Length == 2)
        sides = Mechanics.SpeedOrder(fighters, rules, rng);
      foreach (int side in sides)
        AbilityRuntime.Enter(fighters[side], fighters[1 - side], rules, log.Add);
    }
  }
}
