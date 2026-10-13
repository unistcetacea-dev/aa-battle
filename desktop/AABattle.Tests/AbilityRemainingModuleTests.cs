using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle {
 [TestFixture] public class AbilityRemainingModuleTests {
  static int ids;
  static Fighter F(string ability = "", string type = "노말") {
   return new Fighter(new Pokemon { id = "remaining" + ++ids, name = "검사" + ids, ability = ability, item = "", level = 100,
    types = new[] { type }, @base = new[] { 100, 100, 100, 100, 100, 100 }, iv = new[] { 31, 31, 31, 31, 31, 31 },
    moves = new[] { "몸통박치기", "전기쇼크" }, potentials = new Potential[0] });
  }
  static Move M(string name) { return TestData.Db.moves.First(m => m.name == name); }
  static Move Idle() { return new Move { name = "미등록", category = "변화", types = new[] { "노말" }, target = "자신", accuracy = 100, tags = new string[0] }; }
  static void Rule(Fighter f, string text) {
   string condition, effect; PotentialLibrary.Split(text, out condition, out effect);
   f.Data.potentials = new[] { new Potential { id = "종족 ①", name = "공용 검사", description = text, triggers = condition.Length > 0 ? new string[0] : new[] { TriggerStructures.AlwaysLabel } } };
  }
  [TestCase("자신의 「노말」 타입 기술을 「비행」 타입으로 변경한다.", "자신의 「노말」 기술의 타입을 「비행」으로 한다.")]
  [TestCase("자신의 기술을 「노말」 타입으로 변경한다.", "자신의 기술의 타입을 「노말」로 한다.")]
  [TestCase("자신이 기술을 내보낼 때,자신의 타입이 내보내는 기술과 같은 타입으로 변화한다.", "자신의 타입을 내보내는 기술의 타입으로 한다.")]
  [TestCase("날씨를 「큰가뭄」으로 고정한다.", "날씨를 「큰가뭄」으로 고정한다.")]
  [TestCase("자신의 기술의 반동 대미지를 받지 않는다.", "자신의 기술의 반동 대미지를 받지 않는다.")]
  [TestCase("헤롱헤롱이나 도발 상태가 되지 않는다.", "자신은 「헤롱헤롱/도발」 상태가 되지 않는다.")]
  [TestCase("상대의 〔특성〕을 〔미라〕로 변경한다.", "상대의 특성을 「미라」로 한다.")]
  public void RemainingPhrasesNormalize(string raw, string expected) {
   Assert.That(AbilityPhrases.Rewrite(AbilityModules.Clean(raw)), Is.EqualTo(expected));
   Assert.That(EffectModules.Classify(expected), Is.Not.Empty);
  }
  [TestCase("스카이스킨", "비행")]
  [TestCase("일렉트릭스킨", "전기")]
  [TestCase("페어리스킨", "페어리")]
  [TestCase("프리즈스킨", "얼음")]
  public void SkinChangesJudgedMoveBeforePowerAndDoesNotMutateData(string ability, string type) {
   var a = F(ability); var b = F(); var original = M("몸통박치기");
   var d = Engine.Calculate(TestData.Db, a, b, original, new Rules());
   var move = original.Copy(); move.types = new[] { type };
   var plain = Engine.Calculate(TestData.Db, F(), b, move, new Rules());
   Assert.That(d.Max, Is.GreaterThan(plain.Max), ability);
   Assert.That(original.types, Does.Contain("노말"));
   a.Conditions.Soaked = true;
   Assert.That(Engine.Calculate(TestData.Db, a, F("", "불꽃"), original, new Rules()).Type, Is.EqualTo(2), "수동 상태 타입 변경이 최종 판정 타입이다");
  }
  [Test] public void MoveTypeModuleAlsoWorksAsPotential() {
   var a = F(); Rule(a, "자신의 「노말」 기술의 타입을 「물」로 한다.");
   Assert.That(Engine.Calculate(TestData.Db, a, F("", "불꽃"), M("몸통박치기"), new Rules()).Type, Is.EqualTo(2));
  }
  [TestCase("리베로")] [TestCase("변환자재")]
  public void UserTypeChangesWhenMoveIsActuallyUsed(string ability) {
   var a = F(ability);
   var t = Engine.Resolve(TestData.Db, new[] { a, F() }, new[] { M("전기쇼크"), Idle() }, new Rules(), () => .5);
   Assert.That(t.Fighters[0].Data.types, Is.EqualTo(new[] { "전기" }));
   Assert.That(a.Data.types, Is.EqualTo(new[] { "노말" }), "엔진 복사본만 변화한다");
   Assert.That(Engine.Calculate(TestData.Db, a, F(), M("전기쇼크"), new Rules()).Stab, Is.EqualTo(1.5), "계산 미리보기에도 자속을 적용한다");
   Mechanics.LeaveField(t.Fighters[0]);
   Assert.That(t.Fighters[0].Data.types, Is.EqualTo(new[] { "노말" }));
  }
  [TestCase(false)] [TestCase(true)]
  public void SecondActionPowerRateIsIgnoredOnlyOnCriticalHits(bool critical) {
   var a = F(); var b = F(); var r = new Rules { Critical = critical };
   using (var scope = EffectScope.Begin(new[] { a, b }, r, () => .5)) {
    scope.ExtraPowerOwner = a; scope.ExtraPowerSentence = "자신의 기술의 위력을 약화(0.5배)한다.";
    Assert.That(PotentialModifiers.Power(a, b, M("전기쇼크"), r, 1), Is.EqualTo(critical ? 1 : .5));
    r.Critical = !critical;
    Assert.That(PotentialModifiers.Power(a, b, M("전기쇼크"), r, 1), Is.EqualTo(critical ? .5 : 1), "각 타격의 판정값으로 적용한다");
   }
  }
  [TestCase("무한의검", "강철")]
  [TestCase("무한의불꽃", "불꽃")]
  [TestCase("무한의새싹", "풀")]
  public void InfiniteTypeUsesExistingStabAndRatingModules(string ability, string type) {
   var d = Engine.Calculate(TestData.Db, F(ability, type), F("", type), M("몸통박치기"), new Rules());
   Assert.That(d.Stab, Is.EqualTo(2)); Assert.That(d.Type, Is.EqualTo(2));
  }
  [TestCase("강철정신", "강철", "물리", 1.5)]
  [TestCase("배터리", "노말", "특수", 1.3)]
  [TestCase("파워스폿", "노말", "물리", 1.3)]
  public void OwnSideAllAppliesOnlyToActiveOwner(string ability, string type, string category, double expected) {
   var a = F(ability); var b = F(); var move = M("몸통박치기").Copy(); move.types = new[] { type }; move.category = category;
   Assert.That(PotentialModifiers.Power(a, b, move, new Rules(), 1), Is.EqualTo(expected));
   a.OnField = false;
   Assert.That(PotentialModifiers.Power(b, a, move, new Rules(), 1), Is.EqualTo(1));
  }
  [TestCase("돌머리")] [TestCase("갑옷투구")]
  public void RecoilImmunityUsesModuleAndDoesNotBlockIntercept(string ability) {
   var a = F(ability); var b = F(); int hp = a.HP; var move = M("몸통박치기").Copy(); move.effect = "준 대미지의 1/4만큼 반동 대미지를 받는다.";
   MoveModules.Recoil(a, b, move, new Rules(), 100, new List<string>());
   Assert.That(a.HP, Is.EqualTo(hp));
   Mechanics.Hurt(a, b, new Rules(), 20, "요격 반동", new List<string>());
   Assert.That(a.HP, Is.EqualTo(hp - 20));
  }
  [TestCase("뇌광난무", "전기쇼크")]
  [TestCase("빙결난무", "냉동빔")]
  [TestCase("도검난무", "베어가르기")]
  public void RepeatedAttackUsesExistingExtraActionWithHalfPower(string ability, string moveName) {
   var a = F(ability); a.Moves = a.Data.moves = new[] { moveName };
   var hit = M(moveName).Copy(); hit.power = 10;
   var t = Engine.Resolve(TestData.Db, new[] { a, F() }, new[] { hit, Idle() }, new Rules(), () => .5);
   string log = string.Join("\n", t.Log);
   Assert.That(log.Split(new[] { "추가행동:" }, StringSplitOptions.None).Length - 1, Is.EqualTo(1), log);
   Assert.That(t.Log.Count(x => x.Contains("의 " + moveName + "!")), Is.EqualTo(2), log);
  }
  [TestCase(false)] [TestCase(true)]
  public void RepeatedAttackCriticalUsesFullSecondPowerInBattle(bool critical) {
   var a = F("뇌광난무"); a.Data.types = new[] { "전기" }; var b = F();
   var hit = M("전기쇼크").Copy(); hit.power = 10;
   var plain = a.Copy(); plain.Data.ability = ""; AbilityRuntime.Reset(plain);
   var r = new Rules { Critical = critical };
   var single = Engine.Resolve(TestData.Db, new[] { plain, b }, new[] { hit, Idle() }, r, () => .5);
   var repeated = Engine.Resolve(TestData.Db, new[] { a, b }, new[] { hit, Idle() }, r, () => .5);
   int one = b.HP - single.Fighters[1].HP, both = b.HP - repeated.Fighters[1].HP;
   Assert.That(both, critical ? Is.EqualTo(one * 2) : Is.InRange(one + 1, one * 2 - 1), string.Join("\n", repeated.Log));
  }
  [Test] public void FixedExtraMoveSurvivesFirstMissAndCanBeAnExtension() {
   var a = F(); var b = F(); var hit = M("전기쇼크").Copy(); a.Moves = new[] { "몸통박치기" };
   var grant = new ExtraActionGrant { Side = 0, Owner = a, SameMove = true, FixedMove = hit, PowerRate = .5 };
   var slot = new ActionSlot { Side = 0, Extra = grant };
   Assert.That(ExtraActionRuntime.Prepare(TestData.Db, slot, new[] { a, b }, new BattleCommand(), new Rules(), new List<string>()), Is.True);
   Assert.That(slot.Command.Move.name, Is.EqualTo("전기쇼크"));
   Assert.That(slot.Command.Extension, Is.True);
  }
  [Test] public void DancerCopiesOpponentsMoveOnceWithoutKnowingIt() {
   var a = F("무희"); var b = F(); var dance = M("용의춤");
   var t = Engine.Resolve(TestData.Db, new[] { a, b }, new[] { Idle(), dance }, new Rules(), () => .5);
   Assert.That(t.Fighters[0].Stages[1], Is.EqualTo(1), string.Join("\n", t.Log));
   Assert.That(t.Fighters[0].Stages[5], Is.EqualTo(1));
  }
  [Test] public void NamedExtraActionIsAFunctionExtension() {
   var a = F("일광욕"); a.HP = 100; var b = F(); b.HP = 1;
   a.Data.potentials = a.Data.potentials.Concat(new[] {
    new Potential { id = "종족 ①", name = "행동 관측", description = "자신의 「기술:광합성」 기술을 내보낼 때, 자신의 「특공」이 오른다." }
   }).ToArray();
   var hit = M("몸통박치기").Copy(); hit.priority = 2;
   var t = Engine.Resolve(TestData.Db, new[] { a, b }, new[] { hit, Idle() }, new Rules(), () => .5);
   Assert.That(t.Fighters[0].Stages[3], Is.EqualTo(1), string.Join("\n", t.Log));
   Assert.That(t.Log.Any(x => x.Contains("추가행동: 광합성")), Is.True);
   Assert.That(a.Moves, Does.Not.Contain("광합성"));
  }
  [TestCase("끝의대지", "큰가뭄")]
  [TestCase("시작의바다", "강한 비")]
  [TestCase("델타스트림", "난기류")]
  public void WeatherLockIsContinuousAndEndsWhenOwnerLeaves(string ability, string weather) {
   var t = Engine.Resolve(TestData.Db, new[] { F(ability), F() }, new[] { Idle(), Idle() }, new Rules(), () => .5);
   Assert.That(t.Rules.Weather, Is.EqualTo(weather));
   Assert.That(t.Rules.WeatherTurns, Is.Zero);
   Assert.That(Mechanics.SetWeather(t.Fighters[1], t.Fighters[0], "비", 5, t.Rules, new List<string>(), "검사"), Is.False);
   var next = Engine.Resolve(TestData.Db, t.Fighters, new[] { new BattleCommand { SwitchIn = F() }, new BattleCommand { Move = Idle() } }, t.Rules, t.Trainers, () => .5);
   Assert.That(next.Rules.Weather, Is.EqualTo("없음"));
  }
  [Test] public void WeatherLockReturnsAfterGasAndNewHolderTakesPriority() {
   var a = F("끝의대지"); var b = F("화학변화가스");
   var t = Engine.Resolve(TestData.Db, new[] { a, b }, new[] { Idle(), Idle() }, new Rules(), () => .5);
   Assert.That(t.Rules.Weather, Is.EqualTo("없음"));
   var next = Engine.Resolve(TestData.Db, t.Fighters, new[] { new BattleCommand { Move = Idle() }, new BattleCommand { SwitchIn = F("시작의바다") } }, t.Rules, t.Trainers, () => .5);
   Assert.That(next.Rules.Weather, Is.EqualTo("강한 비"), string.Join("\n", next.Log));
   var last = Engine.Resolve(TestData.Db, next.Fighters, new[] { new BattleCommand { Move = Idle() }, new BattleCommand { SwitchIn = F() } }, next.Rules, next.Trainers, () => .5);
   Assert.That(last.Rules.Weather, Is.EqualTo("큰가뭄"));
  }
  [Test] public void AmbiguousCriticalClauseDoesNotDisableKnownSecondClause() {
   var a = F("다이아가드"); int hp = a.HP;
   Mechanics.Hurt(a, F(), new Rules(), 30, "슬립", new List<string>());
   Assert.That(a.HP, Is.EqualTo(hp));
   Assert.That(AbilityCatalog.Find("다이아가드").support, Is.Not.EqualTo("자동"));
   Assert.That(AbilityCatalog.Find("다이아가드").blocker, Does.Contain("판단"));
  }
  [Test] public void BracketedAbilityChangeRunsAndRestoresOnDeparture() {
   var a = F(); var b = F("미라"); var hit = M("몸통박치기").Copy(); hit.power = 10;
   var t = Engine.Resolve(TestData.Db, new[] { a, b }, new[] { hit, Idle() }, new Rules(), () => .5);
   Assert.That(AbilityRuntime.Name(t.Fighters[0]), Is.EqualTo("미라"), string.Join("\n", t.Log));
   Mechanics.LeaveField(t.Fighters[0]);
   Assert.That(AbilityRuntime.Name(t.Fighters[0]), Is.Empty);
  }
 }
}
