using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AABattle
{
  // 트리거 정규화 규칙 모음(교대·등장). 적용 순서는 PhrasePipeline.cs에만 둔다.
  public static partial class TriggerPhraseRules
  {
    // 교대로 장소에 나올 때 / 교대하면
    // 출처: 칼데아 시트(트리거)
    internal static string SwitchEntry(string t)
    {
      if (Regex.IsMatch(t, @"^아군과 교대(?:로|해서|하여) (?:장소|필드)에 (?:나올|나왔을) 때$"))
        return "자신이 임의교대로 장소에 나올 때";
      if (Regex.IsMatch(t, @"^(?:아군과 (?:임의)?교대하면|아군과 임의교대 ?시|자신을 포함한 임의교대 ?시)$"))
        return "아군과 임의교대할 때";
      return null;
    }

    // 죽어내밀기로 상대가 나왔을 때
    // 출처: 칼데아 시트(트리거)
    internal static string ReplacementEntry(string t)
    {
      if (Regex.IsMatch(t, @"^「죽어내밀기」로 상대가 나왔을 때$"))
        return "상대가 죽어내밀기로 장소에 나왔을 때";
      return null;
    }
  }
}
