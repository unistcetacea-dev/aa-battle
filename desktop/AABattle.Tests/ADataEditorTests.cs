using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using NUnit.Framework;

namespace AABattle {
 [TestFixture] public class ADataEditorTests {
  static IEnumerable<Control> Descendants(Control root) {
   foreach (Control child in root.Controls) {
    yield return child;
    foreach (var nested in Descendants(child)) yield return nested;
   }
  }

  [Test] public void OremasRageFistHasItsOwnLookupName() {
   var fist = TestData.Db.moves.Where(x => x.name.Contains("분노의주먹")).ToArray();
   Assert.That(fist.Select(x => x.name), Is.EquivalentTo(new[] { "분노의주먹(오레마스)", "분노의주먹" }));
   Assert.That(fist.Single(x => x.name == "분노의주먹(오레마스)").types, Does.Contain("격투"));
   Assert.That(fist.Single(x => x.name == "분노의주먹").types, Does.Contain("고스트"));
   Assert.That(MoveNames.Known("분노의주먹(오레마스)"), Is.True);
   Assert.That(MoveNames.Known("분노의주먹"), Is.True);
  }

  [Test] public void RemovedConfirmedTriggerExamplesAreNotSuggested() {
   Assert.That(ConfirmedTriggerCatalog.Items, Does.Not.Contain("장소에 나온 뒤 1T째일 때"));
   Assert.That(ConfirmedTriggerCatalog.Items, Does.Not.Contain("조건 A 그리고 조건 B"));
   Assert.That(ConfirmedTriggerCatalog.Items, Does.Contain("상대의 「속도」 종족치가 「B」 이하일 때"));
   Assert.That(ConfirmedTriggerCatalog.Items, Does.Contain("자신이 나무열매를 소모했을 때"));
  }

  [Test] public void TrainingExMeetsAaThreshold() {
   var trainer = new TrainerRecord();
   trainer.training.rank = "EX";
   Assert.That(TrainerAccessRules.TrainerAtLeast(trainer, "육성", "AA"), Is.True);
  }

  [Test] public void SpeciesValueIncreaseIsARegisteredEffectModule() {
   const string sentence = "자신의 「체력」 종족치를 50 올린다.";
   Assert.That(EffectNormalizer.Normalize(sentence), Is.EqualTo("자신의 「체력」 종족치를 50 올린다"));
   Assert.That(EffectModules.Classify(sentence), Is.EqualTo("종족치 변경"));
   SpeciesEffect effect;
   Assert.That(EffectModules.TrySpecies(sentence, out effect), Is.True);
   Assert.That(effect.Operation, Is.EqualTo("가산"));
   Assert.That(effect.Value, Is.EqualTo(50));
   ModuleDefinition module;
   Dictionary<string, string> values;
   Assert.That(ModuleCatalog.TryRead(ModuleKind.Effect, sentence, out module, out values), Is.True);
  }

  [Test] public void AbaddonItemsArePresentInCatalog() {
   Assert.That(ItemCatalog.Find("돌격조끼"), Is.Not.Null);
   Assert.That(AbilityCatalog.Find("결벽증"), Is.Not.Null);
   Assert.That(AbilityPhrases.Rewrite("상대의 「접촉기술」을 받았을 때, 1/8의 데미지를 입는다."),
    Is.EqualTo("상대의 「접촉」 기술을 받았을 때, 자신에게 최대 HP의 1/8 대미지를 준다."));
  }

  [Test, Category("UI"), Apartment(ApartmentState.STA)] public void IceWallSideInputsSaveIntoRules() {
   using (var form = new RulesForm(new Rules())) {
    var label = Descendants(form).Single(x => x.Text == "1팀 얼음의벽");
    var table = (TableLayoutPanel)label.Parent;
    var number = (PixelNumber)table.GetControlFromPosition(1, table.GetRow(label));
    number.Value = 4;
    var apply = Descendants(form).OfType<FlatButton>().Single(x => x.Text == "적용");
    typeof(Control).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(apply, new object[] { System.EventArgs.Empty });
    Assert.That(form.Value.Sides[0].IceWall, Is.EqualTo(4));
   }
  }
 }
}
