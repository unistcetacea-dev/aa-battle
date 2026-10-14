using System;
using System.Linq;
using NUnit.Framework;

namespace AABattle.Tests
{
  [TestFixture]
  public class ChaldeaFollowupTests
  {
    static Fighter F(string name, params Potential[] potentials)
    {
      return new Fighter(new Pokemon
      {
        id = "followup-" + name,
        name = name,
        level = 100,
        types = new[] { "노말" },
        @base = new[] { 300, 100, 100, 100, 100, 100 },
        iv = new[] { 31, 31, 31, 31, 31, 31 },
        moves = new[] { "몸통박치기" },
        potentials = potentials
      });
    }

    static Potential P(string slot, string name, string effect, string uses = "", params string[] triggers)
    {
      return new Potential
      {
        id = slot,
        name = name,
        description = effect,
        uses = uses,
        triggers = triggers.Length == 0 ? new[] { TriggerStructures.AlwaysLabel } : triggers
      };
    }

    static Move Idle()
    {
      return new Move { name = "대기", category = "변화", target = "자신", types = new[] { "노말" }, accuracy = 100, tags = new string[0] };
    }

    [Test]
    public void NamedChargeSkipAppliesOnlyToTheNamedMoveInABattle()
    {
      var owner = F("충전", P("부수", "태양", "자신의 「솔라빔」을 「모으기」를 생략하고 내보낼 수 있다"));
      var foe = F("상대");
      var rules = new Rules();
      var first = Engine.Resolve(TestData.Db, new[] { owner, foe },
        new[] { TestData.Db.moves.First(x => x.name == "솔라빔"), Idle() }, rules, () => .5);

      Assert.That(first.Fighters[0].Conditions.ChargeMove, Is.Empty);
      Assert.That(first.Log.Any(x => x.Contains("모으기를 생략했다")), Is.True, string.Join("\n", first.Log));

      var next = Engine.Resolve(TestData.Db, first.Fighters,
        new[] { TestData.Db.moves.First(x => x.name == "공중날기"), Idle() }, first.Rules, () => .5);
      Assert.That(next.Fighters[0].Conditions.ChargeMove, Is.EqualTo("공중날기"), string.Join("\n", next.Log));
    }

    [Test]
    public void FollowupPhrasesAndGoldenChanceUseTheirSpecificModules()
    {
      Assert.That(EffectNormalizer.Normalize("아군의 『전용』을 자신의 『전용』으로 사용할 수 있다"),
        Is.EqualTo(ExclusiveBorrowModules.Sentence));
      Assert.That(EffectModules.Classify(ForcedCounterModules.Sentence), Is.EqualTo(ForcedCounterModules.Kind));
      Assert.That(TriggerModules.Parse("상대의 『역할』이 발동했을 때").Module,
        Is.EqualTo(ForcedCounterModules.RoleActivationKind));
      Assert.That(BondModules.UpgradeRegainChance(.10, true), Is.EqualTo(.20));
      Assert.That(BondModules.UpgradeRegainChance(.20, true), Is.EqualTo(.50));
      Assert.That(BondModules.UpgradeRegainChance(.10, false), Is.EqualTo(.10));
      Assert.That(EffectNormalizer.Normalize("공격 이외의 대미지를 무효화한다.(저주등의 자해, 반동 기술 제외)"),
        Is.EqualTo("자신은 공격 이외의 대미지를 받지 않는다."));
    }

