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

  [Test] public void RewrittenAbilityRulesReachTheirBattleChecks() {
   var guard = F("매직가드");
   Assert.That(AbilityPhrases.IgnoresIndirect(guard), Is.True);
   Assert.That(AbilityPhrases.IgnoresHazards(F("부유")), Is.True);
   Assert.That(AbilityPhrases.BlocksDrop(F("클리어바디"), 1), Is.True);
   var sturdy = F("옹골참");
   Assert.That(AbilityPhrases.Endures(sturdy, F()), Is.True);
   var breaker = F("틀깨기");
   breaker.Conditions.Acting = true;
   Assert.That(Mechanics.BypassAbility(breaker), Is.True, string.Join(" | ", PotentialModifiers.Sentences(breaker, null, new Rules(), "관통")));
  }

  [Test] public void CatalogOnlyDefensiveAndPowerEffectsRunInBattle() {
   var attacker = F("철주먹");
   var defender = F();
   var punch = M("냉동펀치");
   var plain = Engine.Calculate(TestData.Db, F(), defender, punch, new Rules()).Max;
   var boosted = Engine.Calculate(TestData.Db, attacker, defender, punch, new Rules()).Max;
   Assert.That(boosted, Is.GreaterThan(plain));
   var full = F("멀티스케일");
   plain = Engine.Calculate(TestData.Db, F(), F(), M("몸통박치기"), new Rules()).Max;
   Assert.That(Engine.Calculate(TestData.Db, F(), full, M("몸통박치기"), new Rules()).Max, Is.LessThan(plain));
  }

  [Test] public void AbsorptionAndEntryEffectsRunFromCatalogText() {
   var t = Turn(F(), F("타오르는불꽃"), M("화염방사"), Idle());
   Assert.That(t.Fighters[1].HP, Is.EqualTo(Max(t.Fighters[1])), Log(t));
   Assert.That(t.Fighters[1].Stages[3], Is.EqualTo(1), Log(t));
   Assert.That(t.Fighters[1].Conditions.FlashFire, Is.False, Log(t));
   t = Turn(F(), F("수의베일"), Idle(), Idle());
   Assert.That(t.Fighters[1].Conditions.AquaRing, Is.True, Log(t));
   var soggy = F("마그마의무장");
   Assert.That(Mechanics.StatusBlock(soggy, F(), "얼음", new Rules()), Does.Contain("특성"));
   Assert.That(Mechanics.StatusBlock(F("수의베일"), F(), "화상", new Rules()), Does.Contain("특성"));
   Assert.That(AbilityPhrases.NoCrit(soggy, F()), Is.True);
  }

  [Test] public void ElectricAbsorptionModuleMatchesElectricAttacks() {
   var source = F();
   var target = F("피뢰침");
   var move = new Move { name = "검사 전기", category = "특수", types = new[] { "전기" }, tags = new string[0], power = 80, accuracy = 100 };
   Assert.That(PotentialModifiers.Sentences(target, source, new Rules(), "무효"), Does.Contain("상대의 「전기」 기술을 무효화한다."));
   var guard = PotentialProtection.Check(TestData.Db, source, target, move, new Rules(), () => .5, false);
   Assert.That(guard.HasValue, Is.True, string.Join(" | ", PotentialModifiers.Sentences(target, source, new Rules(), "무효")));
  }

  [Test] public void CatalogPowerRewritesMatchTheirMoveTags() {
   var defender = F();
   var move = new Move { name = "분류검사", category = "물리", types = new[] { "노말" }, tags = new[] { "펀치" }, power = 60, accuracy = 100 };
   Assert.That(PotentialModifiers.Power(F("테크니션"), defender, move, new Rules(), 1), Is.EqualTo(1.5));
   Assert.That(PotentialModifiers.Power(F("철주먹"), defender, move, new Rules(), 1), Is.EqualTo(1.5));
   move.tags = new[] { "파동" };
   Assert.That(PotentialModifiers.Power(F("메가런처"), defender, move, new Rules(), 1), Is.EqualTo(1.5));
   move.tags = new[] { "물기/엄니" };
   Assert.That(PotentialModifiers.Power(F("옹골찬턱"), defender, move, new Rules(), 1), Is.EqualTo(1.5));
   move.tags = new[] { "칼/검" };
   Assert.That(PotentialModifiers.Power(F("예리함"), defender, move, new Rules(), 1), Is.EqualTo(1.5));
   move.tags = new[] { "반동" };
   Assert.That(PotentialModifiers.Power(F("이판사판"), defender, move, new Rules(), 1), Is.EqualTo(1.2));
  }

  [Test] public void BulletproofOnlyHalvesCatalogListedExplosions() {
   var attacker = F();
   var explosion = new Move { name = "자폭", category = "물리", types = new[] { "노말" }, tags = new string[0], power = 100, accuracy = 100, attack = "공격", defense = "방어" };
   var plain = Engine.Calculate(TestData.Db, attacker, F(), explosion, new Rules()).Max;
   var guarded = Engine.Calculate(TestData.Db, attacker, F("방탄"), explosion, new Rules()).Max;
   Assert.That(guarded, Is.LessThan(plain));
  }
 }
}
