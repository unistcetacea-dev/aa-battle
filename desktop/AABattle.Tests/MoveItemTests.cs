using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle {
 // 소지품 기술 묶음: 빼앗기·교환·떨구기·던지기·열매 기술
 [TestFixture] public class MoveItemTests {
  static int ids;
  static Fighter F(string item = "", string ability = "", string type = "노말") {
   return new Fighter(new Pokemon {
    id = "pk-mi" + (++ids), name = "I" + ids, level = 100, types = new[] { type }, ability = ability, item = item,
    @base = new[] { 100, 100, 100, 100, 100, 100 }, iv = new[] { 31, 31, 31, 31, 31, 31 },
    moves = new[] { "몸통박치기" }, potentials = new Potential[0]
   });
  }
  // 이름 뒤의 (1P) 같은 표기는 앞부분만 맞춰 찾는다
  static Move M(string name) { return TestData.Db.moves.First(x => x.name == name || x.name.StartsWith(name + "（")); }
  static Move Idle() { return new Move { name = "미등록", category = "변화", types = new[] { "노말" }, tags = new string[0], accuracy = 100, target = "자신" }; }
  static TurnResult Turn(Fighter a, Fighter b, Move ma, Move mb = null) { return Engine.Resolve(TestData.Db, new[] { a, b }, new[] { ma, mb ?? Idle() }, new Rules(), () => .5); }
  static string Log(TurnResult t) { return string.Join("\n", t.Log); }
  static string Run(Fighter a, Fighter b, Move m, string line, List<string> log = null) {
   var lines = log ?? new List<string>();
   var rules = new Rules();
   using (EffectScope.Begin(new[] { a, b }, rules, () => .5)) MoveRuntime.Run(a, b, m, line, rules, () => .5, lines);
   return string.Join("\n", lines);
  }

  // 기술 문장이 모두 모듈로 읽힌다(읽지 못한 문장이 없다)
  [TestCase("탁쳐서떨구기")] [TestCase("부식의나뭇잎")] [TestCase("도둑질")] [TestCase("탐내기")] [TestCase("트릭")] [TestCase("바꿔치기")]
  [TestCase("내던지기")] [TestCase("자연의은혜")] [TestCase("배의뭉치")] [TestCase("불태우기")] [TestCase("홍련완")] [TestCase("부식가스")]
  [TestCase("기프트패스")] [TestCase("리사이클")] [TestCase("다과회")] [TestCase("볼가득넣기")] [TestCase("쪼아대기")] [TestCase("벌레먹기")]
  [TestCase("애크러뱃")] [TestCase("폴터가이스트")] [TestCase("트림")] [TestCase("심판의뭉치")]
  public void SentencesAreFullyRead(string name) {
   Assert.That(MoveRuntime.Plan(M(name)).Unknown, Is.Empty);
  }

  // 빼앗기 두 갈래: 빈손이면 빼앗고, 소지품이 있으면 상대 것만 파괴
  [TestCase("도둑질", "", "먹다남은음식")]
  [TestCase("도둑질", "기합의머리띠", "기합의머리띠")]
  [TestCase("탐내기", "", "먹다남은음식")]
  public void StealTakesOnlyWithEmptyHands(string move, string held, string expected) {
   var a = F(held); var b = F("먹다남은음식");
   var t = Turn(a, b, M(move));
   Assert.That(t.Fighters[0].Data.item, Is.EqualTo(expected), Log(t));
   Assert.That(t.Fighters[1].Data.item, Is.Empty);
  }

  [Test] public void StealFailsAgainstStickyHoldAndSubstitute() {
   var t = Turn(F(), F("먹다남은음식", "점착"), M("도둑질"));
   Assert.That(t.Fighters[0].Data.item, Is.Empty);
   Assert.That(t.Fighters[1].Data.item, Is.EqualTo("먹다남은음식"));
   var b = F("먹다남은음식");
   b.Conditions.Substitute = 999;
   var s = Turn(F(), b, M("도둑질"));
   Assert.That(s.Fighters[0].Data.item, Is.Empty, Log(s));
   Assert.That(s.Fighters[1].Data.item, Is.EqualTo("먹다남은음식"));
  }

  // 떨구기: 소지품이 있으면 위력 1.5배, 쓴 뒤에는 상대 소지품이 사라진다. 점착이면 배율도 제거도 없다
  [TestCase("탁쳐서떨구기", "먹다남은음식", "", 1.5)]
  [TestCase("탁쳐서떨구기", "", "", 1.0)]
  [TestCase("탁쳐서떨구기", "먹다남은음식", "점착", 1.0)]
  [TestCase("부식의나뭇잎", "기합의머리띠", "", 1.5)]
  public void KnockOffBoostsPowerWhenItemCanBeDestroyed(string move, string item, string ability, double factor) {
   var a = F(); var b = F(item, ability);
   Assert.That(MoveModules.PowerFactor(a, b, M(move), new Rules()), Is.EqualTo(factor));
   var t = Turn(a, b, M(move));
   bool removed = item.Length > 0 && ability.Length == 0;
   Assert.That(t.Fighters[1].Data.item, Is.EqualTo(removed ? "" : item), Log(t));
  }

  // 「소지품 파괴」 기술 묶음은 소지품을 파괴한다
  [TestCase("홍련완")] [TestCase("검은벼락")] [TestCase("신의주먹")] [TestCase("패왕권")] [TestCase("황금의바람")] [TestCase("녹슨바람")] [TestCase("부식가스")]
  public void ItemDestroyingMoves(string move) {
   var t = Turn(F(), F("먹다남은음식"), M(move));
   Assert.That(t.Fighters[1].Data.item, Is.Empty, Log(t));
  }

  // 불태우기는 나무열매·주얼만 파괴한다
  [TestCase("오카열매", "")] [TestCase("불꽃주얼", "")] [TestCase("먹다남은음식", "먹다남은음식")]
  public void IncinerateBurnsOnlyBerriesAndJewels(string item, string left) {
   var t = Turn(F(), F(item), M("불태우기"));
   Assert.That(t.Fighters[1].Data.item, Is.EqualTo(left), Log(t));
  }

  [Test] public void AcrobaticsDoublesWithoutItem() {
   Assert.That(MoveModules.PowerFactor(F(), F(), M("애크러뱃"), new Rules()), Is.EqualTo(2));
   Assert.That(MoveModules.PowerFactor(F("먹다남은음식"), F(), M("애크러뱃"), new Rules()), Is.EqualTo(1));
  }

  // 교환: 양쪽이 바뀌고, 한쪽이 빈손이어도 되고, 둘 다 빈손이면 실패한다
  [TestCase("트릭", "먹다남은음식", "기합의머리띠", "기합의머리띠", "먹다남은음식")]
  [TestCase("트릭", "", "기합의머리띠", "기합의머리띠", "")]
  [TestCase("트릭", "먹다남은음식", "", "", "먹다남은음식")]
  [TestCase("트릭", "", "", "", "")]
  [TestCase("바꿔치기", "먹다남은음식", "기합의머리띠", "기합의머리띠", "먹다남은음식")]
  public void TrickSwapsItems(string move, string mine, string theirs, string mineAfter, string theirsAfter) {
   var t = Turn(F(mine), F(theirs), M(move));
   Assert.That(t.Fighters[0].Data.item, Is.EqualTo(mineAfter), Log(t));
   Assert.That(t.Fighters[1].Data.item, Is.EqualTo(theirsAfter));
  }

  [Test] public void TrickIsBlockedByStickyHoldAndSubstitute() {
   var sticky = Turn(F("먹다남은음식"), F("기합의머리띠", "점착"), M("트릭"));
   Assert.That(sticky.Fighters[0].Data.item, Is.EqualTo("먹다남은음식"), Log(sticky));
   var b = F("기합의머리띠");
   b.Conditions.Substitute = 999;
   var sub = Turn(F("먹다남은음식"), b, M("트릭"));
   Assert.That(sub.Fighters[0].Data.item, Is.EqualTo("먹다남은음식"), Log(sub));
  }

  // 자신에게 「소지품」이 없으면 실패: 바꿔치기·폴터가이스트(상대)·트림(열매를 먹은 후가 아니면)
  [Test] public void ItemRequirementsFail() {
   Assert.That(MoveModules.FailReason(F(), F("먹다남은음식"), M("바꿔치기")), Is.Not.Empty);
   Assert.That(MoveModules.FailReason(F("먹다남은음식"), F(), M("폴터가이스트")), Is.Not.Empty);
   Assert.That(MoveModules.FailReason(F(), F("먹다남은음식"), M("폴터가이스트")), Is.Empty);
   Assert.That(MoveModules.FailReason(F("먹다남은음식"), F(), M("기프트패스")), Is.Empty);
   Assert.That(MoveModules.FailReason(F("먹다남은음식"), F("기합의머리띠"), M("기프트패스")), Is.Not.Empty);
  }

  [Test] public void BestowGivesItemAway() {
   var t = Turn(F("먹다남은음식"), F(), M("기프트패스"));
   Assert.That(t.Fighters[0].Data.item, Is.Empty, Log(t));
   Assert.That(t.Fighters[1].Data.item, Is.EqualTo("먹다남은음식"));
  }

  // 내던지기: 위력은 소지품 표, 쓴 소지품은 사라지고, 효과가 상대에게 걸린다
  [TestCase("먹다남은음식", 10)] [TestCase("검은철구", 130)] [TestCase("불꽃플레이트", 90)] [TestCase("불꽃주얼", 30)] [TestCase("오카열매", 10)]
  public void FlingPowerComesFromTheItemTable(string item, int power) {
   var judged = Mechanics.JudgedMove(F(item), M("내던지기"), F(), new Rules());
   Assert.That(judged.power, Is.EqualTo(power));
  }

  [Test] public void FlingConsumesItemAndFailsWithoutOne() {
   var t = Turn(F("먹다남은음식"), F(), M("내던지기"));
   Assert.That(t.Fighters[0].Data.item, Is.Empty, Log(t));
   Assert.That(t.Fighters[1].HP, Is.LessThan(Engine.Stats(t.Fighters[1], 100, new Rules())[0]));
   var none = Turn(F(), F(), M("내던지기"));
   Assert.That(none.Fighters[1].HP, Is.EqualTo(Engine.Stats(none.Fighters[1], 100, new Rules())[0]), Log(none));
  }

  [TestCase("독바늘", "독")] [TestCase("화염구슬", "화상")]
  public void FlungItemAppliesItsEffect(string item, string status) {
   var a = F(item); var b = F();
   Run(a, b, M("내던지기"), MoveItemModules.Fling + ".");
   Assert.That(a.Data.item, Is.Empty);
   Assert.That(b.Status, Is.EqualTo(status));
  }

  [Test] public void FlungBerryIsEatenByTheTarget() {
   var a = F("오카열매"); var b = F("자뭉열매"); b.HP = 10;
   Run(a, b, M("내던지기"), MoveItemModules.Fling + ".");
   Assert.That(a.Data.item, Is.Empty);
   Assert.That(b.Data.item, Is.EqualTo("자뭉열매"), "상대의 소지품은 그대로");
  }

  // 자연의은혜·배의뭉치: 열매에 따라 타입·위력이 바뀌고, 열매를 소모하며, 열매가 없으면 실패한다
  [TestCase("자연의은혜", "오카열매", "불꽃", 80)] [TestCase("자연의은혜", "치리열매", "풀", 100)] [TestCase("배의뭉치", "자뭉열매", "에스퍼", 80)]
  public void NaturalGiftUsesTheBerryTable(string move, string berry, string type, int power) {
   var judged = Mechanics.JudgedMove(F(berry), M(move), F(), new Rules());
   Assert.That(judged.types, Is.EqualTo(new[] { type }));
   Assert.That(judged.power, Is.EqualTo(power));
   var t = Turn(F(berry), F(), M(move));
   Assert.That(t.Fighters[0].Data.item, Is.Empty, Log(t));
   Assert.That(t.Fighters[1].HP, Is.LessThan(Engine.Stats(t.Fighters[1], 100, new Rules())[0]));
  }

  [TestCase("자연의은혜", "")] [TestCase("자연의은혜", "먹다남은음식")] [TestCase("배의뭉치", "")]
  public void NaturalGiftFailsWithoutBerry(string move, string item) {
   Assert.That(MoveModules.FailReason(F(item), F(), M(move)), Is.Not.Empty);
   var t = Turn(F(item), F(), M(move));
   Assert.That(t.Fighters[1].HP, Is.EqualTo(Engine.Stats(t.Fighters[1], 100, new Rules())[0]), Log(t));
  }

  [TestCase("불꽃플레이트", "불꽃")] [TestCase("", "노말")] [TestCase("먹다남은음식", "노말")]
  public void JudgmentFollowsHeldPlate(string item, string type) {
   var judged = Mechanics.JudgedMove(F(item), M("심판의뭉치"), F(), new Rules());
   Assert.That(judged.types, Is.EqualTo(new[] { type }));
  }

  // 볼가득넣기: 열매를 먹은 뒤 방어 +2. 열매가 없으면 실패
  [Test] public void StuffCheeksEatsBerryThenRaisesDefense() {
   var t = Turn(F("자뭉열매"), F(), M("볼가득넣기"));
   Assert.That(t.Fighters[0].Data.item, Is.Empty, Log(t));
   Assert.That(t.Fighters[0].Stages[2], Is.EqualTo(2));
   var none = Turn(F(), F(), M("볼가득넣기"));
   Assert.That(none.Fighters[0].Stages[2], Is.EqualTo(0), Log(none));
  }

  [Test] public void TeatimeFeedsEveryoneOnTheField() {
   var t = Turn(F("자뭉열매"), F("리샘열매"), M("다과회"));
   Assert.That(t.Fighters[0].Data.item, Is.Empty, Log(t));
   Assert.That(t.Fighters[1].Data.item, Is.Empty);
  }

  // 쪼아대기·벌레먹기: 상대의 나무열매를 자신이 먹는다. 자신의 소지품은 그대로
  [TestCase("쪼아대기", "기합의머리띠")] [TestCase("벌레먹기", "")]
  public void PluckEatsTheOpposingBerry(string move, string own) {
   var t = Turn(F(own), F("자뭉열매"), M(move));
   Assert.That(t.Fighters[1].Data.item, Is.Empty, Log(t));
   Assert.That(t.Fighters[0].Data.item, Is.EqualTo(own));
   var keep = Turn(F(own), F("먹다남은음식"), M(move));
   Assert.That(keep.Fighters[1].Data.item, Is.EqualTo("먹다남은음식"), "열매가 아니면 먹지 않는다");
  }

  // 리사이클: 소모한 소지품을 되찾는다(빈손일 때). 파괴된 소지품은 되찾지 못한다
  [Test] public void RecycleReturnsOnlyConsumedItems() {
   var a = F("먹다남은음식"); var b = F();
   using (EffectScope.Begin(new[] { a, b }, new Rules(), () => .5)) ItemRuntime.Lose(a, "소비", _ => { });
   Assert.That(a.Data.item, Is.Empty);
   Run(a, b, M("리사이클"), MoveItemModules.RecycleAny + ".");
   Assert.That(a.Data.item, Is.EqualTo("먹다남은음식"));
   var c = F("먹다남은음식");
   using (EffectScope.Begin(new[] { c, b }, new Rules(), () => .5)) ItemRuntime.Lose(c, "파괴", _ => { });
   Run(c, b, M("리사이클"), MoveItemModules.RecycleAny + ".");
   Assert.That(c.Data.item, Is.Empty);
  }
 }
}
