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

    // 대기석용 진입형: 아군과 임의교대해 필드에 나온다(entry=false는 물러나는 둔주형)
    static Potential ReverseTurn(bool entry = true)
    {
      var type = ExclusiveTypes.Find("역선회");
      return new Potential
      {
        id = "전용",
        name = "역선회 검사",
        description = entry ? ExclusiveTypes.Filled(type, type.Effect) : "아군과 임의교대할 수 있다.",
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

    static bool Ask(Func<int, bool> answer, int count)
    {
      return answer == null || answer(count);
    }

    // 여러 T를 이어서 돌리고 매 T 종료 때의 필드 선두 이름과 선택 확인 횟수를 기록한다
    static string[] PlayTurns(int turns, Func<int, bool> answer, out int prompts, out string log, Fighter bench)
    {
      var active = F();
      bench.OnField = false;
      int asked = 0;
      var rules = new Rules { OptionalEffectPrompt = (owner, opponent, potential) => Ask(answer, ++asked) };
      var names = new string[turns];
      var fighters = new[] { active, F() };
      var trainers = new[] { new TrainerBattleState(), new TrainerBattleState() };
      var all = new System.Text.StringBuilder();
      for (int i = 0; i < turns; i++)
      {
        var result = Engine.Resolve(TestData.Db, fighters, new[] { Stay(bench), Stay(F()) }, rules, trainers, () => .5, request => request.Candidates.First());
        fighters = result.Fighters;
        rules = result.Rules;
        trainers = result.Trainers;
        names[i] = fighters[0].Data.name;
        all.AppendLine("--T" + (i + 1)).AppendLine(string.Join("\n", result.Log));
      }

      prompts = asked;
      log = all.ToString();
      return names;
    }

    [Test]
    public void ReverseTurnFiresOnlyAtTheSecondFieldTurnEndAndAsksFirst()
    {
      int prompts;
      string log;
      var holder = F(ReverseTurn());
      var names = PlayTurns(5, null, out prompts, out log, holder);
      Assert.That(names[0], Is.Not.EqualTo(holder.Data.name), log);
      Assert.That(names[1], Is.EqualTo(holder.Data.name), log);
      Assert.That(prompts, Is.EqualTo(1), log);
      Assert.That(log.Split(new[] { "대기석에서 교대를 요청했다" }, StringSplitOptions.None).Length - 1, Is.EqualTo(1), log);
    }

    [Test]
    public void DecliningReverseTurnKeepsTheFieldAllyAndDoesNotSwitchLater()
    {
      int prompts;
      string log;
      var holder = F(ReverseTurn());
      var names = PlayTurns(4, n => false, out prompts, out log, holder);
      Assert.That(names.All(x => x != holder.Data.name), Is.True, log);
      Assert.That(prompts, Is.GreaterThanOrEqualTo(1), log);
    }

    [Test]
    public void LimitedReverseTurnIsUsedOnceAndNotAgainAtLaterSecondTurns()
    {
      int prompts;
      string log;
      var potential = ReverseTurn();
      potential.uses = "1/시";
      var holder = F(potential);
      var names = PlayTurns(7, null, out prompts, out log, holder);
      Assert.That(names[1], Is.EqualTo(holder.Data.name), log);
      Assert.That(prompts, Is.EqualTo(1), log);
      Assert.That(log.Split(new[] { "대기석에서 교대를 요청했다" }, StringSplitOptions.None).Length - 1, Is.EqualTo(1), log);
    }

    [Test]
    public void ReverseTurnDoesNotFireAtTheBenchHoldersOwnLaterTurnEnds()
    {
      int prompts;
      string log;
      var holder = F(ReverseTurn());
      PlayTurns(3, null, out prompts, out log, holder);
      // T1·T3 종료에는 대기석 트리거가 없고, T2 종료의 한 번만 묻는다
      Assert.That(prompts, Is.EqualTo(1), log);
    }

    [Test]
    public void BenchRetreatTypeSwitchDoesNotFireButOnFieldOneDoes()
    {
      int prompts;
      string log;
      var benchHolder = F(ReverseTurn(false));
      var names = PlayTurns(4, null, out prompts, out log, benchHolder);
      Assert.That(names.All(x => x != benchHolder.Data.name), Is.True, log);
      Assert.That(log.Contains("대기석에서 교대를 요청했다"), Is.False, log);

      // 장소에 있는 둔주류는 2T째 종료에 물러난다
      var holder = F(ReverseTurn(false));
      var partner = F();
      var foe = F();
      var rules = new Rules { OptionalEffectPrompt = (o, p, x) => true };
      var fighters = new[] { holder, foe };
      var trainers = new[] { new TrainerBattleState(), new TrainerBattleState() };
      partner.OnField = false;
      string last = null;
      for (int i = 0; i < 2; i++)
      {
        var command = new BattleCommand { Move = Idle(), Party = new[] { holder, partner } };
        var result = Engine.Resolve(TestData.Db, fighters, new[] { command, Stay(F()) }, rules, trainers, () => .5, request => request.Candidates.First());
        fighters = result.Fighters;
        rules = result.Rules;
        trainers = result.Trainers;
        last = fighters[0].Data.name;
      }

      Assert.That(last, Is.EqualTo(partner.Data.name));
    }

    // 사용자 결정: 진입형 교대는 대기석 전용이라 필드의 보유자 본인은 스스로 물러나지 않는다
    [Test]
    public void EntryTypeSwitchOnTheFieldHolderNeverRetreatsOrAsks()
    {
      var holder = F(ReverseTurn());
      var partner = F();
      partner.OnField = false;
      int prompts = 0;
      var rules = new Rules { OptionalEffectPrompt = (o, p, x) => { prompts++; return true; } };
      var fighters = new[] { holder, F() };
      var trainers = new[] { new TrainerBattleState(), new TrainerBattleState() };
      var log = new System.Text.StringBuilder();
      for (int i = 0; i < 4; i++)
      {
        var command = new BattleCommand { Move = Idle(), Party = new[] { holder, partner } };
        var result = Engine.Resolve(TestData.Db, fighters, new[] { command, Stay(F()) }, rules, trainers, () => .5, request => request.Candidates.First());
        fighters = result.Fighters;
        rules = result.Rules;
        trainers = result.Trainers;
        log.AppendLine(string.Join("\n", result.Log));
        Assert.That(fighters[0].Data.name, Is.EqualTo(holder.Data.name), log.ToString());
      }

      Assert.That(prompts, Is.EqualTo(0), log.ToString());
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
