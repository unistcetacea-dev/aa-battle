// 턴 실행 진입점과 실행 문맥 복원
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
    public static TurnResult Resolve(Database db, Fighter[] input, Move[] moves, Rules original, Func<double> rng)
    {
      var commands = moves.Select(m => new BattleCommand { Move = m }).ToArray();
      return Resolve(db, input, commands, original, new[] { new TrainerBattleState(), new TrainerBattleState() }, rng, null);
    }

    public static TurnResult Resolve(Database db, Fighter[] input, BattleCommand[] commands, Rules original, TrainerBattleState[] trainerInput, Func<double> rng)
    {
      return Resolve(db, input, commands, original, trainerInput, rng, null);
    }

    public static TurnResult Resolve(Database db, Fighter[] input, BattleCommand[] commands, Rules original, TrainerBattleState[] trainerInput, Func<double> rng, Func<SwitchRequest, Fighter> chooseSwitch)
    {
      var outer = EffectScope.Current;
      try
      {
        var turn = ResolveTurn(db, input, commands, original, trainerInput, rng, chooseSwitch);
        foreach (var state in turn.Trainers)
          if (state != null)
            state.Usage.EndTurn();
        return turn;
      }
      finally
      {
        EffectScope.Restore(outer);
      }
    }
  }
}
