using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle {
 [TestFixture]
 public class ModuleSchemaTests {
  [Test]
  public void TemplateRoundTripsFilledValuesAndRejectsAnotherSentence() {
   var module = new ModuleDefinition("검사", "기술 무효화", ModuleKind.Effect, "방호",
    "상대의 {조건} 기술을 무효화한다.", new ModuleSlot("조건", ModuleSlotKind.MoveCondition, "「불꽃」"));
   var edited = new Dictionary<string,string> { { "조건", "「접촉」「물리」" } };
   string text = module.Fill(edited);
   Assert.That(EffectModules.Classify(text), Is.EqualTo("기술 무효화"));
   Dictionary<string,string> read;
   Assert.That(module.TryRead(text, out read), Is.True);
   Assert.That(read, Is.EqualTo(edited));
   Assert.That(module.TryRead("자신은 공중에 떠오른다.", out read), Is.False);
  }
  [Test]
  public void EmptyConditionRemovesOnlyOptionalWhitespace() {
   var module = new ModuleDefinition("검사", "기술 사용", ModuleKind.TriggerEvent, "행동",
    "{조건} 기술을 내보낼 때", new ModuleSlot("조건", ModuleSlotKind.MoveCondition, ""));
   Assert.That(module.Fill(), Is.EqualTo("기술을 내보낼 때"));
   Dictionary<string,string> read;
   Assert.That(module.TryRead(module.Fill(), out read), Is.True);
   Assert.That(read["조건"], Is.Empty);
  }
  [Test]
  public void NumericInputCannotProduceInvalidDenominator() {
   var module = new ModuleDefinition("검사", "회복", ModuleKind.Effect, "체력",
    "자신의 체력을 최대 HP의 1/{N} 회복한다.", new ModuleSlot("N", ModuleSlotKind.Denominator, "8"));
   Assert.Throws<System.ArgumentException>(() => module.Fill(new Dictionary<string,string> { { "N", "0" } }));
  }
 }
}
