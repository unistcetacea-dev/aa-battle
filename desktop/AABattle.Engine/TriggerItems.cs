using System.Text.RegularExpressions;

namespace AABattle {
 public static partial class TriggerModules {
  static bool TryParseItemLoss(string text, out TriggerSpec spec) {
   spec = null;
   var m = Regex.Match(text, @"^(?<s>자신|상대)의 소지품이 (?<m>소모\(파괴\)|소모|소비|파괴)(?:됐을|되었을) 때$");
   if (!m.Success) return false;
   string method = m.Groups["m"].Value;
   spec = new TriggerSpec { Module = "소지품 소실", Opponent = m.Groups["s"].Value == "상대",
    Method = method == "소모(파괴)" ? "소모 또는 파괴" : method == "파괴" ? "파괴" : "소모" };
   return true;
  }
  static string CanonicalItemLoss(TriggerSpec spec) {
   string method = spec.Method == "소모 또는 파괴" ? "소모(파괴)" : spec.Method == "파괴" ? "파괴" : "소모";
   return (spec.Opponent ? "상대" : "자신") + "의 소지품이 " + method + "됐을 때";
  }
  static bool MatchesItemLoss(TriggerSpec spec, TriggerFact fact) {
   return fact.Kind == "소지품 소실" && spec.Opponent != fact.Self &&
    (spec.Method == "소모 또는 파괴" || spec.Method == fact.Method);
  }
 }
}
