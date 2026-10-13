using NUnit.Framework;
using System;
using System.Linq;

namespace AABattle.Tests
{
  public class ChanceTurnConditionSplitTests
  {
    [TestCase("필드에 나와 2T째, 자신의 전능력을 올린다.", "장소에 나와 「2」T째일 때", "자신의 전능력을 올린다.")]
    [TestCase("필드에 나와 2T째, 내보내는 공격의 위력을 강화(1.5배)한다.", "장소에 나와 「2」T째일 때", "내보내는 공격의 위력을 강화(1.5배)한다.")]
    [TestCase("필드에 나와 2T째, T의 최후에 「마지막일침」에 의한 추가행동권을 얻는다.", "장소에 나와 「2」T째일 때", "T의 최후에 「마지막일침」에 의한 추가행동권을 얻는다.")]
    [TestCase("「4」배수의 T, 아군이 가장 먼저 「기능확장(성스러운칼)」에 의한 추가행동권을 얻는다.", "「4」배수의 T일 때", "아군이 가장 먼저 「기능확장(성스러운칼)」에 의한 추가행동권을 얻는다.")]
    [TestCase("소속이 《 칼데아 》일 때, 「5」배수의 T, 자신의 기술의 우선도를 「+5」로 할 수 있다.", "소속이 《 칼데아 》일 때", "자신의 기술의 우선도를 「+5」로 할 수 있다.")]
    [TestCase("필드에 나와 2T째, 상대의 속도를 내릴 수 있다.", "장소에 나와 「2」T째일 때", "상대의 속도를 내릴 수 있다.")]
    public void OriginalExamplesSplitToStateConditions(string text, string expectedTrigger, string expectedEffect)
    {
      var split = RuleText.Split(text);
      Assert.That(split.triggers, Does.Contain(expectedTrigger));
      Assert.That(split.effects, Does.Contain(expectedEffect));
    }

    [Test]
    public void MatchingTurnConditionRunsOneShotRankEffectAtTurnStart()
    {
      var owner = AbilityRemaining3MoveTests.F();
      var other = AbilityRemaining3MoveTests.F();
      owner.OnField = true;
      AbilityRemaining3MoveTests.Rule(owner, "필드에 나와 2T째, 자신의 전능력을 올린다.");
      owner.Conditions.FieldTurn = 1;
      owner.Conditions.EntryTriggered = true;
      Assert.That(TurnConditions.Holds(new TriggerSpec { Module = "턴 조건", FieldTurn = 2 }, owner, new Rules { Turn = 1 }), Is.False, "before increment, field-turn condition is false");
      var rules = new Rules { Turn = 1 };
      var move = AbilityRemaining3MoveTests.M("몸통박치기");
      var result = Engine.Resolve(TestData.Db, new[] { owner, other }, new[]
      {
        new BattleCommand { Move = move }, new BattleCommand { Move = move }
      }, rules, null, () => 0);
      Assert.That(result.Fighters[0].Stages.Skip(1).Take(5).Any(stage => stage > 0), Is.True, string.Join("\n", result.Log));
    }

    [Test]
    public void ObserverAnalysisRollRunsForAliveReserveBeforeActions()
    {
      var field = AbilityRemaining3MoveTests.F();
      var observer = AbilityRemaining3MoveTests.F();
      var opponent = AbilityRemaining3MoveTests.F();
      observer.OnField = false;
      observer.Data.potentials = new[]
      {
        new Potential
        {
          id = "종족 ①",
          name = "관측",
          description = "저확률로 상대의 데이터를 해석한다.",
          triggers = new[] { TriggerStructures.ObserverLabel }
        }
      };
      var trainer = new TrainerRecord();
      trainer.command.rank = "A";
      var rules = new Rules();
      rules.Parties[0] = new[] { field, observer };
      rules.Parties[1] = new[] { opponent };
      var states = new[] { new TrainerBattleState { Trainer = trainer }, new TrainerBattleState() };
      var move = AbilityRemaining3MoveTests.M("몸통박치기");
      var result = Engine.Resolve(TestData.Db, new[] { field, opponent }, new[]
      {
        new BattleCommand { Move = move, Order = "공격 지령" },
        new BattleCommand { Move = move }
      }, rules, states, () => 0);
      int analysis = result.Log.FindIndex(line => line.Contains("데이터를 해석한다"));
      int order = result.Log.FindIndex(line => line.Contains("지령!"));
      Assert.That(analysis, Is.GreaterThanOrEqualTo(0), string.Join("\n", result.Log));
      Assert.That(analysis, Is.LessThan(order), string.Join("\n", result.Log));
      Assert.That(BattleRecords.IsAnalyzed(result.Trainers[0], result.Fighters[1]), Is.True);
    }

