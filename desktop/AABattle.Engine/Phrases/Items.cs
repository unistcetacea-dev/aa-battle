using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AABattle
{
  // 정규화 규칙 모음(소지품). 적용 순서는 PhrasePipeline.cs에만 둔다.
  public static partial class PhraseRules
  {
    // 소지품 파괴·약탈 보호
    // 출처: 칼데아 시트
    internal static string ItemProtect(string s)
    {
      if (Regex.IsMatch(s, @"^자신의 소지품이 파괴\(약탈\)되지 않는다$"))
        return "자신의 소지품은 파괴되거나 약탈되지 않는다.";
      return null;
    }
  }
}
