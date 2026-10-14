using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle
{
  // 우선도 모듈 하나: 기술 조건 빈칸을 가진 effect.priority.올린다가 조건 우선도 문장을 대신한다
  [TestFixture]
  public class PriorityModuleMergeTests
  {
    const string Conditional = "자신의 「변화」 기술 우선도를 1 올린다.";

    [TestCase("자신의 「변화」 기술의 우선도를 +1한다.")]
    [TestCase("자신의 「변화」기술의 우선도를 「+1」한다.")]
    [TestCase("자신의 「변화」 기술의 우선도를 ＋1한다.")]
    public void OldConditionalPhraseBecomesTheStandardSentence(string raw)
    {
      Assert.That(EffectModules.Normalize(raw), Is.EqualTo(Conditional));
    }

    [Test]
    public void OneModuleReadsBothTheUnconditionalAndTheConditionalSentence()
    {
      var module = ModuleCatalog.Find("effect.priority.올린다");
      Dictionary<string, string> values;
      Assert.That(module.TryRead(Conditional, out values), Is.True);
      Assert.That(values["기술조건"], Is.EqualTo("「변화」"));
      Assert.That(module.TryRead("자신의 기술 우선도를 2 올린다.", out values), Is.True);
      Assert.That(values["기술조건"], Is.EqualTo(""));
      Assert.That(ModuleCatalog.Find("ability.priority.condition"), Is.Null);
    }

    [Test]
    public void ConditionalStandardSentenceRaisesOnlyMatchingMoves()
    {
      var owner = AbilityRemaining3MoveTests.F();
      AbilityRemaining3MoveTests.Rule(owner, Conditional);
      var status = AbilityRemaining3MoveTests.Idle();
      var attack = AbilityRemaining3MoveTests.M("몸통박치기");
      using (EffectScope.Begin(new[] { owner, AbilityRemaining3MoveTests.F() }, new Rules(), () => 0))
      {
        Assert.That(AbilityPhrases.PriorityBonus(owner, status), Is.EqualTo(1));
        Assert.That(AbilityPhrases.PriorityBonus(owner, attack), Is.EqualTo(0));
      }
    }
  }
}
