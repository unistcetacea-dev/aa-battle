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

    // 능력 변화 반전
    // 출처: 실프 인헨스먼츠
    internal static string InvertRank(string s)
    {
      Match m;
      m = Regex.Match(s, @"^(?<t>자신|상대)의 능력 변화를 반전(?<o>한다|할 수 있다)$");
      if (m.Success)
        return m.Groups["t"].Value + "의 능력 변화를 반전" + m.Groups["o"].Value + ".";
      return null;
    }

    // 상대 능력 하락에 비례해 위력 약화
    // 출처: 실프 인헨스먼츠
    internal static string LowerPower(string s)
    {
      Match m;
      m = Regex.Match(s, @"^상대 능력의 「-」에 비례해,? 상대 기술의 위력을 (?:완화|약화)한다(?:\s*\(\s*-1\s*=\s*(?<r>[0-9.]+)배.*\))?$");
      if (m.Success)
        return "상대의 능력 하락 1랭크마다 상대 기술의 위력을 약화(" + (m.Groups["r"].Success ? m.Groups["r"].Value : "0.9") + "배)한다.";
      return null;
    }

    // 능력 하락 무효(전체)
    // 출처: 특성 도감
    internal static string AbilityDropImmuneAll(string s)
    {
      if (Regex.IsMatch(s, @"^(?:자신의 능력이 감소\(저하\)하지 않는다|상대에 의해 능력이 감소\(저하\)하지 않는다|상대에 의한 능력 감소\(저하\) 효과를 무효화한다|상대로부터의 능력감소\(저하\)를 (?:무효화한다|받지 않는다))$")) return "자신은 상대에 의해 능력이 내려가지 않는다";
      return null;
    }

    // 능력 하락 무효(능력 하나)
    // 출처: 특성 도감
    internal static string AbilityDropImmuneStat(string s)
    {
      Match m;
      if ((m = Regex.Match(s, @"^자신의 (?<s>공|방|명중|속|특공|특방)이 감소\(저하\)하지 않는다$")).Success) return "자신은 상대에 의해 「" + AbilityPhrases.Stat(m.Groups["s"].Value) + "」이 내려가지 않는다";
      return null;
    }

    // 종족치를 등급으로
    // 출처: 특성 도감
    internal static string AbilitySpeciesGrade(string s)
    {
      Match m;
      if ((m = Regex.Match(s, @"^자신의 「(?<s>[^」]+)」 종족치(?:를|가) 「?(?<v>[A-Z+]+)」?(?:로 한다|가 된다)$")).Success) return "자신의 「" + m.Groups["s"].Value + "」 종족치를 「" + m.Groups["v"].Value + "」로 한다";
      return null;
    }
  }
}
