using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AABattle
{
  // 정규화 규칙 모음(기술). 적용 순서는 PhrasePipeline.cs에만 둔다.
  public static partial class PhraseRules
  {
    // 「모으기기술」 표기를 모으기 생략 실행문으로
    // 출처: 칼데아 시트
    internal static string PreChargeSkip(string text)
    {
      text = Regex.Replace(text, @"^자신의\s*「모으기\s*기술」의\s*모으기를\s*생략하고\s*내보낼\s*수\s*있다$", "자신의 「모으기」 기술의 모으기를 생략한다");
      return text;
    }

    // 기술 우선도 「+n」로 할 수 있다 → 우선도를 n 올릴 수 있다
    // 출처: 칼데아 시트
    internal static string PrePriorityRaise(string text)
    {
      text = Regex.Replace(text, @"^자신의\s*기술의\s*우선도를\s*「\+(?<n>[1-7])」로\s*할\s*수\s*있다$", "자신의 기술 우선도를 ${n} 올릴 수 있다");
      return text;
    }

    // 「변화기술」의 우선도 → 「변화」 기술의 우선도
    // 출처: 칼데아 시트
    internal static string PreMoveKindPriority(string text)
    {
      text = Regex.Replace(text, "「(변화|물리|특수|공격)기술」의(?= 우선도)", "「$1」 기술의");
      return text;
    }

    // 필중: 자신의 기술이 「필중」한다
    // 출처: 칼데아 시트
    internal static string FixedHit(string s)
    {
      Match m;
      m = Regex.Match(s, @"^자신의 (?<c>(?:「[^」]+」)*)\s*(?<k>공격|기술)(?:이|은)\s*(?:상대에게\s*)?「?필중」?(?:한다|이다)$");
      if (m.Success)
      {
        string c = Cond(m.Groups["c"].Value);
        if (c != null)
          return "자신의 " + (c.Length > 0 ? c : KindCond(m.Groups["k"].Value)) + "기술은 필중이다.";
      }
      return null;
    }

    // 추격 효과 부여 + 필중
    // 출처: 칼데아 시트
    internal static string PursuitFixedHit(string s)
    {
      Match m;
      m = Regex.Match(s, @"^자신의 (?<c>(?:「[^」]+」)*)\s*(?:공격)?기술에 「추격」\s*효과를 부여하고,?\s*상대에게 「필중」시킨다$");
      if (m.Success)
      {
        string c = Cond(m.Groups["c"].Value);
        if (c != null)
          return "자신의 " + c + "기술에 「추격」 효과를 부여한다. 자신의 " + c + "기술은 필중이다.";
      }
      return null;
    }

    // 우선도 +N (조건이 있으면 특성 문장, 없으면 일반 우선도 문장)
    // 출처: 칼데아 시트
    internal static string PriorityPlus(string s)
    {
      Match m;
      m = Regex.Match(s, @"^자신의 (?<c>(?:「[^」]+」)*)\s*(?<k>변화|공격)?기술의 우선도를 「?[+＋](?<n>\d)」?\s*(?:한다|올린다)$");
      if (m.Success)
      {
        string c = Cond(m.Groups["c"].Value);
        string kind = m.Groups["k"].Value;
        if (c != null && c.Length == 0 && kind.Length > 0)
          c = "「" + kind + "」 ";
        int n = int.Parse(m.Groups["n"].Value);
        if (c != null && c.Length > 0)
          return "자신의 " + c + "기술 우선도를 " + n + " 올린다.";
        if (c != null && n >= 1 && n <= 7)
          return "자신의 기술 우선도를 " + n + " 올린다";
      }
      return null;
    }

    // 이름을 지정한 모으기 생략
    // 출처: 칼데아 POTENTIALS.tsv — 베지터 상급전사·헨리 강어수의 헤엄
    internal static string ChargeSkipByName(string s)
    {
      if (ChargeSkipModules.IsSkip(s))
        return s + ".";
      Match m = Regex.Match(s, @"^자신의 「(?<n>[^」]+)」(?:을|를)\s*(?:「모으기」(?:를|을)\s*|모으기(?:를|을)\s*)생략하고 내보낼 수 있다$");
      if (m.Success)
        return "자신의 「기술:" + m.Groups["n"].Value + "」 기술의 모으기를 생략한다.";
      return null;
    }

    // 필중·추격 부여
    // 출처: 칼데아 3차
    internal static string FixedHitGrant(string s)
    {
      Match m;
      m = Regex.Match(s, @"^자신의 (?<k>공격기술|공격|기술)에 「필중」\s*효과를 부여한다$");
      if (m.Success)
        return "자신의 " + (m.Groups["k"].Value == "기술" ? "" : "「공격」 ") + "기술은 필중이다.";
      m = Regex.Match(s, @"^자신의 (?<k>공격기술|공격|기술)에 「추격」\s*효과를 부여한다$");
      if (m.Success)
        return "자신의 " + (m.Groups["k"].Value == "기술" ? "" : "「공격」 ") + "기술에 「추격」 효과를 부여한다.";
      return null;
    }

    // 상대가 내보내는 기술을 지정 타입으로
    // 출처: 실프 인헨스먼츠
    internal static string FoeMoveType(string s)
    {
      Match m;
      m = Regex.Match(s, @"^상대(?:가|의) 내보내는 (?<c>(?:「[^」]+」)*)\s*기술(?:이|을) 「(?<ty>[^」]+)」\s*타입(?:이 된다|으로 (?:한다|바꾼다))$");
      if (m.Success && MoveCondition.TypeNames.Contains(m.Groups["ty"].Value))
      {
        string c = ConvertCondition(m.Groups["c"].Value);
        if (c != null)
          return "상대의 " + c + "기술을 「" + m.Groups["ty"].Value + "」 타입으로 한다.";
      }
      return null;
    }

    // 접촉 효과 부여 + 방호 특성 관통
    // 출처: 실프 인헨스먼츠
    internal static string ContactPierce(string s)
    {
      Match m;
      m = Regex.Match(s, @"^「(?<n>[^」]+)」(?:이|가) 「접촉(?:기술)?」(?:이|가) 되어,? 상대의 방호 ?특성을 관통한다$");
      if (m.Success)
      {
        string c = ConvertCondition("「" + m.Groups["n"].Value + "」");
        if (c != null)
          return "자신의 " + c + "기술에 「접촉」 효과를 부여한다. 자신의 " + c + "기술은 상대의 〔방호 특성〕을 관통한다";
      }
      return null;
    }

    // 자신의 기술 타입 변경
    // 출처: 실프 인헨스먼츠
    internal static string OwnMoveType(string s)
    {
      Match m;
      m = Regex.Match(s, @"^자신의 (?<c>(?:「[^」]+」)+)의 타입을 「(?<ty>[^」]+)」\s*타입으로 한다$");
      if (m.Success)
      {
        string type = m.Groups["ty"].Value == "다크" ? "악" : m.Groups["ty"].Value;
        string c = ConvertCondition(m.Groups["c"].Value);
        if (c != null && MoveCondition.TypeNames.Contains(type))
          return "자신의 " + c + "기술의 타입을 「" + type + "」" + EffectModules.Ro(type) + " 한다.";
      }
      return null;
    }

    // 타입 추가
    // 출처: 특성 도감
    internal static string AbilityAddType(string s)
    {
      Match m;
      if ((m = Regex.Match(s, @"^자신의 타입에 「?(?<t>[^」\s]+?)」?(?:을|를) 추가한다$")).Success) return "자신의 타입에 「" + m.Groups["t"].Value + "」를 추가한다";
      return null;
    }

    // 조건 기술의 우선도 +N
    // 출처: 특성 도감
    internal static string AbilityConditionPriority(string s)
    {
      Match m;
      if ((m = Regex.Match(s, @"^자신의 「(?<c>[^」]+)」 (?:타입 )?기술의 우선도를 [+＋](?<n>\d)한다$")).Success) return "자신의 「" + m.Groups["c"].Value + "」 기술 우선도를 " + m.Groups["n"].Value + " 올린다";
      return null;
    }

    // 타입이 내보내는 기술과 같은 타입으로 변화
    // 출처: 특성 도감
    internal static string AbilityMatchMoveType(string s)
    {
      if (s == "자신이 기술을 내보낼 때,자신의 타입이 내보내는 기술과 같은 타입으로 변화한다" ||
          Regex.IsMatch(s, @"^자신이 기술을 내보낼 때,\s*자신의 타입이 내보내는 기술과 같은 타입으로 변화한다$")) return AbilityPhrases.MatchMoveType;
      return null;
    }

    // 기술의 타입을 X로 변경
    // 출처: 특성 도감
    internal static string AbilityMoveTypeChange(string s)
    {
      Match m;
      m = Regex.Match(s, @"^자신의\s*(?<c>(?:「[^」]+」\s*)*)\s*(?:타입\s*)?기술(?:을|이)\s*「(?<t>[^」]+)」\s*타입(?:으로|로|이)\s*(?:변경한다|된다)$");
      MoveCondition condition;
      if (m.Success && MoveCondition.TryParse(m.Groups["c"].Value, out condition) && MoveCondition.TypeNames.Contains(m.Groups["t"].Value))
       return "자신의 " + (condition.Empty ? "" : condition.Text + " ") + "기술의 타입을 「" + m.Groups["t"].Value + "」" + EffectModules.Ro(m.Groups["t"].Value) + " 한다";
      return null;
    }
  }
}
