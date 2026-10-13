using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AABattle
{
  // 정규화 규칙 모음(상태). 적용 순서는 PhrasePipeline.cs에만 둔다.
  public static partial class PhraseRules
  {
    // 아군을 「X」 상태로 한다 → 자신(확정 47)
    // 출처: 칼데아 시트
    internal static string PreAllyState(string text)
    {
      text = Regex.Replace(text, @"^(T\s*종료\s*시\s*까지\s*,?\s*)?(\d+T\s*(?:간|동안)\s*)?아군(?:을|를)\s*(\d+T\s*(?:간|동안)\s*)?(?=「[^」]+」\s*(?:상태\s*)?로 한다)", "$1$2자신을 $3");
      return text;
    }

    // 상태이상을 치유하고 회복
    // 출처: 칼데아 3차
    internal static string CureAndHeal(string s)
    {
      Match m;
      m = Regex.Match(s, @"^자신의 상태이상을 해제해,? ?최대 체력의 1/(?<n>\d+)을 회복한다$");
      if (m.Success)
        return "자신의 상태이상을 치유한다. 자신의 체력을 최대 HP의 1/" + m.Groups["n"].Value + " 회복한다.";
      return null;
    }

    // 기간 + 상태 부여의 어미 변형
    // 출처: 칼데아 3차
    internal static string StateBecomes(string s)
    {
      Match m;
      m = Regex.Match(s, @"^(?<d>\d+T간 )?(?<t>상대|자신|자진)(?:를|을) 「(?<s>[^」]+)」 ?상태로 된다$");
      if (m.Success)
        return m.Groups["d"].Value + (m.Groups["t"].Value == "상대" ? "상대" : "자신") + "를 「" + m.Groups["s"].Value + "」 상태로 한다.";
      return null;
    }
  }
}
