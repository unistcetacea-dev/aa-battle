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

    // 상태이상 치유 봉인
    // 출처: 실프 인헨스먼츠
    internal static string SealCure(string s)
    {
      Match m;
      m = Regex.Match(s, @"^(?<t>자신|상대)의 「상태이상」(?:이|은) (?:해제되지|치유되지) 않는다$");
      if (m.Success)
        return m.Groups["t"].Value + "의 「상태이상」은 치유되지 않는다.";
      return null;
    }

    // 「X」 상태가 되지 않고, … → 문장 분리
    // 출처: 특성 도감
    internal static string AbilityStatusImmuneSplit(string t)
    {
      t = Regex.Replace(t, @"(?:자신은\s*)?(?<s>「[^」]+」)\s*상태가\s*되지\s*않고,\s*", "자신은 ${s}상태가 되지 않는다. ");
      return t;
    }

    // 자신의 HP 1/N 회복
    // 출처: 특성 도감
    internal static string AbilitySelfFractionHeal(string s)
    {
      Match m;
      if ((m = Regex.Match(s, @"^(?:자신의 )?HP를 1/(?<n>\d+)만큼 회복한다$")).Success) return "자신의 체력을 최대 HP의 1/" + m.Groups["n"].Value + " 회복한다";
      return null;
    }

    // 상태가 되지 않는다
    // 출처: 특성 도감
    internal static string AbilityStateImmune(string s)
    {
      Match m;
      if ((m = Regex.Match(s, @"^(?:자신이 )?「(?<x>[^」]+)」\s*(?:상태)?(?:가|이) 되지 않는다$")).Success) return "자신은 「" + m.Groups["x"].Value + "」상태가 되지 않는다";
      return null;
    }

    // 헤롱헤롱이나 도발 상태가 되지 않는다
    // 출처: 특성 도감
    internal static string AbilityTauntInfatuation(string s)
    {
      Match m;
      m = Regex.Match(s, @"^(?<n>헤롱헤롱이나 도발) 상태가 되지 않는다$");
      if (m.Success) return "자신은 「헤롱헤롱/도발」 상태가 되지 않는다";
      return null;
    }
  }
}
