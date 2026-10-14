using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle {
 // 편집기 효과 화면(모듈 목록 기반)이 만드는 문장은 엔진 모듈이 그대로 알아본다
 [TestFixture, Category("UI"), Apartment(System.Threading.ApartmentState.STA)] public class EffectBuilderUiTests {
  static T Field<T>(object form, string name) { return (T)typeof(DataEditorForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form); }
  static ModuleBuilderPanel Builder(object form) { return Field<ModuleBuilderPanel>(form, "effectBuilder"); }

  // 모듈을 고르고 빈칸을 채운 뒤 화면의 결과 문장을 돌려준다
  static string Build(DataEditorForm form, string id, params string[] slotValues) {
   var builder = Builder(form);
   Assert.That(builder.Select(id), Is.True, id);
   for (int i = 0; i + 1 < slotValues.Length; i += 2)
    Assert.That(builder.SetValue(slotValues[i], slotValues[i + 1]), Is.True, id + "/" + slotValues[i]);
   Assert.That(builder.Message, Is.Empty, id);
   return builder.Result;
  }

  [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);

  // 안쪽 스크롤 영역의 휠은 바깥 패널을 같이 움직이지 않는다(편집기 오른쪽·가운데 동시 스크롤 버그)
  [Test] public void WheelScrollsOnlyInnermostPanel() {
   using (var form = new System.Windows.Forms.Form { Width = 400, Height = 400 }) {
    var outer = new PixelScrollPanel { Dock = System.Windows.Forms.DockStyle.Fill };
    var tall = new System.Windows.Forms.Panel { Height = 2000 };
    var inner = new PixelScrollPanel { Bounds = new System.Drawing.Rectangle(0, 0, 300, 200) };
    var leaf = new System.Windows.Forms.Panel { Height = 2000 };
    form.Controls.Add(outer); outer.Content = tall; tall.Controls.Add(inner); inner.Content = leaf;
    form.Show(); System.Windows.Forms.Application.DoEvents();
    var point = leaf.PointToScreen(new System.Drawing.Point(10, 10));
    SendMessage(leaf.Handle, 0x20A, new IntPtr(-120 << 16), new IntPtr((point.Y << 16) | (point.X & 0xFFFF)));
    Assert.That(inner.ScrollTop, Is.GreaterThan(0));
    Assert.That(outer.ScrollTop, Is.EqualTo(0));
   }
  }

  [Test] public void EditorModuleSentencesAreEngineModules() {
   using (var form = new DataEditorForm()) {
    string power = Build(form, "effect.scalar.damage.받는강화", "주체", "상대", "조건", "불꽃/얼음 물리 효과가 매우 굉장");
    Assert.That(power, Is.EqualTo("상대가 받는 「불꽃/얼음」「물리」「효과가 굉장」 기술의 대미지를 강화(1.5배)한다."));
    Assert.That(EffectModules.Classify(power), Is.EqualTo("수치 배율"));
    string accuracy = Build(form, "effect.scalar.move.명중약화", "주체", "상대", "조건", "타입 불일치 위력 40 이하", "효과기간", "2T동안");
    Assert.That(accuracy, Does.StartWith("2T동안 상대의 「타입 불일치」「위력 40 이하」 기술의 명중을 약화("));
    Assert.That(EffectModules.Classify(accuracy), Is.EqualTo("수치 배율"));
    string grant = Build(form, "effect.state.grant.normal", "대상", "상대", "상태", "도발", "효과기간", "T종료시까지");
    Assert.That(grant, Is.EqualTo("T종료시까지 상대를 「도발」상태로 한다."));
    Assert.That(EffectModules.Classify(grant), Is.EqualTo("상태 부여"));
    Assert.That(EffectModules.Classify(Build(form, "effect.state.cure.all", "대상", "자신")), Is.EqualTo("상태이상 치유"));
    Assert.That(EffectModules.Classify(Build(form, "effect.hp.소비")), Is.EqualTo("최대 HP의 1/N"));
    string trainer = Build(form, "effect.species.trainer", "대상", "자신의 「방어」", "자질", "통솔");
    Assert.That(trainer, Is.EqualTo("자신의 「방어」 종족치를 트레이너 「통솔」 등급까지 올린다."));
    Assert.That(EffectModules.Classify(trainer), Is.EqualTo("종족치 변경"));
    Assert.That(EffectModules.Classify(Build(form, "effect.rank.lower1", "대상", "상대의 「명중」")), Is.EqualTo("능력 랭크 변화"));
    Assert.That(EffectModules.Classify(Build(form, "effect.hp.대미지", "주체", "상대")), Is.EqualTo("최대 HP의 1/N"));
    // F4a 모듈
    Assert.That(Build(form, "effect.place.weather.한다", "날씨", "모래바람"), Is.EqualTo("날씨를 「모래바람」으로 한다."));
    Assert.That(EffectModules.Classify(Build(form, "effect.place.terrain.한다", "지형", "일렉트릭필드")), Is.EqualTo("장소 전개"));
    Assert.That(EffectModules.Classify(Build(form, "effect.place.room.한다", "룸", "트릭룸")), Is.EqualTo("장소 전개"));
    Assert.That(EffectModules.Classify(Build(form, "effect.hazard.install.한다", "설치물", "압정뿌리기")), Is.EqualTo("설치물"));
    Assert.That(EffectModules.Classify(Build(form, "effect.hazard.clear")), Is.EqualTo("설치물"));
    Assert.That(EffectModules.Classify(Build(form, "effect.switch.force.optional")), Is.EqualTo("강제교대"));
    Assert.That(EffectModules.Classify(Build(form, "effect.switch.force.immune")), Is.EqualTo("강제교대 무효"));
    Assert.That(EffectModules.Classify(Build(form, "effect.floating")), Is.EqualTo("부유"));
    Assert.That(EffectModules.Classify(Build(form, "effect.state.immunity.all", "상태", "마비")), Is.EqualTo("상태이상 면역"));
    string extra = Build(form, "effect.extra.condition.periodic", "배수", "4", "순서", "먼저", "조건", "강철 물리");
    Assert.That(extra, Is.EqualTo("「4」배수 T, 가장 먼저 「강철」「물리」 기술에 의한 추가행동을 얻는다."));
    Assert.That(EffectModules.Classify(extra), Is.EqualTo("추가행동"));
    Assert.That(EffectModules.Classify(Build(form, "effect.extra.same")), Is.EqualTo("추가행동"));
    // G 방호·회피
    string dodge = Build(form, "effect.guard.회피", "조건", "드래곤", "확률", "저확률로");
    Assert.That(dodge, Is.EqualTo("상대의 「드래곤」 기술을 저확률로 회피한다."));
    Assert.That(EffectModules.Classify(dodge), Is.EqualTo("회피"));
    Assert.That(EffectModules.Classify(Build(form, "effect.guard.무효화", "조건", "물리 위력 40 이하")), Is.EqualTo("기술 무효화"));
    Assert.That(EffectModules.Classify(Build(form, "effect.guard.pierce.pokemon", "조건", "격투")), Is.EqualTo("방호 관통"));
    Assert.That(EffectModules.Classify(Build(form, "effect.analysis.immune")), Is.EqualTo("데이터 해석 무효"));
    Assert.That(EffectModules.Classify(Build(form, "effect.guard.반사", "조건", "변화")), Is.EqualTo("기술 반사"));
    Assert.That(Build(form, "effect.guard.pierce.trainer", "조건", "격투"), Is.EqualTo("자신의 「격투」 기술이 상대의 트레이너 방호 포텐셜을 무시한다."));
    // F4b 타입·상성
    Assert.That(Build(form, "effect.type.add.한다", "타입", "불꽃", "조건", "물"), Is.EqualTo("자신의 「물」 기술에 「불꽃」 타입을 추가해 유리한 상성으로 판정한다."));
    Assert.That(EffectModules.Classify(Build(form, "effect.type.resist", "조건", "얼음")), Is.EqualTo("타입 내성 관통"));
    Assert.That(EffectModules.Classify(Build(form, "effect.type.stab", "조건", "불꽃")), Is.EqualTo("자속 배율"));
    Assert.That(EffectModules.Classify(Build(form, "effect.type.change.한다", "주체", "상대", "타입", "고스트")), Is.EqualTo("타입 변경"));
    Assert.That(EffectModules.ActFirstTier(Build(form, "effect.order.행동한다", "순서", "가장 먼저")), Is.EqualTo(2));
    Assert.That(EffectModules.ActFirstTier(Build(form, "effect.order.행동할 수 있다", "순서", "상대보다 먼저")), Is.EqualTo(1));
   }
  }

  // 발동 확률은 표준 접두어로 붙고, 저장한 문장을 다시 열면 확률 빈칸이 채워진다
  [Test] public void ProbabilityPrefixAndSubjectRoundTrip() {
   using (var form = new DataEditorForm()) {
    string chance = Build(form, "effect.priority.내린다", "주체", "상대", "수치", "2", "발동확률", "중확률");
    Assert.That(chance, Is.EqualTo("중확률로 상대의 기술 우선도를 2 내린다."));
    var load = typeof(DataEditorForm).GetMethod("LoadStructuredEffectSettings", BindingFlags.Instance | BindingFlags.NonPublic);
    Builder(form).Select("effect.floating");
    Assert.That((bool)load.Invoke(form, new object[] { chance, "자신" }), Is.True);
    Assert.That(Builder(form).Module.Id, Is.EqualTo("effect.priority.내린다"));
    Assert.That(Builder(form).GetValue("발동확률"), Is.EqualTo("중확률"));
    Assert.That(Builder(form).GetValue("수치"), Is.EqualTo("2"));
    // 자격 조건 꼬리표는 떼고 읽는다
    Assert.That((bool)load.Invoke(form, new object[] { EffectNormalizer.CanonicalSwitchRestriction() + " (필요 「통솔：A」 이상)", "자신" }), Is.True);
    Assert.That(Builder(form).Module.Id, Is.EqualTo("effect.switch.restrict"));
    // 효과 주체가 아군이면 문장 앞머리의 「자신」이 바뀐다
    Field<PixelComboBox>(form, "effectSubject").SelectedItem = "아군";
    Build(form, "effect.hp.회복", "주체", "자신");
    var configured = typeof(DataEditorForm).GetMethod("ConfiguredEffect", BindingFlags.Instance | BindingFlags.NonPublic);
    Assert.That((string)configured.Invoke(form, null), Does.StartWith("아군의 체력을"));
   }
  }

  [Test] public void AbilitySearchSelectionFillsAndRestoresAbilitySlot() {
   using (var form = new DataEditorForm()) {
    form.Show();
    System.Windows.Forms.Application.DoEvents();
    var builder = Builder(form);
    Assert.That(builder.Select("effect.ability.change.한다"), Is.True);
    Assert.That(builder.Editor("특성"), Is.TypeOf<AbilitySlotEditor>());

    string selected = "";
    using (var dialog = new AbilitySearchForm(null, false, "쾌청"))
    using (var timer = new System.Windows.Forms.Timer { Interval = 30 }) {
     Assert.That(dialog.Candidates, Does.Contain("가뭄"), "효과 문장 검색");
     timer.Tick += (s, e) => {
      timer.Stop();
      dialog.Pick("가뭄");
     };
     timer.Start();
     Assert.That(dialog.ShowDialog(form), Is.EqualTo(System.Windows.Forms.DialogResult.OK));
     selected = dialog.SelectedNames.Single();
    }

    Assert.That(builder.SetValue("특성", selected), Is.True);
    string sentence = builder.Result;
    Assert.That(sentence, Is.EqualTo("자신의 특성을 「가뭄」으로 한다."));
    ModuleDefinition module;
    Dictionary<string, string> values;
    Assert.That(ModuleCatalog.TryRead(ModuleKind.Effect, sentence, out module, out values), Is.True);
    Assert.That(values["특성"], Is.EqualTo("가뭄"));
   }
  }

  // 기존 편집기가 만들 수 있던 문장(능력 랭크·우선도·수치 배율·최대 HP 1/N·교대 효과 등)은 모듈 목록으로도 똑같이 나온다
  [Test] public void OldEditorSentencesAreProducedByTheCatalog() {
   var sentences = new List<string>();
   foreach (string target in new[] { "자신의 「공격」", "상대의 「명중」", "자신의 임의의 능력", "자신의 전능력치", "상대의 가장 낮은 능력" })
    foreach (int amount in new[] { 1, 2, 3, -1, -2, -3 })
     sentences.Add(EffectNormalizer.CanonicalRank(target, amount));
   foreach (string target in new[] { "자신", "상대", "아군 1체", "아군 전체" })
    foreach (int amount in new[] { 1, 3, 7, -1, -7 })
     sentences.Add(EffectNormalizer.CanonicalPriority(target, amount));
   foreach (string action in new[] { "강화", "약화" })
    foreach (double rate in new[] { 1.5, 0.5, 2 })
     sentences.Add(EffectNormalizer.Canonical("자신의 「공격」", action, rate));
   sentences.Add(EffectNormalizer.CanonicalSwitch());
   sentences.Add(EffectNormalizer.CanonicalPivotGrant());
   sentences.Add(EffectNormalizer.CanonicalSwitchRestriction());
   sentences.Add(EffectNormalizer.CanonicalAnalysisPermission());
   sentences.Add(EffectNormalizer.CanonicalStatIgnore("상대", "능력", "상승(강화)"));
   sentences.Add(EffectNormalizer.CanonicalStatIgnore("『야생의 두목』", "공격", "강화"));
   sentences.AddRange(new[] {
    "자신의 체력을 최대 HP의 1/4 회복한다.", "상대에게 최대 HP의 1/8 대미지를 준다.",
    "자신의 체력을 최대 HP의 1/2 소비한다.", "상대에게 최대 HP의 1/4 대미지를 준다. 이 대미지로는 빈사로 할 수 없다."
   });
   foreach (string sentence in sentences) {
    ModuleDefinition module;
    Dictionary<string, string> values;
    Assert.That(ModuleCatalog.TryRead(ModuleKind.Effect, sentence, out module, out values), Is.True, sentence);
    Assert.That(module.Fill(values), Is.EqualTo(sentence), module.Id);
   }
  }
 }
}
