using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle
{
  public static class TurnConditions
  {
    internal static bool TryParse(string text, out TriggerSpec spec)
    {
      spec = null;
      var fieldTurn = Regex.Match(text ?? "", @"^(?:장소|필드)에\s*나와\s*「(?<n>\d+)」\s*T째일\s*때$");
      if (fieldTurn.Success)
      {
        spec = new TriggerSpec { Module = "턴 조건", FieldTurn = int.Parse(fieldTurn.Groups["n"].Value) };
        return spec.FieldTurn > 0;
      }

      var every = Regex.Match(text ?? "", @"^「(?<n>\d+)」\s*배수의\s*T일\s*때$");
      if (!every.Success)
        return false;
      spec = new TriggerSpec { Module = "턴 조건", Every = int.Parse(every.Groups["n"].Value) };
      return spec.Every > 0;
    }

    internal static string Canonical(TriggerSpec spec)
    {
      return spec.FieldTurn > 0
        ? "장소에 나와 「" + spec.FieldTurn + "」T째일 때"
        : "「" + spec.Every + "」배수의 T일 때";
    }

    internal static bool Holds(TriggerSpec spec, Fighter owner, Rules rules)
    {
      if (spec.FieldTurn > 0)
        return owner != null && owner.OnField && owner.HP > 0 && owner.Conditions.FieldTurn == spec.FieldTurn;
      return rules != null && rules.Turn > 0 && rules.Turn % spec.Every == 0;
    }
  }

  public static partial class ModuleCatalog
  {
    static void RegisterTurnConditions(List<ModuleDefinition> modules)
    {
      foreach (bool fieldTurn in new[] { true, false })
      {
        bool isFieldTurn = fieldTurn;
        string id = isFieldTurn ? "field" : "multiple";
        string template = isFieldTurn ? "장소에 나와 「{턴}」T째일 때" : "「{배수}」배수의 T일 때";
        var slot = S(isFieldTurn ? "턴" : "배수", ModuleSlotKind.Turns, "2");
        T(modules, "turn.condition." + id, "턴 조건", isFieldTurn ? "등장 후 T 조건" : "배수 T 조건", "턴·행동", template,
          () => new TriggerSpec { Module = "턴 조건", FieldTurn = isFieldTurn ? 2 : 0, Every = isFieldTurn ? 0 : 2 },
          TB(slot, (s, v) =>
          {
            int value = int.Parse(v);
            if (isFieldTurn) s.FieldTurn = value; else s.Every = value;
          }, s => (isFieldTurn ? s.FieldTurn : s.Every).ToString()));
      }
    }
  }
}
