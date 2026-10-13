using System.Linq;
using NUnit.Framework;

namespace AABattle
{
  // 『전투속행』: 상대 기술의 대미지를 자신의 행동 후로 미룬다
  [TestFixture]
  public class DeferredDamageTests
  {
    static int ids;

    const string Desc = "저확률로 상대의 기술의 데미지를 자신의 행동 후로 미룬다.";

    static Fighter Mon(int speed, bool continuing, int hp = 0)
    {
      var potentials = continuing
        ? new[] { new Potential { name = "전투속행", description = Desc, triggers = new string[0] } }
        : new Potential[0];
      var f = new Fighter(new Pokemon
      {
        id = "defer" + ++ids, name = "지연검사" + ids, ability = "", item = "", level = 100,
        types = new[] { "노말" }, @base = new[] { 300, 100, 100, 100, 100, speed },
        iv = new[] { 31, 31, 31, 31, 31, 31 }, moves = new[] { "몸통박치기" }, potentials = potentials
      });
      if (hp > 0)
        f.HP = hp;
      return f;
    }

    static Move Move(string name)
    {
      return TestData.Db.moves.First(m => m.name == name);
    }

    static TurnResult Run(Fighter a, Fighter b, string moveA, string moveB, double roll, string form = "", Fighter bench = null)
    {
      return Engine.Resolve(TestData.Db, new[] { a, b }, new[]
      {
        new BattleCommand { Move = Move(moveA) },
        new BattleCommand { Move = Move(moveB), Form = form, Party = bench == null ? new Fighter[0] : new[] { bench } }
      }, new Rules(), new[] { new TrainerBattleState(), new TrainerBattleState() }, () => roll, q => q.Candidates.First());
    }

    static int Index(TurnResult t, string text)
    {
      return t.Log.FindIndex(l => l.Contains(text));
    }

    [Test]
    public void StandardSentenceIsRecognizedFromBothSpellings()
    {
      string expected = "자신은 상대의 기술의 대미지를 저확률로 자신의 행동 후로 미룬다.";
      Assert.That(EffectNormalizer.Normalize(Desc), Is.EqualTo(expected));
      Assert.That(EffectNormalizer.Normalize(Desc.Replace("데미지", "대미지")), Is.EqualTo(expected));
      Assert.That(EffectModules.Classify(expected), Is.EqualTo("대미지 지연"));
    }

    [Test]
    public void DefenderActsFromFullStrengthThenTakesTheDeferredDamage()
    {
      var attacker = Mon(300, false);
      var defender = Mon(50, true, 1);
      var turn = Run(attacker, defender, "몸통박치기", "몸통박치기", .1);
      string text = string.Join("\n", turn.Log);
      int deferred = Index(turn, "미뤘다");
      int defenderAction = Index(turn, defender.Data.name + "의 몸통박치기!");
      int applied = Index(turn, "보류한 대미지");
      Assert.That(deferred, Is.GreaterThanOrEqualTo(0), text);
      Assert.That(defenderAction, Is.GreaterThan(deferred), text);
      Assert.That(applied, Is.GreaterThan(defenderAction), text);
      Assert.That(turn.Fighters[0].HP, Is.LessThan(attacker.HP), "체력 1이라도 행동은 정상 진행: " + text);
      Assert.That(turn.Fighters[1].HP, Is.Zero, text);
    }

    [Test]
    public void DefenderThatAlreadyActedTakesDamageAtOnce()
    {
      var attacker = Mon(50, false);
      var defender = Mon(300, true, 1);
      var turn = Run(attacker, defender, "몸통박치기", "몸통박치기", .1);
      string text = string.Join("\n", turn.Log);
      Assert.That(Index(turn, "미뤘다"), Is.LessThan(0), text);
      Assert.That(turn.Fighters[1].HP, Is.Zero, text);
    }

    [Test]
    public void FailedRollMeansNormalDamage()
    {
      var attacker = Mon(300, false);
      var defender = Mon(50, true, 1);
      var turn = Run(attacker, defender, "몸통박치기", "몸통박치기", .5);
      string text = string.Join("\n", turn.Log);
      Assert.That(Index(turn, "미뤘다"), Is.LessThan(0), text);
      Assert.That(Index(turn, defender.Data.name + "의 몸통박치기!"), Is.LessThan(0), "쓰러져서 행동하지 못한다: " + text);
      Assert.That(turn.Fighters[1].HP, Is.Zero, text);
    }

    [Test]
    public void DeferredDamageIsAppliedBeforeTheDefenderSwitchesOut()
    {
      var attacker = Mon(300, false);
      var defender = Mon(50, true);
      var bench = Mon(10, false);
      bench.OnField = false;
      var turn = Run(attacker, defender, "몸통박치기", "유턴", .1, "", bench);
      string text = string.Join("\n", turn.Log);
      int deferred = Index(turn, "미뤘다");
      int applied = Index(turn, "보류한 대미지");
      int switched = Index(turn, "등장!");
      Assert.That(deferred, Is.GreaterThanOrEqualTo(0), text);
      Assert.That(applied, Is.GreaterThan(deferred), text);
      Assert.That(switched, Is.GreaterThan(applied), text);
      Assert.That(turn.SwitchedOut[1].HP, Is.LessThan(defender.HP), text);
    }

    [Test]
    public void EnduringIsJudgedWhenTheDamageIsActuallyApplied()
    {
      var attacker = Mon(300, false);
      var defender = Mon(50, true, 1);
      var turn = Run(attacker, defender, "몸통박치기", "몸통박치기", .1, "버텨라！");
      string text = string.Join("\n", turn.Log);
      int deferred = Index(turn, "미뤘다");
      int applied = Index(turn, "보류한 대미지");
      int endured = Index(turn, "버텼다");
      Assert.That(deferred, Is.GreaterThanOrEqualTo(0), text);
      Assert.That(endured, Is.GreaterThan(applied), text);
      Assert.That(turn.Fighters[1].HP, Is.EqualTo(1), text);
    }

    [Test]
    public void SeveralDeferredHitsAreAppliedOnceAsTheirSum()
    {
      var attacker = Mon(300, false);
      var defender = Mon(50, true);
      var turn = Run(attacker, defender, "더블어택", "몸통박치기", .1);
      string text = string.Join("\n", turn.Log);
      var deferred = turn.Log.Where(l => l.Contains("미뤘다")).ToList();
      var applied = turn.Log.Where(l => l.Contains("보류한 대미지")).ToList();
      Assert.That(deferred.Count, Is.EqualTo(2), text);
      Assert.That(applied.Count, Is.EqualTo(1), text);
      int sum = deferred.Sum(l => int.Parse(System.Text.RegularExpressions.Regex.Match(l, @"대미지 (\d+)").Groups[1].Value));
      Assert.That(applied[0], Does.Contain("보류한 대미지 " + sum + " 적용"), text);
    }
  }
}
