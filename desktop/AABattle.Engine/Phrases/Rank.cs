using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AABattle
{
  // 정규화 규칙 모음(능력·랭크). 적용 순서는 PhrasePipeline.cs에만 둔다.
  public static partial class PhraseRules
  {
    // 「방어」, 「특방」 → 「방어/특방」
    // 출처: 칼데아 시트
    internal static string PreStatList(string text)
    {
      text = Regex.Replace(text, "「(" + StatName + ")」\\s*[,、]\\s*「(" + StatName + ")」", "「$1/$2」");
      return text;
    }

    // 방어(특방) → 「방어/특방」
    // 출처: 칼데아 시트
    internal static string PreStatParen(string text)
    {
      text = Regex.Replace(text, "(?<=의 )(" + StatName + ")\\((" + StatName + ")\\)(?=을|를|이|가)", "「$1/$2」");
      return text;
    }

    // 종족치 랭크 한 칸
    // 출처: 칼데아 3차
    internal static string SpeciesRank(string s)
    {
      Match m;
      m = Regex.Match(s, "^자신의 (?<st>" + StatOrHp + @") 종족치를 「(?<n>\d)」랭크 올린다$");
      if (m.Success)
        return "자신의 「" + m.Groups["st"].Value + "」 종족치를 " + m.Groups["n"].Value + "랭크 올린다.";
      return null;
    }

    // 상대에 의해 능력이 감소(저하)하지 않는다
    // 출처: 칼데아 3차
    internal static string NoStatDrop(string s)
    {
      if (Regex.IsMatch(s, @"^상대에 의해 (?:자신의 )?능력이 (?:감소|저하|감소\(저하\))하지 않는다$"))
        return "자신은 상대에 의해 능력이 내려가지 않는다.";
      return null;
    }

    // 유대의 지시 상승
    // 출처: 칼데아 3차
    internal static string BondGuideRise(string s)
    {
      Match m;
      m = Regex.Match(s, @"^트레이너의 지시(?:를|을) 「(?<n>\d+)」\s*랭크 올린다$");
      if (m.Success)
        return BondModules.RiseSentence(int.Parse(m.Groups["n"].Value));
      return null;
    }
  }
}
