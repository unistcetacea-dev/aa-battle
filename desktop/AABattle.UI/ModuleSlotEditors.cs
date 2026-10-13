using System;
using System.Linq;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using System.Collections.Generic;

namespace AABattle {
 // 모듈 빈칸 하나의 입력 칸. 값은 항상 표준 문자열이다(숫자는 영문 숫자 표기).
 abstract class SlotEditor {
  public Control Control { get; protected set; }
  public ModuleSlot Slot { get; protected set; }
  public event EventHandler Changed;
  public abstract string Value { get; set; }
  protected void Raise() {
   if (Changed != null) Changed(this, EventArgs.Empty);
  }
  public virtual void Dispose() {
   if (Control != null) Control.Dispose();
  }
 }

 // 숫자 칸: 배율·분모·랭크·횟수·기간·종족치 수치
 sealed class NumberSlotEditor : SlotEditor {
  readonly PixelNumber box;
  readonly int decimals;

  public NumberSlotEditor(ModuleSlot slot) {
   Slot = slot;
   decimals = slot.Decimals;
   box = new PixelNumber {
    Minimum = slot.Minimum,
    Maximum = slot.Maximum,
    DecimalPlaces = decimals,
    Increment = decimals > 0 ? .05M : 1,
    Dock = DockStyle.Fill,
    Font = Theme.Input(),
    BorderStyle = BorderStyle.FixedSingle
   };
   Control = box;
   box.ValueChanged += (s, e) => Raise();
  }

  public override string Value {
   get { return box.Value.ToString(decimals > 0 ? "0.####" : "0", CultureInfo.InvariantCulture); }
   set {
    decimal parsed;
    if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed)) box.Value = parsed;
   }
  }

  // 편집 중인 포켓몬의 종족치처럼 상황에 따라 바뀌는 범위
  public void SetRange(int minimum, int maximum) {
   if (box.Minimum == minimum && box.Maximum == maximum) return;
   box.Maximum = Math.Max(minimum, maximum);
   box.Minimum = minimum;
  }
 }

 // 글자 칸: 이름·기술 조건·트리거 문장 등
 sealed class TextSlotEditor : SlotEditor {
  readonly PixelTextBox box;

  public TextSlotEditor(ModuleSlot slot) {
   Slot = slot;
   box = new PixelTextBox { Dock = DockStyle.Fill, Font = Theme.Input() };
   Control = box;
   box.TextChanged += (s, e) => Raise();
  }

  public override string Value {
   get { return box.Text; }
   set { if (box.Text != (value ?? "")) box.Text = value ?? ""; }
  }
 }

 // 고르는 칸: 선택지가 적으면 픽셀 콤보, 많으면 검색 목록 창
 sealed class ChoiceSlotEditor : SlotEditor {
  public const int ComboLimit = 14;
  const string Blank = "(비움)";
  readonly Panel host = new Panel { Dock = DockStyle.Fill };
  readonly PixelComboBox combo = new PixelComboBox { Dock = DockStyle.Fill };
  readonly FlatButton button = new FlatButton { Dock = DockStyle.Fill, Font = Theme.UI(8) };
  readonly string title;
  string[] items = new string[0];
  string current = "";
  bool loading;

  public ChoiceSlotEditor(ModuleSlot slot, string[] choices, string caption) {
   Slot = slot;
   title = caption;
   Control = host;
   host.Controls.Add(combo);
   host.Controls.Add(button);
   combo.SelectedIndexChanged += (s, e) => {
    if (loading) return;
    current = FromDisplay(combo.Text);
    Raise();
   };
   button.Click += (s, e) => PickFromList();
   SetItems(choices);
  }

  public string[] Items { get { return items.ToArray(); } }

  static string ToDisplay(string value) { return value.Length == 0 ? Blank : value; }
  static string FromDisplay(string value) { return value == Blank ? "" : value; }

  public void SetItems(string[] choices) {
   items = choices ?? new string[0];
   if (!items.Contains(current)) current = items.Length > 0 ? items[0] : "";
   loading = true;
   combo.Items.Clear();
   combo.Items.AddRange(items.Select(ToDisplay).Cast<object>().ToArray());
   loading = false;
   bool useCombo = items.Length <= ComboLimit;
   combo.Visible = useCombo;
   button.Visible = !useCombo;
   Show();
  }

  void Show() {
   loading = true;
   int index = Array.IndexOf(items, current);
   if (index >= 0) combo.SelectedIndex = index;
   else if (current.Length > 0) combo.Text = current;
   else combo.SelectedIndex = -1;
   button.Text = ToDisplay(current) + " ▼";
   loading = false;
  }

  void PickFromList() {
   var owner = button.FindForm();
   using (var dialog = new SearchToggleForm(title + " 선택 · 검색", items, new[] { current }, 1)) {
    if (dialog.ShowDialog(owner) != DialogResult.OK || dialog.Selected.Length == 0) return;
    current = dialog.Selected[0];
    Show();
    Raise();
   }
  }

  public override string Value {
   get { return current; }
   set {
    if (current == (value ?? "")) return;
    current = value ?? "";
    Show();
   }
  }
 }

 // 여럿을 고르는 칸(상태·타입·능력·기술): 검색/토글 창에서 고르고 「/」로 잇는다
 sealed class MultiSlotEditor : SlotEditor {
  readonly FlatButton button = new FlatButton { Dock = DockStyle.Fill, Font = Theme.UI(8) };
  readonly string title;
  string[] items;
  string current = "";

  public MultiSlotEditor(ModuleSlot slot, string[] choices, string caption) {
   Slot = slot;
   title = caption;
   items = choices ?? new string[0];
   Control = button;
   button.Click += (s, e) => PickFromList();
   Show();
  }

  void Show() {
   button.Text = (current.Length == 0 ? "고르기 · 검색/토글" : current) + " ▼";
  }

  void PickFromList() {
   var selected = current.Split('/').Where(x => x.Length > 0).ToArray();
   using (var dialog = new SearchToggleForm(title + " 선택 · 검색/토글", items, selected, 0)) {
    if (dialog.ShowDialog(button.FindForm()) != DialogResult.OK) return;
    current = string.Join("/", dialog.Selected);
    Show();
    Raise();
   }
  }

  public override string Value {
   get { return current; }
   set {
    if (current == (value ?? "")) return;
    current = value ?? "";
    Show();
   }
  }
 }
}
