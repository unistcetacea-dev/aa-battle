using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace AABattle {
 [TestFixture]
 public class ModuleTriggerCatalogTests {
  public static IEnumerable<TestCaseData> TriggerDefaults() {
   return ModuleCatalog.All.Where(x => x.Kind == ModuleKind.TriggerEvent || x.Kind == ModuleKind.TriggerState)
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
   var names = ModuleCatalog.All.Where(x => x.Kind == ModuleKind.TriggerEvent || x.Kind == ModuleKind.TriggerState)
    .Select(x => x.Expected).Distinct();
   Assert.That(TriggerModules.EventModules.Concat(TriggerModules.StateModules).Except(names), Is.Empty);
  }

  // 편집기에서 되던 확정 목록 문장과 두 줄 등장 문장은 모듈 목록으로 모두 입력할 수 있다.
  [Test]
  public void EveryConfirmedTriggerSentenceCanBeFilledAndReopened() {
   var sentences = ConfirmedTriggerCatalog.Items.Concat(new[] { TriggerStructures.SelfAndOpponentEntryLabel });
   foreach (string sentence in sentences) {
    var kinds = new[] { ModuleKind.TriggerEvent, ModuleKind.TriggerState, ModuleKind.TriggerLabel };
    ModuleDefinition module = null;
    Dictionary<string, string> values = null;
    foreach (var kind in kinds)
     if (module == null) ModuleCatalog.TryRead(kind, sentence, out module, out values);
    Assert.That(module, Is.Not.Null, sentence);
    // 확정 목록의 옛 표기("자신의 체력을 회복했을 때")는 표준 문장으로 같은 뜻이다.
    Assert.That(TriggerStructures.Normalize(module.Fill(values)), Is.EqualTo(TriggerStructures.Normalize(sentence)), module.Id);
   }
  }

  [Test]
  public void SpeciesGradeModuleEditsOnlyTheArgumentSlots() {
   var module = ModuleCatalog.All.First(x => x.Id == "trigger.species.grade");
   var values = module.Defaults();
   values["등급"] = "A";
   Assert.That(module.Fill(values), Is.EqualTo("상대의 「속도」 종족치가 「A」 이하일 때"));
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

  // 「기본 조건」 묶음은 없어졌고, 합친 모듈의 선택지는 원래 고정 문장과 글자 그대로 같다.
  [Test]
  public void NoTriggerModuleIsInTheOldBasicConditionFamily() {
   var triggers = ModuleCatalog.All.Where(x => x.Kind != ModuleKind.Effect);
   Assert.That(triggers.Where(x => x.Family == "기본 조건"), Is.Empty);
  }

  [TestCase("trigger.order", "자신이", "자신이 지령을 받았을 때")]
  [TestCase("trigger.order", "아군이", "아군이 지령을 받았을 때")]
  [TestCase("trigger.selected.both", "필중 기술을", "서로 필중 기술을 선택했을 때")]
  [TestCase("trigger.selected.idle", "상대가", "상대가 공격하지 않는 행동을 선택했을 때")]
  [TestCase("trigger.order.before", "상대가 자신보다", "상대가 자신보다 먼저 행동할 때")]
  [TestCase("trigger.order.after", "자신이 상대보다", "자신이 상대보다 먼저 행동했을 때")]
  [TestCase("trigger.protect", "기술을", "「방어」 등의 기술로 상대의 기술을 무효화했을 때")]
  [TestCase("trigger.analyzedstate", "자신의 데이터가 해석되지 않았을 때", "자신의 데이터가 해석되지 않았을 때")]
  [TestCase("trigger.faintcount", "적을", "아군의 빈사 수가 상대보다 적을 때")]
  [TestCase("trigger.speed", "높을", "자신의 「속도」가 상대보다 높을 때")]
  [TestCase("trigger.floating", "서로", "서로 공중에 떠 있을 때")]
  [TestCase("trigger.switch.opponent", "임의교대", "상대가 임의교대할 때")]
  [TestCase("trigger.defeat", "아인종 포켓몬을", "아인종 포켓몬을 쓰러뜨렸을 때")]
  public void MergedChoiceModulesKeepTheOriginalSentences(string id, string choice, string sentence) {
   var module = ModuleCatalog.Find(id);
   var values = module.Defaults();
   values[module.Slots[0].Id] = choice;
   Assert.That(module.Fill(values), Is.EqualTo(sentence));
   Dictionary<string, string> read;
   Assert.That(module.TryRead(sentence, out read), Is.True, sentence);
   Assert.That(read[module.Slots[0].Id], Is.EqualTo(choice));
  }
 }
}
