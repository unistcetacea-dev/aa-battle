using System;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;

namespace AABattle {
 // 모듈 목록(ModuleCatalog) 기반 빈칸 채우기 화면: 분류 → 모듈 → 빈칸 → 미리보기(표준 문장).
 // 트리거와 효과 편집기가 같은 화면 구조를 쓴다. 문장은 엔진 모듈의 Fill이 만들고,
 // 저장된 문장은 TryRead로 읽어 빈칸을 다시 채운다.
 class ModuleBuilderPanel : Panel {
  const int RowHeight = 46, PreviewHeight = 210, LabelWidth = 215;
  readonly Database db;
  readonly Func<PokemonRecord> pokemon;
  readonly ModuleDefinition[] pool;
  readonly string[] families;
  readonly PixelScrollPanel scroll = new PixelScrollPanel { Dock = DockStyle.Fill };
  readonly TableLayoutPanel table = new TableLayoutPanel { ColumnCount = 2, Padding = new Padding(8) };
  readonly ChoiceSlotEditor familyEditor, moduleEditor;
  readonly PixelText preview = new PixelText { Dock = DockStyle.Fill };
  readonly List<SlotEditor> editors = new List<SlotEditor>();
  readonly List<Control> labels = new List<Control>();
  ModuleDefinition[] listed = new ModuleDefinition[0];
  ModuleDefinition module;
  bool loading, updating;

  public event EventHandler Changed;

