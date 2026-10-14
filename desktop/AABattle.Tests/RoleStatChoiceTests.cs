using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace AABattle
{
  // 「임의의 능력」 역할(탐사대원·야생 포켓몬)은 사용자가 고르고, 한 번만 오른다
  [TestFixture]
  public class RoleStatChoiceTests
  {
    static Fighter Holder(string role, int amount)
    {
      return new Fighter(new Pokemon
      {
        id = "role-choice-" + role,
        name = role + " 보유자",
        level = 100,
        types = new[] { "노말" },
        @base = new[] { 100, 100, 100, 100, 100, 100 },
        iv = new[] { 31, 31, 31, 31, 31, 31 },
        moves = new[] { "몸통박치기" },
        potentials = new[]
        {
          new Potential
          {
            id = "역할",
            name = role,
            description = EffectNormalizer.CanonicalRank("자신의 임의의 능력", amount),
            triggers = new[] { TriggerStructures.EntryLabel }
          }
        }
      });
    }

    // 실제 배틀처럼 효과 실행 범위 안에서 등장시킨다
    static void Enter(Fighter fighter, Rules rules)
    {
      var other = Holder("상대역", 1);
      Mechanics.SetParties(rules, new[] { fighter, other });
      using (EffectScope.Begin(new[] { fighter, other }, rules, () => 0, new[] { new TrainerBattleState(), new TrainerBattleState() }))
        EntryTriggers.Enter(fighter, other, rules, () => 0, EntryReason.Lead, new List<string>().Add);
    }

    // 죽어내밀기 직후(다음 Resolve 전)에 이미 처리되고, 다음 Resolve에서 다시 일어나지 않는다
    [Test]
    public void ReplacementEntryAppliesImmediatelyAndOnce()
    {
      var fighter = Holder("탐사대원", 1);
      var other = Holder("상대역", 1);
      other.Data.potentials = new Potential[0];
      int asked = 0;
      var rules = new Rules { ChooseStat = (s, f, list) => { asked++; return "속도"; } };
      var fighters = new[] { fighter, other };
      Mechanics.SetParties(rules, fighters);
      var states = new[] { new TrainerBattleState(), new TrainerBattleState() };
      using (EffectScope.Begin(fighters, rules, () => 0, states))
      {
        EntryTriggers.Enter(fighter, other, rules, () => 0, EntryReason.Replacement, new List<string>().Add, null, true);
        fighter.Conditions.EntryTriggered = true;
      }
      Assert.That(asked, Is.EqualTo(1));
      Assert.That(fighter.Stages[5], Is.EqualTo(1));
      var idle = TestData.Db.moves.First(m => m.name == "몸통박치기");
      var turn = Engine.Resolve(TestData.Db, fighters, new[] { idle, idle }, rules, () => .5);
      Assert.That(asked, Is.EqualTo(1));
      Assert.That(turn.Fighters[0].Stages[5], Is.EqualTo(1));
    }

    [TestCase("탐사대원", 1)]
    [TestCase("야생 포켓몬", 2)]
    public void ChosenStatRisesOnce(string role, int amount)
    {
      var fighter = Holder(role, amount);
      int asked = 0;
      var rules = new Rules { ChooseStat = (s, f, list) => { asked++; return "특방"; } };
      Enter(fighter, rules);
      Assert.That(asked, Is.EqualTo(1));
      Assert.That(fighter.Stages[4], Is.EqualTo(amount));
      Assert.That(fighter.Stages.Sum(), Is.EqualTo(amount));
    }

    [TestCase("탐사대원")]
    [TestCase("야생 포켓몬")]
    public void NoChooserOrDeclineRaisesNothing(string role)
    {
      var none = Holder(role, 1);
      Enter(none, new Rules());
      Assert.That(none.Stages.Sum(), Is.EqualTo(0));
      var declined = Holder(role, 1);
      Enter(declined, new Rules { ChooseStat = (s, f, list) => null });
      Assert.That(declined.Stages.Sum(), Is.EqualTo(0));
    }
  }
}
