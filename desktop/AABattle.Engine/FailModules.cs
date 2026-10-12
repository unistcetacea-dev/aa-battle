using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 // 실패 조건 모듈: 기술 문장 「…아니면 실패한다」「…면 실패한다」를 구조(FailSpec)로 읽어 기술을 내기 직전에 판정한다.
 //  상태      상대/자신이 「X」(「Y」) 상태가 아니면 · 자신이 「X」 상태면 · 「X」 상태일 때 내보내면
 //  타입      상대/자신이 「X」 타입이 아니면 · 상대가 자신과 같은 타입이 아니면
 //  날씨·필드  날씨가 「X」이 아니면 · 전체 필드가 「필드」 상태가 아니면 · 자진에 「벽」 기술이 전개되어 있지 않으면
 //  소지품    상대에게 「소지품」이 없으면/있으면 · 「소지품」이 없으면 · 자신과 상대 모두에게 없다면 · 「나무열매」가 없으면 · 먹은 후가 아니면
 //  능력변화  상대에게 「능력변화」가 없으면
 //  등장 T    「필드에 나온 후 최초」가 아니면 · 필드에 나온 후 NT째가 아니면
 //  이번 T    내보내기 전에 상대의 공격을 받으면 · 「물리」 기술을 받지 않으면 · 상대가 공격기술을 내보내지 않으면(나중이면)
 //            상대보다 먼저 내보내지 않으면 · 상대가 「방어」 기술을 내보내고 있지 않으면
 //  성별      성별 데이터가 없어 판정하지 않는다
 public sealed class FailSpec {
  public string Kind = "", Subject = "상대";
  public string[] Names = new string[0];
  public int Turn;
  public bool Negate;   // true: 조건이 성립하면 실패 / false: 조건이 성립하지 않으면 실패
 }

 public static class FailModules {
  static IEnumerable<string> Parts(Move m) {
   string t = m == null ? "" : m.effect ?? "";
   t = t.Replace('（', '(').Replace('）', ')').Replace('，', ',').Replace('．', '.').Replace('＋', '+').Replace('－', '-').Replace('：', ':');
   t = Regex.Replace(t, @"\s+", " ");
   return Regex.Split(t, @"\.(?![^()]*\))").Select(x => x.Trim().TrimEnd(',').Trim()).Where(x => x.Length > 0);
  }
  static string[] Names(string raw) { return Regex.Matches(raw ?? "", "「([^」]+)」").Cast<Match>().Select(x => x.Groups[1].Value).ToArray(); }

  public static bool IsModule(string sentence) { FailSpec f; return TryParse(sentence, out f); }
  public static bool TryParse(string sentence, out FailSpec f) {
   f = null;
   string s = Regex.Replace(sentence ?? "", @"\s+", " ").Trim().TrimEnd('.').Trim();
   Match x;
   if ((x = Regex.Match(s, @"^(?<s>상대|자신)(?:이|가) (?<n>(?:「[^」]+」)+) 상태가 아니면 실패한다$")).Success) { f = new FailSpec { Kind = "상태", Subject = x.Groups["s"].Value, Names = Names(x.Groups["n"].Value) }; return true; }
   if ((x = Regex.Match(s, @"^자신이 (?<n>(?:「[^」]+」)+) 상태면 실패한다$")).Success) { f = new FailSpec { Kind = "상태", Subject = "자신", Names = Names(x.Groups["n"].Value), Negate = true }; return true; }
   if ((x = Regex.Match(s, @"^(?<n>(?:「[^」]+」)+) 상태일 때 내보내면 실패한다$")).Success) { f = new FailSpec { Kind = "상태", Subject = "자신", Names = Names(x.Groups["n"].Value), Negate = true }; return true; }
   if ((x = Regex.Match(s, @"^(?<s>상대|자신)(?:이|가) 「(?<t>[^」]+)」 타입이 아니면 실패한다$")).Success) { f = new FailSpec { Kind = "타입", Subject = x.Groups["s"].Value, Names = new[] { x.Groups["t"].Value } }; return true; }
   if (s == "상대가 자신과 같은 타입이 아니면 실패한다") { f = new FailSpec { Kind = "같은 타입" }; return true; }
   if ((x = Regex.Match(s, @"^날씨가 「(?<w>[^」]+)」(?:이|가) 아니면 실패한다$")).Success) { f = new FailSpec { Kind = "날씨", Names = new[] { x.Groups["w"].Value } }; return true; }
   if (s == "전체 필드가 「필드」 상태가 아니면 실패한다") { f = new FailSpec { Kind = "필드" }; return true; }
   if (s == "자진에 「벽」 기술이 전개되어 있지 않으면 실패한다") { f = new FailSpec { Kind = "벽", Subject = "자신" }; return true; }
   if ((x = Regex.Match(s, @"^상대에게 「소지품」이 (?<v>없으면|있으면) 실패한다$")).Success) { f = new FailSpec { Kind = "소지품", Negate = x.Groups["v"].Value == "있으면" }; return true; }
   if (s == "「소지품」이 없으면 실패한다") { f = new FailSpec { Kind = "소지품", Subject = "자신" }; return true; }
   if (s == "자신과 상대 모두에게 「소지품」이 없다면 실패한다") { f = new FailSpec { Kind = "소지품", Subject = "어느 쪽" }; return true; }
   if (s == "「나무열매」가 없으면 실패한다") { f = new FailSpec { Kind = "나무열매", Subject = "자신" }; return true; }
   if (s == "「나무열매」를 먹은 후가 아니면 실패한다") { f = new FailSpec { Kind = "나무열매 먹음", Subject = "자신" }; return true; }
   if (s == "상대에게 「능력변화」가 없으면 실패한다") { f = new FailSpec { Kind = "능력변화" }; return true; }
   if (s == "「필드에 나온 후 최초」가 아니면 실패한다") { f = new FailSpec { Kind = "등장 T", Subject = "자신", Turn = 1 }; return true; }
   if ((x = Regex.Match(s, @"^필드에 나온 후 (?<n>\d+)T째가 아니면 실패한다$")).Success) { f = new FailSpec { Kind = "등장 T", Subject = "자신", Turn = int.Parse(x.Groups["n"].Value) }; return true; }
   if (s == "기술을 내보내기 전에 상대의 공격을 받으면 실패한다") { f = new FailSpec { Kind = "먼저 피격", Subject = "자신", Negate = true }; return true; }
   if (s == "기술을 내보내기 전에 상대의 「물리」 기술을 받지 않으면 실패한다") { f = new FailSpec { Kind = "먼저 피격", Subject = "자신", Names = new[] { "물리" } }; return true; }
   if (Regex.IsMatch(s, @"^상대가 (?:「공격」 기술|공격기술)을 내보내지 않으면 실패한다$")) { f = new FailSpec { Kind = "상대 공격 선택" }; return true; }
   if (Regex.IsMatch(s, @"^상대가 공격기술을 내보내지 않거나\s*,?\s*상대보다 나중에 내보내면 실패한다$")) { f = new FailSpec { Kind = "상대 공격 선택", Names = new[] { "먼저" } }; return true; }
   if (s == "상대보다 먼저 내보내지 않으면 실패한다") { f = new FailSpec { Kind = "먼저" }; return true; }
   if (s == "상대가 「방어」 기술을 내보내고 있지 않으면 실패한다") { f = new FailSpec { Kind = "방어 중" }; return true; }
   if (s == "상대와 성별이 같으면 실패한다") { f = new FailSpec { Kind = "성별" }; return true; }
   return false;
  }

  static bool HasState(Fighter f, string name) {
   if (f == null) return false;
   if (name == "미래예지") return (f.Conditions.DelayedAttacks ?? new List<DelayedAttackState>()).Any(x => x != null && x.Move != null);
   return f.Status == name || name == "상태이상" && f.Status != "정상" || f.Conditions.Hidden == name || Mechanics.HasCondition(f, name);
  }
  // 조건이 성립하는가(Negate는 Reason이 처리한다)
  static bool Holds(FailSpec f, Fighter a, Fighter b, Rules r) {
   var who = f.Subject == "자신" ? a : b;
   switch (f.Kind) {
    case "상태": return who != null && f.Names.Any(n => HasState(who, n));
    case "타입": return who != null && Mechanics.Has(who, f.Names[0]);
    case "같은 타입": return a != null && b != null && (a.Data.types ?? new string[0]).Any(t => Mechanics.Has(b, t));
    case "날씨": { string w = Mechanics.Weather(a, b ?? a, r); return f.Names[0] == w || f.Names[0] == "쾌청" && Mechanics.Sunny(w) || f.Names[0] == "비" && Mechanics.Rain(w); }
    case "필드": return r.Terrain != "없음";
    case "벽": { int side = Mechanics.PartySide(a, r); if (side < 0) side = 0; return r.Sides[side].Reflect > 0 || r.Sides[side].LightScreen > 0; }
    case "소지품": return f.Subject == "어느 쪽" ? !string.IsNullOrEmpty(a.Data.item) || b != null && !string.IsNullOrEmpty(b.Data.item) : who != null && !string.IsNullOrEmpty(who.Data.item);
    case "나무열매": return (a.Data.item ?? "").EndsWith("열매");
    case "나무열매 먹음": return (a.LostItem ?? "").EndsWith("열매");
    case "능력변화": return b != null && (b.Stages.Skip(1).Any(x => x != 0) || b.AccuracyStage != 0 || b.EvasionStage != 0);
    case "등장 T": return a.Conditions.FieldTurn <= 1 ? f.Turn == 1 : a.Conditions.FieldTurn == f.Turn;
    case "먼저 피격": return (a.Conditions.TurnFacts ?? new List<TriggerFact>()).Any(x => x.Kind == "명중" && x.Received && x.Move != null && x.Move.category != "변화" && (f.Names.Length == 0 || x.Move.category == f.Names[0]));
    case "상대 공격 선택": return b != null && (b.Conditions.Selected == "물리" || b.Conditions.Selected == "특수") && (f.Names.Length == 0 || b.Conditions.ActionOrder == 0);
    case "먼저": return b != null && b.Conditions.ActionOrder == 0;
    case "방어 중": return b != null && b.Conditions.Protect;
    case "성별": return false;
   }
   return false;
  }

  // 기술을 내기 직전: 실패하면 이유, 아니면 ""
  public static string Reason(Fighter a, Fighter b, Move m, Rules r) {
   if (a == null || m == null) return "";
   r = r ?? new Rules();
   foreach (string part in Parts(m)) {
    FailSpec f;
    if (!TryParse(part, out f) || f.Kind == "성별") continue;
    bool holds = Holds(f, a, b, r);
    if (f.Negate ? holds : !holds) return part + ".";
   }
   return "";
  }
 }
}
