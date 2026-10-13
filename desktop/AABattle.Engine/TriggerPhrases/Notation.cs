using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AABattle
{
  // 트리거 정규화 규칙 모음(표기). 적용 순서는 PhrasePipeline.cs에만 둔다.
  public static partial class TriggerPhraseRules
  {
    // 「노멀」은 「노말」의 표기 흔들림
    // 출처: 칼데아 시트(트리거)
    internal static string PreNormalSpelling(string t)
    {
      if (t.Contains("노멀"))
        t = t.Replace("노멀", "노말");
      return t;
    }

    // 앞머리가 잘린 「자신이」
    // 출처: 칼데아 시트(트리거)
    internal static string PreHeadCut(string t)
    {
      Match m;
      m = Regex.Match(t, @"^신이 (?<r>.+)$");
      if (m.Success)
        t = "자신" + m.Groups["r"].Value;
      return t;
    }

    // 받침 여부(조사 이/가 선택)
    // 출처: 칼데아 시트(트리거)
    internal static bool EndsWithBatchim(string value)
    {
      for (int i = value.Length - 1; i >= 0; i--)
      {
        char c = value[i];
        if (c >= '가' && c <= '힣')
          return (c - '가') % 28 != 0;
      }

      return false;
    }
  }
}
