using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace AABattle
{
  // 실프 인헨스먼츠 남은 모듈(MODULE_GAPS 묶음 A): 반전 · 우선도 조건 · 같은 등급 · 내림/타입 대미지 · 타입 변경 · 위력 약화 · 치유 봉인 · 기술 지정 · 전용 · 하프 · 반납 · 새 상태
  [TestFixture]
  public class AbaddonGapTests
  {
    static Fighter F(string type = "노말") { return AbilityRemaining3MoveTests.F("", type); }
    static Move M(string name) { return AbilityRemaining3MoveTests.M(name); }
    static void Rule(Fighter f, string text) { AbilityRemaining3MoveTests.Rule(f, text); }

    static TurnResult Turn(Fighter a, Fighter b, Move am, Move bm, Rules rules = null, Func<double> rng = null)
    {
      return Engine.Resolve(TestData.Db, new[] { a, b }, new[] { am, bm ?? AbilityRemaining3MoveTests.Idle() }, rules ?? new Rules(), rng ?? (() => 0));
    }

    [Test]
    public void InvertFlipsEveryRankAndRaisesTheInvertEvent()
    {
      var a = F();
      var b = F();
      b.Stages[1] = 2;
      b.Stages[2] = -1;
      Rule(a, "자신의 공격이 명중했을 때, 상대의 능력 변화를 반전한다.");
      Rule(a, "상대의 능력이 반전했을 때, 자신의 「속도」가 오른다.");
      var t = Turn(a, b, M("몸통박치기"), null);
      Assert.That(t.Fighters[1].Stages[1], Is.EqualTo(-2), string.Join("\n", t.Log));
      Assert.That(t.Fighters[1].Stages[2], Is.EqualTo(1));
      Assert.That(t.Fighters[0].Stages[5], Is.EqualTo(1), "반전 사건 트리거");
    }

    [Test]
    public void InvertTurnStateHoldsOnlyAfterTheInversionThatTurn()
    {
      var a = F();
      var b = F();
      var rules = new Rules();
      Assert.That(TriggerModules.Holds("상대의 능력이 반전한 T", a, b, rules), Is.False);
      a.Conditions.TurnFacts.Add(new TriggerFact { Kind = "능력 반전", Self = false });
      Assert.That(TriggerModules.Holds("상대의 능력이 반전한 T", a, b, rules), Is.True);
    }

    [Test]
    public void PriorityConditionHoldsOnlyForAMoveWithEnoughPriority()
    {
      var a = F();
      var b = F();
      var rules = new Rules();
      const string clause = "자신의 기술이 우선도 「+1」이상일 때";
      a.Conditions.Acting = true;
      a.Conditions.ActingMove = M("몸통박치기");
      Assert.That(TriggerModules.Holds(clause, a, b, rules), Is.False);
      a.Conditions.ActingMove = M("전광석화");
      Assert.That(TriggerModules.Holds(clause, a, b, rules), Is.True);
      Assert.That(TriggerModules.Normalize(clause), Is.EqualTo("자신의 기술의 우선도가 「+1」 이상일 때"));
    }

    [Test]
    public void PriorityConditionMakesTheMoveSureHit()
    {
      var a = F();
      var b = F();
      Rule(a, "자신의 기술이 우선도 「+1」이상일 때, 자신의 기술이 「필중」한다");
      var slow = new Move { name = "검사", category = "물리", types = new[] { "노말" }, power = 40, accuracy = 70, priority = 0, target = "상대", tags = new string[0], effect = "" };
      var quick = slow.Copy();
      quick.priority = 1;
      a.Conditions.Acting = true;
      a.Conditions.ActingMove = slow;
      Assert.That(double.IsPositiveInfinity(Mechanics.Accuracy(a, b, slow, new Rules())), Is.False);
      a.Conditions.ActingMove = quick;
      Assert.That(double.IsPositiveInfinity(Mechanics.Accuracy(a, b, quick, new Rules())), Is.True);
    }

    [TestCase(100, 105, true)]
    [TestCase(100, 109, true)]
    [TestCase(100, 110, false)]
    [TestCase(89, 90, false)]
    public void SameSpeciesGradeComparesTheGradeBand(int mine, int theirs, bool expected)
    {
      var a = F();
      var b = F();
      a.Data.@base[5] = mine;
      b.Data.@base[5] = theirs;
      Assert.That(TriggerModules.Holds("상대의 「속도」의 종족치가 자신과 동랭크일 때", a, b, new Rules()), Is.EqualTo(expected));
    }

    [TestCase("상대의 「방어」를 크게 내린다.", 1)]
    [TestCase("상대의 「방어」를 내린다.", 0)]
    public void BigLoweringByYouRaisesTheLoweredEventOnlyForTwoRanks(string lower, int expectedSpeed)
    {
      var a = F();
      var b = F();
      Rule(a, "자신의 공격이 명중했을 때, " + lower);
      Rule(a, "자신이 상대의 「방어」를 크게 내렸을 때, 자신의 「속도」가 오른다.");
      var t = Turn(a, b, M("몸통박치기"), null);
      Assert.That(t.Fighters[0].Stages[5], Is.EqualTo(expectedSpeed), string.Join("\n", t.Log));
    }

    [Test]
    public void TypedFractionDamageTakesTypeEffectiveness()
    {
      int Extra(string defenderType, bool withRule)
      {
        var a = F();
        var b = F(defenderType);
        if (withRule)
          Rule(a, "자신의 공격이 명중했을 때, 상대에게 최대 체력의 1/8의 「악」데미지를 준다.");
        int before = b.HP;
        var t = Turn(a, b, M("전기쇼크"), null);
        return before - t.Fighters[1].HP;
      }

      int normal = Extra("노말", true) - Extra("노말", false);
      int fairy = Extra("페어리", true) - Extra("페어리", false);
      int ghost = Extra("고스트", true) - Extra("고스트", false);
      Assert.That(normal, Is.GreaterThan(0));
      Assert.That(fairy, Is.InRange(normal / 2 - 1, normal / 2 + 1), "「악」은 페어리에게 반감");
      Assert.That(ghost, Is.InRange(normal * 2 - 2, normal * 2 + 2), "「악」은 고스트에게 2배");
    }

    [Test]
    public void TypeChangeRaisesTheEventAndPoisonsTheOpponentsMoveThatTurn()
    {
      var a = F();
      var b = F();
      Rule(a, "자신의 공격이 명중했을 때, 상대의 타입을 「독」으로 한다.");
      Rule(a, "상대의 타입이 바뀌었을 때, 자신의 「속도」가 오른다.");
      var t = Turn(a, b, M("몸통박치기"), null);
      Assert.That(t.Fighters[1].Data.types, Is.EqualTo(new[] { "독" }));
      Assert.That(t.Fighters[0].Stages[5], Is.EqualTo(1), string.Join("\n", t.Log));
      var owner = F();
      var foe = F();
      Rule(owner, "상대의 타입이 「독」이 된 T, 상대가 내보내는 기술이 「독」타입이 된다");
      Assert.That(Mechanics.JudgedMove(foe, M("몸통박치기"), owner, new Rules()).types, Is.EqualTo(new[] { "노말" }));
      owner.Conditions.TurnFacts.Add(new TriggerFact { Kind = "타입 바뀜", Self = false, Method = "독" });
      Assert.That(Mechanics.JudgedMove(foe, M("몸통박치기"), owner, new Rules()).types, Is.EqualTo(new[] { "독" }));
    }

    [TestCase(1, 0.9)]
    [TestCase(2, 0.8)]
    [TestCase(3, 0.7)]
    public void PowerWeakensByTheAttackersLoweredRanks(int lowered, double expected)
    {
      var owner = F();
      var attacker = F();
      Rule(owner, "상대 능력의 「-」에 비례해, 상대 기술의 위력을 완화한다\n(-1=0.9배 -2=0.8배)");
      attacker.Stages[1] = -lowered;
      Assert.That(PotentialModifiers.Power(attacker, owner, M("몸통박치기"), new Rules(), 1), Is.EqualTo(expected).Within(1e-9));
      attacker.Stages[1] = 0;
      Assert.That(PotentialModifiers.Power(attacker, owner, M("몸통박치기"), new Rules(), 1), Is.EqualTo(1));
    }

    [Test]
    public void StatusCureIsSealedButNaturalWakingIsNot()
    {
      var owner = F();
      var target = F();
      Rule(owner, "소속이 《실프》일 때, 상대의 「상태이상」이 해제되지 않는다");
      target.Status = "화상";
      var rules = new Rules();
      using (EffectScope.Begin(new[] { owner, target }, rules, () => 0))
      {
        var log = new List<string>();
        Assert.That(StateCure.Cure(target, new[] { "상태이상" }, log), Is.False);
        Assert.That(target.Status, Is.EqualTo("화상"));
        Assert.That(StateCure.Cure(owner, new[] { "상태이상" }, log), Is.False, "아군에게는 상태이상이 없다");
      }

      var plainOwner = F();
      using (EffectScope.Begin(new[] { plainOwner, target }, rules, () => 0))
        Assert.That(StateCure.Cure(target, new[] { "상태이상" }, new List<string>()), Is.True);
      var sleeper = F();
      sleeper.Status = "잠듦";
      sleeper.Conditions.SleepTurns = 0;
      var t = Turn(sleeper, owner, M("몸통박치기"), null);
      Assert.That(t.Fighters[0].Status, Is.EqualTo("정상"), "턴 경과로 깨어나는 것은 막지 않는다");
    }

    [Test]
    public void NamedMoveRecoilContactAndTypeRewrites()
    {
      var a = F();
      var b = F();
      Rule(a, "「무릎차기」의 반동 데미지를 받지 않는다");
      Assert.That(AbilityPhrases.IgnoresMoveRecoil(a, new Rules(), M("무릎차기")), Is.True);
      Assert.That(AbilityPhrases.IgnoresMoveRecoil(a, new Rules(), M("몸통박치기")), Is.False);
      var blue = F();
      Rule(blue, "「푸른불꽃」이 「접촉기술」이 되어, 상대의 방호특성을 관통한다");
      var fire = M("푸른불꽃");
      Assert.That(Mechanics.Contact(fire), Is.False);
      Assert.That(Mechanics.Contact(Mechanics.JudgedMove(blue, fire, b, new Rules())), Is.True);
      Assert.That(Mechanics.Contact(Mechanics.JudgedMove(blue, M("화염방사"), b, new Rules())), Is.False);
      var dark = F();
      Rule(dark, "자신의 「접촉기술」의 타입을 「다크」타입으로 한다");
      Assert.That(Mechanics.JudgedMove(dark, M("몸통박치기"), b, new Rules()).types, Is.EqualTo(new[] { "악" }));
    }

    [Test]
    public void FirstContactMovePriorityIsRaisedOnlyUntilTheFirstMove()
    {
      var a = F();
      var b = F();
      Rule(a, "아군이 필드에 나와 최초로 내보내는 「접촉」기술의 우선도를 +2할수 있다");
      var move = M("몸통박치기");
      int baseline = move.priority;
      a.Conditions.MovedSinceEntry = false;
      Assert.That(Mechanics.Priority(a, move, b), Is.EqualTo(baseline + 2));
      a.Conditions.MovedSinceEntry = true;
      Assert.That(Mechanics.Priority(a, move, b), Is.EqualTo(baseline));
    }

    [Test]
    public void ExclusiveReceivedEventFiresWhenAnExclusivePotentialActivates()
    {
      var a = F();
      var b = F();
      a.Data.potentials = new[]
      {
        new Potential { id = TeamRules.ExclusiveSlot, name = "시험 전용", description = "자신의 공격이 명중했을 때, 자신의 「공격」이 오른다.", triggers = new string[0] },
        new Potential { id = "종족 ①", name = "받음", description = "『전용』을 받았을 때, 자신의 「속도」가 오른다.", triggers = new string[0] }
      };
      var t = Turn(a, b, M("몸통박치기"), null);
      Assert.That(t.Fighters[0].Stages[1], Is.EqualTo(1), string.Join("\n", t.Log));
      Assert.That(t.Fighters[0].Stages[5], Is.EqualTo(1), string.Join("\n", t.Log));
    }

    [Test]
    public void HalfWeakensDamageFromMovesOfTheDefendersOwnType()
    {
      var defender = F("전기");
      var attacker = F();
      Rule(defender, "자신과 같은 타입의 기술의 데미지를 반감한다");
      Assert.That(PotentialModifiers.Damage(attacker, defender, M("전기쇼크"), new Rules(), 1), Is.EqualTo(0.5));
      Assert.That(PotentialModifiers.Damage(attacker, defender, M("몸통박치기"), new Rules(), 1), Is.EqualTo(1));
    }

    [TestCase(0.5, 0)]
    [TestCase(0.0, 1)]
    public void FullHealIsRepaidAtTurnEndWithoutFainting(double hpRate, int exactHp)
    {
      var a = F();
      var b = F();
      Rule(a, "자신의 공격이 명중했을 때, 자진의 포켓몬의 체력을 가득 회복한다.\n회복한 체력만큼 턴 끝에 대미지를 입는다(빈사 불가).\n이 포텐셜은 자신이 필드에 없어도 발동한다.");
      int start = exactHp > 0 ? 1 : (int)(a.HP * hpRate);
      a.HP = start;
      var t = Turn(a, b, M("몸통박치기"), null);
      Assert.That(t.Fighters[0].HP, Is.EqualTo(start), string.Join("\n", t.Log));
      Assert.That(t.Log.Any(x => x.Contains("반납")), Is.True, string.Join("\n", t.Log));
    }

    [Test]
    public void ThunderCloudSchedulesAnUnblockableDelayedAttack()
    {
      var a = F();
      var b = F();
      var t = Turn(a, b, M("번개구름"), null);
      Assert.That(Mechanics.HasCondition(t.Fighters[1], "번개구름"), Is.True, string.Join("\n", t.Log));
      Assert.That(StateEffects.Find("번개구름"), Is.Not.Null);
      // 문장으로 거는 경우: 이미 걸려 있으면 실패한다
      var x = F();
      var y = F();
      var rules = new Rules();
      using (EffectScope.Begin(new[] { x, y }, rules, () => 0, null, TestData.Db))
      {
        var log = new List<string>();
        Assert.That(StateEffects.Apply("상대를 「번개구름」 상태로 한다", x, y, rules, () => 0, log, "시험"), Is.EqualTo(EffectOutcome.Success), string.Join("\n", log));
        Assert.That(Mechanics.HasCondition(y, "번개구름"), Is.True);
        Assert.That(StateEffects.Apply("상대를 「번개구름」 상태로 한다", x, y, rules, () => 0, log, "시험"), Is.EqualTo(EffectOutcome.Failed));
      }
    }

    [TestCase(0.0, true)]
    [TestCase(0.99, false)]
    public void ChanceIndirectDamageImmunityRollsPerDamage(double roll, bool ignored)
    {
      var f = F();
      var foe = F();
      Rule(f, "중확률로 상대의 공격 이외의 대미지를 무시한다");
      int before = f.HP;
      using (EffectScope.Begin(new[] { f, foe }, new Rules(), () => roll))
        Mechanics.Hurt(f, foe, new Rules(), 10, "독", new List<string>());
      Assert.That(f.HP == before, Is.EqualTo(ignored));
    }

    [Test]
    public void IceBurnLowersPriorityOnEntryAndIsRemovedByFireOrIce()
    {
      var rules = new Rules();
      Assert.That(Mechanics.InstallHazard(1, "아이스번", rules, new List<string>()), Is.True);
      Assert.That(Mechanics.InstallHazard(1, "아이스번", rules, new List<string>()), Is.False);
      var normal = F();
      var log = new List<string>();
      Mechanics.EnterField(TestData.Db, normal, F(), 1, rules, () => 0, log);
      Assert.That(normal.Conditions.PriorityModifier, Is.EqualTo(-1), string.Join("\n", log));
      Assert.That(rules.Sides[1].IceBurn, Is.True);
      var fire = F("불꽃");
      Mechanics.EnterField(TestData.Db, fire, F(), 1, rules, () => 0, new List<string>());
      Assert.That(rules.Sides[1].IceBurn, Is.False);
      Assert.That(fire.Conditions.PriorityModifier, Is.EqualTo(0));
    }

    [TestCase("상대의 능력 변화를 반전할 수 있다", "능력 반전")]
    [TestCase("상대에게 최대 HP의 1/8 「악」 타입 대미지를 준다.", "타입 대미지")]
    [TestCase("상대의 기술을 「독」 타입으로 한다.", "상대 기술 타입 변경")]
    [TestCase("상대의 「상태이상」은 치유되지 않는다.", "상태이상 치유 봉인")]
    public void StandardSentencesClassifyAndReadBackFromTheEditorCatalog(string sentence, string kind)
    {
      Assert.That(EffectModules.Classify(sentence), Is.EqualTo(kind));
      string normalized = EffectNormalizer.Normalize(sentence);
      Dictionary<string, string> values;
      Assert.That(ModuleCatalog.All.Any(m => m.Kind == ModuleKind.Effect && m.TryRead(normalized.TrimEnd('.') + ".", out values)), Is.True, normalized);
    }
  }
}