    [Test]
    public void MatchingFieldTurnScalarAppliesOnlyDuringThatTurn()
    {
      var owner = AbilityRemaining3MoveTests.F();
      var other = AbilityRemaining3MoveTests.F();
      owner.OnField = true;
      var move = AbilityRemaining3MoveTests.M("몸통박치기");
      var rules = new Rules { Turn = 2 };
      AbilityRemaining3MoveTests.Rule(owner, "필드에 나와 2T째, 내보내는 공격의 위력을 강화(1.5배)한다.");
      owner.Conditions.FieldTurn = 2;
      Assert.That(TurnConditions.Holds(new TriggerSpec { Module = "턴 조건", FieldTurn = 2 }, owner, rules), Is.True, "fixture turn condition precheck");
      using (EffectScope.Begin(new[] { owner, other }, rules, () => 0, new[] { new TrainerBattleState(), new TrainerBattleState() }))
        TurnStateEffects.Run(new[] { owner, other }, rules, null);
      Assert.That(PotentialModifiers.Power(owner, other, move, rules, 1), Is.EqualTo(1.5));
    }

    [Test]
    public void MatchingTurnConditionPowerIsAppliedOnceInRealDamageResolution()
    {
      int normal = ResolveDamage(false);
      int boosted = ResolveDamage(true, 1);
      Assert.That(boosted, Is.GreaterThan(normal));
      Assert.That((double)boosted / normal, Is.InRange(1.4, 1.6), "condition scalar must not be multiplied twice");
      int laterTurn = ResolveDamage(true, 2);
      Assert.That((double)laterTurn / normal, Is.InRange(0.95, 1.05), "turn scalar must not leak to a nonmatching field turn");
    }

    [Test]
    public void MixedTurnConditionScalarAndOneShotEffectsBothRunOnce()
    {
      var owner = AbilityRemaining3MoveTests.F();
      var target = AbilityRemaining3MoveTests.F();
      owner.OnField = target.OnField = true;
      owner.Conditions.EntryTriggered = target.Conditions.EntryTriggered = true;
      owner.Conditions.FieldTurn = target.Conditions.FieldTurn = 1;
      AbilityRemaining3MoveTests.Rule(owner, "필드에 나와 2T째, 내보내는 공격의 위력을 강화(1.5배)한다. 상대의 속도를 내린다.");
      var rules = new Rules { Turn = 1 };
      var move = AbilityRemaining3MoveTests.M("몸통박치기");
      int before = target.HP;
      var result = Engine.Resolve(TestData.Db, new[] { owner, target }, new[]
      {
        new BattleCommand { Move = move },
        new BattleCommand { Move = AbilityRemaining3MoveTests.Idle() }
      }, rules, null, () => .99);
      int damage = before - result.Fighters[1].HP;
      int normal = ResolveDamage(false);
      Assert.That(result.Fighters[1].Stages[5], Is.LessThan(0), string.Join("\n", result.Log));
      Assert.That((double)damage / normal, Is.InRange(1.4, 1.6), "mixed scalar applies once");
    }

