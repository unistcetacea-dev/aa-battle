using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AABattle
{
  // 정규화 규칙 모음(시점·추가행동). 적용 순서는 PhrasePipeline.cs에만 둔다.
  public static partial class PhraseRules
  {
    // 추가공격(= 방금 낸 공격을 한 번 더)과 현재 HP 절반 소비는 버서크의 표준 문장 쌍을 쓴다
    // 출처: 칼데아 3차
    const string HalfCost = "자신의 현재 HP의 1/2을 소비한다.";
    const string Repeat = "이때 가장 먼저 같은 기술에 의한 추가공격을 한다.";
    const string RepeatAction = "이때 가장 먼저 같은 기술에 의한 추가행동을 얻는다.";


    // 턴 조건의 위치 명시 추가행동 → 추가행동 executor 문장
    // 출처: 칼데아 시트
    internal static string PreExtraActionPosition(string text)
    {
      text = Regex.Replace(text, @"^(?:아군이\s*)?(?<when>T의\s*최후에|가장\s*먼저)\s*「(?!(?:변화기술)」)(?:기능확장\()?(?<move>[^)」]+)\)?」에\s*의한\s*추가행동권을\s*얻는다$", m =>
        "가장 " + (Regex.IsMatch(m.Groups["when"].Value, @"먼저") ? "먼저" : "마지막에") + " 「기술:" + m.Groups["move"].Value.Trim() + "」 기술에 의한 추가행동을 얻는다");
      return text;
    }

    // 기간이 문장 가운데 있는 경우 → 앞으로
    // 출처: 칼데아 시트
    internal static string PreMiddleDuration(string text)
    {
      var middle = Regex.Match(text, @"^(?<a>.+?)\s+(?:T\s*종료\s*시\s*까지)\s+(?<b>[^,]+)$");
      if (middle.Success && !text.StartsWith("T"))
        text = "T종료시까지 " + middle.Groups["a"].Value + " " + middle.Groups["b"].Value;
      return text;
    }

    // 체력을 절반으로 하고 추가공격
    // 출처: 칼데아 3차
    internal static string CostExtraHalfHp(string s)
    {
      if (Regex.IsMatch(s, @"^자신의 체력을 절반으로 하고,? ?추가공격을 (?:할 수 있다|한다)$"))
        return HalfCost + " " + Repeat;
      return null;
    }

    // 상태를 해제하고 추가공격
    // 출처: 칼데아 3차
    internal static string CostExtraState(string s)
    {
      Match m;
      m = Regex.Match(s, @"^자신의 「(?<st>[^」]+)」 ?상태를 해제(?:하여|해|하고),? ?추가공격을 (?:할 수 있다|한다)$");
      if (m.Success)
        return "자신의 「" + m.Groups["st"].Value + "」 상태를 해제한다. " + RepeatAction;
      return null;
    }

    // T의 최후에 상태를 해제해 기술을 내보낸다
    // 출처: 칼데아 3차
    internal static string CostExtraLast(string s)
    {
      Match m;
      m = Regex.Match(s, @"^T의 최후에 「(?<st>[^」]+)」를 해제해 「(?<mv>[^」]+)」을 내보낼 수 있다$");
      if (m.Success)
        return "자신의 「" + m.Groups["st"].Value + "」 상태를 해제한다. 이때 가장 마지막에 「기술:" + m.Groups["mv"].Value + "」 기술에 의한 추가행동을 얻는다.";
      return null;
    }
  }
}
