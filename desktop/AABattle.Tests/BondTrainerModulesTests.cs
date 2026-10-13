using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle
{
  [TestFixture]
  public class BondTrainerModulesTests
  {
    static Fighter Fighter(string name, int[] stats, params Potential[] potentials)
    {
      return new Fighter(new Pokemon
      {
        id = name,
        name = name,
        level = 50,
        types = new[] { "노말" },
        @base = stats,
        iv = new int[6],
        moves = new string[0],
        potentials = potentials
      });
    }

    static PotentialInstance Source(Fighter owner, string effect)
    {
      return new PotentialInstance
      {
        Owner = owner,
        Pokemon = new Potential { id = "유대", name = "검사", description = effect },
        Name = "검사",
        Slot = "유대",
        Effect = effect
      };
    }

    static EffectOutcome Run(string line, PotentialInstance source, Rules rules, TrainerBattleState[] states, Fighter[] fighters, List<string> log)
    {
      string kind = BondModules.Classify(line) == BondModules.KindHot ? "유대: 모든 종족치 ++" : "트레이너 자질 변화";
      var executor = PotentialExecutors.All.FirstOrDefault(x => x.Name == kind);
      Assert.That(executor, Is.Not.Null, "No effect executor for: " + line);
      using (EffectScope.Begin(fighters, rules, () => 0, states))
        return executor.Run(new EffectCall { Source = source, Line = line, Rules = rules, Rng = () => 0, Log = log });
    }

    [TestCase("자신의 모든 종족치에 「++」를 부여한다", "자신의 모든 종족치에 「++」를 부여한다.", BondModules.KindHot)]
    public void HotBondSentenceHasAnExecutableModule(string raw, string expected, string kind)
    {
      Assert.That(EffectNormalizer.Normalize(raw), Is.EqualTo(expected));
      Assert.That(EffectModules.Classify(expected), Is.EqualTo(kind));
    }

    [Test]
    public void HotBondMovesAllSixStatsIntoTheirCurrentGradePlusPlus()
    {
      var fighter = Fighter("유대", new[] { 30, 98, 122, 150, 35, 199 });
      var log = new List<string>();
      var outcome = Run(BondModules.HotSentence, Source(fighter, BondModules.HotSentence), new Rules(), null, new[] { fighter, Fighter("상대", new[] { 100, 100, 100, 100, 100, 100 }) }, log);
      Assert.That(outcome, Is.EqualTo(EffectOutcome.Success));
      Assert.That(fighter.Data.@base, Is.EqualTo(new[] { 39, 109, 139, 169, 39, 199 }));
      Assert.That(fighter.Conditions.OriginalBase, Is.EqualTo(new[] { 30, 98, 122, 150, 35, 199 }));
    }

    [Test]
    public void DirectiveHistoryCanBeSelectedForRegain()
    {
      var fighter = Fighter("지령 사용자", new[] { 100, 100, 100, 100, 100, 100 });
      var state = new TrainerBattleState { Trainer = new TrainerRecord { name = "지령 트레이너" } };
      state.UsedOrders.Add("숙달 공격 지령");
      BondModules.RecordOrder(state, fighter, "숙달 공격 지령");
      Assert.That(BondModules.Own(state, fighter).Select(x => x.Name), Is.EqualTo(new[] { "숙달 공격 지령" }));
      Assert.That(BondModules.Regainable(state, fighter).Single().IsOrder, Is.True);
      state.UsedOrders.Remove("숙달 공격 지령");
      Assert.That(BondModules.Regainable(state, fighter), Is.Empty);
    }

    [Test]
    public void RoleAppointmentAddsAnAdditionalRoleAndRoleChangeTriggerIsParsed()
    {
      string effect = RoleModules.NormalizeEffect("자신을 『탐사대원』으로 임명할 수 있다");
      Assert.That(effect, Is.EqualTo("자신을 『탐사대원』로 임명할 수 있다."));
      Assert.That(EffectModules.Classify(effect), Is.EqualTo(RoleModules.EffectKind));
      string trigger = TriggerModules.Normalize("『에이스』『탐사대장』이 되면");
      Assert.That(trigger, Is.EqualTo("자신이 『에이스』『탐사대장』이 되었을 때"));
      var parsed = TriggerModules.Parse(trigger);
      Assert.That(parsed.Module, Is.EqualTo(RoleModules.EventModule));
      Assert.That(RoleModules.Matches(parsed, new TriggerFact { Kind = RoleModules.EventModule, Method = "에이스" }), Is.True);
    }

    [Test]
    public void AptitudeChangeAffectsCriticalAccessWithoutChangingTrainerData()
    {
      var trainer = new TrainerRecord { name = "자질 변화" };
      trainer.command.rank = "B";
      var state = new TrainerBattleState { Trainer = trainer };
      var fighter = Fighter("통솔자", new[] { 100, 100, 100, 100, 100, 100 });
      var opponent = Fighter("상대", new[] { 100, 100, 100, 100, 100, 100 });
      var rules = new Rules { Parties = new[] { new[] { fighter }, new[] { opponent } } };
      string line = "트레이너의 「지시」을 1랭크 올린다.";
      var log = new List<string>();
      var result = Run(line, Source(fighter, line), rules, new[] { state, new TrainerBattleState() }, new[] { fighter, opponent }, log);
      Assert.That(result, Is.EqualTo(EffectOutcome.Success));
      Assert.That(TrainerAccessRules.CommandCriticalBonus(trainer, 0, state), Is.EqualTo(1));
      Assert.That(trainer.command.rank, Is.EqualTo("B"));
      Assert.That(TrainerAccessRules.CommandCriticalBonus(trainer), Is.EqualTo(0));
    }
  }
}
