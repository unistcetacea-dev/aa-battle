using System;
using System.Linq;
using NUnit.Framework;

namespace AABattle {
 // 기술 효과 모듈(드레인·조이기·모으기·숨기)과 그것을 쓰는 소지품
 [TestFixture] public class MoveModuleTests {
  static int ids;
  static Fighter F(string item = "", string type = "노말") {
   return new Fighter(new Pokemon {
    id = "pk-mm" + (++ids), name = "M" + ids, level = 100, types = new[] { type }, ability = "", item = item,
    @base = new[] { 100, 100, 100, 100, 100, 100 }, iv = new[] { 31, 31, 31, 31, 31, 31 },
    moves = new[] { "몸통박치기" }, potentials = new Potential[0]
   });
  }
  static Move M(string name) { return TestData.Db.moves.First(x => x.name == name); }
  static Move Idle() { return new Move { name = "미등록", category = "변화", types = new[] { "노말" }, tags = new string[0], accuracy = 100, target = "자신" }; }
  static TurnResult Turn(Fighter a, Fighter b, Move ma, Move mb, Rules r = null) { return Engine.Resolve(TestData.Db, new[] { a, b }, new[] { ma, mb }, r ?? new Rules(), () => .5); }
  static TurnResult Next(TurnResult t, Move ma, Move mb) { return Engine.Resolve(TestData.Db, t.Fighters, new[] { ma, mb }, t.Rules, () => .5); }
  static string Log(TurnResult t) { return string.Join("\n", t.Log); }

  [TestCase("체력을 흡수하는 기술의 회복량이 30% 증가한다.", "자신의 「드레인」 기술의 회복량을 1.3배로 한다.")]
  [TestCase("「조이기」상태에 의한 데미지가 「1/6」으로 증가한다.", "자신이 거는 「조이기」의 대미지를 최대 HP의 1/6로 한다.")]
  [TestCase("기술로 발생하는 「조이기」상태의 지속시간이 언제나 5T이 된다.", "자신이 거는 「조이기」의 지속 T를 5T로 한다.")]
  [TestCase("이전 턴과 같은 기술을 내보낸 횟수에 비례해서 기술의 위력이 강화된다. (1.1～2배)", "자신이 같은 기술을 연속으로 내보낼 때마다 그 기술의 위력을 0.1배씩 강화한다(최대 2배).")]
  [TestCase("자신의 「접촉기술」에 반응하는 효과를 받지 않는다.", "자신의 「접촉」 기술에 반응하는 효과를 받지 않는다.")]
  [TestCase("다음T 내보내는 기술의 우선도를 +1한다.", "다음 T 자신의 기술 우선도를 1 올린다.")]
  public void SourceSentencesNormalize(string raw, string canonical) {
   Assert.That(EffectModules.Normalize(raw), Is.EqualTo(canonical));
   Assert.That(EffectModules.Classify(canonical), Is.Not.Empty);
  }

  [TestCase("큰뿌리")] [TestCase("조임밴드")] [TestCase("끈기갈고리손톱")] [TestCase("메트로놈")] [TestCase("파워풀허브")]
  [TestCase("방호패드")] [TestCase("하양허브")] [TestCase("애슈열매")] [TestCase("끈적끈적바늘")]
  public void RemainingItemsRunAsModules(string item) { Assert.That(ItemRuntime.Status(ItemCatalog.Find(item)), Is.EqualTo("자동 구현")); }

  // 드레인: 준 대미지의 1/2, 큰뿌리는 1.3배
  [Test] public void DrainHealsFromDamageAndBigRootBoosts() {
   int Gain(string item) {
    var a = F(item); a.HP = 100; var b = F();
    var t = Turn(a, b, M("기가드레인"), Idle());
    int dealt = b.HP - t.Fighters[1].HP;
    Assert.That(dealt, Is.GreaterThan(0));
    return (t.Fighters[0].HP - 100) * 100 / dealt;
   }
   Assert.That(Gain(""), Is.InRange(48, 50));
   Assert.That(Gain("큰뿌리"), Is.InRange(63, 65));
  }

  [Test] public void DreamEaterFailsUnlessAsleep() {
   var t = Turn(F(), F(), M("꿈먹기"), Idle());
   Assert.That(t.Fighters[1].HP, Is.EqualTo(Mechanics.MaxHP(t.Fighters[1], t.Fighters[0], new Rules())));
   Assert.That(Log(t), Does.Contain("잠들어 있지 않아"));
  }

  // 조이기: 4～5T, 끈기갈고리손톱은 5T, 조임밴드는 턴 끝 대미지 1/6
  [Test] public void BindingMovesBindAndItemsChangeIt() {
   var t = Turn(F(), F(), M("김밥말이"), Idle());
   Assert.That(t.Fighters[1].Conditions.Bound, Is.InRange(3, 4), "4～5T, 이번 T 끝에 1 줄어든다\n" + Log(t));
   var claw = Turn(F("끈기갈고리손톱"), F(), M("가시창"), Idle());
   Assert.That(claw.Fighters[1].Conditions.Bound, Is.EqualTo(4), "언제나 5T(2T 기술도)");
   var band = Turn(F("조임밴드"), F(), M("김밥말이"), Idle());
   Assert.That(band.Fighters[1].Conditions.BindDivisor, Is.EqualTo(6));
   Assert.That(Log(band), Does.Contain("조이기: HP"));
  }

  // 모으기: 1T째 준비, 2T째 자동 공격. 쾌청이면 생략, 파워풀허브는 1번 생략하고 사라진다
  [Test] public void ChargeMovesTakeTwoTurns() {
   var b = F(); int full = Mechanics.MaxHP(b, F(), new Rules());
   var t1 = Turn(F(), b, M("솔라빔"), Idle());
   Assert.That(t1.Fighters[1].HP, Is.EqualTo(full));
   Assert.That(t1.Fighters[0].Conditions.ChargeMove, Is.EqualTo("솔라빔"));
   Assert.That(Mechanics.MoveBlock(t1.Fighters[0], M("몸통박치기")), Does.Contain("모으기"));
   var t2 = Next(t1, M("몸통박치기"), Idle());
   Assert.That(t2.Fighters[1].HP, Is.LessThan(full), Log(t2));
   Assert.That(t2.Fighters[0].Conditions.ChargeMove, Is.Empty);
   var sun = Turn(F(), F(), M("솔라빔"), Idle(), new Rules { Weather = "쾌청" });
   Assert.That(sun.Fighters[0].Conditions.ChargeMove, Is.Empty);
   var herb = Turn(F("파워풀허브"), F(), M("솔라빔"), Idle());
   Assert.That(herb.Fighters[1].HP, Is.LessThan(full));
   Assert.That(herb.Fighters[0].LostItem, Is.EqualTo("파워풀허브"));
  }

  // 숨기: 하늘 위에는 번개 등만 닿는다. 땅속에는 지진이 2배
  [Test] public void HiddenTargetsOnlyTakeReachingMoves() {
   var flyer = F(); var other = F(); other.Data.@base[5] = 50;
   var t1 = Turn(flyer, other, M("공중날기"), M("몸통박치기"));
   Assert.That(Log(t1), Does.Contain("「하늘 위」에 있어 맞지 않았다"));
   var high = t1.Fighters[0];
   Assert.That(high.Conditions.Hidden, Is.EqualTo("하늘 위"));
   Assert.That(MoveModules.Reaches(other, high, M("몸통박치기"), 1), Is.False);
   Assert.That(MoveModules.Reaches(other, high, M("번개"), 1), Is.True);
   var digger = F(); digger.Conditions.Hidden = "땅속";
   Assert.That(MoveModules.PowerFactor(other, digger, M("지진"), new Rules()), Is.EqualTo(2));
  }

  [Test] public void MetronomeGrowsWithRepeats() {
   var a = F("메트로놈");
   a.Conditions.RepeatMove = "몸통박치기"; a.Conditions.RepeatCount = 3;
   Assert.That(MoveModules.PowerFactor(a, F(), M("몸통박치기"), new Rules()), Is.EqualTo(1.3).Within(1e-9));
   a.Conditions.RepeatCount = 20;
   Assert.That(MoveModules.PowerFactor(a, F(), M("몸통박치기"), new Rules()), Is.EqualTo(2));
   Assert.That(MoveModules.PowerFactor(a, F(), M("할퀴기"), new Rules()), Is.EqualTo(1));
  }

  // 방호패드: 상대의 접촉 반응(울퉁불퉁멧)을 받지 않는다
  [Test] public void ProtectivePadsBlockContactReactions() {
   var plain = Turn(F(), F("울퉁불퉁멧"), M("몸통박치기"), Idle());
   Assert.That(plain.Fighters[0].HP, Is.LessThan(Mechanics.MaxHP(plain.Fighters[0], plain.Fighters[1], new Rules())));
   var pads = Turn(F("방호패드"), F("울퉁불퉁멧"), M("몸통박치기"), Idle());
   Assert.That(pads.Fighters[0].HP, Is.EqualTo(Mechanics.MaxHP(pads.Fighters[0], pads.Fighters[1], new Rules())), Log(pads));
  }

  [Test] public void WhiteHerbRestoresDropsOnce() {
   var t = Turn(F("하양허브"), F(), M("스케일샷"), Idle());
   Assert.That(t.Fighters[0].Stages[2], Is.EqualTo(0), Log(t));
   Assert.That(t.Fighters[0].LostItem, Is.EqualTo("하양허브"));
  }

  [Test] public void CustapBerryRaisesNextTurnPriority() {
   var holder = F("애슈열매"); holder.HP = Mechanics.MaxHP(holder, F(), new Rules()) / 4 + 2;
   var t = Turn(holder, F(), Idle(), M("몸통박치기"));
   Assert.That(t.Fighters[0].Conditions.NextPriority, Is.EqualTo(1), Log(t));
   Assert.That(t.Fighters[0].LostItem, Is.EqualTo("애슈열매"));
  }

  // 끈적끈적바늘: 턴 끝 1/8, 소지품이 없는 상대의 접촉 기술을 받으면 옮겨진다
  [Test] public void StickyBarbHurtsAndMovesOnContact() {
   var t = Turn(F("끈적끈적바늘"), F(), Idle(), M("몸통박치기"));
   Assert.That(t.Fighters[0].Data.item, Is.Empty, Log(t));
   Assert.That(t.Fighters[1].Data.item, Is.EqualTo("끈적끈적바늘"));
   var idle = Turn(F("끈적끈적바늘"), F(), Idle(), Idle());
   int max = Mechanics.MaxHP(idle.Fighters[0], idle.Fighters[1], new Rules());
   Assert.That(idle.Fighters[0].HP, Is.EqualTo(max - max / 8), Log(idle));
  }
 }
}
