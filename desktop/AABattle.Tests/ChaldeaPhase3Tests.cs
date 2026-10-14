using System;
using System.Linq;
using NUnit.Framework;

namespace AABattle.Tests
{
  // 칼데아 3차: 사용 조건 수식문 · 대미지 문장 변형(타입 대미지) · 기능확장/추가행동 변형 · 포텐셜 부여
  [TestFixture]
  public class ChaldeaPhase3Tests
  {
    static Fighter Mon(string name, string type, params Potential[] potentials)
    {
      return new Fighter(new Pokemon
      {
        id = "g3" + name,
        name = name,
        level = 100,
        types = new[] { type },
        ability = "",
        item = "",
        @base = new[] { 300, 100, 100, 100, 100, 100 },
        iv = new[] { 31, 31, 31, 31, 31, 31 },
        moves = new[] { "몸통박치기" },
        potentials = potentials
      });
    }

    static Potential Rule(string text)
    {
      return new Potential { id = "부수", name = "검사", description = text, activation = "자동", triggers = new[] { "T 개시 시" } };
    }

    static TurnResult OneIdleTurn(Fighter a, Fighter b)
    {
      var idle = new Move { name = "대기", category = "변화", target = "자신", types = new[] { "노말" }, accuracy = 100, tags = new string[0] };
      return Engine.Resolve(TestData.Db, new[] { a, b }, new[] { new BattleCommand { Move = idle }, new BattleCommand { Move = idle } }, new Rules(), new[] { new TrainerBattleState(), new TrainerBattleState() }, () => .5, q => q.Candidates.First());
    }

    // ---------- 1. 사용 조건 수식문 ----------
    [TestCase("이 포텐셜은 자신이 필드에 없어도 사용할 수 있다.", "OffField")]
    [TestCase("이 포텐셜은 자신이 필드에 있지 않아도 발동할 수 있다.", "OffField")]
    [TestCase("이 포텐셜은 PT의 전원이 사용할 수 있다.", "PartyWide")]
    [TestCase("이 포텐셜은 통상교대로 나온 T의 최후에도 발동할 수 있다.", "SwitchTurnLast")]
    [TestCase("이 퍼텐셜은 공격 명중시에 사용할 수 있다.", "OnHit")]
    public void UseConditionSentencesBecomeFlagsAndLeaveTheBody(string sentence, string flag)
    {
      string condition, effect;
      PotentialLibrary.Split("1/시 아군의 체력을 1/2 회복한다. " + sentence, out condition, out effect);
      var conditions = UseConditionModules.Of("1/시 아군의 체력을 1/2 회복한다. " + sentence);
      Assert.That((bool)typeof(UseConditions).GetField(flag).GetValue(conditions), Is.True, flag);
      Assert.That(effect, Does.Not.Contain("이 포텐셜은").And.Not.Contain("이 퍼텐셜은"));
      Assert.That(effect + condition, Does.Not.Contain("사용할 수 있다").And.Not.Contain("발동할 수 있다"));
    }

    [Test]
    public void OnHitUseConditionBecomesTheHitConfirmTrigger()
    {
      string condition, effect;
      PotentialLibrary.Split("이 퍼텐셜은 공격 명중시에 사용할 수 있다. 자신의 기술의 데미지를 2배로 한다.", out condition, out effect);
      Assert.That(condition, Is.EqualTo(UseConditionModules.OnHitTrigger));
      Assert.That(TriggerModules.Parse(condition).Module, Is.EqualTo("명중 확정"));
      Assert.That(effect, Does.Contain("2배"));
    }

    [Test]
    public void BodyWithoutUseConditionsIsLeftAlone()
    {
      const string text = "상대를 쓰러뜨렸을 때, 자신의 체력을 최대 HP의 1/4 회복한다.\r\n이 줄은 그대로다.";
      UseConditions conditions;
      Assert.That(UseConditionModules.Prepare(text, out conditions), Is.SameAs(text));
      Assert.That(conditions.Any, Is.False);
    }

