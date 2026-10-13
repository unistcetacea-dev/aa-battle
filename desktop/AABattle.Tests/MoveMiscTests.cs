using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle {
 // 아픔나누기 · 소란피기(상태 소란) · 참기 · 날씨별 회복/랭크(광합성류·성장) · 날씨 조건 방어 관통(마그마어퍼)
 [TestFixture] public class MoveMiscTests {
  static int ids;
  static Fighter F(string type = "노말") {
   return new Fighter(new Pokemon {
    id = "pk-mm" + (++ids), name = "M" + ids, level = 100, types = new[] { type }, ability = "", item = "",
    @base = new[] { 100, 100, 100, 100, 100, 100 }, iv = new[] { 31, 31, 31, 31, 31, 31 },
    moves = new[] { "몸통박치기" }, potentials = new Potential[0]
   });
  }
  static Move M(string name) { return TestData.Db.moves.First(x => x.name == name); }
  static Move Idle() { return new Move { name = "미등록", category = "변화", types = new[] { "노말" }, tags = new string[0], accuracy = 100, target = "자신" }; }
  static int Max(Fighter f) { return Mechanics.MaxHP(f, f, new Rules()); }
  static bool Use(Fighter a, Fighter b, string move, Rules r = null) {
   return MoveRuntime.Status(a, b, M(move), r ?? new Rules(), () => .5, new List<string>(), 0);
  }

  // 현재 HP를 더해 똑같이 나눈다(홀수는 1 버림)
  [TestCase(300, 100, 200)]
  [TestCase(301, 100, 200)]
  [TestCase(10, 330, 170)]
  public void PainSplitSharesTotalHp(int mine, int theirs, int each) {
   var a = F(); var b = F();
   a.HP = mine; b.HP = theirs;
   Assert.That(Use(a, b, "아픔나누기"), Is.True);
   Assert.That(a.HP, Is.EqualTo(each));
   Assert.That(b.HP, Is.EqualTo(each));
  }

  [Test] public void PainSplitPiercesProtectSubstituteAndMagicCoat() {
   var move = M("아픔나누기");
   Assert.That(MoveMiscModules.PiercesState(move, "대타출동"), Is.True);
   Assert.That(MoveMiscModules.PiercesState(move, "매직코트"), Is.True);
   Assert.That(Mechanics.BypassSub(F(), move), Is.True);
   var a = F(); var b = F(); b.Conditions.MagicCoat = true;
   Assert.That(Mechanics.Block(TestData.Db, a, b, move, new Rules()), Is.Not.EqualTo(StateTicks.Reflected));
  }

  // 광합성·아침햇살·달빛: 쾌청 2/3, 악천후 1/4, 날씨 없음 1/2
  [TestCase("광합성", "쾌청", 2, 3)]
  [TestCase("아침햇살", "큰가뭄", 2, 3)]
  [TestCase("달빛", "비", 1, 4)]
  [TestCase("광합성", "싸라기눈", 1, 4)]
  [TestCase("광합성", "모래바람", 1, 4)]
  [TestCase("달빛", "없음", 1, 2)]
  public void WeatherDecidesHealAmount(string move, string weather, int n, int d) {
   var a = F(); var b = F();
   int max = Max(a);
   a.HP = 1;
   var log = new List<string>();
   var plan = MoveRuntime.Plan(M(move));
   Assert.That(MoveRuntime.Status(a, b, M(move), new Rules { Weather = weather }, () => .5, log, 0), Is.True, string.Join(" | ", plan.Use) + " ## " + string.Join(" | ", plan.Unknown) + " ## " + string.Join(" | ", log));
   Assert.That(a.HP, Is.EqualTo(1 + (int)Math.Floor(max * (double)n / d)));
  }

  [TestCase("없음", 1)]
  [TestCase("비", 1)]
  [TestCase("쾌청", 2)]
  public void GrowthRaisesMoreInSun(string weather, int steps) {
   var a = F(); var b = F();
   Assert.That(Use(a, b, "성장", new Rules { Weather = weather }), Is.True);
   Assert.That(a.Stages[1], Is.EqualTo(steps));
   Assert.That(a.Stages[3], Is.EqualTo(steps));
  }

  // 마그마어퍼: 쾌청일 때만 「방어」를 관통한다(태그만으로는 관통하지 않는다)
  [Test] public void WeatherConditionalProtectPierce() {
   var move = M("마그마어퍼");
   Assert.That(MoveModules.PiercesProtect(move), Is.False);
   Assert.That(MoveMiscModules.WeatherPierces(F(), F(), move, new Rules { Weather = "쾌청" }), Is.True);
   Assert.That(MoveMiscModules.WeatherPierces(F(), F(), move, new Rules { Weather = "비" }), Is.False);
   var b = F(); b.Conditions.Protect = true;
   Assert.That(Mechanics.Block(TestData.Db, F(), b, move, new Rules { Weather = "쾌청" }), Is.Empty);
   Assert.That(Mechanics.Block(TestData.Db, F(), b, move, new Rules()), Is.Not.Empty);
  }

  // 소란: 걸린 동안 양쪽 모두 잠들 수 없고 잠든 포켓몬은 깬다. 지속이 끝나면 풀린다
  [Test] public void UproarWakesBlocksSleepAndEnds() {
   var a = F(); var b = F("고스트");   // 노말 기술이 통하지 않아 쓰러지지 않는다
   b.Status = "잠듦";
   var t = Engine.Resolve(TestData.Db, new[] { a, b }, new[] { M("소란피기"), Idle() }, new Rules(), () => .5);
   Assert.That(t.Fighters[0].Conditions.UproarTurns, Is.EqualTo(3), "4T에서 첫 행동 1회");
   Assert.That(t.Fighters[1].Status, Is.EqualTo("정상"), string.Join("\n", t.Log));
   Assert.That(Mechanics.ApplyStatus(t.Fighters[1], t.Fighters[0], "잠듦", t.Rules, () => .5, new List<string>()), Is.False);
   Assert.That(Mechanics.ApplyStatus(t.Fighters[0], t.Fighters[1], "잠듦", t.Rules, () => .5, new List<string>()), Is.False);
   for (int i = 0; i < 3; i++)
    t = Engine.Resolve(TestData.Db, t.Fighters, new[] { Idle(), Idle() }, t.Rules, () => .5);
   Assert.That(t.Fighters[0].Conditions.UproarTurns, Is.EqualTo(0));
   Assert.That(t.Fighters[0].Conditions.UproarMove, Is.Empty);
   Assert.That(t.Fighters[0].Conditions.Confusion, Is.EqualTo(0), "소란은 끝나도 혼란이 없다");
   t = Engine.Resolve(TestData.Db, t.Fighters, new[] { M("수면가루"), Idle() }, t.Rules, () => .5);
   Assert.That(t.Fighters[1].Status, Is.EqualTo("잠듦"), string.Join(" / ", t.Log));
  }

  [Test] public void UproarStateByEffectSentenceAndReapply() {
   var a = F(); var b = F(); var rules = new Rules(); var log = new List<string>();
   Mechanics.SetParties(rules, new[] { a, b });
   Assert.That(StateEffects.Apply("자신을 3T간 「소란」상태로 한다", a, b, rules, () => .5, log, "검사"), Is.EqualTo(EffectOutcome.Success));
   Assert.That(a.Conditions.UproarTurns, Is.EqualTo(3));
   Assert.That(StateEffects.Apply("자신을 5T간 「소란」상태로 한다", a, b, rules, () => .5, log, "검사"), Is.EqualTo(EffectOutcome.Failed));
   Assert.That(a.Conditions.UproarTurns, Is.EqualTo(3));
  }

  // 참기: 2T 동안 받은 대미지를 모아 2배의 고정 대미지로 돌려준다
  [Test] public void BideStoresDamageAndReleasesDouble() {
   var a = F(); var b = F();
   var tackle = M("몸통박치기");
   var t = Engine.Resolve(TestData.Db, new[] { a, b }, new[] { M("참기"), tackle }, new Rules(), () => .5);
   int hp0 = Max(a);
   Assert.That(t.Fighters[0].Conditions.BideMove, Is.EqualTo("참기"));
   Assert.That(t.Fighters[1].HP, Is.EqualTo(Max(b)), "모으는 동안은 공격하지 않는다");
   t = Engine.Resolve(TestData.Db, t.Fighters, new[] { Idle(), tackle }, t.Rules, () => .5);
   int stored = t.Fighters[0].Conditions.BideStored;
   Assert.That(stored, Is.EqualTo(hp0 - t.Fighters[0].HP).And.GreaterThan(0), string.Join("\n", t.Log));
   int foeBefore = t.Fighters[1].HP;
   t = Engine.Resolve(TestData.Db, t.Fighters, new[] { Idle(), Idle() }, t.Rules, () => .5);
   Assert.That(foeBefore - t.Fighters[1].HP, Is.EqualTo(stored * 2), string.Join("\n", t.Log));
   Assert.That(t.Fighters[0].Conditions.BideMove, Is.Empty);
  }

  [Test] public void BideFailsWithoutDamage() {
   var t = Engine.Resolve(TestData.Db, new[] { F(), F() }, new[] { M("참기"), Idle() }, new Rules(), () => .5);
   for (int i = 0; i < 2; i++)
    t = Engine.Resolve(TestData.Db, t.Fighters, new[] { Idle(), Idle() }, t.Rules, () => .5);
   Assert.That(t.Fighters[1].HP, Is.EqualTo(Max(t.Fighters[1])));
   Assert.That(t.Log.Any(x => x.Contains("받은 대미지가 없다")), Is.True, string.Join("\n", t.Log));
   Assert.That(t.Fighters[0].Conditions.BideMove, Is.Empty);
  }
 }
}
