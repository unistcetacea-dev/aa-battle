using System;
using System.Linq;
using NUnit.Framework;

namespace AABattle
{
  [TestFixture]
  public class BenchEventsTests
  {
    static int ids;

    static Fighter F(params Potential[] potentials)
    {
      return new Fighter(new Pokemon
      {
        id = "bench-test-" + (++ids),
        name = "벤치검사" + ids,
        level = 100,
        types = new[] { "노말" },
        @base = new[] { 300, 100, 100, 100, 100, 100 },
        iv = new[] { 31, 31, 31, 31, 31, 31 },
        moves = new[] { "몸통박치기" },
        potentials = potentials
      });
    }

    static Move Idle()
    {
      return new Move { name = "미등록", category = "변화", types = new[] { "노말" }, target = "자신", accuracy = 100, tags = new string[0] };
    }

    static Potential ReverseTurn()
    {
      var type = ExclusiveTypes.Find("역선회");
      return new Potential
      {
        id = "전용",
        name = "역선회 검사",
        description = ExclusiveTypes.Filled(type, type.Effect),
        activation = type.Activation,
        uses = type.Uses,
        triggers = ExclusiveTypes.Filled(type, type.Trigger).Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries)
      };
    }

    static BattleCommand Stay(Fighter bench)
    {
      return new BattleCommand { Move = Idle(), Party = new[] { bench } };
    }

    [Test]
    public void OffFieldObserverReceivesARealHitEvent()
    {
      var active = F();
      var bench = F(new Potential
      {
        id = "종족 ①",
        name = "대기 관측",
        description = "자신의 공격이 명중했을 때, 자신의 「공격」이 오른다. 이 포텐셜은 자신이 필드에 없어도 발동할 수 있다.",
        triggers = new[] { "자신의 공격이 명중했을 때" }
      });
      bench.OnField = false;
      var foe = F();
      var rules = new Rules();
      Mechanics.SetParties(rules, new[] { active, foe }, new[] { new[] { active, bench }, new[] { foe } });
      using (EffectScope.Begin(new[] { active, foe }, rules, () => .5, new[] { new TrainerBattleState(), new TrainerBattleState() }, TestData.Db))
      {
        BenchEvents.Dispatch(new TriggerFact { Kind = "명중", Move = AbilityRemaining3MoveTests.M("몸통박치기"), User = active, Damaged = true }, active, foe, "검사 공격 명중", _ => { });
      }

      Assert.That(bench.Stages[1], Is.EqualTo(1));
      Assert.That(active.Stages[1], Is.EqualTo(0));
    }

    [Test]
    public void StalkingSwitchesInFromTheBenchWhenTheOpposingAceEnters()
    {
      var active = F();
      var type = ExclusiveTypes.Find("스토킹");
      var stalking = new Potential
      {
        id = "전용",
        name = "스토킹 검사",
        description = ExclusiveTypes.Filled(type, type.Effect),
        activation = type.Activation,
        uses = type.Uses,
        triggers = ExclusiveTypes.Filled(type, type.Trigger).Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries)
      };
      var bench = F(stalking);
      bench.OnField = false;
      var ace = F(new Potential { id = "역할", name = "에이스", description = "역할", triggers = new string[0] });
      var rules = new Rules { OptionalEffectPrompt = (owner, opponent, potential) => true };
      var result = Engine.Resolve(TestData.Db, new[] { active, ace }, new[] { Stay(bench), Stay(F()) }, rules,
        new[] { new TrainerBattleState(), new TrainerBattleState() }, () => .5, request => request.Candidates.First());

