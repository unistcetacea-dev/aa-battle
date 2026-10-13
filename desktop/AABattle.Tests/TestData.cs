using System;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using NUnit.Framework;

// WinForms 컨트롤을 만드는 검사가 있으므로 전부 STA 스레드에서 실행한다
[assembly: Apartment(ApartmentState.STA)]

namespace AABattle {
 // 내장 데이터는 한 번만 읽어 모든 검사가 공유한다
 static class TestData {
  static Database db;
  public static Database Db { get { if (db == null) { Theme.Instant = true; db = Database.Load(); } return db; } }
 }

 // 기존 파일별 자체 검사를 각각 하나의 테스트로 실행한다
 [TestFixture] public class SuiteTests {
  [Test] public void PriorityEffects() => PriorityEffectTests.Run(TestData.Db);
  [Test] public void FieldStates() => FieldStateTests.Run(TestData.Db);
  [Test] public void SwitchRuntime() => SwitchRuntimeTests.Run(TestData.Db);
  [Test] public void TriggerBatch() => TriggerBatchTests.Run();
  [Test] public void Engine() { Assert.That(AABattle.Engine.Test(TestData.Db), Does.StartWith("PASS")); }
  [Test] public void EntryTriggers() => AABattle.EntryTriggers.Test();
  [Test] public void Catalogs() => CatalogTests.Run();
 }

 // 편집기 데이터 구조·템플릿 검사(예전 AABattleDataEditor --self-test)
 [TestFixture] public class EditorTests {
  [Test] public void ProjectRoundtrip() {
   var p = new EditorProject(); var t = new TrainerRecord { name = "T" }; t.potentials = EditorTemplate.Orders(false); p.trainers.Add(t);
   var mon = new PokemonRecord { name = "P" }; mon.potentials = EditorTemplate.PokemonSlots(); p.pokemon.Add(mon);
   var q = new JavaScriptSerializer().Deserialize<EditorProject>(new JavaScriptSerializer().Serialize(p));
   Assert.That(q.trainers.Count == 1 && q.pokemon.Count == 1, "roundtrip");
   Assert.That(t.potentials.Count, Is.EqualTo(10), "base four and six orders");
   Assert.That(mon.potentials.Count == EditorTemplate.PokemonSlotNames.Length && mon.potentials[3].slot == "이명" && mon.potentials.Last().slot == "PT ②", "pokemon potential slots");
  }
  [Test] public void PotentialChoices() {
   Assert.That(EditorTemplate.TrainerSlotNames, Is.EqualTo(new[] { "고유", "지령" }), "trainer potential types");
   Assert.That(EditorTemplate.Role("에이스") != null && EditorTemplate.Role("에이스").effect.Contains("1.33배"), "fixed role potential");
   Assert.That(EditorTemplate.Role("탐사대원").adjunct == "프라이멀 헌트" && EditorTemplate.Role("탐사대장").adjunct == "프라이멀 헌트" && EditorTemplate.Role("조사대원").adjunct == "리서치 헌트", "Abaddon role adjuncts");
   Assert.That(EditorTemplate.FixedOption("범용", "군략").effect.Contains("1.2배"), "fixed common potential");
   Assert.That(EditorTemplate.FixedOption("선", "삼종의 신기").effect.Contains("육성：AA"), "fixed initiative potential");
   Assert.That(EditorTemplate.CounterOptions("회피", "불꽃").First().name == "대화회피" && EditorTemplate.CounterOption("격", "드래곤", "극룡반격").effect.Contains("중확률"), "typed counter potentials");
   Assert.That(EditorTemplate.InferCounterType("대염내성") == "에스퍼" && EditorTemplate.PotentialTriggerCatalog.Length > 0 && EditorTemplate.PotentialEffectCatalog().Length > 0, "potential catalogs");
   Assert.That(EditorTemplate.ExtendedOrders(false).Count, Is.EqualTo(3), "aim, evasion and sure-hit directives");
  }
  [Test] public void PotentialSplit() {
   EffectNormalizer.Test();
   var parsed = EditorTemplate.Parse("『시험』… 1/시/선행/상대가 교대할 때, 자신의 속도를 올린다.", "종족 ①");
   Assert.That(parsed.name == "시험" && parsed.uses.Length > 0 && parsed.activation == "선행" && parsed.trigger.Length > 0 && parsed.effect.Length > 0, "potential split");
  }
 }
}