  public ModuleBuilderPanel(ModuleKind[] kinds, Database database, Func<PokemonRecord> pokemonContext) {
   db = database;
   pokemon = pokemonContext;
   pool = ModuleCatalog.All.Where(x => kinds.Contains(x.Kind)).ToArray();
   families = pool.Select(x => x.Family).Distinct().ToArray();
   Dock = DockStyle.Fill;
   table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, LabelWidth));
   table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
   scroll.Content = table;
   Controls.Add(scroll);
   familyEditor = new ChoiceSlotEditor(new ModuleSlot("분류", ModuleSlotKind.Text, families[0]), families, "분류");
   moduleEditor = new ChoiceSlotEditor(new ModuleSlot("모듈", ModuleSlotKind.Text, ""), new string[0], "모듈");
   familyEditor.Changed += (s, e) => {
    if (loading) return;
    ListFamily(familyEditor.Value);
    Rebuild(null);
   };
   moduleEditor.Changed += (s, e) => {
    if (loading) return;
    Rebuild(null);
   };
   ListFamily(families[0]);
   Rebuild(null);
  }

  public ModuleDefinition Module { get { return module; } }
  // 미리보기와 같은 완성 문장. 빈칸이 모자라거나 읽을 수 없으면 빈 문자열
  public string Result { get; private set; }
  public string Message { get; private set; }
  public PixelText Preview { get { return preview; } }
  public bool IsFamily(string name) { return families.Contains(name); }
  public string[] Families { get { return families.ToArray(); } }
  public string[] ModuleNames { get { return listed.Select(DisplayName).ToArray(); } }

  string DisplayName(ModuleDefinition candidate) {
   bool duplicate = listed.Count(x => x.Name == candidate.Name) > 1;
   return duplicate ? candidate.Name + " · " + candidate.Id : candidate.Name;
  }

  void ListFamily(string family) {
   listed = pool.Where(x => x.Family == family).ToArray();
   bool before = loading;
   loading = true;
   moduleEditor.SetItems(listed.Select(DisplayName).ToArray());
   if (listed.Length > 0) moduleEditor.Value = DisplayName(listed[0]);
   loading = before;
  }

  // 분류·모듈·빈칸 값을 지정해 연다. 값에 없는 빈칸은 기본값이다
  public bool Select(string id, IDictionary<string, string> values = null) {
   var target = pool.FirstOrDefault(x => x.Id == id);
   if (target == null) return false;
   Open(target, values);
   return true;
  }

  void Open(ModuleDefinition target, IDictionary<string, string> values) {
   loading = true;
   familyEditor.Value = target.Family;
   ListFamily(target.Family);
   moduleEditor.Value = DisplayName(target);
   loading = false;
   Rebuild(values);
  }

  public string GetValue(string slotId) {
   var editor = editors.FirstOrDefault(x => x.Slot.Id == slotId);
   return editor == null ? null : editor.Value;
  }

  public bool SetValue(string slotId, string value) {
   var editor = editors.FirstOrDefault(x => x.Slot.Id == slotId);
   if (editor == null) return false;
   editor.Value = value;
   Recompute();
   return true;
  }

  public SlotEditor Editor(string slotId) { return editors.FirstOrDefault(x => x.Slot.Id == slotId); }

  // 후보 문장을 차례로 읽어 처음 읽히는 것으로 화면을 채운다
  public bool Load(params string[] candidates) {
   foreach (string text in candidates) {
    if (string.IsNullOrWhiteSpace(text)) continue;
    ModuleDefinition found;
    Dictionary<string, string> values;
    if (!TryReadModule(text, out found, out values)) continue;
    Open(found, values);
    return true;
   }
   return false;
  }

  protected virtual bool TryReadModule(string text, out ModuleDefinition found, out Dictionary<string, string> values) {
   foreach (var candidate in pool) {
    if (candidate.TryRead(text, out values)) { found = candidate; return true; }
   }
   found = null; values = null; return false;
  }

  protected IEnumerable<ModuleDefinition> Pool { get { return pool; } }

  // 빈칸 값 → 문장. 트리거처럼 한 번에 여러 줄을 내야 하는 곳에서 바꾼다
  protected virtual string Compose(ModuleDefinition target, Dictionary<string, string> values) {
   return target.Fill(values);
  }

  SlotEditor CreateEditor(ModuleSlot slot) {
   if (slot.Numeric) return new NumberSlotEditor(slot);
   if (slot.Kind == ModuleSlotKind.MoveCondition) return new MoveConditionSlotEditor(slot, db);
   if (slot.Kind == ModuleSlotKind.Trigger) return new TextSlotEditor(slot);
   string[] choices = slot.GetChoices(db);
   if (slot.Multiple && choices.Count(x => x.Length > 0) > 1)
    return new MultiSlotEditor(slot, choices.Where(x => x.Length > 0).ToArray(), slot.Name);
   bool listed = slot.Choices.Length > 0 || slot.Kind != ModuleSlotKind.Text;
   if (!slot.Multiple && listed && choices.Length > 1) return new ChoiceSlotEditor(slot, choices, slot.Name);
   return new TextSlotEditor(slot);
  }

  void AddRow(string label, Control control, int height) {
   int row = table.RowCount++;
   table.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
   var caption = Theme.Label(label);
   labels.Add(caption);
   table.Controls.Add(caption, 0, row);
   table.Controls.Add(control, 1, row);
  }

  void Rebuild(IDictionary<string, string> values) {
   string shown = moduleEditor.Value;
   module = listed.FirstOrDefault(x => DisplayName(x) == shown) ?? listed.FirstOrDefault();
   if (module == null) return;
   table.SuspendLayout();
   table.Controls.Clear();
   foreach (var label in labels) label.Dispose();
   labels.Clear();
   foreach (var editor in editors) editor.Dispose();
   editors.Clear();
   table.RowStyles.Clear();
   table.RowCount = 0;
   AddRow("분류", familyEditor.Control, RowHeight);
   AddRow("모듈", moduleEditor.Control, RowHeight);
   foreach (var slot in module.Slots) {
    var editor = CreateEditor(slot);
    string value;
    editor.Value = values != null && values.TryGetValue(slot.Id, out value) ? value : slot.Default;
    editor.Changed += (s, e) => { if (!loading && !updating) Recompute(); };
    editors.Add(editor);
    AddRow(slot.Name, editor.Control, RowHeight);
   }
   AddRow("결과", preview, PreviewHeight);
   table.Height = table.Padding.Vertical + table.RowStyles.Cast<RowStyle>().Sum(x => (int)x.Height);
   table.ResumeLayout(true);
   Theme.Enlarge(table);
   Recompute();
  }

  // 편집 중인 포켓몬에 맞춰 바뀌는 숫자 범위(종족치 등)
  void UpdateContextRanges(Dictionary<string, string> values) {
   updating = true;
   try {
    foreach (var editor in editors) {
     var numeric = editor as NumberSlotEditor;
     if (numeric == null || editor.Slot.ContextRange == null) continue;
     int[] range = editor.Slot.ContextRange(pokemon == null ? null : pokemon(), values);
     numeric.SetRange(range[0], range[1]);
     values[editor.Slot.Id] = numeric.Value;
    }
   } finally { updating = false; }
  }

  void Recompute() {
   if (module == null) return;
   var values = editors.ToDictionary(x => x.Slot.Id, x => x.Value);
   UpdateContextRanges(values);
   string text = "", error = null;
   try {
    NormalizeInputs(values);
    text = Compose(module, values);
   } catch (ArgumentException ex) {
    error = ex.Message;
   } catch (KeyNotFoundException) {
    error = "빈칸 값을 읽을 수 없습니다.";
   }
   Result = error == null ? text : "";
   Message = error ?? "";
   preview.Text = error ?? text;
   if (Changed != null && !loading) Changed(this, EventArgs.Empty);
  }

  // 느슨하게 적은 기술 조건·트리거 문장을 표준 표기로 바꾸고, 필요한 빈칸이 비었는지 본다
  void NormalizeInputs(Dictionary<string, string> values) {
   foreach (var slot in module.Slots) {
    string value = values[slot.Id];
    if (slot.Kind == ModuleSlotKind.MoveCondition) {
     string condition = ModuleCatalog.ConditionFromInput(value);
     if (condition == null) throw new ArgumentException("기술 조건을 읽을 수 없습니다: " + value);
     values[slot.Id] = condition;
    } else if (slot.Kind == ModuleSlotKind.Trigger) {
     values[slot.Id] = ModuleCatalog.TriggerFromInput(value);
    }
    bool mayBeBlank = slot.Default.Length == 0 || slot.Choices.Contains("") || slot.Kind == ModuleSlotKind.MoveCondition;
    if (values[slot.Id].Trim().Length == 0 && !mayBeBlank)
     throw new ArgumentException("빈칸을 채우세요: " + slot.Name);
   }
  }
 }
}
