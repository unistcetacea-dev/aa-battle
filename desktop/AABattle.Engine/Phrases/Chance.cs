using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AABattle
{
  // 정규화 규칙 모음(확률). 적용 순서는 PhrasePipeline.cs에만 둔다.
  public static partial class PhraseRules
  {
    // 확률 접두어까지 한 문장으로 읽는 변형(기합: 남은 HP 비례)
    // 출처: 칼데아 시트
    internal static string HpScaledEndureWhole(string clean)
    {
      var m = Regex.Match(clean ?? "", @"^(?<p>드물게|드믈게|저확률로|중확률로)\s*기합으로 상대의 공격을 (?:버틴다|참는다)(?:[.．。]\s*남은 체력이 많을수록 발동하기 어렵다)?$");
      if (m.Success)
      {
        bool hpScaled = clean.Contains("남은 체력이 많을수록 발동하기 어렵다");
        return "자신은 빈사가 되는 공격을 " + (m.Groups["p"].Value == "드믈게" ? "드물게" : m.Groups["p"].Value) + " HP 1로 버틴다" + (hpScaled ? " (남은 HP 비례)" : "") + ".";
      }
      return null;
    }

    // N할로 상태 부여
    // 출처: 특성 도감
    internal static string AbilityChanceState(string s)
    {
      Match m;
      if ((m = Regex.Match(s, @"^(?<n>\d+)할로 (?<r>(?:상대를 )?「[^」]+」\s*상태로 한다)$")).Success) return m.Groups["n"].Value + "할로 " + (m.Groups["r"].Value.StartsWith("상대") ? m.Groups["r"].Value : "상대를 " + m.Groups["r"].Value);
      return null;
    }
  }
}
