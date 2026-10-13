using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 public static partial class ModuleCatalog {
  // 확정 목록(ConfirmedTriggerCatalog)의 인자 자리: 「…」·『…』 안, 숫자·분수, 「조건 A/B」의 A/B
  static readonly Regex LabelArgument = new Regex(
   @"(?<=「)[^」]+(?=」)|(?<=『)[^』]+(?=』)|[+-]?\d+(?:/\d+)?|(?<=조건 )[AB]");

  // 트리거 모듈로 읽히지 않는 문장은 문장 틀 모듈로 등록해 편집기에서 입력할 수 있게 한다.
  // 이 모듈들은 엔진 자동 실행을 뜻하지 않는다(TriggerLabel).
  static void RegisterTriggerLabels(List<ModuleDefinition> modules) {
   modules.Add(new ModuleDefinition("trigger.entry.both", "자신과 상대의 등장 (두 줄)", ModuleKind.TriggerLabel,
    "등장·교대", TriggerStructures.SelfAndOpponentEntryLabel) { Expected = "" });
   var existing = modules.Where(x => x.Kind != ModuleKind.Effect).ToArray();
   for (int index = 0; index < ConfirmedTriggerCatalog.Items.Length; index++) {
    string item = ConfirmedTriggerCatalog.Items[index];
    Dictionary<string, string> ignored;
    if (existing.Any(x => x.TryRead(item, out ignored))) continue;
    modules.Add(CreateLabelModule(index, item));
   }
  }

  static ModuleDefinition CreateLabelModule(int index, string item) {
   var slots = new List<ModuleSlot>();
   string template = "";
   int position = 0;
   foreach (Match match in LabelArgument.Matches(item)) {
    string id = "값" + (slots.Count + 1);
    slots.Add(S(id, ModuleSlotKind.Text, match.Value));
    template += item.Substring(position, match.Index - position) + "{" + id + "}";
    position = match.Index + match.Length;
   }
   template += item.Substring(position);
   return new ModuleDefinition("trigger.label." + index, item, ModuleKind.TriggerLabel,
    "확정 목록 문장", template, slots.ToArray()) { Expected = "" };
  }
 }
}
