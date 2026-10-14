namespace AABattle {
 // 유턴 효과 부여 문장의 대상 한정. 공격기술 한정은 변화기술에 적용하지 않는다.
 public static class PivotGrantRules {
  public static bool IsAttackOnly(string sentence) {
   string value = PotentialParts.Clean(sentence ?? "").Replace(" ", "").Replace("　", "");
   return value.Contains("공격기술") && value.Contains("유턴") && value.Contains("부여");
  }

  public static bool AppliesTo(string sentence, Move move) {
   if (move == null) return false;
   return !IsAttackOnly(sentence) || move.category != "변화";
  }
 }
}