    [Test]
    public void BorrowedExclusiveConsumesItsOwnersUseAndIsRecordedForTheBorrower()
    {
      var borrowerPotential = P("고유", "철여제의 징세", ExclusiveBorrowModules.Sentence, "1/시", TriggerStructures.TurnStartLabel);
      var sourceExclusive = P(TeamRules.ExclusiveSlot, "아군의 칼날", "자신의 「공격」을 크게 올린다", "1/시");
      var owner = F("메어리", borrowerPotential);
      var ally = F("동료", sourceExclusive);
      var foe = F("상대");
      var trainer = new TrainerRecord { name = "메어리" };
      var states = new[] { new TrainerBattleState { Trainer = trainer }, new TrainerBattleState() };
      var rules = new Rules
      {
        Parties = new[] { new[] { owner, ally }, new[] { foe } },
        ChooseBondPotential = (fighter, options) => 0
      };
      var start = TriggerStructures.LabelEvent(owner, TriggerStructures.TurnStartLabel, "시작", null);
      var log = new System.Collections.Generic.List<string>();
      start.Log = log.Add;
      using (EffectScope.Begin(new[] { owner, foe }, rules, () => .5, states, TestData.Db))
      {
        PotentialQueue.Raise(new[] { start });
        var call = new EffectCall
        {
          Source = new PotentialInstance { Owner = owner, Slot = "고유", Effect = ExclusiveBorrowModules.Sentence },
          Event = start, Line = ExclusiveBorrowModules.Sentence, Rules = rules, Rng = () => .5, Log = log
        };
        Assert.That(PotentialExecutors.Find(call).Run(call), Is.EqualTo(EffectOutcome.Failed), "원래 주인의 1/시를 소모했으므로 다시 빌릴 수 없다");
      }

      string key = PotentialKeys.For(ally.Data, sourceExclusive);
      Assert.That(states[0].Usage.Battle.ContainsKey(key), Is.True, string.Join("\n", log));
      Assert.That(BondModules.Own(states[0], owner).Any(x => x.Name == sourceExclusive.name), Is.True, string.Join("\n", log));
      Assert.That(BondModules.Own(states[0], ally).Any(x => x.Name == sourceExclusive.name), Is.False);
      Assert.That(owner.Stages[1], Is.EqualTo(2), "빌린 전용의 효과는 빌린 포켓몬에게 적용한다");
      Assert.That(ally.Stages[1], Is.EqualTo(0));
    }

    [Test]
    public void OpponentRoleActivationTriggersForcedCounters()
    {
      var observer = F("알리사");
      var ordinary = F("아군", P("격", "대노저격", "저확률로 자신의 「공격」을 크게 올린다", "", TriggerStructures.TurnStartLabel), P("고유", "표적", ForcedCounterModules.Sentence, "", "상대의 『역할』이 발동했을 때"), P("격", "대노반격", "저확률로 자신의 「방어」를 크게 올린다", "", TriggerStructures.TurnStartLabel));
      var excluded = F("반격", P("격", "대저격반격", "저확률로 자신의 「공격」을 크게 올린다", "", TriggerStructures.TurnStartLabel));
      var actor = F("상대 역할", P("역할", "주장", "자신의 「공격」을 크게 올린다"));
      var rules = new Rules { Parties = new[] { new[] { observer, ordinary, excluded }, new[] { actor } } };
      var log = new System.Collections.Generic.List<string>();
      var states = new[] { new TrainerBattleState(), new TrainerBattleState() };
      using (EffectScope.Begin(new[] { ordinary, actor }, rules, () => .9, states, TestData.Db))
      {
        var start = TriggerStructures.LabelEvent(ordinary, TriggerStructures.TurnStartLabel, "T 개시", null);
        start.Log = log.Add;
        PotentialQueue.Raise(new[] { start });
        Assert.That(ordinary.Stages[1], Is.EqualTo(0), string.Join("\n", log));
        TriggerModules.Raise(new TriggerFact { Kind = "특성 발동", Method = "역할" }, actor, ordinary, "역할 발동", log.Add);
        Assert.That(ordinary.Stages[1], Is.EqualTo(0), "원래 트리거 전에는 즉시 실행하지 않는다");
        start = TriggerStructures.LabelEvent(ordinary, TriggerStructures.TurnStartLabel, "같은 T 개시 판정", null);
        start.Log = log.Add;
        PotentialQueue.Raise(new[] { start });
        Assert.That(ordinary.Stages[1], Is.GreaterThan(0), "역할 발동 뒤 원래 트리거가 맞으면 확률을 보장한다");
        states[0].Usage.EndTurn();
        start = TriggerStructures.LabelEvent(ordinary, TriggerStructures.TurnStartLabel, "다음 T 개시", null);
        start.Log = log.Add;
        PotentialQueue.Raise(new[] { start });
      }
      Assert.That(ordinary.Stages[1], Is.GreaterThan(0), string.Join("\n", log));
      Assert.That(ordinary.Stages[1], Is.EqualTo(2), "다음 턴에는 확률 보장이 끝나 높은 굴림이 실패한다");
      Assert.That(excluded.Stages[1], Is.EqualTo(0), "대저격반격은 보장 대상이 아니다");
      Assert.That(ordinary.Stages[2], Is.EqualTo(0), "같은 장소의 실제 대노반격도 원래 확률을 유지한다");
    }

