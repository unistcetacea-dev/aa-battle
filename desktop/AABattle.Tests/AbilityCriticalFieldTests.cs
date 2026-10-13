using System;
using System.Linq;
using NUnit.Framework;

namespace AABattle {
 [TestFixture] public class AbilityCriticalFieldTests {
  static int ids;
  static Fighter F(string ability = "", params string[] types) {
   return new Fighter(new Pokemon { id = "critical-field-" + ++ids, name = "검사" + ids, ability = ability, item = "", level = 100,
    types = types.Length > 0 ? types : new[] { "노말" }, @base = new[] { 100, 100, 100, 100, 100, 100 },
    iv = new[] { 31, 31, 31, 31, 31, 31 }, moves = new[] { "몸통박치기" }, potentials = new Potential[0] });
  }
  static void Rule(Fighter f, string text) {
   f.Data.potentials = f.Data.potentials.Concat(new[] { new Potential { id = "종족 ①", name = "공용 필드", description = text, triggers = new[] { TriggerStructures.AlwaysLabel } } }).ToArray();
  }
  static Move M(string type = "노말") {
   var move = TestData.Db.moves.First(x => x.name == "몸통박치기").Copy();
   move.types = new[] { type }; move.power = 10; move.priority = 3;
   return move;
  }
  static Move Idle() {
   return new Move { name = "미등록", category = "변화", types = new[] { "노말" }, target = "자신", accuracy = 100, tags = new string[0] };
  }

  [TestCase("상대의 「C」가 「±0」이 된다.", "상대의 기술의 급소 랭크 보정을 0으로 한다.")]
  [TestCase("자신을 제외한 전원의 「방어」를 약화(0.5배)시킨다.", "자신을 제외한 전원의 「방어」를 약화(0.5배)한다.")]
  [TestCase("필드에 있는 전원의 악기술의 위력을 강화(1.5배)한다.", "필드에 있는 전원의 「악」 기술의 위력을 강화(1.5배)한다.")]
  [TestCase("필드에 있는 전원의 「페어리」「악」 기술을 강화(1.5배)한다.", "필드에 있는 전원의 「페어리/악」 기술의 위력을 강화(1.5배)한다.")]
  [TestCase("필드에 있는 전원의 타입 상성을 반전한다.", "필드에 있는 전원의 타입 상성을 반전한다.")]
  [TestCase("필드에 있는 전원의 기술의 타입상성이 「등배」가 된다.", "필드에 있는 전원의 기술의 타입 상성을 「효과 보통」으로 한다.")]
  public void CriticalAndFieldSourcePhrasesNormalize(string source, string expected) {
   Assert.That(EffectModules.Normalize(source), Is.EqualTo(expected));
   Assert.That(EffectModules.Classify(expected), Is.Not.Empty);
  }

  [TestCase("")]
  [TestCase("틀깨기")]
  [TestCase("관통 기술")]
  public void CriticalRankZeroIncludesAllBonusesAndSurvivesAbilityBypass(string bypass) {
   var a = F(bypass == "틀깨기" ? bypass : ""); var b = F("딱딱한비늘");
   a.CriticalStage = 4; a.Conditions.FocusEnergy = true; a.Conditions.Acting = true;
   a.Conditions.PiercingAbility = bypass == "관통 기술";
   Rule(a, "자신의 기술이 급소에 맞기 쉬워진다(C+3).");
   Assert.That(Mechanics.HitCritical(a, M(), new Rules(), () => .1, b), Is.False);
   Assert.That(Mechanics.HitCritical(a, M(), new Rules(), () => .05, b), Is.True, "C=0 확률은 1/16이다");
   Assert.That(a.CriticalStage, Is.EqualTo(4), "보유 랭크를 변경하지 않는다");
  }

  [Test] public void CriticalRankZeroAlsoWorksAsPotentialAndLeavesGuaranteedCritical() {
   var a = F(); var b = F(); a.CriticalStage = 4;
   Rule(b, AbilityCriticalFieldModules.CriticalRankZero + ".");
   Assert.That(Mechanics.HitCritical(a, M(), new Rules(), () => .1, b), Is.False);
   Assert.That(Mechanics.HitCritical(a, M(), new Rules { Critical = true }, () => .9, b), Is.True);
   a.Conditions.LaserFocus = 1;
   Assert.That(Mechanics.HitCritical(a, M(), new Rules(), () => .9, b), Is.True);
   a.Conditions.LaserFocus = 0;
   var guaranteed = TestData.Db.moves.First(x => x.name == "얼음숨결");
   Assert.That(Mechanics.HitCritical(a, guaranteed, new Rules(), () => .9, b), Is.True);
  }

  [Test] public void OnlyCriticalSentenceInMixedAbilitySurvivesBypassAndGasSuppressesIt() {
   var a = F("틀깨기"); var b = F("딱딱한비늘"); a.CriticalStage = 4;
   a.Conditions.Acting = true;
   Assert.That(PotentialModifiers.Damage(a, b, M(), new Rules(), 1), Is.EqualTo(1));
   Assert.That(AbilityCriticalFieldModules.HasCriticalRankZero(b, a, new Rules()), Is.True);
   a.Conditions.Acting = false;
   Assert.That(PotentialModifiers.Damage(a, b, M(), new Rules(), 1), Is.EqualTo(.75));
   var gas = F("화학변화가스");
   AbilityRuntime.RefreshSuppression(new[] { b, gas }, null);
   Assert.That(AbilityCriticalFieldModules.HasCriticalRankZero(b, gas, new Rules()), Is.False);
   Assert.That(Mechanics.HitCritical(a, M(), new Rules(), () => .1, b), Is.True);
  }

  [Test] public void CriticalRankZeroIsAppliedInResolvedBattleAgainstBreaker() {
   var a = F("틀깨기"); a.CriticalStage = 4; var b = F("딱딱한비늘");
   var turn = Engine.Resolve(TestData.Db, new[] { a, b }, new[] { M(), Idle() }, new Rules(), () => .1);
   Assert.That(string.Join("\n", turn.Log), Does.Not.Contain("급소!"));
  }

  [TestCase(false)]
  [TestCase(true)]
  public void GuaranteedCriticalStillOccursInBattleAgainstRankZero(bool explicitCritical) {
   var move = explicitCritical ? M() : TestData.Db.moves.First(x => x.name == "얼음숨결").Copy();
   move.accuracy = 100; move.power = 10; move.priority = 3;
   var turn = Engine.Resolve(TestData.Db, new[] { F("틀깨기"), F("딱딱한비늘") }, new[] { move, Idle() },
    new Rules { Critical = explicitCritical }, () => .9);
   Assert.That(string.Join("\n", turn.Log), Does.Contain("급소!"));
  }

  [TestCase("재앙의검", 2)]
  [TestCase("재앙의구슬", 4)]
  [TestCase("재앙의그릇", 3)]
  [TestCase("재앙의목간", 1)]
  public void FieldOthersDebuffsOnlyActiveOpponent(string ability, int index) {
   var a = F(ability); var b = F(); var r = new Rules();
   Assert.That(PotentialModifiers.Stat(a, b, index, r), Is.EqualTo(1));
   Assert.That(PotentialModifiers.Stat(b, a, index, r), Is.EqualTo(.5));
   a.OnField = false;
   Assert.That(PotentialModifiers.Stat(b, a, index, r), Is.EqualTo(1));
  }

  [TestCase("다크오라", "악")]
  [TestCase("페어리오라", "페어리")]
  [TestCase("터지는오라", "악")]
  [TestCase("터지는오라", "페어리")]
  public void FieldPowerBoostAppliesToBothActiveAttackers(string ability, string type) {
   var a = F(ability); var b = F(); var r = new Rules();
   Assert.That(PotentialModifiers.Power(a, b, M(type), r, 1), Is.EqualTo(1.5));
   Assert.That(PotentialModifiers.Power(b, a, M(type), r, 1), Is.EqualTo(1.5));
   Assert.That(PotentialModifiers.Power(b, a, M("물"), r, 1), Is.EqualTo(1));
   a.OnField = false;
   Assert.That(PotentialModifiers.Power(b, a, M(type), r, 1), Is.EqualTo(1));
  }

  [Test] public void FieldScalarAlsoWorksAsPotentialAndIgnoresReserveTarget() {
   var a = F(); var b = F(); var r = new Rules();
   Rule(a, "필드에 있는 전원의 「공격」을 강화(1.5배)한다.");
   Assert.That(PotentialModifiers.Stat(a, b, 1, r), Is.EqualTo(1.5));
   Assert.That(PotentialModifiers.Stat(b, a, 1, r), Is.EqualTo(1.5));
   b.OnField = false;
   Assert.That(PotentialModifiers.Stat(b, a, 1, r), Is.EqualTo(1));
  }

  [TestCase("노말", "고스트", "바위", 4)]
  [TestCase("불꽃", "풀", "벌레", .25)]
  [TestCase("땅", "비행", "강철", 1)]
  public void InverseFieldInvertsEachMatchupBeforeMultiplying(string attack, string type1, string type2, double expected) {
   var a = F("거꾸로"); var b = F("", type1, type2);
   Assert.That(Engine.Calculate(TestData.Db, a, b, M(attack), new Rules()).Type, Is.EqualTo(expected));
   AbilityRuntime.SetCurrent(a, ""); AbilityRuntime.SetCurrent(b, "거꾸로");
   Assert.That(Engine.Calculate(TestData.Db, a, b, M(attack), new Rules()).Type, Is.EqualTo(expected));
  }

  [Test] public void InverseFieldIsAppliedOnceAndCanBePotential() {
   var a = F("거꾸로"); var b = F("거꾸로", "고스트");
   Assert.That(Engine.Calculate(TestData.Db, a, b, M(), new Rules()).Type, Is.EqualTo(2));
   AbilityRuntime.SetCurrent(a, ""); AbilityRuntime.SetCurrent(b, "");
   Rule(a, TypeRules.InverseField + ".");
   Assert.That(Engine.Calculate(TestData.Db, a, b, M(), new Rules()).Type, Is.EqualTo(2));
  }

  [Test] public void NeutralFieldMakesBothSidesImmuneAndSuperEffectiveMatchupsNeutral() {
   var a = F("중립"); var b = F("", "고스트");
   Assert.That(Engine.Calculate(TestData.Db, a, b, M(), new Rules()).Type, Is.EqualTo(1));
   a.Data.types = new[] { "풀" };
   Assert.That(Engine.Calculate(TestData.Db, b, a, M("불꽃"), new Rules()).Type, Is.EqualTo(1));
  }

  // 남은 1개짜리 고유 효과 중 기존 수치 배율 모듈 문장으로 표현되는 것
  [TestCase("내진", "땅", 0.5)]
  [TestCase("헥사그램", "얼음", 0.5)]
  [TestCase("헥사그램", "비행", 1)]
  [TestCase("옥타그램", "비행", 0.67)]
  [TestCase("정령", "페어리", 2)]
  public void ReceivedTypeDamageRewritesUseScalarModule(string ability, string type, double expected) {
   var attacker = F(); var holder = F(ability);
   Assert.That(PotentialModifiers.Damage(attacker, holder, M(type), new Rules(), 1), Is.EqualTo(expected).Within(1e-9));
  }
 }
}
