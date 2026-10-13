using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AABattle
{
  // 정규화 규칙 모음(대미지). 적용 순서는 PhrasePipeline.cs에만 둔다.
  public static partial class PhraseRules
  {
    // 내보내는 공격의 위력 강화/약화 → 자신의 기술의 위력
    // 출처: 칼데아 시트
    internal static string PreAttackPower(string text)
    {
      text = Regex.Replace(text, @"^내보내는\s*(?<kind>공격|기술)의\s*위력을\s*(?<effect>강화|약화)\((?<rate>[0-9]+(?:\.[0-9]+)?)배\)(?<end>한다|시킨다)$", "자신의 「${kind}」 기술의 위력을 ${effect}(${rate}배)${end}");
      return text;
    }

    // 배화 = 2배 강화, 반감 = 0.5배 약화
    // 출처: 칼데아 시트
    internal static string PreDoubleHalve(string text)
    {
      text = Regex.Replace(text, @"배화한다$", "강화(2배)한다");
      text = Regex.Replace(text, @"(?<=을|를) 반감한다$", " 약화(0.5배)한다");
      return text;
    }

    // 위력 강화 + 추격 효과 부여 / 방어 관통
    // 출처: 칼데아 시트
    internal static string PowerPursuitPierce(string s)
    {
      Match m;
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
      return null;
    }

    // 공격 이외의 대미지
    // 출처: 칼데아 시트
    internal static string NonAttackDamage(string s)
    {
      Match m;
      m = Regex.Match(s, @"^상대의 공격 이외의 (?:대미지|데미지)(?:로|를)\s*(?<v>빈사가 되지 않는다|쓰러지지 않는다|무시한다)$");
      if (m.Success)
        return m.Groups["v"].Value == "무시한다" ? "자신은 공격 이외의 대미지를 받지 않는다." : "자신은 공격 이외의 대미지로 빈사가 되지 않는다.";
      return null;
    }

    // 급소에 맞지 않는다
    // 출처: 칼데아 시트
    internal static string NoCriticalHit(string s)
    {
      if (Regex.IsMatch(s, @"^상대의 공격이 급소에 맞지 않는다$"))
        return "자신은 급소에 맞지 않는다.";
      return null;
    }

    // 대미지를 반감하고 반드시 버틴다
    // 출처: 칼데아 3차
    internal static string HalveAndEndure(string s)
    {
      if (Regex.IsMatch(s, @"^상대의 (?:기술|공격)의 대미지를 반감해 반드시 참는다$"))
        return "자신이 받는 대미지를 약화(0.5배)한다. 자신은 빈사가 되는 공격을 HP 1로 버틴다.";
      return null;
    }

    // 공격 이외의 대미지를 받지 않는다(자신)
    // 출처: 칼데아 3차
    internal static string NonAttackDamageSelf(string s)
    {
      if (Regex.IsMatch(s, @"^(?:자신은\s*)?공격 이외의 (?:대미지|데미지)(?:를|로)\s*(?:받지 않는다|무효화한다|무시한다)$"))
        return "자신은 공격 이외의 대미지를 받지 않는다.";
      return null;
    }

    // 반동 대미지 무시
    // 출처: 칼데아 3차
    internal static string RecoilIgnore(string s)
    {
      if (Regex.IsMatch(s, @"^(?:자신의 )?기술의 반동\s*(?:대미지|데미지)를 (?:무시한다|무효화한다)$"))
        return "자신의 기술의 반동 대미지를 받지 않는다.";
      return null;
    }

    // 받는 대미지 반감(아군이어도 자신)
    // 출처: 칼데아 3차
    internal static string HalveReceived(string s)
    {
      Match m;
      m = Regex.Match(s, @"^(?<d>T\s*종료\s*시\s*까지\s*)?(?:자신|아군)이 받는 (?:대미지|데미지)를 (?:반감한다|약화\(0\.5배\)한다)$");
      if (m.Success)
        return (m.Groups["d"].Success ? "T종료시까지 " : "") + "자신이 받는 대미지를 약화(0.5배)한다.";
      return null;
    }

    // 타입일치 배율: 「2.0」배가 된다
    // 출처: 칼데아 3차
    internal static string StabRate(string s)
    {
      Match m;
      m = Regex.Match(s, @"^자신의 타입일치기술의 보정이 「(?<v>[0-9.]+)」배가 된다$");
      double rate;
      if (m.Success && double.TryParse(m.Groups["v"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out rate))
        return "자신의 기술의 타입 일치 배율을 「" + rate.ToString("0.##", CultureInfo.InvariantCulture) + "배」로 한다.";
      return null;
    }
  }
}
