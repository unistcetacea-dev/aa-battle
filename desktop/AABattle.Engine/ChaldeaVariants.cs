// 칼데아 3차: 효과 문장 변형 중 기존 표준 문장으로 바뀌는 것(선택 효과·기능확장·추가행동·기타).
// 새 효과가 아니라 표기만 다른 문장을 표준 문장으로 바꾼다. 연결: ChaldeaModules.Normalize(끝)
using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AABattle
{
  public static class ChaldeaVariants
  {
    // 추가공격(= 방금 낸 공격을 한 번 더)과 현재 HP 절반 소비는 버서크의 표준 문장 쌍을 쓴다
    const string HalfCost = "자신의 현재 HP의 1/2을 소비한다.";
    const string Repeat = "이때 가장 먼저 같은 기술에 의한 추가공격을 한다.";
    const string RepeatAction = "이때 가장 먼저 같은 기술에 의한 추가행동을 얻는다.";

    const string Stat = "(?:공격|방어|특공|특방|속도|공|방|속|체력)";

    static readonly string[] OwnPieces =
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

    static bool IsOwnPiece(string piece)
    {
      string p = piece.Trim().TrimEnd('.', '。', '．').Trim();
      return DamageVariantModules.Classify(p).Length > 0 || OwnPieces.Any(x => Regex.IsMatch(p, x));
    }

    // 정리된 효과 문장(끝 마침표 없음)을 받아 표준 문장을 돌려준다. 해당 없으면 null
    public static string Normalize(string s)
    {
      Match m;

      // 이미 이 파일이 만든 여러 문장 표준형이면 다시 정규화해도 같다(마침표만 맞춘다)
      var pieces = Regex.Split(s, @"(?<=[.．。])\s+");
      if (pieces.Length > 1 && pieces.All(IsOwnPiece))
        return string.Join(" ", pieces.Select(x => x.TrimEnd('.', '。', '．') + "."));

      // 이름을 지정한 모으기 생략: 자신의 「솔라빔」(을|를) (「모으기」를) 모으기를 생략하고 내보낼 수 있다
      // 이름 한정은 아직 없으므로 「모으기」 기술 전체의 모으기 생략과 같게 읽는다(보고: 사용자 판단 필요)
      m = Regex.Match(s, @"^자신의 「[^」]+」(?:을|를)\s*(?:「모으기」(?:를|을)\s*|모으기(?:를|을)\s*)생략하고 내보낼 수 있다$");
      if (m.Success)
        return "자신의 「모으기」 기술의 모으기를 생략한다.";

      // 상대의 공격 대미지를 반감하고 반드시 버틴다 = 받는 대미지 약화(0.5배) + 반드시 버팀
      if (Regex.IsMatch(s, @"^상대의 (?:기술|공격)의 대미지를 반감해 반드시 참는다$"))
        return "자신이 받는 대미지를 약화(0.5배)한다. 자신은 빈사가 되는 공격을 HP 1로 버틴다.";

      // 공격 이외의 대미지를 받지 않는다(자신)
      if (Regex.IsMatch(s, @"^(?:자신은\s*)?공격 이외의 (?:대미지|데미지)(?:를|로)\s*(?:받지 않는다|무효화한다|무시한다)$"))
        return "자신은 공격 이외의 대미지를 받지 않는다.";

      // 반동 대미지 무시
      if (Regex.IsMatch(s, @"^(?:자신의 )?기술의 반동\s*(?:대미지|데미지)를 (?:무시한다|무효화한다)$"))
        return "자신의 기술의 반동 대미지를 받지 않는다.";

      // 받는 대미지 반감(주어가 아군이어도 1대1에서는 자신)
      m = Regex.Match(s, @"^(?<d>T\s*종료\s*시\s*까지\s*)?(?:자신|아군)이 받는 (?:대미지|데미지)를 (?:반감한다|약화\(0\.5배\)한다)$");
      if (m.Success)
        return (m.Groups["d"].Success ? "T종료시까지 " : "") + "자신이 받는 대미지를 약화(0.5배)한다.";

      // 상태이상을 치유하고 회복
      m = Regex.Match(s, @"^자신의 상태이상을 해제해,? ?최대 체력의 1/(?<n>\d+)을 회복한다$");
      if (m.Success)
        return "자신의 상태이상을 치유한다. 자신의 체력을 최대 HP의 1/" + m.Groups["n"].Value + " 회복한다.";

      // 종족치 랭크 한 칸
      m = Regex.Match(s, "^자신의 (?<st>" + Stat + @") 종족치를 「(?<n>\d)」랭크 올린다$");
      if (m.Success)
        return "자신의 「" + m.Groups["st"].Value + "」 종족치를 " + m.Groups["n"].Value + "랭크 올린다.";

      // 필중·추격 부여
      m = Regex.Match(s, @"^자신의 (?<k>공격기술|공격|기술)에 「필중」\s*효과를 부여한다$");
      if (m.Success)
        return "자신의 " + (m.Groups["k"].Value == "기술" ? "" : "「공격」 ") + "기술은 필중이다.";
      m = Regex.Match(s, @"^자신의 (?<k>공격기술|공격|기술)에 「추격」\s*효과를 부여한다$");
      if (m.Success)
        return "자신의 " + (m.Groups["k"].Value == "기술" ? "" : "「공격」 ") + "기술에 「추격」 효과를 부여한다.";

      // 타입일치 배율: 「2.0」배가 된다
      m = Regex.Match(s, @"^자신의 타입일치기술의 보정이 「(?<v>[0-9.]+)」배가 된다$");
      double rate;
      if (m.Success && double.TryParse(m.Groups["v"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out rate))
        return "자신의 기술의 타입 일치 배율을 「" + rate.ToString("0.##", CultureInfo.InvariantCulture) + "배」로 한다.";

      // 상대에 의해 능력이 감소(저하)하지 않는다
      if (Regex.IsMatch(s, @"^상대에 의해 (?:자신의 )?능력이 (?:감소|저하|감소\(저하\))하지 않는다$"))
        return "자신은 상대에 의해 능력이 내려가지 않는다.";

      // 기간 + 상태 부여의 어미 변형: 2T간 상대를 「도발」상태로 된다 / 자진을 …
      m = Regex.Match(s, @"^(?<d>\d+T간 )?(?<t>상대|자신|자진)(?:를|을) 「(?<s>[^」]+)」 ?상태로 된다$");
      if (m.Success)
        return m.Groups["d"].Value + (m.Groups["t"].Value == "상대" ? "상대" : "자신") + "를 「" + m.Groups["s"].Value + "」 상태로 한다.";

      // 비용을 치르고 하는 추가공격(추가행동)
      if (Regex.IsMatch(s, @"^자신의 체력을 절반으로 하고,? ?추가공격을 (?:할 수 있다|한다)$"))
        return HalfCost + " " + Repeat;
      m = Regex.Match(s, @"^자신의 「(?<st>[^」]+)」 ?상태를 해제(?:하여|해|하고),? ?추가공격을 (?:할 수 있다|한다)$");
      if (m.Success)
        return "자신의 「" + m.Groups["st"].Value + "」 상태를 해제한다. " + RepeatAction;
      m = Regex.Match(s, @"^T의 최후에 「(?<st>[^」]+)」를 해제해 「(?<mv>[^」]+)」을 내보낼 수 있다$");
      if (m.Success)
        return "자신의 「" + m.Groups["st"].Value + "」 상태를 해제한다. 이때 가장 마지막에 「기술:" + m.Groups["mv"].Value + "」 기술에 의한 추가행동을 얻는다.";

      // 역할 임명의 조사 변형: 자신을 『엠페러』에 임명한다 → 자신을 『엠퍼러』로 임명(할 수 있다)
      m = Regex.Match(s, @"^자신을 『(?<r>[^』]+)』(?:에|으로|로) 임명(?<o>한다|할 수 있다)$");
      if (m.Success)
        return RoleModules.NormalizeEffect("자신을 『" + (m.Groups["r"].Value == "엠페러" ? "엠퍼러" : m.Groups["r"].Value) + "』로 임명" + m.Groups["o"].Value);

      return PotentialGrantModules.Normalize(s);
    }
  }
}