    [Test]
    public void TurnConditionOptionalEffectPromptsAndRunsAtTheMatchingStart()
    {
      var owner = AbilityRemaining3MoveTests.F();
      var target = AbilityRemaining3MoveTests.F();
      owner.OnField = target.OnField = true;
      owner.Conditions.EntryTriggered = target.Conditions.EntryTriggered = true;
      owner.Conditions.FieldTurn = 1;
      target.Conditions.FieldTurn = 1;
      AbilityRemaining3MoveTests.Rule(owner, "필드에 나와 2T째, 상대의 속도를 내릴 수 있다.");
      int prompts = 0;
      var rules = new Rules { Turn = 1, OptionalEffectPrompt = (a, b, p) => { prompts++; return true; } };
      var move = AbilityRemaining3MoveTests.M("몸통박치기");
      var result = Engine.Resolve(TestData.Db, new[] { owner, target }, new[]
      {
        new BattleCommand { Move = AbilityRemaining3MoveTests.Idle() },
        new BattleCommand { Move = move }
      }, rules, null, () => .99);
      Assert.That(prompts, Is.EqualTo(1), string.Join("\n", result.Log));
      Assert.That(result.Fighters[1].Stages[5], Is.LessThan(0), string.Join("\n", result.Log));
    }

    [Test]
    public void TurnMultipleConditionExplicitFirstAdditionalActionRunsBeforeNormalActions()
    {
      var owner = AbilityRemaining3MoveTests.F();
      var target = AbilityRemaining3MoveTests.F();
      owner.OnField = target.OnField = true;
      owner.Conditions.EntryTriggered = target.Conditions.EntryTriggered = true;
      AbilityRemaining3MoveTests.Rule(owner, "「4」배수의 T, 아군이 가장 먼저 「기능확장(성스러운칼)」에 의한 추가행동권을 얻는다.");
      var rules = new Rules { Turn = 4 };
      var move = AbilityRemaining3MoveTests.M("몸통박치기");
      var result = Engine.Resolve(TestData.Db, new[] { owner, target }, new[]
      {
        new BattleCommand { Move = move },
        new BattleCommand { Move = AbilityRemaining3MoveTests.Idle() }
      }, rules, null, () => .99);
      int grant = result.Log.FindIndex(line => line.Contains("추가행동"));
      int baseAttack = result.Log.FindIndex(line => line.Contains("몸통박치기"));
      Assert.That(grant, Is.GreaterThanOrEqualTo(0), string.Join("\n", result.Log));
      Assert.That(baseAttack, Is.GreaterThan(grant), string.Join("\n", result.Log));
    }

    [Test]
    public void TurnConditionExplicitFinalAdditionalActionRunsAfterNormalActions()
    {
      var owner = AbilityRemaining3MoveTests.F();
      var target = AbilityRemaining3MoveTests.F();
      owner.OnField = target.OnField = true;
      owner.Conditions.EntryTriggered = target.Conditions.EntryTriggered = true;
      owner.Conditions.FieldTurn = target.Conditions.FieldTurn = 1;
      AbilityRemaining3MoveTests.Rule(owner, "필드에 나와 2T째, T의 최후에 「마지막일침」에 의한 추가행동권을 얻는다.");
      var rules = new Rules { Turn = 1 };
      var move = AbilityRemaining3MoveTests.M("몸통박치기");
      var result = Engine.Resolve(TestData.Db, new[] { owner, target }, new[]
      {
        new BattleCommand { Move = move },
        new BattleCommand { Move = AbilityRemaining3MoveTests.Idle() }
      }, rules, null, () => .99);
      int grant = result.Log.FindIndex(line => line.Contains("추가행동"));
      int baseAttack = result.Log.FindIndex(line => line.Contains("몸통박치기"));
      int finalAttack = result.Log.FindLastIndex(line => line.Contains("마지막일침"));
      Assert.That(grant, Is.GreaterThanOrEqualTo(0), string.Join("\n", result.Log));
      Assert.That(finalAttack, Is.GreaterThan(baseAttack), string.Join("\n", result.Log));
    }

