// 이름 지정 포텐셜 무효화 모듈.
//  상대의 『X』(『Y』…)을 무효화한다.                       이름으로 지정(○는 한 글자 와일드카드: 『대○회피』)
//  상대의 「효과 종류」 효과를 가진 포텐셜을 무효화한다.     효과 종류로 지정(지원: 공격을 회피한다)
//  자신의 『X』을 무효화한다.                               자기 포텐셜(올드 타입 등)
// 무효화된 포텐셜은 발동하지 않는다(트리거·상시 모두). 무효화 문장을 가진 포텐셜이 장소에 있는 동안만 유효하다.
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle
{
  public static class PotentialNullify
  {
    public const string Kind = "포텐셜 무효화";
    public const string ByEffectKind = "효과 종류별 포텐셜 무효화";

    static readonly Regex Named = new Regex(@"^(?<t>자신|상대)의 (?<names>(?:『[^』]+』)+)(?:을|를|이|가)? 무효화한다$");
    static readonly Regex ByEffect = new Regex(@"^(?<t>자신|상대)의 「(?<e>[^」]+)」 효과를 가진 포텐셜(?:을|를)? 무효화한다$");

    // 효과 종류 이름 → 대상 포텐셜 판정(효과 문장 한 줄)
    static readonly KeyValuePair<string, Func<string, bool>>[] EffectKinds =
    {
      new KeyValuePair<string, Func<string, bool>>("공격을 회피한다", IsEvasionLine)
    };

    public static IEnumerable<string> EffectKindNames
    {
      get
      {
        return EffectKinds.Select(x => x.Key);
      }
    }

    static bool IsEvasionLine(string line)
    {
      string kind;
      MoveCondition condition;
      double? rate;
      return PotentialProtection.TryGuard(line, out kind, out condition, out rate) && kind == "회피";
    }

    static string Clean(string text)
    {
      return Regex.Replace(PotentialParts.Clean(text ?? ""), @"\s+", " ").Trim().TrimEnd('.', '。', '．').Trim();
    }

    // 수집 원문 변형 → 표준 문장. 아니면 null
    public static string Normalize(string text)
    {
      string s = Clean(text);
      if (s.Length == 0 || s.IndexOf("무효화한다", StringComparison.Ordinal) < 0)
        return null;
      // 「…종이 이 개체뿐이기에 『올드 타입』을 무효화한다」: 앞의 설정 문장은 플레이버, 자기 포텐셜의 무효화
      var flavor = Regex.Match(s, @"^.*?(?:뿐이기에|와는 다른 생태를 갖는다\.?|과는 다른 생태를 갖는다\.?|다르기에)\s*(?<rest>『[^』]+』(?:을|를)? 무효화한다)$");
      if (flavor.Success)
        s = "자신의 " + flavor.Groups["rest"].Value;
      var byEffect = Regex.Match(s, @"^(?<t>자신|상대)의 「(?<e>[^」]+)」(?:의)? ?효과를 가진 포텐셜(?:을|를)? 무효화한다$");
      if (byEffect.Success)
        return byEffect.Groups["t"].Value + "의 「" + byEffect.Groups["e"].Value + "」 효과를 가진 포텐셜을 무효화한다.";
      var named = Regex.Match(s, @"^(?:(?<t>자신|상대)의 )?(?<names>(?:『[^』]+』)+)(?:을|를|이|가)? 무효화한다$");
      if (named.Success)
        return (named.Groups["t"].Success ? named.Groups["t"].Value : "상대") + "의 " + named.Groups["names"].Value + "을 무효화한다.";
      return null;
    }

    public static string Classify(string sentence)
    {
      string s = Clean(sentence);
      if (ByEffect.IsMatch(s))
        return ByEffectKind;
      return Named.IsMatch(s) ? Kind : "";
    }

    sealed class Rule
    {
      public bool Self;
      public List<Regex> Names = new List<Regex>();
      public Func<string, bool> EffectLine;
    }

    static Rule Parse(string sentence)
    {
      string s = Clean(sentence);
      var named = Named.Match(s);
      if (named.Success)
      {
        var rule = new Rule { Self = named.Groups["t"].Value == "자신" };
        foreach (Match m in Regex.Matches(named.Groups["names"].Value, @"『([^』]+)』"))
          rule.Names.Add(new Regex("^" + Regex.Escape(m.Groups[1].Value.Trim()).Replace("○", ".") + "$"));
        return rule;
      }

      var byEffect = ByEffect.Match(s);
      if (byEffect.Success)
      {
        var kind = EffectKinds.FirstOrDefault(x => x.Key == byEffect.Groups["e"].Value.Trim());
        if (kind.Value != null)
          return new Rule { Self = byEffect.Groups["t"].Value == "자신", EffectLine = kind.Value };
      }

      return null;
    }

    static string NameOf(Potential p)
    {
      return Regex.Replace(p.name ?? "", @"\s*[(（]\d+[)）]$", "").Trim();
    }

    static bool Hits(Rule rule, Potential target)
    {
      if (rule.EffectLine != null)
      {
        string condition, effect;
        PotentialLibrary.Split(target.description ?? "", out condition, out effect);
        string text = effect.Length > 0 ? effect : target.description ?? "";
        return Regex.Split(EffectNormalizer.NormalizeLines(text).Replace("\r", ""), @"\n|(?<=[.．。])\s+").Any(x => x.Trim().Length > 0 && rule.EffectLine(x.Trim()));
      }

      string name = NameOf(target);
      return rule.Names.Any(r => r.IsMatch(name));
    }

    [ThreadStatic]
    static int depth;

    // holder의 포텐셜 p가 무효화됐는가. 무효화 문장을 가진 포텐셜이 (상대 쪽 또는 자신 쪽) 장소에서 성립 중일 때
    public static bool IsNullified(Fighter holder, Potential p, Fighter opponent, Rules rules)
    {
      if (holder == null || p == null || depth > 0)
        return false;
      depth++;
      try
      {
        if (opponent == null)
          opponent = PotentialQueue.OpponentOf(holder);
        var scoped = rules ?? new Rules();
        if (Nullifies(opponent, holder, p, scoped, false))
          return true;
        return Nullifies(holder, opponent, p, scoped, true);
      }
      finally
      {
        depth--;
      }
    }

    // source의 성립 중인 포텐셜 가운데 target의 p를 무효화하는 문장이 있는가
    static bool Nullifies(Fighter source, Fighter sourceOpponent, Potential p, Rules rules, bool self)
    {
      if (source == null || source.Data == null || source.HP <= 0 || !source.OnField)
        return false;
      foreach (var s in (source.Data.potentials ?? new Potential[0]).Where(x => x != null && !ReferenceEquals(x, p) && (x.description ?? "").IndexOf("무효화", StringComparison.Ordinal) >= 0))
      {
        if (!PotentialModifiers.Continuous(s, source, sourceOpponent, rules))
          continue;
        string condition, effect;
        PotentialLibrary.Split(s.description ?? "", out condition, out effect);
        string text = effect.Length > 0 ? effect : s.description ?? "";
        foreach (string line in Regex.Split(EffectNormalizer.NormalizeLines(text).Replace("\r", ""), @"\n|(?<=[.．。])\s+"))
        {
          var rule = Parse(line);
          if (rule != null && rule.Self == self && Hits(rule, p))
            return true;
        }
      }

      return false;
    }
  }
}
