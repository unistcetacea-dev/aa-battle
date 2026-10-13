// 선택 효과 「~할 수 있다」를 직설형 「~한다」로 바꾼다.
// 실행기는 직설형 표준 문장만 읽는다. 선택 여부는 큐의 OptionalEffectPrompt가 묻는다.
using System;
using System.Text.RegularExpressions;

namespace AABattle
{
  public static class OptionalForm
  {
    static readonly Regex Ending = new Regex(@"^(?<head>.*\S)\s*수\s*있다$");

    // 직설형 문장. 「수 있다」로 끝나지 않거나 어미를 바꿀 수 없으면 null
    public static string Direct(string line)
    {
      string text = (line ?? "").Trim().TrimEnd('.', '。', '．').Trim();
      var match = Ending.Match(text);
      if (!match.Success)
        return null;
      string head = match.Groups["head"].Value.TrimEnd();
      if (head.Length == 0)
        return null;
      char last = head[head.Length - 1];
      // 받침이 ㄹ인 음절(할·릴·올·될…)은 ㄴ으로 바꾼다: 내릴→내린, 부여할→부여한
      if (last != '을' && last >= '가' && last <= '힣' && (last - '가') % 28 == 8)
      {
        char changed = (char)(last - 8 + 4);
        string body = head.Substring(0, head.Length - 1);
        if (changed == '한')
          body = body.TrimEnd();   // 「변경 할」처럼 띄어 쓴 경우
        return body + changed + "다.";
      }

      // 받침이 있는 어간 + 을: 받을→받는다
      if (last == '을' && head.Length >= 2)
        return head.Substring(0, head.Length - 1) + "는다.";
      return null;
    }
  }
}
