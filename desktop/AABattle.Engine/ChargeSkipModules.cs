using System;
using System.Text.RegularExpressions;

namespace AABattle
{
  // 모으기 생략 효과의 기술 조건은 MoveCondition과 같은 「기술:X」 토큰을 쓴다.
  public static class ChargeSkipModules
  {
    const string Generic = "자신의 「모으기」 기술의 모으기를 생략한다";
    static readonly Regex Named = new Regex(@"^자신의 (?<condition>(?:「[^」]+」\s*)+) 기술의 모으기를 생략한다$");

    static string Clean(string text)
    {
      return Regex.Replace(PotentialParts.Clean(text ?? ""), @"\s+", " ").Trim().TrimEnd('.', '。', '．');
    }

    public static bool IsSkip(string text)
    {
      string value = Clean(text);
      return value == Generic || Named.IsMatch(value);
    }

    public static bool Matches(string text, Move move)
    {
      string value = Clean(text);
      if (value == Generic)
        return true;
      var match = Named.Match(value);
      if (!match.Success || move == null)
        return false;
      MoveCondition condition;
      return MoveCondition.TryParse(match.Groups["condition"].Value, out condition) && condition.Matches(move, null, 1);
    }

    public static string Classify(string text)
    {
      return IsSkip(text) ? "모으기 생략" : "";
    }
  }
}