    [TestCase("대노저격", "저확률로 자신의 기술이 「필중」한다.", RollPoint.Action)]
    [TestCase("대노박격", "저확률로 자신이 주는 대미지를 강화(2배)한다.", RollPoint.Action)]
    [TestCase("대노추격", "저확률로 자신의 기술에 「추격」 효과를 부여한다.", RollPoint.Selection)]
    public void ForcedCountersKeepTheirStateConditionAndOriginalRollPoint(string name, string effect, RollPoint point)
    {
      var owner = F("격", P("격", name, effect, "", "적진에 「노말」 포켓몬이 있을 때"),
        P("고유", "표적", ForcedCounterModules.Sentence, "", "상대의 『역할』이 발동했을 때"));
      var foe = F("역할", P("역할", "선봉", "자신의 「공격」을 올린다", "", TriggerStructures.TurnStartLabel));
      var rules = new Rules();
      var states = new[] { new TrainerBattleState(), new TrainerBattleState() };
      var log = new System.Collections.Generic.List<string>();
      var move = TestData.Db.moves.First(x => x.name == "몸통박치기");
      Action roll = () =>
      {
        if (point == RollPoint.Action)
          StateRolls.Action(TestData.Db, new[] { owner, foe }, 0, move, rules, () => .99, log);
        else
          StateRolls.Selection(TestData.Db, new[] { owner, foe }, new[] { new BattleCommand { Move = move }, new BattleCommand { Move = Idle() } }, rules, () => .99, log);
      };
      using (EffectScope.Begin(new[] { owner, foe }, rules, () => .99, states, TestData.Db))
      {
        roll();
        Assert.That(owner.Conditions.Modifiers.Any(m => m.Rolled), Is.False);
        var start = TriggerStructures.LabelEvent(foe, TriggerStructures.TurnStartLabel, "역할 실행", null);
        start.Log = log.Add;
        PotentialQueue.Raise(new[] { start });
        Assert.That(foe.Stages[1], Is.EqualTo(1), string.Join("\n", log));
        Assert.That(owner.Conditions.Modifiers.Any(m => m.Rolled), Is.False, "역할 발동만으로 격을 즉시 실행하지 않는다");
        foe.Data.types = new[] { "격투" };
        roll();
        Assert.That(owner.Conditions.Modifiers.Any(m => m.Rolled), Is.False, "원래 타입 조건이 불충족이면 실행하지 않는다");
        foe.Data.types = new[] { "노말" };
        roll();
        Assert.That(owner.Conditions.Modifiers.Any(m => m.Rolled), Is.True, string.Join("\n", log));
        owner.Conditions.Modifiers.Clear();
        states[0].Usage.EndTurn();
        roll();
        Assert.That(owner.Conditions.Modifiers.Any(m => m.Rolled), Is.False, "다음 턴에는 높은 난수에 실패한다");
      }
    }