    [TestCase("이 포텐셜은 「능력:A」 이하일 때는 발동하지 않는다. 자신의 체력을 최대 HP의 1/4 회복한다.", "A", false)]
    [TestCase("이 포텐셜은 「능력:A」 이하일 때는 발동하지 않는다. 자신의 체력을 최대 HP의 1/4 회복한다.", "AA", true)]
    [TestCase("이 포텐셜은 「능력:A」 이하일 때는 발동하지 않는다. 자신의 체력을 최대 HP의 1/4 회복한다.", "B", false)]
    [TestCase("이 포텐셜은 원래 「능력」이 「AA」 이상일 때는 발동하지 않는다. 자신의 체력을 최대 HP의 1/4 회복한다.", "AA", false)]
    [TestCase("이 포텐셜은 원래 「능력」이 「AA」 이상일 때는 발동하지 않는다. 자신의 체력을 최대 HP의 1/4 회복한다.", "A", true)]
    public void AptitudeGateBlocksThePotentialAtTheStatedRank(string text, string rank, bool allowed)
    {
      var owner = Mon("가", "노말");
      var other = Mon("나", "노말");
      var trainer = new TrainerRecord();
      trainer.ability.rank = rank;
      var states = new[] { new TrainerBattleState { Trainer = trainer }, new TrainerBattleState() };
      using (EffectScope.Begin(new[] { owner, other }, new Rules(), () => .5, states))
        Assert.That(UseConditionModules.Allows(text, owner, EffectScope.Current), Is.EqualTo(allowed));
    }

    [Test]
    public void AptitudeGateDoesNotBlockWithoutTrainerData()
    {
      var owner = Mon("가", "노말");
      using (EffectScope.Begin(new[] { owner, Mon("나", "노말") }, new Rules(), () => .5, null))
        Assert.That(UseConditionModules.Allows("이 포텐셜은 「능력:A」 이하일 때는 발동하지 않는다. 회복한다.", owner, EffectScope.Current), Is.True);
    }

    // ---------- 2. 대미지 문장 변형 ----------

    [Test]
    public void ForcedEntryDamageSentenceSplitsIntoTriggerAndTypedDamage()
    {
      string condition, effect;
      PotentialLibrary.Split("「죽어내밀기」로 나온 상대에게 1/8의 「불꽃」대미지를 준다. 빈사로 할 수 없다.", out condition, out effect);
      Assert.That(TriggerModules.Parse(TriggerStructures.NormalizeLines(condition)).Module, Is.Not.Empty, condition);
      var steps = PotentialQueue.JudgeSteps(effect, false);
      Assert.That(steps, Has.Count.EqualTo(1));
      Assert.That(steps[0].Ok, Is.True, steps[0].Line);
      Assert.That(steps[0].Kind, Is.EqualTo(DamageVariantModules.Kind));
    }

    [Test]
    public void UntypedWordIsNotATypedDamage()
    {
      Assert.That(EffectNormalizer.Normalize("상대에게 1/8의 반동 데미지를 준다"), Does.Not.Contain("타입"));
    }

    static int LossFrom(string defenderType, string typeInSentence)
    {
      var attacker = Mon("공", "노말", Rule("상대에게 최대 HP의 1/8 「" + typeInSentence + "」 타입 대미지를 준다."));
      var defender = Mon("수", defenderType);
      int before = defender.HP;
      var result = OneIdleTurn(attacker, defender);
      return before - result.Fighters[1].HP;
    }

    [Test]
    public void TypedDamageAppliesTheTypeChart()
    {
      int neutral = LossFrom("노말", "불꽃");
      int weak = LossFrom("풀", "불꽃");
      int resist = LossFrom("물", "불꽃");
      Assert.That(neutral, Is.GreaterThan(0));
      Assert.That(Math.Abs(weak - neutral * 2), Is.LessThanOrEqualTo(2), "효과가 굉장 2배");
      Assert.That(Math.Abs(resist * 2 - neutral), Is.LessThanOrEqualTo(2), "효과가 별로 0.5배");
      Assert.That(LossFrom("고스트", "노말"), Is.EqualTo(0), "무효");
    }

    [Test]
    public void TypedDamageThatCannotFaintLeavesOneHp()
    {
      var attacker = Mon("공", "노말", Rule("상대에게 최대 HP의 1/8 「불꽃」 타입 대미지를 준다. 이 대미지로는 빈사로 할 수 없다."));
      var defender = Mon("수", "풀");
      defender.HP = 3;
      var result = OneIdleTurn(attacker, defender);
      Assert.That(result.Fighters[1].HP, Is.EqualTo(1));
    }

