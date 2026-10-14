using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle
{
  // 『아름다운 육식수』: 자신이 건 「저주」의 대미지는 매직가드 등 간접 대미지 면역으로 막히지 않는다
  [TestFixture]
  public class CursePierceTests
  {
    const string Raw = "상대의 「저주」대미지가 무효화되지 않는다.";

    static Fighter Curser()
    {
      var f = AbilityRemaining3MoveTests.F();
      AbilityRemaining3MoveTests.Rule(f, EffectModules.Normalize(Raw));
      return f;
    }

    static int CurseLoss(Fighter victim, Fighter source, Fighter foe)
    {
      var rules = new Rules();
      victim.Conditions.Curse = true;
      victim.Conditions.CurseSource = source;
      int before = victim.HP;
      using (EffectScope.Begin(new[] { victim, foe }, rules, () => 0))
        Mechanics.Hurt(victim, foe, rules, 25, "저주", new List<string>());
      return before - victim.HP;
    }

    [Test]
    public void RawSentenceBecomesTheStandardSentence()
    {
      Assert.That(EffectModules.Normalize(Raw), Is.EqualTo(AbilityStateModules.CursePierce + "."));
    }

    [Test]
    public void CurseFromTheHolderPiercesMagicGuard()
    {
      var holder = Curser();
      var victim = AbilityRemaining3MoveTests.F("매직가드");
      Assert.That(CurseLoss(victim, holder, holder), Is.GreaterThan(0));
    }

    [Test]
    public void CurseFromAnotherPokemonIsStillBlocked()
    {
      var holder = Curser();
      var other = AbilityRemaining3MoveTests.F();
      var victim = AbilityRemaining3MoveTests.F("매직가드");
      Assert.That(CurseLoss(victim, other, holder), Is.EqualTo(0));
    }

    [Test]
    public void OnlyCurseDamageIsPierced()
    {
      var holder = Curser();
      var victim = AbilityRemaining3MoveTests.F("매직가드");
      victim.Conditions.Curse = true;
      victim.Conditions.CurseSource = holder;
      int before = victim.HP;
      using (EffectScope.Begin(new[] { victim, holder }, new Rules(), () => 0))
        Mechanics.Hurt(victim, holder, new Rules(), 25, "독", new List<string>());
      Assert.That(victim.HP, Is.EqualTo(before));
    }
  }
}
