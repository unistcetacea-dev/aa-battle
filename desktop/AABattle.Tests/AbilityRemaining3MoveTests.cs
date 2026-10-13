using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle {
 // 남은 특성 모듈화 3차: 기술 판정 모듈(필중·급소·분류·위력/명중 지정·접촉·추가효과·관통·반동·실패·기술 변경)
 [TestFixture] public class AbilityRemaining3MoveTests {
  static int ids;
  public static Fighter F(string ability = "", string type = "노말", int hp = 0) {
   var f = new Fighter(new Pokemon { id = "r3m" + ++ids, name = "검사" + ids, ability = ability, item = "", level = 100,
    types = new[] { type }, @base = new[] { 100, 100, 100, 100, 100, 100 }, iv = new[] { 31, 31, 31, 31, 31, 31 },
    moves = new[] { "몸통박치기", "전기쇼크" }, potentials = new Potential[0] });
   if (hp > 0) f.HP = hp;
   return f;
  }
  public static Move M(string name) { return TestData.Db.moves.First(m => m.name == name); }
  public static Move Idle() { return new Move { name = "미등록", category = "변화", types = new[] { "노말" }, target = "자신", accuracy = 100, tags = new string[0] }; }
  public static void Rule(Fighter f, string text) {
   string condition, effect; PotentialLibrary.Split(text, out condition, out effect);
   f.Data.potentials = (f.Data.potentials ?? new Potential[0]).Concat(new[] { new Potential { id = "종족 ①", name = "공용 검사" + f.Data.potentials.Length, description = text, triggers = condition.Length > 0 ? new string[0] : new[] { TriggerStructures.AlwaysLabel } } }).ToArray();
  }
  static Move Custom(string name, string category, int power, string type, params string[] tags) {
   return new Move { name = name, category = category, types = new[] { type }, power = power, accuracy = 100, target = "상대", tags = tags, effect = "" };
  }

  // ----- 효과 없음(사용자 결정 1) -----
  [TestCase("공생")] [TestCase("리시버")] [TestCase("협연")] [TestCase("사령탑")]
  public void MultiBattleOnlyAbilityIsAutomaticWithNothingToRun(string ability) {
   var definition = AbilityCatalog.Find(ability);
   Assert.That(definition.support, Is.EqualTo("자동"));
   Assert.That(definition.blocker, Is.EqualTo(AbilityNoEffectModules.Reason));
   Assert.That(AbilityModules.ToPotentials(definition), Is.Empty);
   var t = Engine.Resolve(TestData.Db, new[] { F(ability), F() }, new[] { M("몸통박치기"), M("몸통박치기") }, new Rules(), () => .5);
   Assert.That(t.Log.Any(x => x.Contains("판정 대기")), Is.False, string.Join("\n", t.Log));
  }
  [Test] public void OnlyTheMultiBattleSentenceIsSkippedAndTheRestRuns() {
   var mixed = new AbilityDefinition { name = "혼합검사", effect = "다인전 전용 효과이므로 싱글에서는 효과가 없다. 필드에 나왔을 때, 자신의 「공격」이 오른다." };
   mixed.triggers = new string[0]; mixed.effects = new string[0];
   var potentials = AbilityModules.ToPotentials(mixed);
   Assert.That(potentials.Length, Is.EqualTo(1));
   Assert.That(AbilityModules.Runnable(mixed), Is.True);
   Assert.That(AbilityNoEffectModules.Describe(AbilityModules.Sentences(mixed)), Is.EqualTo(AbilityNoEffectModules.PartialReason));
   // 포텐셜에서도 같은 문장은 판정 대기를 남기지 않고 나머지 문장이 실행된다
   var a = F(); Rule(a, "필드에 나왔을 때, 다인전 전용 효과이므로 싱글에서는 효과가 없다. 자신의 「공격」이 오른다.");
   var t = Engine.Resolve(TestData.Db, new[] { a, F() }, new[] { Idle(), Idle() }, new Rules(), () => .5);
   Assert.That(t.Fighters[0].Stages[1], Is.EqualTo(1), string.Join("\n", t.Log));
   Assert.That(t.Log.Any(x => x.Contains("판정 대기")), Is.False, string.Join("\n", t.Log));
  }
  [TestCase("볼줍기")]
  public void OutOfBattleAbilityHasNothingToRun(string ability) {
   var definition = AbilityCatalog.Find(ability);
   Assert.That(definition.support, Is.EqualTo("자동"));
   Assert.That(definition.blocker, Is.EqualTo(AbilityNoEffectModules.Reason));
  }

  // ----- 필중 -----
  [TestCase("노가드", "몸통박치기", true)]
  [TestCase("명인", "몸통박치기", true)]
  [TestCase("명인", "전기쇼크", true)]
  public void SureHitModuleMakesAccuracyInfinite(string ability, string move, bool sure) {
   var a = F(ability); var m = M(move);
   Assert.That(double.IsPositiveInfinity(Mechanics.Accuracy(a, F(), m, new Rules())), Is.EqualTo(sure), ability + move + m.power);
  }
  [Test] public void SureHitConditionAndFieldFormsApply() {
   var strong = M("몸통박치기").Copy(); strong.power = 120; strong.accuracy = 70;
   var weak = strong.Copy(); weak.power = 40;
   var a = F("명인"); var b = F();
   Assert.That(double.IsPositiveInfinity(Mechanics.Accuracy(a, b, weak, new Rules())), Is.True, "위력 60 이하만");
   Assert.That(double.IsPositiveInfinity(Mechanics.Accuracy(a, b, strong, new Rules())), Is.False);
   var field = F("노가드");
   Assert.That(double.IsPositiveInfinity(Mechanics.Accuracy(b, field, strong, new Rules())), Is.True, "필드 전원의 기술");
   var full = F("멀티록"); var hurt = F("멀티록", "노말", 10);
   Assert.That(double.IsPositiveInfinity(Mechanics.Accuracy(full, b, strong, new Rules())), Is.True);
   Assert.That(double.IsPositiveInfinity(Mechanics.Accuracy(hurt, b, strong, new Rules())), Is.False, "체력이 가득일 때만");
   var steel = F(); Rule(steel, "자신의 「전기」 기술은 「강철」 타입에게 필중이다.");
   var electric = strong.Copy(); electric.types = new[] { "전기" };
   Assert.That(double.IsPositiveInfinity(Mechanics.Accuracy(steel, F("", "강철"), electric, new Rules())), Is.True);
   Assert.That(double.IsPositiveInfinity(Mechanics.Accuracy(steel, b, electric, new Rules())), Is.False);
  }
  [Test] public void SetAccuracyModuleMakesEveryMoveAccuracyOneHundred() {
   var m = M("몸통박치기").Copy(); m.accuracy = 50;
   var rules = new Rules();
   Assert.That(Mechanics.Accuracy(F(), F(), Mechanics.JudgedMove(F(), m, F(), rules), rules), Is.EqualTo(50).Within(.01));
   var own = F("인력");
   Assert.That(Mechanics.Accuracy(own, F(), Mechanics.JudgedMove(own, m, F(), rules), rules), Is.EqualTo(100).Within(.01));
   var other = F("인력");
   Assert.That(Mechanics.Accuracy(F(), other, Mechanics.JudgedMove(F(), m, other, rules), rules), Is.EqualTo(100).Within(.01), "필드의 모든 포켓몬");
   Assert.That(m.accuracy, Is.EqualTo(50), "원본은 바뀌지 않는다");
  }

  // ----- 반드시 급소 · 급소 배율 -----
  [TestCase("달인", 40, true)] [TestCase("달인", 80, false)]
  [TestCase("창의달인", 40, true)] [TestCase("창의달인", 80, false)]
  public void AlwaysCriticalAppliesToTheConditionOnly(string ability, int power, bool critical) {
   var m = M("몸통박치기").Copy(); m.power = power;
   Assert.That(Mechanics.HitCritical(F(ability), m, new Rules(), () => .99, F()), Is.EqualTo(critical));
  }
  [Test] public void AlwaysCriticalAfterStateConditionRequiresPoison() {
   var a = F("무도한행동"); var plain = F(); var poisoned = F(); poisoned.Status = "독";
   Assert.That(Mechanics.HitCritical(a, M("몸통박치기"), new Rules(), () => .99, plain), Is.False);
   Assert.That(Mechanics.HitCritical(a, M("몸통박치기"), new Rules(), () => .99, poisoned), Is.True);
  }
  [Test] public void CriticalRateModuleChangesTheFinalMultiplier() {
   var r = new Rules { Critical = true };
   var normal = Engine.Calculate(TestData.Db, F(), F(), M("몸통박치기"), r).Max;
   var sniper = Engine.Calculate(TestData.Db, F("스나이퍼"), F(), M("몸통박치기"), r).Max;
   Assert.That(sniper / (double)normal, Is.EqualTo(1.5).Within(.05), "2배 → 3배");
   Assert.That(Engine.Calculate(TestData.Db, F("스나이퍼"), F(), M("몸통박치기"), new Rules()).Max, Is.EqualTo(Engine.Calculate(TestData.Db, F(), F(), M("몸통박치기"), new Rules()).Max), "급소가 아니면 그대로");
  }

  // ----- 기술 자체를 바꾸는 모듈 -----
  [Test] public void CategoryFollowsTheHigherAttackingStat() {
   var physical = F("이천일류"); physical.Data.@base = new[] { 100, 150, 100, 60, 100, 100 };
   var special = F("이천일류"); special.Data.@base = new[] { 100, 60, 100, 150, 100, 100 };
   var shock = M("전기쇼크"); var tackle = M("몸통박치기");
   Assert.That(Mechanics.JudgedMove(physical, shock, F(), new Rules()).category, Is.EqualTo("물리"));
   Assert.That(Mechanics.JudgedMove(special, tackle, F(), new Rules()).category, Is.EqualTo("특수"));
   Assert.That(shock.category, Is.EqualTo("특수"), "원본은 바뀌지 않는다");
   Assert.That(Mechanics.JudgedMove(special, Idle(), F(), new Rules()).category, Is.EqualTo("변화"), "변화기술은 그대로");
  }
  [Test] public void SetPowerModuleOnlyAffectsTheConditionAndKeepsTheOriginal() {
   var magic = Custom("마법검사", "특수", 30, "페어리", "마법");
   var plain = Custom("일반검사", "특수", 30, "페어리");
   var a = F("마법의진수");
   Assert.That(Mechanics.JudgedMove(a, magic, F(), new Rules()).power, Is.EqualTo(150));
   Assert.That(Mechanics.JudgedMove(a, plain, F(), new Rules()).power, Is.EqualTo(30));
   Assert.That(magic.power, Is.EqualTo(30));
  }
  [TestCase("원격")] [TestCase("원격최면")]
  public void NotContactModuleRemovesTheContactTag(string ability) {
   var contact = Custom("접촉검사", "물리", 40, "노말", "접촉");
   Assert.That(Mechanics.Contact(contact), Is.True);
   var judged = Mechanics.JudgedMove(F(ability), contact, F(), new Rules());
   Assert.That(Mechanics.Contact(judged), Is.False);
   Assert.That(Mechanics.Contact(contact), Is.True);
   Assert.That(MoveConditionOf("「비접촉」").Matches(judged, null, 1), Is.True);
  }
  static MoveCondition MoveConditionOf(string text) { MoveCondition c; Assert.That(MoveCondition.TryParse(text, out c), Is.True); return c; }

  // ----- 조건 토큰(비접촉 · 효과가 굉장 이외 · 우선도 ±0) -----
  [Test] public void ConditionTokensMatchByTheirMeaning() {
   var contact = Custom("접촉", "물리", 40, "노말", "접촉"); var ranged = Custom("원거리", "특수", 40, "노말");
   Assert.That(MoveConditionOf("「비접촉」").Matches(contact, null, 1), Is.False);
   Assert.That(MoveConditionOf("「비접촉」").Matches(ranged, null, 1), Is.True);
   var notSuper = MoveConditionOf("「효과가 굉장 이외」");
   Assert.That(notSuper.Matches(ranged, null, 2), Is.False);
   Assert.That(notSuper.Matches(ranged, null, 1), Is.True);
   Assert.That(notSuper.Matches(ranged, null, .5), Is.True);
   Assert.That(MoveConditionOf("「효과가 굉장」").Matches(ranged, null, 2), Is.True, "기존 「효과가 굉장」은 그대로");
   var zero = MoveConditionOf("「우선도 ±0」"); var quick = ranged.Copy(); quick.priority = 1;
   Assert.That(zero.Matches(ranged, null, 1), Is.True);
   Assert.That(zero.Matches(quick, null, 1), Is.False);
   Assert.That(zero.Text, Is.EqualTo("「우선도 ±0」"));
  }

  // ----- 추가효과 · 연속기 · 방어 관통 · 반동 -----
  [Test] public void SecondaryEffectModulesSuppressAndDouble() {
   var hit = Custom("추가검사", "물리", 40, "노말"); var log = new List<string>();
   int rolls = 0;
   Func<double> rng = () => { rolls++; return .35; };   // 3할은 성공 못하지만 6할은 성공
   var rules = new Rules();
   var plain = F(); var target = F();
   Assert.That(SecondaryEffects.Roll(plain, target, "「3할:독」", "추가효과", rules, rng, log, null), Is.False);
   Assert.That(SecondaryEffects.Roll(F("하늘의은총"), F(), "「3할:독」", "추가효과", rules, rng, log, null), Is.True, "확률 2배");
   Assert.That(SecondaryEffects.Roll(F("우격다짐"), F(), "「9할:독」", "추가효과", rules, () => 0, log, null), Is.False, "자신의 추가효과 무효");
   Assert.That(SecondaryEffects.Roll(F(), F("인분"), "「9할:독」", "추가효과", rules, () => 0, log, null), Is.False, "상대의 추가효과 무효");
   Assert.That(SecondaryEffects.Roll(F(), F(), "「9할:독」", "추가효과", rules, () => 0, log, null), Is.True);
  }
  [Test] public void FullMultiHitUsesTheMaximumCount() {
   var multi = Custom("연속검사", "물리", 20, "노말", "연속"); multi.effect = "2~5회 「연속」 기술";
   int normal = Mechanics.HitCount(multi, () => 0, new List<string>());
   Assert.That(normal, Is.EqualTo(2));
   Assert.That(AbilityMoveModules.HitCount(F("스킬링크"), F(), multi, new Rules(), () => 0, new List<string>()), Is.EqualTo(5));
   Assert.That(AbilityMoveModules.HitCount(F(), F(), multi, new Rules(), () => 0, new List<string>()), Is.EqualTo(2));
  }
  [TestCase("반향", "소리")] [TestCase("보이지않는주먹", "접촉")] [TestCase("악마의손", "접촉")]
  public void ProtectPierceModuleNeedsTheConditionTag(string ability, string tag) {
   var tagged = Custom("태그검사", "물리", 40, "노말", tag); var plain = Custom("무태그검사", "물리", 40, "노말");
   Assert.That(AbilityPhrases.PiercesProtect(F(ability), F(), tagged, new Rules()), Is.True);
   Assert.That(AbilityPhrases.PiercesProtect(F(ability), F(), plain, new Rules()), Is.False);
  }
  [Test] public void ConditionalRecoilImmunityOnlyCoversTheCondition() {
   var a = F("파괴의힘"); int hp = a.HP;
   var normal = Custom("반동노말", "물리", 40, "노말"); normal.effect = "준 대미지의 1/4만큼 반동 대미지를 받는다.";
   var fire = normal.Copy(); fire.types = new[] { "불꽃" };
   MoveModules.Recoil(a, F(), normal, new Rules(), 100, new List<string>());
   Assert.That(a.HP, Is.EqualTo(hp));
   MoveModules.Recoil(a, F(), fire, new Rules(), 100, new List<string>());
   Assert.That(a.HP, Is.EqualTo(hp - 25));
  }
  [TestCase("비비드바디")] [TestCase("테일아머")]
  public void PriorityMovesFailAgainstTheHolderWithoutBeingNullified(string ability) {
   var quick = Custom("선공검사", "물리", 40, "노말"); quick.priority = 1;
   Assert.That(MoveModules.FailReason(F(), F(ability), quick), Is.Not.Empty);
   Assert.That(MoveModules.FailReason(F(), F(ability), M("몸통박치기")), Is.Empty);
   Assert.That(PotentialProtection.Check(TestData.Db, F(), F(ability), quick, new Rules(), () => .5, false), Is.Null, "무효화(방호)가 아니다");
  }
  [Test] public void FieldFailModuleStopsSelfDestructForBothSides() {
   var boom = M("자폭");
   Assert.That(MoveModules.FailReason(F(), F("습기"), boom), Is.Not.Empty);
   Assert.That(MoveModules.FailReason(F("습기"), F(), boom), Is.Not.Empty);
   Assert.That(MoveModules.FailReason(F(), F(), boom), Is.Empty);
  }

  // ----- 기술 변경 · 게으름 · 쉬는 행동 -----
  [TestCase("동결", "고드름떨구기")] [TestCase("증발", "열탕")]
  public void OpponentAttackIsReplacedByTheNamedMoveWithTheChance(string ability, string name) {
   var holder = F(ability); var attacker = F(); var log = new List<string>();
   var replaced = AbilityMoveModules.Replace(TestData.Db, attacker, holder, M("몸통박치기"), new Rules(), () => .1, log);
   Assert.That(replaced.name, Is.EqualTo(name));
   Assert.That(AbilityMoveModules.Replace(TestData.Db, attacker, holder, M("몸통박치기"), new Rules(), () => .9, log).name, Is.EqualTo("몸통박치기"), "5할 실패");
   Assert.That(AbilityMoveModules.Replace(TestData.Db, attacker, holder, Idle(), new Rules(), () => .1, log).name, Is.EqualTo("미등록"), "변화기술은 그대로");
  }
  [Test] public void TruantMovesAlternateBetweenActingAndFailing() {
   var a = F("게으름"); var b = F();
   var rules = new Rules();
   Assert.That(MoveModules.FailReason(a, b, M("몸통박치기")), Is.Empty);
   Assert.That(MoveModules.FailReason(a, b, M("몸통박치기")), Is.Not.Empty);
   Assert.That(MoveModules.FailReason(a, b, M("몸통박치기")), Is.Empty);
  }
  [Test] public void ReplacementAndTruantRunInARealTurn() {
   var a = F("게으름"); var b = F("", "노말", 9999);
   var t = Engine.Resolve(TestData.Db, new[] { a, b }, new[] { M("몸통박치기"), Idle() }, new Rules(), () => .5);
   int afterFirst = t.Fighters[1].HP;
   Assert.That(afterFirst, Is.LessThan(b.HP), "첫 행동은 맞는다");
   var t2 = Engine.Resolve(TestData.Db, t.Fighters, new[] { new BattleCommand { Move = M("몸통박치기") }, new BattleCommand { Move = Idle() } }, t.Rules, t.Trainers, () => .5);
   Assert.That(t2.Fighters[1].HP, Is.EqualTo(afterFirst), string.Join("\n", t2.Log));
   Assert.That(t2.Log.Any(x => x.Contains("행동한 다음 T")), Is.True);
  }
  [Test] public void ConditionalLagAndAbilityPierceUseTheMoveBeingUsed() {
   var holder = F("균사의힘");
   Assert.That(MiscModules.Lags(holder, F(), new Rules(), Idle()), Is.True);
   Assert.That(MiscModules.Lags(holder, F(), new Rules(), M("몸통박치기")), Is.False);
   Assert.That(MiscModules.Lags(holder, F(), new Rules()), Is.False, "기술을 모르면 걸지 않는다");
   holder.Conditions.Acting = true; holder.Conditions.ActingMove = Idle();
   Assert.That(Mechanics.BypassAbility(holder), Is.True);
   holder.Conditions.ActingMove = M("몸통박치기");
   Assert.That(Mechanics.BypassAbility(holder), Is.False);
  }
  [Test] public void GroundMovesReachFloatingTargetsOnlyWithTheModule() {
   var ground = M("지진"); var floating = F("", "노말"); Rule(floating, "자신은 공중에 떠오른다.");
   var plain = F();
   Assert.That(Mechanics.Block(TestData.Db, plain, floating, ground, new Rules()), Is.Not.Empty, "기본은 막힌다");
   var user = F("천원돌파");
   Assert.That(Mechanics.Block(TestData.Db, user, floating, ground, new Rules()), Does.Not.Contain("공중"));
  }
 }
}
