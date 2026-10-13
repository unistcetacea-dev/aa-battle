using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AABattle
{
  // 정규화 규칙 모음(공통). 적용 순서는 PhrasePipeline.cs에만 둔다.
  public static partial class PhraseRules
  {
    // 능력 이름 하나(괄호 쌍·쉼표 나열에 쓴다)
    // 출처: 칼데아 시트
    internal const string StatName = "(?:공격|방어|특공|특방|속도|공|방|속)";

    // 능력 이름 + 체력(종족치 랭크 문장에 쓴다)
    // 출처: 칼데아 3차
    internal const string StatOrHp = "(?:공격|방어|특공|특방|속도|공|방|속|체력)";

    // 기술 조건 문자열을 표준 조건 글자로(해석 못 하면 null)
    // 출처: 칼데아 시트
    internal static string Cond(string raw)
    {
      MoveCondition condition;
      if (!MoveCondition.TryParse(raw ?? "", out condition))
        return null;
      return condition.Empty ? "" : condition.Text + " ";
    }

    // 기술 조건 낱말(「공격」 기술 등)
    // 출처: 칼데아 시트
    internal static string KindCond(string kind)
    {
      return kind == "공격" ? "「공격」 " : "";
    }


    // 칼데아 3차가 만든 여러 문장 표준형의 조각(다시 정규화해도 그대로이게 한다)
    // 출처: 칼데아 3차
    internal static readonly string[] OwnPieces =
    {
      @"^자신이 받는 대미지를 약화\(0\.5배\)한다$",
      @"^자신은 빈사가 되는 공격을 HP 1로 버틴다$",
      @"^자신의 상태이상을 치유한다$",
      @"^자신의 체력을 최대 HP의 1/\d+ 회복한다$",
      @"^상대로부터의 「데이터해석」을 무효화한다$",
      @"^자신의 현재 HP의 1/2을 소비한다$",
      @"^이때 가장 먼저 같은 기술에 의한 추가(?:공격을 한다|행동을 얻는다)$",
      @"^자신의 「[^」]+」 상태를 해제한다$",
      @"^이때 가장 마지막에 「기술:[^」]+」 기술에 의한 추가행동을 얻는다$"
    };

    internal static bool IsOwnPiece(string piece)
    {
      string p = piece.Trim().TrimEnd('.', '。', '．').Trim();
      return DamageVariantModules.Classify(p).Length > 0 || OwnPieces.Any(x => Regex.IsMatch(p, x));
    }

    // 「노멀」은 「노말」의 표기 흔들림
    // 출처: 칼데아 시트
    internal static string PreNormalSpelling(string text)
    {
      text = text.Replace("노멀", "노말");
      return text;
    }

    // 자신(아군)의 → 자신의 (싱글에서 아군은 자신)
    // 출처: 칼데아 시트
    internal static string PreAllyIsSelf(string text)
    {
      text = Regex.Replace(text, @"자신\(아군\)(?=의)", "자신");
      return text;
    }

    // 이미 표준형인 여러 문장은 마침표만 맞춘다
    // 출처: 칼데아 3차
    internal static string AlreadyCanonical(string s)
    {
      var pieces = Regex.Split(s, @"(?<=[.．。])\s+");
      if (pieces.Length > 1 && pieces.All(IsOwnPiece))
        return string.Join(" ", pieces.Select(x => x.TrimEnd('.', '。', '．') + "."));
      return null;
    }
  }
}
