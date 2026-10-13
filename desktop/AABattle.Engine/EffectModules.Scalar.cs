// 능력과 기술의 수치 배율 해석
using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle
{
  public static partial class EffectModules
  {
    // 배율 동사 → (강화/약화, 배율)
    static bool TryVerb(string verb, out string action, out double rate)
    {
      action = "";
      rate = 0;
      string v = verb.Replace(" ", "");
      Match m;
      if (Regex.IsMatch(v, @"^반감(한다|시킨다|된다)$"))
      {
        action = "약화";
        rate = .5;
        return true;
      }

      if (Regex.IsMatch(v, @"^배화(한다|된다)$"))
      {
        action = "강화";
        rate = 2;
        return true;
      }

      m = Regex.Match(v, @"^(?<a>강화|약화|완화|저하|감소)\(" + Rate + @"배\)(한다|시킨다|된다)$");
      if (m.Success)
      {
        rate = double.Parse(m.Groups["r"].Value, CultureInfo.InvariantCulture);
        action = m.Groups["a"].Value == "강화" ? "강화" : "약화";
        return rate > 0;
      }

      m = Regex.Match(v, "^" + Rate + @"배(가된다|로한다|로된다|가된다)$");
      if (m.Success)
      {
        rate = double.Parse(m.Groups["r"].Value, CultureInfo.InvariantCulture);
        action = rate >= 1 ? "강화" : "약화";
        return rate > 0;
      }

      return false;
    }

    // ===== 표준 문장 해석 =====
    public static bool TryScalar(string text, out ScalarEffect effect)
    {
      effect = null;
      if (AbilityRemaining4Modules.TryProportional(text, out effect))
        return true;
      string target, action;
      double rate;
      if (!EffectStructures.TryScalar(text, out target, out action, out rate))
        return false;
      target = Regex.Replace(target.Trim(), @"\s+", " ");
      var e = new ScalarEffect
      {
        Rate = action == "강화" ? rate : rate,
        Text = text
      };
      if (action == "약화" && rate > 1)
        e.Rate = 1 / rate;
      Match m;
      MoveCondition c;
      if ((m = Regex.Match(target, "^" + Subject + @"의\s*(?<c>.*?)\s*기술의\s*(?<stage>위력|명중)$")).Success && MoveCondition.TryParse(m.Groups["c"].Value, out c))
      {
        e.Subject = Subj(m.Groups["s"].Value);
        e.Stage = m.Groups["stage"].Value;
        e.Condition = c;
        effect = e;
        return true;
      }

      if ((m = Regex.Match(target, "^" + Subject + @"(?:이|가)\s*(?<dir>주는|받는)\s*(?:(?<c>.*?)\s*기술의\s*)?대미지$")).Success && MoveCondition.TryParse(m.Groups["c"].Value, out c))
      {
        e.Subject = Subj(m.Groups["s"].Value);
        e.Stage = m.Groups["dir"].Value + " 대미지";
        e.Condition = c;
        effect = e;
        return true;
      }

      if ((m = Regex.Match(target, "^" + Subject + @"의\s*(?<st>「[^」]+」|전능력치|가장 높은 능력|가장 낮은 능력|공격|방어|특공|특방|속도|명중|회피)$")).Success)
      {
        e.Subject = Subj(m.Groups["s"].Value);
        e.Stage = "능력치";
        string st = m.Groups["st"].Value;
        if (st == "전능력치")
          e.Stats = new[]
          {
            1,
            2,
            3,
            4,
            5
          };
        else if (st == "가장 높은 능력")
          e.HighestStat = true;
        else if (st == "가장 낮은 능력")
          e.LowestStat = true;
        else
        {
          var list = StatList(st.Trim('「', '」'));
          if (list == null || list.Any(x => x < 1 || x > 7))
            return false;
          e.Stats = list;
        }

        effect = e;
        return true;
      }

      return false;
    }
  }
}
