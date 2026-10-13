using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace AABattle {
 // 편집기 빈칸에 사람이 적은 느슨한 입력을 표준 표기로 바꾸는 공용 함수.
 // 판정·실행에는 관여하지 않는다.
 public static partial class ModuleCatalog {
  static readonly string[][] ConditionAliases = {
   new[] { "타입 불일치", "타입불일치" },
   new[] { "타입 일치", "타입일치" },
   new[] { "효과가 굉장", "효과가굉장" },
   new[] { "효과가 매우 굉장", "효과가굉장" },
   new[] { "효과가 별로", "효과가별로" },
   new[] { "효과가 매우 별로", "효과가별로" },
   new[] { "효과 보통", "효과보통" },
   new[] { "효과가 보통", "효과보통" }
  };

  // 기술 조건 입력(예: 불꽃/얼음 물리 타입 불일치 효과가 굉장 위력 40 이하)을
  // 표준 괄호 토큰(「불꽃/얼음」「물리」…)으로 바꾼다. 읽을 수 없으면 null.
  public static string ConditionFromInput(string text) {
   string raw = (text ?? "").Trim();
   if (raw.Length == 0) return "";
   raw = Regex.Replace(raw, @"우선도\s*\+", "우선도+");
   if (raw.Contains("「")) raw = raw.Replace("「", " ").Replace("」", " ");
   foreach (var pair in ConditionAliases) raw = raw.Replace(pair[0], pair[1]);
   raw = Regex.Replace(raw, @"위력\s*:?\s*(\d+)\s*(이하|이상)", "위력$1$2");
   var words = raw.Split(new[] { ' ', ',', '，' }, StringSplitOptions.RemoveEmptyEntries);
   string bracketed = string.Concat(words.Select(
    x => "「" + Regex.Replace(x, @"^위력(\d+)(이하|이상)$", "위력 $1 $2") + "」"));
   MoveCondition condition;
   return MoveCondition.TryParse(bracketed, out condition) ? condition.Text : null;
  }

  // 읽을 때 먼저 시도하는 엄격한 빈칸 모양: 숫자·기술 조건·선택지 값만 받는다.
  // 인접한 빈칸 사이의 글자를 엉뚱하게 가르는 것(예: 「야생의 두목」의 「의」)을 막는다.
  internal static string StrictSlotPattern(ModuleSlot slot) {
   if (slot.Numeric) return slot.Decimals > 0 ? @"-?[0-9]+(?:\.[0-9]+)?" : @"-?[0-9]+";
   if (slot.Kind == ModuleSlotKind.MoveCondition) return @"(?:「[^」]+」\s*)*";
   if (slot.Choices.Length > 0 && !slot.Multiple)
    return "(?:" + string.Join("|", slot.Choices.OrderByDescending(x => x.Length).Select(Regex.Escape)) + ")";
   return ".*?";
  }

  // 트리거 문장 입력(예: 공격을 받았을 때)을 표준 트리거 문장으로 바꾼다.
  // 모듈로 읽을 수 없으면 입력을 그대로 돌려준다.
  public static string TriggerFromInput(string text) {
   string raw = (text ?? "").Trim();
   if (raw.Length == 0) return "";
   var spec = TriggerModules.Parse(TriggerStructures.Normalize(raw));
   return spec.Module.Length == 0 ? raw : TriggerModules.Canonical(spec);
  }
 }
}