    // ---------- 3·4·6. 변형 문장 ----------
    [TestCase("자신의 「솔라빔」을 「모으기」를 생략하고 내보낼 수 있다", "자신의 「기술:솔라빔」 기술의 모으기를 생략한다.")]
    [TestCase("자신의 「고스트다이브」를 모으기를 생략하고 내보낼 수 있다", "자신의 「기술:고스트다이브」 기술의 모으기를 생략한다.")]
    [TestCase("상대의 기술의 대미지를 반감해 반드시 참는다", "자신이 받는 대미지를 약화(0.5배)한다. 자신은 빈사가 되는 공격을 HP 1로 버틴다.")]
    [TestCase("상대의 공격의 대미지를 반감해 반드시 참는다", "자신이 받는 대미지를 약화(0.5배)한다. 자신은 빈사가 되는 공격을 HP 1로 버틴다.")]
    [TestCase("공격 이외의 대미지를 받지 않는다", "자신은 공격 이외의 대미지를 받지 않는다.")]
    [TestCase("기술의 반동대미지를 무시한다", "자신의 기술의 반동 대미지를 받지 않는다.")]
    [TestCase("T종료시까지 아군이 받는 데미지를 반감한다", "T종료시까지 자신이 받는 대미지를 약화(0.5배)한다.")]
    [TestCase("자신의 상태이상을 해제해 최대 체력의 1/8을 회복한다", "자신의 상태이상을 치유한다. 자신의 체력을 최대 HP의 1/8 회복한다.")]
    [TestCase("자신의 체력 종족치를 「1」랭크 올린다", "자신의 「체력」 종족치를 1랭크 올린다.")]
    [TestCase("자신의 공격기술에 「추격」효과를 부여한다", "자신의 「공격」 기술에 「추격」 효과를 부여한다.")]
    [TestCase("자신의 타입일치기술의 보정이 「2.0」배가 된다", "자신의 기술의 타입 일치 배율을 「2배」로 한다.")]
    [TestCase("상대에 의해 능력이 감소(저하)하지 않는다", "자신은 상대에 의해 능력이 내려가지 않는다.")]
    [TestCase("자신의 체력을 절반으로 하고, 추가공격을 할 수 있다", "자신의 현재 HP의 1/2을 소비한다. 이때 가장 먼저 같은 기술에 의한 추가공격을 한다.")]
    [TestCase("자신의 「아쿠아링」 상태를 해제하여 추가공격을 할 수 있다", "자신의 「아쿠아링」 상태를 해제한다. 이때 가장 먼저 같은 기술에 의한 추가행동을 얻는다.")]
    [TestCase("T의 최후에 「난동부리기」를 해제해 「야만의칼」을 내보낼 수 있다", "자신의 「난동부리기」 상태를 해제한다. 이때 가장 마지막에 「기술:야만의칼」 기술에 의한 추가행동을 얻는다.")]
    public void SheetVariantsBecomeStandardSentences(string raw, string expected)
    {
      string once = EffectNormalizer.Normalize(raw);
      Assert.That(once, Is.EqualTo(expected));
      Assert.That(EffectNormalizer.Normalize(once), Is.EqualTo(once), "재정규화");
      foreach (var step in PotentialQueue.JudgeSteps(once, false))
        Assert.That(step.Ok, Is.True, step.Line);
    }

    [TestCase("필드에 나와 2T간 자신의 전능력을 올린다.", "필드에 나올 때")]
    [TestCase("데이터가 해석된 상대에 대해서, 자신의 「특공/속」을 강화(1.2배)한다.", "상대의 데이터가 해석된 상태일 때")]
    public void LeadClausesSplitIntoTriggers(string text, string trigger)
    {
      string condition, effect;
      PotentialLibrary.Split(text, out condition, out effect);
      Assert.That(condition, Is.EqualTo(trigger));
      Assert.That(effect, Is.Not.Empty);
      Assert.That(TriggerModules.Parse(TriggerStructures.NormalizeLines(condition)).Module, Is.Not.Empty);
    }

    [Test]
    public void NotesAndHeadlinesAreNotEffects()
    {
      var steps = PotentialQueue.JudgeSteps("알리사 라인폴트 전용 포텐셜.\r\n자신의 체력을 최대 HP의 1/4 회복한다.\r\n(※체력이 「1」일 때는 사용할 수 없다)", false);
      Assert.That(steps, Has.Count.EqualTo(1));
      Assert.That(steps[0].Ok, Is.True);
    }

