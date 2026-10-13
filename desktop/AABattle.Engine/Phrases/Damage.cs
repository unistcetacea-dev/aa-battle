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

    // 반동 대미지를 받지 않는다
    // 출처: 실프 인헨스먼츠
    internal static string RecoilNoDamage(string s)
    {
      Match m;
      m = Regex.Match(s, @"^「(?<n>[^」]+)」의 (?:반동 )?(?:대미지|데미지)(?:를|을) 받지 않는다$");
      if (m.Success)
      {
        string c = ConvertCondition("「" + m.Groups["n"].Value + "」");
        if (c != null)
          return "자신의 " + c + "기술의 반동 대미지를 받지 않는다.";
      }
      return null;
    }

    // 자신과 같은 타입 기술의 대미지 반감
    // 출처: 실프 인헨스먼츠
    internal static string SameTypeHalve(string s)
    {
      if (Regex.IsMatch(s, @"^자신과 같은 타입의 기술의 (?:데미지|대미지)를 (?:반감한다|약화\(0\.5배\)한다)$"))
        return "자신이 받는 「자신과 같은 타입」 기술의 대미지를 약화(0.5배)한다.";
      return null;
    }

    // 회복한 체력만큼 턴 끝에 대미지
    // 출처: 실프 인헨스먼츠
    internal static string RepayDamage(string s)
    {
      if (Regex.IsMatch(s, @"^(?:T\s*종료\s*시|T종료시)\s*회복한 체력을 (?:데미지|대미지)(?:로) 받는다$") || Regex.IsMatch(s, @"^회복한 체력만큼 턴 끝에 대미지를 입는다\(빈사 불가\)$"))
        return AbaddonModules.RepaySentence;
      return null;
    }

    // 접촉기술을 받았을 때 1/8 대미지
    // 출처: 특성 도감
    internal static string AbilityContactChip(string t)
    {
      t = Regex.Replace(t, @"상대의\s*「접촉기술」을 받았을 때,?\s*1/8의 (?:대미지|데미지)를 입는다", "상대의 「접촉」 기술을 받았을 때, 자신에게 최대 HP의 1/8 대미지를 준다.");
      return t;
    }

    // 급소 보정: 급소에 맞기 쉬워진다(C+N)
    // 출처: 특성 도감
    internal static string AbilityCriticalBonus(string t)
    {
      t = Regex.Replace(t, @"급소에 맞기 쉬워진다\.?\s*\(\s*[CＣ]\s*\+\s*(\d)\s*\)\.?", "급소에 맞기 쉬워진다(C+$1).");
      return t;
    }

    // 날씨 대미지를 받지 않는다
    // 출처: 특성 도감
    internal static string AbilityWeatherDamage(string s)
    {
      Match m;
      m = Regex.Match(s, @"^(?:자신은\s*)?(?<w>(?:「[^」]+」\s*)+)의\s*대미지를\s*받지\s*않는다$");
      if (m.Success && AbilityPhrases.QuotedNames(m.Groups["w"].Value).All(AbilityPhrases.WeatherNames.Contains)) return "자신은 " + string.Concat(AbilityPhrases.QuotedNames(m.Groups["w"].Value).Select(x => "「" + x + "」")) + "의 대미지를 받지 않는다";
      return null;
    }

    // 급소 무효
    // 출처: 특성 도감
    internal static string AbilityNoCritical(string s)
    {
      if (Regex.IsMatch(s, @"^(?:상대의 기술이 급소에 맞지 않게 된다|상대의 공격이 급소에 맞지 않는다|급소 ?무효)$")) return AbilityPhrases.NoCritical;
      return null;
    }

    // 반드시 버틴다
    // 출처: 특성 도감
    internal static string AbilitySturdy(string s)
    {
      if (Regex.IsMatch(s, @"^(?:반드시 버틴다|「빈사」 상태가 되지 않는다)$")) return AbilityPhrases.Sturdy;
      return null;
    }

    // 공격 이외의 대미지 무효
    // 출처: 특성 도감
    internal static string AbilityMagicGuard(string s)
    {
      if (Regex.IsMatch(s, @"^(?:상대의 공격 이외의 대미지를 (?:무효화한다|무시한다)|상대의 공격 이외로 대미지를 받지 않는다)$")) return AbilityPhrases.MagicGuard;
      return null;
    }

    // 상대에게 HP 1/N 대미지
    // 출처: 특성 도감
    internal static string AbilityFoeFractionDamage(string s)
    {
      Match m;
      if ((m = Regex.Match(s, @"^상대(?:에게 HP의|의 체력을) 1/(?<n>\d+)(?:만큼 대미지를 준다| ?감소한다)$")).Success) return "상대에게 최대 HP의 1/" + m.Groups["n"].Value + " 대미지를 준다";
      return null;
    }

    // 받는 대미지 N배
    // 출처: 특성 도감
    internal static string AbilityTakenDamageRate(string s)
    {
      Match m;
      if ((m = Regex.Match(s, @"^「(?<x>[^」]+)」 기술로 받는 대미지가 (?<n>\d+(?:\.\d+)?)배가 된다$")).Success) return "자신이 받는 「" + m.Groups["x"].Value + "」 기술의 대미지를 " + (double.Parse(m.Groups["n"].Value) >= 1 ? "강화" : "약화") + "(" + m.Groups["n"].Value + "배)한다";
      return null;
    }

    // 반동 대미지 무효
    // 출처: 특성 도감
    internal static string AbilityNoRecoil(string s)
    {
      if (s == "자신의 기술의 반동 대미지를 무효화한다") return AbilityPhrases.NoRecoil;
      return null;
    }
  }
}
