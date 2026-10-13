using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using NUnit.Framework;

namespace AABattle {
 [TestFixture] public class AbilityItemModuleTests {
  static int ids;
  static Fighter F(string ability = "", string item = "") {
   return new Fighter(new Pokemon { id = "itemability" + ++ids, name = "검사" + ids, ability = ability, item = item,
    level = 100, types = new[] { "노말" }, @base = new[] { 100, 100, 100, 100, 100, 100 },
    iv = new[] { 31, 31, 31, 31, 31, 31 }, moves = new[] { "몸통박치기" }, potentials = new Potential[0] });
  }
  static Move Idle() { return new Move { name = "미등록", category = "변화", types = new[] { "노말" }, target = "자신", accuracy = 100, tags = new string[0] }; }
  static void Rule(Fighter f, string text) {
   string condition, effect; PotentialLibrary.Split(text, out condition, out effect);
   f.Data.potentials = new[] { new Potential { id = "종족 ①", name = "공용", description = text,
    triggers = condition.Length > 0 ? new string[0] : new[] { TriggerStructures.AlwaysLabel } } };
  }
  [TestCase("", "먹다남은음식")]
  [TestCase("기합의머리띠", "기합의머리띠")]
  public void TheftUsesEmptyHandOrDestruction(string held, string expected) {
   var a = F("매지션", held); var b = F("", "먹다남은음식");
   var move = TestData.Db.moves.First(m => m.name == "몸통박치기").Copy(); move.priority = 2;
   var turn = Engine.Resolve(TestData.Db, new[] { a, b }, new[] { move, Idle() }, new Rules(), () => .5);
   Assert.That(turn.Fighters[0].Data.item, Is.EqualTo(expected));
   Assert.That(turn.Fighters[1].Data.item, Is.Empty);
   Assert.That(a.Data.item, Is.EqualTo(held), "입력 데이터 보존");
  }
  [TestCase("소비", 2)] [TestCase("부서졌다", 2)] [TestCase("약탈", 1)]
  public void ItemLossEventSupportsGenericPotential(string reason, double factor) {
   var a = F("", "먹다남은음식"); var b = F();
   Rule(a, "자신의 소지품이 소모(파괴)됐을 때, 자신의 「속도」를 강화(2배)한다.");
   using (EffectScope.Begin(new[] { a, b }, new Rules(), () => .5)) ItemRuntime.Lose(a, reason, _ => { });
   Assert.That(PotentialModifiers.Stat(a,b,5,new Rules()), Is.EqualTo(factor));
  }
  [Test] public void UnburdenConsumesAndResetsOnLeavingField() {
   var a = F("곡예", "먹다남은음식"); var b = F(); var r = new Rules();
   using (EffectScope.Begin(new[] { a, b }, r, () => .5)) ItemRuntime.Lose(a, "소비", _ => { });
   Assert.That(Engine.Effective(a, b, 5, r), Is.EqualTo(Engine.Effective(b, a, 5, r) * 2));
   Mechanics.LeaveField(a);
   Assert.That(PotentialModifiers.Stat(a,b,5,r), Is.EqualTo(1));
  }
  [TestCase("서투름", "먹다남은음식", false)]
  [TestCase("서투름", "생명의구슬", false)]
  [TestCase("긴장감", "리샘열매", true)]
  public void BlockedItemsKeepPossessionAndSuppressEffects(string ability, string item, bool opposing) {
   var a = F(opposing ? "" : ability, item); var b = F(opposing ? ability : ""); var r = new Rules();
   using (EffectScope.Begin(new[] { a, b }, r, () => .5)) {
    AbilityRuntime.RefreshSuppression(new[] { a, b }, _ => { });
    Assert.That(ItemRuntime.Name(a), Is.Empty);
    Assert.That(a.Data.item, Is.EqualTo(item));
    Assert.That(a.Data.potentials.Any(p => p.id == ItemRuntime.Slot), Is.False);
   }
  }
  [Test] public void UnnerveAllowsNonConsumableAndRestoresAfterExit() {
   var a = F("", "먹다남은음식"); var b = F("긴장감");
   using (EffectScope.Begin(new[] { a, b }, new Rules(), () => .5)) {
    AbilityRuntime.RefreshSuppression(new[] { a, b }, _ => { });
    Assert.That(ItemRuntime.Name(a), Is.EqualTo("먹다남은음식"));
    a.Data.item = "리샘열매"; ItemRuntime.Sync(a);
    b.OnField = false; ItemRuntime.Sync(a);
    Assert.That(ItemRuntime.Name(a), Is.EqualTo("리샘열매"));
   }
  }
  [Test] public void KlutzActuallyBlocksTurnEndHealingAndGasRestoresIt() {
   var a = F("서투름", "먹다남은음식"); a.HP -= 100;
   var turn = Engine.Resolve(TestData.Db, new[] { a, F() }, new[] { Idle(), Idle() }, new Rules(), () => .5);
   Assert.That(turn.Fighters[0].HP, Is.EqualTo(a.HP));
   var gas = Engine.Resolve(TestData.Db, new[] { a, F("화학변화가스") }, new[] { Idle(), Idle() }, new Rules(), () => .5);
   Assert.That(gas.Fighters[0].HP, Is.GreaterThan(a.HP));
  }
  [Test, Category("UI"), Apartment(ApartmentState.STA)] public void ItemLossTriggerEditorRoundTrips() {
   var type = typeof(DataEditorForm); var flags = BindingFlags.Static | BindingFlags.NonPublic;
   var kinds = (string[])type.GetMethod("TriggerKinds", flags).Invoke(null, null);
   Assert.That(kinds, Does.Contain("소지품 소실"));
   var variants = (string[])type.GetMethod("TriggerVariants", flags).Invoke(null, new object[] { "소지품 소실", "자신" });
   Assert.That(variants, Does.Contain("소모 또는 파괴"));
   var spec = TriggerModules.Parse("상대의 소지품이 소모됐을 때");
   Assert.That(spec.Module, Is.EqualTo("소지품 소실"));
   Assert.That(TriggerModules.Canonical(spec), Is.EqualTo("상대의 소지품이 소모됐을 때"));
  }
 }
}
