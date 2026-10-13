// 종족치·랭크·최대 HP 비율과 치유 효과
using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle
{
  public static partial class EffectModules
  {
    static string SpeciesTarget(string subject, string stats)
    {
      string s = Subj(subject);
      string st = stats.Trim();
      if (st.Replace(" ", "") == "전종족치" || st.Replace(" ", "") == "전")
        return s + "의 전종족치";
      return s + "의 " + (st.StartsWith("「") ? st : "「" + st + "」");
    }

    static string NormalizeSpecies(string text)
    {
      if (!text.Contains("종족치"))
        return null;
      const string Who = @"(?:(?<s>자신|상대)의\s*)?";
      const string What = @"(?<st>「[^」]+」|전\s*종족치)";
      Match m;
      m = Regex.Match(text, "^" + Who + What + @"(?:의)?\s*종족치(?:를|을)\s*「(?<g>[A-S][A-S]*[+-]?)」\s*(?:로|으로)\s*한다$");
      if (m.Success)
        return SpeciesTarget(m.Groups["s"].Value, m.Groups["st"].Value) + " 종족치를 「" + m.Groups["g"].Value + "」로 한다.";
      m = Regex.Match(text, "^" + Who + What + @"(?:의)?\s*종족치(?:를|을)\s*「(?<g>[A-S][A-S]*[+-]?)」\s*까지\s*올린다$");
      if (m.Success)
        return SpeciesTarget(m.Groups["s"].Value, m.Groups["st"].Value) + " 종족치를 「" + m.Groups["g"].Value + "」까지 올린다.";
      m = Regex.Match(text, "^" + Who + What + @"(?:의)?\s*종족치(?:를|을|가|이)\s*(?<n>[12])\s*랭크\s*(?:올린다|오른다)$");
      if (m.Success)
        return SpeciesTarget(m.Groups["s"].Value, m.Groups["st"].Value) + " 종족치를 " + m.Groups["n"].Value + "랭크 올린다.";
      m = Regex.Match(text, "^" + Who + @"(?<st>「[^」]+」)(?:의\s*종족치)?(?:을|를)\s*가장\s*(?<h>높은|낮은)\s*종족치(?:와\s*동랭크)?로\s*한다$");
      if (m.Success)
        return SpeciesTarget(m.Groups["s"].Value, m.Groups["st"].Value) + " 종족치를 가장 " + m.Groups["h"].Value + " 종족치로 한다.";
      // 「동랭크까지 올린다」는 더 높은 쪽에 맞춘다(낮추지 않는다)
      m = Regex.Match(text, "^" + Who + What + @"(?:의)?\s*종족치(?:를|을)\s*(?:자신의\s*)?(?<src>「[^」]+」)(?:의)?\s*종족치(?:와\s*동랭크|와\s*같은\s*랭크)까지\s*올린다$");
      if (m.Success)
        return SpeciesTarget(m.Groups["s"].Value, m.Groups["st"].Value) + " 종족치를 자신의 " + m.Groups["src"].Value + " 종족치까지 올린다.";
      m = Regex.Match(text, "^" + Who + What + @"(?:의)?\s*종족치(?:를|을)\s*(?:자신의\s*)?(?<src>「[^」]+」)(?:의)?\s*종족치와\s*(?:동랭크까지\s*올린다|같게\s*한다|동랭크로\s*한다)$");
      if (m.Success)
        return SpeciesTarget(m.Groups["s"].Value, m.Groups["st"].Value) + " 종족치를 자신의 " + m.Groups["src"].Value + " 종족치와 같게 한다.";
      m = Regex.Match(text, "^" + Who + What + @"(?:의)?\s*종족치(?:를|을|가)\s*(?:아군\s*)?트레이너의?\s*「(?<a>지시|육성|통솔|능력)」(?:과|와)\s*(?:같은\s*랭크|동랭크)(?:까지\s*올린다|로\s*한다|가\s*된다)$");
      if (m.Success)
        return SpeciesTarget(m.Groups["s"].Value, m.Groups["st"].Value) + " 종족치를 트레이너 「" + m.Groups["a"].Value + "」 등급까지 올린다.";
      return null;
    }

    static string NormalizeFraction(string text)
    {
      Match m;
      string cure = "";
      var withCure = Regex.Match(text, @"^(?<head>.+?회복)(?:해|하고|하여)\s*,?\s*상태이상을\s*(?:치유|회복|해제)\s*(?:한다|시킨다)$");
      if (withCure.Success)
      {
        text = withCure.Groups["head"].Value + "한다";
        cure = "#";
      }

      m = Regex.Match(text, @"^(?:(?<t>자신|아군|상대)(?:의|은|는)\s*)?(?:(?:최대|전체)\s*)?체력(?:의|을|를|이|가)?\s*(?:최대\s*HP의\s*)?1/(?<n>\d+)(?:을|를|만큼)?\s*회복(?:한다|시킨다)$");
      if (m.Success)
      {
        string t = Subj(m.Groups["t"].Value);
        string result = t + "의 체력을 최대 HP의 1/" + m.Groups["n"].Value + " 회복한다.";
        return cure.Length > 0 ? result + " " + t + "의 상태이상을 치유한다." : result;
      }

      if (cure.Length > 0)
        return null;
      m = Regex.Match(text, @"^(?<t>자신|상대)의\s*체력(?:을|를)\s*1/(?<n>\d+)\s*감소(?:시킨다|한다)$");
      if (m.Success)
        return m.Groups["t"].Value + "에게 최대 HP의 1/" + m.Groups["n"].Value + " 대미지를 준다.";
      m = Regex.Match(text, @"^(?<t>상대|자신)에게\s*(?:(?:최대|전체)\s*)?(?:체력|HP)의\s*1/(?<n>\d+)의\s*(?:「[^」]*」\s*)?(?:대미지|데미지)를\s*준다$");
      if (m.Success)
        return m.Groups["t"].Value + "에게 최대 HP의 1/" + m.Groups["n"].Value + " 대미지를 준다.";
      return null;
    }

    public static bool TrySpecies(string text, out SpeciesEffect effect)
    {
      effect = null;
      string t = PotentialParts.Clean(text ?? "").Trim().TrimEnd('.', '。').Trim();
      var m = Regex.Match(t, @"^(?<s>자신|상대)의\s*(?:(?<all>전종족치)|「(?<st>[^」]+)」)\s*종족치를\s*(?<rest>.+)$");
      if (!m.Success)
      {
        m = Regex.Match(t, @"^(?<s>자신|상대)의\s*(?<all>전종족치)를\s*(?<rest>.+)$");
        if (!m.Success)
          return false;
      }

      var e = new SpeciesEffect
      {
        Subject = m.Groups["s"].Value
      };
      if (m.Groups["all"].Success && m.Groups["all"].Value.Length > 0)
        e.Stats = new[]
        {
          0,
          1,
          2,
          3,
          4,
          5
        };
      else
      {
        var list = StatList(m.Groups["st"].Value);
        if (list == null || list.Any(x => x > 5))
          return false;
        e.Stats = list;
      }

      string rest = m.Groups["rest"].Value.Trim();
      Match r;
      if ((r = Regex.Match(rest, @"^「(?<g>[A-S][A-S]*[+-]?)」\s*범위의\s*「(?<v>\d+)」(?<op>로 한다|까지 올린다|까지 내린다)$")).Success)
      {
        e.Grade = r.Groups["g"].Value;
        e.Value = int.Parse(r.Groups["v"].Value);
        e.Operation = r.Groups["op"].Value == "로 한다" ? "변경" : r.Groups["op"].Value.Contains("올린") ? "상승" : "하락";
      }
      else if ((r = Regex.Match(rest, @"^「(?<g>[A-S][A-S]*[+-]?)」(?<op>로 한다|까지 올린다|까지 내린다)$")).Success)
      {
        e.Grade = r.Groups["g"].Value;
        e.Operation = r.Groups["op"].Value == "로 한다" ? "변경" : r.Groups["op"].Value.Contains("올린") ? "상승" : "하락";
      }
      else if ((r = Regex.Match(rest, @"^「(?<v>\d+)」로 하여 (?<n>[12])랭크 올린다$")).Success)
      {
        e.Operation = "랭크";
        e.Ranks = int.Parse(r.Groups["n"].Value);
      }
      else if ((r = Regex.Match(rest, @"^(?<n>[12])랭크 올린다$")).Success)
      {
        e.Operation = "랭크";
        e.Ranks = int.Parse(r.Groups["n"].Value);
      }
      else if ((r = Regex.Match(rest, @"^「(?<v>\d+)」로 한다$")).Success)
      {
        e.Operation = "수치";
        e.Value = int.Parse(r.Groups["v"].Value);
      }
      else if ((r = Regex.Match(rest, @"^가장 (?<h>높은|낮은) 종족치로 한다$")).Success)
        e.Operation = r.Groups["h"].Value == "높은" ? "최고 복사" : "최저 복사";
      else if ((r = Regex.Match(rest, @"^자신의 「(?<src>[^」]+)」 종족치와 같게 한다$")).Success)
      {
        e.Operation = "복사";
        e.Source = StatIndex(r.Groups["src"].Value);
        if (e.Source < 0 || e.Source > 5)
          return false;
      }
      else if ((r = Regex.Match(rest, @"^자신의 「(?<src>[^」]+)」 종족치까지 올린다$")).Success)
      {
        e.Operation = "복사 상승";
        e.Source = StatIndex(r.Groups["src"].Value);
        if (e.Source < 0 || e.Source > 5)
          return false;
      }
      else if ((r = Regex.Match(rest, @"^트레이너 「(?<a>지시|육성|통솔|능력)」 등급까지 올린다$")).Success)
      {
        e.Operation = "트레이너";
        e.Aptitude = r.Groups["a"].Value;
      }
      else
        return false;
      if (e.Grade.Length > 0)
      {
        try
        {
          SpeciesStatRanks.LabelRange(e.Grade);
        }
        catch
        {
          return false;
        }
      }

      effect = e;
      return true;
    }

    public static bool TryFraction(string text, out FractionEffect effect)
    {
      effect = null;
      string t = PotentialParts.Clean(text ?? "").Trim();
      bool canFaint = !t.Contains("빈사로 할 수 없다");
      t = Regex.Replace(t, @"\s*이 대미지로는 빈사로 할 수 없다\.?$", "").Trim().TrimEnd('.', '。').Trim();
      Match m;
      if ((m = Regex.Match(t, @"^(?<t>자신|상대|아군 1체|아군 전체|아군)의 체력을 최대 HP의 1/(?<n>\d+) 회복한다$")).Success)
      {
        effect = new FractionEffect
        {
          Target = m.Groups["t"].Value,
          Action = "회복",
          Denominator = int.Parse(m.Groups["n"].Value)
        };
      }
      else if ((m = Regex.Match(t, @"^(?<t>자신|상대|아군 1체|아군 전체|아군)에게 최대 HP의 1/(?<n>\d+) 대미지를 준다$")).Success)
      {
        effect = new FractionEffect
        {
          Target = m.Groups["t"].Value,
          Action = "대미지",
          Denominator = int.Parse(m.Groups["n"].Value),
          CanFaint = canFaint
        };
      }
      else if ((m = Regex.Match(t, @"^(?:자신의\s*)?(?:최대\s*)?체력(?:의|을|를)?\s*(?:최대 HP의\s*)?1/(?<n>\d+)(?:을|를|만큼)?\s*소비(?:해서|해|하여|하고|한다)$")).Success)
      {
        effect = new FractionEffect
        {
          Target = "자신",
          Action = "소비",
          Denominator = int.Parse(m.Groups["n"].Value)
        };
      }
      else
      {
        string target, action;
        int n;
        if (!EffectStructures.TryFraction(t, out target, out action, out n))
          return false;
        effect = new FractionEffect
        {
          Target = target,
          Action = action,
          Denominator = n,
          CanFaint = canFaint
        };
      }

      return effect.Denominator > 0;
    }

    public static bool TryCure(string text, out string target)
    {
      var m = Regex.Match(PotentialParts.Clean(text ?? "").Trim().TrimEnd('.', '。').Trim(), @"^(?<t>자신|상대|아군 1체|아군 전체|아군)의 상태이상을 치유한다$");
      target = m.Success ? m.Groups["t"].Value : "";
      return m.Success;
    }

    public static bool TryRank(string text, out RankEffect effect)
    {
      effect = null;
      string t = PotentialParts.Clean(text ?? "").Trim().TrimEnd('.', '。').Trim();
      var m = Regex.Match(t, "^" + Subject + @"의\s*(?<what>「[^」]+」|임의의 능력|랜덤한 능력치|가장 높은 능력|가장 낮은 능력|전능력치)(?:이|가|을|를)\s*(?<d>매우 크게|크게)?\s*(?<dir>오른다|내린다)$");
      if (!m.Success)
        return false;
      var e = new RankEffect
      {
        Subject = Subj(m.Groups["s"].Value)
      };
      int amount = m.Groups["d"].Value == "매우 크게" ? 3 : m.Groups["d"].Value == "크게" ? 2 : 1;
      e.Amount = m.Groups["dir"].Value == "내린다" ? -amount : amount;
      string what = m.Groups["what"].Value;
      if (what == "임의의 능력")
        e.Mode = "임의";
      else if (what == "랜덤한 능력치")
        e.Mode = "랜덤";
      else if (what == "가장 높은 능력")
        e.Mode = "최고";
      else if (what == "가장 낮은 능력")
        e.Mode = "최저";
      else if (what == "전능력치")
      {
        e.Mode = "전능력치";
        e.Stats = new[]
        {
          1,
          2,
          3,
          4,
          5
        };
      }
      else
      {
        var list = StatList(what.Trim('「', '」'));
        if (list == null || list.Any(x => x < 1))
          return false;
        e.Stats = list;
      }

      effect = e;
      return true;
    }
  }
}
