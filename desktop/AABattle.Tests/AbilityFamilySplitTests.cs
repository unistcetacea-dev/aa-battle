using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace AABattle
{
  [TestFixture]
  public class AbilityFamilySplitTests
  {
    static string[][] OriginalRows()
    {
      return File.ReadAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "ability-choices-before.tsv"), Encoding.UTF8)
        .Skip(1).Where(line => line.Length > 0).Select(line => line.Split('\t')).ToArray();
    }

    public static IEnumerable<TestCaseData> Choices()
    {
      return OriginalRows().Select(row => new TestCaseData(row[1], row[2], row[3], row[4], row[5])
        .SetName("OChoice_" + row[0]));
    }

    [TestCaseSource(nameof(Choices))]
    public void OriginalSentenceAndChoiceRestoreExactly(string id, string slot, string choice, string sentence, string kind)
    {
      var module = ModuleCatalog.Find(id);
      Dictionary<string,string> values;
      Assert.That(module.TryRead(sentence, out values), Is.True, sentence);
      Assert.That(values[slot], Is.EqualTo(choice));
      Assert.That(module.Fill(values), Is.EqualTo(sentence), "이전 표준 문장의 글자를 보존한다");
      Assert.That(EffectModules.Classify(module.Fill(values)), Is.EqualTo(kind));
      Assert.That(EffectNormalizer.Normalize(module.Fill(values)), Is.EqualTo(sentence), "마침표까지 재정규화 불변");
      string probabilistic = "저확률로 " + sentence;
      Assert.That(module.TryRead(probabilistic, out values), Is.True);
      Assert.That(values[slot], Is.EqualTo(choice));
      Assert.That(values["발동확률"], Is.EqualTo("저확률"));
      Assert.That(module.Fill(values), Is.EqualTo(probabilistic));
    }

    [Test]
    public void EveryRegisteredChoiceKindHasAnOriginalSentence()
    {
      foreach (var group in OriginalRows().GroupBy(row => row[1]))
        Assert.That(ModuleCatalog.Find(group.Key).ExpectedKinds,
          Is.EquivalentTo(group.Select(row => row[5]).Distinct()), group.Key);
    }

    [Test]
    public void EffectFamiliesFollowFunctionsAndNoEffectIsLast()
    {
      var effects = ModuleCatalog.All.Where(m => m.Kind == ModuleKind.Effect).ToArray();
      Assert.That(effects.Any(m => m.Family == "특성·계속 효과"), Is.False);
      Assert.That(effects.Select(m => m.Family).Distinct().Last(), Is.EqualTo("효과 없음"));
      Assert.That(ModuleCatalog.Find("ability.item.type").Family, Is.EqualTo("타입·상성"));
      Assert.That(ModuleCatalog.Find("ability.stats.copyhighest").Family, Is.EqualTo("종족치"));
      Assert.That(ModuleCatalog.Find("ability.move.accuracy").Family, Is.EqualTo("기술 변형"), "고정 명중 지정은 배율과 다르다");
    }

    static Fighter F(string effect = "", string type = "노말")
    {
      return new Fighter(new Pokemon
      {
        name = "분류검사", types = new[] { type }, level = 100,
        @base = new[] { 300, 100, 100, 100, 100, 100 }, iv = new[] { 31, 31, 31, 31, 31, 31 },
        moves = new[] { "몸통박치기" },
        potentials = effect.Length == 0 ? new Potential[0] : new[] { new Potential
        { id = "범용", name = "보존검사", description = effect, triggers = new[] { TriggerStructures.AlwaysLabel } } }
      });
    }

    static string Reopen(string id, string sentence)
    {
      Dictionary<string,string> values;
      var module = ModuleCatalog.Find(id);
      Assert.That(module.TryRead(sentence, out values), Is.True);
      return module.Fill(values);
    }

    [TestCase("필드에 있는 전원", 2, 2)]
    [TestCase("자신을 제외한 전원", 1, 2)]
    public void FieldScalarKeepsItsOwnerAndOpponentScope(string subject, double ownerRate, double opponentRate)
    {
      string sentence = Reopen("ability.field.scalar.stats", subject + "의 「공격」을 강화(2배)한다.");
      var owner = F(sentence);
      var opponent = F();
      Assert.That(PotentialModifiers.Stat(owner, opponent, 1, new Rules()), Is.EqualTo(ownerRate));
      Assert.That(PotentialModifiers.Stat(opponent, owner, 1, new Rules()), Is.EqualTo(opponentRate));
    }

    [TestCase("자신의 기술은 「물」 타입에게 필중이다.", "물", true)]
    [TestCase("자신의 기술은 「물」 타입에게 필중이다.", "노말", false)]
    [TestCase("자신의 「특수」 기술은 필중이다.", "노말", false)]
    public void SureHitKeepsTypeAndMoveConditions(string original, string foeType, bool sure)
    {
      var owner = F(Reopen("ability.surehit", original));
      var move = TestData.Db.moves.First(m => m.name == "몸통박치기");
      Assert.That(double.IsPositiveInfinity(Mechanics.Accuracy(owner, F("", foeType), move, new Rules())), Is.EqualTo(sure));
    }

    [Test]
    public void FieldSureHitStillAffectsTheOtherSide()
    {
      var holder = F(Reopen("ability.surehit", "필드에 있는 전원의 기술은 필중이다."));
      var attacker = F();
      Assert.That(double.IsPositiveInfinity(Mechanics.Accuracy(attacker, holder,
        TestData.Db.moves.First(m => m.name == "몸통박치기"), new Rules())), Is.True);
    }

    [TestCase("자신의 「노말」 기술의 반동 대미지를 받지 않는다.", 25)]
    [TestCase("자신의 기술의 반동 대미지를 받지 않는다.", 0)]
    public void RecoilChoiceKeepsConditionalAndWholeMoveProtection(string original, int loss)
    {
      var owner = F(Reopen("ability.recoil.condition", original));
      var move = TestData.Db.moves.First(m => m.name == "몸통박치기").Copy();
      move.types = new[] { "불꽃" };
      move.effect = "준 대미지의 1/4만큼 반동 대미지를 받는다.";
      int before = owner.HP;
      MoveModules.Recoil(owner, F(), move, new Rules(), 100, new List<string>());
      Assert.That(before - owner.HP, Is.EqualTo(loss));
    }

    [TestCase("접지한 상대는 「통상교대」를 할 수 없다.", "비행", false)]
    [TestCase("접지한 상대는 「통상교대」를 할 수 없다.", "노말", true)]
    [TestCase("「강철」 타입인 상대는 「통상교대」를 할 수 없다.", "노말", false)]
    [TestCase("「강철」 타입인 상대는 「통상교대」를 할 수 없다.", "강철", true)]
    public void TrapChoiceKeepsGroundingAndTypeConditions(string original, string type, bool trapped)
    {
      var holder = F(Reopen("ability.switch.trap", original));
      Assert.That(AbilityStateModules.SwitchTrapReason(holder, F("", type)).Length > 0, Is.EqualTo(trapped));
    }
  }
}
