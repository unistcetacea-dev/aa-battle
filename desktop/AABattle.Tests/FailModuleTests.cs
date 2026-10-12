using System;
using System.Linq;
using NUnit.Framework;

namespace AABattle {
 // 실패 조건 모듈: 기술 문장의 「…아니면 실패한다」를 내기 직전에 판정한다
 [TestFixture] public class FailModuleTests {
  static int ids;
  static Fighter F(string type = "노말") {
   return new Fighter(new Pokemon {
    id = "pk-fm" + (++ids), name = "X" + ids, level = 100, types = new[] { type }, ability = "", item = "",
    @base = new[] { 100, 100, 100, 100, 100, 100 }, iv = new[] { 31, 31, 31, 31, 31, 31 },
    moves = new[] { "몸통박치기" }, potentials = new Potential[0]
   });
  }
  static Move M(string name) { return TestData.Db.moves.First(x => x.name == name); }
  static Move Idle() { return new Move { name = "미등록", category = "변화", types = new[] { "노말" }, tags = new string[0], accuracy = 100, target = "자신" }; }
  static TurnResult Turn(Fighter a, Fighter b, Move ma, Move mb) { return Engine.Resolve(TestData.Db, new[] { a, b }, new[] { ma, mb }, new Rules(), () => .5); }
  static int Full(TurnResult t, int i) { return Mechanics.MaxHP(t.Fighters[i], t.Fighters[1 - i], new Rules()); }

  [TestCase("상대가 「폭탄」「분진」 상태가 아니면 실패한다")]
  [TestCase("「화상」 상태일 때 내보내면 실패한다")]
  [TestCase("상대가 「벌레」 타입이 아니면 실패한다")]
  [TestCase("날씨가 「싸라기눈」이 아니면 실패한다")]
  [TestCase("필드에 나온 후 2T째가 아니면 실패한다")]
  [TestCase("기술을 내보내기 전에 상대의 「물리」 기술을 받지 않으면 실패한다")]
  [TestCase("상대가 공격기술을 내보내지 않거나，상대보다 나중에 내보내면 실패한다")]
  [TestCase("자신과 상대 모두에게 「소지품」이 없다면 실패한다")]
  public void FailSentencesParse(string sentence) { Assert.That(FailModules.IsModule(sentence.Replace('，', ',')), Is.True); }

  // 기습: 상대가 공격기술을 고르지 않으면 실패
  [Test] public void SuckerPunchNeedsAnAttack() {
   var t = Turn(F(), F(), M("기습"), Idle());
   Assert.That(t.Fighters[1].HP, Is.EqualTo(Full(t, 1)));
   var hit = Turn(F(), F(), M("기습"), M("몸통박치기"));
   Assert.That(hit.Fighters[1].HP, Is.LessThan(Full(hit, 1)));
  }

  // 힘껏펀치: 내보내기 전에 공격을 받으면 실패
  [Test] public void FocusPunchFailsWhenHitFirst() {
   var t = Turn(F(), F(), M("힘껏펀치"), M("몸통박치기"));
   Assert.That(t.Fighters[1].HP, Is.EqualTo(Full(t, 1)), string.Join("\n", t.Log));
   var calm = Turn(F(), F(), M("힘껏펀치"), Idle());
   Assert.That(calm.Fighters[1].HP, Is.LessThan(Full(calm, 1)));
  }

  [TestCase("불사르기", "노말")]
  [TestCase("조여짜기", "노말")]
  [TestCase("철갑탄", "노말")]
  public void ConditionMovesFail(string move, string type) {
   var t = Turn(F(type), F(), M(move), Idle());
   Assert.That(t.Fighters[1].HP, Is.EqualTo(Full(t, 1)), string.Join("\n", t.Log));
  }

  [Test] public void BurnUpWorksForFireType() {
   var t = Turn(F("불꽃"), F(), M("불사르기"), Idle());
   Assert.That(t.Fighters[1].HP, Is.LessThan(Full(t, 1)));
  }
 }
}
