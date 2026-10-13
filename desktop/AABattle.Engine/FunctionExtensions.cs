using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 // 기능확장: 포켓몬 기술 목록(개수 제한 없음)에 없는 기술을 내보내는 것.
 // 포텐셜 「자신이/아군 전원이 「기능확장:X」를 내보낼 수 있다」가 고를 수 있는 기술을 정하고, 그렇게 낸 기술은 Move.extension이 켜진다
 // (기술 조건 「기능확장」으로 위력·타입·트리거 등이 알아본다). 오레마스 시스템: 기능확장 기술은 「구애」에 묶이지 않는다.
 public static class FunctionExtensions {
  static readonly Regex Grant = new Regex(@"^(?<s>자신|아군 전원)이 (?<m>(?:「기능확장:[^」]+」)+)(?:을|를) 내보낼 수 있다$");

  static string Clean(string text) { return Regex.Replace(PotentialParts.Clean(text ?? ""), @"\s+", " ").Trim().TrimEnd('.', '。', '．').Trim(); }

  // 수집 원문 → 표준 문장. 아니면 null
  public static string Normalize(string text) {
   string t = Clean(text);
   if (!Regex.IsMatch(t, @"기능\s*확장")) return null;
   // 이미 표준 문장이면(이름에 괄호가 있어도: 「기능확장:아라가미탄(불꽃)」) 그대로 다시 만든다
   string standardSubject; string[] standardMoves;
   if (TryGrant(t, out standardSubject, out standardMoves)) return Canonical(standardSubject, standardMoves);
   var m = Regex.Match(t, @"^(?:(?<s>자신|아군\s*전원|아군|PT\s*전원|PT의\s*아군\s*전원|PT의\s*전원)(?:이|가)\s*)?(?<body>.+?)\s*(?:을|를)\s*(?:내보낼|사용할)\s*수\s*있다$");
   if (!m.Success) return null;
   var names = new List<string>();
   string body = m.Groups["body"].Value;
   var first = Regex.Match(body, @"^(?:「\s*기능\s*확장\s*[:：(]\s*(?<n>[^」)]+?)\s*\)?\s*」|기능\s*확장\s*「(?<n>[^」]+)」)");
   if (!first.Success) return null;
   names.Add(first.Groups["n"].Value.Trim());
   string rest = body.Substring(first.Length);
   foreach (Match extra in Regex.Matches(rest, "「([^」]+)」")) names.Add(extra.Groups[1].Value.Trim());
   if (Regex.Replace(rest, "「[^」]+」", "").Trim(' ', ',', '，', '、').Length > 0) return null;
   if (names.Any(x => x.Length == 0 || x.Contains("기능확장"))) return null;
   string s = m.Groups["s"].Value.Replace(" ", "");
   string subject = s.Contains("전원") ? "아군 전원" : "자신";
   return Canonical(subject, names);
  }
  public static bool TryGrant(string text, out string subject, out string[] moves) {
   var m = Grant.Match(Clean(text));
   subject = m.Success ? m.Groups["s"].Value : "";
   moves = m.Success ? Regex.Matches(m.Groups["m"].Value, "「기능확장:([^」]+)」").Cast<Match>().Select(x => x.Groups[1].Value.Trim()).ToArray() : new string[0];
   return m.Success && moves.Length > 0;
  }
  public static string Canonical(string subject, IEnumerable<string> moves) {
   var list = moves.ToList();
   string last = list.Count == 0 ? "" : list[list.Count - 1];
   char ch = last.Length == 0 ? ' ' : last[last.Length - 1];
   bool batchim = ch >= '가' && ch <= '힣' && (ch - '가') % 28 != 0;
   return subject + "이 " + string.Concat(list.Select(x => "「기능확장:" + x + "」")) + (batchim ? "을" : "를") + " 내보낼 수 있다.";
  }

  // 포텐셜 하나가 지금 주는 기능확장 기술(조건이 지금 성립하는 것만). party: 「아군 전원」만 받는다
  static IEnumerable<string> Grants(string effectText, IEnumerable<string> clauses, Fighter holder, Fighter opponent, Rules rules, bool party) {
   var lines = EffectNormalizer.NormalizeLines(effectText ?? "").Replace("\r", "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
   var granted = new List<string>();
   foreach (string line in lines) {
    string subject; string[] moves;
    if (!TryGrant(line, out subject, out moves)) continue;
    if (party && subject != "아군 전원") continue;
    granted.AddRange(moves);
   }
   if (granted.Count == 0) return granted;
   // 사건 트리거로 주는 것은 실행기가 그때 기록한다(ConditionState.ExtensionMoves). 여기서는 계속 성립하는 조건만
   foreach (string clause in clauses.Where(x => x.Length > 0)) if (!TriggerModules.Holds(clause, holder, opponent, rules ?? new Rules())) return new string[0];
   return granted;
  }

  // 이 포켓몬이 지금 기능확장으로 고를 수 있는 기술 이름(자기 기술 목록에 있는 것은 뺀다)
  public static string[] Available(Fighter f, Fighter opponent, Rules rules, TrainerRecord trainer, IEnumerable<Fighter> party) {
   if (f == null || f.Data == null) return new string[0];
   if (f.Conditions.ChoiceMove.Length > 0 && ItemRuntime.Choice(f)) return new string[0];   // 구애된 상태에서는 기능확장 불가(오레마스 시스템)
   var names = new List<string>();
   var machine = System.Text.RegularExpressions.Regex.Match(ItemRuntime.Name(f), @"^기술머신\((?<m>[^)]+)\)$");
   if (machine.Success) names.Add(machine.Groups["m"].Value.Trim());
   foreach (var p in (f.Data.potentials ?? new Potential[0]).Where(x => x != null)) {
    string condition, effect; PotentialLibrary.Split(p.description ?? "", out condition, out effect);
    names.AddRange(Grants(effect.Length > 0 ? effect : p.description, TriggerStructures.Clauses(p), f, opponent, rules, false));
   }
   // 「아군 전원」: PT의 다른 멤버(빈사여도, 오레마스 시스템 「아군이 PT에 참가하고 있을 때」)
   foreach (var member in (party ?? new Fighter[0]).Where(x => x != null && x.Data != null && !Mechanics.SameMember(x, f)))
    foreach (var p in (member.Data.potentials ?? new Potential[0]).Where(x => x != null)) {
     string condition, effect; PotentialLibrary.Split(p.description ?? "", out condition, out effect);
     names.AddRange(Grants(effect.Length > 0 ? effect : p.description, new string[0], f, opponent, rules, true));
    }
   // 트레이너 고유: 파티 전체
   if (trainer != null)
    foreach (var record in (trainer.potentials ?? new List<PotentialRecord>()).Where(x => x != null && x.enabled && x.slot == "고유"))
     names.AddRange(Grants(record.effect, TrainerTriggers.ClausesOf(record), f, opponent, rules, false));
   names.AddRange(f.Conditions.ExtensionMoves ?? new List<string>());
   var own = f.Moves ?? new string[0];
   return names.Where(x => !own.Contains(x)).Distinct().ToArray();
  }
 }
}
