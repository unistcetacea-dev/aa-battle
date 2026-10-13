using System;
using System.Collections.Generic;
using System.Linq;

namespace AABattle
{
  // 실프 인헨스먼츠의 새 상태. 「번개구름」: 오레마스_기술 「「방어」관통. 상대를 「번개구름」상태로 한다 (*「미래예지」의 유형)」
  public static partial class StateEffects
  {
    static Database fallbackDb;

    static void AddAbaddonStates(List<StateEffectRule> rules)
    {
      // 미래예지류 예약 공격: 사용 당시 공격자의 능력치로 다음 T의 최후에 판정한다
      rules.Add(Rule("번개구름", c =>
      {
        if (c.Source == null || c.Target == null || c.Target.HP <= 0)
          return false;
        var scope = EffectScope.Current;
        var db = scope == null ? null : scope.Db;
        if (db == null)
        {
          if (fallbackDb == null)
            fallbackDb = Database.Load();
          db = fallbackDb;
        }

        var move = db.moves.FirstOrDefault(x => x != null && x.name == "번개구름");
        if (move == null)
          return false;
        var rules2 = c.Rules ?? new Rules();
        int side = Math.Max(0, Mechanics.PartySide(c.Source, rules2));
        var log = c.Log ?? new List<string>();
        Mechanics.ScheduleDelayedMove(c.Source, c.Target, move, rules2, side, log);
        return true;
      }));
    }
  }
}
