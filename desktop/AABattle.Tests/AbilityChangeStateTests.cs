using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle {
 [TestFixture] public class AbilityChangeStateTests {
  static int ids;
  static Fighter F(string ability = "") {
   return new Fighter(new Pokemon {
    id = "change-state-" + ++ids, name = "특성 검사" + ids, ability = ability, item = "", level = 100,
    types = new[] { "노말" }, @base = new[] { 100, 100, 100, 100, 100, 100 },
    iv = new[] { 31, 31, 31, 31, 31, 31 }, moves = new[] { "몸통박치기" }, potentials = new Potential[0]
   });
  }
  static void Rule(Fighter owner, string sentence) {
   owner.Data.potentials = owner.Data.potentials.Concat(new[] {
    new Potential { id = "종족 ①", name = "공용 특성 효과", description = sentence, triggers = new[] { TriggerStructures.AlwaysLabel } }
   }).ToArray();
  }
  static Move Idle() {
   return new Move { name = "미등록", category = "변화", types = new[] { "노말" }, target = "자신", accuracy = 100, tags = new string[0] };
  }
  static EffectOutcome Run(string line, Fighter owner, Fighter other, Rules rules) {
   var call = new EffectCall {
    Source = new PotentialInstance { Owner = owner, Name = "공용 검사" },
    Event = new PotentialEvent { Owner = owner, Other = other }, Line = line,
    Rules = rules, Rng = () => .5, Log = new List<string>()
   };
   var executor = PotentialExecutors.Find(call);
   Assert.That(executor, Is.Not.Null, line);
   return executor.Run(call);
  }

  [TestCase("자신과 상대의 〔특성〕을 뒤바꾼다.", "자신과 상대의 특성을 교환한다.")]
  [TestCase("자신의 특성을 상대와 같은 특성으로 변화시킨다.", "상대의 특성을 복사한다.")]
  [TestCase("이 특성은 변화하지 않는다.", "자신의 특성은 변화되지 않는다.")]
  [TestCase("이 〔특성〕은 무효화되지 않는다.", "자신의 특성은 무효화되지 않는다.")]
  [TestCase("이 〔특성〕은 관통되지 않는다.", "자신의 특성은 관통되지 않는다.")]
  public void NormalizesAbilityChangeModules(string raw, string expected) {
   Assert.That(EffectModules.Normalize(raw), Is.EqualTo(expected));
   Assert.That(EffectModules.Classify(expected), Is.Not.Empty);
  }

  [Test] public void CopyUsesCurrentAbilityAndRestoresOriginalOnLeave() {
   var owner = F("가속"); var other = F("가뭄"); AbilityRuntime.SetCurrent(other, "쓱쓱"); var rules = new Rules();
   using (EffectScope.Begin(new[] { owner, other }, rules, () => .5)) {
    Assert.That(Run("상대의 특성을 복사한다.", owner, other, rules), Is.EqualTo(EffectOutcome.Success));
    Assert.That(AbilityRuntime.Name(owner), Is.EqualTo("쓱쓱"));
    Assert.That(AbilityRuntime.Name(other), Is.EqualTo("쓱쓱"));
    Assert.That(Run("상대의 특성을 복사한다.", owner, other, rules), Is.EqualTo(EffectOutcome.Failed));
    Mechanics.LeaveField(owner);
    Assert.That(AbilityRuntime.Name(owner), Is.EqualTo("가속"));
   }
  }

  [Test] public void SwapFailsAtomicallyWhenEitherCurrentAbilityCannotChange() {
   var owner = F("가속"); var other = F("가뭄"); Rule(other, "자신의 특성은 변화되지 않는다.");
   Assert.That(Run("자신과 상대의 특성을 교환한다.", owner, other, new Rules()), Is.EqualTo(EffectOutcome.Failed));
   Assert.That(AbilityRuntime.Name(owner), Is.EqualTo("가속"));
   Assert.That(AbilityRuntime.Name(other), Is.EqualTo("가뭄"));
   Assert.That(owner.Conditions.AbilityBefore, Is.Null);
  }

  [Test] public void GainingEntryAbilityDoesNotDispatchEntryAndGainingGasUpdatesSuppression() {
   var owner = F("가속"); var other = F("가뭄"); var rules = new Rules();
   using (EffectScope.Begin(new[] { owner, other }, rules, () => .5)) {
    Assert.That(Run("상대의 특성을 복사한다.", owner, other, rules), Is.EqualTo(EffectOutcome.Success));
    Assert.That(rules.Weather, Is.EqualTo("없음"));
    AbilityRuntime.SetCurrent(other, "화학변화가스");
    Assert.That(owner.AbilitySuppressed, Is.True);
    Assert.That(Run("상대의 특성을 복사한다.", owner, other, rules), Is.EqualTo(EffectOutcome.Success));
    Assert.That(owner.AbilitySuppressed, Is.False);
   }
  }

  [Test] public void FaintedAllyCopyUsesOwnSideHistoryAndRejectsSelf() {
   var owner = F("가속"); owner.FieldSide = 0;
   var rules = new Rules {
    LastFaintedAbility = new[] { "쓱쓱", "가뭄" },
    LastFaintedMember = new[] { "#departed-ally", "#departed-rival" }
   };
   Assert.That(Run("직전에 빈사한 아군의 특성을 복사한다.", owner, F(), rules), Is.EqualTo(EffectOutcome.Success));
   Assert.That(AbilityRuntime.Name(owner), Is.EqualTo("쓱쓱"));
   rules.LastFaintedMember[0] = Mechanics.MemberKey(owner.Data);
   rules.LastFaintedAbility[0] = "잔비";
   Assert.That(Run("직전에 빈사한 아군의 특성을 복사한다.", owner, F(), rules), Is.EqualTo(EffectOutcome.Failed));
   Assert.That(AbilityRuntime.Name(owner), Is.EqualTo("쓱쓱"));
  }

  [Test] public void ChangeProtectionBlocksExistingNamedChangeAndCurrentSetter() {
   var owner = F("가속");
   Rule(owner, "자신의 특성은 변화되지 않는다.");
   var log = new List<string>();
   MiscModules.ChangeAbility(owner, "가뭄", 2, log, "공용 검사");
   Assert.That(AbilityRuntime.Name(owner), Is.EqualTo("가속"));
   Assert.That(owner.Conditions.AbilityBefore, Is.Null);
   AbilityRuntime.SetCurrent(owner, "잔비");
   Assert.That(AbilityRuntime.Name(owner), Is.EqualTo("가속"));
  }

  [Test] public void SuppressionProtectionRestoresSlotWithoutFiringEntry() {
   var owner = F("가뭄"); var gas = F("화학변화가스"); var rules = new Rules();
   Rule(owner, "자신의 특성은 무효화되지 않는다.");
   using (EffectScope.Begin(new[] { owner, gas }, rules, () => .5)) {
    AbilityRuntime.RefreshSuppression(new[] { owner, gas }, null);
    Assert.That(owner.AbilitySuppressed, Is.False);
    Assert.That(owner.Data.potentials.Any(p => p.id == AbilityModules.Slot), Is.True);
    Assert.That(rules.Weather, Is.EqualTo("없음"));
   }
  }

  [TestCase(false)] [TestCase(true)]
  public void AbilityBypassProtectionPreservesAbilityDropGuard(bool protectedAbility) {
   var source = F("틀깨기"); var target = F("클리어바디"); source.Conditions.Acting = true;
   if (protectedAbility) Rule(target, "자신의 특성은 관통되지 않는다.");
   using (EffectScope.Begin(new[] { source, target }, new Rules(), () => .5)) {
    bool changed = Mechanics.ChangeStage(target, source, 1, -1, new Rules(), "검사", new List<string>());
    Assert.That(changed, Is.EqualTo(!protectedAbility));
    Assert.That(target.Stages[1], Is.EqualTo(protectedAbility ? 0 : -1));
   }
  }

  [TestCase("도발")]
  [TestCase("씨뿌리기")]
  [TestCase("하품")]
  [TestCase("금제")]
  public void OpponentConditionImmunityBlocksAllConditionModulesButAllowsSelf(string state) {
   var source = F(); var target = F(); var rules = new Rules(); var log = new List<string>();
   Rule(target, "상대에 의해 자신은 「상태변화」 상태가 되지 않는다.");
   Assert.That(StateEffects.Apply("상대를 「" + state + "」 상태로 한다.", source, target, rules, () => .5, log, "검사"), Is.EqualTo(EffectOutcome.Failed));
   Assert.That(Mechanics.HasCondition(target, state), Is.False);
   Assert.That(StateEffects.Apply("자신을 「" + state + "」 상태로 한다.", target, source, rules, () => .5, log, "검사"), Is.EqualTo(EffectOutcome.Success));
  }

  [Test] public void ConditionGroupDoesNotBlockMajorStatusAndExplicitIgnorePiercesIt() {
   var source = F(); var target = F(); var rules = new Rules(); var log = new List<string>();
   Rule(target, "상대에 의해 자신은 「상태변화」 상태가 되지 않는다.");
   Assert.That(StateEffects.Apply("상대를 「독」 상태로 한다.", source, target, rules, () => .5, log, "검사"), Is.EqualTo(EffectOutcome.Success));
   Assert.That(StateEffects.Apply("상태 면역을 무시하고 상대를 「도발」 상태로 한다.", source, target, rules, () => .5, log, "검사"), Is.EqualTo(EffectOutcome.Success));
  }

  [Test] public void AbilityBypassDoesNotBypassOrdinaryPotentialConditionImmunity() {
   var source = F("틀깨기"); var target = F(); source.Conditions.Acting = true;
   Rule(target, "상대에 의해 자신은 「상태변화」 상태가 되지 않는다.");
   Assert.That(StateEffects.Apply("상대를 「도발」 상태로 한다.", source, target, new Rules(), () => .5, new List<string>(), "검사"), Is.EqualTo(EffectOutcome.Failed));
  }

  [TestCase("도발", "도발")]
  [TestCase("씨뿌리기", "씨뿌리기")]
  [TestCase("앙코르", "앙코르")]
  public void ConditionImmunityAlsoBlocksDirectStatusMovePaths(string moveName, string state) {
   var source = F(); var target = F("검은연기"); var rules = new Rules();
   target.Conditions.LastMove = "몸통박치기";
   Mechanics.SetParties(rules, new[] { source, target });
   var move = new Move { name = moveName, category = "변화", target = "상대", types = new[] { "노말" }, tags = new string[0], accuracy = 100 };
   Mechanics.StatusMove(source, target, move, rules, () => .5, new List<string>());
   Assert.That(Mechanics.HasCondition(target, state), Is.False);
  }

  [Test] public void ConditionImmunityAlsoBlocksDirectBindPath() {
   var source = F(); var target = F("검은연기");
   var move = new Move { name = "공용 조이기", category = "물리", tags = new[] { "조이기" }, effect = "4~5T 동안 상대를 「조이기」 상태로 한다." };
   MoveModules.Bind(source, target, move, new Rules(), () => .5, new List<string>());
   Assert.That(target.Conditions.Bound, Is.EqualTo(0));
  }

  [Test] public void ContactSwapUsesCurrentAbilitiesAndDoesNotRunGainedEntry() {
   var attacker = F("가속"); AbilityRuntime.SetCurrent(attacker, "가뭄"); var owner = F("떠도는영혼");
   attacker.Conditions.EntryTriggered = owner.Conditions.EntryTriggered = true;
   attacker.Conditions.AbilityEntryApplied = owner.Conditions.AbilityEntryApplied = true;
   var move = TestData.Db.moves.First(m => m.name == "몸통박치기").Copy(); move.power = 10;
   var result = Engine.Resolve(TestData.Db, new[] { attacker, owner }, new[] { move, Idle() }, new Rules(), () => .5);
   Assert.That(AbilityRuntime.Name(result.Fighters[0]), Is.EqualTo("떠도는영혼"));
   Assert.That(AbilityRuntime.Name(result.Fighters[1]), Is.EqualTo("가뭄"));
   Assert.That(result.Rules.Weather, Is.EqualTo("없음"));
   Mechanics.LeaveField(result.Fighters[1]);
   Assert.That(AbilityRuntime.Name(result.Fighters[1]), Is.EqualTo("떠도는영혼"));
  }
 }
}
