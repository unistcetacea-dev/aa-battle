using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle
{
  // 상태 조건(상시) + 확률 효과 포텐셜: 효과가 쓰이는 판정 지점마다 확률을 굴린다(StateRolls)
  [TestFixture]
  public class StateRollTests
  {
    static int ids;

    static Potential State(string trigger, string effect, string name = "검사격", string slot = "격")
    {
      return new Potential
      {
        id = slot,
        name = name,
        description = effect,
        activation = "자동",
        triggers = new[] { trigger }
      };
    }

    static Fighter F(int speed, string[] types, params Potential[] potentials)
    {
      return new Fighter(new Pokemon
      {
        id = "roll" + (++ids),
        name = "굴" + ids,
        level = 100,
        types = types,
        ability = "",
        item = "",
        @base = new[] { 300, 100, 100, 100, 100, speed },
        iv = new[] { 31, 31, 31, 31, 31, 31 },
        moves = new[] { "몸통박치기" },
        potentials = potentials
      });
    }

    static Fighter Normal(params Potential[] potentials)
    {
      return F(100, new[] { "노말" }, potentials);
    }

    static Move Hit(double accuracy = 100, int priority = 0)
    {
      return new Move { name = "검사공격", category = "물리", target = "상대", types = new[] { "노말" }, power = 60, accuracy = accuracy, attack = "공격", defense = "방어", priority = priority, tags = new string[0] };
    }

    static Move Idle()
    {
      return new Move { name = "대기", category = "변화", target = "자신", types = new[] { "노말" }, accuracy = 100, tags = new string[0] };
    }

    static BattleCommand Use(Move move)
    {
      return new BattleCommand { Move = move };
    }

    static TurnResult Run(Fighter a, Fighter b, BattleCommand ca, BattleCommand cb, Func<double> rng)
    {
      return Engine.Resolve(TestData.Db, new[] { a, b }, new[] { ca, cb }, new Rules(), new[] { new TrainerBattleState(), new TrainerBattleState() }, rng, q => q.Candidates.First());
    }

    const string Fighting = "적진에 「격투」 포켓몬이 있을 때";
    static readonly string[] FightingType = { "격투" };

    // ===== 박격: 대미지 계산 지점 =====
    [TestCase(.15, true)]
    [TestCase(.90, false)]
    public void SmashDoublesDamageOnlyWhenTheRollSucceeds(double roll, bool doubled)
    {
      Func<double> rng = () => roll;
      var controlTarget = F(50, FightingType);
      var controlResult = Run(F(100, new[] { "노말" }), controlTarget, Use(Hit()), Use(Idle()), rng);
      var target = F(50, FightingType);
      var user = Normal(State(Fighting, "저확률로 자신이 주는 대미지를 강화(2배)한다."));
      var result = Run(user, target, Use(Hit()), Use(Idle()), rng);
      int plain = controlTarget.HP - controlResult.Fighters[1].HP, actual = target.HP - result.Fighters[1].HP;
      Assert.That(plain, Is.GreaterThan(0));
      if (doubled)
        Assert.That(actual, Is.InRange(plain * 2 - 3, plain * 2 + 3), string.Join("\n", result.Log));
      else
        Assert.That(actual, Is.EqualTo(plain), string.Join("\n", result.Log));
    }

    [Test]
    public void ConditionNotMetNeverRolls()
    {
      var user = Normal(State(Fighting, "저확률로 자신이 주는 대미지를 강화(2배)한다."));
      var target = F(50, new[] { "노말" });
      var result = Run(user, target, Use(Hit()), Use(Idle()), () => .15);
      Assert.That(result.Log.Any(x => x.Contains("확률 판정")), Is.False, string.Join("\n", result.Log));
    }

    [Test]
    public void SmashIsRolledAgainEveryAction()
    {
      var user = Normal(State(Fighting, "중확률로 자신이 주는 대미지를 강화(2배)한다."));
      var target = F(50, FightingType);
      var first = Run(user, target, Use(Hit()), Use(Idle()), () => .30);
      Assert.That(first.Log.Count(x => x.Contains("확률 판정 성공")), Is.EqualTo(1));
      var second = Run(first.Fighters[0], first.Fighters[1], Use(Hit()), Use(Idle()), () => .80);
      Assert.That(second.Log.Any(x => x.Contains("확률 판정 실패")), Is.True, string.Join("\n", second.Log));
      Assert.That(second.Fighters[0].Conditions.Modifiers.Any(m => m.Rolled), Is.False, "행동이 끝나면 걸린 효과는 사라진다");
    }

    // ===== 저격: 기술 명중 판정 지점 =====
    [TestCase(.15, true)]
    [TestCase(.90, false)]
    public void SnipeMakesTheMoveSureHitOnlyWhenTheRollSucceeds(double roll, bool hits)
    {
      var user = Normal(State(Fighting, "저확률로 자신의 기술이 「필중」한다."));
      var target = F(50, FightingType);
      var result = Run(user, target, Use(Hit(10)), Use(Idle()), () => roll);
      Assert.That(result.Fighters[1].HP < target.HP, Is.EqualTo(hits), string.Join("\n", result.Log));
    }

    // ===== 추격: 기술 사용 지점 =====
    [TestCase(.15, true)]
    [TestCase(.90, false)]
    public void ChaseGrantsPursuitOnlyWhenTheRollSucceeds(double roll, bool chases)
    {
      var user = Normal(State("적진에 「노말」 포켓몬이 있을 때", "저확률로 자신의 기술에 「추격」 효과를 부여한다.", "대노추격"));
      var target = Normal();
      var bench = Normal();
      var result = Run(user, target, Use(Hit(100, -7)), new BattleCommand { SwitchIn = bench }, () => roll);
      Assert.That(result.SwitchedOut[1], Is.Not.Null, string.Join("\n", result.Log));
      Assert.That(result.SwitchedOut[1].HP < target.HP, Is.EqualTo(chases), string.Join("\n", result.Log));
    }

    // ===== 반격: 턴 마지막 추가행동 =====
    [TestCase(.15, 2)]
    [TestCase(.90, 1)]
    public void CounterGrantsALastExtraActionOnlyWhenTheRollSucceeds(double roll, int attacks)
    {
      var user = Normal(State("적진에 「노말」 포켓몬이 있을 때", "저확률로 가장 마지막에 추가행동을 얻는다.", "대노반격"));
      var target = Normal();
      var result = Run(user, target, Use(Hit()), Use(Idle()), () => roll);
      Assert.That(1 + result.Log.Count(x => x.Contains("의 추가행동:")), Is.EqualTo(attacks), string.Join("\n", result.Log));
    }

    // ===== 선: 행동 순서 결정 지점 =====
    [TestCase(.40, true)]
    [TestCase(.60, false)]
    public void FirstRollDecidesWhoActsFirst(double roll, bool userFirst)
    {
      var user = F(10, new[] { "노말" }, State("적진에 「노말」 포켓몬이 있을 때", "중확률로 자신의 기술 우선도를 1 올린다.", "대노선", "선"));
      var fast = F(200, new[] { "노말" });
      var result = Run(user, fast, Use(Hit()), Use(Hit()), () => roll);
      string first = result.Log.First(x => x.Contains("의 검사공격!"));
      Assert.That(first.StartsWith(user.Data.name + "의"), Is.EqualTo(userFirst), string.Join("\n", result.Log));
    }

    // ===== 중확률 보장: 같은 시점의 중확률이 전부 실패하면 하나는 발동 =====
    [Test]
    public void MediumChancesAtTheSamePointGuaranteeOne()
    {
      var user = Normal(
        State(Fighting, "중확률로 자신이 주는 대미지를 강화(2배)한다.", "극투박격"),
        State(Fighting, "중확률로 자신의 기술이 「필중」한다.", "극투저격"));
      var target = F(50, FightingType);
      var result = Run(user, target, Use(Hit()), Use(Idle()), () => .90);
      Assert.That(result.Log.Count(x => x.Contains("확률 판정 성공")), Is.EqualTo(1), string.Join("\n", result.Log));
    }

    // ===== 템플릿·샘플: 상태 조건 + 확률 효과는 모두 판정 지점이 연결돼 있다 =====
    [Test]
    public void EveryCounterTemplateWithAChanceHasARollPoint()
    {
      var problems = new List<string>();
      foreach (string type in EditorTemplate.CounterTypeNames())
        foreach (string slot in new[] { "회피", "내성", "격" })
          foreach (var option in EditorTemplate.CounterOptions(slot, type))
            problems.AddRange(Unrolled(option.name, option.effect));
      Assert.That(problems, Is.Empty, string.Join("\n", problems.Take(20)));
    }

    static IEnumerable<string> Unrolled(string name, string text)
    {
      string trigger, effect;
      PotentialLibrary.Split(text, out trigger, out effect);
      foreach (string clause in TriggerStructures.NormalizeLines(trigger).Replace("\r", "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
      {
        if (TriggerModules.Parse(clause).IsEvent)
          yield break;
      }

      foreach (string line in EffectNormalizer.NormalizeLines(effect).Replace("\r", "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
      {
        string probability, body;
        if (!EffectNormalizer.TryProbabilityPrefix(line, out probability, out body))
          continue;
        if (PotentialProtection.TryGuard(line, out string kind, out MoveCondition condition, out double? rate))
          continue;
        if (BondModules.Classify(body).Length > 0)
          continue;   // 유대 재사용·재발동은 전용 실행기가 확률을 굴린다
        if (!StateRolls.IsRollable(body))
          yield return name + ": " + line;
      }
    }

    [Test]
    public void SampleProbabilityPotentialsWithOnlyStateConditionsAreRollable()
    {
      string dir = AppDomain.CurrentDomain.BaseDirectory;
      string path = null;
      while (dir != null && path == null)
      {
        string candidate = Path.Combine(dir, "samples", "abaddon-sylph.aabdata.json");
        if (File.Exists(candidate))
          path = candidate;
        dir = Path.GetDirectoryName(dir);
      }

      var project = ProjectStore.Parse(File.ReadAllText(path, Encoding.UTF8));
      var problems = new List<string>();
      int checkedCount = 0;
      foreach (var member in project.teams.Single().members)
        foreach (var record in member.potentials.Where(x => x.enabled && x.name.Length > 0 && x.trigger.Length > 0))
        {
          string text = record.trigger + ", " + (record.effect.Length > 0 ? record.effect : record.raw);
          var found = Unrolled(record.name, text).ToList();
          checkedCount++;
          problems.AddRange(found);
        }

      Assert.That(checkedCount, Is.GreaterThan(10));
      Assert.That(problems, Is.Empty, string.Join("\n", problems));
    }
  }
}
