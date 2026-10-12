using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace AABattle {
 // 편집기 구조화 효과 입력기가 만드는 문장은 엔진 모듈이 그대로 알아본다
 [TestFixture, Category("UI")] public class EffectBuilderUiTests {
  static T Field<T>(object form, string name) { return (T)typeof(DataEditorForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form); }

  static string Build(DataEditorForm form, string kind, string target, string action = null, string condition = null, string state = null, string duration = null) {
   Field<PixelComboBox>(form, "effectKind").SelectedItem = kind;
   Field<PixelComboBox>(form, "effectTarget").SelectedItem = target;
   if (action != null) Field<PixelComboBox>(form, "effectAction").SelectedItem = action;
   if (condition != null) Field<PixelTextBox>(form, "effectCondition").Text = condition;
   if (state != null) Field<PixelComboBox>(form, "effectState").SelectedItem = state;
   if (duration != null) Field<PixelComboBox>(form, "effectDuration").SelectedItem = duration;
   typeof(DataEditorForm).GetMethod("RefreshStructuredEffect", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null);
   return Field<PixelText>(form, "effectPreview").Text;
  }

  [Test] public void EditorModuleSentencesAreEngineModules() {
   using (var form = new DataEditorForm()) {
    string power = Build(form, "수치 배율", "상대가 받는 대미지", "강화", "불꽃/얼음 물리 효과가 매우 굉장");
    Assert.That(power, Is.EqualTo("상대가 받는 「불꽃/얼음」「물리」「효과가 굉장」 기술의 대미지를 강화(1.5배)한다."));
    Assert.That(EffectModules.Classify(power), Is.EqualTo("수치 배율"));
    string accuracy = Build(form, "수치 배율", "상대의 기술의 명중", "약화", "타입 불일치 위력 40 이하", null, "2T동안");
    Assert.That(accuracy, Does.StartWith("2T동안 상대의 「타입 불일치」「위력 40 이하」 기술의 명중을 약화("));
    Assert.That(EffectModules.Classify(accuracy), Is.EqualTo("수치 배율"));
    string grant = Build(form, "상태 부여", "상대", null, null, "도발", "T종료시까지");
    Assert.That(grant, Is.EqualTo("T종료시까지 상대를 「도발」상태로 한다."));
    Assert.That(EffectModules.Classify(grant), Is.EqualTo("상태 부여"));
    Assert.That(EffectModules.Classify(Build(form, "상태이상 치유", "자신")), Is.EqualTo("상태이상 치유"));
    Assert.That(EffectModules.Classify(Build(form, "최대 HP의 1/N", "자신", "소비")), Is.EqualTo("최대 HP의 1/N"));
    Field<PixelComboBox>(form, "effectAptitude").SelectedItem = "통솔";
    string trainer = Build(form, "종족치 변경", "자신의 「방어」", "트레이너 자질 등급까지 상승");
    Assert.That(trainer, Is.EqualTo("자신의 「방어」 종족치를 트레이너 「통솔」 등급까지 올린다."));
    Assert.That(EffectModules.Classify(trainer), Is.EqualTo("종족치 변경"));
    Assert.That(EffectModules.Classify(Build(form, "능력 랭크 변화", "상대의 「명중」", "하락")), Is.EqualTo("능력 랭크 변화"));
    Assert.That(EffectModules.Classify(Build(form, "최대 HP의 1/N", "상대", "대미지")), Is.EqualTo("최대 HP의 1/N"));
    // F4a 모듈
    Field<PixelComboBox>(form, "effectDuration").SelectedItem = "없음";
    Assert.That(Build(form, "장소 전개", "날씨", "모래바람"), Is.EqualTo("날씨를 「모래바람」으로 한다."));
    Assert.That(EffectModules.Classify(Build(form, "장소 전개", "지형", "일렉트릭필드")), Is.EqualTo("장소 전개"));
    Assert.That(EffectModules.Classify(Build(form, "장소 전개", "트릭룸", "전개")), Is.EqualTo("장소 전개"));
    Assert.That(EffectModules.Classify(Build(form, "설치물", "적진", "압정뿌리기")), Is.EqualTo("설치물"));
    Assert.That(EffectModules.Classify(Build(form, "설치물", "자진", "설치물 제거")), Is.EqualTo("설치물"));
    Assert.That(EffectModules.Classify(Build(form, "강제교대", "상대", "강제교대시킬 수 있다")), Is.EqualTo("강제교대"));
    Assert.That(EffectModules.Classify(Build(form, "강제교대", "자신", "강제교대되지 않는다")), Is.EqualTo("강제교대 무효"));
    Assert.That(EffectModules.Classify(Build(form, "부유", "자신", "공중에 떠오른다")), Is.EqualTo("부유"));
    Assert.That(EffectModules.Classify(Build(form, "상태이상 면역", "자신", "마비")), Is.EqualTo("상태이상 면역"));
   }
  }
 }
}
