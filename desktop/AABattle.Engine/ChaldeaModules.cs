// 피니스 칼데아 시트에서 나온 효과 문장 변형 → 표준 문장.
// 새 효과가 아니라 "이미 있는 모듈과 같은 뜻인데 표기만 다른" 문장을 표준 문장으로 바꾼다.
// 연결: EffectNormalizer.NormalizeUncached(Pre) · EffectModules.NormalizeBody(Normalize)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AABattle
{
  public static class ChaldeaModules
  {
    static readonly string[] Stats = { "공격", "방어", "특공", "특방", "속도", "공", "방", "속" };

    // 능력 이름 하나(괄호 쌍·쉼표 나열에 쓴다)
    const string StatName = "(?:공격|방어|특공|특방|속도|공|방|속)";

    // 앞처리: 글자만 다른 표기를 먼저 통일한다. 이후 기존 정규화가 이어서 처리한다
    public static string Pre(string clean)
    {
      string text = clean ?? "";
      if (text.Length == 0)
        return text;
      // 「노멀」은 「노말」의 표기 흔들림
      text = text.Replace("노멀", "노말");
      // 「통상교대」이외 → 「통상교대」 이외
      text = Regex.Replace(text, @"」이외의 수단", "」 이외의 수단");
      // 「방어」, 「특방」 → 「방어/특방」 (쉼표로 이은 능력 나열)
      text = Regex.Replace(text, "「(" + StatName + ")」\\s*[,、]\\s*「(" + StatName + ")」", "「$1/$2」");
      // 방어(특방) → 「방어/특방」
      text = Regex.Replace(text, "(?<=의 )(" + StatName + ")\\((" + StatName + ")\\)(?=을|를|이|가)", "「$1/$2」");
      // 「변화기술」 → 「변화」 (기술 조건 표기)
      text = Regex.Replace(text, "「(변화|물리|특수|공격)기술」의(?= 우선도)", "「$1」 기술의");
      // 배화 = 2배 강화, 반감 = 0.5배 약화
      text = Regex.Replace(text, @"배화한다$", "강화(2배)한다");
      text = Regex.Replace(text, @"(?<=을|를) 반감한다$", " 약화(0.5배)한다");
      // 아군을 「X」 상태로 한다 → 1대1에서 아군은 자신(확정 47)
      text = Regex.Replace(text, @"^(T\s*종료\s*시\s*까지\s*,?\s*)?(\d+T\s*(?:간|동안)\s*)?아군(?:을|를)\s*(\d+T\s*(?:간|동안)\s*)?(?=「[^」]+」\s*(?:상태\s*)?로 한다)", "$1$2자신을 $3");
      // 기간이 문장 가운데 있는 경우: 「X를 T 종료 시까지 Y」 → 「T종료시까지 X를 Y」
      var middle = Regex.Match(text, @"^(?<a>.+?)\s+(?:T\s*종료\s*시\s*까지)\s+(?<b>[^,]+)$");
      if (middle.Success && !text.StartsWith("T"))
        text = "T종료시까지 " + middle.Groups["a"].Value + " " + middle.Groups["b"].Value;
      return text;
    }

    static string Cond(string raw)
    {
      MoveCondition condition;
      if (!MoveCondition.TryParse(raw ?? "", out condition))
        return null;
      return condition.Empty ? "" : condition.Text + " ";
    }

    // 기술 조건 낱말(「공격」 기술 등)
    static string KindCond(string kind)
    {
      return kind == "공격" ? "「공격」 " : "";
    }

    // 표준 문장으로 바꿀 수 있으면 바꾼다. 아니면 null(Prepare를 거친 문장, 끝 마침표 없음)
    public static string Normalize(string text)
    {
      string s = Regex.Replace(PotentialParts.Clean(text ?? ""), @"\s+", " ").Trim().TrimEnd('.', '。', '．').Trim();
      if (s.Length == 0)
        return null;
      Match m;

      // 필중: 자신의 기술이 「필중」한다 / 상대에게 「필중」시킨다
      m = Regex.Match(s, @"^자신의 (?<c>(?:「[^」]+」)*)\s*(?<k>공격|기술)(?:이|은)\s*(?:상대에게\s*)?「?필중」?(?:한다|이다)$");
      if (m.Success)
      {
        string c = Cond(m.Groups["c"].Value);
        if (c != null)
          return "자신의 " + (c.Length > 0 ? c : KindCond(m.Groups["k"].Value)) + "기술은 필중이다.";
      }

      // 추격 효과 부여 + 필중
      m = Regex.Match(s, @"^자신의 (?<c>(?:「[^」]+」)*)\s*(?:공격)?기술에 「추격」\s*효과를 부여하고,?\s*상대에게 「필중」시킨다$");
      if (m.Success)
      {
        string c = Cond(m.Groups["c"].Value);
        if (c != null)
          return "자신의 " + c + "기술에 「추격」 효과를 부여한다. 자신의 " + c + "기술은 필중이다.";
      }

      // 위력 강화 + 추격 효과 부여 / 방어 관통
      m = Regex.Match(s, @"^자신의 (?<c>(?:「[^」]+」)*)\s*기술의 위력을 강화\s*\((?<r>[0-9.]+)배\)(?:하고|해),?\s*(?<rest>.+)$");
      if (m.Success)
      {
        string c = Cond(m.Groups["c"].Value);
        string rest = m.Groups["rest"].Value.Trim();
        string tail = null;
        if (Regex.IsMatch(rest, @"^「추격」\s*효과를 부여한다$"))
          tail = "자신의 " + c + "기술에 「추격」 효과를 부여한다.";
        else if (Regex.IsMatch(rest, @"^상대의 「방어」\s*등의 기술을 관통한다$"))
          tail = "자신의 " + c + "기술이 상대의 「방어」 기술을 관통한다.";
        if (c != null && tail != null)
          return "자신의 " + c + "기술의 위력을 강화(" + m.Groups["r"].Value + "배)한다. " + tail;
      }

      // 방어 등의 기술 관통 (주어·조건 생략 가능)
      m = Regex.Match(s, @"^(?:자신의 )?(?<c>(?:「[^」]+」)*)\s*(?:기술(?:이|은)\s*)?상대의 「?방어」?\s*등의 기술(?:을|도)\s*(?:관통|무시)한다$");
      if (m.Success)
      {
        string c = Cond(m.Groups["c"].Value);
        if (c != null)
          return "자신의 " + c + "기술이 상대의 「방어」 기술을 관통한다.";
      }

      m = Regex.Match(s, @"^자신의 (?<c>(?:「[^」]+」)*)\s*(?:기술(?:이|은)\s*)?상대의 「?방어」?\s*등의 기술(?:을|도)\s*(?:관통|무시|무효화)한다$");
      if (m.Success)
      {
        string c = Cond(m.Groups["c"].Value);
        if (c != null)
          return "자신의 " + c + "기술이 상대의 「방어」 기술을 관통한다.";
      }

      // 방어 기술·특성·방호 포텐셜을 한꺼번에 뚫는 문장
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

      m = Regex.Match(s, @"^상대 포켓몬과 상대 트레이너의 방호 포텐셜을 무효화한다$");
      if (m.Success)
        return "자신의 기술이 상대의 방호 포텐셜을 무시한다. 자신의 기술이 상대의 트레이너 방호 포텐셜을 무시한다.";

      // 우선도 +N (조건이 있으면 특성 문장, 없으면 일반 우선도 문장)
      m = Regex.Match(s, @"^자신의 (?<c>(?:「[^」]+」)*)\s*(?<k>변화|공격)?기술의 우선도를 「?[+＋](?<n>\d)」?\s*(?:한다|올린다)$");
      if (m.Success)
      {
        string c = Cond(m.Groups["c"].Value);
        string kind = m.Groups["k"].Value;
        if (c != null && c.Length == 0 && kind.Length > 0)
          c = "「" + kind + "」 ";
        int n = int.Parse(m.Groups["n"].Value);
        if (c != null && c.Length > 0)
          return "자신의 " + c + "기술의 우선도를 +" + n + "한다.";
        if (c != null && n >= 1 && n <= 7)
          return "자신의 기술 우선도를 " + n + " 올린다";
      }

      // 공격 이외의 대미지
      m = Regex.Match(s, @"^상대의 공격 이외의 (?:대미지|데미지)(?:로|를)\s*(?<v>빈사가 되지 않는다|쓰러지지 않는다|무시한다)$");
      if (m.Success)
        return m.Groups["v"].Value == "무시한다" ? "자신은 공격 이외의 대미지를 받지 않는다." : "자신은 공격 이외의 대미지로 빈사가 되지 않는다.";

      // 급소에 맞지 않는다
      if (Regex.IsMatch(s, @"^상대의 공격이 급소에 맞지 않는다$"))
        return "자신은 급소에 맞지 않는다.";

      return null;
    }

    // 확률 접두어까지 한 문장으로 읽어야 하는 변형(방호·회피처럼 확률이 문장 안으로 들어가는 것)
    public static string NormalizeWhole(string clean)
    {
      var m = Regex.Match(clean ?? "", @"^(?<p>드물게|드믈게|저확률로|중확률로)\s*기합으로 상대의 공격을 (?:버틴다|참는다)$");
      if (m.Success)
        return "자신은 빈사가 되는 공격을 " + (m.Groups["p"].Value == "드믈게" ? "드물게" : m.Groups["p"].Value) + " HP 1로 버틴다.";
      return null;
    }
  }
}
