using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle {
 // 가변 위력 · 예민해지기 · 봉인 유지 · 날따름 · 무리로덮치기
 [TestFixture] public class VariablePowerTests {
  static int ids;
  static Fighter F(string type = "노말") {
   return new Fighter(new Pokemon {
    id = "pk-vp" + (++ids), name = "V" + ids, level = 100, types = new[] { type }, ability = "", item = "",
    @base = new[] { 100, 100, 100, 100, 100, 100 }, iv = new[] { 31, 31, 31, 31, 31, 31 },
    moves = new[] { "몸통박치기", "할퀴기" }, potentials = new Potential[0]
   });
  }
  static Move M(string name) { return TestData.Db.moves.First(x => x.name == name); }
  static Move Idle() { return new Move { name = "미등록", category = "변화", types = new[] { "노말" }, tags = new string[0], accuracy = 100, target = "자신" }; }
  static double Power(Fighter a, Fighter b, string move) { return VariablePower.BasePower(a, b, M(move), new Rules()).Value; }
  static int Max(Fighter a) { return Mechanics.MaxHP(a, a, new Rules()); }

  [Test] public void HpBasedPower() {
   var low = F(); low.HP = 1;
   Assert.That(Power(low, F(), "기사회생（1P）"), Is.EqualTo(200));
   Assert.That(Power(F(), F(), "기사회생（1P）"), Is.EqualTo(20));
   var half = F(); half.HP = Max(half) / 2;
   Assert.That(Power(half, F(), "분화（1P）"), Is.InRange(74, 75));
   Assert.That(Power(F(), F(), "묵사발（1P）"), Is.EqualTo(120));
  }

  [Test] public void StageAndCountBasedPower() {
   var up = F(); up.Stages[1] = 2; up.Stages[5] = 1;
   Assert.That(Power(up, F(), "어시스트파워（1P）"), Is.EqualTo(80));
   Assert.That(Power(F(), up, "혼내기（1P）"), Is.EqualTo(120));
   var stock = F(); stock.Conditions.Stockpile = 2;
   Assert.That(Power(stock, F(), "토해내기"), Is.EqualTo(200));
   Assert.That(VariablePower.Roll(F(), F(), M("토해내기"), new Rules(), () => .5, new List<string>()), Does.Contain("실패"));
   var hit = F(); hit.Conditions.TimesHit = 2;
   var rageFist = TestData.Db.moves.First(x => x.name == "분노의주먹" && (x.effect ?? "").Contains("횟수"));  // 같은 이름 기술이 둘 있다
   Assert.That(VariablePower.BasePower(hit, F(), rageFist, new Rules()), Is.EqualTo(150));
   var cutter = F(); cutter.Conditions.RepeatMove = "연속자르기"; cutter.Conditions.RepeatCount = 5;
   Assert.That(Power(cutter, F(), "연속자르기"), Is.EqualTo(160));
  }

  [Test] public void WeatherBallAndMagnitude() {
   var ball = VariablePower.Transform(F(), F(), M("웨더볼"), new Rules { Weather = "비" });
   Assert.That(ball.types, Is.EqualTo(new[] { "물" }));
   Assert.That(ball.power, Is.EqualTo(100));
   var a = F();
   VariablePower.Roll(a, F(), M("매그니튜드"), new Rules(), () => .99, new List<string>());
   Assert.That(a.Conditions.RolledPower, Is.EqualTo(150));
  }

  // 예민해지기 = 레이저포커스(사용자 결정)
  [Test] public void LaserFocusForcesTheNextCritical() {
   var a = F(); a.Conditions.LaserFocus = 2;
   Assert.That(Mechanics.HitCritical(a, M("몸통박치기"), new Rules(), () => .99), Is.True);
  }

  // 봉인(오레마스): 고르는 창 + 교대해도 유지
  [Test] public void SealChoosesAndPersists() {
   var r = new Rules { ChooseSealMove = (t, moves) => moves.Last() };
   var self = F(); var target = F(); Mechanics.SetParties(r, new[] { self, target });
   StateEffects.Apply("상대를 「봉인」상태로 한다", self, target, r, () => .5, new List<string>(), "검사");
   Assert.That(target.Conditions.Sealed, Is.EqualTo("할퀴기"));
   Assert.That(Mechanics.MoveBlock(target, M("할퀴기")), Does.Contain("봉인"));
   var back = new Fighter(target.Data);
   Mechanics.EnterField(TestData.Db, back, self, 1, r, () => .5, new List<string>());
   Assert.That(back.Conditions.Sealed, Is.EqualTo("할퀴기"), "교대해 다시 나와도 유지");
  }

  // 날따름: 효과 없는 상태변화지만 걸린다(트리거가 될 수 있다)
  [Test] public void FollowMeIsANoEffectCondition() {
   var a = F();
   StateEffects.Apply("자신을 「날따름」상태로 한다", a, F(), new Rules(), () => .5, new List<string>(), "검사");
   Assert.That(Mechanics.HasCondition(a, "날따름"), Is.True);
  }

  // 무리로덮치기: 물리 예약 공격은 공격으로 계산
  [Test] public void PhysicalDelayedAttackUsesAttack() {
   var a = F(); a.Data.@base[1] = 200; a.Data.@base[3] = 50; var b = F();
   Mechanics.ScheduleDelayedMove(a, b, M("무리로덮치기"), new Rules(), 0, new List<string>());
   Assert.That(b.Conditions.DelayedAttacks[0].SourceSpecialAttack, Is.EqualTo(Engine.Stats(a, b.Data.level, new Rules())[1]));
  }
 }
}
