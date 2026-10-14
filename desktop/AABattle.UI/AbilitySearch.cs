using System;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;

namespace AABattle {
 // 특성 도감 탭과 특성 찾기 창이 같은 이름·효과 검색 규칙을 쓴다.
 static class AbilityDexSearch {
  public static string SearchText(AbilityDefinition ability) {
   return ability.name + " " + string.Join(" ", ability.triggers ?? new string[0]) + " "
    + string.Join(" ", ability.effects ?? new string[0]) + " " + ability.effect + " "
    + ability.support + " " + ability.blocker;
  }

  public static IEnumerable<AbilityDefinition> Filter(IEnumerable<AbilityDefinition> abilities, string query) {
   string q = (query ?? "").Trim();
   return (abilities ?? new AbilityDefinition[0])
    .Where(x => x != null && SearchText(x).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
    .OrderBy(x => x.name);
  }

  public static string Preview(AbilityDefinition ability) {
   if (ability == null) return "검색 결과가 없습니다.";
   string effects = ability.effects == null || ability.effects.Length == 0 ? ability.effect : string.Join(" ", ability.effects);
   return "【" + ability.name + "】  " + ability.support
    + "\r\n트리거: " + (ability.triggers == null || ability.triggers.Length == 0 ? "상시/원문 직접 효과" : string.Join(" / ", ability.triggers))
    + "\r\n효과: " + effects + "\r\n원문: " + ability.effect;
  }
 }

 // 특성 도감과 같은 내용을 검색하고 효과 미리보기를 보여 주는 선택 창.
 sealed class AbilitySearchForm : Form {
  readonly PixelSearchBox search;
  readonly DataGridView grid;
  readonly PixelText detail;
  readonly Label count;
  readonly bool multiple;
  readonly HashSet<string> initial;

  public string[] SelectedNames { get; private set; }
  public string[] Candidates { get { return grid.Rows.Cast<DataGridViewRow>().Select(x => ((AbilityDefinition)x.Tag).name).ToArray(); } }

  public AbilitySearchForm(IEnumerable<string> selected = null, bool allowMultiple = false, string initialQuery = "") {
   multiple = allowMultiple;
   initial = new HashSet<string>((selected ?? Enumerable.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)));
   SelectedNames = new string[0];
   Load += (s, e) => Theme.Enlarge(this);
   Text = "특성 찾기 · 특성 도감 검색";
   Size = new Size(940, 760);
   StartPosition = FormStartPosition.CenterParent;
   BackColor = Color.White;
   Font = Theme.UI();
   count = Theme.Label("특성 찾기", 36);
   search = new PixelSearchBox { Dock = DockStyle.Top, Font = Theme.UI(8) };
   grid = MoveDexSearch.CreateGrid();
   grid.MultiSelect = multiple;
   MoveDexSearch.AddColumn(grid, "특성", 170);
   MoveDexSearch.AddColumn(grid, "효과 요약", 600);
   detail = new PixelText { Dock = DockStyle.Bottom, Height = 170 };
   var choose = new FlatButton { Text = "선택 적용", Dock = DockStyle.Bottom, Height = 50, Primary = true };
   choose.Click += (s, e) => Accept();
   Controls.Add(new PixelGridHost(grid));
   Controls.Add(detail);
   Controls.Add(choose);
   Controls.Add(search);
   Controls.Add(count);
   search.TextChanged += (s, e) => Reload();
   grid.SelectionChanged += (s, e) => ShowDetail();
   grid.CurrentCellChanged += (s, e) => ShowDetail();
   grid.CellDoubleClick += (s, e) => { if (!multiple && e.RowIndex >= 0) Accept(); };
   search.Text = initialQuery ?? "";
   Reload();
  }

  public void Pick(string name) {
   foreach (DataGridViewRow row in grid.Rows) {
    var ability = row.Tag as AbilityDefinition;
    if (ability != null && ability.name == name) {
     grid.ClearSelection();
     row.Selected = true;
     grid.CurrentCell = row.Cells[0];
     Accept();
     return;
    }
   }
  }

  void Reload() {
   grid.Rows.Clear();
   foreach (var ability in AbilityDexSearch.Filter(AbilityCatalog.All, search.Text)) {
    string effects = ability.effects == null || ability.effects.Length == 0 ? ability.effect : string.Join(" ", ability.effects);
    int row = grid.Rows.Add(ability.name, effects);
    grid.Rows[row].Tag = ability;
    grid.Rows[row].Selected = initial.Contains(ability.name);
   }
   if (grid.RowCount > 0 && grid.CurrentCell == null) grid.CurrentCell = grid.Rows[0].Cells[0];
   count.Text = "특성 찾기 · " + grid.RowCount + "개 · 이름 / 효과 문장 검색";
   ShowDetail();
  }

  void ShowDetail() {
   detail.Text = AbilityDexSearch.Preview(grid.CurrentRow == null ? null : grid.CurrentRow.Tag as AbilityDefinition);
  }

  void Accept() {
   var rows = grid.SelectedRows.Cast<DataGridViewRow>().Where(x => x.Tag is AbilityDefinition).ToArray();
   if (rows.Length == 0 && grid.CurrentRow != null && grid.CurrentRow.Tag is AbilityDefinition) rows = new[] { grid.CurrentRow };
   var names = rows.Select(x => ((AbilityDefinition)x.Tag).name).Distinct().ToArray();
   if (names.Length == 0) return;
   SelectedNames = multiple ? names : new[] { names[0] };
   DialogResult = DialogResult.OK;
  }
 }
}
