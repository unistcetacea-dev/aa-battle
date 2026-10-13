// 피니스 칼데아 시트에서 나온 트리거 문장 변형 → 표준 트리거 문장.
// 이미 있는 트리거 모듈과 같은 뜻인데 표기만 다른 것을 표준 문장으로 바꾼다(연결: TriggerModules.Normalize).
using System;
using System.Text.RegularExpressions;

namespace AABattle
{
  public static class ChaldeaTriggers
  {
    // 정리된 트리거 절(Clean)을 받아 표준 문장을 돌려준다. 해당 없으면 null
    public static string Normalize(string clause)
    {
      string t = Regex.Replace(clause ?? "", @"\s+", " ").Trim();
      if (t.Length == 0)
        return null;
      // 「노멀」은 「노말」의 표기 흔들림
      if (t.Contains("노멀"))
        t = t.Replace("노멀", "노말");
      Match m;

      // 앞머리가 잘린 「자신이」
      m = Regex.Match(t, @"^신이 (?<r>.+)$");
      if (m.Success)
        t = "자신" + m.Groups["r"].Value;

      // 자신(아군)의 공격·기술 명중 — 1대1에서 아군은 자신(확정 47)
      m = Regex.Match(t, @"^(?:자신|아군)의 (?<k>공격|기술)(?:이)? ?명중 ?(?:했을 ?때|시에?)$");
      if (m.Success)
        return "자신의 " + m.Groups["k"].Value + "이 명중했을 때";

      // 「급소」에 맞았을 때 (따옴표 없는 표준형)
      m = Regex.Match(t, @"^자신의 (?<k>공격|기술)이 「급소」에 맞았을 때$");
      if (m.Success)
        return "자신의 " + m.Groups["k"].Value + "이 급소에 맞았을 때";

      // 교대로 장소에 나올 때 / 교대하면 — 통상·임의교대
      if (Regex.IsMatch(t, @"^아군과 교대(?:로|해서|하여) (?:장소|필드)에 (?:나올|나왔을) 때$"))
        return "자신이 임의교대로 장소에 나올 때";
      if (Regex.IsMatch(t, @"^(?:아군과 (?:임의)?교대하면|아군과 임의교대 ?시|자신을 포함한 임의교대 ?시)$"))
        return "아군과 임의교대할 때";

      // 상대를 빈사로 했을 때 = 쓰러뜨렸을 때
      if (Regex.IsMatch(t, @"^상대를 빈사로 했을 ?때$"))
        return "상대를 쓰러뜨렸을 때";
      if (Regex.IsMatch(t, @"^자신(?:이|은)? 나무열매를 소모했을 때$"))
        return "자신이 「나무열매」를 사용했을 때";
      m = Regex.Match(t, @"^「(?<m>[^」]+)」로 상대를 빈사로 했을 ?때$");
      if (m.Success)
        return "자신의 「" + m.Groups["m"].Value + "」 기술로 상대를 쓰러뜨렸을 때";

      // 죽어내밀기로 상대가 나왔을 때
      if (Regex.IsMatch(t, @"^「죽어내밀기」로 상대가 나왔을 때$"))
        return "상대가 죽어내밀기로 장소에 나왔을 때";

      // 주어가 빠진 상태 조건: 「난동부리기」상태일 때 → 자신
      m = Regex.Match(t, @"^「(?<s>[^」]+)」 ?상태일 때$");
      if (m.Success && StateEffects.Find(m.Groups["s"].Value) != null)
        return "자신이 「" + m.Groups["s"].Value + "」 상태일 때";

      // 속도 비교
      if (Regex.IsMatch(t, @"^상대보다 (?:속도가|「속」이|「속도」가) 높을 때$"))
        return "자신의 「속도」가 상대보다 높을 때";

      // 트레이너 이름 (성별과 구분)
      m = Regex.Match(t, @"^트레이너가 「(?<n>[^」]+)」일 때$");
      if (m.Success && m.Groups["n"].Value != "남성" && m.Groups["n"].Value != "여성")
        return "「" + m.Groups["n"].Value + "」" + (EndsWithBatchim(m.Groups["n"].Value) ? "이" : "가") + " 트레이너일 때";

      // 상대가 공격하지 않을 때
      if (Regex.IsMatch(t, @"^상대가 공격을 (?:실시하지|하지) 않을 ?때$"))
        return "상대가 공격하지 않을 때";
      if (Regex.IsMatch(t, @"^공격을 하지 않을 ?때$"))
        return "자신이 공격하지 않을 때";

      return t == (clause ?? "").Trim() ? null : t;
    }

    static bool EndsWithBatchim(string value)
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
