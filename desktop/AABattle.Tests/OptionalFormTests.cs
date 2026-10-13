using System;
using System.Linq;
using NUnit.Framework;

namespace AABattle
{
  // 「~할 수 있다」 선택 효과: 직설형으로 바꿔 실행기를 찾고, 확인은 큐가 한 번만 묻는다
  [TestFixture]
  public class OptionalFormTests
  {
    [TestCase("상대의 속도를 내릴 수 있다.", "상대의 속도를 내린다.")]
    [TestCase("상대를 강제교대시킬 수 있다.", "상대를 강제교대시킨다.")]
    [TestCase("자신의 특성을 「매직미러」로 변경 할 수 있다.", "자신의 특성을 「매직미러」로 변경한다.")]
    [TestCase("자신의 공격을 올릴 수 있다", "자신의 공격을 올린다.")]
    [TestCase("무기를 받을 수 있다.", "무기를 받는다.")]
    [TestCase("상대의 속도를 내린다.", null)]
    public void DirectForm(string text, string direct)
    {
      Assert.That(OptionalForm.Direct(text), Is.EqualTo(direct));
    }

    [TestCase("상대의 능력의 상승(하락)을 인계할 수 있다.", "교대 인계")]
    [TestCase("자신의 능력 상승을 인계할 수 있다.", "교대 인계")]
    [TestCase("상대를 「강제교대」시킬 수 있다.", "강제교대")]
    [TestCase("상대의 속도를 내릴 수 있다.", "능력 랭크 변화")]
    [TestCase("자신(아군)의 특성을 「매직미러」로 할 수 있다.", "특성 변경")]
    public void OptionalSentencesFindAnExecutor(string sentence, string executor)
    {
      var steps = PotentialQueue.JudgeSteps(sentence, false);
      Assert.That(steps.Select(s => s.Kind), Is.EqualTo(new[] { executor }), string.Join(" | ", steps.Select(s => s.Line)));
    }

    static Fighter F(int speed, params Potential[] potentials)
    {
      return new Fighter(new Pokemon
      {
        id = "opt" + speed + potentials.Length,
        name = "선" + speed,
        level = 100,
        types = new[] { "노말" },
        ability = "",
        item = "",
        @base = new[] { 300, 100, 100, 100, 100, speed },
        iv = new[] { 31, 31, 31, 31, 31, 31 },
        moves = new[] { "몸통박치기" },
        potentials = potentials
      });
    }

    [TestCase(true, -1)]
    [TestCase(false, 0)]
    public void ChoiceDecidesWhetherTheEffectApplies(bool accept, int speedStage)
    {
      var potential = new Potential { id = "부수", name = "선택", description = "상대의 속도를 내릴 수 있다.", activation = "자동", triggers = new[] { "T 개시 시" } };
      var rules = new Rules();
      int asked = 0;
      rules.OptionalEffectPrompt = (owner, other, p) =>
      {
        asked++;
        return accept;
      };
      var idle = new Move { name = "대기", category = "변화", target = "자신", types = new[] { "노말" }, accuracy = 100, tags = new string[0] };
      var result = Engine.Resolve(TestData.Db, new[] { F(100, potential), F(50) }, new[] { new BattleCommand { Move = idle }, new BattleCommand { Move = idle } }, rules, new[] { new TrainerBattleState(), new TrainerBattleState() }, () => .5, q => q.Candidates.First());
      Assert.That(asked, Is.EqualTo(1), "확인은 한 번만 묻는다\n" + string.Join("\n", result.Log));
      Assert.That(result.Fighters[1].Stages[5], Is.EqualTo(speedStage), string.Join("\n", result.Log));
    }
  }
}
