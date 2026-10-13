using System;
using System.Linq;
using NUnit.Framework;

namespace AABattle
{
  // 칼데아 시트의 기능확장·추가행동 변형: 기존 추가행동·기능확장 표준 문장으로 바뀌어 실제로 실행된다
  [TestFixture]
  public class ExtraActionVariantTests
  {
    [TestCase("「칼춤」에 의한 추가행동권을 얻는다.", "가장 마지막에 「기술:칼춤」 기술에 의한 추가행동을 얻는다.")]
    [TestCase("가장 마지막에 「기능확장：기가임팩트」에 의한 추가행동을 얻는다.", "가장 마지막에 「기술:기가임팩트」 기술에 의한 추가행동을 얻는다.")]
    [TestCase("「4」배수의 T, 아군이 가장 먼저 「기능확장(성스러운칼)」에 의한 추가행동권을 얻는다.", "「4」배수 T, 가장 먼저 「기술:성스러운칼」 기술에 의한 추가행동을 얻는다.")]
    [TestCase("T의 마지막에 추가행동권을 얻는다.", "가장 마지막에 추가행동을 얻는다.")]
    [TestCase("T의 마지막에 추가공격을 할 수 있다.", "가장 마지막에 같은 기술에 의한 추가행동을 얻는다.")]
    [TestCase("공격 명중 후, 추가공격을 내보낸다.", "가장 먼저 같은 기술에 의한 추가행동을 얻는다.")]
    [TestCase("「꿰뚫어보기」을 내보낸다.", "가장 먼저 「기술:꿰뚫어보기」 기술에 의한 추가행동을 얻는다.")]
    [TestCase("저확률로 턴의 최후에 「흡혈」을 내보낸다.", "저확률로 가장 마지막에 「기술:흡혈」 기술에 의한 추가행동을 얻는다.")]
    [TestCase("아군이 「우드해머」를 내보낼 수 있다.", "아군 전원이 「기능확장:우드해머」를 내보낼 수 있다.")]
    public void VariantsBecomeStandardSentences(string raw, string standard)
    {
      Assert.That(EffectNormalizer.Normalize(raw), Is.EqualTo(standard));
    }

    [Test]
    public void ChaldeaVariantsAreReadAsExistingModules()
    {
      Assert.That(EffectModules.Classify(EffectNormalizer.Normalize("T의 마지막에 추가공격을 할 수 있다.")), Is.EqualTo("추가행동"));
      Assert.That(EffectModules.Classify(EffectNormalizer.Normalize("아군이 「우드해머」를 내보낼 수 있다.")), Is.Not.Empty);
    }

    static Fighter F(int speed, params Potential[] potentials)
    {
      return new Fighter(new Pokemon
      {
        id = "xa" + speed + potentials.Length,
        name = "추" + speed,
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

    [Test]
    public void NamedExtraActionVariantActuallyActs()
    {
      var potential = new Potential { id = "부수", name = "추가", description = "「칼춤」에 의한 추가행동권을 얻는다.", activation = "자동", triggers = new[] { "T 개시 시" } };
      var idle = new Move { name = "대기", category = "변화", target = "자신", types = new[] { "노말" }, accuracy = 100, tags = new string[0] };
      var result = Engine.Resolve(TestData.Db, new[] { F(100, potential), F(50) }, new[] { new BattleCommand { Move = idle }, new BattleCommand { Move = idle } }, new Rules(), new[] { new TrainerBattleState(), new TrainerBattleState() }, () => .5, q => q.Candidates.First());
      Assert.That(result.Log.Any(x => x.Contains("추가행동: 칼춤")), Is.True, string.Join("\n", result.Log));
    }
  }
}
