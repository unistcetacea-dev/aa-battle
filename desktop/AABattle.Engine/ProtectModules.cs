using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 // 방어 기술 모듈(사용자 결정: 방어 기술은 「방어」 상태가 되는 것)
 //  「방어」 기술(변화)      자신을 「방어」 상태로 한다. 막는 범위는 기술 문장 「상대의 「공격/변화/전체」 기술을 「방어」한다」(없으면 전부)
 //  반응                    「「접촉」 기술을 무효화했을 때, X」 · 「상대의 기술을 무효화했을 때, X」(X는 효과 모듈)
 //  연속 사용               「연속해서 내보내면 실패하기 쉽다」 1, 1/3, 1/9…(「방어」 기술과 같은 판정을 공유, 「별도 판정」은 그 기술만)
 //                          「연속해서 내보내면 실패한다」 같은 기술을 바로 다시 내면 실패
 //  버티기                  자신을 「버티기」 상태로 한다(빈사가 되는 공격을 HP 1로 버틴다, T 종료까지)
 public static class ProtectModules {
  public const string Blocked = "방어가 기술을 막았다";

  static string Plain(Move m) {
   string t = m == null ? "" : m.effect ?? "";
   t = t.Replace('（', '(').Replace('）', ')').Replace('，', ',').Replace('．', '.').Replace('＋', '+').Replace('－', '-').Replace('：', ':');
   return Regex.Replace(t, @"\s+", " ");
  }
  static IEnumerable<string> Parts(Move m) { return Regex.Split(Plain(m), @"\.(?![^()]*\))").Select(x => x.Trim()).Where(x => x.Length > 0); }

  // 「방어」 상태가 되는 변화기술인가(패스트가드처럼 다른 상태가 되는 것은 제외)
  public static bool IsGuard(Move m) {
   if (m == null || m.category != "변화" || !(m.tags ?? new string[0]).Contains("방어")) return false;
   return !Regex.IsMatch(Plain(m), @"「[^」]+」 상태로 한다");
  }
  static string Kind(Move guard) {
   var x = Regex.Match(Plain(guard), @"「(?<k>공격|변화|전체)」 기술을 「방어」한다");
   return x.Success ? x.Groups["k"].Value : "";
  }
  public static bool Blocks(Database db, Fighter b, Move m) {
   if (b == null || !b.Conditions.Protect || m == null || m.name == "페인트" || MoveModules.PiercesProtect(m)) return false;
   var guard = db == null || string.IsNullOrEmpty(b.Conditions.ProtectMove) ? null : db.moves.FirstOrDefault(x => x.name == b.Conditions.ProtectMove);
   string kind = Kind(guard);
   if (kind == "공격") return m.category != "변화";
   if (kind == "변화") return m.category == "변화";
   if (kind == "전체") return m.target == "전체";
   return true;
  }

  // 막았을 때의 반응
  public static void React(Database db, Fighter protector, Fighter attacker, Move m, Rules r, Func<double> rng, List<string> log) {
   var guard = db == null || string.IsNullOrEmpty(protector.Conditions.ProtectMove) ? null : db.moves.FirstOrDefault(x => x.name == protector.Conditions.ProtectMove);
   if (guard == null) return;
   foreach (string part in Parts(guard)) {
    var x = Regex.Match(part, @"^(?<c>「접촉」 기술|상대의 기술)을 무효화했을 때\s*,\s*(?<e>.+)$");
    if (!x.Success) continue;
    if (x.Groups["c"].Value.StartsWith("「접촉」") && (!Mechanics.Contact(m) || MoveModules.IgnoresContact(attacker))) continue;
    var unknown = new List<string>();
    foreach (string line in MoveRuntime.Convert(guard, x.Groups["e"].Value, unknown)) MoveRuntime.Run(protector, attacker, guard, line, r, rng, log);
    foreach (string u in unknown) log.Add(guard.name + ": 효과 판정 대기 — " + u);
   }
  }

  // 연속 사용 판정. 실패하면 이유, 아니면 ""
  public static bool SharesChain(Move m) { string t = Plain(m); return t.Contains("연속해서 내보내면 실패하기 쉽다") && !t.Contains("별도 판정"); }
  public static string ChainFail(Fighter a, Move m, Func<double> rng) {
   if (a == null || m == null) return "";
   string t = Plain(m);
   var c = a.Conditions;
   if (t.Contains("연속해서 내보내면 실패한다")) return c.LastMove == m.name ? "연속해서 내보내 실패했다." : "";
   if (!t.Contains("연속해서 내보내면 실패하기 쉽다")) return "";
   bool shared = !t.Contains("별도 판정");
   int chain = shared ? c.ProtectChain : c.LastMove == m.name ? c.SoloChain : 0;
   if ((rng ?? (() => .5))() < Math.Pow(1.0 / 3, chain)) {
    if (shared) c.ProtectChain = chain + 1; else c.SoloChain = chain + 1;
    return "";
   }
   if (shared) c.ProtectChain = 0; else c.SoloChain = 0;
   return "연속 사용 실패.";
  }
 }
}
