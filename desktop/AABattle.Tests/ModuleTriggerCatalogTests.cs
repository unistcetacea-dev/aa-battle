using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace AABattle {
 [TestFixture]
 public class ModuleTriggerCatalogTests {
  public static IEnumerable<TestCaseData> TriggerDefaults() {
   return ModuleCatalog.All.Where(x => x.Kind != ModuleKind.Effect)
    .Select(x => new TestCaseData(x).SetName("TriggerCatalog_" + x.Id));
  }

  [TestCaseSource(nameof(TriggerDefaults))]
  public void DefaultsAreCanonicalRecognizedAndRoundTrip(ModuleDefinition module) {
   string text = module.Fill();
   var spec = TriggerModules.Parse(text);
   Assert.That(TriggerModules.IsModule(text), Is.True, text);
   Assert.That(spec.Module, Is.EqualTo(module.Expected), text);
   Assert.That(TriggerModules.Canonical(spec), Is.EqualTo(text));
   Dictionary<string, string> read;
   Assert.That(module.TryRead(text, out read), Is.True, text);
   Assert.That(read, Is.EquivalentTo(module.Defaults()));
  }

  [Test]
  public void EveryRecognizedEventAndStateHasAnInputTemplate() {
   var names = ModuleCatalog.All.Where(x => x.Kind != ModuleKind.Effect).Select(x => x.Expected).Distinct();
   Assert.That(TriggerModules.EventModules.Concat(TriggerModules.StateModules).Except(names), Is.Empty);
  }

  [TestCase("trigger.entry.method", "주체", "상대", "방식", "강제교대")]
  [TestCase("trigger.move.True", "주체", "상대", "기술조건", "「변화」")]
  [TestCase("trigger.state.상태", "주체", "아군", "이름", "화상/독")]
  [TestCase("trigger.state.상태", "주체", "자신과 상대", "이름", "상태이상")]
  [TestCase("trigger.state.상태 됨", "주체", "상대", "이름", "빈사")]
  [TestCase("trigger.rank.하락", "주체", "상대", "능력", "특공/속도")]
  [TestCase("trigger.stats.compare.높음", "왼쪽", "특공", "오른쪽", "공격")]
  [TestCase("trigger.hp.이하", "분자", "3", "분모", "4")]
  [TestCase("trigger.weatherchange", "장소", "모래바람", "장소", "모래바람")]
  [TestCase("trigger.named.부여", "이름", "특권", "이름", "특권")]
  [TestCase("trigger.flavor.name", "이름", "그린", "이름", "그린")]
  [TestCase("trigger.held.특성", "주체", "상대", "이름", "위협")]
  [TestCase("trigger.or", "조건A", "자신의 체력이 가득일 때", "조건B", "날씨가 「강한 비」일 때")]
  public void EditedSlotsKeepMeaningAndRoundTrip(string id, string a, string valueA, string b, string valueB) {
   var module = ModuleCatalog.Find(id);
   var values = module.Defaults();
   values[a] = valueA;
   values[b] = valueB;
   string text = module.Fill(values);
   var spec = TriggerModules.Parse(text);
   Assert.That(spec.Module, Is.EqualTo(module.Expected), text);
   Assert.That(TriggerModules.Canonical(spec), Is.EqualTo(text));
   Dictionary<string, string> read;
   Assert.That(module.TryRead(text, out read), Is.True, text);
   Assert.That(read, Is.EquivalentTo(values));
  }

  [TestCase("trigger.rank.상승", "trigger.rank.하락")]
  [TestCase("trigger.move.False", "trigger.move.True")]
  [TestCase("trigger.hp.이상", "trigger.hp.이하")]
  [TestCase("trigger.roomchange.False", "trigger.roomchange.True")]
  public void SeparateVariantsDoNotReadEachOthersSentences(string left, string right) {
   Dictionary<string, string> read;
   Assert.That(ModuleCatalog.Find(left).TryRead(ModuleCatalog.Find(right).Fill(), out read), Is.False);
  }
 }
}
