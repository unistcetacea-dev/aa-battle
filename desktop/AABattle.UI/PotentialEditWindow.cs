using System;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;

namespace AABattle {
 // 포텐셜 한 개의 편집 값. 편집 창은 이 사본만 고치고, 「확인」을 누를 때만 표(DataGridView)에 쓴다.
 sealed class PotentialDraft {
  public bool Disabled;
  public string Slot = "", TargetType = "", Name = "", Unlocks = "";
  public string Trigger = "", Effect = "", Raw = "";
  public string Activation = "", Uses = "", TriggerSubject = "", EffectSubject = "";

  public PotentialDraft Copy() { return (PotentialDraft)MemberwiseClone(); }
 }

 // 포텐셜 편집 창. 이름·종류·상대 타입·해제 권한·트리거·효과(빈칸 채우기 모듈)를 한곳에서 고친다.
 // 입력하는 동안에는 표·트리·미리보기를 건드리지 않는다(다시 불러오지 않음). 「취소」는 사본을 버린다.
 sealed class PotentialEditWindow : Form {
  readonly DataEditorForm owner;
  readonly Database db;
  readonly PotentialDraft draft;
  readonly bool trainerRecord;
  readonly ChoiceSlotEditor slotEditor, typeEditor;
  readonly Panel nameHost = new Panel { Dock = DockStyle.Fill };
  readonly PixelTextBox nameText = new PixelTextBox { Dock = DockStyle.Fill, Font = Theme.Input() };
  readonly PixelTextBox unlocks = new PixelTextBox { Dock = DockStyle.Fill, Font = Theme.Input() };
  readonly PixelCheckBox disabled = new PixelCheckBox { Text = "발동 금지", Dock = DockStyle.Fill, Font = Theme.UI(8) };
  readonly Label typeLabel, notice, status;
  readonly Panel builderHost = new Panel { Dock = DockStyle.Fill };
  ChoiceSlotEditor nameChoice;
  bool loading, released;

  public PixelTextBox TriggerBox { get; private set; }
  public PixelTextBox EffectBox { get; private set; }
  // 「확인」으로 닫았을 때만 채워지는 결과 사본
  public PotentialDraft Result { get; private set; }
  public bool Confirmed { get; private set; }

