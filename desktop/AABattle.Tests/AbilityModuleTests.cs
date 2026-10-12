using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle {
 // 특성 모듈(사용자 결정): 도감 문장을 같은 실행기로, 슬롯 「특성」·로그 〔특성명〕은 따로
 [TestFixture] public class AbilityModuleTests {
  static int ids;
  static Fighter F(string ability = "", string type = "노말") {
   return new Fighter(new Pokemon {
    id = "pk-am" + (++ids), name = "A" + ids, level = 100, types = new[] { type }, ability = ability, item = "",
    @base = new[] { 100, 100, 100, 100, 100, 100 }, iv = new[] { 31, 31, 31, 31, 31, 31 },
    moves = new[] { "몸통박치기" }, potentials = new Potential[0]
   });
  }
  static Move M(string name) { return TestData.Db.moves.First(x => x.name == name); }
  static Move Idle() { return new Move { name = "미등록", category = "변화", types = new[] { "노말" }, tags = new string[0], accuracy = 100, target = "자신" }; }
  static TurnResult Turn(Fighter a, Fighter b, Move ma, Move mb, double roll = .5) { return Engine.Resolve(TestData.Db, new[] { a, b }, new[] { ma, mb }, new Rules(), () => roll); }
  static string Log(TurnResult t) { return string.Join("\n", t.Log); }
  static int Max(Fighter f) { return Mechanics.MaxHP(f, f, new Rules()); }

  [TestCase("「접촉」 기술을 받았을 때,상대에게 HP의 1/8만큼 대미지를 준다.", "상대의 「접촉」 기술을 받았을 때, 상대에게 최대 HP의 1/8 대미지를 준다.")]
  [TestCase("급소무효. 상대의 공격 이외의 대미지를 무효화한다.", "자신은 급소에 맞지 않는다. 자신은 공격 이외의 대미지를 받지 않는다.")]
  [TestCase("「에스퍼」 기술을 받으면 흡수해서 「특공」이 오른다.", "상대의 「에스퍼」 기술을 무효화한다. 상대의 「에스퍼」 기술을 무효화했을 때, 자신의 「특공」이 오른다.")]
  [TestCase("상대의 「불꽃」 타입 기술을 무효화한다.이 〔특성〕이 발동했을 때,자신의 「방어」가 크게 오른다.", "상대의 「불꽃」 기술을 무효화한다. 상대의 「불꽃」 기술을 무효화했을 때, 자신의 「방어」가 크게 오른다.")]
  public void CatalogTextBecomesModules(string raw, string canonical) {
   Assert.That(AbilityPhrases.Fold(AbilityPhrases.Rewrite(AbilityModules.Clean(raw))), Is.EqualTo(canonical));
  }

  [Test] public void RoughSkinRunsAsAbilitySlot() {
   var t = Turn(F(), F("까칠한피부"), M("몸통박치기"), Idle());
   Assert.That(t.Fighters[0].HP, Is.EqualTo(Max(t.Fighters[0]) - Max(t.Fighters[0]) / 8), Log(t));
   Assert.That(Log(t), Does.Contain("〔까칠한피부〕"));
   Assert.That(t.Fighters[1].Data.potentials.Any(p => p.id == AbilityModules.Slot), Is.True);
  }

  [Test] public void AbsorbAbilityNullifiesAndBoosts() {
   var t = Turn(F(), F("공감"), M("사이코키네시스"), Idle());
   Assert.That(t.Fighters[1].HP, Is.EqualTo(Max(t.Fighters[1])), Log(t));
   Assert.That(t.Fighters[1].Stages[3], Is.EqualTo(1), Log(t));
  }

  [Test] public void TypeAddAndCriticalImmunity() {
   var f = F("기계몸");
   Assert.That(f.Data.types, Does.Contain("강철"));
   Assert.That(AbilityPhrases.NoCrit(f, F()), Is.True);
   f.AbilitySuppressed = true; AbilityModules.Sync(f);
   Assert.That(f.Data.types, Does.Not.Contain("강철"), "무효화되면 붙인 타입도 뗀다");
  }

  [Test] public void TorrentBoostsAtLowHp() {
   var low = F("급류"); low.HP = Max(low) / 3;
   double boosted = Engine.Calculate(TestData.Db, low, F(), M("물대포"), new Rules()).Max;
   double plain = Engine.Calculate(TestData.Db, F("급류"), F(), M("물대포"), new Rules()).Max;
   Assert.That(boosted, Is.GreaterThan(plain * 1.4));
  }

  [Test] public void MagicGuardAndHazardImmunity() {
   var guard = F("다이아아머");
   Assert.That(AbilityPhrases.IgnoresIndirect(guard), Is.True);
   Assert.That(AbilityPhrases.IgnoresHazards(F("딱딱한발굽")), Is.True);
  }
 }
}