    [Test]
    public void RoleActivationDiscriminatorPreservesOrdinaryAbilityActivationTriggers()
    {
      var ordinary = TriggerModules.Parse("자신의 「위협」 특성이 발동했을 때");
      var anyAbility = TriggerModules.Parse("자신의 특성이 발동했을 때");
      var role = TriggerModules.Parse("상대의 『역할』이 발동했을 때");
      var owner = F("특성 사용자");
      Assert.That(ordinary.IsEvent, Is.True);
      Assert.That(TriggerModules.Matches(ordinary, new TriggerFact { Kind = "특성 발동", Method = "위협", Self = true }, owner), Is.True);
      Assert.That(TriggerModules.Matches(ordinary, new TriggerFact { Kind = "특성 발동", Method = "역할", Self = true }, owner), Is.False);
      Assert.That(TriggerModules.Matches(anyAbility, new TriggerFact { Kind = "특성 발동", Method = "위협", Self = true }, owner), Is.True);
      Assert.That(TriggerModules.Matches(anyAbility, new TriggerFact { Kind = "특성 발동", Method = "역할", Self = true }, owner), Is.False);
      Assert.That(TriggerModules.Matches(role, new TriggerFact { Kind = "특성 발동", Method = "역할", Self = false }, owner), Is.True);
      Assert.That(TriggerModules.Matches(role, new TriggerFact { Kind = "특성 발동", Method = "위협", Self = false }, owner), Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FailedOrExhaustedForceDoesNotGuaranteeCounterProbability(bool exhausted)
    {
      var force = P("고유", "표적", (exhausted ? "" : "저확률로 ") + ForcedCounterModules.Sentence,
        exhausted ? "1/시" : "", "상대의 『역할』이 발동했을 때");
      var owner = F("격", force, P("격", "대노저격", "저확률로 자신의 기술이 「필중」한다.", "", "적진에 「노말」 포켓몬이 있을 때"));
      var foe = F("상대");
      var rules = new Rules();
      var states = new[] { new TrainerBattleState(), new TrainerBattleState() };
      if (exhausted)
        states[0].Usage.Use(PotentialKeys.For(owner.Data, force), force.uses);
      var log = new System.Collections.Generic.List<string>();
      using (EffectScope.Begin(new[] { owner, foe }, rules, () => .99, states, TestData.Db))
      {
        TriggerModules.Raise(new TriggerFact { Kind = "특성 발동", Method = "역할" }, foe, owner, "역할", log.Add);
        StateRolls.Action(TestData.Db, new[] { owner, foe }, 0, TestData.Db.moves.First(x => x.name == "몸통박치기"), rules, () => .99, log);
        Assert.That(owner.Conditions.Modifiers.Any(m => m.Rolled), Is.False, string.Join("\n", log));
      }
    }

    [Test]
    public void GoldenRaisesLowBondRegainChanceToMediumForYukinari()
    {
      var owner = F("유대");
      var trainer = new TrainerRecord { name = "유키나리" };
      var states = new[] { new TrainerBattleState { Trainer = trainer }, new TrainerBattleState() };
      var rules = new Rules { ChooseBondPotential = (fighter, options) => 0 };
      var limited = new PotentialInstance
      {
        Owner = owner, Key = "limited", Name = "고유", Slot = "고유", Uses = "1/시",
        Effect = "자신의 「공격」을 크게 올린다"
      };
      BondModules.Record(states[0], limited, owner);
      states[0].Usage.Use(limited.Key, limited.Uses);
      var regain = new PotentialInstance
      {
        Owner = owner, Key = "soul", Name = "영혼의 유대", Slot = "유대",
        Effect = "저확률로 포텐셜을 다시 받을 수 있다"
      };
      var eventNow = TriggerStructures.LabelEvent(owner, TriggerStructures.TurnStartLabel, "시작", null);
      using (EffectScope.Begin(new[] { owner, F("상대") }, rules, () => .25, states, TestData.Db))
      {
        var regainCall = new EffectCall { Source = regain, Event = eventNow, Line = "저확률로 포텐셜을 다시 받을 수 있다", Rules = rules, Rng = () => .25 };
        var regainExecutor = PotentialExecutors.Find(regainCall);
        Assert.That(regainExecutor.Run(regainCall), Is.EqualTo(EffectOutcome.Failed), "저확률 20%는 0.25에 실패");

        owner.Data.potentials = new[] { P("고유", "황금", "트레이너가 「유키나리」일 때, 포텐셜을 다시 받을 확률이 오른다") };
        Assert.That(regainExecutor.Run(regainCall), Is.EqualTo(EffectOutcome.Success), "중확률 50%는 0.25에 성공");
      }
      Assert.That(states[0].Usage.Battle.ContainsKey(limited.Key), Is.False);
    }
  }
}
