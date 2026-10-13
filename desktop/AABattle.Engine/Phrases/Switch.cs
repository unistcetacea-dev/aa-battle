using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AABattle
{
  // 정규화 규칙 모음(교대·인계). 적용 순서는 PhrasePipeline.cs에만 둔다.
  public static partial class PhraseRules
  {
    // 능력 상승(하락) 인계 → 「능력변화」 인계
    // 출처: 칼데아 시트
    internal static string PreRankPass(string text)
    {
      text = Regex.Replace(text, @"^(?<t>자신|상대)의 능력(?:의)? ?상승(?:\(하락\))?(?:을|를) 인계(?<o>한다|할 수 있다)$", "${t}의 「능력변화」를 인계${o}");
      return text;
    }

    // 상대를 「강제교대」시킨다 → 강제교대시킨다
    // 출처: 칼데아 시트
    internal static string PreForcedSwitch(string text)
    {
      text = Regex.Replace(text, @"^상대(?:를|을) 「강제교대」\s*시킨다$", "상대를 강제교대시킨다");
      return text;
    }

    // 「통상교대」이외 → 「통상교대」 이외
    // 출처: 칼데아 시트
    internal static string PreNormalSwitchOnly(string text)
    {
      text = Regex.Replace(text, @"」이외의 수단", "」 이외의 수단");
      return text;
    }

    // 아군과 임의교대할 수 있다
    // 출처: 실프 인헨스먼츠
    internal static string OptionalSwitch(string s)
    {
      if (Regex.IsMatch(s, @"^아군과 (?:임의)?교대(?:할| ?해 (?:필드|장소)에 나올) 수 있다$"))
        return AbaddonModules.OptionalSwitchSentence;
      return null;
    }

    // 공격기술에 유턴 효과 부여
    // 출처: 실프 인헨스먼츠
    internal static string AttackPivot(string s)
    {
      if (Regex.IsMatch(s, @"^자신의 공격기술에 「유턴」 효과를 부여한다$"))
        return AbaddonModules.AttackPivotSentence;
      return null;
    }

    // 강제교대 무효
    // 출처: 특성 도감
    internal static string AbilityNoForcedSwitch(string s)
    {
      if (s == "강제교대 무효") return "자신은 강제교대되지 않는다";
      return null;
    }
  }
}
