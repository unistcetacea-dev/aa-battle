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

  [Test] public void StageTwoModulesBuildEngineSentences() {
   var cases = new[] {
    new object[] { "체력", "자신", "1/N 이하", "", 4, "자신의 체력이 1/4 이하일 때" },
    new object[] { "체력 변화", "상대", "「1」이 됨", "", null, "상대의 체력이 「1」이 되었을 때" },
    new object[] { "랭크", "상대", "내려감", "속도", null, "상대의 「속도」가 내려갔을 때" },
    new object[] { "날씨", null, "비", "", null, "날씨가 「비」일 때" },
    new object[] { "장소 변화", null, "중력 해제", "", null, "「중력」이 해제되었을 때" },
    new object[] { "빈사가 되었을 때", "상대", null, "", null, "상대가 「빈사」 상태가 되었을 때" }
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
