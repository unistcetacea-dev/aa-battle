using System;
using System.Linq;
using System.Windows.Forms;
using System.Collections.Generic;

namespace AABattle {
 // 포텐셜 표 ↔ 편집 창 연결. 표는 보기 전용이고, 편집은 별도 창에서 사본으로 하다가
 // 「확인」을 누를 때 한 번만 표에 쓴다(입력마다 SaveCurrent·LoadCurrent·트리 갱신을 하지 않는다).
 partial class DataEditorForm {
  PotentialEditWindow editWindow;

  internal PotentialDraft DraftFromRow(DataGridViewRow row) {
   var record = row.Tag as PotentialRecord ?? new PotentialRecord();
   var disabledValue = row.Cells["disabled"].Value;
   return new PotentialDraft {
    Disabled = disabledValue != null && Convert.ToBoolean(disabledValue),
    Slot = Cell(row, "slot"),
    TargetType = Cell(row, "targetType"),
    Name = Cell(row, "name"),
    Unlocks = Cell(row, "unlocks"),
    Trigger = Cell(row, "trigger"),
    Effect = Cell(row, "effect"),
    Raw = Cell(row, "raw"),
    Activation = record.activation ?? "",
    Uses = record.uses ?? "",
    TriggerSubject = record.triggerSubject ?? "",
    EffectSubject = record.effectSubject ?? ""
   };
  }

  // 초안을 한 번에 표 행에 쓴다. 셀을 쓰는 동안은 변경 알림(loading)을 막고, 끝나고 한 번만 저장한다
  void WriteRow(DataGridViewRow row, PotentialDraft d) {
   bool before = loading;
   loading = true;
   try {
    row.Cells["disabled"].Value = d.Disabled;
    row.Cells["slot"].Value = d.Slot;
    row.Cells["targetType"].Value = d.TargetType;
    row.Cells["name"].Value = d.Name;
    row.Cells["unlocks"].Value = d.Unlocks;
    row.Cells["trigger"].Value = d.Trigger;
    row.Cells["effect"].Value = d.Effect;
    row.Cells["raw"].Value = d.Raw;
    var record = row.Tag as PotentialRecord ?? new PotentialRecord();
    record.activation = d.Activation;
    record.uses = d.Uses;
    record.triggerSubject = d.TriggerSubject;
    record.effectSubject = d.EffectSubject;
    record.targetType = d.TargetType;
    row.Tag = record;
   } finally { loading = before; }
  }

  PotentialRecord RecordFromDraft(PotentialDraft d) {
   return new PotentialRecord {
    enabled = !d.Disabled,
    slot = d.Slot,
    targetType = d.TargetType,
    name = d.Name,
    activation = d.Activation,
    uses = d.Uses,
    unlocks = d.Unlocks.Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
     .Select(x => TrainerAccessRules.NormalizeUnlock(x)).Where(x => x.Length > 0).Distinct().ToArray(),
    triggerSubject = d.TriggerSubject,
    effectSubject = d.EffectSubject,
    trigger = TriggerStructures.NormalizeLines(d.Trigger),
    effect = EffectNormalizer.NormalizeLines(d.Effect),
    raw = d.Raw
   };
  }

  // 「포텐셜 종류」 선택지: 포켓몬은 전용까지, 트레이너는 고유·지령(옛 전용은 그 행에서만 남는다)
  internal string[] SlotChoicesFor(bool trainerRecord, string current) {
   var choices = (trainerRecord ? EditorTemplate.TrainerEditSlotNames : EditorTemplate.PokemonEditSlotNames).ToList();
   if (!string.IsNullOrWhiteSpace(current) && !choices.Contains(current)) choices.Add(current);
   return choices.ToArray();
  }

  PotentialEditWindow CreatePotentialWindow(PotentialDraft draft) {
   bool trainerRecord = Current is TrainerRecord;
   var window = new PotentialEditWindow(this, catalogDb, draft, SlotChoicesFor(trainerRecord, draft.Slot), trainerRecord);
   editWindow = window;
   return window;
  }

  // 편집 창을 열어 확인하면 결과 사본을, 취소하면 null을 돌려준다
  PotentialDraft RunPotentialWindow(PotentialDraft draft) {
   using (var window = CreatePotentialWindow(draft)) {
    if (window.ShowDialog(this) != DialogResult.OK) return null;
    return window.Result;
   }
  }

  void EditSelectedPotential() {
   var row = SelectedPotentialRow();
   if (row == null) { GameDialog.Show(this, "표에서 편집할 포텐셜을 먼저 고르세요."); return; }
   EditPotentialRow(row);
  }

  internal void EditPotentialRow(DataGridViewRow row) {
   if (row == null || row.IsNewRow || Current == null) return;
   var before = DraftFromRow(row);
   var result = RunPotentialWindow(before);
   if (result == null) return;
   CommitPotentialRow(row, before, result);
  }

