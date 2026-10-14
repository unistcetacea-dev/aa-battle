using System;
using System.Linq;

namespace AABattle
{
  public static partial class Engine
  {
    static void ResolveBenchTriggeredSwitches(Database db, Fighter[] fighters, TrainerBattleState[] states, Rules rules, Func<double> rng, TurnResult result, int side, Fighter owner, int logStart, SwitchTiming timing)
    {
      if (fighters[side] != owner || owner == null || owner.HP <= 0)
        return;
      foreach (var bench in (rules.Parties[side] ?? new Fighter[0]).Where(x => x != null && x.HP > 0 && !x.OnField && !Mechanics.SameMember(x, owner)).ToArray())
      foreach (var potential in BenchEvents.SwitchPotentials(bench))
      {
        string key = PotentialKeys.For(bench.Data, potential);
        int line = BenchEvents.TriggerLine(bench, potential, result.Log, logStart, result.HandledBenchLines);
        if (line < 0)
          continue;
        if (!states[side].Usage.CanUse(key, potential.uses))
          continue;
        string source = "『" + potential.name + "』";
        string block;
        if (!SwitchEffectRuntime.CanSwitch(fighters[1 - side], owner, SwitchReason.Potential, out block))
          continue;
        if (!BenchEvents.IsBenchSwitch(bench, owner, rules))
          continue;
        // 같은 트리거로 두 번 묻거나 두 번 교대하지 않도록 이 로그 위치는 여기서 처리한 것으로 본다
        result.HandledBenchLines.Add(line);
        // 「~할 수 있다」: 선택 확인을 먼저 한다(확인 함수가 없을 때만 기존 기본값인 교대)
        if (rules != null && rules.OptionalEffectPrompt != null && !rules.OptionalEffectPrompt(bench, fighters[1 - side], potential))
        {
          result.Log.Add(bench.Data.name + "의 " + source + " — 사용하지 않았다.");
          continue;
        }

        result.Log.Add(bench.Data.name + "의 " + source + " — 대기석에서 교대를 요청했다.");
        SwitchNow(db, fighters, states, rules, rng, result, side, bench.Copy(), SwitchReason.Potential, timing, source);
        states[side].Usage.Use(key, potential.uses);
        break;
      }
    }
  }
}