    [Test]
    public void AffiliationAndTurnMultipleConditionsReachOptionalPriorityEffect()
    {
      var owner = AbilityRemaining3MoveTests.F();
      var target = AbilityRemaining3MoveTests.F();
      owner.OnField = target.OnField = true;
      owner.Conditions.EntryTriggered = target.Conditions.EntryTriggered = true;
      AbilityRemaining3MoveTests.Rule(owner, "소속이 《 칼데아 》일 때, 「5」배수의 T, 자신의 기술의 우선도를 「+5」로 할 수 있다.");
      int prompts = 0;
      var rules = new Rules { Turn = 5, OptionalEffectPrompt = (a, b, p) => { prompts++; return true; } };
      var move = AbilityRemaining3MoveTests.M("몸통박치기");
      var result = Engine.Resolve(TestData.Db, new[] { owner, target }, new[]
      {
        new BattleCommand { Move = move },
        new BattleCommand { Move = AbilityRemaining3MoveTests.Idle() }
      }, rules, null, () => .99);
      Assert.That(prompts, Is.EqualTo(1), string.Join("\n", result.Log));
      Assert.That(result.Log.Any(line => line.Contains("우선도 +5")), Is.True, string.Join("\n", result.Log));
    }

    [Test]
    public void OptionalPriorityChangesActionOrderOnlyWhenAcceptedOnMatchingTurn()
    {
      Assert.That(PriorityFirstActor(true, 5, out _), Is.EqualTo(0));
      Assert.That(PriorityFirstActor(false, 5, out _), Is.EqualTo(1));
      int prompts;
      Assert.That(PriorityFirstActor(true, 4, out prompts), Is.EqualTo(1));
      Assert.That(prompts, Is.EqualTo(0));
    }

    [Test]
    public void TurnConditionChargeSkipExecutesChargedMoveImmediately()
    {
      var owner = AbilityRemaining3MoveTests.F();
      var target = AbilityRemaining3MoveTests.F();
      owner.OnField = target.OnField = true;
      owner.Conditions.EntryTriggered = target.Conditions.EntryTriggered = true;
      owner.Conditions.FieldTurn = target.Conditions.FieldTurn = 1;
      AbilityRemaining3MoveTests.Rule(owner, "필드에 나와 2T째, 자신의 「모으기기술」의 모으기를 생략하고 내보낼 수 있다.");
      var originalPotentials = owner.Data.potentials;
      var move = AbilityRemaining3MoveTests.M("솔라빔");
      var result = Engine.Resolve(TestData.Db, new[] { owner, target }, new[]
      {
        new BattleCommand { Move = move },
        new BattleCommand { Move = AbilityRemaining3MoveTests.Idle() }
      }, new Rules { Turn = 1 }, null, () => .99);
      Assert.That(result.Fighters[1].HP, Is.LessThan(Mechanics.MaxHP(result.Fighters[1], result.Fighters[0], result.Rules)), string.Join("\n", result.Log));
      Assert.That(result.Fighters[0].Conditions.ChargeMove, Is.Empty);
      Assert.That(result.Log.Any(line => line.Contains("모으기를 생략했다")), Is.True, string.Join("\n", result.Log));
      Assert.That(owner.Data.potentials.Select(p => p.name), Is.EquivalentTo(originalPotentials.Select(p => p.name)));
      Assert.That(result.Fighters[0].Data.potentials.Select(p => p.name), Is.EquivalentTo(originalPotentials.Select(p => p.name)));
      Assert.That(result.Fighters[0].Data.potentials.Any(p => p.name.Contains("임시 모으기 생략")), Is.False);
    }

