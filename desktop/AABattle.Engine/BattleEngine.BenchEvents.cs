using System;
using System.Linq;

namespace AABattle
{
  public static partial class Engine
  {
    static void ResolveBenchTriggeredSwitches(Database db, Fighter[] fighters, TrainerBattleState[] states, Rules rules, Func<double> rng, TurnResult result, int side, Fighter owner, string[] lines, SwitchTiming timing)
    {
      if (fighters[side] != owner || owner == null || owner.HP <= 0)
        return;
      foreach (var bench in (rules.Parties[side] ?? new Fighter[0]).Where(x => x != null && x.HP > 0 && !x.OnField && !Mechanics.SameMember(x, owner)).ToArray())
      foreach (var potential in BenchEvents.TriggeredSwitches(bench, lines))
      {
        string key = PotentialKeys.For(bench.Data, potential);
        if (!states[side].Usage.CanUse(key, potential.uses))
          continue;
        string source = "『" + potential.name + "』";
        string block;
        if (!SwitchEffectRuntime.CanSwitch(fighters[1 - side], owner, SwitchReason.Potential, out block))
          continue;
        if (!BenchEvents.IsBenchSwitch(bench, owner, rules))
          continue;
        result.Log.Add(bench.Data.name + "의 " + source + " — 대기석에서 교대를 요청했다.");
        SwitchNow(db, fighters, states, rules, rng, result, side, bench.Copy(), SwitchReason.Potential, timing, source);
        states[side].Usage.Use(key, potential.uses);
        break;
      }
    }
  }
}