      Assert.That(result.Fighters[0].Data.name, Is.EqualTo(bench.Data.name), string.Join("\n", result.Log));
      Assert.That(result.Log.Any(x => x.Contains("스토킹 검사") && x.Contains("등장")), Is.True, string.Join("\n", result.Log));
    }

    [Test]
    public void ReverseTurnObservesTheFieldAllysSecondEntryTurn()
    {
      var active = F();
      var bench = F(ReverseTurn());
      bench.OnField = false;
      var foe = F();
      var rules = new Rules { OptionalEffectPrompt = (owner, opponent, potential) => true };
      var first = Engine.Resolve(TestData.Db, new[] { active, foe }, new[] { Stay(bench), Stay(F()) }, rules,
        new[] { new TrainerBattleState(), new TrainerBattleState() }, () => .5, request => request.Candidates.First());
      Assert.That(first.Fighters[0].Data.name, Is.EqualTo(active.Data.name), string.Join("\n", first.Log));
      var second = Engine.Resolve(TestData.Db, first.Fighters, new[] { Stay(bench), Stay(F()) }, first.Rules,
        first.Trainers, () => .5, request => request.Candidates.First());

      Assert.That(second.Fighters[0].Data.name, Is.EqualTo(bench.Data.name), string.Join("\n", second.Log));
    }

    [Test]
    public void BenchRobotCanBeDeclaredAndItsHealingIsRepaid()
    {
      var active = F();
      active.HP -= 40;
      var bench = F(new Potential
      {
        id = "전용",
        name = "대기 메이드",
        activation = "선행",
        uses = "1/시",
        description = "자신의 공격이 명중했을 때, 자진의 포켓몬의 체력을 가득 회복한다.\n회복한 체력만큼 턴 끝에 대미지를 입는다(빈사 불가).\n이 포텐셜은 자신이 필드에 없어도 발동할 수 있다.",
        triggers = new[] { "자신의 공격이 명중했을 때" }
      });
      bench.OnField = false;
      var foe = F();
      var command = new BattleCommand
      {
        Move = AbilityRemaining3MoveTests.M("몸통박치기"),
        PreInputs = new[] { "대기 메이드" },
        Party = new[] { bench }
      };
      var result = Engine.Resolve(TestData.Db, new[] { active, foe }, new[] { command, Stay(F()) }, new Rules(),
        new[] { new TrainerBattleState(), new TrainerBattleState() }, () => .5, request => request.Candidates.First());

      Assert.That(result.Log.Any(x => x.Contains("대기 메이드") && x.Contains("효과 적용")), Is.True, string.Join("\n", result.Log));
      Assert.That(result.Log.Any(x => x.Contains("회복한 체력의 반납")), Is.True, string.Join("\n", result.Log));
      Assert.That(result.Fighters[0].HP, Is.EqualTo(active.HP), string.Join("\n", result.Log));
    }

    [Test]
    public void BenchPreInputRequiresAnOffFieldOrPartyWideUseCondition()
    {
      var active = F();
      var bench = F(new Potential { id = "전용", name = "대기 전용", activation = "선행", uses = "1/시", description = "자신의 공격이 명중했을 때, 자신의 「공격」이 오른다.", triggers = new[] { "자신의 공격이 명중했을 때" } });
      bench.OnField = false;
      var rules = new Rules();
      Mechanics.SetParties(rules, new[] { active, F() }, new[] { new[] { active, bench }, new[] { F() } });
      var usage = new PotentialUsage();

      Assert.That(BenchEvents.TryDeclarePreInput(rules, 0, "대기 전용", active, usage, _ => { }), Is.False);
      Assert.That(usage.Declared, Is.Empty);
    }

    [Test]
    public void BenchPreInputIgnoresFaintedHolders()
    {
      var active = F();
      var bench = F(new Potential { id = "전용", name = "대기 관측", activation = "선행", uses = "1/시", description = "자신의 공격이 명중했을 때, 자신의 「공격」이 오른다. 이 포텐셜은 자신이 필드에 없어도 발동할 수 있다.", triggers = new[] { "자신의 공격이 명중했을 때" } });
      bench.OnField = false;
      bench.HP = 0;
      var rules = new Rules();
      Mechanics.SetParties(rules, new[] { active, F() }, new[] { new[] { active, bench }, new[] { F() } });

      Assert.That(BenchEvents.TryDeclarePreInput(rules, 0, "대기 관측", active, new PotentialUsage(), _ => { }), Is.False);
    }

    [Test]
    public void PivotGrantDistinguishesAttackOnlyFromAllMoves()
    {
      var status = new Move { category = "변화" };
      var attack = new Move { category = "물리" };
      Assert.That(PivotGrantRules.AppliesTo("자신의 공격기술에 유턴 효과를 부여한다.", status), Is.False);
      Assert.That(PivotGrantRules.AppliesTo("자신의 공격기술에 유턴 효과를 부여한다.", attack), Is.True);
      Assert.That(PivotGrantRules.AppliesTo("자신의 기술에 유턴 효과를 부여한다.", status), Is.True);
    }

    [Test]
    public void PendingRepaymentIsAppliedAfterItsOwnerSwitchesToTheBench()
    {
      var outgoing = F();
      var initialHp = outgoing.HP;
      outgoing.Conditions.Repay = 20;
      var incoming = F();
      incoming.OnField = false;
      var foe = F();
      var result = Engine.Resolve(TestData.Db, new[] { outgoing, foe }, new[]
      {
        new BattleCommand { SwitchIn = incoming, Party = new[] { outgoing, incoming } },
        Stay(F())
      }, new Rules(), new[] { new TrainerBattleState(), new TrainerBattleState() }, () => .5, request => request.Candidates.First());
      var reserve = result.Rules.Parties[0].First(x => Mechanics.SameMember(x, outgoing));

      Assert.That(result.Fighters[0].Data.name, Is.EqualTo(incoming.Data.name));
      Assert.That(reserve.HP, Is.EqualTo(initialHp - 20), string.Join("\n", result.Log));
      Assert.That(reserve.Conditions.Repay, Is.EqualTo(0));
    }
  }
}
