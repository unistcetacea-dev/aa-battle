// 장소·설치물·교대와 상태 면역 효과
using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle
{
  public static partial class EffectModules
  {
    // 받침이 있으면 「으로」, 없거나 ㄹ 받침이면 「로」
    public static string Ro(string word)
    {
      if (string.IsNullOrEmpty(word))
        return "로";
      char ch = word[word.Length - 1];
      if (ch < '가' || ch > '힣')
        return "로";
      int last = (ch - '가') % 28;
      return last == 0 || last == 8 ? "로" : "으로";
    }

    // 장소 전개·설치물·강제교대·부유·상태이상 면역 변형 → 표준 문장
    static string NormalizePlace(string text)
    {
      Match m;
      string optional = @"(?<opt>한다|할\s*수\s*있다)";
      string Opt(Match x)
      {
        return x.Groups["opt"].Value.Contains("있다") ? "할 수 있다." : "한다.";
      }

      m = Regex.Match(text, @"^날씨를\s*(?:(?<n>\d+)\s*T\s*(?:간|동안)\s*)?「(?<w>[^」]+)」\s*(?:로|으로)\s*" + optional + "$");
      if (m.Success && Mechanics.Weathers.Contains(m.Groups["w"].Value))
        return (m.Groups["n"].Success ? m.Groups["n"].Value + "T동안 " : "") + "날씨를 「" + m.Groups["w"].Value + "」" + Ro(m.Groups["w"].Value) + " " + Opt(m);
      m = Regex.Match(text, @"^(?:필드|지형)를\s*(?:(?<n>\d+)\s*T\s*(?:간|동안)\s*)?「(?<t>[^」]+)」\s*(?:상태)?\s*(?:로|으로)\s*" + optional + "$");
      if (!m.Success)
        m = Regex.Match(text, @"^(?:필드에\s*)?(?:(?<n>\d+)\s*T\s*(?:간|동안)\s*)?「(?<t>[^」]+)」(?:을|를)\s*전개(?<opt>한다|할\s*수\s*있다)$");
      if (m.Success)
      {
        string t = m.Groups["t"].Value, n = m.Groups["n"].Success ? m.Groups["n"].Value + "T동안 " : "";
        string opt = m.Groups["opt"].Value.Contains("있다") ? "할 수 있다." : "한다.";
        if (TerrainNames.Contains(t))
          return n + "지형을 「" + t + "」" + Ro(t) + " " + opt;
        if (t == "중력" || t == "트릭룸")
          return n + "「" + t + "」을 전개" + opt;
      }

      m = Regex.Match(text, @"^적진에\s*「(?<h>스텔스록|압정뿌리기|압정|독압정|끈적끈적네트|아이스번)」(?:을|를)\s*설치" + optional + "$");
      if (m.Success)
      {
        string h = m.Groups["h"].Value == "압정" ? "압정뿌리기" : m.Groups["h"].Value;
        return "적진에 「" + h + "」을 설치" + Opt(m);
      }

      if (Regex.IsMatch(text, @"^자(?:진|신의\s*진영)의\s*설치물을\s*(?:해제|제거|철거)한다$"))
        return "자진의 설치물을 제거한다.";
      m = Regex.Match(text, @"^(?:상대를\s*)?강제교대\s*(?:시킨다|한다|(?<opt>시킬\s*수\s*있다|할\s*수\s*있다))$");
      if (m.Success)
        return "상대를 강제교대시" + (m.Groups["opt"].Success ? "킬 수 있다." : "킨다.");
      if (Regex.IsMatch(text, @"^(?:자신은\s*)?(?:상대(?:로부터의|에\s*의해서?)\s*)?(?:「강제교대」|강제교대)\s*(?:되지\s*않는다|를\s*받지\s*않는다|가\s*되지\s*않는다)$"))
        return "자신은 강제교대되지 않는다.";
      if (Regex.IsMatch(text, @"^(?:자신은\s*)?공중에\s*떠오른다$"))
        return "자신은 공중에 떠오른다.";
      m = Regex.Match(text, @"^(?:자신은\s*)?(?<w>가장\s*먼저|상대보다\s*먼저|먼저)\s*행동(?<opt>한다|할\s*수\s*있다)$");
      if (m.Success)
        return "자신은 " + (m.Groups["w"].Value.StartsWith("가장") ? "가장" : "상대보다") + " 먼저 행동" + (m.Groups["opt"].Value.Contains("있다") ? "할 수 있다." : "한다.");
      m = Regex.Match(text, @"^(?:자신은\s*)?「(?<s>[^」]+)」\s*상태(?:가|에)\s*(?:되지|걸리지)\s*않는다$");
      if (m.Success && m.Groups["s"].Value.Split('/').All(IsImmunityName))
        return "자신은 「" + m.Groups["s"].Value + "」상태가 되지 않는다.";
      return null;
    }

    // 장소 전개: kind = 날씨·지형·중력·트릭룸
    public static bool TryPlace(string text, out string kind, out string value)
    {
      kind = value = "";
      var m = Regex.Match(Clean(text), @"^(?<k>날씨|지형)을?를?\s*「(?<v>[^」]+)」(?:으로|로)\s*(?:한다|할 수 있다)$");
      if (m.Success)
      {
        kind = m.Groups["k"].Value;
        value = m.Groups["v"].Value;
        return kind == "날씨" ? Mechanics.Weathers.Contains(value) : TerrainNames.Contains(value);
      }

      m = Regex.Match(Clean(text), @"^「(?<v>중력|트릭룸|물놀이|흙놀이)」을\s*전개(?:한다|할 수 있다)$");
      if (m.Success)
      {
        kind = value = m.Groups["v"].Value;
        return true;
      }

      return false;
    }

    public static bool TryHazard(string text, out bool install, out string name)
    {
      install = false;
      name = "";
      string t = Clean(text);
      if (t == "자진의 설치물을 제거한다")
        return true;
      var m = Regex.Match(t, @"^적진에 「(?<h>[^」]+)」을 설치(?:한다|할 수 있다)$");
      if (!m.Success || !Mechanics.HazardNames.Contains(m.Groups["h"].Value))
        return false;
      install = true;
      name = m.Groups["h"].Value;
      return true;
    }

    public static bool IsForcedSwitch(string text)
    {
      return Regex.IsMatch(Clean(text), @"^상대를 강제교대시(?:킨다|킬 수 있다)$");
    }

    public static bool IsForcedSwitchImmunity(string text)
    {
      return Clean(text) == "자신은 강제교대되지 않는다";
    }

    public static int ActFirstTier(string text)
    {
      var m = Regex.Match(Clean(text), @"^자신은 (?<w>가장|상대보다) 먼저 행동(?:한다|할 수 있다)$");
      return !m.Success ? 0 : m.Groups["w"].Value == "가장" ? 2 : 1;
    }

    public static bool IsActFirst(string text)
    {
      return ActFirstTier(text) > 0;
    }

    public static bool IsFloating(string text)
    {
      return Clean(text) == "자신은 공중에 떠오른다";
    }

    static bool IsImmunityName(string name)
    {
      return name.Trim() == "상태변화" || name.Trim() == "상태이상" || MajorStatuses.Contains(name.Trim()) || StateEffects.Find(name.Trim()) != null;
    }

    public static bool TryStatusImmunity(string text, out string[] statuses)
    {
      bool opponentOnly;
      return TryStatusImmunity(text, out statuses, out opponentOnly);
    }

    public static bool TryStatusImmunity(string text, out string[] statuses, out bool opponentOnly)
    {
      var m = Regex.Match(Clean(text), @"^(?<o>상대의 기술에 의해 |상대에 의해 )?자신은 「(?<s>[^」]+)」\s*상태가 되지 않는다$");
      statuses = m.Success ? m.Groups["s"].Value.Split('/').Select(x => x.Trim()).ToArray() : new string[0];
      opponentOnly = m.Success && m.Groups["o"].Success;
      return m.Success && statuses.All(IsImmunityName);
    }
  }
}
