using System;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 // 편집기 트리거 구조: 모듈(등장·교대·퇴장·타이밍·피격·명중·급소·쓰러뜨림·기술 사용) → 주체 → 구분·값.
 // 문장은 엔진의 TriggerModules.Canonical이 만들고, 불러올 때는 TriggerModules.Parse로 값을 되돌린다
 partial class DataEditorForm {
  PixelText triggerPreview;
  PixelComboBox triggerKind, triggerSubject, triggerEntryMethod, triggerType, triggerAptitude, triggerRank, triggerActivation;
  PixelTextBox triggerGranted, triggerCondition;
  PixelNumber triggerNumber;
  FlatButton triggerStatusButton;
  string[] triggerStatusNames = new string[0];
  TableLayoutPanel triggerTable;

  const string AlwaysKind = "상시 (X)";
  static readonly string[] SubjectModules = { "등장", "교대", "퇴장", "기술 사용", "체력", "체력 변화", "회복", "랭크" };
  // 상태 모듈은 상태 도감 선택(상태이상·상태변화)으로 입력한다
  static readonly string[] StatusModules = { "상태", "상태 됨", "상태 해제" };
  const string ReleaseKind = "상태가 해제되었을 때", FaintKind = "빈사가 되었을 때";
  static string[] UiModules() { return TriggerModules.EventModules.Concat(TriggerModules.StateModules).Where(x => !StatusModules.Contains(x)).Distinct().ToArray(); }

  static string[] TriggerKinds() {
   return new[] { AlwaysKind, TriggerStructures.FieldLabel, TriggerStructures.ObserverLabel }
    .Concat(UiModules())
    .Concat(new[] {
     TriggerStructures.ReceivedOrNullifiedLabel, TriggerStructures.DamagedLabel, "적진 타입 포켓몬", "트레이너 자질 등급", "XX가 부여되어 있을 때",
     "상태이상일 때", "상태이상이 되었을 때", "상태변화일 때", "상태변화가 되었을 때", ReleaseKind, FaintKind,
     TriggerStructures.HereLabel, TriggerStructures.SelfOrderLabel, TriggerStructures.AllyOrderLabel, TriggerStructures.AnalyzedLabel, TriggerStructures.BattleStartLabel
    }).ToArray();
  }

  // 모듈별 「구분」 선택지
  static string[] TriggerVariants(string module, string subject) {
   switch (module) {
    case "등장": return subject == "자신" ? new[] { "모든 등장" }.Concat(TriggerModules.EntryMethods).Concat(new[] { "타입 아군과 교대해" }).ToArray() : new[] { "모든 등장" }.Concat(TriggerModules.EntryMethods).ToArray();
    case "교대": return subject == "상대" ? new[] { "모든 교대", "임의교대" } : new[] { "임의교대", "타입 아군과 교대" };
    case "타이밍": return new[] { TriggerStructures.TurnStartLabel, TriggerStructures.TurnLastLabel, TriggerStructures.TurnEndLabel, "배수 T 개시 시", "배수 T 종료 시", "장소에 나와 N T째 종료 시", "사건이 성립한 T 종료 시" };
    case "피격": case "명중": return new[] { "공격", "기술" };
    case "기술 사용": return new[] { "내보낼 때(직전)", "내보냈을 때(직후)" };
    case "쓰러뜨림": return new[] { "상대", "타입 포켓몬", "아인종", "기술로" };
    case "체력": return new[] { "가득", "가득하지 않음", "1/N 이하", "1/N 이상" };
    case "체력 변화": return new[] { "1/N 이하가 됨", "가득하지 않게 됨", "「1」이 됨", "감소", "가득해짐" };
    case "날씨": return new[] { "날씨 있음" }.Concat(Mechanics.Weathers.Where(x => x != "없음")).ToArray();
    case "날씨 변화": return new[] { "모든 변화" }.Concat(Mechanics.Weathers.Where(x => x != "없음")).ToArray();
    case "장소": return EffectModules.TerrainNames.Concat(new[] { "트릭룸", "중력" }).ToArray();
    case "장소 변화": return EffectModules.TerrainNames.Concat(new[] { "트릭룸 전개", "트릭룸 해제", "중력 전개", "중력 해제" }).ToArray();
    case "랭크": return new[] { "오름", "크게 오름", "내려감" };
   }
   return new string[0];
  }

  Panel BuildStructuredTriggerPanel() {
   var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
   var header = Theme.Label("트리거 구조 · 원문 없이 값으로 구성", 40);
   var help = Theme.Label("구조를 고르면 그 조건에 필요한 값만 표시합니다.", 58);
   help.ForeColor = Theme.Muted;
   triggerKind = Theme.Combo(TriggerKinds(), AlwaysKind);
   triggerSubject = Theme.Combo(new[] { "자신", "자신(상대)", "아군", "상대" }, "자신");
   triggerEntryMethod = Theme.Combo(new[] { "모든 등장" }, "모든 등장");
   triggerType = Theme.Combo(catalogDb.types, "불꽃");
   triggerNumber = new PixelNumber { Minimum = 1, Maximum = 99, Value = 2, Dock = DockStyle.Fill, Font = Theme.Input(), BorderStyle = BorderStyle.FixedSingle };
   triggerCondition = new PixelTextBox { Dock = DockStyle.Fill, Font = Theme.Input() };
   triggerAptitude = Theme.Combo(new[] { "지시", "육성", "통솔", "능력" }, "통솔");
   triggerRank = Theme.Combo(new[] { "E", "D", "C", "B", "A", "AA", "AAA", "S" }, "A");
   triggerGranted = new PixelTextBox { Dock = DockStyle.Fill, Font = Theme.Input(), Text = "계약의 특권" };
   triggerStatusButton = new FlatButton { Dock = DockStyle.Fill, Font = Theme.UI(8), Text = "상태 선택 · 검색/토글" };
   triggerStatusButton.Click += (s, e) => PickTriggerStatuses();
   triggerPreview = new PixelText { Dock = DockStyle.Fill };
   triggerActivation = Theme.Combo(new[] { "자동", "선행" }, "자동");
   triggerTable = Theme.Table();
   Theme.Row(triggerTable, "구조", triggerKind);
   Theme.Row(triggerTable, "주체", triggerSubject);
   Theme.Row(triggerTable, "구분", triggerEntryMethod);
   Theme.Row(triggerTable, "타입", triggerType);
   Theme.Row(triggerTable, "수치", triggerNumber);
   Theme.Row(triggerTable, "기술 조건", triggerCondition);
   Theme.Row(triggerTable, "부여된 이름", triggerGranted);
   Theme.Row(triggerTable, "상태 이름", triggerStatusButton);
   Theme.Row(triggerTable, "트레이너 자질", triggerAptitude);
   Theme.Row(triggerTable, "자질 등급", triggerRank);
   Theme.Row(triggerTable, "결과", triggerPreview);
   Theme.Row(triggerTable, "입력 방식", triggerActivation);
   var apply = Button("구조 트리거 입력", 220, () => ApplyPart(true));
   apply.Dock = DockStyle.Bottom;
   panel.Controls.Add(triggerTable); panel.Controls.Add(help); panel.Controls.Add(header); panel.Controls.Add(apply);
   EventHandler refresh = (s, e) => RefreshStructuredTrigger();
   triggerKind.SelectedIndexChanged += refresh; triggerSubject.SelectedIndexChanged += refresh; triggerEntryMethod.SelectedIndexChanged += refresh;
   triggerType.SelectedIndexChanged += refresh; triggerNumber.ValueChanged += refresh; triggerCondition.TextChanged += refresh;
   triggerGranted.TextChanged += refresh; triggerAptitude.SelectedIndexChanged += refresh; triggerRank.SelectedIndexChanged += refresh;
   RefreshStructuredTrigger();
   return panel;
  }

  void ShowTriggerRow(Control control, bool shown, string labelText = null) {
   if (triggerTable == null || control == null) return;
   int row = triggerTable.GetRow(control);
   if (row < 0) return;
   triggerTable.RowStyles[row].Height = shown ? 46 : 0;
   control.Visible = shown;
   var label = triggerTable.GetControlFromPosition(0, row);
   if (label != null) { label.Visible = shown; if (labelText != null) label.Text = labelText; }
  }
  bool IsStatusKind(string kind) { return (kind ?? "").StartsWith("상태이상") || (kind ?? "").StartsWith("상태변화") || kind == ReleaseKind; }
  void PickTriggerStatuses() {
   bool change = (triggerKind.Text ?? "").StartsWith("상태변화"), release = triggerKind.Text == ReleaseKind;
   string category = release ? "상태" : change ? "상태변화" : "상태이상";
   var names = release ? StateDex.StatusNames("상태이상").Concat(StateDex.StatusNames("상태변화")).Distinct().ToArray() : StateDex.StatusNames(category);
   using (var f = new SearchToggleForm(category + " 선택 · 검색/토글", names, triggerStatusNames, 0))
    if (f.ShowDialog(this) == DialogResult.OK) { triggerStatusNames = f.Selected; RefreshStructuredTrigger(); }
  }
  static void SetChoices(PixelComboBox combo, string[] choices, string preferred = null) {
   if (combo.Items.Cast<object>().Select(x => x.ToString()).SequenceEqual(choices)) { if (preferred != null && choices.Contains(preferred)) combo.SelectedItem = preferred; return; }
   string current = preferred ?? combo.Text;
   combo.Items.Clear(); combo.Items.AddRange(choices.Cast<object>().ToArray());
   if (choices.Length > 0) combo.SelectedItem = choices.Contains(current) ? current : choices[0];
  }

  bool refreshingTrigger;
  void RefreshStructuredTrigger() {
   if (triggerPreview == null || refreshingTrigger) return;
   refreshingTrigger = true;
   try {
    string kind = triggerKind.Text;
    bool module = UiModules().Contains(kind);
    bool faint = kind == FaintKind;
    bool status = IsStatusKind(kind), granted = kind == "XX가 부여되어 있을 때", aptitude = kind == "트레이너 자질 등급", opponentType = kind == "적진 타입 포켓몬";
    if (module) {
     if (SubjectModules.Contains(kind)) SetChoices(triggerSubject, kind == "등장" ? new[] { "자신", "상대", "자신(상대)" } : new[] { "자신", "상대" });
     SetChoices(triggerEntryMethod, TriggerVariants(kind, triggerSubject.Text));
    } else if (faint) SetChoices(triggerSubject, new[] { "자신", "상대" });
    else SetChoices(triggerSubject, new[] { "자신", "자신(상대)", "아군", "상대" });
    string variant = triggerEntryMethod.Text;
    bool typed = module && variant.Contains("타입") || opponentType;
    bool number = kind == "타이밍" && (variant.StartsWith("배수") || variant.Contains("N T째")) || variant.StartsWith("1/N");
    bool condition = kind == "피격" || kind == "명중" || kind == "급소" || kind == "기술 사용" || kind == "쓰러뜨림" && variant == "기술로" || kind == "타이밍" && variant.StartsWith("사건") || kind == "랭크";
    ShowTriggerRow(triggerSubject, module && SubjectModules.Contains(kind) || status || faint, "주체");
    ShowTriggerRow(triggerEntryMethod, module && TriggerVariants(kind, triggerSubject.Text).Length > 0, kind == "등장" ? "등장 방식" : kind == "타이밍" ? "타이밍" : kind == "기술 사용" ? "시점" : "구분");
    ShowTriggerRow(triggerType, typed);
    ShowTriggerRow(triggerNumber, number, variant.StartsWith("배수") ? "배수 N" : variant.StartsWith("1/N") ? "1/N의 N" : "N T째");
    ShowTriggerRow(triggerCondition, condition, kind == "타이밍" ? "사건 트리거" : kind == "랭크" ? "능력(비우면 능력 전체)" : "기술 조건");
    ShowTriggerRow(triggerGranted, granted, "부여된 이름");
    ShowTriggerRow(triggerStatusButton, status, "상태 이름");
    ShowTriggerRow(triggerAptitude, aptitude);
    ShowTriggerRow(triggerRank, aptitude);
    if (status) {
     bool change = kind.StartsWith("상태변화");
     var allowed = StateDex.StatusNames(change ? "상태변화" : "상태이상");
     triggerStatusNames = (triggerStatusNames ?? new string[0]).Where(x => allowed.Contains(x)).ToArray();
     triggerStatusButton.Text = triggerStatusNames.Length == 0 ? "상태 선택 · 검색/토글" : "선택: " + string.Join(", ", triggerStatusNames);
    } else triggerStatusButton.Text = "상태 선택 · 검색/토글";
    if (kind == AlwaysKind) triggerPreview.Text = TriggerStructures.AlwaysLabel;
    else if (module) triggerPreview.Text = ModuleTriggerText(kind, variant);
    else if (opponentType) triggerPreview.Text = TriggerStructures.OpponentType(triggerType.Text);
    else if (faint) triggerPreview.Text = TriggerModules.Canonical(new TriggerSpec { Module = "상태 됨", Subject = triggerSubject.Text, Names = new[] { "빈사" } });
    else if (status && kind == ReleaseKind) triggerPreview.Text = triggerStatusNames.Length == 0 ? "상태를 검색·토글로 선택하세요." : TriggerModules.Canonical(new TriggerSpec { Module = "상태 해제", Subject = triggerSubject.Text == "상대" ? "상대" : "자신", Names = triggerStatusNames });
    else if (status) triggerPreview.Text = triggerStatusNames.Length == 0 ? "상태를 검색·토글로 선택하세요." : string.Join("\r\n", triggerStatusNames.Select(x => TriggerStructures.StatusCondition(triggerSubject.Text, x, kind.Contains("되었을 때"), kind.StartsWith("상태변화"))));
    else if (granted) triggerPreview.Text = TriggerStructures.Granted(triggerGranted.Text.Length > 0 ? triggerGranted.Text : "이름 미지정");
    else if (aptitude) triggerPreview.Text = TriggerStructures.TrainerAptitude(triggerAptitude.Text, triggerRank.Text);
    else triggerPreview.Text = kind;
   } finally { refreshingTrigger = false; }
  }

  // 모듈 선택값 → 표준 트리거 문장(엔진 TriggerModules.Canonical)
  string ModuleTriggerText(string kind, string variant) {
   var spec = new TriggerSpec { Module = kind, Opponent = triggerSubject.Text == "상대", Subject = triggerSubject.Text == "상대" ? "상대" : "자신" };
   int n = (int)triggerNumber.Value;
   switch (kind) {
    case "체력":
     spec.Op = variant == "가득" ? "가득" : variant == "가득하지 않음" ? "가득 아님" : variant.EndsWith("이상") ? "이상" : "이하"; spec.Bottom = Math.Max(1, n);
     return TriggerModules.Canonical(spec);
    case "체력 변화":
     spec.Op = variant.StartsWith("1/N") ? "이하" : variant.StartsWith("가득하지") ? "가득 아님" : variant.StartsWith("「1」") ? "1" : variant == "감소" ? "감소" : "가득"; spec.Bottom = Math.Max(1, n);
     return TriggerModules.Canonical(spec);
    case "회복": return TriggerModules.Canonical(spec);
    case "날씨": case "날씨 변화": spec.Place = variant == "날씨 있음" || variant == "모든 변화" ? "" : variant; return TriggerModules.Canonical(spec);
    case "장소": spec.Place = variant; return TriggerModules.Canonical(spec);
    case "장소 변화": spec.Place = variant.Split(' ')[0]; spec.Release = variant.EndsWith("해제"); return TriggerModules.Canonical(spec);
    case "랭크": {
     var stats = TriggerModules.Parse(TriggerStructures.Normalize("자신의 「" + Regex.Replace(triggerCondition.Text.Trim(), @"[\s,]+", "/") + "」이 올랐을 때"));
     if (triggerCondition.Text.Trim().Length > 0 && stats.Module != "랭크") return "능력 이름을 읽을 수 없습니다(예: 공격 특공).";
     spec.Stats = triggerCondition.Text.Trim().Length == 0 ? new int[0] : stats.Stats; spec.Rise = variant != "내려감"; spec.Big = variant == "크게 오름";
     return TriggerModules.Canonical(spec);
    }
   }
   string tokens = ConditionTokens(triggerCondition.Text);
   MoveCondition condition = new MoveCondition();
   if (kind != "타이밍" && tokens == null) return "기술 조건을 읽을 수 없습니다.";
   if (kind != "타이밍") MoveCondition.TryParse(tokens ?? "", out condition);
   switch (kind) {
    case "등장":
     if (triggerSubject.Text == "자신(상대)") return variant == "모든 등장" ? TriggerStructures.SelfAndOpponentEntryLabel : "자신(상대)는 「모든 등장」만 고를 수 있습니다.";
     if (variant.Contains("타입")) spec.Types = new[] { triggerType.Text }; else if (variant != "모든 등장") spec.Method = variant;
     break;
    case "교대":
     if (variant.Contains("타입")) spec.Types = new[] { triggerType.Text };
     spec.Method = variant == "모든 교대" ? "" : "임의";
     break;
    case "타이밍":
     if (variant.StartsWith("사건")) {
      var inner = TriggerModules.Parse(TriggerStructures.Normalize(triggerCondition.Text));
      if (!inner.IsEvent || inner.Module == "타이밍") return "사건 트리거 문장을 입력하세요(예: 상대의 공격을 받았을 때).";
      spec.Method = TriggerStructures.TurnEndLabel; spec.Inner = TriggerModules.Canonical(inner);
     } else if (variant.Contains("N T째")) { spec.Method = TriggerStructures.TurnEndLabel; spec.FieldTurn = n; }
     else if (variant.StartsWith("배수")) { spec.Method = variant.Contains("개시") ? TriggerStructures.TurnStartLabel : TriggerStructures.TurnEndLabel; spec.Every = n; }
     else spec.Method = variant;
     break;
    case "피격": case "명중":
     spec.Attack = variant == "공격";
     if (condition.Category == "공격") { spec.Attack = true; condition.Category = ""; }
     spec.Condition = condition;
     break;
    case "급소": spec.Condition = condition; break;
    case "기술 사용": spec.After = variant.Contains("직후"); spec.Condition = condition; break;
    case "쓰러뜨림":
     if (variant == "아인종") spec.Ain = true;
     else if (variant == "타입 포켓몬") spec.Types = new[] { triggerType.Text };
     else if (variant == "기술로") spec.Condition = condition;
     break;
   }
   return TriggerModules.Canonical(spec);
  }

  string ConfiguredTrigger() { return triggerPreview == null ? "" : triggerPreview.Text; }

  // 저장된 트리거 첫 줄을 모듈 값으로 되돌린다. 모듈이 아니면 false
  bool LoadTriggerModule(string line) {
   var spec = TriggerModules.Parse(TriggerStructures.Normalize(line));
   if (spec.Module.Length == 0) return false;
   if (StatusModules.Contains(spec.Module)) {
    bool change = spec.Names.All(x => !Mechanics.Statuses.Contains(x) && x != "상태이상");
    string kind = spec.Module == "상태 해제" ? ReleaseKind : spec.Names.Contains("빈사") ? FaintKind : spec.Module == "상태" ? (change ? "상태변화일 때" : "상태이상일 때") : (change ? "상태변화가 되었을 때" : "상태이상이 되었을 때");
    triggerKind.SelectedItem = kind;
    triggerSubject.SelectedItem = triggerSubject.Items.Contains(spec.Subject) ? spec.Subject : "자신";
    triggerStatusNames = spec.Names.Where(x => x != "빈사").ToArray();
    return true;
   }
   triggerKind.SelectedItem = spec.Module;
   if (SubjectModules.Contains(spec.Module)) triggerSubject.SelectedItem = spec.Opponent ? "상대" : "자신";
   string variant = "";
   string condition = spec.Condition.Empty ? "" : Regex.Replace(spec.Condition.Text, "」「", " ").Trim('「', '」');
   switch (spec.Module) {
    case "등장": variant = spec.Types.Length > 0 ? "타입 아군과 교대해" : spec.Method.Length > 0 ? spec.Method : "모든 등장"; break;
    case "교대": variant = spec.Types.Length > 0 ? "타입 아군과 교대" : spec.Opponent && spec.Method == "" ? "모든 교대" : "임의교대"; break;
    case "타이밍":
     if (spec.Inner.Length > 0) { variant = "사건이 성립한 T 종료 시"; condition = spec.Inner; }
     else if (spec.FieldTurn > 0) { variant = "장소에 나와 N T째 종료 시"; triggerNumber.Value = spec.FieldTurn; }
     else if (spec.Every > 0) { variant = spec.Method == TriggerStructures.TurnStartLabel ? "배수 T 개시 시" : "배수 T 종료 시"; triggerNumber.Value = spec.Every; }
     else variant = spec.Method;
     break;
    case "피격": case "명중": variant = spec.Attack ? "공격" : "기술"; break;
    case "기술 사용": variant = spec.After ? "내보냈을 때(직후)" : "내보낼 때(직전)"; break;
    case "쓰러뜨림": variant = spec.Ain ? "아인종" : spec.Types.Length > 0 ? "타입 포켓몬" : spec.Condition.Empty ? "상대" : "기술로"; break;
    case "체력": variant = spec.Op == "가득" ? "가득" : spec.Op == "가득 아님" ? "가득하지 않음" : spec.Op == "이상" ? "1/N 이상" : "1/N 이하"; triggerNumber.Value = Math.Max(1, spec.Bottom); break;
    case "체력 변화": variant = spec.Op == "이하" ? "1/N 이하가 됨" : spec.Op == "가득 아님" ? "가득하지 않게 됨" : spec.Op == "1" ? "「1」이 됨" : spec.Op == "감소" ? "감소" : "가득해짐"; triggerNumber.Value = Math.Max(1, spec.Bottom); break;
    case "날씨": variant = spec.Place.Length == 0 ? "날씨 있음" : spec.Place; break;
    case "날씨 변화": variant = spec.Place.Length == 0 ? "모든 변화" : spec.Place; break;
    case "장소": variant = spec.Place; break;
    case "장소 변화": variant = spec.Place + (spec.Place == "트릭룸" || spec.Place == "중력" ? (spec.Release ? " 해제" : " 전개") : ""); break;
    case "랭크": variant = spec.Rise ? (spec.Big ? "크게 오름" : "오름") : "내려감"; condition = string.Join(" ", spec.Stats.Select(i => new[] { "", "공격", "방어", "특공", "특방", "속도", "명중", "회피" }[i])); break;
   }
   SetChoices(triggerEntryMethod, TriggerVariants(spec.Module, triggerSubject.Text), variant);
   if (spec.Types.Length > 0 && triggerType.Items.Contains(spec.Types[0])) triggerType.SelectedItem = spec.Types[0];
   triggerCondition.Text = condition;
   return true;
  }
 }
}
