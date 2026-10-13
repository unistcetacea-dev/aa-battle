// 기존 엔진 자체 검사
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
    public static string Test(Database db)
    {
      TrainerAccessRules.Test();
      FieldEffectRuntime.Test(db);
      var a = new Fighter(db.pokemon.First(p => p.name == "선데이"));
      var b = new Fighter(db.pokemon.First(p => p.name == "잭 한마"));
      var m = db.moves.First(v => v.name == "파괴광선");
      var r = new Rules();
      b.Stages[4] = 1;
      BattleStateReport.Test(db);
      Check(Stats(a, b.Data.level, r)[0] == 598, "HP");
      Check(Effective(a, b, 1, r) == 269, "level correction");
      var d = Calculate(db, a, b, m, r);
      Check(d.Min == 290 && d.Max == 342 && d.Rolls[2] == 297, "spreadsheet damage");
      Check(Match(db, new[] { "노말" }, new[] { "고스트" }) == 0, "immunity");
      Check(Match(db, new[] { "물" }, new[] { "불꽃", "땅", "바위" }) == 8, "triple types");
      var ko = b.Copy();
      ko.HP = 1;
      var fast = new Move
      {
        name = m.name,
        category = m.category,
        types = m.types,
        power = m.power,
        accuracy = 100,
        priority = 7,
        attack = m.attack,
        defense = m.defense
      };
      var t = Resolve(db, new[] { a, ko }, new[] { fast, m }, r, () => 0);
      Check(t.Fighters[1].HP == 0 && t.Fighters[0].HP == a.HP && ko.HP == 1, "KO order and input isolation");
      var hunter = new Fighter(new Pokemon { id = "trigger-test", name = "사냥꾼", level = a.Data.level, @base = (int[])a.Data.@base.Clone(), iv = (int[])a.Data.iv.Clone(), types = (string[])a.Data.types.Clone(), moves = (string[])a.Data.moves.Clone(), potentials = new[] { new Potential { name = "승전", description = "공격이 오른다.", triggers = new[] { DefeatTriggers.Label } } } });
      ko = b.Copy();
      ko.HP = 1;
      var directKo = Resolve(db, new[] { hunter, ko }, new[] { fast, m }, new Rules(), () => 0);
      Check(directKo.Log.Any(x => x.Contains("『승전』") && x.Contains(DefeatTriggers.Label)), "direct move defeat trigger");
      var poisoned = b.Copy();
      poisoned.HP = 1;
      poisoned.Status = "독";
      var protect = db.moves.First(v => v.name == "방어");
      var indirectKo = Resolve(db, new[] { hunter, poisoned }, new[] { protect, protect }, new Rules(), () => 0);
      Check(indirectKo.Fighters[1].HP == 0 && !indirectKo.Log.Any(x => x.Contains("『승전』")), "indirect damage does not trigger defeat");
      var tap = new Move
      {
        name = "약한공격",
        category = m.category,
        types = m.types,
        power = 1,
        accuracy = 100,
        priority = 7,
        attack = m.attack,
        defense = m.defense
      };
      var receiver = TriggerFighter(b, "수신자", "반격", TriggerStructures.ReceivedLabel);
      var received = Resolve(db, new[] { a, receiver }, new[] { tap, protect }, new Rules(), () => 0);
      Check(received.Log.Any(x => x.Contains("『반격』") && x.Contains(TriggerStructures.ReceivedLabel)), "received once after move");
      var switcher = TriggerFighter(a, "교대자", "선회", TriggerStructures.SwitchLabel);
      var reserveForTest = new Fighter(db.pokemon.First(p => p.name == "원시 가이오가"));
      var switched = Resolve(db, new[] { switcher, b }, new[] { new BattleCommand { SwitchIn = reserveForTest }, new BattleCommand { Move = protect } }, new Rules(), new[] { new TrainerBattleState(), new TrainerBattleState() }, () => 0);
      Check(switched.Log.Any(x => x.Contains("『선회』") && x.Contains(TriggerStructures.SwitchLabel)), "voluntary switch trigger");
      var entryTrainer = new TrainerRecord
      {
        potentials = new List<PotentialRecord>
        {
          new PotentialRecord
          {
            slot = "고유",
            name = "대면 강화",
            trigger = TriggerStructures.OpponentEntryLabel,
            effect = "자신의 공격이 오른다."
          }
        }
      };
      var entryState = new[]
      {
        new TrainerBattleState
        {
          Trainer = entryTrainer
        },
        new TrainerBattleState()
      };
      var opponentSwitch = Resolve(db, new[] { a, b }, new[] { new BattleCommand { Move = protect }, new BattleCommand { SwitchIn = reserveForTest } }, new Rules(), entryState, () => 0);
      Check(opponentSwitch.Fighters[0].Stages[1] == 2 && opponentSwitch.Log.Any(x => x.Contains("『대면 강화』")), "trainer unique opponent entry affects allied active pokemon");
      var ender = TriggerFighter(a, "종료자", "윤회", TriggerStructures.TurnEndLabel);
      ender.Status = "독";
      var ended = Resolve(db, new[] { ender, b }, new[] { protect, protect }, new Rules(), () => 0);
      int residualAt = ended.Log.FindIndex(x => x.Contains("독: HP")), triggerAt = ended.Log.FindIndex(x => x.Contains("『윤회』"));
      Check(residualAt >= 0 && triggerAt > residualAt, "turn end trigger after residual");
      var burned = a.Copy();
      burned.Status = "동상";
      Check(Calculate(db, burned, b, m, r).Max < d.Max, "frostbite");
      var debuff = a.Copy();
      debuff.Stages[3] = -6;
      r.Critical = true;
      Check(Calculate(db, debuff, b, m, r).Max == Calculate(db, a, b, m, r).Max, "critical ranks");
      var clean = new Rules();
      var states = new[]
      {
        new TrainerBattleState(),
        new TrainerBattleState()
      };
      var dodge = Resolve(db, new[] { a, b }, new[] { new BattleCommand { Move = m }, new BattleCommand { Move = m, Form = "피해라！", Order = "숙달 공격 지령" } }, clean, states, () => 0);
      Check(dodge.Fighters[1].HP == b.HP && dodge.Fighters[1].Stages[1] == 1 && dodge.Trainers[1].UsedForms.Contains("피해라！") && dodge.Trainers[1].UsedOrders.Contains("숙달 공격 지령"), "trainer form and order");
      bool duplicate = false;
      try
      {
        Resolve(db, new[] { a, b }, new[] { new BattleCommand { Move = m }, new BattleCommand { Move = m, Form = "피해라！" } }, clean, dodge.Trainers, () => 0);
      }
      catch (Exception)
      {
        duplicate = true;
      }

      Check(duplicate, "directive once per battle");
      var endure = b.Copy();
      endure.HP = 1;
      var held = Resolve(db, new[] { a, endure }, new[] { new BattleCommand { Move = fast }, new BattleCommand { Move = m, Form = "버텨라！" } }, clean, states, () => 0);
      Check(held.Fighters[1].HP == 1, "endure trainer protection");
      var reserve = new Fighter(db.pokemon.First(p => p.name == "원시 가이오가"));
      int reserveHP = reserve.HP;
      var returned = Resolve(db, new[] { a, b }, new[] { new BattleCommand { SwitchIn = reserve, Form = "돌아와！" }, new BattleCommand { Move = fast } }, clean, states, () => 0);
      Check(returned.Switched[0] && returned.Fighters[0].Data.name == reserve.Data.name && returned.Fighters[0].HP == reserveHP && returned.SwitchedOut[0].HP == a.HP && returned.Log.Any(x => x.Contains("볼 안에 있어 실패")), "return withdraws before opposing action and enters last");
      var future = db.moves.First(v => v.name == "미래예지");
      var futureTarget = new Fighter(db.pokemon.First(p => p.name == "원시 가이오가"));
      var futureCast = Resolve(db, new[] { a.Copy(), futureTarget }, new[] { new BattleCommand { Move = future }, new BattleCommand { Move = protect, Form = "피해라！" } }, new Rules(), new[] { new TrainerBattleState(), new TrainerBattleState() }, () => 0);
      Check(futureCast.Fighters[1].Conditions.DelayedAttacks.Count == 1 && futureCast.Fighters[1].Conditions.DelayedAttacks[0].TurnsRemaining == 1 && futureCast.Fighters[1].HP == futureTarget.HP, "future sight schedules through protect");
      int futureHp = futureCast.Fighters[1].HP;
      var futureResolved = Resolve(db, futureCast.Fighters, new[] { new BattleCommand { Move = protect }, new BattleCommand { Move = protect, Form = "버텨라！" } }, futureCast.Rules, futureCast.Trainers, () => 0);
      Check(futureResolved.Fighters[1].HP < futureHp && futureResolved.Log.Any(x => x.Contains("선데이의 미래예지!")), "future sight resolves at true final");
      var magicGuardInput = futureCast.Fighters.Select(x => x.Copy()).ToArray();
      magicGuardInput[1].Data.ability = "매직가드";
      var magicGuardFuture = Resolve(db, magicGuardInput, new[] { new BattleCommand { Move = protect }, new BattleCommand { Move = protect } }, futureCast.Rules, futureCast.Trainers, () => 0);
      Check(magicGuardFuture.Fighters[1].HP < futureHp, "future sight is direct despite magic guard");
      Check(futureCast.Fighters[1].Conditions.DelayedAttacks[0].SourceSpecialAttack == Stats(a, futureTarget.Data.level, new Rules())[3], "future sight snapshots natural special attack");
      var boostedInput = futureCast.Fighters.Select(x => x.Copy()).ToArray();
      boostedInput[0].Stages[3] = 6;
      var boostedRules = futureCast.Rules.Copy();
      boostedRules.PowerMultiplier = 5;
      boostedRules.DamageMultiplier = 5;
      var boostedFuture = Resolve(db, boostedInput, new[] { new BattleCommand { Move = protect }, new BattleCommand { Move = protect } }, boostedRules, futureCast.Trainers, () => 0);
      var normalFuture = Resolve(db, futureCast.Fighters, new[] { new BattleCommand { Move = protect }, new BattleCommand { Move = protect } }, futureCast.Rules, futureCast.Trainers, () => 0);
      Check(boostedFuture.Fighters[1].HP == normalFuture.Fighters[1].HP, "future sight ignores source changes and multipliers");
      var sourceSwitch = Resolve(db, futureCast.Fighters, new[] { new BattleCommand { SwitchIn = new Fighter(db.pokemon.First(p => p.name == "테스트2")) }, new BattleCommand { Move = protect } }, futureCast.Rules, futureCast.Trainers, () => 0);
      Check(sourceSwitch.Fighters[1].HP < futureHp && sourceSwitch.Log.Any(x => x.Contains("선데이의 미래예지!")), "future sight survives source switch");
      var cancelCast = Resolve(db, new[] { a.Copy(), futureTarget.Copy() }, new[] { new BattleCommand { Move = future }, new BattleCommand { Move = protect } }, new Rules(), new[] { new TrainerBattleState(), new TrainerBattleState() }, () => 0);
      var cancelled = Resolve(db, cancelCast.Fighters, new[] { new BattleCommand { Move = protect }, new BattleCommand { SwitchIn = new Fighter(db.pokemon.First(p => p.name == "테스트2")) } }, cancelCast.Rules, cancelCast.Trainers, () => 0);
      Check(!cancelled.Log.Any(x => x.Contains("선데이의 미래예지!")) && cancelled.Fighters[1].Data.name == "테스트2", "future sight cancels on target switch");
      var missInput = futureCast.Fighters.Select(x => x.Copy()).ToArray();
      missInput[1].EvasionStage = 6;
      var missedFuture = Resolve(db, missInput, new[] { new BattleCommand { Move = protect }, new BattleCommand { Move = protect } }, futureCast.Rules, futureCast.Trainers, () => .99);
      Check(missedFuture.Fighters[1].HP == futureHp && missedFuture.Log.Any(x => x.Contains("최후 명중판정에서 빗나갔다")), "future sight accuracy is judged at final timing");
      var prep = TriggerFighter(a, "선행자", "예고", TriggerStructures.AlwaysLabel);
      prep.Data.potentials[0].activation = "선행";
      prep.Data.potentials[0].uses = "1/시";
      var prepared = Resolve(db, new[] { prep, b }, new[] { new BattleCommand { Move = fast, PreInputs = new[] { "예고" }, Party = new[] { prep } }, new BattleCommand { Move = m } }, clean, states, () => 0);
      Check(prepared.Trainers[0].Usage.Used(PotentialKeys.ForName(prep.Data, "예고")) && prepared.Log.Any(x => x.Contains("선행입력 『예고』")), "pre-input potential declaration");
      return "PASS: HP, level correction, spreadsheet 290-342 / .87=297, immunity, triple types, KO order, isolated inputs, structured battle triggers, frostbite, critical ranks, trainer directives, return/pivot/potential switches and delayed Future Sight timing.";
    }

    static void Check(bool ok, string label)
    {
      if (!ok)
        throw new Exception("FAIL: " + label);
    }
  }
}
