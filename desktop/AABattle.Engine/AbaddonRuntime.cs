// 실프 인헨스먼츠 효과의 판정 지점(계속 효과): 기술 변형 · 위력 약화 · 치유 봉인 · 최초 기술 우선도 · 회복 반납.
// 계속 효과는 상시 포텐셜의 표준 문장과 사건으로 걸린 문장(ActiveModifier)을 함께 읽는다.
// 연결: Mechanics.JudgedMove · Mechanics.Priority · PotentialModifiers.Power · StateCure.Cure · StateTicks.EndTurn
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AABattle
{
  public static class AbaddonRuntime
  {
    public const string ContactGrantKeyword = "접촉」 효과를 부여";
    static readonly Regex ContactGrant = new Regex(@"^자신의 (?<c>(?:「[^」]+」)*)\s*기술에 「접촉」 효과를 부여한다$");
    static readonly Regex FoeType = new Regex(@"^상대의 (?<c>(?:「[^」]+」)*)\s*기술을 「(?<t>[^」]+)」 타입으로 한다$");
    static readonly Regex LowerPower = new Regex(@"^상대의 능력 하락 1랭크마다 상대 기술의 위력을 약화\((?<r>[0-9.]+)배\)한다$");
    static readonly Regex SealCure = new Regex(@"^(?<t>자신|상대)의 「상태이상」은 치유되지 않는다$");
    static readonly Regex FirstPriority = new Regex(@"^아군이 장소에 나와 최초로 내보내는 (?<c>(?:「[^」]+」)*)\s*기술의 우선도를 (?<n>[1-7]) 올릴 수 있다$");

    [ThreadStatic]
    static int depth;

    static string Clean(string text)
    {
      return Regex.Replace(PotentialParts.Clean(text ?? ""), @"\s+", " ").Trim().TrimEnd('.', '。', '．').Trim();
    }

    // 상시 포텐셜 문장 + 사건으로 걸린 문장
    static IEnumerable<string> Lines(Fighter owner, Fighter opponent, Rules rules, string keyword)
    {
      if (owner == null || owner.Data == null)
        yield break;
      foreach (string line in PotentialModifiers.Sentences(owner, opponent, rules, keyword))
        yield return Clean(line);
      foreach (string line in PotentialModifiers.EventSentences(owner, keyword))
        yield return Clean(line);
    }

    static Fighter FoeOf(Fighter f)
    {
      var scope = EffectScope.Current;
      return (scope == null ? null : scope.Opponent(f)) ?? f.FieldOpponent;
    }

    static Rules ScopeRules()
    {
      var scope = EffectScope.Current;
      return scope == null ? null : scope.Rules;
    }

    static TrainerRecord TrainerOf(Fighter f)
    {
      var scope = EffectScope.Current;
      if (scope == null || scope.States == null || scope.Fighters == null)
        return null;
      int side = Array.IndexOf(scope.Fighters, f);
      return side < 0 || side >= scope.States.Length || scope.States[side] == null ? null : scope.States[side].Trainer;
    }

    // 지금 내는 기술의 우선도(포텐셜 보정 포함)가 n 이상인가
    public static bool MovePriorityAtLeast(Fighter owner, Fighter opponent, Rules rules, int n)
    {
      if (owner == null || !owner.Conditions.Acting || owner.Conditions.ActingMove == null || depth > 0)
        return false;
      depth++;
      try
      {
        var foe = opponent ?? FoeOf(owner);
        return Mechanics.Priority(owner, owner.Conditions.ActingMove, foe, TrainerOf(owner), TrainerOf(foe)) >= n;
      }
      finally
      {
        depth--;
      }
    }

    // 기술 변형: 자신의 기술에 「접촉」 부여, 상대의 기술을 지정 타입으로
    public static Move JudgeMove(Fighter a, Fighter b, Move move, Rules rules)
    {
      if (a == null || move == null)
        return move;
      foreach (string line in Lines(a, b, rules, "접촉"))
      {
        var m = ContactGrant.Match(line);
        MoveCondition condition;
        if (!m.Success || !MoveCondition.TryParse(m.Groups["c"].Value, out condition) || !condition.Matches(move, a, 1))
          continue;
        if (Mechanics.Contact(move))
          break;
        move = move.Copy();
        move.tags = (move.tags ?? new string[0]).Concat(new[] { "접촉" }).ToArray();
        break;
      }

      if (b != null)
        foreach (string line in Lines(b, a, rules, "타입"))
        {
          var m = FoeType.Match(line);
          MoveCondition condition;
          if (!m.Success || !MoveCondition.TryParse(m.Groups["c"].Value, out condition) || !condition.Matches(move, a, 1))
            continue;
          move = move.Copy();
          move.types = new[] { m.Groups["t"].Value };
          break;
        }

      return move;
    }

    // 상대(방어 측)가 가진 「능력 하락 1랭크마다 위력 약화」: 공격 측의 하락한 랭크 합계에 비례(선형)
    public static double PowerFactor(Fighter a, Fighter b, Rules rules)
    {
      if (a == null || b == null || rules != null && rules.Critical)
        return 1;
      int lowered = 0;
      for (int i = 1; i <= 5 && i < a.Stages.Length; i++)
        lowered += Math.Max(0, -a.Stages[i]);
      if (lowered == 0)
        return 1;
      foreach (string line in Lines(b, a, rules, null))
      {
        var m = LowerPower.Match(line);
        if (!m.Success)
          continue;
        double rate = double.Parse(m.Groups["r"].Value, System.Globalization.CultureInfo.InvariantCulture);
        return Math.Max(0, 1 - (1 - rate) * lowered);
      }

      return 1;
    }

    static bool IsStatusName(string name)
    {
      return name == "상태이상" || name == "혼란" || Mechanics.Statuses.Contains(name) && name != "정상";
    }

    // 「상태이상은 치유되지 않는다」. 턴 경과 자연 해제(잠듦 카운트·얼음 해동·혼란)는 치유가 아니므로 막지 않는다
    public static bool Sealed(Fighter f, string name, List<string> log)
    {
      if (f == null || !IsStatusName(name))
        return false;
      var foe = FoeOf(f);
      var rules = ScopeRules();
      bool blocked = Lines(foe, f, rules, "상태이상").Any(x => SealCure.Match(x).Success && SealCure.Match(x).Groups["t"].Value == "상대")
        || Lines(f, foe, rules, "상태이상").Any(x => SealCure.Match(x).Success && SealCure.Match(x).Groups["t"].Value == "자신");
      if (blocked && log != null)
        log.Add(f.Data.name + ": 「상태이상」은 치유되지 않는다");
      return blocked;
    }

    // 아군이 장소에 나와 처음 내보내는 기술의 우선도 보정(할 수 있다 = 손해가 없으므로 항상 쓴다)
    public static int FirstActionPriority(Fighter a, Move move, TrainerRecord trainer)
    {
      if (a == null || move == null || a.Conditions.MovedSinceEntry)
        return 0;
      var foe = FoeOf(a);
      var sentences = new List<string>(Lines(a, foe, ScopeRules(), "최초로"));
      var owner = trainer ?? TrainerOf(a);
      if (owner != null)
        foreach (var p in TrainerTriggers.Matching(owner, TriggerStructures.AlwaysLabel).Concat(TrainerTriggers.Matching(owner, TriggerStructures.FieldLabel)).Distinct())
          foreach (string raw in EffectNormalizer.NormalizeLines(p.effect ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            sentences.Add(Clean(raw));
      int total = 0;
      foreach (string line in sentences)
      {
        var m = FirstPriority.Match(line);
        MoveCondition condition;
        if (m.Success && MoveCondition.TryParse(m.Groups["c"].Value, out condition) && condition.Matches(move, a, 1))
          total += int.Parse(m.Groups["n"].Value);
      }

      return total;
    }

    // 아이스번: 필드에 나온 T 동안 그 포켓몬의 기술 우선도를 -1한다. 「불꽃」「얼음」 타입이 나오면 해제된다
    public static void IceBurnEntry(Fighter entering, SideFieldState hazards, List<string> log)
    {
      if (entering == null || hazards == null || !hazards.IceBurn)
        return;
      var types = entering.Data.types ?? new string[0];
      if (types.Contains("불꽃") || types.Contains("얼음"))
      {
        hazards.IceBurn = false;
        log.Add(entering.Data.name + ": 「불꽃」「얼음」 타입이 나와 아이스번이 해제되었다");
        return;
      }

      entering.Conditions.PriorityModifier -= 1;
      entering.Conditions.PriorityModifierTurns = 1;
      log.Add(entering.Data.name + ": 아이스번으로 이번 T 기술의 우선도가 -1된다");
    }

    // 초근성: 「중확률로 자신은 공격 이외의 대미지를 받지 않는다」. 간접 대미지를 받을 때마다 확률을 굴린다
    // (확률 접두어가 붙은 문장은 상시 문장 목록에 없으므로 여기서 직접 읽는다)
    public static bool IgnoresIndirectByChance(Fighter f)
    {
      var scope = EffectScope.Current;
      if (f == null || f.Data == null || scope == null || scope.Rng == null)
        return false;
      var foe = FoeOf(f);
      var rules = scope.Rules ?? new Rules();
      foreach (var p in (f.Data.potentials ?? new Potential[0]).Where(x => x != null && (x.description ?? "").Contains("공격 이외")))
      {
        if (!PotentialModifiers.Continuous(p, f, foe, rules))
          continue;
        string trigger, effect;
        PotentialLibrary.Split(p.description ?? "", out trigger, out effect);
        foreach (string line in EffectNormalizer.NormalizeLines(effect.Length > 0 ? effect : p.description).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
          string probability, body;
          if (!EffectNormalizer.TryProbabilityPrefix(line.Trim(), out probability, out body) || Clean(body) != AbilityPhrases.MagicGuard)
            continue;
          double? rate = PotentialProbability.Rate(probability);
          if (rate.HasValue && scope.Rng() < rate.Value)
            return true;
        }
      }

      return false;
    }

    // 턴 끝: 회복한 만큼 반납(빈사가 되지 않는다)
    public static void EndTurn(Fighter[] fighters, Rules rules, List<string> log)
    {
      for (int i = 0; i < fighters.Length; i++)
      {
        var f = fighters[i];
        if (f == null || f.Conditions.Repay <= 0)
          continue;
        int amount = Math.Min(f.Conditions.Repay, f.HP - 1);
        f.Conditions.Repay = 0;
        if (f.HP <= 0 || amount <= 0)
          continue;
        Mechanics.Hurt(f, fighters[fighters.Length - 1 - i] ?? f, rules, amount, "회복한 체력의 반납", log, false);
      }
    }
  }
}
