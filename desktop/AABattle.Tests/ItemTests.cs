using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle {
 // 소지품: 「아바돈_소지품」 기준(사용자 지정) + 오레마스 시스템
 [TestFixture] public class ItemTests {
  static int ids;
  static Fighter F(string item = "", string type = "노말", params string[] moves) {
   return new Fighter(new Pokemon {
    id = "pk-i" + (++ids), name = "I" + ids, level = 100, types = new[] { type }, ability = "", item = item,
    @base = new[] { 100, 100, 100, 100, 100, 100 }, iv = new[] { 31, 31, 31, 31, 31, 31 },
    moves = moves.Length > 0 ? moves : new[] { "몸통박치기" }, potentials = new Potential[0]
   });
  }
  static Move M(string name) { return TestData.Db.moves.First(x => x.name == name); }
  static Move Idle() { return new Move { name = "미등록", category = "변화", types = new[] { "노말" }, tags = new string[0], accuracy = 100, target = "자신" }; }
  static int Max(Fighter a, Fighter b, Move m) { return Engine.Calculate(TestData.Db, a, b, m, new Rules()).Max; }
  static TurnResult Turn(Fighter a, Fighter b, Move ma, Move mb, Rules r = null) { return Engine.Resolve(TestData.Db, new[] { a, b }, new[] { ma, mb }, r ?? new Rules(), () => .5); }

  [Test] public void CatalogReadsDocumentAndTypedNames() {
   Assert.That(ItemCatalog.All.Length, Is.GreaterThan(100));
   Assert.That(ItemCatalog.Find("불꽃플레이트").Effect, Does.Contain("「불꽃」"));
   Assert.That(ItemCatalog.Find("불구슬플레이트").Type, Is.EqualTo("불꽃"));
   Assert.That(ItemCatalog.Find("물주얼").Effect, Does.Contain("「물」"));
   Assert.That(ItemCatalog.Find("철구").Name, Is.EqualTo("검은철구"));
   Assert.That(ItemCatalog.Find("탈출팩"), Is.Not.Null, "(8세대) 표기는 이름에서 뗀다");
  }

  [Test] public void MoveListCanExceedFour() {
   var f = F("", "노말", "몸통박치기", "할퀴기", "전광석화", "방어", "칼춤", "지진");
   Assert.That(f.Moves.Length, Is.EqualTo(6));
   Assert.That(f.Copy().Moves.Length, Is.EqualTo(6));
  }

  [Test] public void TypeBoostItemRunsAsPotential() {
   Assert.That(Max(F("목탄"), F(), M("화염방사")), Is.GreaterThan(Max(F(), F(), M("화염방사")) * 1.15));
   Assert.That(Max(F("목탄"), F(), M("물대포")), Is.EqualTo(Max(F(), F(), M("물대포"))));
  }

  // 「1번만」: 발동하면 소지품이 사라진다
  [Test] public void SitrusBerryHealsOnceAndIsConsumed() {
   var holder = F("자뭉열매"); holder.HP = Mechanics.MaxHP(holder, F(), new Rules()) / 2 + 3;
   var turn = Turn(holder, F(), Idle(), M("몸통박치기"));
   Assert.That(turn.Fighters[0].Data.item, Is.Empty, string.Join("\n", turn.Log));
   Assert.That(turn.Fighters[0].LostItem, Is.EqualTo("자뭉열매"));
   Assert.That(turn.Fighters[0].HP, Is.GreaterThan(Mechanics.MaxHP(holder, F(), new Rules()) / 2));
  }

  [Test] public void ResistBerryHalvesOnceThenIsConsumed() {
   var attacker = F(); var plain = F("", "풀"); var berry = F("오카열매", "풀");
   int normal = Max(attacker, plain, M("화염방사"));
   Assert.That(Max(attacker, berry, M("화염방사")), Is.InRange(normal / 2 - 2, normal / 2 + 2));
   Assert.That(Max(attacker, F("오카열매"), M("화염방사")), Is.EqualTo(Max(attacker, F(), M("화염방사"))), "not super effective");
   var turn = Turn(attacker, berry, M("화염방사"), Idle());
   Assert.That(turn.Fighters[1].Data.item, Is.Empty);
   Assert.That(turn.Log.Any(x => x.Contains("대미지 반감")), Is.True);
  }

  [Test] public void LifeOrbBoostsAndRecoils() {
   Assert.That(Max(F("생명의구슬"), F(), M("몸통박치기")), Is.GreaterThan(Max(F(), F(), M("몸통박치기")) * 1.25));
   var orb = F("생명의구슬"); int max = Mechanics.MaxHP(orb, F(), new Rules());
   var turn = Turn(orb, F(), M("몸통박치기"), Idle());
   Assert.That(turn.Fighters[0].HP, Is.EqualTo(max - max / 10));
  }

  // 구애: 1.5배, 장소에 나와 처음 내보낸 기술만, 구애 중에는 기능확장 불가(오레마스 시스템)
  [Test] public void ChoiceLocksFirstMoveAndBlocksExtension() {
   var holder = F("구애머리띠", "노말", "몸통박치기", "할퀴기");
   Assert.That(Engine.Effective(holder, F(), 1, new Rules()), Is.EqualTo((int)Math.Floor(Engine.Effective(F(), F(), 1, new Rules()) * 1.5)));
   var turn = Turn(holder, F(), M("몸통박치기"), Idle());
   var after = turn.Fighters[0];
   Assert.That(Mechanics.MoveBlock(after, M("할퀴기")), Does.Contain("구애"));
   Assert.That(Mechanics.MoveBlock(after, M("몸통박치기")), Is.Empty);
   Assert.That(FunctionExtensions.Available(after, F(), new Rules(), null, null), Is.Empty);
   Mechanics.LeaveField(after);
   Assert.That(Mechanics.MoveBlock(after, M("할퀴기")), Is.Empty, "leaving the field clears the lock");
  }

  [Test] public void RockyHelmetDamagesContactAttacker() {
   var attacker = F(); int max = Mechanics.MaxHP(attacker, F(), new Rules());
   var turn = Turn(attacker, F("울퉁불퉁멧"), M("몸통박치기"), Idle());
   Assert.That(turn.Fighters[0].HP, Is.EqualTo(max - max / 8), string.Join("\n", turn.Log));
  }

  [Test] public void LumBerryCuresStatusOnce() {
   var holder = F("리샘열매");
   var turn = Turn(holder, F(), Idle(), M("도깨비불"));
   Assert.That(turn.Fighters[0].Status, Is.EqualTo("정상"), string.Join("\n", turn.Log));
   Assert.That(turn.Fighters[0].Data.item, Is.Empty);
  }

  [Test] public void SpongeNullifiesWaterAndBreaksOnOtherAttack() {
   Assert.That(Mechanics.Block(TestData.Db, F(), F("스펀지"), M("물대포"), new Rules()), Does.Contain("스펀지"));
   var turn = Turn(F(), F("스펀지"), M("몸통박치기"), Idle());
   Assert.That(turn.Fighters[1].Data.item, Is.Empty);
  }

  [Test] public void WeatherRockExtendsTurns() {
   var rules = new Rules();
   Mechanics.SetWeather(F("뜨거운바위"), F(), "쾌청", 5, rules, new List<string>(), "검사");
   Assert.That(rules.WeatherTurns, Is.EqualTo(8));
  }

  // 기술머신(X): 배틀 중 한 번 X를 기능확장으로 내보낸다
  [Test] public void TechnicalMachineIsOneExtension() {
   var holder = F("기술머신(전광석화)");
   Assert.That(FunctionExtensions.Available(holder, F(), new Rules(), null, null), Is.EqualTo(new[] { "전광석화" }));
   var turn = Engine.Resolve(TestData.Db, new[] { holder, F() }, new[] { new BattleCommand { Move = M("전광석화"), Extension = true }, new BattleCommand { Move = Idle() } }, new Rules(), null, () => .5);
   Assert.That(turn.Fighters[0].Data.item, Is.Empty);
  }
 }
}
