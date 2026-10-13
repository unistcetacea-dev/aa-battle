using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle {
 // 편집기 트리거 화면(모듈 목록 기반)이 만드는 문장은 엔진 트리거 모듈이 그대로 알아본다
 [TestFixture, Category("UI")] public class TriggerBuilderUiTests {
  static ModuleBuilderPanel Builder(object form) {
   return (ModuleBuilderPanel)typeof(DataEditorForm).GetField("triggerBuilder", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
  }
  static object Call(object form, string name, params object[] arguments) {
   return typeof(DataEditorForm).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, arguments);
  }
  static string Build(DataEditorForm form, string id, params string[] slotValues) {
   var builder = Builder(form);
   Assert.That(builder.Select(id), Is.True, id);
   for (int i = 0; i + 1 < slotValues.Length; i += 2)
    Assert.That(builder.SetValue(slotValues[i], slotValues[i + 1]), Is.True, id + "/" + slotValues[i]);
   return builder.Result;
  }

  // 효과 안내: 모듈로 읽히는 트리거는 구현됨, 나머지는 미구현으로 나온다
  [Test] public void GuideListsTriggersWithStatus() {
   var entries = EffectImplementationDex.TriggerEntries();
   var entry = entries.First(x => x.effect == "죽어내밀기로 장소에 나올 때");
   Assert.That(entry.status, Is.EqualTo("자동 구현"));
   Assert.That(entry.family, Is.EqualTo("트리거 · 등장"));
   Assert.That(entries.Any(x => x.status == "미구현" && x.family == "트리거 · 기타"), Is.True);
  }

  // 남은 특성 모듈화 3차의 트리거 모듈도 편집기에서 만들고 다시 불러올 수 있다
  [Test] public void AbilityBatchThreeModulesBuildAndReloadEngineSentences() {
   var cases = new[] {
    new[] { "trigger.speed", "비교", "낮을", "자신의 「속도」가 상대보다 낮을 때" },
    new[] { "trigger.floating", "주체", "서로", "서로 공중에 떠 있을 때" },
    new[] { "trigger.firstaction", "", "", "장소에 나와 아직 기술을 내보내지 않았을 때" },
    new[] { "trigger.sametype", "", "", "상대가 자신과 같은 타입일 때" },
    new[] { "trigger.notlast", "", "", "자신이 「최후의 1체」가 아닐 때" },
    new[] { "trigger.party.ability", "이름", "마이너스/플러스", "PT에 특성이 「마이너스/플러스」인 아군이 있을 때" },
    new[] { "trigger.critical.received", "기술조건", "접촉", "상대의 「접촉」 기술이 급소에 맞았을 때" },
    new[] { "trigger.faint.any", "", "", "필드의 누군가가 「빈사」 상태가 되었을 때" },
    new[] { "trigger.berry", "", "", "자신이 「나무열매」를 사용했을 때" },
    new[] { "trigger.damage.small", "", "", "「작은」 이하의 대미지를 받았을 때" },
    new[] { "trigger.rank.nullified", "", "", "상대에 의한 능력 하락을 무효화했을 때" },
    new[] { "trigger.intimidated", "", "", "상대의 〔위협〕을 받았을 때" },
    new[] { "trigger.received.fullhp", "", "", "체력이 가득인 상태로 공격을 받았을 때" },
    new[] { "trigger.timing.event.True", "사건", "자신이 「나무열매」를 사용했을 때", "「자신이 「나무열매」를 사용했을 때」가 성립한 다음 T 종료 시" }
   };
   using (var form = new DataEditorForm())
    foreach (var c in cases) {
     string text = c[1].Length == 0 ? Build(form, c[0]) : Build(form, c[0], c[1], c[2]);
     Assert.That(text, Is.EqualTo(c[3]));
     Assert.That(TriggerModules.IsModule(text), Is.True, text);
     // 저장한 문장을 다시 불러오면 같은 모듈·같은 문장이 만들어진다
     Builder(form).Select("trigger.always");
     Assert.That((bool)Call(form, "LoadTriggerModule", text), Is.True, text);
     Assert.That(Builder(form).Module.Id, Is.EqualTo(c[0]));
     Assert.That(Builder(form).Result, Is.EqualTo(text), "불러오기: " + text);
    }
  }

  [Test] public void StageTwoModulesBuildEngineSentences() {
   var cases = new[] {
    new[] { "trigger.hp.이하", "분모", "4", "", "", "자신의 체력이 1/4 이하일 때" },
    new[] { "trigger.hpchange.1", "주체", "상대", "", "", "상대의 체력이 「1」이 되었을 때" },
    new[] { "trigger.rank.하락", "주체", "상대", "능력", "속도", "상대의 「속도」가 내려갔을 때" },
    new[] { "trigger.weather", "장소", "비", "", "", "날씨가 「비」일 때" },
    new[] { "trigger.roomchange.True", "장소", "중력", "", "", "「중력」이 해제되었을 때" },
    new[] { "trigger.state.상태 됨", "주체", "상대", "이름", "빈사", "상대가 「빈사」 상태가 되었을 때" },
    new[] { "trigger.flavor.affiliation", "이름", "칼데아", "", "", "소속이 《칼데아》일 때" },
    new[] { "trigger.idle", "주체", "상대", "", "", "상대가 공격하지 않을 때" },
    new[] { "trigger.ability.named", "이름", "위협", "", "", "자신의 「위협」 특성이 발동했을 때" }
   };
   using (var form = new DataEditorForm())
    foreach (var c in cases) {
     string text = c[3].Length == 0 ? Build(form, c[0], c[1], c[2]) : Build(form, c[0], c[1], c[2], c[3], c[4]);
     Assert.That(text, Is.EqualTo(c[5]));
     Assert.That(TriggerModules.Classify(text), Is.Not.Empty, text);
    }
  }

  // 느슨하게 적은 입력도 표준 표기로 바뀐다(기술 조건 · 트리거 문장)
  [Test] public void LooseInputIsNormalizedToStandardSentences() {
   using (var form = new DataEditorForm()) {
    Assert.That(Build(form, "trigger.move.False", "기술조건", "공격"), Is.EqualTo(TriggerStructures.AttackMoveUseLabel));
    Assert.That(Build(form, "trigger.move.True", "기술조건", "불꽃"), Is.EqualTo("「불꽃」 기술을 내보냈을 때"));
    Assert.That(Build(form, "trigger.timing.event.False", "사건", "공격을 받았을 때"), Is.EqualTo("「상대의 공격을 받았을 때」가 성립한 T 종료 시"));
    Assert.That(Build(form, "trigger.entry.method", "주체", "상대", "방식", "강제교대"), Is.EqualTo("상대가 강제교대로 장소에 나왔을 때"));
    Assert.That(Builder(form).Message, Is.Empty);
    // 필요한 빈칸이 비면 결과가 비고 이유가 나온다
    Builder(form).Select("trigger.state.상태");
    Builder(form).SetValue("이름", "");
    Assert.That(Builder(form).Result, Is.Empty);
    Assert.That(Builder(form).Message, Is.Not.Empty);
   }
  }

  // 상태는 여럿을 골라 한 줄씩 저장하고, 여러 줄은 다시 하나의 선택으로 불러온다
  [Test] public void SeveralStatesBecomeOneLineEachAndReload() {
   using (var form = new DataEditorForm()) {
    string text = Build(form, "trigger.state.상태 됨", "주체", "자신", "이름", "화상/독");
    Assert.That(text, Is.EqualTo(TriggerStructures.StatusCondition("자신", "화상", true, false) + "\r\n" + TriggerStructures.StatusCondition("자신", "독", true, false)));
    Builder(form).Select("trigger.always");
    Assert.That((bool)Call(form, "LoadTriggerModule", text), Is.True);
    Assert.That(Builder(form).GetValue("이름"), Is.EqualTo("화상/독"));
    Assert.That(Builder(form).Result, Is.EqualTo(text));
    Assert.That(Build(form, "trigger.state.상태 됨", "주체", "자신", "이름", "화상"), Is.EqualTo(TriggerStructures.StatusCondition("자신", "화상", true, false)));
    Assert.That(Build(form, "trigger.state.상태", "주체", "아군", "이름", "혼란"), Is.EqualTo(TriggerStructures.StatusCondition("아군", "혼란", false, true)));
   }
  }

  // 구조 문장(적진 타입·트레이너 자질·부여·두 줄 등장)과 확정 목록은 모두 화면으로 불러온다
  [Test] public void StructureSentencesAndConfirmedListLoadIntoTheScreen() {
   using (var form = new DataEditorForm()) {
    Assert.That(Build(form, "trigger.opponent.type", "이름", "불꽃"), Is.EqualTo(TriggerStructures.OpponentType("불꽃")));
    Assert.That(Build(form, "trigger.trainer.aptitude.False", "자질", "통솔", "등급", "A"), Is.EqualTo(TriggerStructures.TrainerAptitude("통솔", "A")));
    Assert.That(Build(form, "trigger.named.부여", "이름", "계약의 특권"), Is.EqualTo(TriggerStructures.Granted("계약의 특권")));
    Assert.That(Build(form, "trigger.entry.both"), Is.EqualTo(TriggerStructures.SelfAndOpponentEntryLabel));
    foreach (string sentence in ConfirmedTriggerCatalog.Items.Concat(new[] { TriggerStructures.AlwaysLabel, TriggerStructures.FieldLabel, TriggerStructures.ObserverLabel })) {
     Assert.That((bool)Call(form, "LoadTriggerModule", sentence), Is.True, sentence);
     Assert.That(TriggerStructures.Normalize(Builder(form).Result), Is.EqualTo(TriggerStructures.Normalize(sentence)), sentence);
    }
    // 읽을 수 없는 문장은 화면을 바꾸지 않는다
    string id = Builder(form).Module.Id;
    Assert.That((bool)Call(form, "LoadTriggerModule", "알 수 없는 문장입니다"), Is.False);
    Assert.That(Builder(form).Module.Id, Is.EqualTo(id));
   }
  }

  // 빈칸 종류에 맞는 픽셀 컨트롤이 놓인다(Windows 기본 입력 컨트롤 금지)
  [Test] public void SlotKindsUsePixelControls() {
   using (var form = new DataEditorForm()) {
    var builder = Builder(form);
    builder.Select("trigger.hp.이하");
    Assert.That(builder.Editor("분모").Control, Is.TypeOf<PixelNumber>());
    builder.Select("trigger.move.False");
    Assert.That(builder.Editor("기술조건").Control, Is.TypeOf<PixelTextBox>());
    builder.Select("trigger.state.상태");
    Assert.That(builder.Editor("이름").Control, Is.TypeOf<FlatButton>());
    Assert.That(builder.Editor("주체").Control.Controls.OfType<PixelComboBox>().Any(), Is.True);
    var picked = builder.Families;
    Assert.That(picked, Does.Contain("등장·교대"));
    Assert.That(builder.ModuleNames, Is.Not.Empty);
   }
  }
 }
}
