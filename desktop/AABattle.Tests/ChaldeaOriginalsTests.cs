using System;
using System.Linq;
using NUnit.Framework;

namespace AABattle {
 // 지시서 R: 칼데아 오리지널 기술 2개 · 특성 5개
 [TestFixture] public class ChaldeaOriginalsTests {
  static int ids;
  static Fighter F(string ability = "", string item = "", string type = "노말") {
   return new Fighter(new Pokemon {
    id = "pk-co" + (++ids), name = "C" + ids, level = 100, types = new[] { type }, ability = ability, item = item,
    @base = new[] { 100, 100, 100, 100, 100, 100 }, iv = new[] { 31, 31, 31, 31, 31, 31 },
    moves = new[] { "몸통박치기" }, potentials = new Potential[0]
   });
  }
  static Move M(string name) { return TestData.Db.moves.First(x => x.name == name); }
  static Move Idle() { return new Move { name = "미등록", category = "변화", types = new[] { "노말" }, tags = new string[0], accuracy = 100, target = "자신" }; }
  static TurnResult Turn(Fighter a, Fighter b, Move ma, Move mb, Rules rules = null) {
   return Engine.Resolve(TestData.Db, new[] { a, b }, new[] { ma, mb }, rules ?? new Rules(), () => .5);
  }
  static string Log(TurnResult t) { return string.Join("\n", t.Log); }

  [Test] public void ShoutChangesOpponentAbilityToHotBlood() {
   var move = M("열창");
   Assert.That(move.tags, Does.Contain("소리"));
   var t = Turn(F(), F("부유"), move, Idle());
   Assert.That(AbilityRuntime.Name(t.Fighters[1]), Is.EqualTo("열혈"), Log(t));
  }

  [Test] public void ShoutFailsAgainstAbilityThatDoesNotChange() {
   var t = Turn(F(), F("천개"), M("열창"), Idle());
   Assert.That(AbilityRuntime.Name(t.Fighters[1]), Is.EqualTo("천개"), Log(t));
  }

  [Test] public void HypersoundConfusesAndLowersAttackAndSpecialAttack() {
   var move = M("하이퍼사운드");
   Assert.That(move.tags, Does.Contain("소리"));
   var t = Turn(F(), F(), move, Idle());
   Assert.That(Log(t), Does.Contain("「혼란」"), Log(t));
   Assert.That(t.Fighters[1].Stages[1], Is.EqualTo(-1), Log(t));
   Assert.That(t.Fighters[1].Stages[3], Is.EqualTo(-1), Log(t));
  }

  [Test] public void CanopySuppressesOpponentAbilityAndKeepsItsOwn() {
   var owner = F("천개"); var other = F("쓱쓱");
   AbilityRuntime.RefreshSuppression(new[] { owner, other }, null);
   Assert.That(other.AbilitySuppressed, Is.True);
   Assert.That(owner.AbilitySuppressed, Is.False);
   Assert.That(AbilityChangeModules.CannotChange(owner), Is.True);
  }

  [Test] public void WesternMakesBulletAndBallMovesSureHitForBothSides() {
   var bullet = TestData.Db.moves.First(x => x.tags.Contains("탄/볼") && x.accuracy > 0 && x.accuracy < 100 && x.category != "변화");
   var plain = TestData.Db.moves.First(x => !x.tags.Contains("탄/볼") && !x.effect.Contains("탄") && !x.effect.Contains("볼") && !x.tags.Contains("필중") && x.accuracy > 0 && x.accuracy < 100 && x.category != "변화");
   var rules = new Rules();
   var owner = F("서부극"); var other = F();
   Assert.That(Mechanics.Accuracy(owner, other, bullet, rules), Is.EqualTo(double.PositiveInfinity), "자신의 탄 기술");
   Assert.That(Mechanics.Accuracy(other, owner, bullet, rules), Is.EqualTo(double.PositiveInfinity), "상대의 탄 기술");
   Assert.That(Mechanics.Accuracy(owner, other, plain, rules), Is.LessThan(100));
   Assert.That(Mechanics.Accuracy(F(), F(), bullet, rules), Is.LessThan(100), "특성이 없으면 평소대로");
  }

  [Test] public void SenseBlockDisablesOwnItemsAndIgnoresOpponentEvasionRise() {
   var owner = F("감각차폐", "먹다남은음식");
   Assert.That(AbilityItemModules.CanUseItem(owner), Is.False);
   var move = TestData.Db.moves.First(x => x.accuracy == 100 && x.category != "변화" && !x.tags.Contains("필중") && !x.tags.Contains("연속"));
   var rules = new Rules();
   var evasive = F(); evasive.EvasionStage = 6;
   double blocked = Mechanics.Accuracy(owner, evasive, move, rules);
   double normal = Mechanics.Accuracy(F(), evasive, move, rules);
   Assert.That(normal, Is.LessThan(100));
   Assert.That(blocked, Is.EqualTo(100), "상대의 회피 상승을 무시한다");
   evasive.EvasionStage = -2;
   Assert.That(Mechanics.Accuracy(owner, evasive, move, rules), Is.EqualTo(Mechanics.Accuracy(F(), evasive, move, rules)), "하락은 그대로");
  }

  [Test] public void WindBladeBoostsAttackUnderOwnTailwind() {
   var move = M("몸통박치기");
   var plain = Turn(F("바람칼날"), F(), move, Idle());
   var rules = new Rules();
   rules.Sides[0].Tailwind = 4;
   var boosted = Turn(F("바람칼날"), F(), move, Idle(), rules);
   int plainDamage = Max(plain.Fighters[1]) - plain.Fighters[1].HP;
   int boostedDamage = Max(boosted.Fighters[1]) - boosted.Fighters[1].HP;
   Assert.That(plainDamage, Is.GreaterThan(0), Log(plain));
   Assert.That(boostedDamage, Is.GreaterThan(plainDamage * 1.3), Log(boosted));
  }
  static int Max(Fighter f) { return Mechanics.MaxHP(f, f, new Rules()); }

  [Test] public void RegenerationAddsCassetteTypeAndBoostsTechnoBuster() {
   var owner = F("재생", "블레이즈카세트");
   ItemRuntime.Sync(owner);
   Assert.That(owner.Data.types, Is.EqualTo(new[] { "노말", "불꽃" }));
   var move = M("테크노버스터（1P）");
   var rules = new Rules();
   double boosted = PotentialModifiers.Power(owner, F(), move, rules, 1);
   double normal = PotentialModifiers.Power(F(), F(), move, rules, 1);
   Assert.That(boosted / normal, Is.EqualTo(1.2).Within(.001));
   owner.Data.item = ""; ItemRuntime.Sync(owner);
   Assert.That(owner.Data.types, Is.EqualTo(new[] { "노말" }), "카세트가 없으면 원래 타입");
  }
 }
}
