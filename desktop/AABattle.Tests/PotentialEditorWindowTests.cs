using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using System.Runtime.ExceptionServices;
using System.Collections.Generic;
using NUnit.Framework;

namespace AABattle {
 // 포텐셜 편집 창(별도 창), 기술 조건 입력 방식, 성별 칸, 전용 포텐셜 슬롯 선택지
 [TestFixture, Category("UI"), Apartment(System.Threading.ApartmentState.STA)] public class PotentialEditorWindowTests {
  static DataEditorForm OpenPokemonEditor() {
   var form = new DataEditorForm();
   form.Show();
   form.Sample();
   form.SelectTabForTest(1);
   Application.DoEvents();
   return form;
  }
  static string Cell(DataGridViewRow row, string name) { return Convert.ToString(row.Cells[name].Value) ?? ""; }
  static DataGridViewRow RowOf(DataEditorForm form, string slot) {
   return form.PotentialGridForTest.Rows.Cast<DataGridViewRow>().First(x => !x.IsNewRow && Cell(x, "slot") == slot);
  }
  static T Field<T>(object form, string name) {
   return (T)typeof(DataEditorForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
  }

  // 모달 편집 창이 뜨면 script를 실행한다(script가 확인·취소로 닫는다)
  static void Drive(DataEditorForm form, Action open, Action<PotentialEditWindow> script) {
   Exception failure = null;
   using (var timer = new Timer { Interval = 30 }) {
    timer.Tick += (s, e) => {
     var window = form.ActivePotentialWindow;
     if (window == null || !window.Visible) return;
     timer.Stop();
     try { script(window); } catch (Exception ex) { failure = ex; window.Cancel(); }
    };
    timer.Start();
    open();
    timer.Stop();
   }
   if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
  }

  // ===== 편집 창: 확인할 때만 반영 =====
  [Test] public void WindowAppliesOnConfirmAndDiscardsOnCancel() {
   using (var form = OpenPokemonEditor()) {
    var row = RowOf(form, "종족 ②");
    Drive(form, () => form.EditPotentialRow(row), w => {
     w.TypeName("시험의 힘");
     w.TriggerBox.Text = TriggerStructures.EntryLabel;
     w.EffectBox.Text = EffectNormalizer.CanonicalRank("자신의 임의의 능력", 1);
     w.Cancel();
    });
    Assert.That(Cell(row, "name"), Is.EqualTo(""), "취소는 버린다");
    Assert.That(Cell(row, "trigger"), Is.EqualTo(""));
    Assert.That(form.DirtyForTest, Is.False);
    Assert.That(form.ActivePotentialWindow, Is.Null);
    Assert.That(form.BuildersParked, Is.True, "닫으면 모듈 화면을 편집기로 돌려놓는다");

    Drive(form, () => form.EditPotentialRow(row), w => {
     Assert.That(w.NameIsFixedChoice, Is.False, "종족 ②는 이름을 직접 입력한다");
     w.TypeName("시험의 힘");
     w.TriggerBox.Text = TriggerStructures.EntryLabel;
     w.EffectBox.Text = EffectNormalizer.CanonicalRank("자신의 임의의 능력", 1);
     Assert.That(Cell(row, "name"), Is.EqualTo(""), "창이 열려 있는 동안 표는 그대로");
     w.Accept();
    });
    Assert.That(Cell(row, "name"), Is.EqualTo("시험의 힘"));
    Assert.That(Cell(row, "trigger"), Is.EqualTo(TriggerStructures.EntryLabel));
    Assert.That(Cell(row, "effect"), Is.EqualTo(EffectNormalizer.CanonicalRank("자신의 임의의 능력", 1)));
    Assert.That(form.DirtyForTest, Is.True);
    var record = form.ProjectForTest.pokemon[0].potentials.First(x => x.slot == "종족 ②");
    Assert.That(record.name, Is.EqualTo("시험의 힘"), "확인할 때 한 번 저장된다");
    form.CloseForTest();
   }
  }

  [Test] public void TypingInTheWindowNeverRebuildsTheTable() {
   using (var form = OpenPokemonEditor()) {
    var grid = form.PotentialGridForTest;
    var tree = form.TreeForTest;
    var row = RowOf(form, "종족 ③");
    grid.CurrentCell = row.Cells["name"];
    var rows = grid.Rows.Cast<DataGridViewRow>().ToArray();
    var selected = tree.SelectedNode;
    string nodeText = selected.Text;
    int changes = 0;
    DataGridViewCellEventHandler cellChanged = (s, e) => changes++;
    DataGridViewRowsAddedEventHandler added = (s, e) => changes++;
    DataGridViewRowsRemovedEventHandler removed = (s, e) => changes++;
    TreeViewEventHandler afterSelect = (s, e) => changes++;
    grid.CellValueChanged += cellChanged; grid.RowsAdded += added; grid.RowsRemoved += removed; tree.AfterSelect += afterSelect;
    Drive(form, () => form.EditPotentialRow(row), w => {
     foreach (string typed in new[] { "시", "시험", "시험의", "시험의 ", "시험의 힘" }) w.TypeName(typed);
     for (int i = 1; i <= 8; i++) { w.TriggerBox.Text = new string('가', i); w.EffectBox.Text = new string('나', i); }
     w.Cancel();
    });
    grid.CellValueChanged -= cellChanged; grid.RowsAdded -= added; grid.RowsRemoved -= removed; tree.AfterSelect -= afterSelect;
    Assert.That(changes, Is.EqualTo(0), "입력 중에는 표·트리를 다시 만들지 않는다");
    Assert.That(grid.Rows.Cast<DataGridViewRow>().SequenceEqual(rows), Is.True, "행 개체가 그대로");
    Assert.That(grid.CurrentRow, Is.SameAs(row));
    Assert.That(tree.SelectedNode, Is.SameAs(selected));
    Assert.That(selected.Text, Is.EqualTo(nodeText));
    form.CloseForTest();
   }
  }

  [Test] public void PotentialTableIsReadOnlyExceptTheDisableToggle() {
   using (var form = OpenPokemonEditor()) {
    var grid = form.PotentialGridForTest;
    Assert.That(grid.AllowUserToAddRows, Is.False);
    foreach (DataGridViewColumn column in grid.Columns)
     Assert.That(column.ReadOnly, Is.EqualTo(column.Name != "disabled"), column.Name);
    // 발동 금지 토글은 표에서 바로 바뀌고 한 번 저장된다
    var row = RowOf(form, "종족 ①");
    row.Cells["disabled"].Value = true;
    Assert.That(form.ProjectForTest.pokemon[0].potentials.First(x => x.slot == "종족 ①").enabled, Is.False);
    form.CloseForTest();
   }
  }

  [Test] public void TemplateNamesAndTypesFillTheWindowOnly() {
   using (var form = OpenPokemonEditor()) {
    var row = RowOf(form, "종족 ④");
    var window = form.OpenPotentialWindowForTest(form.DraftFromRow(row));
    window.ChooseSlot("역할");
    Assert.That(window.NameIsFixedChoice && window.NameChoices.Contains("귀인"), Is.True);
    window.ChooseName("귀인");
    Assert.That(window.EffectBox.Text, Does.Contain("공격"));
    Assert.That(window.TriggerBox.Text, Is.Not.Empty);
    window.ChooseSlot("종족 ①");
    Assert.That(window.NameIsFixedChoice, Is.False);
    Assert.That(window.EffectBox.Text + window.TriggerBox.Text, Is.Empty, "종류를 바꾸면 내용을 비운다");
    window.ChooseSlot("회피");
    Assert.That(window.TypeRowVisible, Is.True);
    Assert.That(window.NameChoices, Is.EqualTo(new[] { "" }), "타입을 고르기 전에는 이름 선택지가 없다");
    window.ChooseTargetType("불꽃");
    Assert.That(window.NameChoices, Does.Contain("대화회피"));
    window.ChooseName("대화회피");
    Assert.That(window.EffectBox.Text, Does.Contain("불꽃"));
    window.ChooseSlot("특권");
    window.ChooseName("계약의 특권");
    Assert.That(window.TypeRowVisible, Is.True);
    window.ChooseTargetType("물");
    Assert.That(window.EffectBox.Text, Does.Contain("물"));
    Assert.That(Cell(row, "name"), Is.EqualTo(""), "표는 그대로");
    window.Cancel();
    window.Dispose();
    form.CloseForTest();
   }
  }

  [Test] public void NewPotentialIsOnlyAddedWhenConfirmed() {
   using (var form = OpenPokemonEditor()) {
    var grid = form.PotentialGridForTest;
    int count = grid.Rows.Count;
    Drive(form, form.NewPotential, w => w.Cancel());
    Assert.That(grid.Rows.Count, Is.EqualTo(count));
    Drive(form, form.NewPotential, w => { w.TypeName("새 힘"); w.EffectBox.Text = EffectNormalizer.CanonicalRank("자신의 「공격」", 1); w.Accept(); });
    Assert.That(grid.Rows.Count, Is.EqualTo(count + 1));
    Assert.That(form.ProjectForTest.pokemon[0].potentials.Any(x => x.name == "새 힘" && x.slot == "종족 ①"), Is.True);
    form.CloseForTest();
   }
  }

  // ===== 전용 포텐셜 선택지 =====
  [Test] public void ExclusiveSlotIsOfferedForPokemonAndHiddenFromTheTrainer() {
   using (var form = OpenPokemonEditor()) {
    Assert.That(form.SlotChoicesFor(false, ""), Does.Contain(TeamRules.ExclusiveSlot));
    Assert.That(form.SlotChoicesFor(true, ""), Does.Not.Contain(TeamRules.ExclusiveSlot));
    form.CloseForTest();
   }
  }

  // ===== 성별 칸 =====
  [Test] public void GenderSelectorSavesAndReloads() {
   using (var form = OpenPokemonEditor()) {
    var gender = Field<PixelComboBox>(form, "pGender");
    Assert.That(gender.Text, Is.EqualTo("없음"), "기본값");
    Assert.That(gender.Items.Cast<object>().Select(x => x.ToString()), Is.EqualTo(new[] { "수컷", "암컷", "없음" }));
    gender.SelectedItem = "암컷";
    string json = (string)typeof(DataEditorForm).GetMethod("Json", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null);
    Assert.That(ProjectStore.Parse(json).pokemon[0].gender, Is.EqualTo("암컷"));
    form.ProjectForTest.pokemon[0].gender = "수컷";
    typeof(DataEditorForm).GetMethod("LoadCurrent", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null);
    Assert.That(gender.Text, Is.EqualTo("수컷"));
    form.CloseForTest();
   }
  }

  // ===== 기술 조건 빈칸: 분류 선택과 기술 이름 검색 =====
  static MoveConditionSlotEditor ConditionEditor(ModuleBuilderPanel builder) { return (MoveConditionSlotEditor)builder.Editor("기술조건"); }

  static string Sentence(string condition) {
   var builder = new TriggerModulePanel(TestData.Db);
   builder.Select("trigger.move.False");
   builder.SetValue("기술조건", condition);
   return builder.Result;
  }

  [Test] public void CategoryChoiceAndMoveNameSearchProduceTheSameStandardTokens() {
   var builder = new TriggerModulePanel(TestData.Db);
   Assert.That(builder.Select("trigger.move.False"), Is.True);
   var editor = ConditionEditor(builder);
   Assert.That(editor.Mode, Is.EqualTo(MoveConditionSlotEditor.CategoryMode));
   Assert.That(editor.Categories, Is.SupersetOf(new[] { "공격", "변화", "물리", "특수", "접촉", "불꽃" }));
   string contact = Sentence("접촉"), fire = Sentence("불꽃");
   builder.SetValue("기술조건", "접촉");
   Assert.That(builder.Result, Is.EqualTo(contact));
   editor.SetMode(MoveConditionSlotEditor.NameMode);
   Assert.That(builder.Result, Is.EqualTo(contact), "방식을 바꿔도 같은 문장");
   editor.NameText = "불꽃";
   Assert.That(builder.Result, Is.EqualTo(fire), "이름 방식에 적은 분류도 같은 문장");
   // 기술 이름: 기술 데이터에 있는 이름은 「기술:이름」 토큰으로 들어간다
   editor.NameText = "몸통박치기";
   string named = builder.Result;
   Assert.That(named, Does.Contain("「기술:몸통박치기」"));
   editor.SetName("몸통박치기");
   Assert.That(editor.NameText, Is.EqualTo("「기술:몸통박치기」"));
   Assert.That(builder.Result, Is.EqualTo(named), "검색으로 고른 것과 직접 적은 이름이 같은 문장");
   Assert.That(Sentence("「기술:몸통박치기」"), Is.EqualTo(named));
   Assert.That(editor.Candidates("몸통박치기"), Does.Contain("몸통박치기"), "기술 도감 검색으로 후보를 거른다");
   Assert.That(editor.Candidates("몸통박치기").Length, Is.LessThan(editor.Candidates("").Length));
   // 기술 도감 탭과 같은 검색 규칙을 쓴다
   Assert.That(editor.Candidates("불꽃").SequenceEqual(MoveDexSearch.Filter(TestData.Db.moves, "불꽃").Select(x => x.name).Take(50)), Is.True);
  }

  [TestCase("공격", MoveConditionSlotEditor.CategoryMode, "공격")]
  [TestCase("「불꽃」", MoveConditionSlotEditor.CategoryMode, "불꽃")]
  [TestCase("「기술:몸통박치기」", MoveConditionSlotEditor.NameMode, "「기술:몸통박치기」")]
  [TestCase("「불꽃」「접촉」", MoveConditionSlotEditor.NameMode, "「불꽃」「접촉」")]
  public void SavedSentenceRestoresTheInputMode(string condition, string mode, string shown) {
   string sentence = Sentence(condition);
   var builder = new TriggerModulePanel(TestData.Db);
   Assert.That(builder.Load(sentence), Is.True, sentence);
   Assert.That(builder.Module.Id, Is.EqualTo("trigger.move.False"));
   var editor = ConditionEditor(builder);
   Assert.That(editor.Mode, Is.EqualTo(mode), sentence);
   Assert.That(mode == MoveConditionSlotEditor.CategoryMode ? editor.CategoryEditor.Value : editor.NameText, Is.EqualTo(shown));
   Assert.That(builder.Result, Is.EqualTo(sentence), "다시 열어도 같은 표준 문장");
  }

  [Test] public void EffectModulesUseTheSameConditionEditor() {
   var builder = new ModuleBuilderPanel(new[] { ModuleKind.Effect }, TestData.Db, null);
   Assert.That(builder.Select("effect.guard.pierce.pokemon", new Dictionary<string, string> { { "조건", "「격투」" } }), Is.True);
   var editor = (MoveConditionSlotEditor)builder.Editor("조건");
   Assert.That(editor.Mode, Is.EqualTo(MoveConditionSlotEditor.CategoryMode));
   Assert.That(editor.CategoryEditor.Value, Is.EqualTo("격투"));
   string byCategory = builder.Result;
   editor.SetName("몸통박치기");
   Assert.That(builder.Result, Is.EqualTo(byCategory.Replace("「격투」", "「기술:몸통박치기」")));
   Assert.That(builder.Load(builder.Result), Is.True);
   Assert.That(((MoveConditionSlotEditor)builder.Editor("조건")).Mode, Is.EqualTo(MoveConditionSlotEditor.NameMode));
  }
 }
}
