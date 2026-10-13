// 체력이 줄어드는 순간의 버팀 판정(『버텨라！』·기합의띠·기합의머리띠·특성 버팀·버티기)
using System;
using System.Collections.Generic;

namespace AABattle
{
  public static partial class Engine
  {
    // 대미지가 체력을 넘을 때 버팀 효과를 적용한 최종 대미지를 돌려준다
    internal static int ReduceByEndure(Database db, Fighter a, Fighter b, Move m, Move hitMove, Rules r, Func<double> rng, string defendingForm, bool multi, int damage, List<string> log)
    {
      if ((defendingForm ?? "").Contains("버텨라") && !PotentialProtection.PiercesTrainer(db, a, b, hitMove, r))
      {
        if (!Mechanics.Ohko(m))
          damage = (int)Math.Floor(damage / 2.0);
        if (damage >= b.HP)
          damage = Math.Max(0, b.HP - 1);
        log.Add(b.Data.name + "은 『" + defendingForm + "』로 대미지를 반감하고 버텼다!");
      }
      else if ((multi || Mechanics.Ohko(m)) && damage >= b.HP && ItemRuntime.Name(b) == "기합의띠" && !b.Conditions.FocusSashUsed && b.HP == Stats(b, a.Data.level, r)[0])
      {
        damage = Math.Max(0, b.HP - 1);
        b.Conditions.FocusSashUsed = true;
        log.Add(b.Data.name + "은 기합의띠를 소모해 HP 1을 남겼다!");
      }
      else if (damage >= b.HP && ItemRuntime.Holds(b, "기합의머리띠") && rng() < .1)
      {
        damage = Math.Max(0, b.HP - 1);
        log.Add(b.Data.name + "은 「기합의머리띠」로 HP 1을 남겼다!");
      }
      else if (damage >= b.HP && AbilityPhrases.Endures(b, a) && !Mechanics.BypassAbility(a))
      {
        damage = Math.Max(0, b.HP - 1);
        log.Add(b.Data.name + "은 「" + AbilityRuntime.Name(b) + "」으로 HP 1을 남겼다!");
        AnsweredTriggers.AbilityActivated(b, AbilityRuntime.Name(b), log.Add);
      }
      else if (damage >= b.HP && b.Conditions.Enduring)
      {
        damage = Math.Max(0, b.HP - 1);
        log.Add(b.Data.name + "은 「버티기」로 HP 1을 남겼다!");
      }
      else if (damage >= b.HP && AbilityRuntime.Endures(a, b, r))
      {
        damage = Math.Max(0, b.HP - 1);
        log.Add(b.Data.name + "은 「" + AbilityRuntime.Name(b) + "」으로 HP 1을 남겼다!");
        AnsweredTriggers.AbilityActivated(b, AbilityRuntime.Name(b), log.Add);
      }

      return damage;
    }
  }
}
