using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AABattle
{
  // 트리거 정규화 규칙 모음(상태). 적용 순서는 PhrasePipeline.cs에만 둔다.
  public static partial class TriggerPhraseRules
  {
    // 주어가 빠진 상태 조건
    // 출처: 칼데아 시트(트리거)
    internal static string OmittedStateSubject(string t)
    {
      Match m;
      m = Regex.Match(t, @"^「(?<s>[^」]+)」 ?상태일 때$");
      if (m.Success && StateEffects.Find(m.Groups["s"].Value) != null)
        return "자신이 「" + m.Groups["s"].Value + "」 상태일 때";
      return null;
    }

    // 속도 비교
    // 출처: 칼데아 시트(트리거)
    internal static string SpeedCompare(string t)
    {
      if (Regex.IsMatch(t, @"^상대보다 (?:속도가|「속」이|「속도」가) 높을 때$"))
        return "자신의 「속도」가 상대보다 높을 때";
      return null;
    }

    // 트레이너 이름(성별과 구분)
    // 출처: 칼데아 시트(트리거)
    internal static string TrainerName(string t)
    {
      Match m;
      m = Regex.Match(t, @"^트레이너가 「(?<n>[^」]+)」일 때$");
      if (m.Success && m.Groups["n"].Value != "남성" && m.Groups["n"].Value != "여성")
        return "「" + m.Groups["n"].Value + "」" + (TriggerPhraseRules.EndsWithBatchim(m.Groups["n"].Value) ? "이" : "가") + " 트레이너일 때";
      return null;
    }

    // 체력이 1/N 이하가 되었을 때
    // 출처: 특성 도감
    internal static string AbilityHpBelow(string c)
    {
      Match m;
      if ((m = Regex.Match(c, @"^(?:자신의 )?(?:HP|체력)가 1/(?<n>\d) ?이하가 (?:됐을 때|되었을 때|되면)$")).Success) return "자신의 체력이 1/" + m.Groups["n"].Value + " 이하가 되었을 때";
      return null;
    }

    // 체력이 1/N 이하·이상일 때
    // 출처: 특성 도감
    internal static string AbilityHpRange(string c)
    {
      Match m;
      if ((m = Regex.Match(c, @"^(?:자신의 )?(?:HP|체력)이? ?가? ?1/(?<n>\d) ?(?<o>이하|이상)일 때$")).Success) return "자신의 체력이 1/" + m.Groups["n"].Value + " " + m.Groups["o"].Value + "일 때";
      return null;
    }

    // 체력이 가득일 때
    // 출처: 특성 도감
    internal static string AbilityHpFull(string c)
    {
      if (Regex.IsMatch(c, @"^(?:자신의 )?(?:체력이 가득일 때|체력이 가득할 때|HP가 「가득」일 때)$")) return "자신의 체력이 가득일 때";
      return null;
    }

    // 혼란·화상 상태일 때
    // 출처: 특성 도감
    internal static string AbilityStatusState(string c)
    {
      Match m;
      if ((m = Regex.Match(c, @"^(?<s>혼란|화상)상태일 때$")).Success) return "자신이 「" + m.Groups["s"].Value + "」 상태일 때";
      return null;
    }
  }
}
