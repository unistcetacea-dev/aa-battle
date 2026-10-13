using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AABattle
{
  // 트리거 정규화 규칙 모음(사건). 적용 순서는 PhrasePipeline.cs에만 둔다.
  public static partial class TriggerPhraseRules
  {
    // 자신(아군)의 공격·기술 명중
    // 출처: 칼데아 시트(트리거)
    internal static string HitSelf(string t)
    {
      Match m;
      m = Regex.Match(t, @"^(?:자신|아군)의 (?<k>공격|기술)(?:이)? ?명중 ?(?:했을 ?때|시에?)$");
      if (m.Success)
        return "자신의 " + m.Groups["k"].Value + "이 명중했을 때";
      return null;
    }

    // 「급소」에 맞았을 때 표준형
    // 출처: 칼데아 시트(트리거)
    internal static string CriticalReceived(string t)
    {
      Match m;
      m = Regex.Match(t, @"^자신의 (?<k>공격|기술)이 「급소」에 맞았을 때$");
      if (m.Success)
        return "자신의 " + m.Groups["k"].Value + "이 급소에 맞았을 때";
      return null;
    }

    // 상대를 빈사로 했을 때 = 쓰러뜨렸을 때 · 나무열매 소모
    // 출처: 칼데아 시트(트리거)
    internal static string DefeatByFaint(string t)
    {
      Match m;
      if (Regex.IsMatch(t, @"^상대를 빈사로 했을 ?때$"))
        return "상대를 쓰러뜨렸을 때";
      if (Regex.IsMatch(t, @"^자신(?:이|은)? 나무열매를 소모했을 때$"))
        return "자신이 「나무열매」를 사용했을 때";
      m = Regex.Match(t, @"^「(?<m>[^」]+)」로 상대를 빈사로 했을 ?때$");
      if (m.Success)
        return "자신의 「" + m.Groups["m"].Value + "」 기술로 상대를 쓰러뜨렸을 때";
      return null;
    }

    // 공격하지 않을 때
    // 출처: 칼데아 시트(트리거)
    internal static string NoAttack(string t)
    {
      if (Regex.IsMatch(t, @"^상대가 공격을 (?:실시하지|하지) 않을 ?때$"))
        return "상대가 공격하지 않을 때";
      if (Regex.IsMatch(t, @"^공격을 하지 않을 ?때$"))
        return "자신이 공격하지 않을 때";
      return null;
    }

    // 서로 필중 기술을 선택했을 때
    // 출처: 일반(서로 선택 트리거)
    internal static string MutualFixedHit(string t)
    {
   if (Regex.IsMatch(t, @"^서로\s*(?:「)?필중(?:」)?\s*기술을\s*(?:내보낼|선택했을)\s*때$")) return "서로 필중 기술을 선택했을 때";
      return null;
    }

    // 서로 공격기를 선택했을 때
    // 출처: 일반(서로 선택 트리거)
    internal static string MutualAttack(string t)
    {
   if (Regex.IsMatch(t, @"^서로\s*공격(?:기)?을\s*(?:내보낼|선택했을)\s*때$")) return "서로 공격기를 선택했을 때";
      return null;
    }

    // 서로 변화기술을 선택했을 때
    // 출처: 일반(서로 선택 트리거)
    internal static string MutualStatusMove(string t)
    {
   if (Regex.IsMatch(t, @"^서로\s*(?:「)?변화(?:기술)?(?:」)?(?:을)?\s*(?:내보낼|선택했을|내보내고\s*있을)\s*때$")) return "서로 변화기술을 선택했을 때";
      return null;
    }

    // 접촉기술을 받았을 때
    // 출처: 특성 도감
    internal static string AbilityContactReceived(string c)
    {
      if (Regex.IsMatch(c, @"^(?:「?접촉\s*기술」?|「접촉」 기술)을 (?:받으면|받았을 때)$")) return "상대의 「접촉」 기술을 받았을 때";
      return null;
    }

    // 비접촉 기술을 받았을 때
    // 출처: 특성 도감
    internal static string AbilityNonContactReceived(string c)
    {
      if (Regex.IsMatch(c, @"^「비접촉」 기술을 받았을 때$")) return "상대의 「비접촉」 기술을 받았을 때";
      return null;
    }

    // 상대의 공격을 받았을 때
    // 출처: 특성 도감
    internal static string AbilityAttackReceived(string c)
    {
      if (Regex.IsMatch(c, @"^(?:상대의 공격기술을 받으면|「공격」 기술을 받았을 때|상대의 공격을 받았을 때)$")) return "상대의 공격을 받았을 때";
      return null;
    }

    // 물리·특수 기술을 받았을 때
    // 출처: 특성 도감
    internal static string AbilityPhysicalSpecialReceived(string c)
    {
      Match m;
      if ((m = Regex.Match(c, @"^「(?<x>물리|특수)\s*(?:기술)?」\s*(?:기술)?을 (?:받으면|받았을 때)$")).Success) return "상대의 「" + m.Groups["x"].Value + "」 기술을 받았을 때";
      return null;
    }

    // 물리·특수 기술을 명중시키면
    // 출처: 특성 도감
    internal static string AbilityPhysicalSpecialHit(string c)
    {
      Match m;
      if ((m = Regex.Match(c, @"^「(?<x>물리|특수)기술」을 명중시키면$")).Success) return "자신의 「" + m.Groups["x"].Value + "」 기술이 명중했을 때";
      return null;
    }

    // 상대를 쓰러뜨리면
    // 출처: 특성 도감
    internal static string AbilityDefeat(string c)
    {
      if (c == "상대를 쓰러트리면" || c == "상대를 쓰러뜨리면") return "상대를 쓰러뜨렸을 때";
      return null;
    }
  }
}
