using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle {
 // 기술 효과 실행기: 문서 문장 → 효과 모듈(사용자 결정: 변화기술은 사용 시, 공격기술은 명중 후, 랭크 추가효과는 +자신 −상대)
 [TestFixture] public class MoveRuntimeTests {
  static int ids;
  static Fighter F(string type = "노말") {
   return new Fighter(new Pokemon {
    id = "pk-mr" + (++ids), name = "R" + ids, level = 100, types = new[] { type }, ability = "", item = "",
    @base = new[] { 100, 100, 100, 100, 100, 100 }, iv = new[] { 31, 31, 31, 31, 31, 31 },
    moves = new[] { "몸통박치기" }, potentials = new Potential[0]
   });
  }
  static Move M(string name) { return TestData.Db.moves.First(x => x.name == name); }
  static Move Idle() { return new Move { name = "미등록", category = "변화", types = new[] { "노말" }, tags = new string[0], accuracy = 100, target = "자신" }; }
  static TurnResult Turn(Fighter a, Fighter b, Move ma, Move mb, double roll = .5, Rules r = null) { return Engine.Resolve(TestData.Db, new[] { a, b }, new[] { ma, mb }, r ?? new Rules(), () => roll); }
  static string Log(TurnResult t) { return string.Join("\n", t.Log); }

  [TestCase("칼춤", "자신의 「공격」이 크게 오른다.")]
  [TestCase("도깨비불", "상대를 「화상」 상태로 한다.")]
  [TestCase("충전", "자신의 「특방」이 오른다.")]
  [TestCase("자기재생", "자신의 체력을 최대 HP의 1/2 회복한다.")]
  public void StatusMoveTextBecomesModules(string move, string line) {
   Assert.That(MoveRuntime.TextDriven(M(move)), Is.True);
   Assert.That(MoveRuntime.Plan(M(move)).Use, Does.Contain(line));
  }

  // 문서 기준: 충전은 특방+1도, 분노는 5T
  [Test] public void StatusMovesFollowTheDocument() {
   var t = Turn(F(), F(), M("충전"), Idle());
   Assert.That(t.Fighters[0].Conditions.Charged, Is.True);
   Assert.That(t.Fighters[0].Stages[4], Is.EqualTo(1), Log(t));
   var rage = F(); MoveRuntime.Status(rage, F(), M("분노"), new Rules(), () => .5, new List<string>(), 0);
   Assert.That(rage.Conditions.Rage, Is.EqualTo(5));
  }

  [Test] public void AttackSecondaryAndAfterHitLines() {
   var burn = Turn(F(), F(), M("폭포오르기"), Idle(), .05);
   Assert.That(burn.Fighters[1].Conditions.Flinch || Log(burn).Contains("풀죽음"), Is.True, Log(burn));
   var close = Turn(F(), F(), M("인파이트"), Idle());
   Assert.That(close.Fighters[0].Stages[2], Is.EqualTo(-1));
   Assert.That(close.Fighters[0].Stages[4], Is.EqualTo(-1));
   var beam = Turn(F(), F(), M("파괴광선"), Idle());
   Assert.That(beam.Fighters[0].Conditions.Recharge, Is.True, "명중 후 행동불능");
  }

  // 랭크 추가효과는 「N할」 추가효과와 같은 모듈: −는 상대, +는 자신
  [Test] public void RankSecondaryTargetsBySign() {
   var a = F(); var b = F(); var log = new List<string>();
   Assert.That(SecondaryEffects.Roll(a, b, "「10할:공격-1」", "시험", new Rules(), () => .5, log, null), Is.True, string.Join("\n", log));
   Assert.That(b.Stages[1], Is.EqualTo(-1));
   SecondaryEffects.Roll(a, b, "「10할:방어/특방+1」", "시험", new Rules(), () => .5, log, null);
   Assert.That(a.Stages[2], Is.EqualTo(1));
   Assert.That(a.Stages[4], Is.EqualTo(1));
   Assert.That(EffectModules.Normalize("자신의 기술에 「1할:공격-1」의 추가효과를 부여한다."), Is.Not.Null);
  }

  // 오로라빔 「1할:공격-1」: 확률이 맞으면 상대 공격 하락
  [Test] public void AuroraBeamLowersTargetAttack() {
   var t = Turn(F(), F(), M("오로라빔"), Idle(), .05);
   Assert.That(t.Fighters[1].Stages[1], Is.EqualTo(-1), Log(t));
  }

  // 급소 문장(예전에는 태그가 없어 적용되지 않았다)·방어 관통·날씨 필중
  [Test] public void CriticalProtectAndSureHitComeFromText() {
   Assert.That(MoveModules.CritBonus(M("리프블레이드")), Is.EqualTo(1));
   Assert.That(MoveModules.AlwaysCrit(M("수류연타")), Is.True);
   Assert.That(Mechanics.HitCritical(F(), M("수류연타"), new Rules(), () => .99), Is.True);
   Assert.That(MoveModules.PiercesProtect(M("마그마다이브")), Is.True);
   Assert.That(MoveModules.SureHit(F(), F(), M("번개"), new Rules { Weather = "비" }), Is.True);
   Assert.That(MoveModules.SureHit(F(), F(), M("번개"), new Rules()), Is.False);
   var flyer = F(); flyer.Conditions.Hidden = "하늘 위";
   Assert.That(MoveModules.PowerFactor(F(), flyer, M("번개"), new Rules()), Is.EqualTo(2), "문서: 하늘 위 상태일 때 2배");
  }
 }
}
