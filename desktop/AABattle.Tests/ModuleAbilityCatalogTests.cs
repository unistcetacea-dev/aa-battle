using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace AABattle {
 [TestFixture]
 public class ModuleAbilityCatalogTests {
  public static IEnumerable<TestCaseData> Defaults() {
   return ModuleCatalog.All.Where(x => x.Id.StartsWith("ability."))
    .Select(x => new TestCaseData(x).SetName("AbilityCatalog_" + x.Id));
  }

  [TestCaseSource(nameof(Defaults))]
  public void DefaultEffectIsRecognizedAndRoundTrips(ModuleDefinition module) {
   string text = module.Fill();
   Assert.That(EffectModules.Classify(text), Is.EqualTo(module.Expected), text);
   Dictionary<string, string> read;
   Assert.That(module.TryRead(text, out read), Is.True, text);
   Assert.That(read, Is.EquivalentTo(module.Defaults()));
  }

  [TestCase("ability.move.type", "타입", "강철")]
  [TestCase("ability.move.power", "위력", "150")]
  [TestCase("ability.rank.set", "랭크", "-3")]
  [TestCase("ability.survive.chance", "확률", "중확률로")]
  [TestCase("ability.status.random", "상태", "화상/독")]
  [TestCase("ability.field.healtype", "분모", "16")]
  [TestCase("ability.floating.hit", "기술조건", "「물리」")]
  public void EditedSlotsRoundTrip(string id, string key, string value) {
   var module = ModuleCatalog.Find(id);
   var values = module.Defaults();
   values[key] = value;
   string text = module.Fill(values);
   Assert.That(EffectModules.Classify(text), Is.EqualTo(module.Expected), text);
   Dictionary<string, string> read;
   Assert.That(module.TryRead(text, out read), Is.True, text);
   Assert.That(read, Is.EquivalentTo(values));
  }
 }
}
