using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle
{
  public static partial class ModuleCatalog
  {
    // 복수 상태는 / 구분 입력을 엔진의 인접 괄호 문장으로 옮긴다.
    static void CoreBracketList(ModuleDefinition module, string slotId)
    {
      var original = module.Formatter;
      var listSlots = module.Slots;
      listSlots.First(x => x.Id == slotId).Multiple = true;
      Func<IDictionary<string, string>, string> listFormatter = values =>
      {
        var resolved = new Dictionary<string, string>(values);
        resolved[slotId] = string.Join("」「", values[slotId].Split('/'));
        return original(resolved);
      };
      module.Formatter = listFormatter;
      string pattern = Regex.Escape(module.Template);
      foreach (var slot in listSlots)
        pattern = pattern.Replace(Regex.Escape("{" + slot.Id + "}"), slot.Id == slotId ? "(?<" + slot.Id + ">.+?)" : "(?<" + slot.Id + ">.*?)");
      pattern = pattern.Replace(@"\ ", @"\s*");
      var regex = new Regex("^" + pattern + "$");
      module.Reader = text =>
      {
        var match = regex.Match(text ?? "");
        if (!match.Success) return null;
        var values = listSlots.ToDictionary(x => x.Id, x => match.Groups[x.Id].Value.Trim());
        values[slotId] = values[slotId].Replace("」「", "/");
        return listFormatter(values) == text ? values : null;
      };
    }
  }
}


