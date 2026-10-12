using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace AABattle {
 public partial class AbilityModuleTests {
  static Fighter ModuleOwner(string ability, string text, bool potential) {
   var f = F(potential ? "" : ability);
   if (potential) {
    string condition, effect; PotentialLibrary.Split(text, out condition, out effect);
    f.Data.potentials = new[] { new Potential { id = "종족 ①", name = "공용 모듈 검사", description = text, triggers = condition.Length == 0 ? new[] { TriggerStructures.AlwaysLabel } : new string[0] } };
   }
   return f;
  }

  [TestCase(130, 90, 3)]
  [TestCase(90, 130, 1)]
  [TestCase(100, 100, 1)]
  public void DownloadChecksOpponentEffectiveStatsOnEntry(int defense, int specialDefense, int expected) {
   var b = F(); b.Data.@base[2] = defense; b.Data.@base[4] = specialDefense;
   var turn = Turn(F("다운로드"), b, Idle(), Idle());
   Assert.That(turn.Fighters[0].Stages[expected], Is.EqualTo(1), Log(turn));
   Assert.That(turn.Fighters[0].Stages[expected == 1 ? 3 : 1], Is.Zero);
   Assert.That(Log(turn), Does.Contain("〔다운로드"));
  }

  [TestCase("단순", "자신의 능력 변화량을 1 늘린다.", 2, 3, false)]
  [TestCase("단순", "자신의 능력 변화량을 1 늘린다.", -2, -3, false)]
  [TestCase("단순", "자신의 능력 변화량을 1 늘린다.", 2, 3, true)]
  [TestCase("심술꾸러기", "자신에 대한 능력 변화를 반대로 한다.", -2, 2, false)]
  [TestCase("심술꾸러기", "자신에 대한 능력 변화를 반대로 한다.", 2, -2, true)]
  public void StageTransformsUseModules(string ability, string text, int amount, int expected, bool potential) {
   var owner = ModuleOwner(ability, text, potential);
   Mechanics.ChangeStage(owner, owner, 1, amount, new Rules(), "검사", new List<string>());
   Assert.That(owner.Stages[1], Is.EqualTo(expected));
  }

  [TestCase(false)] [TestCase(true)]
  public void MirrorArmorReflectsOnlyOnce(bool potential) {
   const string text = "상대에 의한 능력 하락을 상대에게 되돌린다.";
   var a = ModuleOwner("미러아머", text, potential); var b = ModuleOwner("미러아머", text, potential);
   Mechanics.ChangeStage(a, b, 2, -2, new Rules(), "검사", new List<string>());
   Assert.That(a.Stages[2], Is.Zero);
   Assert.That(b.Stages[2], Is.EqualTo(-2));
  }

  [TestCase("날씨부정", false)] [TestCase("에어록", false)] [TestCase("", true)]
  public void WeatherNegationDisablesSwiftSwimAndReturnsWhenOwnerLeaves(string ability, bool potential) {
   var a = ModuleOwner(ability, "날씨의 효과를 무효화한다.", potential); var b = F("쓱쓱");
   var r = new Rules { Weather = "비" };
   int dry = Engine.Effective(b, a, 5, new Rules());
   Assert.That(Mechanics.Weather(a, b, r), Is.EqualTo("없음"));
   Assert.That(Engine.Effective(b, a, 5, r), Is.EqualTo(dry));
   a.OnField = false;
   Assert.That(Engine.Effective(b, a, 5, r), Is.EqualTo(dry * 2));
  }

  [TestCase(false)] [TestCase(true)]
  public void InfiltratorPiercesProtectForStatusOnlyAndKeepsSubstituteAndScreens(bool potential) {
   var a = ModuleOwner("틈새포착", "자신의 「변화」 기술이 상대의 「방어」 기술을 관통한다.", potential);
   var b = F(); b.Conditions.Protect = true;
   Assert.That(Mechanics.Block(TestData.Db, a, b, M("꼬리흔들기"), new Rules()), Is.Empty);
   Assert.That(Mechanics.Block(TestData.Db, a, b, M("몸통박치기"), new Rules()), Is.EqualTo(ProtectModules.Blocked));
   b.Conditions.Protect = false; b.Conditions.Substitute = 10;
   Assert.That(Mechanics.Block(TestData.Db, a, b, M("꼬리흔들기"), new Rules()), Does.Contain("대타"));
   Assert.That(Mechanics.BypassSub(a, M("몸통박치기")), Is.False);
   var r = new Rules(); r.Sides[1].Reflect = 2;
   Assert.That(Mechanics.DamageFactor(a, b, M("몸통박치기"), r, 1), Is.EqualTo(.5));
  }

  [TestCase("독", false)] [TestCase("강철", false)] [TestCase("독", true)]
  public void CorrosionPiercesTypeImmunityButNotStatusImmunity(string type, bool potential) {
   var a = ModuleOwner("부식", "자신이 거는 「독」「맹독」은 타입 면역을 관통한다.", potential);
   var b = F("", type); var r = new Rules();
   Assert.That(Mechanics.ApplyStatus(b, a, "독", r, () => .5, new List<string>()), Is.True);
   Assert.That(Mechanics.StatusBlock(F("면역", type), a, "독", r), Is.Not.Empty);
  }

  [TestCase(SwitchReason.Normal, false)] [TestCase(SwitchReason.Forced, false)]
  [TestCase(SwitchReason.Fainted, false)] [TestCase(SwitchReason.Forced, true)]
  public void NaturalCureUsesDepartureEventAndExcludesFainted(SwitchReason reason, bool potential) {
   var a = ModuleOwner("자연회복", "강제교대를 포함해 장소를 떠날 때, 자신의 「상태이상」을 치유한다.", potential);
   var b = F(); a.Status = "독";
   if (reason == SwitchReason.Fainted) a.HP = 0;
   using (EffectScope.Begin(new[] { a, b }, new Rules(), () => .5)) RuntimeTriggers.Leave(a, reason, _ => { });
   Assert.That(a.Status, Is.EqualTo(reason == SwitchReason.Fainted ? "독" : "정상"));
  }

  [TestCase(false)] [TestCase(true)]
  public void SuppressionResumesContinuousEffectsWithoutEntry(bool potential) {
   var a = ModuleOwner("화학변화가스", "상대의 〔특성〕을 무효화한다.", potential); var b = F("다운로드");
   var t = Turn(a, b, Idle(), Idle());
   Assert.That(t.Fighters[1].AbilitySuppressed, Is.True);
   Assert.That(t.Fighters[1].Stages[1], Is.Zero);
   t.Fighters[0].OnField = false;
   AbilityRuntime.RefreshSuppression(t.Fighters, _ => { });
   Assert.That(t.Fighters[1].AbilitySuppressed, Is.False);
   Assert.That(t.Fighters[1].Data.potentials.Any(p => p.id == AbilityModules.Slot), Is.True);
   Assert.That(t.Fighters[1].Stages[1], Is.Zero);
  }

  [TestCase(false)] [TestCase(true)]
  public void TwoSuppressorsAreOrderIndependentEvenWithStaleSuppression(bool reverse) {
   var a = F("화학변화가스"); var b = F("화학변화가스");
   b.AbilitySuppressed = true; AbilityModules.Sync(b);
   var pair = reverse ? new[] { b, a } : new[] { a, b };
   AbilityRuntime.RefreshSuppression(pair, _ => { });
   Assert.That(pair.All(f => !f.AbilitySuppressed), Is.True);
   AbilityRuntime.SetCurrent(a, "쓱쓱");
   AbilityRuntime.RefreshSuppression(pair, _ => { });
   Assert.That(a.AbilitySuppressed, Is.True);
   Assert.That(b.AbilitySuppressed, Is.False);
  }

  [Test] public void OrdinarySuppressionDoesNotMakeAnUnrelatedAbilityImmune() {
   var a = F("쓱쓱"); var b = F("화학변화가스");
   var ordinary = ModuleOwner("", "상대의 〔특성〕을 무효화한다.", true).Data.potentials;
   a.Data.potentials = a.Data.potentials.Concat(ordinary).ToArray();
   AbilityRuntime.RefreshSuppression(new[] { a, b }, null);
   Assert.That(a.AbilitySuppressed, Is.True, "무효화 면역은 무효화 효과를 가진 특성 자체에만 적용한다");
   Assert.That(b.AbilitySuppressed, Is.False);
   Assert.That(a.Data.potentials.Any(p => p.id == "종족 ①"), Is.True);
   AbilityRuntime.RefreshSuppression(new[] { a, b }, null);
   Assert.That(a.AbilitySuppressed, Is.True, "다시 갱신해도 결과가 흔들리지 않는다");
  }

  [Test] public void SuppressedAbilityComesBackAfterLeavingAndReentering() {
   var a = F("쓱쓱"); var b = F("화학변화가스"); var r = new Rules { Weather = "비" };
   int normalSpeed = Engine.Effective(a, b, 5, new Rules());
   AbilityRuntime.RefreshSuppression(new[] { a, b }, null);
   Mechanics.LeaveField(a);
   a.OnField = true; b.OnField = false;
   AbilityRuntime.RefreshSuppression(new[] { a, b }, null);
   Assert.That(Engine.Effective(a, b, 5, r), Is.EqualTo(normalSpeed * 2));
  }

  [TestCase(false)] [TestCase(true)]
  public void IncomingDownloadIsSuppressedBeforeItsEntryEvent(bool forced) {
   var gas = F("화학변화가스"); var departing = F(); var incoming = F("다운로드"); incoming.OnField = false;
   if (forced) gas.Data.potentials = gas.Data.potentials.Concat(new[] {
    new Potential { id = "종족 ①", name = "교대 검사", triggers = new[] { TriggerStructures.AttackSuccessLabel }, description = "상대를 강제교대시킨다." }
   }).ToArray();
   var hit = M("몸통박치기").Copy(); hit.power = 1;
   var commands = new[] {
    new BattleCommand { Move = forced ? hit : Idle() },
    new BattleCommand { Move = Idle(), SwitchIn = forced ? null : incoming, Party = new[] { departing, incoming } }
   };
   var t = Engine.Resolve(TestData.Db, new[] { gas, departing }, commands, new Rules(), new[] { new TrainerBattleState(), new TrainerBattleState() }, () => .5);
   Assert.That(t.Fighters[1].Data.id, Is.EqualTo(incoming.Data.id), Log(t));
   Assert.That(t.Fighters[1].AbilitySuppressed, Is.True);
   Assert.That(t.Fighters[1].Stages[1], Is.Zero, Log(t));
  }

  [Test] public void AcquiredDownloadWaitsForReentryAndGasSuppressesWeatherNegation() {
   var t = Turn(F(), F(), Idle(), Idle());
   AbilityRuntime.SetCurrent(t.Fighters[0], "다운로드");
   var next = Turn(t.Fighters[0], t.Fighters[1], Idle(), Idle());
   Assert.That(next.Fighters[0].Stages[1], Is.Zero, Log(next));
   var a = F("날씨부정"); var b = F("화학변화가스");
   AbilityRuntime.RefreshSuppression(new[] { a, b }, null);
   Assert.That(Mechanics.Weather(a, b, new Rules { Weather = "비" }), Is.EqualTo("비"));
   b.HP = 0; AbilityRuntime.RefreshSuppression(new[] { a, b }, null);
   Assert.That(Mechanics.Weather(a, b, new Rules { Weather = "비" }), Is.EqualTo("없음"));
  }

  [TestCase("단순", "자신의 능력 변화량을 1 늘린다.", -2)]
  [TestCase("심술꾸러기", "자신에 대한 능력 변화를 반대로 한다.", -2)]
  [TestCase("미러아머", "상대에 의한 능력 하락을 상대에게 되돌린다.", -2)]
  public void MoldBreakerBypassesAbilityStageRulesButNotOrdinaryPotentials(string ability, string text, int amount) {
   foreach (bool potential in new[] { false, true }) {
    var target = ModuleOwner(ability, text, potential); var source = F("틀깨기"); source.Conditions.Acting = true;
    Mechanics.ChangeStage(target, source, 1, amount, new Rules(), "검사", new List<string>());
    Assert.That(target.Stages[1], potential ? Is.Not.EqualTo(amount) : Is.EqualTo(amount));
   }
  }

  [Test] public void StatComparisonUsesStagesAndWorksAsOrdinaryEntryCondition() {
   var a = ModuleOwner("", "필드에 나왔을 때, 상대의 「방어」가 「특방」보다 높을 때, 자신의 「특공」이 오른다.", true);
   var b = F(); b.Stages[2] = 1;
   var t = Turn(a, b, Idle(), Idle());
   Assert.That(t.Fighters[0].Stages[3], Is.EqualTo(1), Log(t));
   var again = Turn(t.Fighters[0], t.Fighters[1], Idle(), Idle());
   Assert.That(again.Fighters[0].Stages[3], Is.EqualTo(1), "등장 사건 없이 비교 상태만으로 발동하지 않는다");
  }
 }
}
