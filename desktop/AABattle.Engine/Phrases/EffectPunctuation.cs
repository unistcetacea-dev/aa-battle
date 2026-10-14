namespace AABattle {
 // 효과 표준 문장만 마침표 하나로 끝낸다. 트리거 문장에는 적용하지 않는다.
 // 출처: 2026-10-14 K 후속 사용자 결정, 지시서 N 1항.
 internal static class EffectPunctuation {
  public static string Standardize(string text) {
   if (string.IsNullOrWhiteSpace(text)) return text;
   return text.TrimEnd('.', '。', '．').Trim() + ".";
  }
 }
}
