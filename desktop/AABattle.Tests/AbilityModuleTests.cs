using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle {
 // 특성 모듈(사용자 결정): 도감 문장을 같은 실행기로, 슬롯 「특성」·로그 〔특성명〕은 따로
 [TestFixture] public partial class AbilityModuleTests {
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
  [TestCase("상대의 「접촉기술」을 받았을 때, 1/8의 데미지를 입는다.", "상대의 「접촉」 기술을 받았을 때, 자신에게 최대 HP의 1/8 대미지를 준다.")]
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

  [Test] public void AbaddonContactAbilityUsesExistingContactDamageModule() {
   var t = Turn(F(), F("결벽증"), M("몸통박치기"), Idle());
   Assert.That(t.Fighters[1].HP, Is.LessThan(Max(t.Fighters[1]) - Max(t.Fighters[1]) / 8), Log(t));
   Assert.That(Log(t), Does.Contain("〔결벽증〕: HP -42"), Log(t));
   Assert.That(AbilityCatalog.Find("결벽증").support, Is.EqualTo("자동"));
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

  [TestCase(false)]
  [TestCase(true)]
  public void RegeneratorDoesNotHealOnForcedSwitch(bool blocked) {
   var source = F();
   source.Data.potentials = new[] { new Potential { id = "종족 ①", name = "강제교대 검사", triggers = new[] { TriggerStructures.AttackSuccessLabel }, description = "상대를 강제교대시킨다." } };
   var target = F("재생력"); target.HP = 100; target.Conditions.HealBlock = blocked ? 3 : 0;
   var plain = target.Copy(); plain.Data.ability = ""; AbilityRuntime.Reset(plain);
   var reserve = F(); reserve.OnField = false;
   var hit = M("몸통박치기").Copy(); hit.power = 10; hit.priority = 1;
   Func<Fighter, TurnResult> run = t => Engine.Resolve(TestData.Db, new[] { source, t }, new[] { new BattleCommand { Move = hit }, new BattleCommand { Move = Idle(), Party = new[] { t, reserve } } }, new Rules(), new[] { new TrainerBattleState(), new TrainerBattleState() }, () => .5);
   var baseline = run(plain); var actual = run(target);
   Assert.That(actual.Switches.Single().Reason, Is.EqualTo(SwitchReason.Forced), Log(actual));
   Assert.That(actual.Switches.Single().Outgoing.HP, Is.EqualTo(baseline.Switches.Single().Outgoing.HP), Log(actual));
  }

  [TestCase(SwitchReason.Normal, false)] [TestCase(SwitchReason.Normal, true)]
  [TestCase(SwitchReason.Pivot, false)] [TestCase(SwitchReason.Return, false)]
  public void RegeneratorKeepsVoluntaryHealingAndHealBlock(SwitchReason reason, bool blocked) {
   var owner = F("재생력"); owner.HP = 100; owner.Conditions.HealBlock = blocked ? 3 : 0;
   using (EffectScope.Begin(new[] { owner, F() }, new Rules(), () => .5)) RuntimeTriggers.Leave(owner, reason, _ => { });
   Assert.That(owner.HP, Is.EqualTo(100 + (blocked ? 0 : Max(owner) / 3)));
  }

  [Test] public void RegeneratorDoesNotHealFaintedDeparture() {
   var owner = F("재생력"); owner.HP = 0;
   var log = new List<string>();
   using (EffectScope.Begin(new[] { owner, F() }, new Rules(), () => .5, new[] { new TrainerBattleState(), new TrainerBattleState() })) RuntimeTriggers.Leave(owner, SwitchReason.Fainted, log.Add);
   Assert.That(owner.HP, Is.Zero, string.Join("\n", log));
  }

  [TestCase("방진", "모래바람")]
  [TestCase("방진", "싸라기눈")]
  [TestCase("모래헤치기", "모래바람")]
  [TestCase("두꺼운모피", "싸라기눈")]
  [TestCase("방진코트", "모래바람")]
  public void WeatherDamageImmunityComesFromModules(string ability, string weather) {
   var owner = F(ability); int hp = owner.HP; var log = new List<string>();
   Mechanics.EndTurn(new[] { owner, F() }, new Rules { Weather = weather }, log);
   Assert.That(owner.HP, Is.EqualTo(hp), string.Join("\n", log));
   owner.AbilitySuppressed = true; AbilityModules.Sync(owner);
   Mechanics.EndTurn(new[] { owner, F() }, new Rules { Weather = weather }, log);
   Assert.That(owner.HP, Is.EqualTo(hp - Max(owner) / 16), "억제되면 날씨 피해를 받는다");
  }

  [TestCase("가위자르기", false)]
  [TestCase("뿔드릴", false)]
  [TestCase("땅가르기", false)]
  [TestCase("절대영도", false)]
  [TestCase("태그 일격필살", true)]
  public void SturdyModuleBlocksOhkoNamesAndTags(string name, bool tagged) {
   var owner = F("옹골참"); owner.HP = 70;
   var move = new Move { name = name, types = new[] { "노말" }, category = "물리", power = 1, accuracy = 100, tags = tagged ? new[] { "일격필살" } : new string[0] };
   Assert.That(Engine.Calculate(TestData.Db, F(), owner, move, new Rules()).Max, Is.Zero);
   Assert.That(Engine.Calculate(TestData.Db, F("틀깨기"), owner, move, new Rules()).Max, Is.GreaterThan(0));
  }

  [TestCase("화상", "물리")]
  [TestCase("동상", "특수")]
  public void GutsModuleIgnoresStatusPowerPenalty(string status, string category) {
   var owner = F("근성"); owner.Status = status;
   var move = M("몸통박치기").Copy(); move.category = category;
   Assert.That(Mechanics.PowerFactor(owner, F(), move, new Rules()), Is.EqualTo(1));
   owner.AbilitySuppressed = true; AbilityModules.Sync(owner);
   Assert.That(Mechanics.PowerFactor(owner, F(), move, new Rules()), Is.EqualTo(.5));
  }

  [Test] public void GenericWeatherImmunityOnlyBlocksListedWeather() {
   var owner = F();
   owner.Data.potentials = new[] { new Potential { id = "종족 ①", name = "날씨 방호 검사", triggers = new[] { TriggerStructures.AlwaysLabel }, description = "자신은 「모래바람」의 대미지를 받지 않는다." } };
   int hp = owner.HP; var log = new List<string>();
   Mechanics.EndTurn(new[] { owner, F() }, new Rules { Weather = "모래바람" }, log);
   Assert.That(owner.HP, Is.EqualTo(hp));
   Mechanics.EndTurn(new[] { owner, F() }, new Rules { Weather = "싸라기눈" }, log);
   Assert.That(owner.HP, Is.EqualTo(hp - Max(owner) / 16));
  }

  [Test] public void GenericIndirectImmunityStopsSeedDrain() {
   var owner = F(); owner.Conditions.Seed = true;
   owner.Data.potentials = new[] { new Potential { id = "종족 ①", name = "간접 방호 검사", triggers = new[] { TriggerStructures.AlwaysLabel }, description = "자신은 공격 이외의 대미지를 받지 않는다." } };
   var other = F(); other.HP = 100; int hp = owner.HP;
   Mechanics.EndTurn(new[] { owner, other }, new Rules(), new List<string>());
   Assert.That(owner.HP, Is.EqualTo(hp));
   Assert.That(other.HP, Is.EqualTo(100), "피해가 없으면 드레인 회복도 없다");
  }

  [Test] public void PranksterStatusMoveAffectsDarkTypes() {
   var turn = Turn(F("짓궂은마음"), F("", "악"), M("꼬리흔들기"), Idle());
   Assert.That(turn.Fighters[1].Stages[2], Is.EqualTo(-1), Log(turn));
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