    [Test]
    public void TurnConditionTemporaryChargeEffectIsRestoredWhenTurnThrows()
    {
      var owner = AbilityRemaining3MoveTests.F();
      var target = AbilityRemaining3MoveTests.F();
      target.Data.@base[5] = 50;
      owner.OnField = target.OnField = true;
      owner.Conditions.EntryTriggered = target.Conditions.EntryTriggered = true;
      owner.Conditions.FieldTurn = target.Conditions.FieldTurn = 1;
      AbilityRemaining3MoveTests.Rule(owner, "필드에 나와 2T째, 자신의 「모으기기술」의 모으기를 생략하고 내보낼 수 있다.");
      var originalNames = owner.Data.potentials.Select(p => p.name).ToArray();
      var move = AbilityRemaining3MoveTests.M("솔라빔");
      Assert.Throws<InvalidOperationException>(() => Engine.Resolve(TestData.Db, new[] { owner, target }, new[]
      {
        new BattleCommand { Move = move },
        new BattleCommand { Move = AbilityRemaining3MoveTests.Idle() }
      }, new Rules { Turn = 1 }, null, () => { throw new InvalidOperationException("forced turn failure"); }));
      Assert.That(owner.Data.potentials.Select(p => p.name), Is.EqualTo(originalNames));
      Assert.That(owner.Data.potentials.Any(p => p.name.Contains("임시 모으기 생략")), Is.False);
    }

    static int PriorityFirstActor(bool accept, int turn, out int prompts)
    {
      var owner = AbilityRemaining3MoveTests.F();
      var target = AbilityRemaining3MoveTests.F();
      target.Data.@base[5] = 200;
      owner.OnField = target.OnField = true;
      owner.Conditions.EntryTriggered = target.Conditions.EntryTriggered = true;
      AbilityRemaining3MoveTests.Rule(owner, "소속이 《 칼데아 》일 때, 「5」배수의 T, 자신의 기술의 우선도를 「+5」로 할 수 있다.");
      int promptCount = 0;
      var rules = new Rules { Turn = turn, OptionalEffectPrompt = (a, b, p) => { promptCount++; return accept; } };
      var move = AbilityRemaining3MoveTests.M("몸통박치기");
      var result = Engine.Resolve(TestData.Db, new[] { owner, target }, new[]
      {
        new BattleCommand { Move = move },
        new BattleCommand { Move = move }
      }, rules, null, () => .99);
      prompts = promptCount;
      int firstOwner = result.Log.FindIndex(line => line.Contains(owner.Data.name + "의 몸통박치기"));
      int firstTarget = result.Log.FindIndex(line => line.Contains(target.Data.name + "의 몸통박치기"));
      Assert.That(firstOwner, Is.GreaterThanOrEqualTo(0), string.Join("\n", result.Log));
      Assert.That(firstTarget, Is.GreaterThanOrEqualTo(0), string.Join("\n", result.Log));
      return firstOwner < firstTarget ? 0 : 1;
    }

    static int ResolveDamage(bool withTurnCondition, int initialFieldTurn = 0)
    {
      var owner = AbilityRemaining3MoveTests.F();
      var target = AbilityRemaining3MoveTests.F();
      owner.OnField = target.OnField = true;
      owner.Conditions.EntryTriggered = target.Conditions.EntryTriggered = true;
      owner.Conditions.FieldTurn = initialFieldTurn;
      target.Conditions.FieldTurn = initialFieldTurn;
      if (withTurnCondition)
        AbilityRemaining3MoveTests.Rule(owner, "필드에 나와 2T째, 내보내는 공격의 위력을 강화(1.5배)한다.");
      var rules = new Rules { Turn = 1 };
      var before = target.HP;
      var move = AbilityRemaining3MoveTests.M("몸통박치기");
      var result = Engine.Resolve(TestData.Db, new[] { owner, target }, new[]
      {
        new BattleCommand { Move = move },
        new BattleCommand { Move = AbilityRemaining3MoveTests.Idle() }
      }, rules, null, () => .99);
      return before - result.Fighters[1].HP;
    }
  }
}
