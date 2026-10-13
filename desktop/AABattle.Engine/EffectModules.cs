// 공통 어휘·정규화 진입점과 캐시
using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle
{
  public static partial class EffectModules
  {
    // 정규식 캐시(기본 15개)를 넘는 패턴을 쓰므로 늘린다
    static EffectModules()
    {
      if (Regex.CacheSize < 256)
        Regex.CacheSize = 256;
    }

    static Regex stateNames;
    static Regex StateNames
    {
      get
      {
        if (stateNames == null)
        {
          var names = StateEffects.Table.SelectMany(r => new[] { r.Name }.Concat(r.Aliases)).Distinct().OrderByDescending(x => x.Length).Select(Regex.Escape);
          stateNames = new Regex(@"(?<=^|[\s,을를])(?<!「)(?<n>" + string.Join("|", names) + @")(?=\s*상태(?:로|가))", RegexOptions.Compiled);
        }

        return stateNames;
      }
    }

    const string Subject = @"(?<s>자신|상대|아군(?:\s*1체|\s*전체)?)";
    const string Rate = @"(?<r>[0-9]+(?:\.[0-9]+)?)";
    public static int StatIndex(string name)
    {
      switch ((name ?? "").Replace(" ", ""))
      {
        case "체력":
        case "HP":
          return 0;
        case "공격":
        case "공":
          return 1;
        case "방어":
        case "방":
          return 2;
        case "특공":
          return 3;
        case "특방":
          return 4;
        case "속도":
        case "속":
        case "스피드":
          return 5;
        case "명중":
          return 6;
        case "회피":
          return 7;
        case "C":
        case "급소":
          return 8;
        default:
          return -1;
      }
    }

    static int[] StatList(string token)
    {
      var list = token.Split('/').Select(StatIndex).ToArray();
      return list.All(x => x >= 0) ? list : null;
    }

    static string F(double value)
    {
      return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    static string Subj(string s)
    {
      s = Regex.Replace(s ?? "자신", @"\s+", " ").Trim();
      return s.Length == 0 ? "자신" : s;
    }

    static string SubjectParticle(string s)
    {
      return s.EndsWith("신") || s.EndsWith("군") || s.EndsWith("체") ? "이" : "가";
    }

    static string Scalar(string target, string action, double rate)
    {
      return EffectNormalizer.Canonical(target, action, rate);
    }

    static string Cond(MoveCondition c)
    {
      return c.Empty ? "" : c.Text + " ";
    }

    // 수집 원문 변형 → 표준 문장. 바꿀 수 없으면 null
    static readonly string[] StatWords =
    {
      "공격",
      "방어",
      "특공",
      "특방",
      "속도",
      "명중",
      "회피"
    };
    static bool AllStats(string value)
    {
      return value.Split('/').All(x => StatIndex(x) >= 0);
    }

    static bool AllTypes(string value)
    {
      return value.Split('/').All(x => MoveCondition.TypeNames.Contains(x.Trim()));
    }

    // 표기 통일: 각주 표시 제거, 『타입』→「타입」, 이어 붙은 능력·타입 괄호 병합, 전능력→전능력치, 괄호 없는 상태 이름
    public static string Prepare(string value)
    {
      string text = (value ?? "").Trim();
      text = Regex.Replace(text, @"\s*\.?\s*\*\s*\d+\s*$", "");
      text = text.TrimEnd('.', '。', '．').Trim();
      text = Regex.Replace(text, "『([^』]+)』", m => AllTypes(m.Groups[1].Value) ? "「" + m.Groups[1].Value + "」" : m.Value);
      text = Regex.Replace(text, "「([^」]+)」\\s*「([^」]+)」(?:\\s*「([^」]+)」)?", m =>
      {
        var items = new[]
        {
          m.Groups[1].Value,
          m.Groups[2].Value,
          m.Groups[3].Value
        }.Where(x => x.Length > 0).ToArray();
        return items.All(AllStats) || items.All(AllTypes) ? "「" + string.Join("/", items) + "」" : m.Value;
      });
      text = Regex.Replace(text, "전능력(?!치)", "전능력치");
      text = Regex.Replace(text, @"「위력\s*:?\s*(\d+)」\s*(이하|이상)(?:인|의)?", "「위력 $1 $2」");
      text = Regex.Replace(text, @"위력\s*「(\d+)」\s*(이하|이상)(?:인|의)?", "「위력 $1 $2」");
      text = Regex.Replace(text, @"[.．。]\s*(이 대미지로는 빈사로 할 수 없다)", " $1");
      text = Regex.Replace(text, @"우선도가\s*「\+」\s*인", "「우선도 +」");
      text = StateNames.Replace(text, m => "「" + m.Groups["n"].Value + "」");
      return text;
    }

    public static string Normalize(string clean)
    {
      var activation = Activation.Match((clean ?? "").Trim());
      if (activation.Success)
      {
        string inner = Normalize(clean.Trim().Substring(activation.Length));
        return inner == null ? null : activation.Groups[1].Value + " " + inner;
      }

      string text = Prepare(clean);
      if (text.Length == 0)
        return null;
      // 기간 접두어는 떼었다가 다시 붙인다
      string prefix = "";
      var duration = Regex.Match(text, @"^(?:(?<n>\d+)\s*T\s*(?:간|동안)|(?<end>T\s*종료\s*시\s*까지))\s*,?\s*");
      if (duration.Success && StateEffects.Parse(text) != null)
        return text + ".";
      if (duration.Success)
      {
        prefix = duration.Groups["end"].Success ? "T종료시까지 " : duration.Groups["n"].Value + "T동안 ";
        text = text.Substring(duration.Length).Trim();
      }

      string body = NormalizeBody(text);
      return body == null ? null : prefix + body;
    }

    public static readonly string[] TerrainNames =
    {
      "그래스필드",
      "일렉트릭필드",
      "미스트필드",
      "사이코필드"
    };
    static readonly string[] MajorStatuses =
    {
      "독",
      "맹독",
      "마비",
      "화상",
      "얼음",
      "잠듦",
      "동상"
    };
    // 방호(무효화)·회피·방호 관통·데이터 해석 무효 변형 → 표준 문장. 확률은 문장 안(공격마다 판정)으로 옮긴다
    // 「자동」「선행」「1/자동/」 같은 발동 방식 접두어는 떼었다가 다시 붙인다
    static readonly Regex Activation = new Regex(@"^((?:\d+/)?(?:자동|선행|T시)(?:/\S+)?)\s+");
    static string Clean(string text)
    {
      return Regex.Replace(PotentialParts.Clean(text ?? "").Trim().TrimEnd('.', '。', '．').Trim(), @"\s+", " ");
    }

    static int Amount(string degree, string direction)
    {
      int n = degree == "매우 크게" ? 3 : degree == "크게" ? 2 : 1;
      return direction == "내린다" ? -n : n;
    }

    // 포텐셜 도감·실행기 공용: 표준 문장의 모듈 이름. 모듈이 아니면 ""
    static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> classified = new System.Collections.Concurrent.ConcurrentDictionary<string, string>();
  }
}