    [Test]
    public void HalfHpAttackVariantActuallyActsTwice()
    {
      var potential = new Potential { id = "부수", name = "광란", description = "자신의 공격 명중 시, 자신의 체력을 절반으로 하고, 추가공격을 할 수 있다.", activation = "자동" };
      var attacker = Mon("공", "노말", potential);
      var defender = Mon("수", "노말");
      var tackle = TestData.Db.moves.First(m => m.name == "몸통박치기");
      int hits = 0;
      var result = Engine.Resolve(TestData.Db, new[] { attacker, defender }, new[] { new BattleCommand { Move = tackle }, new BattleCommand { Move = tackle } }, new Rules(), new[] { new TrainerBattleState(), new TrainerBattleState() }, () => 0, q => q.Candidates.First());
      hits = result.Log.Count(x => x.Contains("몸통박치기"));
      Assert.That(result.Log.Any(x => x.Contains("추가공격")), Is.True, string.Join("\n", result.Log));
      Assert.That(hits, Is.GreaterThanOrEqualTo(2), string.Join("\n", result.Log));
    }

    [TestCase("「골드 십」종의 정점에 있는 『고유종』.")]
    [TestCase("원종인 「캐스터」종과는 다른 생태를 갖는다.")]
    public void SettingFlavorSentencesAreNotEffects(string flavor)
    {
      var steps = PotentialQueue.JudgeSteps(flavor + " 자신의 체력을 최대 HP의 1/4 회복한다.", false);
      Assert.That(steps, Has.Count.EqualTo(1));
    }

    [Test]
    public void StrayClosingParenthesisIsDropped()
    {
      string condition, effect;
      PotentialLibrary.Split("『에이스』가 되면 트레이너의 「통솔」이 1랭크 오른다.)", out condition, out effect);
      Assert.That(effect, Does.Not.Contain(")"));
      foreach (var step in PotentialQueue.JudgeSteps(effect, false))
        Assert.That(step.Ok, Is.True, step.Line);
    }

    [Test]
    public void BareBondRiseReadsAsStandardModule()
    {
      Assert.That(EffectNormalizer.Normalize("트레이너의 지시를 「1」랭크 올린다"), Is.EqualTo(BondModules.RiseSentence(1)));
    }

    // ---------- 5. 포텐셜 부여 ----------
    [TestCase("자신에게 『별의 주인』을 부여한다", "자신에게 『별의 주인』을 부여한다.")]
    [TestCase("자신에게 『관의주인』『주인의특권』을 부여한다.", "자신에게 『관의 주인』『주인의 특권』을 부여한다.")]
    [TestCase("자신에게 『길의 주인』『주인의 특권』을 부여한다.", "자신에게 『길의 주인』『주인의 특권』을 부여한다.")]
    [TestCase("자신에게 『해양의 주인』을 부여한다.", "자신에게 『해양의 주인』을 부여한다.")]
    public void GrantSentencesBecomeTheStandardSentence(string raw, string expected)
    {
      string once = EffectNormalizer.Normalize(raw);
      Assert.That(once, Is.EqualTo(expected));
      Assert.That(EffectNormalizer.Normalize(once), Is.EqualTo(once));
      Assert.That(EffectModules.Classify(once), Is.EqualTo(PotentialGrantModules.Kind));
    }

    [Test]
    public void UnknownGrantNameIsNotAModule()
    {
      Assert.That(PotentialGrantModules.Classify("자신에게 『없는이름』을 부여한다"), Is.Empty);
    }

    [Test]
    public void GrantAddsThePotentialOnceAndItsEffectApplies()
    {
      var giver = Rule("자신에게 『주인의 특권』을 부여한다.");
      var owner = Mon("가", "노말", giver);
      var other = Mon("나", "노말");
      int before = Engine.Stats(owner, 100, new Rules())[0];
      var first = OneIdleTurn(owner, other);
      var granted = first.Fighters[0].Data.potentials.Where(x => x.name == "주인의 특권").ToList();
      Assert.That(granted, Has.Count.EqualTo(1), string.Join("\n", first.Log));
      Assert.That(granted[0].id, Is.EqualTo("특권"));
      var second = OneIdleTurn(first.Fighters[0], other);
      Assert.That(second.Fighters[0].Data.potentials.Count(x => x.name == "주인의 특권"), Is.EqualTo(1), "이미 가졌으면 다시 부여하지 않는다");
    }

  }
}
