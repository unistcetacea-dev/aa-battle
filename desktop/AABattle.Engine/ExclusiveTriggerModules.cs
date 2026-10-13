using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AABattle
{
  // 오레마스 전용 유형에서 쓰는 통상교대 등장 턴의 최후 트리거.
  public static class ExclusiveTriggerModules
  {
    const string NormalSwitchInner = "통상교대로 장소에 나왔을 때";
    public const string NormalSwitchLastSentence = "통상교대로 장소에 나온 T의 최후";

    public static bool TryParse(string text, out TriggerSpec spec)
    {
      spec = null;
      if (!string.Equals((text ?? "").Trim().TrimEnd('.', '。', '．').Trim(), NormalSwitchLastSentence, StringComparison.Ordinal))
        return false;
      spec = new TriggerSpec
      {
        Module = "타이밍",
        Method = TriggerStructures.TurnLastLabel,
        Inner = NormalSwitchInner
      };
      return true;
    }

    public static bool IsNormalSwitchLast(TriggerSpec spec)
    {
      return spec != null && spec.Module == "타이밍" && spec.Method == TriggerStructures.TurnLastLabel
        && spec.Inner == NormalSwitchInner;
    }

    public static bool MatchesNormalSwitchLast(TriggerFact fact, Fighter owner)
    {
      return fact != null && fact.Kind == "타이밍" && fact.Method == TriggerStructures.TurnLastLabel
        && owner != null && (owner.Conditions.TurnFacts ?? new List<TriggerFact>()).Any(x =>
          x != null && x.Kind == "등장" && x.Self && x.Method == "통상교대");
    }
  }

  public static partial class ModuleCatalog
  {
    static void RegisterExclusiveTriggers(List<ModuleDefinition> modules)
    {
      TF(modules, "exclusive.normal-switch-last", "타이밍", "통상교대 등장 턴의 최후", "턴·행동",
        ExclusiveTriggerModules.NormalSwitchLastSentence);
    }

    static void RegisterExclusiveEffects(List<ModuleDefinition> modules)
    {
      CoreAdd(modules, "exclusive.berserk.attack", "버서크 추가공격", "버서크 추가공격", "행동",
        "이때 가장 먼저 같은 기술에 의한 추가공격을 한다.");
    }

  }
}
