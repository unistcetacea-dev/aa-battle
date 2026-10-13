// 지령과 트레이너 지정 효과
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
    static int ApplyOrder(Fighter a, Fighter b, string order, Rules r, List<string> log)
    {
      bool deploy = order.Contains("전개");
      if (order.Contains("회복"))
      {
        int max = Stats(a, b.Data.level, r)[0], amount = Math.Max(1, max / (deploy ? 2 : 4));
        int gain = Mechanics.Heal(a, b, r, amount, order, log);
        log.Add(a.Data.name + "의 HP가 " + gain + " 회복됐다." + (deploy ? " 행동은 생략된다." : ""));
        return deploy ? -2 : -1;
      }

      if (order.Contains("저격") || order.Contains("명중"))
      {
        if (deploy)
        {
          a.AccuracyMultiplier *= 2;
          log.Add(a.Data.name + ": 명중 2배(이번 턴)");
          return -3;
        }

        int before = a.AccuracyStage;
        a.AccuracyStage = Math.Min(6, a.AccuracyStage + 1);
        AnsweredTriggers.StageChanged(a, b, 6, before, a.AccuracyStage, log.Add);
        log.Add(a.Data.name + ": 명중 +1");
        return -1;
      }

      if (order.Contains("회피"))
      {
        if (deploy)
        {
          a.EvasionMultiplier *= 2;
          log.Add(a.Data.name + ": 회피 2배(이번 턴)");
          return -4;
        }

        int before = a.EvasionStage;
        a.EvasionStage = Math.Min(6, a.EvasionStage + 1);
        AnsweredTriggers.StageChanged(a, b, 7, before, a.EvasionStage, log.Add);
        log.Add(a.Data.name + ": 회피 +1");
        return -1;
      }

      string[] labels =
      {
        "공격",
        "방어",
        "특공",
        "특방",
        "민첩",
        "속도"
      };
      int[] indexes =
      {
        1,
        2,
        3,
        4,
        5,
        5
      };
      int at = Array.FindIndex(labels, x => order.Contains(x));
      if (at < 0)
        return -1;
      int stat = indexes[at];
      if (deploy)
      {
        a.Multipliers[stat] *= 2;
        log.Add(a.Data.name + ": " + Names[stat] + " 2배(이번 턴)");
        return stat;
      }

      Mechanics.ChangeStage(a, b, stat, 1, r, order, log);
      return -1;
    }

    static Fighter TriggerFighter(Fighter source, string name, string potential, string trigger)
    {
      var p = source.Data;
      return new Fighter(new Pokemon { id = "trigger-test", name = name, level = p.level, @base = (int[])p.@base.Clone(), iv = (int[])p.iv.Clone(), types = (string[])p.types.Clone(), moves = (string[])p.moves.Clone(), potentials = new[] { new Potential { name = potential, description = "공격이 오른다.", triggers = new[] { trigger } } } });
    }
  }
}
