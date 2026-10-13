using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace AABattle {
 // 편집기 트리거 구조가 만드는 문장은 엔진 트리거 모듈이 그대로 알아본다
 [TestFixture, Category("UI")] public class TriggerBuilderUiTests {
  static T Field<T>(object form, string name) { return (T)typeof(DataEditorForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form); }
  static string Build(DataEditorForm form, string kind, string subject = null, string variant = null, string condition = null, int? number = null) {
   Field<PixelComboBox>(form, "triggerKind").SelectedItem = kind;
   if (subject != null) Field<PixelComboBox>(form, "triggerSubject").SelectedItem = subject;
   if (variant != null) Field<PixelComboBox>(form, "triggerEntryMethod").SelectedItem = variant;
   if (condition != null) Field<PixelTextBox>(form, "triggerCondition").Text = condition;
   if (number != null) Field<PixelNumber>(form, "triggerNumber").Value = number.Value;
   typeof(DataEditorForm).GetMethod("RefreshStructuredTrigger", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null);
   return Field<PixelText>(form, "triggerPreview").Text;
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
    new object[] { "속도 비교", null, "낮음", "", null, "자신의 「속도」가 상대보다 낮을 때" },
    new object[] { "공중", null, "서로", "", null, "서로 공중에 떠 있을 때" },
    new object[] { "첫 행동 전", null, null, "", null, "장소에 나와 아직 기술을 내보내지 않았을 때" },
    new object[] { "같은 타입", null, null, "", null, "상대가 자신과 같은 타입일 때" },
    new object[] { "최후 아님", null, null, "", null, "자신이 「최후의 1체」가 아닐 때" },
    new object[] { "PT 특성", null, null, "마이너스/플러스", null, "PT에 특성이 「마이너스/플러스」인 아군이 있을 때" },
    new object[] { "급소 받음", null, null, "접촉", null, "상대의 「접촉」 기술이 급소에 맞았을 때" },
    new object[] { "필드 빈사", null, null, "", null, "필드의 누군가가 「빈사」 상태가 되었을 때" },
    new object[] { "나무열매 사용", null, null, "", null, "자신이 「나무열매」를 사용했을 때" },
    new object[] { "타이밍", null, "사건이 성립한 다음 T 종료 시", "자신이 「나무열매」를 사용했을 때", null, "「자신이 「나무열매」를 사용했을 때」가 성립한 다음 T 종료 시" }
   };
   using (var form = new DataEditorForm())
    foreach (var c in cases) {
     string text = Build(form, (string)c[0], (string)c[1], (string)c[2], (string)c[3], (int?)c[4]);
     Assert.That(text, Is.EqualTo((string)c[5]));
     Assert.That(TriggerModules.IsModule(text), Is.True, text);
     // 저장한 문장을 다시 불러오면 같은 문장이 만들어진다
     Field<PixelComboBox>(form, "triggerKind").SelectedItem = "상시 (X)";
     typeof(DataEditorForm).GetMethod("RefreshStructuredTrigger", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null);
     Assert.That((bool)typeof(DataEditorForm).GetMethod("LoadTriggerModule", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, new object[] { text }), Is.True, text);
     typeof(DataEditorForm).GetMethod("RefreshStructuredTrigger", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null);
     Assert.That(Field<PixelText>(form, "triggerPreview").Text, Is.EqualTo(text), "불러오기: " + text);
    }
  }

  [Test] public void StageTwoModulesBuildEngineSentences() {
   var cases = new[] {
    new object[] { "체력", "자신", "1/N 이하", "", 4, "자신의 체력이 1/4 이하일 때" },
    new object[] { "체력 변화", "상대", "「1」이 됨", "", null, "상대의 체력이 「1」이 되었을 때" },
    new object[] { "랭크", "상대", "내려감", "속도", null, "상대의 「속도」가 내려갔을 때" },
    new object[] { "날씨", null, "비", "", null, "날씨가 「비」일 때" },
    new object[] { "장소 변화", null, "중력 해제", "", null, "「중력」이 해제되었을 때" },
    new object[] { "빈사가 되었을 때", "상대", null, "", null, "상대가 「빈사」 상태가 되었을 때" },
    new object[] { "플레이버", null, "소속", "칼데아", null, "소속이 《칼데아》일 때" },
    new object[] { "공격하지 않음", "상대", null, "", null, "상대가 공격하지 않을 때" },
    new object[] { "특성 발동", null, null, "위협", null, "자신의 「위협」 특성이 발동했을 때" }
   };
   using (var form = new DataEditorForm())
    foreach (var c in cases) {
     string text = Build(form, (string)c[0], (string)c[1], (string)c[2], (string)c[3], (int?)c[4]);
     Assert.That(text, Is.EqualTo((string)c[5]));
     Assert.That(TriggerModules.Classify(text), Is.Not.Empty, text);
    }
  }
 }
}
