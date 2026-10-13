using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle {
 // 남은 특성 모듈화 3차: 소지품 모듈(약탈 규칙·파괴·해석·나무열매) 과 종족치 이벤트
 [TestFixture] public class AbilityRemaining3ItemTests {
  static Fighter F(string ability = "", string type = "노말", int hp = 0, string item = "") {
   var f = AbilityRemaining3MoveTests.F(ability, type, hp);
   if (item.Length > 0) { f.Data.item = item; ItemRuntime.Sync(f); }
   return f;
  }
  static Move M(string name) { return AbilityRemaining3MoveTests.M(name); }
  static EffectScope Scope(Fighter[] fighters, Rules rules) { return EffectScope.Begin(fighters, rules, () => .5, new[] { new TrainerBattleState(), new TrainerBattleState() }); }
  static Move Idle() { return AbilityRemaining3MoveTests.Idle(); }

  // ----- 사용자 결정 2: 약탈 -----
  [Test] public void LootTakesTheItemWhenTheHolderIsEmptyHanded() {
   var robber = F("약탈"); var foe = F("", "노말", 1, "구애스카프");
   var t = Engine.Resolve(TestData.Db, new[] { robber, foe }, new[] { M("몸통박치기"), Idle() }, new Rules(), () => .5);
   Assert.That(t.Fighters[1].HP, Is.EqualTo(0));
   Assert.That(t.Fighters[0].Data.item, Is.EqualTo("구애스카프"), string.Join("\n", t.Log));
   Assert.That(t.Fighters[1].Data.item, Is.Empty);
  }
  [Test] public void LootDestroysOnlyTheOpponentsItemWhenTheHolderHoldsOne() {
   var robber = F("약탈", "노말", 0, "구애머리띠"); var foe = F("", "노말", 1, "구애스카프");
   var t = Engine.Resolve(TestData.Db, new[] { robber, foe }, new[] { M("몸통박치기"), Idle() }, new Rules(), () => .5);
   Assert.That(t.Fighters[0].Data.item, Is.EqualTo("구애머리띠"), "자신의 소지품은 그대로");
   Assert.That(t.Fighters[1].Data.item, Is.Empty, string.Join("\n", t.Log));
   Assert.That(t.Log.Any(x => x.Contains("파괴")), Is.True);
  }
  [Test] public void LootDoesNotTriggerWithoutDefeating() {
   var robber = F("약탈"); var foe = F("", "노말", 0, "구애스카프");
   var t = Engine.Resolve(TestData.Db, new[] { robber, foe }, new[] { M("몸통박치기"), Idle() }, new Rules(), () => .5);
   Assert.That(t.Fighters[0].Data.item, Is.Empty);
   Assert.That(t.Fighters[1].Data.item, Is.EqualTo("구애스카프"));
  }

  // ----- 파괴 · 해석 -----
  [Test] public void BreakDestroysTheOpponentsItemOnHitUnlessProtected() {
   var logic = F("파괴의논리"); var foe = F("", "노말", 0, "구애스카프");
   var t = Engine.Resolve(TestData.Db, new[] { logic, foe }, new[] { M("몸통박치기"), Idle() }, new Rules(), () => .5);
   Assert.That(t.Fighters[1].Data.item, Is.Empty, string.Join("\n", t.Log));
   // 파괴의논리 자신은 방호 특성을 관통하므로 점착도 뚫는다. 같은 문장을 관통 없이 쓰면 점착이 막는다
   var plainBreaker = F(); AbilityRemaining3MoveTests.Rule(plainBreaker, "자신의 공격이 명중했을 때, 상대의 소지품을 파괴한다.");
   var sticky = F("점착", "노말", 0, "구애스카프");
   var t2 = Engine.Resolve(TestData.Db, new[] { plainBreaker, sticky }, new[] { M("몸통박치기"), Idle() }, new Rules(), () => .5);
   Assert.That(t2.Fighters[1].Data.item, Is.EqualTo("구애스카프"), string.Join(" / ", t2.Log));
   var pierced = F("점착", "노말", 0, "구애스카프");
   var t4 = Engine.Resolve(TestData.Db, new[] { F("파괴의논리"), pierced }, new[] { M("몸통박치기"), Idle() }, new Rules(), () => .5);
   Assert.That(t4.Fighters[1].Data.item, Is.Empty);
   var t3 = Engine.Resolve(TestData.Db, new[] { F("파괴의논리"), F() }, new[] { Idle(), Idle() }, new Rules(), () => .5);
   Assert.That(t3.Fighters[1].Data.item, Is.Empty, "명중하지 않으면 해당 없음");
  }
  [Test] public void FriskAnnouncesTheOpponentsItemOnEntry() {
   var t = Engine.Resolve(TestData.Db, new[] { F("통찰"), F("", "노말", 0, "구애스카프") }, new[] { Idle(), Idle() }, new Rules(), () => .5);
   Assert.That(t.Log.Any(x => x.Contains("상대의 소지품은") && x.Contains("구애스카프")), Is.True, string.Join("\n", t.Log));
   var t2 = Engine.Resolve(TestData.Db, new[] { F("통찰"), F() }, new[] { Idle(), Idle() }, new Rules(), () => .5);
   Assert.That(t2.Log.Any(x => x.Contains("상대의 소지품은 없다")), Is.True);
  }

  // ----- 나무열매 사건 · 사용 -----
  [Test] public void BerryUsedEventHealsWithCheekPouchAndRestoresWithHarvestLikeAbilities() {
   var rules = new Rules();
   var pouch = F("볼주머니", "노말", 10, "치리열매"); var foe = F();
   using (Scope(new[] { pouch, foe }, rules)) {
    var log = new List<string>();
    ItemRuntime.Lose(pouch, "소비", log.Add);
    Assert.That(pouch.HP, Is.GreaterThan(10), string.Join("\n", log));
    Assert.That(pouch.LostBerry, Is.EqualTo("치리열매"));
   }
   var other = F("볼주머니", "노말", 10, "구애스카프");
   using (Scope(new[] { other, foe }, rules)) {
    var log = new List<string>();
    ItemRuntime.Lose(other, "소비", log.Add);
    Assert.That(other.HP, Is.EqualTo(10), "나무열매가 아니면 해당 없음");
    ItemRuntime.Lose(F("볼주머니", "노말", 10, "치리열매"), "파괴", log.Add);
   }
   var bumper = F("풍작", "노말", 0, "치리열매");
   using (Scope(new[] { bumper, foe }, rules)) {
    var log = new List<string>();
    ItemRuntime.Lose(bumper, "소비", log.Add);
    Assert.That(bumper.Data.item, Is.EqualTo("치리열매"), "나무열매가 부활한다: " + string.Join("\n", log));
    Assert.That(bumper.LostBerry, Is.Empty);
   }
  }
  [Test] public void GluttonyUsesTheBerryAtHalfHealthRegardlessOfItsOwnCondition() {
   var glutton = F("먹보", "노말", 0, "치리열매"); var foe = F();
   var rules = new Rules();
   int max = Mechanics.MaxHP(glutton, foe, rules);
   using (Scope(new[] { glutton, foe }, rules)) {
    var log = new List<string>();
    Mechanics.Hurt(glutton, foe, rules, max / 4, "검사", log);
    Assert.That(glutton.Data.item, Is.EqualTo("치리열매"), "1/2보다 높으면 쓰지 않는다");
    Mechanics.Hurt(glutton, foe, rules, max / 2, "검사", log);
    Assert.That(glutton.HP, Is.LessThan(max / 2));
    Assert.That(glutton.Data.item, Is.Empty, string.Join("\n", log));
    Assert.That(glutton.Stages[1], Is.EqualTo(1), "열매의 효과는 그대로 일어난다");
   }
  }
  [TestCase(.0, true, false)] [TestCase(.9, false, false)] [TestCase(.9, true, true)]
  public void HarvestRestoresWithChanceAndAlwaysUnderSun(double roll, bool sunny, bool restored) {
   var holder = F("수확"); holder.LostBerry = "치리열매";
   var rules = new Rules { Weather = sunny ? "쾌청" : "없음" };
   var t = Engine.Resolve(TestData.Db, new[] { holder, F() }, new[] { Idle(), Idle() }, rules, () => roll);
   bool got = t.Fighters[0].Data.item == "치리열매";
   Assert.That(got, Is.EqualTo(roll < .5 || restored), string.Join("\n", t.Log));
  }
  [Test] public void CudChewEatsTheBerryAgainAtTheEndOfTheNextTurn() {
   var holder = F("되새김질");
   holder.Conditions.PrevTurnFacts = new List<TriggerFact> { new TriggerFact { Kind = "소지품 소실", Method = "소모", Self = true, Item = "치리열매" } };
   var t = Engine.Resolve(TestData.Db, new[] { holder, F() }, new[] { Idle(), Idle() }, new Rules(), () => .5);
   Assert.That(t.Fighters[0].Stages[1], Is.EqualTo(1), string.Join("\n", t.Log));
   Assert.That(t.Fighters[0].Data.item, Is.Empty, "다시 먹은 열매는 사라진다");
   var none = F("되새김질");
   var t2 = Engine.Resolve(TestData.Db, new[] { none, F() }, new[] { Idle(), Idle() }, new Rules(), () => .5);
   Assert.That(t2.Fighters[0].Stages[1], Is.EqualTo(0), "지난 T에 쓰지 않았으면 해당 없음");
  }
  [Test] public void RipenDoublesBerryEffectsAndOnlyWhenTheHolderHasIt() {
   Assert.That(AbilityItemModules.Ripen("자신의 체력이 1/2 이하가 되었을 때, 자신의 체력을 최대 HP의 1/4 회복한다."), Does.Contain("최대 HP의 1/2 회복"));
   Assert.That(AbilityItemModules.Ripen("자신의 체력이 1/4 이하가 되었을 때, 자신의 「공격」이 오른다."), Does.Contain("크게 오른다"));
   Assert.That(AbilityItemModules.Ripen("자신의 체력이 1/4 이하가 되었을 때, 자신의 「공격」이 크게 오른다."), Does.Contain("매우 크게 오른다"));
   Assert.That(AbilityItemModules.Ripen("상대의 「불꽃」 기술을 받았을 때, 자신이 받는 「불꽃」 기술의 대미지를 약화(0.5배)한다."), Does.Contain("약화(0.25배)"));
   var ripe = F("숙성", "노말", 0, "치리열매"); var plain = F("", "노말", 0, "치리열매");
   Func<Fighter, string> text = f => string.Join(" ", f.Data.potentials.Where(p => p.id == ItemRuntime.Slot).Select(p => p.description));
   Assert.That(text(ripe), Does.Contain("크게"), text(ripe));
   Assert.That(text(plain), Does.Not.Contain("크게"), text(plain));
   // 특성을 잃으면 보통 효과로 돌아온다
   ripe.CurrentAbility = ""; ripe.AbilityChanged = true; AbilityModules.Sync(ripe);
   Assert.That(text(ripe), Does.Not.Contain("크게"));
  }
  [Test] public void ForcedUseDoesNothingWithoutABerry() {
   var glutton = F("먹보", "노말", 0, "구애스카프"); var foe = F(); var rules = new Rules();
   int max = Mechanics.MaxHP(glutton, foe, rules);
   using (Scope(new[] { glutton, foe }, rules)) {
    Mechanics.Hurt(glutton, foe, rules, max * 3 / 4, "검사", new List<string>());
    Assert.That(glutton.Data.item, Is.EqualTo("구애스카프"));
   }
  }

  // ----- 플레이트 타입 -----
  [Test] public void MultitypeFollowsThePlateAndRestoresWithoutIt() {
   var holder = F("멀티타입", "노말", 0, "불구슬플레이트");
   Assert.That(holder.Data.types, Is.EqualTo(new[] { "불꽃" }));
   holder.Data.item = "물방울플레이트"; ItemRuntime.Sync(holder);
   Assert.That(holder.Data.types, Is.EqualTo(new[] { "물" }));
   holder.Data.item = ""; ItemRuntime.Sync(holder);
   Assert.That(holder.Data.types, Is.EqualTo(new[] { "노말" }));
   var other = F("", "노말", 0, "불구슬플레이트");
   Assert.That(other.Data.types, Is.EqualTo(new[] { "노말" }), "특성이 없으면 변하지 않는다");
   holder.Data.item = "불구슬플레이트"; ItemRuntime.Sync(holder);
   holder.CurrentAbility = ""; holder.AbilityChanged = true; AbilityModules.Sync(holder);
   Assert.That(holder.Data.types, Is.EqualTo(new[] { "노말" }), "특성이 바뀌면 원래 타입");
  }

  // ----- 종족치(장소에 나와 있는 동안) -----
  [TestCase("라이트메탈", 5)] [TestCase("헤비메탈", 2)] [TestCase("천하장사", 1)] [TestCase("퍼코트", 2)]
  public void SpeciesAbilitiesFixTheStatToTheGradeAndRestoreOnLeaving(string ability, int stat) {
   var holder = F(ability);
   var t = Engine.Resolve(TestData.Db, new[] { holder, F() }, new[] { Idle(), Idle() }, new Rules(), () => .5);
   Assert.That(t.Fighters[0].Data.@base[stat], Is.InRange(165, 169), string.Join("\n", t.Log));
   Mechanics.LeaveField(t.Fighters[0]);
   Assert.That(t.Fighters[0].Data.@base[stat], Is.EqualTo(100));
  }
  [Test] public void SteelAbilitiesAddTheSteelType() {
   Assert.That(F("라이트메탈").Data.types, Does.Contain("강철"));
   Assert.That(F("헤비메탈").Data.types, Does.Contain("강철"));
  }
  [Test] public void DualBladeRaisesTheLowerAttackingSpeciesStatToTheHigher() {
   var holder = F("이도류"); holder.Data.@base = new[] { 100, 140, 100, 80, 100, 100 };
   var t = Engine.Resolve(TestData.Db, new[] { holder, F() }, new[] { Idle(), Idle() }, new Rules(), () => .5);
   Assert.That(t.Fighters[0].Data.@base[3], Is.EqualTo(140), string.Join("\n", t.Log));
   Assert.That(t.Fighters[0].Data.@base[1], Is.EqualTo(140));
   var reverse = F("이도류"); reverse.Data.@base = new[] { 100, 80, 100, 130, 100, 100 };
   var t2 = Engine.Resolve(TestData.Db, new[] { reverse, F() }, new[] { Idle(), Idle() }, new Rules(), () => .5);
   Assert.That(t2.Fighters[0].Data.@base[1], Is.EqualTo(130));
   Assert.That(t2.Fighters[0].Data.@base[3], Is.EqualTo(130));
  }
  [Test] public void GulpMissileBitesThenSpitsWhenHit() {
   var holder = F("그대로꿀꺽미사일"); var foe = F();
   var dive = M("몸통박치기").Copy(); dive.name = "다이빙";
   var t = Engine.Resolve(TestData.Db, new[] { holder, foe }, new[] { dive, Idle() }, new Rules(), () => .5);
   Assert.That(t.Fighters[0].Conditions.Gulping, Is.True, string.Join("\n", t.Log));
   int foeHp = t.Fighters[1].HP;
   var t2 = Engine.Resolve(TestData.Db, t.Fighters, new[] { new BattleCommand { Move = Idle() }, new BattleCommand { Move = M("몸통박치기") } }, t.Rules, t.Trainers, () => .5);
   Assert.That(t2.Fighters[0].Conditions.Gulping, Is.False, string.Join("\n", t2.Log));
   Assert.That(t2.Fighters[1].HP, Is.LessThan(foeHp), "먹이를 뱉어 대미지를 준다");
  }
 }
}
