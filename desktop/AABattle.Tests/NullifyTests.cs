using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle
{
  // 이름 지정 포텐셜 무효화: 정규화 · 분류 · 실제 배틀에서 무효화된 포텐셜이 멈추고 돌아온다
  [TestFixture]
  public class NullifyTests
  {
    static int ids;

    static Potential Pot(string slot, string name, string trigger, string effect)
    {
      return new Potential
      {
        id = slot,
        name = name,
        description = effect,
        activation = "자동",
        triggers = trigger.Length == 0 ? new string[0] : new[] { trigger }
      };
    }

    static Fighter F(int speed, params Potential[] potentials)
    {
      return new Fighter(new Pokemon
      {
        id = "nul" + (++ids),
        name = "무" + ids,
        level = 100,
        types = new[] { "노말" },
        ability = "",
        item = "",
        @base = new[] { 300, 100, 100, 100, 100, speed },
        iv = new[] { 31, 31, 31, 31, 31, 31 },
        moves = new[] { "몸통박치기" },
        potentials = potentials
      })
      {
        OnField = true
      };
    }

    static readonly Potential Primal = Pot("부수", "프라이멀 헌트", "", "상대의 『투쟁본능』을 무효화한다");

    [TestCase("상대의 『투쟁본능』을 무효화한다.", "상대의 『투쟁본능』을 무효화한다.")]
    [TestCase("상대의 『대○회피』『대○내성』을 무효화한다.", "상대의 『대○회피』『대○내성』을 무효화한다.")]
    [TestCase("상대의 「공격을 회피한다」의 효과를 가진 포텐셜을 무효화한다.", "상대의 「공격을 회피한다」 효과를 가진 포텐셜을 무효화한다.")]
    [TestCase("원종인 「캐스터」종과는 다른 생태를 갖는다. 『올드 타입』을 무효화한다.", "자신의 『올드 타입』을 무효화한다.")]
    [TestCase("「？？？」종이 이 개체뿐이기에 『올드 타입』을 무효화한다.", "자신의 『올드 타입』을 무효화한다.")]
    public void CollectedVariantsBecomeStandardSentences(string raw, string standard)
    {
      Assert.That(EffectNormalizer.Normalize(raw), Is.EqualTo(standard));
    }

    [TestCase("상대의 『투쟁본능』을 무효화한다.", PotentialNullify.Kind)]
    [TestCase("상대의 「공격을 회피한다」 효과를 가진 포텐셜을 무효화한다.", PotentialNullify.ByEffectKind)]
    public void SentencesClassifyAndAreRegistered(string sentence, string kind)
    {
      Assert.That(EffectModules.Classify(sentence), Is.EqualTo(kind));
      Assert.That(ModuleCatalog.All.Any(x => x.Kind == ModuleKind.Effect && x.Expected == kind), Is.True);
    }

    [Test]
    public void CatalogModuleRoundTripsANameList()
    {
      var module = ModuleCatalog.Find("effect.potential.nullify");
      var values = new Dictionary<string, string> { { "대상", "상대" }, { "이름", "대○회피/대○내성" } };
      string text = module.Fill(values);
      Assert.That(text, Is.EqualTo("상대의 『대○회피』『대○내성』을 무효화한다."));
      Dictionary<string, string> read;
      Assert.That(module.TryRead(text, out read), Is.True);
      Assert.That(read["이름"], Is.EqualTo("대○회피/대○내성"));
    }

    // ===== 무효화 판정 =====
    [Test]
    public void NamedPotentialIsNullifiedOnlyWhileTheNullifierIsOnTheField()
    {
      var hunter = F(100, Primal);
      var prey = F(100, Pot("부수", "투쟁본능", "", "자신이 주는 대미지를 강화(2배)한다"), Pot("부수", "다른 포텐셜", "", "자신이 주는 대미지를 강화(2배)한다"));
      Assert.That(PotentialNullify.IsNullified(prey, prey.Data.potentials[0], hunter, new Rules()), Is.True);
      Assert.That(PotentialNullify.IsNullified(prey, prey.Data.potentials[1], hunter, new Rules()), Is.False, "이름이 다르면 그대로");
      Assert.That(PotentialNullify.IsNullified(hunter, Primal, prey, new Rules()), Is.False, "자신의 포텐셜은 상대의 무효화 대상이 아니다");
      hunter.OnField = false;
      Assert.That(PotentialNullify.IsNullified(prey, prey.Data.potentials[0], hunter, new Rules()), Is.False, "장소를 떠나면 돌아온다");
    }

    [Test]
    public void WildcardMatchesExactlyOneCharacter()
    {
      var hunter = F(100, Pot("부수", "분쇄", "", "상대의 『대○회피』을 무효화한다"));
      var prey = F(100, Pot("회피", "대화회피", "", "상대의 「불꽃」 기술을 저확률로 회피한다"), Pot("회피", "극화회피", "", "상대의 「불꽃」 기술을 저확률로 회피한다"), Pot("회피", "대회피", "", "상대의 「불꽃」 기술을 저확률로 회피한다"));
      var rules = new Rules();
      Assert.That(PotentialNullify.IsNullified(prey, prey.Data.potentials[0], hunter, rules), Is.True);
      Assert.That(PotentialNullify.IsNullified(prey, prey.Data.potentials[1], hunter, rules), Is.False);
      Assert.That(PotentialNullify.IsNullified(prey, prey.Data.potentials[2], hunter, rules), Is.False);
    }

    [Test]
    public void EffectKindNullifiesEvasionPotentialsOnly()
    {
      var hunter = F(100, Pot("부수", "와일드 헌트", "", "상대의 「공격을 회피한다」 효과를 가진 포텐셜을 무효화한다"));
      var prey = F(100, Pot("회피", "극화회피", "", "상대의 「불꽃」 기술을 저확률로 회피한다"), Pot("부수", "투쟁본능", "", "자신이 주는 대미지를 강화(2배)한다"));
      Assert.That(PotentialNullify.IsNullified(prey, prey.Data.potentials[0], hunter, new Rules()), Is.True);
      Assert.That(PotentialNullify.IsNullified(prey, prey.Data.potentials[1], hunter, new Rules()), Is.False);
    }

    [Test]
    public void MutualNullifiersDoNotRecurseForever()
    {
      var a = F(100, Pot("부수", "가", "", "상대의 『나』를 무효화한다"));
      var b = F(100, Pot("부수", "나", "", "상대의 『가』를 무효화한다"));
      Assert.That(() => PotentialNullify.IsNullified(a, a.Data.potentials[0], b, new Rules()), Throws.Nothing);
    }

    // ===== 배틀에서: 상시 효과와 사건 효과가 모두 멈춘다 =====
    static TurnResult Run(Fighter a, Fighter b)
    {
      var move = new Move { name = "검사공격", category = "물리", target = "상대", types = new[] { "노말" }, power = 60, accuracy = 100, attack = "공격", defense = "방어", tags = new string[0] };
      var idle = new Move { name = "대기", category = "변화", target = "자신", types = new[] { "노말" }, accuracy = 100, tags = new string[0] };
      return Engine.Resolve(TestData.Db, new[] { a, b }, new[] { new BattleCommand { Move = move }, new BattleCommand { Move = idle } }, new Rules(), new[] { new TrainerBattleState(), new TrainerBattleState() }, () => .5, q => q.Candidates.First());
    }

    [Test]
    public void NullifiedContinuousPotentialStopsAndComesBack()
    {
      Func<Fighter> prey = () => F(100, Pot("부수", "투쟁본능", "", "자신이 주는 대미지를 강화(2배)한다"));
      var target = F(50);
      int boosted = target.HP - Run(prey(), target).Fighters[1].HP;
      var target2 = F(50);
      var withHunter = Run(prey(), target2, Primal);
      int stopped = target2.HP - withHunter.Fighters[1].HP;
      Assert.That(stopped, Is.LessThan(boosted * 2 / 3), string.Join("\n", withHunter.Log));
    }

    static TurnResult Run(Fighter a, Fighter b, Potential nullifier)
    {
      b.Data.potentials = new[] { nullifier };
      return Run(a, b);
    }

    [Test]
    public void NullifiedTriggerPotentialDoesNotFire()
    {
      var rise = Pot("부수", "투쟁본능", "T 개시 시", "자신의 「공격」이 오른다.");
      var free = Run(F(100, rise), F(50));
      Assert.That(free.Fighters[0].Stages[1], Is.EqualTo(1), string.Join("\n", free.Log));
      var blocked = Run(F(100, rise), F(50, Primal));
      Assert.That(blocked.Fighters[0].Stages[1], Is.EqualTo(0), string.Join("\n", blocked.Log));
    }
  }
}
