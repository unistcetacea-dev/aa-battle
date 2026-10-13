using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 // 효과 없음 모듈(사용자 결정 2026-10-13): 더블 배틀(다인전) 전용 효과와 배틀 밖 효과는 싱글에서 실행할 것이 없다.
 //  다인전 전용 문장   「…」 → 다인전 전용 효과이므로 싱글에서는 효과가 없다.
 //  배틀 밖 문장       「…」 → 배틀 밖 효과이므로 효과가 없다.
 // 실행할 문장이 없는 것으로 분류하고(오류·판정 대기 없음), 같은 특성의 다른 문장은 그대로 실행한다.
 public static class AbilityNoEffectModules {
  public const string MultiBattle = "다인전 전용 효과이므로 싱글에서는 효과가 없다";
  public const string OutOfBattle = "배틀 밖 효과이므로 효과가 없다";
  public const string Reason = "효과 없음(싱글)";
  public const string PartialReason = "일부 문장 효과 없음(싱글)";

  static string Clean(string text) {
   return Regex.Replace(PotentialParts.Clean(text ?? ""), @"\s+", " ").Trim().TrimEnd('.', '。', '．').Trim();
  }

  // 표준 문장(트리거 절이 앞에 붙어도 효과 부분이 이 문장이면 효과 없음)
  public static bool IsNoEffect(string sentence) {
   string s = Clean(sentence);
   return s.EndsWith(MultiBattle, StringComparison.Ordinal) || s.EndsWith(OutOfBattle, StringComparison.Ordinal);
  }

  public static string Normalize(string text) {
   string s = Clean(text);
   if (s == MultiBattle || s == OutOfBattle) return s + ".";
   return null;
  }

  public static string Classify(string sentence) {
   string s = Clean(sentence);
   if (s == MultiBattle) return "다인전 전용 효과 없음";
   if (s == OutOfBattle) return "배틀 밖 효과 없음";
   return "";
  }

  // 도감 표시용: 특성의 모든 문장이 효과 없음이면 「효과 없음(싱글)」, 일부면 「일부 문장 효과 없음(싱글)」
  public static string Describe(string[] sentences) {
   var list = (sentences ?? new string[0]).Where(x => (x ?? "").Trim().Length > 0).ToList();
   if (list.Count == 0 || !list.Any(IsNoEffect)) return "";
   return list.All(IsNoEffect) ? Reason : PartialReason;
  }
 }
}
