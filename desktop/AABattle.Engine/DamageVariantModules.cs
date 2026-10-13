// 칼데아 시트의 대미지 문장 변형을 표준 문장으로 바꾸고, 타입을 단 대미지를 실행한다.
// 표준 문장: 「상대에게 최대 HP의 1/N 「X」 타입 대미지를 준다.」 — 타입을 단 대미지는 타입 상성을 받는다(사용자 결정).
// 타입 없는 대미지는 기존 「최대 HP의 1/N」 모듈이 맡는다. F(실프 남은 모듈)의 같은 표준 문장과 하나로 합친다.
// 연결: ChaldeaModules.Normalize(끝) · EffectModules.Classify · PotentialExecutors(Register) · ModuleCatalog(RegisterRoleEffects 옆)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AABattle
{
  public static class DamageVariantModules
  {
    public const string Kind = "타입 대미지";
    const string FaintTail = "이 대미지로는 빈사로 할 수 없다";
    // 최대 HP의 1/N 앞에 붙는 낱말(최대 체력의 / 상대 체력의 / HP의 …)
    const string HpWord = @"(?:(?:최대|전체)\s*)?(?:체력|HP)의\s*";
    const string Fraction = @"「?1/(?<n>\d+)」?";
    const string TypeWord = @"(?:「(?<ty>[^」]+)」|(?<ty>[가-힣]{1,4}))\s*(?:타입\s*)?";
    const string DamageWord = @"(?:대미지|데미지)";

    static readonly Regex Typed = new Regex(
      @"^(?<t>상대|자신)에게\s*(?:" + HpWord + @")?" + Fraction + @"의\s*" + TypeWord + DamageWord + @"(?:를|을)\s*(?:준다|입힌다)$");
    // 대상 낱말 없이 시작하는 형: 최대 체력의 1/8의 「전기」데미지를 준다 / 상대 체력의 1/8의 …
    static readonly Regex Bare = new Regex(
      @"^(?:(?<t>상대)\s*)?" + HpWord + Fraction + @"의\s*" + TypeWord + DamageWord + @"(?:를|을)\s*(?:준다|입힌다)$");
    // 「1/8」의 전기 대미지를 상대에게 준다
    static readonly Regex Postposed = new Regex(
      @"^" + Fraction + @"의\s*" + TypeWord + DamageWord + @"(?:를|을)\s*(?<t>상대|자신)에게\s*(?:준다|입힌다)$");
    // 표준 문장(실행기와 분류가 읽는다)
    static readonly Regex Standard = new Regex(
      @"^(?<t>자신|상대)에게 최대 HP의 1/(?<n>\d+) 「(?<ty>[^」]+)」 타입 대미지를 준다$");

    static bool IsType(string name)
    {
      return Array.IndexOf(MoveCondition.TypeNames, name) >= 0;
    }

    // 「… 이 대미지로는 빈사로 할 수 없다」 꼬리를 떼어 낸다
    static string StripTail(string text, out bool noFaint)
    {
      noFaint = text.Contains("빈사로 할 수 없다");
      return Regex.Replace(text, @"[.．。]?\s*이 대미지로는 빈사로 할 수 없다\.?$", "").Trim().TrimEnd('.', '。', '．').Trim();
    }

    static string Build(string target, string denominator, string type, bool noFaint)
    {
      string sentence = target + "에게 최대 HP의 1/" + denominator + (type.Length > 0 ? " 「" + type + "」 타입" : "") + " 대미지를 준다";
      return noFaint ? sentence + " " + FaintTail + "." : sentence + ".";
    }

    // 표준 문장으로 바꾼다. 해당 없으면 null (Prepare를 거친 문장)
    public static string Normalize(string text)
    {
      string s = Regex.Replace(PotentialParts.Clean(text ?? ""), @"\s+", " ").Trim();
      if (s.Length == 0)
        return null;
      bool noFaint;
      s = StripTail(s, out noFaint);

      // 상대의 데이터 해석을 막고 대미지를 준다 → 두 문장
      var combined = Regex.Match(s, @"^(?<a>상대로부터의 「데이터\s*해석」을 무효화)하고,?\s*(?<b>.+)$");
      if (combined.Success)
      {
        string second = Normalize(combined.Groups["b"].Value + (noFaint ? " " + FaintTail : ""));
        if (second != null)
          return "상대로부터의 「데이터해석」을 무효화한다. " + second;
      }

      // 표준 문장은 그대로(다시 정규화해도 같다)
      Match m = Standard.Match(s);
      if (m.Success)
        return Build(m.Groups["t"].Value, m.Groups["n"].Value, m.Groups["ty"].Value, noFaint);
      m = Typed.Match(s);
      if (!m.Success)
        m = Postposed.Match(s);
      if (!m.Success)
        m = Bare.Match(s);
      if (!m.Success)
        return null;
      string type = m.Groups["ty"].Success ? m.Groups["ty"].Value.Trim() : "";
      if (type.Length > 0 && !IsType(type))
        return null;
      string target = m.Groups["t"].Success && m.Groups["t"].Value.Length > 0 ? m.Groups["t"].Value : "상대";
      return Build(target, m.Groups["n"].Value, type, noFaint);
    }

    public static bool TryParse(string line, out string target, out int denominator, out string type, out bool canFaint)
    {
      target = "";
      type = "";
      denominator = 0;
      bool noFaint;
      string s = StripTail(Regex.Replace(PotentialParts.Clean(line ?? ""), @"\s+", " ").Trim(), out noFaint);
      var m = Standard.Match(s);
      canFaint = !noFaint;
      if (!m.Success)
        return false;
      target = m.Groups["t"].Value;
      type = m.Groups["ty"].Value;
      denominator = int.Parse(m.Groups["n"].Value);
      return denominator > 0 && IsType(type);
    }

    public static string Classify(string line)
    {
      string target, type;
      int denominator;
      bool canFaint;
      return TryParse(line, out target, out denominator, out type, out canFaint) ? Kind : "";
    }

    public static void Register()
    {
      PotentialExecutors.Register(Kind, c => Classify(c.Line).Length > 0, Run);
    }

    // 타입 상성 배율(대상의 타입에 대한 표를 곱한다). 표가 없으면 1
    public static double Effectiveness(Fighter source, Fighter target, string type, Rules rules)
    {
      var scope = EffectScope.Current;
      var db = scope == null ? null : scope.Db;
      if (db == null || db.chart == null || target == null || target.Data == null || target.Data.types == null)
        return 1;
      var probe = new Move { name = "타입 대미지", category = "특수", types = new[] { type }, target = "상대", accuracy = 100, tags = new string[0], effect = "" };
      try
      {
        return Mechanics.TypeEffect(db, source ?? target, target, probe, rules ?? new Rules());
      }
      catch (Exception)
      {
        return target.Data.types.Aggregate(1.0, (v, d) => v * (db.chart.ContainsKey(d) && db.chart[d].ContainsKey(type) ? db.chart[d][type] : 1));
      }
    }

    static EffectOutcome Run(EffectCall c)
    {
      string targetName, type;
      int denominator;
      bool canFaint;
      if (!TryParse(c.Line, out targetName, out denominator, out type, out canFaint))
        return EffectOutcome.Failed;
      var target = targetName == "상대" ? c.Opponent : c.Owner;
      if (target == null || target.HP <= 0)
        return EffectOutcome.Failed;
      var other = ReferenceEquals(target, c.Owner) ? c.Opponent ?? target : c.Owner;
      var rules = c.Rules ?? new Rules();
      var log = c.Log ?? new List<string>();
      if (AbilityPhrases.IgnoresIndirect(target))
      {
        c.Say(target.Data.name + ": 간접 대미지 무효로 대미지를 받지 않는다");
        return EffectOutcome.Failed;
      }

      double effect = Effectiveness(c.Owner, target, type, rules);
      if (effect <= 0)
      {
        c.Say(target.Data.name + ": 「" + type + "」 타입 대미지는 효과가 없다");
        return EffectOutcome.Failed;
      }

      int max = Mechanics.MaxHP(target, other, rules);
      int amount = Math.Max(1, (int)Math.Floor(FractionalHpRule.Amount(max, denominator) * effect));
      if (!canFaint)
        amount = Math.Min(amount, target.HP - 1);
      if (amount <= 0)
        return EffectOutcome.Failed;
      Mechanics.Hurt(target, other, rules, amount, c.Source.Label + " 「" + type + "」", log);
      return EffectOutcome.Success;
    }
  }
}
