using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;

namespace AABattle {
 [TestFixture] public class AbilityRemainingCleanupTests {
  static Fighter Fighter(string ability = "", bool potentialGuard = false) {
   return new Fighter(new Pokemon {
    id = Guid.NewGuid().ToString(), name = "검사", level = 100,
    types = new[] { "노말" }, ability = ability, item = "",
    @base = new[] { 100, 100, 100, 100, 100, 100 }, iv = new[] { 31, 31, 31, 31, 31, 31 },
    moves = new string[0], potentials = potentialGuard ? new[] {
     new Potential { id = "계제", name = "간접 방호", triggers = new[] { TriggerStructures.FieldLabel }, description = "자신은 공격 이외의 대미지를 받지 않는다." }
    } : new Potential[0]
   });
  }

  // 이름이 다른 특성과 일반 포텐셜도 같은 간접 대미지 방호 문장으로 막는다.
  [TestCase("", false, false, false)]
  [TestCase("매직가드", false, false, true)]
  [TestCase("다이아아머", false, false, true)]
  [TestCase("", true, false, true)]
  [TestCase("매직가드", false, true, false)]
  public void FractionDamageUsesIndirectProtectionModule(string ability, bool potentialGuard, bool suppressed, bool protectedTarget) {
   var owner = Fighter();
   var target = Fighter(ability, potentialGuard);
   target.AbilitySuppressed = suppressed;
   AbilityModules.Sync(target);
   var rules = new Rules();
   var call = new EffectCall {
    Source = new PotentialInstance { Owner = owner, Name = "분수 대미지" },
    Event = new PotentialEvent { Owner = owner, Other = target },
    Line = "상대에게 최대 HP의 1/4 대미지를 준다.", Rules = rules, Log = new List<string>()
   };
   int before = target.HP;
   using (EffectScope.Begin(new[] { owner, target }, rules, () => .5, new[] { new TrainerBattleState(), new TrainerBattleState() })) {
    Assert.That(PotentialExecutors.Find(call).Run(call), Is.EqualTo(protectedTarget ? EffectOutcome.Failed : EffectOutcome.Success));
   }
   Assert.That(target.HP, Is.EqualTo(protectedTarget ? before : before - 85));
  }

  static T Field<T>(DataEditorForm form, string name) {
   return (T)typeof(DataEditorForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
  }
  static object Invoke(DataEditorForm form, string name, params object[] arguments) {
   return typeof(DataEditorForm).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, arguments);
  }

  [TestCase("상대", "방어", "특방", "높음", "상대의 「방어」가 「특방」보다 높을 때")]
  [TestCase("자신", "공격", "속도", "이하", "자신의 「공격」이 「속도」 이하일 때")]
  [TestCase("아군", "특공", "방어", "높음", "아군의 「특공」이 「방어」보다 높을 때")]
  [Category("UI"), Apartment(ApartmentState.STA)]
  public void StatComparisonSelectorsBuildAndReload(string subject, string left, string right, string op, string expected) {
   using (var form = new DataEditorForm()) {
    Field<PixelComboBox>(form, "triggerKind").SelectedItem = "능력치 비교";
    Field<PixelComboBox>(form, "triggerSubject").SelectedItem = subject;
    Field<PixelComboBox>(form, "triggerCompareLeft").SelectedItem = left;
    Field<PixelComboBox>(form, "triggerCompareRight").SelectedItem = right;
    Field<PixelComboBox>(form, "triggerCompareOp").SelectedItem = op;
    Invoke(form, "RefreshStructuredTrigger");
    Assert.That(Invoke(form, "ConfiguredTrigger"), Is.EqualTo(expected));
    Assert.That(TriggerModules.Parse(expected).IsEvent, Is.False);
    foreach (string selector in new[] { "triggerCompareLeft", "triggerCompareRight" })
     Assert.That(Field<PixelComboBox>(form, selector).Items.Cast<object>().Select(x => x.ToString()), Is.EquivalentTo(new[] { "공격", "방어", "특공", "특방", "속도" }));

    Field<PixelComboBox>(form, "triggerKind").SelectedItem = "체력";
    Assert.That(Invoke(form, "LoadTriggerModule", expected), Is.True);
    Invoke(form, "RefreshStructuredTrigger");
    Assert.That(Field<PixelComboBox>(form, "triggerSubject").Text, Is.EqualTo(subject));
    Assert.That(Field<PixelComboBox>(form, "triggerCompareLeft").Text, Is.EqualTo(left));
    Assert.That(Field<PixelComboBox>(form, "triggerCompareRight").Text, Is.EqualTo(right));
    Assert.That(Field<PixelComboBox>(form, "triggerCompareOp").Text, Is.EqualTo(op));
    Assert.That(Invoke(form, "ConfiguredTrigger"), Is.EqualTo(expected));
   }
  }
 }
}
