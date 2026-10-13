using System;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;

namespace AABattle {
 // 트리거 모듈 입력 화면. 엔진 모듈 목록(TriggerEvent·TriggerState·TriggerLabel)의 문장 틀 빈칸을 채운다.
 // 상태 모듈에서 상태를 여럿 고르면 상태마다 한 줄씩 문장을 낸다(기존 편집기와 같은 저장 형식).
 sealed class TriggerModulePanel : ModuleBuilderPanel {
  public TriggerModulePanel(Database db)
   : base(new[] { ModuleKind.TriggerEvent, ModuleKind.TriggerState, ModuleKind.TriggerLabel }, db, null) { }

  static bool IsStateModule(ModuleDefinition module) {
   return module.Id.StartsWith("trigger.state.", StringComparison.Ordinal);
  }

  protected override string Compose(ModuleDefinition target, Dictionary<string, string> values) {
   if (!IsStateModule(target)) return base.Compose(target, values);
   var names = values["이름"].Split('/').Select(x => x.Trim()).Where(x => x.Length > 0).Distinct().ToArray();
   if (names.Length <= 1) return base.Compose(target, values);
   var lines = new List<string>();
   foreach (string name in names) {
    var single = new Dictionary<string, string>(values);
    single["이름"] = name;
    lines.Add(target.Fill(single));
   }
   return string.Join("\r\n", lines);
  }

  protected override bool TryReadModule(string text, out ModuleDefinition found, out Dictionary<string, string> values) {
   var lines = (text ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
    .Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
   found = null; values = null;
   if (lines.Length == 0) return false;
   if (lines.Length == 1) return ReadLine(lines[0], out found, out values);
   // 두 줄 등장 문장처럼 여러 줄이 하나의 모듈인 경우
   string whole = TriggerStructures.NormalizeLines(string.Join("\r\n", lines));
   if (base.TryReadModule(whole, out found, out values)) return true;
   if (ReadStateLines(lines, out found, out values)) return true;
   // 그 밖의 여러 줄은 첫 줄만 연다(기존 편집기와 같다)
   return ReadLine(lines[0], out found, out values);
  }

  bool ReadStateLines(string[] lines, out ModuleDefinition found, out Dictionary<string, string> values) {
   found = null; values = null;
   var names = new List<string>();
   foreach (string line in lines) {
    ModuleDefinition module;
    Dictionary<string, string> read;
    if (!ReadLine(line, out module, out read) || !IsStateModule(module)) return false;
    if (found == null) { found = module; values = read; }
    else if (module != found || !SameExceptName(values, read)) return false;
    names.AddRange(read["이름"].Split('/'));
   }
   values["이름"] = string.Join("/", names.Distinct());
   return true;
  }

  static bool SameExceptName(Dictionary<string, string> a, Dictionary<string, string> b) {
   return a.Where(x => x.Key != "이름").All(x => b.ContainsKey(x.Key) && b[x.Key] == x.Value);
  }

  bool ReadLine(string line, out ModuleDefinition found, out Dictionary<string, string> values) {
   var candidates = new List<string> { line, TriggerStructures.Normalize(line) };
   string legacy = LegacySentence(line);
   if (legacy != null) candidates.Add(legacy);
   foreach (string candidate in candidates.Distinct())
    if (base.TryReadModule(candidate, out found, out values)) return true;
   found = null; values = null;
   return false;
  }

  // 모듈 표기로 바뀌기 전의 구조 문장(상태 조건·부여·적진 타입·트레이너 자질)을 표준 문장으로 옮긴다
  static string LegacySentence(string line) {
   string subject, name, type, aptitude, rank;
   bool became, change;
   if (TriggerStructures.TryStatusCondition(line, out subject, out name, out became, out change))
    return TriggerStructures.StatusCondition(subject, name, became, change);
   if (TriggerStructures.TryGranted(line, out name)) return TriggerStructures.Granted(name);
   if (TriggerStructures.TryOpponentType(line, out type)) return TriggerStructures.OpponentType(type);
   if (TriggerStructures.TryTrainerAptitude(line, out aptitude, out rank)) return TriggerStructures.TrainerAptitude(aptitude, rank);
   return null;
  }
 }

 partial class DataEditorForm {
  TriggerModulePanel triggerBuilder;
  PixelText triggerPreview;
  PixelComboBox triggerActivation;

  Panel BuildStructuredTriggerPanel() {
   var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
   var header = Theme.Label("트리거 · 문장 틀의 빈칸 채우기", 40);
   var help = Theme.Label("분류 → 모듈 → 빈칸 순서로 채웁니다.", 58);
   help.ForeColor = Theme.Muted;
   triggerBuilder = new TriggerModulePanel(catalogDb);
   triggerPreview = triggerBuilder.Preview;
   triggerActivation = Theme.Combo(new[] { "자동", "선행" }, "자동");
   var options = Theme.Table();
   Theme.Row(options, "입력 방식", triggerActivation);
   var apply = Button("구조 트리거 입력", 220, () => ApplyPart(true));
   apply.Dock = DockStyle.Bottom;
   panel.Controls.Add(triggerBuilder);
   panel.Controls.Add(options);
   panel.Controls.Add(help);
   panel.Controls.Add(header);
   panel.Controls.Add(apply);
   return panel;
  }

  // 화면에 만들어진 트리거 문장(여러 줄일 수 있다). 빈칸이 모자라면 빈 문자열
  string ConfiguredTrigger() { return triggerBuilder == null ? "" : triggerBuilder.Result; }

  // 포텐셜 행에 저장할 「주체」 표시값(모듈에 주체 빈칸이 있을 때만)
  string TriggerSubjectText() {
   return triggerBuilder == null ? "" : triggerBuilder.GetValue("주체") ?? "";
  }

  // 저장된 트리거 문장을 모듈 빈칸으로 되돌린다. 읽을 수 없으면 false(화면은 그대로)
  bool LoadTriggerModule(string text) {
   return triggerBuilder != null && triggerBuilder.Load(text);
  }
 }
}
