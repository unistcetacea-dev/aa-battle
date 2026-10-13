using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AABattle
{
  // 정규화 규칙 모음(방호·관통·부여). 적용 순서는 PhrasePipeline.cs에만 둔다.
  public static partial class PhraseRules
  {
    // 방어 등의 기술 관통 (주어·조건 생략 가능)
    // 출처: 칼데아 시트
    internal static string PierceGuardA(string s)
    {
      Match m;
      m = Regex.Match(s, @"^(?:자신의 )?(?<c>(?:「[^」]+」)*)\s*(?:기술(?:이|은)\s*)?상대의 「?방어」?\s*등의 기술(?:을|도)\s*(?:관통|무시)한다$");
      if (m.Success)
      {
        string c = Cond(m.Groups["c"].Value);
        if (c != null)
          return "자신의 " + c + "기술이 상대의 「방어」 기술을 관통한다.";
      }
      return null;
    }

    // 방어 등의 기술 관통(무효화 포함)
    // 출처: 칼데아 시트
    internal static string PierceGuardB(string s)
    {
      Match m;
      m = Regex.Match(s, @"^자신의 (?<c>(?:「[^」]+」)*)\s*(?:기술(?:이|은)\s*)?상대의 「?방어」?\s*등의 기술(?:을|도)\s*(?:관통|무시|무효화)한다$");
      if (m.Success)
      {
        string c = Cond(m.Groups["c"].Value);
        if (c != null)
          return "자신의 " + c + "기술이 상대의 「방어」 기술을 관통한다.";
      }
      return null;
    }

    // 방어 기술·특성·방호 포텐셜을 한꺼번에 뚫는 문장
    // 출처: 칼데아 시트
    internal static string PierceAll(string s)
    {
      Match m;
      m = Regex.Match(s, @"^(?:자신의 (?<c>(?:「[^」]+」)*)\s*(?:기술(?:이|은)\s*)?)?상대 포켓몬의 「방어」\s*등의 기술,?\s*(?<ab>특성,?\s*)?상대 포켓몬과 상대 트레이너의 방호 포텐셜을 무효화한다$");
      if (m.Success)
      {
        string c = Cond(m.Groups["c"].Value);
        if (c != null)
        {
          var list = new List<string>();
          list.Add("자신의 " + c + "기술이 상대의 「방어」 기술을 관통한다.");
          if (m.Groups["ab"].Success)
            list.Add("자신의 " + c + "기술은 상대의 〔방호 특성〕을 관통한다.");
          list.Add("자신의 " + c + "기술이 상대의 방호 포텐셜을 무시한다.");
          list.Add("자신의 " + c + "기술이 상대의 트레이너 방호 포텐셜을 무시한다.");
          return string.Join(" ", list);
        }
      }
      return null;
    }

    // 상대 방호 포텐셜 무효화
    // 출처: 칼데아 시트
    internal static string IgnoreGuardPotential(string s)
    {
      Match m;
      m = Regex.Match(s, @"^상대 포켓몬과 상대 트레이너의 방호 포텐셜을 무효화한다$");
      if (m.Success)
        return "자신의 기술이 상대의 방호 포텐셜을 무시한다. 자신의 기술이 상대의 트레이너 방호 포텐셜을 무시한다.";
      return null;
    }

    // 상태이상 보호
    // 출처: 칼데아 시트
    internal static string StatusProtect(string s)
    {
      if (Regex.IsMatch(s, @"^(?:상대로부터의 「상태이상」을 무효화한다|상대에 의해 자신은 「상태이상」 ?상태가 되지 않는다)$"))
        return "상대에 의해 자신은 「상태이상」 상태가 되지 않는다.";
      return null;
    }

    // 역할 임명의 조사 변형
    // 출처: 칼데아 3차
    internal static string RoleAppoint(string s)
    {
      Match m;
      m = Regex.Match(s, @"^자신을 『(?<r>[^』]+)』(?:에|으로|로) 임명(?<o>한다|할 수 있다)$");
      if (m.Success)
        return RoleModules.NormalizeEffect("자신을 『" + (m.Groups["r"].Value == "엠페러" ? "엠퍼러" : m.Groups["r"].Value) + "』로 임명" + m.Groups["o"].Value);
      return null;
    }

    // 자진의 포켓몬 체력 가득 회복
    // 출처: 실프 인헨스먼츠
    internal static string FullHeal(string s)
    {
      if (Regex.IsMatch(s, @"^자진의 포켓몬의 체력을 가득(?:으로)? 회복한다$"))
        return AbaddonModules.FullHealSentence;
      return null;
    }

    // 필드 밖 발동
    // 출처: 실프 인헨스먼츠
    internal static string OffField(string s)
    {
      if (Regex.IsMatch(s, @"^이 포텐셜은 자신이 필드에 없어도 발동(?:한다|할 수 있다)$"))
        return AbaddonModules.OffFieldSentence;
      return null;
    }

    // 설치물 무시
    // 출처: 특성 도감
    internal static string AbilityHazardProof(string s)
    {
      if (Regex.IsMatch(s, @"^설치기술의 (?:효과를 무시한다|영향을 받지 않는다)$")) return AbilityPhrases.HazardProof;
      return null;
    }

    // 방호 특성 관통
    // 출처: 특성 도감
    internal static string AbilityPierce(string s)
    {
      if (Regex.IsMatch(s, @"^(?:상대의 방호특성을 무시한다|방호특성 무효|자신의 기술이 상대의 〔방호 특성〕을 관통한다)$")) return AbilityPhrases.Pierce;
      return null;
    }

    // 〔특성〕을 〔X〕로 변경한다
    // 출처: 특성 도감
    internal static string AbilityChangeAbility(string s)
    {
      Match m;
      m = Regex.Match(s, @"^(?<s>자신|상대)의 〔특성〕을 〔(?<a>[^〕]+)〕(?:으로|로) 변경한다$");
      if (m.Success) return m.Groups["s"].Value + "의 특성을 「" + m.Groups["a"].Value + "」" + EffectModules.Ro(m.Groups["a"].Value) + " 한다";
      return null;
    }
  }
}
