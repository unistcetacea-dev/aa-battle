using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle {
 // 남은 특성 모듈화 3차: 트리거 모듈(상태·사건), 상태·대미지 모듈, 사건 효과 모듈
 [TestFixture] public class AbilityRemaining3StateTests {
  static Fighter F(string ability = "", string type = "노말", int hp = 0) { return AbilityRemaining3MoveTests.F(ability, type, hp); }
  static Move M(string name) { return AbilityRemaining3MoveTests.M(name); }
  static Move Idle() { return AbilityRemaining3MoveTests.Idle(); }
  static void Rule(Fighter f, string text) { AbilityRemaining3MoveTests.Rule(f, text); }
  static BattleCommand Cmd(Move m) { return new BattleCommand { Move = m }; }
  static Move Custom(string name, string category, int power, string type, string effect = "") {
   return new Move { name = name, category = category, types = new[] { type }, power = power, accuracy = 100, target = "상대", tags = new string[0], effect = effect };
  }

  // ----- 새 트리거 모듈의 표준 문장 -----
  [TestCase("자신의 「속도」가 상대보다 높을 때", "속도 비교")]
  [TestCase("자신의 「속도」가 상대보다 낮을 때", "속도 비교")]
  [TestCase("장소에 나와 아직 기술을 내보내지 않았을 때", "첫 행동 전")]
  [TestCase("자신이 공중에 떠 있을 때", "공중")]
  [TestCase("서로 공중에 떠 있을 때", "공중")]
  [TestCase("상대가 자신과 같은 타입일 때", "같은 타입")]
  [TestCase("자신이 「최후의 1체」가 아닐 때", "최후 아님")]
  [TestCase("PT에 특성이 「마이너스/플러스」인 아군이 있을 때", "PT 특성")]
  [TestCase("자신의 소지품이 「부스트에너지」이거나 날씨가 「쾌청」일 때", "또는")]
  [TestCase("상대의 기술이 급소에 맞았을 때", "급소 받음")]
  [TestCase("상대의 「접촉」 기술이 급소에 맞았을 때", "급소 받음")]
  [TestCase("필드의 누군가가 「빈사」 상태가 되었을 때", "필드 빈사")]
  [TestCase("자신이 「나무열매」를 사용했을 때", "나무열매 사용")]
  [TestCase("서로 변화기술을 선택했을 때", "행동 선택")]
  [TestCase("「자신이 「나무열매」를 사용했을 때」가 성립한 다음 T 종료 시", "타이밍")]
  public void NewTriggerModulesAreRecognizedWithTheirStandardSentence(string clause, string module) {
   Assert.That(TriggerModules.Normalize(clause), Is.EqualTo(clause), clause);
   Assert.That(TriggerModules.Parse(clause).Module, Is.EqualTo(module), clause);
  }
  [Test] public void OrClauseNeedsBothSidesToBeStateModules() {
   Assert.That(TriggerModules.IsModule("자신이 「최후의 1체」가 아닐 때이거나 날씨가 「쾌청」일 때"), Is.False);
   Assert.That(TriggerModules.IsModule("자신이 공중에 떠 있을 때이거나 서로 공중에 떠 있을 때"), Is.False);
   Assert.That(TriggerModules.IsModule("상대가 자신과 같은 타입이거나 날씨가 「쾌청」일 때"), Is.True, "왼쪽에 「일 때」를 붙여도 상태 모듈이면 된다");
   Assert.That(TriggerModules.IsModule("자신이 「나무열매」를 사용했을 때이거나 날씨가 「쾌청」일 때"), Is.False, "사건 모듈은 안 된다");
  }

  // ----- 상태 조건 판정 -----
  [Test] public void SpeedComparisonUsesTheEffectiveSpeedAndNeverRecurses() {
   var fast = F(); fast.Data.@base = new[] { 100, 100, 100, 100, 100, 200 };
   var slow = F();
   var rules = new Rules();
   Assert.That(TriggerModules.Holds("자신의 「속도」가 상대보다 높을 때", fast, slow, rules), Is.True);
   Assert.That(TriggerModules.Holds("자신의 「속도」가 상대보다 낮을 때", fast, slow, rules), Is.False);
   Assert.That(TriggerModules.Holds("자신의 「속도」가 상대보다 낮을 때", slow, fast, rules), Is.True);
   Assert.That(TriggerModules.Holds("자신의 「속도」가 상대보다 높을 때", slow, slow.Copy(), rules), Is.False, "같으면 높지도 낮지도 않다");
   // 자신의 속도를 올리는 문장이 자기 조건을 재귀 평가하지 않는다
   var user = F("하극상"); var other = F(); other.Data.@base = new[] { 100, 100, 100, 100, 100, 110 };
   Assert.DoesNotThrow(() => Engine.Effective(user, other, 5, rules));
   Assert.That(Engine.Effective(user, other, 5, rules), Is.GreaterThan(Engine.Effective(F(), other, 5, rules)), "느리면 1.33배");
  }
  [Test] public void SpeciesGradeComparisonUsesDocumentedBaseStatBands() {
   var owner = F(); var opponent = F();
   opponent.Data.@base = new[] { 100, 100, 100, 100, 100, 109 };
   Assert.That(TriggerModules.Normalize("상대의 「속도」 종족치가 「B」 이하일 때"), Is.EqualTo("상대의 「속도」 종족치가 「B」 이하일 때"));
   Assert.That(TriggerModules.Holds("상대의 「속도」 종족치가 「B」 이하일 때", owner, opponent, new Rules()), Is.True);
   opponent.Data.@base[5] = 110;
   Assert.That(TriggerModules.Holds("상대의 「속도」 종족치가 「B」 이하일 때", owner, opponent, new Rules()), Is.False);
   Assert.That(TriggerModules.Holds("상대의 「속도」 종족치가 「A」 이상일 때", owner, opponent, new Rules()), Is.True);
  }
  [Test] public void TurnConditionsAreStateChecksRatherThanTimingEvents() {
   var owner = F(); var opponent = F(); var rules = new Rules { Turn = 4 };
   Assert.That(TriggerModules.Parse("장소에 나와 「2」T째일 때").IsEvent, Is.False);
   Assert.That(TriggerModules.Parse("「2」배수의 T일 때").IsEvent, Is.False);
   owner.Conditions.FieldTurn = 2;
   Assert.That(TriggerModules.Holds("장소에 나와 「2」T째일 때", owner, opponent, rules), Is.True);
   Assert.That(TriggerModules.Holds("「2」배수의 T일 때", owner, opponent, rules), Is.True);
   rules.Turn = 3;
   Assert.That(TriggerModules.Holds("「2」배수의 T일 때", owner, opponent, rules), Is.False);
  }
  [Test] public void SpeedComparisonModuleChangesPowerOnBothSides() {
   var fast = F("고압적"); fast.Data.@base = new[] { 100, 100, 100, 100, 100, 200 };
   var slow = F("고압적");
   var rules = new Rules();
   Assert.That(PotentialModifiers.Power(fast, slow, M("몸통박치기"), rules, 1), Is.EqualTo(1.33).Within(.001));
   Assert.That(PotentialModifiers.Power(slow, fast, M("몸통박치기"), rules, 1), Is.EqualTo(.67).Within(.001));
  }
  [Test] public void FirstActionStateEndsAfterTheFirstMove() {
   var a = F("스타트대시"); var b = F();
   var rules = new Rules();
   Assert.That(TriggerModules.Holds("장소에 나와 아직 기술을 내보내지 않았을 때", a, b, rules), Is.True);
   Assert.That(Engine.Effective(a, b, 5, rules), Is.GreaterThan(Engine.Effective(F(), b, 5, rules)));
   var t = Engine.Resolve(TestData.Db, new[] { a, b }, new[] { M("몸통박치기"), Idle() }, rules, () => .5);
   Assert.That(t.Fighters[0].Conditions.MovedSinceEntry, Is.True);
   Assert.That(TriggerModules.Holds("장소에 나와 아직 기술을 내보내지 않았을 때", t.Fighters[0], t.Fighters[1], t.Rules), Is.False);
   Mechanics.LeaveField(t.Fighters[0]);
   Assert.That(t.Fighters[0].Conditions.MovedSinceEntry, Is.False, "떠나면 다시 처음");
  }
  [TestCase("외뿔", 2.0)] [TestCase("일번창", 1.5)]
  public void FirstMovePowerModulesApplyOnlyBeforeTheFirstMove(string ability, double rate) {
   var a = F(ability); var rules = new Rules();
   Assert.That(PotentialModifiers.Power(a, F(), M("몸통박치기"), rules, 1), Is.EqualTo(rate));
   a.Conditions.MovedSinceEntry = true;
   Assert.That(PotentialModifiers.Power(a, F(), M("몸통박치기"), rules, 1), Is.EqualTo(1));
  }
  [Test] public void FloatingStatesAndAbilitiesThatNeedThem() {
   var rules = new Rules();
   var floater = F(); Rule(floater, "자신은 공중에 떠오른다."); var ground = F();
   Assert.That(TriggerModules.Holds("자신이 공중에 떠 있을 때", floater, ground, rules), Is.True);
   Assert.That(TriggerModules.Holds("자신이 공중에 떠 있을 때", ground, floater, rules), Is.False);
   Assert.That(TriggerModules.Holds("서로 공중에 떠 있을 때", floater, ground, rules), Is.False);
   var other = F(); Rule(other, "자신은 공중에 떠오른다.");
   Assert.That(TriggerModules.Holds("서로 공중에 떠 있을 때", floater, other, rules), Is.True);
   // 로켓윙: 떠 있을 때만 비행 기술 우선도 +2, 제공권: 서로 떠 있을 때만 상성 굉장
   var rocket = F("로켓윙"); var fly = Custom("비행검사", "물리", 40, "비행");
   Assert.That(AbilityPhrases.PriorityBonus(rocket, fly), Is.EqualTo(0));
   Rule(rocket, "자신은 공중에 떠오른다.");
   Assert.That(AbilityPhrases.PriorityBonus(rocket, fly), Is.EqualTo(2));
   var sky = F("제공권"); Rule(sky, "자신은 공중에 떠오른다.");
   Assert.That(TypeRules.Adjust(sky, ground, fly, rules, 1), Is.EqualTo(1));
   Assert.That(TypeRules.Adjust(sky, other, fly, rules, 1), Is.EqualTo(2));
  }
  [Test] public void SameTypeAndLastPokemonAndPartyAbilityStates() {
   var rules = new Rules();
   var fire = F("투쟁심", "불꽃"); var fire2 = F("", "불꽃"); var water = F("", "물");
   Assert.That(PotentialModifiers.Power(fire, fire2, M("몸통박치기"), rules, 1), Is.EqualTo(1.5));
   Assert.That(PotentialModifiers.Power(fire, water, M("몸통박치기"), rules, 1), Is.EqualTo(1));
   // 최후의요새: 자신만 생존이면 1.5배, 아니면 0.5배
   var mine = F("최후의요새"); var bench = F(); bench.OnField = false; var foe = F();
   Mechanics.SetParties(rules, new[] { mine, foe }, new IEnumerable<Fighter>[] { new[] { mine, bench }, new[] { foe } });
   Assert.That(Engine.Effective(mine, foe, 1, rules), Is.LessThan(Engine.Effective(F(), foe, 1, rules)), "대기 멤버가 있으면 0.5배");
   bench.HP = 0;
   Assert.That(Engine.Effective(mine, foe, 1, rules), Is.GreaterThan(Engine.Effective(F(), foe, 1, rules)), "자신만 생존이면 1.5배");
   // 마이너스·플러스: PT에 자신 외의 마이너스/플러스가 있을 때
   var plus = F("플러스"); var partner = F("마이너스"); partner.OnField = false;
   var rules2 = new Rules();
   Mechanics.SetParties(rules2, new[] { plus, foe }, new IEnumerable<Fighter>[] { new[] { plus, partner }, new[] { foe } });
   Assert.That(Engine.Effective(plus, foe, 1, rules2), Is.GreaterThan(Engine.Effective(F(), foe, 1, rules2)));
   partner.HP = 0;
   Assert.That(Engine.Effective(plus, foe, 1, rules2), Is.EqualTo(Engine.Effective(F(), foe, 1, rules2)), "생존하지 않으면 해당 없음");
  }
  [Test] public void OrStateHoldsWhenEitherSideHolds() {
   var rules = new Rules();
   var holder = F("고대활성"); var foe = F();
   var plain = Engine.Effective(F(), foe, 1, rules);
   Assert.That(Engine.Effective(holder, foe, 1, rules) == plain && Engine.Effective(holder, foe, 3, rules) == Engine.Effective(F(), foe, 3, rules), Is.True, "소지품도 날씨도 없으면 그대로");
   rules.Weather = "큰가뭄";
   int highest = Enumerable.Range(1, 5).OrderByDescending(i => Engine.Stats(holder, 100, rules)[i]).First();
   Assert.That(Engine.Effective(holder, foe, highest, rules), Is.GreaterThan(Engine.Effective(F(), foe, highest, rules)));
   rules.Weather = "없음";
   holder.Data.item = "부스트에너지";
   Assert.That(TriggerModules.Holds("자신의 소지품이 「부스트에너지」이거나 날씨가 「쾌청」일 때", holder, foe, rules), Is.True);
   var field = F("쿼크차지"); rules.Terrain = "일렉트릭필드";
   Assert.That(TriggerModules.Holds("자신의 소지품이 「부스트에너지」이거나 「일렉트릭필드」일 때", field, foe, rules), Is.True);
  }

  // ----- 사건 트리거 -----
  [Test] public void CriticalTakenTriggerRaisesAttackToTheMaximum() {
   var holder = F("분노의경혈"); var foe = F();
   var t = Engine.Resolve(TestData.Db, new[] { holder, foe }, new[] { Idle(), M("몸통박치기") }, new Rules { Critical = true }, () => .5);
   Assert.That(t.Fighters[0].Stages[1], Is.EqualTo(6), string.Join("\n", t.Log));
   var calm = F("분노의경혈");
   var t2 = Engine.Resolve(TestData.Db, new[] { calm, F() }, new[] { Idle(), M("몸통박치기") }, new Rules(), () => .5);
   Assert.That(t2.Fighters[0].Stages[1], Is.EqualTo(0), "급소가 아니면 발동하지 않는다");
  }
  [Test] public void AnyFaintTriggerRaisesSpecialAttackWhenEitherFaints() {
   var holder = F("소울하트"); var foe = F("", "노말", 1);
   var t = Engine.Resolve(TestData.Db, new[] { holder, foe }, new[] { M("몸통박치기"), Idle() }, new Rules(), () => .5);
   Assert.That(t.Fighters[1].HP, Is.EqualTo(0));
   Assert.That(t.Fighters[0].Stages[3], Is.EqualTo(1), string.Join("\n", t.Log));
  }
  [TestCase("증기기관", "불꽃", 5, true)] [TestCase("증기기관", "물", 5, true)] [TestCase("증기기관", "풀", 5, false)]
  public void SetRankModuleSetsSpeedToPlusSix(string ability, string type, int stat, bool expected) {
   var holder = F(ability); var move = Custom("속성검사", "특수", 40, type);
   var t = Engine.Resolve(TestData.Db, new[] { holder, F() }, new[] { Idle(), move }, new Rules(), () => .5);
   Assert.That(t.Fighters[0].Stages[stat], Is.EqualTo(expected ? 6 : 0), string.Join("\n", t.Log));
  }

  // ----- 상태·대미지 모듈 -----
  [Test] public void MoveLockMakesOnlyTheFirstMoveUsable() {
   var a = F("무아지경"); var b = F();
   var t = Engine.Resolve(TestData.Db, new[] { a, b }, new[] { M("몸통박치기"), Idle() }, new Rules(), () => .5);
   Assert.That(t.Fighters[0].Conditions.ChoiceMove, Is.EqualTo("몸통박치기"));
   Assert.That(ItemRuntime.Choice(t.Fighters[0]), Is.True);
   Assert.That(Mechanics.MoveBlock(t.Fighters[0], M("전기쇼크")), Is.Not.Empty);
   Assert.That(Mechanics.MoveBlock(t.Fighters[0], M("몸통박치기")), Is.Empty);
   Mechanics.LeaveField(t.Fighters[0]);
   Assert.That(Mechanics.MoveBlock(t.Fighters[0], M("전기쇼크")), Is.Empty, "떠나면 풀린다");
  }
  [TestCase(.0, 1)] [TestCase(.5, 1)] [TestCase(.99, 2)]
  public void EarlyBirdHalvesSleepRoundedUp(double roll, int expected) {
   var holder = F("일찍기상"); var source = F();
   Mechanics.ApplyStatus(holder, source, "잠듦", new Rules(), () => roll, new List<string>());
   Assert.That(holder.Conditions.SleepTurns, Is.EqualTo(expected));
   Assert.That(holder.Conditions.SleepStart, Is.EqualTo(expected));
   var plain = F();
   Mechanics.ApplyStatus(plain, source, "잠듦", new Rules(), () => roll, new List<string>());
   Assert.That(plain.Conditions.SleepTurns, Is.EqualTo(roll < .34 ? 1 : roll < .67 ? 2 : 3));
  }
  [Test] public void IndirectDamageCannotFaintAndAttackStillCan() {
   var holder = F("불로불사", "노말", 5);
   Mechanics.Hurt(holder, F(), new Rules(), 100, "독", new List<string>());
   Assert.That(holder.HP, Is.EqualTo(1));
   Mechanics.Hurt(holder, F(), new Rules(), 100, "독", new List<string>());
   Assert.That(holder.HP, Is.EqualTo(1), "더 줄지 않는다");
   var attacked = F("불로불사", "노말", 3);
   var t = Engine.Resolve(TestData.Db, new[] { F(), attacked }, new[] { M("몸통박치기"), Idle() }, new Rules(), () => .5);
   Assert.That(t.Fighters[1].HP, Is.GreaterThan(0).Or.EqualTo(0), "공격은 막지 않는다(체력이 가득이 아니면 쓰러진다)");
   Assert.That(t.Fighters[1].HP, Is.EqualTo(0), string.Join("\n", t.Log));
  }
  [TestCase(.05, true)] [TestCase(.5, false)]
  public void ChanceEndureRollsOncePerAttack(double roll, bool endures) {
   var holder = F("기합");
   Assert.That(AbilityStateModules.EndureChance(holder, F(), new Rules(), () => roll), Is.EqualTo(endures));
   Assert.That(AbilityStateModules.EndureChance(F(), F(), new Rules(), () => 0), Is.False);
  }
  [Test] public void FullHpSturdyAndChanceBothWorkInARealHit() {
   var holder = F("기합"); var strong = M("몸통박치기").Copy(); strong.power = 400;
   var t = Engine.Resolve(TestData.Db, new[] { F(), holder }, new[] { strong, Idle() }, new Rules(), () => .5);
   Assert.That(t.Fighters[1].HP, Is.EqualTo(1), "체력이 가득이면 반드시 버틴다: " + string.Join("\n", t.Log));
   var hurt = F("기합", "노말", 50);
   var t2 = Engine.Resolve(TestData.Db, new[] { F(), hurt }, new[] { strong, Idle() }, new Rules(), () => .05);
   Assert.That(t2.Fighters[1].HP, Is.EqualTo(1), "드물게 버틴다: " + string.Join("\n", t2.Log));
   var hurt2 = F("기합", "노말", 50);
   var t3 = Engine.Resolve(TestData.Db, new[] { F(), hurt2 }, new[] { strong, Idle() }, new Rules(), () => .5);
   Assert.That(t3.Fighters[1].HP, Is.EqualTo(0));
  }
  [Test] public void StatusDamageImmunityKeepsPoisonHealAsHealing() {
   var holder = F("포이즌힐"); holder.Status = "독"; holder.HP = Mechanics.MaxHP(holder, holder, new Rules()) / 2;
   int hp = holder.HP;
   Mechanics.Hurt(holder, F(), new Rules(), 50, "독", new List<string>());
   Assert.That(holder.HP, Is.EqualTo(hp), "독 대미지를 받지 않는다");
   Mechanics.Hurt(holder, F(), new Rules(), 50, "화상", new List<string>());
   Assert.That(holder.HP, Is.EqualTo(hp - 50), "다른 사유는 받는다");
   var t = Engine.Resolve(TestData.Db, new[] { holder, F() }, new[] { Idle(), Idle() }, new Rules(), () => .5);
   int max = Mechanics.MaxHP(t.Fighters[0], t.Fighters[1], t.Rules);
   Assert.That(t.Fighters[0].HP, Is.EqualTo(hp - 50 + max / 8), string.Join("\n", t.Log));
  }
  [Test] public void ReverseDrainHurtsTheDrainer() {
   var drain = Custom("흡수검사", "특수", 40, "풀", "준 대미지의 1/2만큼 HP를 회복한다.");
   var attacker = F(); attacker.HP = 100; var ooze = F("해감액");
   MoveModules.Drain(attacker, ooze, drain, new Rules(), 60, new List<string>());
   Assert.That(attacker.HP, Is.EqualTo(70));
   var normal = F(); attacker.HP = 100;
   MoveModules.Drain(attacker, normal, drain, new Rules(), 60, new List<string>());
   Assert.That(attacker.HP, Is.EqualTo(130));
  }
  [Test] public void AuraBreakInvertsFieldWidePowerBoosts() {
   var dark = M("몸통박치기").Copy(); dark.types = new[] { "악" };
   var rules = new Rules();
   Assert.That(PotentialModifiers.Power(F("다크오라"), F(), dark, rules, 1), Is.EqualTo(1.5));
   Assert.That(PotentialModifiers.Power(F("다크오라"), F("오라브레이크"), dark, rules, 1), Is.EqualTo(1 / 1.5).Within(.001));
   Assert.That(PotentialModifiers.Power(F("오라브레이크"), F("다크오라"), dark, rules, 1), Is.EqualTo(1 / 1.5).Within(.001));
   Assert.That(PotentialModifiers.Power(F("오라브레이크"), F(), dark, rules, 1), Is.EqualTo(1), "다른 효과는 반전하지 않는다");
  }
  [Test] public void AnyMajorStatusImmunityDoesNotBlockConditionsOrOtherSources() {
   var salt = F("정화의소금"); var rules = new Rules(); var foe = F();
   foreach (string status in new[] { "독", "맹독", "마비", "화상", "얼음", "잠듦", "동상" })
    Assert.That(Mechanics.StatusBlock(salt, foe, status, rules), Is.Not.Empty, status);
   var leaf = F("리프가드");
   Assert.That(Mechanics.StatusBlock(leaf, foe, "독", rules), Is.Empty, "날씨가 없으면 막지 않는다");
   rules.Weather = "큰가뭄";
   Assert.That(Mechanics.StatusBlock(leaf, foe, "독", rules), Is.Not.Empty);
  }
  [Test] public void AlwaysSwitchableIgnoresSwitchBlocks() {
   var runner = F("도주"); runner.Conditions.MeanLook = true;
   string block;
   Assert.That(SwitchEffectRuntime.CanSwitch(null, runner, SwitchReason.Normal, out block), Is.True);
   var trapped = F(); trapped.Conditions.MeanLook = true;
   Assert.That(SwitchEffectRuntime.CanSwitch(null, trapped, SwitchReason.Normal, out block), Is.False);
   var shed = F("탈피"); shed.Conditions.Bound = 3;
   Assert.That(SwitchEffectRuntime.CanSwitch(null, shed, SwitchReason.Normal, out block), Is.True);
  }
  [TestCase("그림자밟기", "노말", false, false)]
  [TestCase("그림자밟기", "비행", false, false)]
  [TestCase("개미지옥", "노말", false, false)]
  [TestCase("개미지옥", "노말", true, true)]
  [TestCase("자력", "강철", false, false)]
  [TestCase("자력", "노말", false, true)]
  public void SwitchTrapModulesBlockNormalSwitchingOfTheirTargets(string ability, string userType, bool floating, bool allowed) {
   var trap = F(ability); var user = F("", userType);
   if (floating) Rule(user, "자신은 공중에 떠오른다.");
   string block;
   Assert.That(SwitchEffectRuntime.CanSwitch(trap, user, SwitchReason.Normal, out block), Is.EqualTo(allowed), block);
   Assert.That(SwitchEffectRuntime.CanSwitch(trap, user, SwitchReason.Return, out block), Is.EqualTo(allowed), "『돌아와！』도 통상교대다");
   Assert.That(SwitchEffectRuntime.CanSwitch(trap, user, SwitchReason.Forced, out block), Is.True, "강제교대는 막지 않는다");
   var runner = F("도주", userType);
   Assert.That(SwitchEffectRuntime.CanSwitch(trap, runner, SwitchReason.Normal, out block), Is.True, "도주는 반드시 교대할 수 있다");
  }
  [Test] public void SteelTrapAlsoGrantsSureHitAgainstSteel() {
   var magnet = F("자력"); var shock = Custom("전기검사", "특수", 40, "전기"); shock.accuracy = 70;
   Assert.That(double.IsPositiveInfinity(Mechanics.Accuracy(magnet, F("", "강철"), shock, new Rules())), Is.True);
   Assert.That(double.IsPositiveInfinity(Mechanics.Accuracy(magnet, F(), shock, new Rules())), Is.False);
  }
  [Test] public void SameTypeAvoidanceNeedsTheTargetsOwnType() {
   var holder = F("만트라", "불꽃"); var fire = Custom("불꽃검사", "특수", 40, "불꽃"); var water = Custom("물검사", "특수", 40, "물");
   Assert.That(PotentialProtection.Check(TestData.Db, F(), holder, fire, new Rules(), () => .5, false), Is.Not.Null);
   Assert.That(PotentialProtection.Check(TestData.Db, F(), holder, water, new Rules(), () => .5, false), Is.Null);
   Assert.That(PotentialProtection.Check(TestData.Db, F(), holder, fire, new Rules(), () => .5, true), Is.Null, "필중 기술은 회피하지 못한다");
   Assert.That(PotentialProtection.Check(TestData.Db, F(), holder, Custom("변화검사", "변화", 0, "불꽃"), new Rules(), () => .5, false), Is.Null, "공격기술만");
  }
  [Test] public void OnceGuardBlocksOnceAndRestoresOnWeather() {
   var holder = F("아이스페이스"); var foe = F();
   var rules = new Rules();
   var physical = M("몸통박치기");
   var first = PotentialProtection.Check(TestData.Db, foe, holder, physical, rules, () => .5, false);
   Assert.That(first, Is.Not.Null);
   Assert.That(PotentialProtection.Check(TestData.Db, foe, holder, physical, rules, () => .5, false), Is.Null, "한 번만");
   Assert.That(PotentialProtection.Check(TestData.Db, foe, holder, M("전기쇼크"), rules, () => .5, false), Is.Null, "물리만");
   // 싸라기눈이 되었을 때 복구
   var t = Engine.Resolve(TestData.Db, new[] { holder, foe }, new[] { Idle(), Idle() }, rules, () => .5);
   Assert.That(t.Fighters[0].Conditions.GuardUsed.Count, Is.GreaterThan(0));
   var hail = F("눈퍼뜨리기");
   var t2 = Engine.Resolve(TestData.Db, new[] { t.Fighters[0], hail }, new[] { Idle(), Idle() }, t.Rules, () => .5);
   Assert.That(t2.Log.Any(x => x.Contains("사용 횟수가 복구")) || t2.Fighters[0].Conditions.GuardUsed.Count == 0, Is.True, string.Join("\n", t2.Log));
  }
  [Test] public void TaleAbilityBlocksFirstAttackInABattle() {
   var holder = F("탈"); var attacker = F();
   var t = Engine.Resolve(TestData.Db, new[] { attacker, holder }, new[] { M("몸통박치기"), Idle() }, new Rules(), () => .5);
   Assert.That(t.Fighters[1].HP, Is.EqualTo(holder.HP), string.Join("\n", t.Log));
   var t2 = Engine.Resolve(TestData.Db, t.Fighters, new[] { Cmd(M("몸통박치기")), Cmd(Idle()) }, t.Rules, t.Trainers, () => .5);
   Assert.That(t2.Fighters[1].HP, Is.LessThan(holder.HP), "두 번째는 맞는다");
  }

  // ----- 사건 효과 모듈 -----
  [Test] public void CopyTypeAndTakenTypeChangeThenRestoreOnLeaving() {
   var copycat = F("도플갱어"); var foe = F("", "불꽃");
   var t = Engine.Resolve(TestData.Db, new[] { copycat, foe }, new[] { Idle(), Idle() }, new Rules(), () => .5);
   Assert.That(t.Fighters[0].Data.types, Is.EqualTo(new[] { "불꽃" }), string.Join("\n", t.Log));
   Mechanics.LeaveField(t.Fighters[0]);
   Assert.That(t.Fighters[0].Data.types, Is.EqualTo(new[] { "노말" }));
   var color = F("변색");
   var t2 = Engine.Resolve(TestData.Db, new[] { color, F() }, new[] { Idle(), M("전기쇼크") }, new Rules(), () => .5);
   Assert.That(t2.Fighters[0].Data.types, Is.EqualTo(new[] { "전기" }), string.Join("\n", t2.Log));
  }
  [Test] public void ReturnDamageUsesTheLastDamageTakenAsAFixedHit() {
   var holder = F("내용물분출", "노말", 30); var foe = F();
   int foeHp = foe.HP;
   var t = Engine.Resolve(TestData.Db, new[] { foe, holder }, new[] { M("몸통박치기"), Idle() }, new Rules(), () => .5);
   Assert.That(t.Fighters[1].HP, Is.EqualTo(0));
   Assert.That(foeHp - t.Fighters[0].HP, Is.EqualTo(30), "마지막으로 받은 대미지 = 실제로 줄어든 HP: " + string.Join("\n", t.Log));
  }
  [Test] public void MirrorStatusCopiesTheSameStatusToTheOpponent() {
   var holder = F("싱크로"); var foe = F();
   using (var scope = EffectScope.Begin(new[] { holder, foe }, new Rules(), () => .5, new[] { new TrainerBattleState(), new TrainerBattleState() })) {
    var log = new List<string>();
    Mechanics.ApplyStatus(holder, foe, "맹독", scope.Rules, () => .5, log);
    StateWatch.CheckCurrent(log.Add);
    Assert.That(foe.Status, Is.EqualTo("맹독"), string.Join("\n", log));
    Assert.That(holder.Status, Is.EqualTo("맹독"));
   }
   var plain = F(); var other = F();
   using (var scope = EffectScope.Begin(new[] { plain, other }, new Rules(), () => .5, new[] { new TrainerBattleState(), new TrainerBattleState() })) {
    var log = new List<string>();
    Mechanics.ApplyStatus(plain, other, "독", scope.Rules, () => .5, log);
    StateWatch.CheckCurrent(log.Add);
    Assert.That(other.Status, Is.EqualTo("정상"));
   }
  }
  [Test] public void FollowRiseCopiesTheOpponentsBoostAmount() {
   var holder = F("편승"); var foe = F();
   using (var scope = EffectScope.Begin(new[] { holder, foe }, new Rules(), () => .5, new[] { new TrainerBattleState(), new TrainerBattleState() })) {
    var log = new List<string>();
    Mechanics.ChangeStage(foe, holder, 3, 2, scope.Rules, "검사", log);
    Assert.That(foe.Stages[3], Is.EqualTo(2));
    Assert.That(holder.Stages[3], Is.EqualTo(2), string.Join("\n", log));
    Mechanics.ChangeStage(foe, holder, 1, -1, scope.Rules, "검사", log);
    Assert.That(holder.Stages[1], Is.EqualTo(0), "내려가는 것은 따라가지 않는다");
   }
  }
  [Test] public void ClearScreensRemovesBothSidesOnEntry() {
   var rules = new Rules();
   rules.Sides[0].Reflect = 4; rules.Sides[1].LightScreen = 3;
   var t = Engine.Resolve(TestData.Db, new[] { F("배리어프리"), F() }, new[] { Idle(), Idle() }, rules, () => .5);
   Assert.That(t.Rules.Sides[0].Reflect, Is.EqualTo(0));
   Assert.That(t.Rules.Sides[1].LightScreen, Is.EqualTo(0));
  }
  [TestCase("폭풍을부르는자", 3)] [TestCase("빛의신전", 3)]
  public void SideStatesFromAbilitiesLastForTheirTurns(string ability, int turns) {
   var t = Engine.Resolve(TestData.Db, new[] { F(ability), F() }, new[] { Idle(), Idle() }, new Rules(), () => .5);
   int total = t.Rules.Sides[0].Tailwind + t.Rules.Sides[0].LightScreen;
   Assert.That(total, Is.InRange(turns - 1, turns), string.Join("\n", t.Log));
   Assert.That(t.Rules.Sides[1].Tailwind + t.Rules.Sides[1].LightScreen, Is.EqualTo(0));
  }
  [Test] public void PerishCountsAndSporeAndMiscEntryEffects() {
   var song = F("멸망의노랫소리");
   var t = Engine.Resolve(TestData.Db, new[] { song, F() }, new[] { Idle(), Idle() }, new Rules(), () => .5);
   Assert.That(t.Fighters[1].Conditions.DeathCount, Is.GreaterThan(0), string.Join("\n", t.Log));
   Assert.That(t.Fighters[0].Conditions.DeathCount, Is.EqualTo(0));
   var body = F("멸망의바디"); var contact = Custom("접촉검사", "물리", 40, "노말"); contact.tags = new[] { "접촉" };
   var t2 = Engine.Resolve(TestData.Db, new[] { F(), body }, new[] { contact, Idle() }, new Rules(), () => .5);
   Assert.That(t2.Fighters[0].Conditions.DeathCount, Is.GreaterThan(0), string.Join("\n", t2.Log));
   Assert.That(t2.Fighters[1].Conditions.DeathCount, Is.GreaterThan(0));
   var spore = F("포자");
   var t3 = Engine.Resolve(TestData.Db, new[] { F(), spore }, new[] { contact, Idle() }, new Rules(), () => .1);
   Assert.That(new[] { "독", "마비", "잠듦" }, Does.Contain(t3.Fighters[0].Status), string.Join("\n", t3.Log));
  }
  [Test] public void SelectionTriggerLetsTheHolderMoveFirstWhenBothUseStatusMoves() {
   var holder = F("앞지르기"); var foe = F(); foe.Data.@base = new[] { 100, 100, 100, 100, 100, 200 };
   var slowStatus = new Move { name = "느린변화", category = "변화", types = new[] { "노말" }, target = "자신", accuracy = 100, tags = new string[0], effect = "" };
   var t = Engine.Resolve(TestData.Db, new[] { holder, foe }, new[] { slowStatus, slowStatus }, new Rules(), () => .5);
   Assert.That(t.Log.Any(x => x.Contains("먼저 행동")), Is.True, string.Join("\n", t.Log));
   var t2 = Engine.Resolve(TestData.Db, new[] { F("앞지르기"), foe.Copy() }, new[] { M("몸통박치기"), slowStatus }, new Rules(), () => .5);
   Assert.That(t2.Log.Any(x => x.Contains("포텐셜로") && x.Contains("먼저 행동")), Is.False, "한쪽이 공격하면 발동하지 않는다");
  }
  [Test] public void TypeAndRatingModulesFromAbilities() {
   var rules = new Rules();
   var steel = F("", "강철"); var poison = Custom("독검사", "특수", 40, "독");
   Assert.That(Mechanics.TypeEffect(TestData.Db, F(), steel, poison, rules), Is.EqualTo(0));
   Assert.That(Mechanics.TypeEffect(TestData.Db, F("붉은녹"), steel, poison, rules), Is.GreaterThan(0));
   var ground = F("", "땅"); var electric = Custom("전기검사", "특수", 40, "전기");
   Assert.That(Mechanics.TypeEffect(TestData.Db, F(), ground, electric, rules), Is.EqualTo(0));
   Assert.That(Mechanics.TypeEffect(TestData.Db, F("통전"), ground, electric, rules), Is.GreaterThan(0));
   var ghost = F("", "고스트"); var normal = Custom("노말검사", "특수", 40, "노말");
   Assert.That(Mechanics.TypeEffect(TestData.Db, F(), ghost, normal, rules), Is.EqualTo(0));
   var ghostMove = Custom("고스트검사", "특수", 40, "고스트");
   Assert.That(Mechanics.TypeEffect(TestData.Db, F("나이트위저드"), F(), ghostMove, rules), Is.EqualTo(1));
   Assert.That(Mechanics.TypeEffect(TestData.Db, F("호담"), steel, poison, rules), Is.GreaterThan(0));
   // 음양사: 공격기술에 고스트 타입을 더해 유리한 상성으로 판정(기존 타입 추가 모듈)
   Assert.That(Mechanics.TypeEffect(TestData.Db, F("음양사"), ghost, normal, rules), Is.EqualTo(2));
   Assert.That(Mechanics.TypeEffect(TestData.Db, F("음양사"), F(), normal, rules), Is.EqualTo(1));
   Assert.That(Mechanics.TypeEffect(TestData.Db, F("음양사"), ghost, Custom("변화검사", "변화", 0, "노말"), rules), Is.EqualTo(0), "변화기술에는 적용하지 않는다");
   var fire = Custom("불꽃검사", "특수", 40, "불꽃");
   Assert.That(Mechanics.TypeEffect(TestData.Db, F(), F("용의비늘"), fire, rules), Is.EqualTo(.5));
   Assert.That(Mechanics.TypeEffect(TestData.Db, F("용의비늘"), F(), fire, rules), Is.EqualTo(1), "자신의 기술에는 적용하지 않는다");
   Assert.That(Mechanics.TypeEffect(TestData.Db, F(), F("유체금속"), Custom("바위검사", "특수", 40, "바위"), rules), Is.EqualTo(.5));
   Assert.That(Mechanics.TypeEffect(TestData.Db, F("참철"), F("", "강철"), Custom("검검사", "물리", 40, "노말"), rules), Is.EqualTo(.5), "태그가 없으면 적용하지 않는다");
  }
 }
}