  // 확인: 표에 한 번 쓰고 저장한다. 역할 이름을 바꿨으면 짝이 되는 부수 포텐셜도 맞춘다
  internal void CommitPotentialRow(DataGridViewRow row, PotentialDraft before, PotentialDraft result) {
   result.Trigger = TriggerStructures.NormalizeLines(result.Trigger);
   result.Effect = EffectNormalizer.NormalizeLines(result.Effect);
   WriteRow(row, result);
   LinkAdjunctIfRole(result, before);
   GridChanged();
   RequestResponsiveLayout();
  }

  void LinkAdjunctIfRole(PotentialDraft result, PotentialDraft before) {
   if (result.Slot != "역할" || (before != null && before.Slot == "역할" && before.Name == result.Name)) return;
   var option = EditorTemplate.FixedOption("역할", result.Name);
   if (option == null) return;
   bool before2 = loading;
   loading = true;   // 짝 행을 쓰는 동안은 칸마다 저장하지 않는다(끝나고 한 번만)
   try { SetLinkedAdjunct(option); } finally { loading = before2; }
  }

  // 새 포텐셜: 편집 창에서 확인해야 목록에 들어간다(취소하면 아무것도 만들지 않는다)
  internal void NewPotential() {
   var list = CurrentPotentials();
   if (list == null) { GameDialog.Show(this, "트레이너 또는 포켓몬을 먼저 선택하세요."); return; }
   SaveCurrent();
   var draft = new PotentialDraft { Slot = Current is TrainerRecord ? "고유" : "종족 ①" };
   var result = RunPotentialWindow(draft);
   if (result == null) return;
   AddPotential(RecordFromDraft(result));
   LinkAdjunctIfRole(result, null);
   GridChanged();
  }

  // ---- 편집 창이 쓰는 모듈 화면(트리거·효과 빈칸 채우기) ----
  internal void AttachBuilders(Control host) {
   if (inlineCatalog == null) return;
   inlineCatalog.Dock = DockStyle.Fill;
   inlineCatalog.Visible = true;
   host.Controls.Add(inlineCatalog);
  }

  internal void ReleaseBuilders(PotentialEditWindow window) {
   if (editWindow == window) editWindow = null;
   if (IsDisposed || inlineCatalog == null || tabs == null || tabs.IsDisposed) return;
   var page = tabs.TabPages[5];
   if (inlineCatalog.Parent != page) page.Controls.Add(inlineCatalog);
  }

  // 초안의 트리거·효과 문장과 입력 방식·주체를 모듈 빈칸으로 다시 채운다(읽을 수 없는 문장은 화면 그대로)
  internal void LoadBuilders(PotentialDraft d) {
   if (triggerActivation != null) triggerActivation.SelectedItem = triggerActivation.Items.Contains(d.Activation) ? d.Activation : "자동";
   if (effectSubject != null) effectSubject.SelectedItem = effectSubject.Items.Contains(d.EffectSubject) ? d.EffectSubject : "자신";
   LoadTriggerModule(d.Trigger);
   LoadStructuredEffectSettings(d.Effect, d.EffectSubject);
  }

  // ---- 검사용 접근점 ----
  internal DataGridView PotentialGridForTest { get { return potentials; } }
  internal TreeView TreeForTest { get { return tree; } }
  internal EditorProject ProjectForTest { get { return project; } }
  internal bool DirtyForTest { get { return dirty; } }
  internal PotentialEditWindow ActivePotentialWindow { get { return editWindow; } }
  internal bool BuildersParked { get { return inlineCatalog != null && inlineCatalog.Parent == tabs.TabPages[5]; } }

  // 검사 화면(--render-preview)용: 편집 창을 열어 그림으로 저장하고 버린다. moveInputs면 기술 조건 빈칸을 채워 보인다
  public void RenderPotentialWindow(string path, bool moveInputs) {
   var row = potentials.Rows.Cast<DataGridViewRow>().FirstOrDefault(x => !x.IsNewRow && Cell(x, "name") == "샘플의 전도")
    ?? potentials.Rows.Cast<DataGridViewRow>().First(x => !x.IsNewRow);
   var window = OpenPotentialWindowForTest(DraftFromRow(row));
   if (moveInputs) {
    triggerBuilder.Select("trigger.move.False");
    ((MoveConditionSlotEditor)triggerBuilder.Editor("기술조건")).SetName("몸통박치기");
    effectBuilder.Select("effect.guard.pierce.pokemon", new Dictionary<string, string> { { "조건", "「격투」" } });
    Application.DoEvents();
   }
   using (var bitmap = new System.Drawing.Bitmap(window.Width, window.Height)) {
    window.DrawToBitmap(bitmap, new System.Drawing.Rectangle(System.Drawing.Point.Empty, bitmap.Size));
    bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
   }
   window.Cancel();
   window.Dispose();
  }

  internal PotentialEditWindow OpenPotentialWindowForTest(PotentialDraft draft) {
   var window = CreatePotentialWindow(draft);
   window.Show(this);
   Application.DoEvents();
   return window;
  }
 }
}
