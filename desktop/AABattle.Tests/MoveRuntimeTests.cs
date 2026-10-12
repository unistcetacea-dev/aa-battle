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

  // 방어 기술 = 「방어」 상태(사용자 결정). 연속 사용은 1, 1/3…
  [Test] public void GuardMovesBecomeProtectState() {
   var a = F(); var b = F(); int full = Mechanics.MaxHP(b, a, new Rules());
   var t1 = Turn(a, b, M("몸통박치기"), M("방어"));
   Assert.That(t1.Fighters[0].HP, Is.EqualTo(full));
   Assert.That(t1.Fighters[1].HP, Is.EqualTo(full), Log(t1));
   var t2 = Engine.Resolve(TestData.Db, t1.Fighters, new[] { M("몸통박치기"), M("방어") }, t1.Rules, () => .5);
   Assert.That(Log(t2), Does.Contain("연속 사용 실패"));
   Assert.That(t2.Fighters[1].HP, Is.LessThan(full));
  }

  // 킹실드: 공격만 막고, 접촉 기술을 막으면 상대 공격 -2. 니들가드: 접촉이면 상대에게 1/8
  [Test] public void GuardReactionsAndScope() {
   var k = Turn(F(), F(), M("몸통박치기"), M("킹실드"));
   Assert.That(k.Fighters[0].Stages[1], Is.EqualTo(-2), Log(k));
   Assert.That(ProtectModules.Blocks(TestData.Db, k.Fighters[1], M("도깨비불")), Is.False, "공격 기술만");
   var n = Turn(F(), F(), M("몸통박치기"), M("니들가드"));
   int max = Mechanics.MaxHP(n.Fighters[0], n.Fighters[1], new Rules());
   Assert.That(n.Fighters[0].HP, Is.EqualTo(max - max / 8), Log(n));
  }

  [Test] public void EndureLeavesOneHp() {
   var b = F(); b.HP = 5;
   var t = Turn(F(), b, M("몸통박치기"), M("버티기"));
   Assert.That(t.Fighters[1].HP, Is.EqualTo(1), Log(t));
  }

  // 안개제거(사용자 결정): 상대 회피 -1, 양쪽 벽·설치물과 지형·중력·룸이 사라진다
  [Test] public void DefogClearsTheWholeField() {
   var r = new Rules { Terrain = "그래스필드", Gravity = 3, TrickRoom = 3 };
   r.Sides[0].Reflect = 3; r.Sides[1].StealthRock = true; r.Sides[0].Spikes = 2; r.Sides[1].LightScreen = 2;
   var t = Turn(F(), F(), M("안개제거"), Idle(), .5, r);
   Assert.That(t.Fighters[1].EvasionStage, Is.EqualTo(-1), Log(t));
   Assert.That(t.Rules.Terrain, Is.EqualTo("없음"));
   Assert.That(t.Rules.Gravity + t.Rules.TrickRoom + t.Rules.Sides[0].Reflect + t.Rules.Sides[1].LightScreen + t.Rules.Sides[0].Spikes, Is.EqualTo(0));
   Assert.That(t.Rules.Sides[1].StealthRock, Is.False);
  }

  // 자폭류(사용자 결정): 빗나가거나 막혀도 빈사
  [Test] public void SelfDestructFaintsEvenWhenBlocked() {
   var t = Turn(F(), F(), M("자폭"), M("방어"));
   Assert.That(t.Fighters[0].HP, Is.EqualTo(0), Log(t));
   Assert.That(t.Fighters[1].HP, Is.EqualTo(Mechanics.MaxHP(t.Fighters[1], t.Fighters[0], new Rules())));
  }

  // 반동: 준 대미지 1/3 · 최대 HP 1/2(언제나) · 빗나가거나 무효화됐을 때 1/2
  [Test] public void RecoilKinds() {
   var a = F(); var b = F();
   var t = Turn(a, b, M("이판사판태클"), Idle());
   int dealt = Mechanics.MaxHP(b, a, new Rules()) - t.Fighters[1].HP; int max = Mechanics.MaxHP(a, b, new Rules());
   Assert.That(max - t.Fighters[0].HP, Is.EqualTo(dealt / 3), Log(t));
   var beam = Turn(F(), F(), M("철제광선"), Idle());
   Assert.That(beam.Fighters[0].HP, Is.EqualTo(max - max / 2), Log(beam));
   var crash = Turn(F(), F("고스트"), M("무릎차기"), Idle());
   Assert.That(crash.Fighters[0].HP, Is.EqualTo(max - max / 2), "고스트에게 무효화 → 반동\n" + Log(crash));
  }

  // 대미지 계산 문장: 방어 반감 · 상대 능력치 · 상승 관통 · 상성 지정 · 무효 관통
  static int Max(Fighter a, Fighter b, Move m) { return Engine.Calculate(TestData.Db, a, b, m, new Rules()).Max; }
  [Test] public void CalculationSentences() {
   var plain = M("자폭").Copy(); plain.effect = "";
   Assert.That(Max(F(), F(), M("자폭")), Is.GreaterThan(Max(F(), F(), plain) * 1.8));
   var strong = F(); strong.Stages[1] = 2;
   Assert.That(Max(F(), strong, M("속임수")), Is.GreaterThan(Max(F(), F(), M("속임수")) * 1.8), "상대의 공격으로 계산");
   var wall = F(); wall.Stages[2] = 2;
   Assert.That(Max(F(), wall, M("성스러운칼")), Is.EqualTo(Max(F(), F(), M("성스러운칼"))), "방어 상승 관통");
   Assert.That(Mechanics.TypeEffect(TestData.Db, F(), F("물"), M("프리즈드라이"), new Rules()), Is.EqualTo(2));
   Assert.That(Mechanics.TypeEffect(TestData.Db, F(), F("고스트"), M("성스러운노래"), new Rules()), Is.EqualTo(2));
   Assert.That(Mechanics.TypeEffect(TestData.Db, F(), F("고스트"), M("미라클펀치"), new Rules()), Is.EqualTo(1));
  }

  // 고정 대미지: 현재 HP 1/2 · 20 · 최대 HP 1/3 · 받은 물리 대미지 2배
  [Test] public void FixedDamageMoves() {
   var b = F(); b.HP = 100;
   Assert.That(Max(F(), b, M("분노의앞니")), Is.EqualTo(50));
   Assert.That(Max(F(), F(), M("소닉붐")), Is.EqualTo(20));
   var target = F(); Assert.That(Max(F(), target, M("지구던지기")), Is.EqualTo(Mechanics.MaxHP(target, F(), new Rules()) / 3));
   Assert.That(Max(F(), F("고스트"), M("소닉붐")), Is.EqualTo(0), "무효는 따른다");
   var slow = F(); slow.Data.@base[5] = 50;
   var t = Turn(slow, F(), M("카운터"), M("몸통박치기"));
   int taken = Mechanics.MaxHP(t.Fighters[0], t.Fighters[1], new Rules()) - t.Fighters[0].HP;
   Assert.That(Mechanics.MaxHP(t.Fighters[1], t.Fighters[0], new Rules()) - t.Fighters[1].HP, Is.EqualTo(taken * 2), Log(t));
  }

  // 벽 깨기 · 방호 특성 관통 · 능력 변화 초기화
  [Test] public void ScreensAbilitiesAndStageReset() {
   var r = new Rules(); r.Sides[1].Reflect = 5;
   var t = Turn(F(), F(), M("깨트리기"), Idle(), .5, r);
   Assert.That(t.Rules.Sides[1].Reflect, Is.EqualTo(0), Log(t));
   Assert.That(MoveModules.PiercesAbility(M("대지진")), Is.True);
   var boosted = F(); boosted.Stages[1] = 2;
   var s = Turn(F(), boosted, M("클리어스모그"), Idle());
   Assert.That(s.Fighters[1].Stages[1], Is.EqualTo(0), Log(s));
  }
 }
}
