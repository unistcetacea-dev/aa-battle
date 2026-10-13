using System;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 // 기술 도감 검색 공용 로직. 기술 도감 탭과 기술 조건 빈칸의 「기술 찾기」 창이 같은 규칙으로 후보를 거른다.
 static class MoveDexSearch {
  // 이름·타입·분류·대상·태그·효과 글자에 검색어가 들어 있는 기술(이름순)
  public static IEnumerable<Move> Filter(IEnumerable<Move> moves, string query) {
   string q = (query ?? "").Trim();
   return (moves ?? new Move[0])
    .Where(x => x != null && SearchText(x).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
    .OrderBy(x => x.name);
  }

  public static string SearchText(Move m) {
   return m.name + " " + string.Join(" ", m.types ?? new string[0]) + " " + m.category + " " + m.target + " "
    + string.Join(" ", m.tags ?? new string[0]) + " " + m.effect;
  }

  // 기술 도감 표(탭과 검색 창이 같은 모양을 쓴다)
  public static DataGridView CreateGrid() {
   return new PixelDexGrid {
    Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
    AllowUserToResizeRows = false, RowHeadersVisible = false, MultiSelect = false,
    SelectionMode = DataGridViewSelectionMode.FullRowSelect, BackgroundColor = Color.White,
    BorderStyle = BorderStyle.None, EnableHeadersVisualStyles = false, ColumnHeadersHeight = 34,
    RowTemplate = { Height = 36 },
    DefaultCellStyle = new DataGridViewCellStyle { Font = Theme.UI(10), SelectionBackColor = Color.Black, SelectionForeColor = Color.White },
    ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.Black, ForeColor = Color.White, Font = Theme.UI(8) }
   };
  }

  public static void AddColumn(DataGridView grid, string title, int width) {
   grid.Columns.Add(new DataGridViewTextBoxColumn {
    HeaderText = title, Width = width, MinimumWidth = width,
    AutoSizeMode = title.StartsWith("효과") ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None,
    SortMode = DataGridViewColumnSortMode.NotSortable
   });
  }
 }

 // 기술 찾기 창: 검색어를 입력하면 기술 도감 데이터에서 후보가 걸러지고, 고르면 이름을 돌려준다
 sealed class MoveSearchForm : Form {
  readonly Database db;
  readonly PixelSearchBox search;
  readonly DataGridView grid;
  readonly PixelText detail;
  readonly Label count;

  public string SelectedName { get; private set; }

  public MoveSearchForm(Database database, string initial) {
   db = database;
   SelectedName = "";
   Load += (s, e) => Theme.Enlarge(this);
   Text = "기술 찾기 · 기술 도감 검색";
   Size = new Size(940, 760);
   StartPosition = FormStartPosition.CenterParent;
   BackColor = Color.White;
   Font = Theme.UI();
   count = Theme.Label("기술 찾기", 36);
   search = new PixelSearchBox { Dock = DockStyle.Top, Font = Theme.UI(8) };
   grid = MoveDexSearch.CreateGrid();
   foreach (var column in new[] { new[] { "기술", "170" }, new[] { "타입", "110" }, new[] { "분류", "80" }, new[] { "위력", "80" }, new[] { "명중", "80" }, new[] { "효과 요약", "300" } })
    MoveDexSearch.AddColumn(grid, column[0], int.Parse(column[1]));
   detail = new PixelText { Dock = DockStyle.Bottom, Height = 150 };
   var choose = new FlatButton { Text = "선택 적용", Dock = DockStyle.Bottom, Height = 50, Primary = true };
   choose.Click += (s, e) => Accept();
   var host = new PixelGridHost(grid);
   Controls.Add(host);
   Controls.Add(detail);
   Controls.Add(choose);
   Controls.Add(search);
   Controls.Add(count);
   host.BringToFront();
   search.TextChanged += (s, e) => Reload();
   grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) Accept(); };
   grid.SelectionChanged += (s, e) => ShowDetail();
   grid.CurrentCellChanged += (s, e) => ShowDetail();
   search.Text = initial ?? "";
   Reload();
  }

  // 검색어에 맞는 기술 이름(화면에 보이는 순서)
  public string[] Candidates {
   get { return grid.Rows.Cast<DataGridViewRow>().Select(x => ((Move)x.Tag).name).ToArray(); }
  }

  public void Pick(string name) {
   foreach (DataGridViewRow row in grid.Rows)
    if (((Move)row.Tag).name == name) { grid.CurrentCell = row.Cells[0]; Accept(); return; }
  }

  void Reload() {
   grid.Rows.Clear();
   foreach (var m in MoveDexSearch.Filter(db == null ? null : db.moves, search.Text)) {
    int i = grid.Rows.Add(m.name, string.Join("/", m.types ?? new string[0]), m.category, m.power, m.accuracy, m.effect);
    grid.Rows[i].Tag = m;
   }
   if (grid.RowCount > 0) grid.CurrentCell = grid.Rows[0].Cells[0];
   count.Text = "기술 찾기 · " + grid.RowCount + "개 · 이름 / 타입 / 대상 / 태그 / 효과 검색";
   ShowDetail();
  }

  void ShowDetail() {
   var m = grid.CurrentRow == null ? null : grid.CurrentRow.Tag as Move;
   detail.Text = m == null ? "검색 결과가 없습니다."
    : "【" + m.name + "】  " + string.Join(" / ", m.types ?? new string[0]) + " · " + m.category
      + "\r\n위력 " + m.power + "   명중 " + m.accuracy + "   우선도 " + m.priority + "   대상 " + m.target
      + "\r\n효과: " + m.effect;
  }

  void Accept() {
   var m = grid.CurrentRow == null ? null : grid.CurrentRow.Tag as Move;
   if (m == null) return;
   SelectedName = m.name;
   DialogResult = DialogResult.OK;
  }
 }

 // 기술 조건 빈칸의 입력 칸. 입력 방식이 둘이다.
 //  분류 방식: 공격·변화·물리·특수·접촉·타입 등 MoveCondition이 읽는 토큰을 콤보에서 고른다.
 //  이름 방식: 기술 이름(또는 「불꽃」「접촉」 같은 직접 입력)을 적고, 기술 도감 검색으로 후보를 걸러 고른다.
 // 두 방식 모두 같은 표준 토큰(「공격」·「기술:이름」)을 내고, 저장된 문장을 다시 열면 알맞은 방식으로 복원된다.
 sealed class MoveConditionSlotEditor : SlotEditor {
  public const string CategoryMode = "분류", NameMode = "기술 이름";
  readonly Database db;
  readonly string[] categories;
  readonly HashSet<string> categoryKeys;
  readonly HashSet<string> moveNames;
  readonly Panel host = new Panel { Dock = DockStyle.Fill };
  readonly Panel body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 0, 0, 0) };
  readonly PixelComboBox mode = new PixelComboBox { Dock = DockStyle.Left, Width = 150 };
  readonly ChoiceSlotEditor category;
  readonly Panel namePanel = new Panel { Dock = DockStyle.Fill };
  readonly PixelTextBox text = new PixelTextBox { Dock = DockStyle.Fill, Font = Theme.Input() };
  readonly FlatButton find = new FlatButton { Dock = DockStyle.Right, Width = 130, Text = "기술 찾기", Font = Theme.UI(8) };
  string active = CategoryMode;
  bool loading;

  public MoveConditionSlotEditor(ModuleSlot slot, Database database) {
   Slot = slot;
   db = database;
   categories = new ModuleSlot("분류", ModuleSlotKind.MoveCondition, "").GetChoices(null)
    .Where(x => !x.StartsWith("기술:")).ToArray();
   categoryKeys = new HashSet<string>(categories.Select(Key));
   moveNames = new HashSet<string>((db == null || db.moves == null ? new Move[0] : db.moves).Select(x => x.name));
   category = new ChoiceSlotEditor(new ModuleSlot("분류", ModuleSlotKind.MoveCondition, ""), categories, "기술 분류");
   Control = host;
   mode.Items.AddRange(new object[] { CategoryMode, NameMode });
   mode.SelectedIndex = 0;
   namePanel.Controls.Add(text);
   namePanel.Controls.Add(find);
   namePanel.Visible = false;
   body.Controls.Add(category.Control);
   body.Controls.Add(namePanel);
   host.Controls.Add(body);
   host.Controls.Add(mode);
   find.Enabled = db != null;
   category.Changed += (s, e) => { if (!loading) Raise(); };
   text.TextChanged += (s, e) => { if (!loading) Raise(); };
   mode.SelectedIndexChanged += (s, e) => { if (!loading) SwitchMode(Convert.ToString(mode.SelectedItem)); };
   find.Click += (s, e) => OpenSearch();
  }

  static string Key(string token) {
   return (token ?? "").Replace(" ", "").Replace("　", "");
  }

  public string Mode { get { return active; } }
  public ChoiceSlotEditor CategoryEditor { get { return category; } }
  public string[] Categories { get { return categories.ToArray(); } }

  // 기술 이름 칸에 적은 말. 기술 데이터에 있는 이름은 표준 토큰 「기술:이름」으로 바꾼다
  public string NameText {
   get { return text.Text; }
   set { text.Text = value ?? ""; }
  }

  // 입력한 글자에 맞는 기술 이름 후보(기술 도감과 같은 검색 규칙)
  public string[] Candidates(string query, int limit = 50) {
   return MoveDexSearch.Filter(db == null ? null : db.moves, query).Select(x => x.name).Take(limit).ToArray();
  }

  public void SetMode(string name) {
   if (name != CategoryMode && name != NameMode) throw new ArgumentException("알 수 없는 입력 방식: " + name);
   if (name == active) return;
   SwitchMode(name);
  }

  // 방식을 바꿔도 같은 조건이 남도록 값을 옮긴다
  void SwitchMode(string next) {
   string carried = ValueIn(active);
   loading = true;
   try {
    Show(next);
    if (next == CategoryMode) category.Value = SingleCategory(carried) ?? "";
    else text.Text = carried;
   } finally { loading = false; }
   Raise();
  }

  void Show(string name) {
   active = name;
   mode.SelectedItem = name;
   category.Control.Visible = name == CategoryMode;
   namePanel.Visible = name == NameMode;
  }

  string ValueIn(string which) {
   return which == CategoryMode ? category.Value : Resolve(text.Text);
  }

  // 분류 토큰 하나(또는 빈칸)면 그 선택지, 그렇지 않으면 null
  string SingleCategory(string value) {
   string v = (value ?? "").Trim();
   if (v.Length == 0) return "";
   var tokens = Regex.Matches(v, "「([^」]*)」").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
   if (tokens.Count == 0) tokens = v.Split(new[] { ' ', ',', '，' }, StringSplitOptions.RemoveEmptyEntries).ToList();
   else if (Regex.Replace(v, "「[^」]*」", "").Trim().Length > 0) return null;
   if (tokens.Count != 1) return null;
   string key = Key(tokens[0]);
   return categories.FirstOrDefault(x => x.Length > 0 && Key(x) == key);
  }

  // 기술 이름 칸의 글자 → 빈칸 값. 기술 데이터에 있는 이름만 「기술:이름」으로 바꾸고 나머지는 그대로 둔다
  string Resolve(string raw) {
   string t = (raw ?? "").Trim();
   if (t.Length == 0 || moveNames.Count == 0) return t;
   return Regex.Replace(t, "「[^」]*」|[^\\s,，「」]+", m => {
    string inner = m.Value.StartsWith("「") ? m.Value.Substring(1, m.Value.Length - 2).Trim() : m.Value;
    if (inner.StartsWith("기술:") || categoryKeys.Contains(Key(inner)) || !moveNames.Contains(inner)) return m.Value;
    return "「기술:" + inner + "」";
   });
  }

  public override string Value {
   get { return ValueIn(active); }
   set {
    string v = value ?? "";
    loading = true;
    try {
     string single = SingleCategory(v);
     if (single != null) { Show(CategoryMode); category.Value = single; }
     else { Show(NameMode); text.Text = v; }
    } finally { loading = false; }
   }
  }

  // 검색 창에서 고른 기술(또는 코드에서 지정한 기술) 이름을 「기술:이름」 토큰으로 넣는다
  public void SetName(string name) {
   if (string.IsNullOrWhiteSpace(name)) return;
   loading = true;
   try {
    Show(NameMode);
    text.Text = "「기술:" + name.Trim() + "」";
   } finally { loading = false; }
   Raise();
  }

  void OpenSearch() {
   string start = Regex.IsMatch(text.Text, "[「」]") ? "" : text.Text.Trim();
   using (var dialog = new MoveSearchForm(db, start)) {
    if (dialog.ShowDialog(host.FindForm()) != DialogResult.OK || dialog.SelectedName.Length == 0) return;
    SetName(dialog.SelectedName);
   }
  }
 }
}
