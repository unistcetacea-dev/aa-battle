using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle
{
  [TestFixture]
  public class ModuleEffectCoreTests
  {
    static IEnumerable<TestCaseData> CoreModules()
    {
      return ModuleCatalog.All.Where(x => x.Kind == ModuleKind.Effect &&
        ModuleCatalog.CoreEffectIds.Contains(x.Id))
        .Select(x => new TestCaseData(x).SetName("CoreSchema_" + x.Id));
    }

    [TestCaseSource(nameof(CoreModules))]
    public void DefaultSentenceClassifiesAndRestoresSlots(ModuleDefinition module)
    {
      var defaults = module.Defaults();
      string sentence = module.Fill(defaults);
      Assert.That(EffectModules.Classify(sentence), Is.EqualTo(module.Expected), sentence);
      Dictionary<string, string> restored;
      Assert.That(module.TryRead(sentence, out restored), Is.True, sentence);
      Assert.That(restored, Is.EquivalentTo(defaults));
    }

    [TestCase("effect.scalar.move.위력강화", "조건", "「불꽃」「물리」")]
    [TestCase("effect.scalar.damage.받는약화", "주체", "상대")]
    [TestCase("effect.type.change.한다", "타입", "불꽃")]
    [TestCase("effect.place.weather.한다", "날씨", "쾌청")]
    [TestCase("effect.rank.raise2", "대상", "상대의 가장 낮은 능력")]
    [TestCase("effect.hp.대미지 보호", "분모", "8")]
    [TestCase("effect.secondary.rank", "능력", "방어/특방")]
    [TestCase("effect.extension", "기술", "몸통박치기/불대문자")]
    [TestCase("effect.baton.transfer.할 수 있다", "인계", "「능력변화」「상태변화」")]
    [TestCase("effect.move.repeat", "배율", "0.25")]
    [TestCase("effect.state.cure.named", "상태", "독/혼란")]
    [TestCase("effect.weather.ignore", "날씨", "쾌청/비")]
    [TestCase("effect.place.extend", "상태", "리플렉터/빛의장막")]
    public void EditedSlotsRemainExecutableAndReopen(string id, string key, string value)
    {
      var module = ModuleCatalog.Find(id);
      var edited = module.Defaults();
      edited[key] = value;
      string sentence = module.Fill(edited);
      Assert.That(EffectModules.Classify(sentence), Is.EqualTo(module.Expected), sentence);
      Dictionary<string, string> restored;
      Assert.That(module.TryRead(sentence, out restored), Is.True, sentence);
      Assert.That(restored, Is.EquivalentTo(edited));
    }

    [Test]
    public void ParticleReaderRejectsSentenceThatFormatterWouldNotProduce()
    {
      Dictionary<string, string> values;
      Assert.That(ModuleCatalog.Find("effect.type.change.한다").TryRead("상대의 타입을 「불꽃」로 한다.", out values), Is.False);
      Assert.That(ModuleCatalog.Find("effect.rank.raise2").TryRead("자신의 「공격」가 크게 오른다.", out values), Is.False);
    }
  }
}


