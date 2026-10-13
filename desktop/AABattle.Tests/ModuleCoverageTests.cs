using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle {
 [TestFixture]
 public class ModuleCoverageTests {
  // 포텐셜 도감·특성 도감·소지품 도감의 효과 문장이 돌려주는 종류 이름을 모은다.
  static IEnumerable<string> CorpusSentences() {
   foreach (var potential in PotentialLibrary.Load())
    foreach (string line in (potential.record.effect ?? "").Split('\n'))
     yield return line.Trim();
   foreach (var ability in AbilityCatalog.All)
    foreach (string line in ability.effects)
     yield return line.Trim();
   foreach (var item in ItemCatalog.All)
    foreach (string line in (item.Effect ?? "").Split('\n'))
     yield return line.Trim();
  }

  [Test]
  public void EveryEffectKindInTheCorporaIsRegisteredOrOmittedWithReason() {
   var registered = new HashSet<string>(ModuleCatalog.All.Where(x => x.Kind == ModuleKind.Effect).Select(x => x.Expected));
   var missing = new SortedSet<string>();
   var corpus = CorpusSentences().Where(x => x.Length > 0).Distinct().ToList();
   Assert.That(corpus.Count, Is.GreaterThan(500), "도감 문장을 읽지 못했다");
   foreach (string sentence in corpus) {
    string kinds = EffectModules.Classify(sentence);
    foreach (string kind in kinds.Split(new[] { " + " }, System.StringSplitOptions.RemoveEmptyEntries))
     if (!registered.Contains(kind) && !ModuleCatalog.OmittedEffectKinds.ContainsKey(kind)) missing.Add(kind + "  <=  " + sentence);
   }
   Assert.That(missing, Is.Empty, string.Join("\n", missing.Take(80)));
  }

  [Test]
  public void OmittedKindsHaveReasonAndAreNotAlsoRegistered() {
   var registered = new HashSet<string>(ModuleCatalog.All.Where(x => x.Kind == ModuleKind.Effect).Select(x => x.Expected));
   foreach (var pair in ModuleCatalog.OmittedEffectKinds) {
    Assert.That(pair.Value.Trim().Length, Is.GreaterThan(5), pair.Key);
    Assert.That(registered.Contains(pair.Key), Is.False, pair.Key + " 은 등록되어 있다");
   }
  }

  [Test]
  public void EveryTriggerModuleNameIsRegistered() {
   var registered = new HashSet<string>(ModuleCatalog.All
    .Where(x => x.Kind == ModuleKind.TriggerEvent || x.Kind == ModuleKind.TriggerState).Select(x => x.Expected));
   Assert.That(TriggerModules.EventModules.Concat(TriggerModules.StateModules).Where(x => !registered.Contains(x)), Is.Empty);
  }
 }
}