  public PotentialEditWindow(DataEditorForm editor, Database database, PotentialDraft source, string[] slotChoices, bool trainer) {
   owner = editor;
   db = database;
   draft = source.Copy();
   trainerRecord = trainer;
   Text = "포텐셜 편집";
   BackColor = Color.White;
   Font = Theme.UI();
   KeyPreview = true;
   StartPosition = FormStartPosition.CenterParent;
   var area = Screen.FromControl(owner).WorkingArea;
   ClientSize = new Size(Math.Min(1560, area.Width - 40), Math.Min(1040, area.Height - 60));
   MinimumSize = new Size(Math.Min(1200, area.Width - 40), Math.Min(820, area.Height - 60));

   slotEditor = new ChoiceSlotEditor(new ModuleSlot("종류", ModuleSlotKind.Text, ""), new[] { "" }.Concat(slotChoices.Where(x => x.Length > 0)).ToArray(), "포텐셜 종류");
   typeEditor = new ChoiceSlotEditor(new ModuleSlot("상대 타입", ModuleSlotKind.Type, ""), new[] { "" }, "상대 타입");
   typeLabel = Caption("상대 타입");
   notice = Caption("");
   notice.ForeColor = Theme.Muted;
   status = Caption("확인을 눌러야 표에 반영됩니다.");
   status.ForeColor = Theme.Muted;

   // 한 줄: 종류 · 이름 · (필요할 때만) 상대 타입 / 둘째 줄: 해제 권한 · 발동 금지 / 셋째 줄: 안내
   var fields = new TableLayoutPanel { Dock = DockStyle.Top, Height = 150, ColumnCount = 6, RowCount = 3, Padding = new Padding(10, 8, 10, 4) };
   fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
   fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));
   fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
   fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
   fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
   fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));
   fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
   fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
   fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
   fields.Controls.Add(Caption("종류"), 0, 0);
   fields.Controls.Add(slotEditor.Control, 1, 0);
   fields.Controls.Add(Caption("이름"), 2, 0);
   fields.Controls.Add(nameHost, 3, 0);
   fields.Controls.Add(typeLabel, 4, 0);
   fields.Controls.Add(typeEditor.Control, 5, 0);
   fields.Controls.Add(Caption("해제 권한"), 0, 1);
   fields.Controls.Add(unlocks, 1, 1);
   fields.SetColumnSpan(unlocks, 3);
   fields.Controls.Add(disabled, 4, 1);
   fields.SetColumnSpan(disabled, 2);
   fields.Controls.Add(notice, 0, 2);
   fields.SetColumnSpan(notice, 6);

   var texts = new TableLayoutPanel { Dock = DockStyle.Top, Height = 170, ColumnCount = 2, RowCount = 2, Padding = new Padding(10, 0, 10, 4) };
   texts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
   texts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
   texts.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
   texts.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
   TriggerBox = new PixelTextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, Font = Theme.Input() };
   EffectBox = new PixelTextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, Font = Theme.Input() };
   texts.Controls.Add(Caption("트리거 조건"), 0, 0);
   texts.Controls.Add(Caption("효과"), 1, 0);
   texts.Controls.Add(TriggerBox, 0, 1);
   texts.Controls.Add(EffectBox, 1, 1);

   var bar = new Panel { Dock = DockStyle.Bottom, Height = 72, Padding = new Padding(10, 8, 10, 8) };
   var cancel = new FlatButton { Text = "취소", Dock = DockStyle.Right, Width = 170, Font = Theme.UI(16) };
   var ok = new FlatButton { Text = "확인", Dock = DockStyle.Right, Width = 170, Primary = true, Font = Theme.UI(16) };
   cancel.Click += (s, e) => Cancel();
   ok.Click += (s, e) => Accept();
   bar.Controls.Add(status);
   bar.Controls.Add(cancel);
   bar.Controls.Add(ok);
   CancelButton = cancel;

   Controls.Add(builderHost);
   Controls.Add(bar);
   Controls.Add(texts);
   Controls.Add(fields);
   builderHost.BringToFront();

   // 같은 모듈 화면(분류 → 모듈 → 빈칸)을 이 창에 옮겨 쓴다. 닫을 때 편집기로 돌려놓는다
   owner.AttachBuilders(builderHost);
   Theme.Enlarge(this);

   slotEditor.Changed += (s, e) => { if (!loading) SlotChanged(); };
   typeEditor.Changed += (s, e) => { if (!loading) TypeChanged(); };
   nameText.TextChanged += (s, e) => { };   // 이름 입력은 읽어 두기만 한다(다시 그리거나 불러오지 않음)
   LoadFromDraft();
  }

  // 트리거·효과 칸의 글자와 입력 방식·주체를 모듈 빈칸 채우기 화면에 다시 채운다
  void PushToBuilders() {
   draft.Trigger = TriggerBox.Text;
   draft.Effect = EffectBox.Text;
   owner.LoadBuilders(draft);
  }

  string CurrentName { get { return nameChoice != null && nameChoice.Control.Parent == nameHost ? nameChoice.Value : nameText.Text.Trim(); } }
  bool NameIsChoice { get { return nameChoice != null && nameChoice.Control.Parent == nameHost; } }

  // 칸 이름표: 편집기의 다른 화면과 같은 큰 도트 글자, 칸 높이의 가운데
  static Label Caption(string text) {
   var label = Theme.Label(text);
   label.Font = Theme.UI(16);
   label.Dock = DockStyle.Fill;
   return label;
  }

  // 편집 중인 값(트리거·효과 칸의 글자까지 모은 사본)
  public PotentialDraft Collect() {
   draft.Slot = slotEditor.Value.Trim();
   draft.Name = CurrentName.Trim();
   draft.TargetType = typeEditor.Control.Visible ? typeEditor.Value : draft.TargetType;
   draft.Unlocks = unlocks.Text.Trim();
   draft.Disabled = disabled.Checked;
   draft.Trigger = TriggerBox.Text.Trim();
   draft.Effect = EffectBox.Text.Trim();
   return draft.Copy();
  }

  void LoadFromDraft() {
   loading = true;
   try {
    slotEditor.Value = draft.Slot;
    disabled.Checked = draft.Disabled;
    unlocks.Text = draft.Unlocks;
    TriggerBox.Text = draft.Trigger;
    EffectBox.Text = draft.Effect;
    // 상대 타입이 비어 있는 옛 회피·내성·격 행은 이름(대투회피 등)에서 타입을 알아내야 템플릿 이름이 불러와진다
    if (EditorTemplate.IsCounterSlot(draft.Slot) && draft.TargetType.Length == 0)
      draft.TargetType = EditorTemplate.InferCounterType(draft.Name);
    RefreshTypeChoices(draft.TargetType);
    RebuildName(draft.Name);
    UpdateNotice();
   } finally { loading = false; }
   PushToBuilders();
  }

  // 종류·이름에 따라 상대 타입 칸을 보이거나 숨기고, 선택지를 맞춘다
  void RefreshTypeChoices(string value) {
   string slot = slotEditor.Value;
   bool typed = EditorTemplate.IsTypedPotential(slot, nameChoice == null ? draft.Name : CurrentName);
   var items = new List<string> { "" };
   if (EditorTemplate.IsCounterSlot(slot)) items.AddRange(EditorTemplate.CounterTypeNames());
   else if (typed) items.AddRange(db.types);
   if (value.Length > 0 && !items.Contains(value)) items.Add(value);
   typeEditor.SetItems(items.ToArray());
   typeEditor.Value = value;
   typeEditor.Control.Visible = typeLabel.Visible = typed;
  }

  // 종류에 고정 선택지(역할·범용·선·특권·타입별 포텐셜)가 있으면 고르는 칸, 없으면 직접 입력 칸
  void RebuildName(string value) {
   string slot = slotEditor.Value, type = typeEditor.Value;
   var options = EditorTemplate.FixedOptions(slot) ?? (EditorTemplate.IsCounterSlot(slot) ? EditorTemplate.CounterOptions(slot, type) : null);
   foreach (Control c in nameHost.Controls.Cast<Control>().ToArray()) nameHost.Controls.Remove(c);
   if (nameChoice != null) { nameChoice.Dispose(); nameChoice = null; }
   if (options != null) {
    var items = new List<string> { "" };
    items.AddRange(options.Select(x => x.name));
    if (value.Length > 0 && !items.Contains(value)) items.Add(value);
    nameChoice = new ChoiceSlotEditor(new ModuleSlot("이름", ModuleSlotKind.Text, ""), items.ToArray(), "포텐셜 이름");
    nameChoice.Value = value;
    nameChoice.Changed += (s, e) => { if (!loading) NameChosen(); };
    nameHost.Controls.Add(nameChoice.Control);
   } else {
    nameText.Text = value;
    nameHost.Controls.Add(nameText);
   }
   Theme.Enlarge(nameHost);
  }

  void UpdateNotice() {
   string slot = slotEditor.Value;
   if (slot == TeamRules.ExclusiveSlot) notice.Text = "전용: 트레이너 포텐셜입니다. 개수 제한은 파티 전체에서 셉니다.";
   else notice.Text = "";
  }

  // 종류를 바꾸면 이름·타입·내용을 비운다(템플릿이 다르다)
  void SlotChanged() {
   loading = true;
   try {
    draft.Raw = ""; draft.Activation = ""; draft.Uses = ""; draft.TargetType = "";
    TriggerBox.Text = ""; EffectBox.Text = "";
    typeEditor.SetItems(new[] { "" });
    typeEditor.Value = "";
    RebuildName("");
    RefreshTypeChoices("");
    UpdateNotice();
   } finally { loading = false; }
   PushToBuilders();
  }

  void TypeChanged() {
   string slot = slotEditor.Value;
   loading = true;
   try {
    if (EditorTemplate.IsCounterSlot(slot)) {
     // 타입별 포텐셜은 타입이 정해야 이름 선택지가 나온다
     draft.Raw = ""; draft.Activation = ""; draft.Uses = "";
     TriggerBox.Text = ""; EffectBox.Text = "";
     RebuildName("");
     PushToBuilders();
     return;
    }
   } finally { loading = false; }
   ApplyTemplate();
  }

  void NameChosen() {
   loading = true;
   try {
    RefreshTypeChoices(typeEditor.Value);
    if (!typeEditor.Control.Visible) draft.TargetType = "";
   } finally { loading = false; }
   ApplyTemplate();
  }

  // 고른 이름의 표준 트리거·효과를 칸에 채운다(기존 템플릿 입력과 같은 규칙)
  void ApplyTemplate() {
   string slot = slotEditor.Value, name = CurrentName, type = typeEditor.Value;
   var option = EditorTemplate.FixedOption(slot, name) ?? EditorTemplate.CounterOption(slot, type, name);
   if (option == null) return;
   draft.Activation = "자동";
   draft.Uses = option.name == "베테랑" ? "1/시" : "";
   string text = slot == "특권" && option.name == "계약의 특권"
    ? option.effect.Replace("○", type.Length > 0 ? type : "타입 미지정") : option.effect;
   string condition, result;
   if (option.template != null) { condition = option.template.trigger; result = option.template.EffectText; text = option.template.Text; }
   else PotentialLibrary.Split(text, out condition, out result);
   if (slot == "특권" && condition.Length == 0) condition = TriggerStructures.AlwaysLabel;
   loading = true;
   try { TriggerBox.Text = condition; EffectBox.Text = result; } finally { loading = false; }
   draft.Raw = text;
   PushToBuilders();
  }

  // 편집기의 「구조 트리거/효과 입력」 단추가 호출한다
  public void SetPart(bool trigger, string text, string activation, string subject) {
   if (trigger) {
    TriggerBox.Text = text;
    draft.Activation = activation;
    draft.TriggerSubject = subject;
   } else {
    EffectBox.Text = text;
    draft.EffectSubject = subject;
   }
  }

  public void AppendEffect(string effect, string subject) {
   string current = EffectBox.Text.Trim();
   EffectBox.Text = current.Length == 0 ? effect : current + "\r\n" + effect;
   draft.EffectSubject = subject;
  }

  // ---- 코드(검사)에서 같은 경로로 입력할 때 쓰는 진입점 ----
  public void ChooseSlot(string slot) { slotEditor.Value = slot; SlotChanged(); }
  public void ChooseTargetType(string type) { typeEditor.Value = type; TypeChanged(); }
  public void ChooseName(string name) {
   if (NameIsChoice) { nameChoice.Value = name; NameChosen(); }
   else nameText.Text = name;
  }
  public void TypeName(string name) {
   if (NameIsChoice) throw new InvalidOperationException("이 종류는 이름을 고르는 칸이다");
   nameText.Text = name;
  }
  public string[] SlotChoices { get { return slotEditor.Items; } }
  public bool NameIsFixedChoice { get { return NameIsChoice; } }
  public string[] NameChoices { get { return NameIsChoice ? nameChoice.Items : new string[0]; } }
  public bool TypeRowVisible { get { return typeEditor.Control.Visible; } }
  public string NoticeText { get { return notice.Text; } }
  public bool Disabled { get { return disabled.Checked; } set { disabled.Checked = value; } }
  public string UnlockText { get { return unlocks.Text; } set { unlocks.Text = value; } }

  public void Accept() {
   var value = Collect();
   if (value.Slot.Length == 0 && value.Name.Length == 0) {
    GameDialog.Show(this, "종류나 이름을 입력하세요.", "포텐셜 편집");
    return;
   }
   Result = value;
   Confirmed = true;
   DialogResult = DialogResult.OK;
  }

  public void Cancel() {
   Confirmed = false;
   Result = null;
   DialogResult = DialogResult.Cancel;
  }

  protected override void OnFormClosed(FormClosedEventArgs e) {
   base.OnFormClosed(e);
   Release();
  }

  void Release() {
   if (released) return;
   released = true;
   owner.ReleaseBuilders(this);
  }

  protected override void Dispose(bool disposing) {
   if (disposing) Release();
   base.Dispose(disposing);
  }
 }
}
