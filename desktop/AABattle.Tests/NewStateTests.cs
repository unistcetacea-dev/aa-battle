using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle {
 // 기술 문장의 새 상태(사용자 결정: 오레마스 상태 정의, 없으면 실기)
 [TestFixture] public class NewStateTests {
  static int ids;
  static Fighter F(string type = "노말") {
   return new Fighter(new Pokemon {
    id = "pk-ns" + (++ids), name = "S" + ids, level = 100, types = new[] { type }, ability = "", item = "",
    @base = new[] { 100, 100, 100, 100, 100, 100 }, iv = new[] { 31, 31, 31, 31, 31, 31 },
    moves = new[] { "몸통박치기" }, potentials = new Potential[0]
   });
  }
  static Move M(string name) { return TestData.Db.moves.First(x => x.name == name); }
  static Move Idle() { return new Move { name = "미등록", category = "변화", types = new[] { "노말" }, tags = new string[0], accuracy = 100, target = "자신" }; }
  static TurnResult Turn(Fighter a, Fighter b, Move ma, Move mb, Rules r = null) { return Engine.Resolve(TestData.Db, new[] { a, b }, new[] { ma, mb }, r ?? new Rules(), () => .5); }
  static TurnResult Next(TurnResult t, Move ma, Move mb) { return Engine.Resolve(TestData.Db, t.Fighters, new[] { ma, mb }, t.Rules, () => .5); }
  static string Log(TurnResult t) { return string.Join("\n", t.Log); }

  [Test] public void YawnSleepsAtTheEndOfTheNextTurn() {
   var t1 = Turn(F(), F(), M("하품"), Idle());
   Assert.That(t1.Fighters[1].Status, Is.EqualTo("정상"), Log(t1));
   var t2 = Next(t1, Idle(), Idle());
   Assert.That(t2.Fighters[1].Status, Is.EqualTo("잠듦"), Log(t2));
  }

  [Test] public void CursePaysHalfAndCurses() {
   var t = Turn(F(), F(), M("저주"), Idle());
   int max = Mechanics.MaxHP(t.Fighters[0], t.Fighters[1], new Rules());
   Assert.That(t.Fighters[0].HP, Is.EqualTo(max - max / 2), Log(t));
   Assert.That(Log(t), Does.Contain("저주"));
  }

  [Test] public void SafeguardAndMistProtectTheSide() {
   var guard = Turn(F(), F(), M("도깨비불"), M("신비의부적"));
   Assert.That(guard.Fighters[1].Status, Is.EqualTo("정상"), Log(guard));
   var mist = Turn(F(), F(), M("오로라빔"), M("흰안개"));
   var log = new List<string>();
   var a = mist.Fighters[0]; var b = mist.Fighters[1];
   using (EffectScope.Begin(mist.Fighters, mist.Rules, () => .5)) SecondaryEffects.Roll(a, b, "「10할:공격-1」", "검사", mist.Rules, () => .5, log, null);
   Assert.That(b.Stages[1], Is.EqualTo(0), string.Join("\n", log));
  }

  // 작아지기: 짓밟기는 2배·필중(오레마스). 꿰뚫어보기: 노말이 고스트에게 맞는다
  [Test] public void MinimizeAndForesight() {
   var small = F(); small.Conditions.Minimized = true;
   Assert.That(MoveModules.PowerFactor(F(), small, M("짓밟기"), new Rules()), Is.EqualTo(2));
   Assert.That(MoveModules.SureHit(F(), small, M("짓밟기"), new Rules()), Is.True);
   var seer = F(); seer.Conditions.Foresight = "꿰뚫어보기";
   Assert.That(Mechanics.TypeEffect(TestData.Db, seer, F("고스트"), M("몸통박치기"), new Rules()), Is.EqualTo(1));
   var t = Turn(F(), F(), M("꿰뚫어보기"), Idle());
   Assert.That(t.Fighters[0].Conditions.Foresight, Is.EqualTo("꿰뚫어보기"), Log(t));
  }

  [Test] public void MagicCoatReflectsStatusMoves() {
   var slow = F(); slow.Data.@base[5] = 50;
   var t = Turn(slow, F(), M("도깨비불"), M("매직코트"));
   Assert.That(t.Fighters[0].Status, Is.EqualTo("화상"), Log(t));
   Assert.That(t.Fighters[1].Status, Is.EqualTo("정상"));
  }

  [Test] public void StockpileAndSwallow() {
   var t = Turn(F(), F(), M("비축하기"), Idle());
   Assert.That(t.Fighters[0].Conditions.Stockpile, Is.EqualTo(1));
   Assert.That(t.Fighters[0].Stages[2], Is.EqualTo(1), Log(t));
   t.Fighters[0].HP = 10;
   var s = Next(t, M("꿀꺽"), Idle());
   Assert.That(s.Fighters[0].HP, Is.GreaterThan(10), Log(s));
   Assert.That(s.Fighters[0].Conditions.Stockpile, Is.EqualTo(0));
   Assert.That(s.Fighters[0].Stages[2], Is.EqualTo(0));
  }

  [Test] public void MagnetRiseAvoidsGroundMoves() {
   var f = F(); f.Conditions.MagnetRise = 5;
   Assert.That(Mechanics.Grounded(f, new Rules()), Is.False);
  }
 }
}
