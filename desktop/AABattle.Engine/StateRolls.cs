// 상태 조건(상시) + 확률 효과 포텐셜의 발동.
// 『대○저격』『대○박격』『대○반격』『대○추격』『선』 같은 포텐셜은 "적진에 X가 있을 때" 같은 상태 조건만 가지고,
// 효과 앞에 확률이 붙는다. 굴릴 사건이 없으므로 효과가 실제로 쓰이는 판정 지점에서 그때마다 확률을 굴린다.
//
// 판정 지점 표 (RollPoint)
//   Selection  기술 사용(선택) 직후, 추격·교대 판단 전   추격 효과 부여
//   Order      행동 순서 결정 직전                        우선도 증감 · 먼저 행동 · 능력치 배율 · 추가행동(반격)
//   Action     행동 하나의 판정 직전(JudgedMove 뒤)       필중·관통 부여 · 위력/대미지/명중 배율(박격)
// 성공하면 이번 판정 범위 동안만 유지되는 효과(ActiveModifier Rolled)나 순서 보정으로 건다.
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle
{
  public enum RollPoint
  {
    Selection,
    Order,
    Action
  }

  public static class StateRolls
  {
    // 확률 접두어가 붙은 문장 한 줄의 효과 종류와 판정 지점
    sealed class Kind
    {
      public string Name;
      public RollPoint Point;
      public Func<string, bool> Matches;
    }

    static readonly Kind[] Kinds =
    {
      new Kind { Name = "기술 판정 부여", Point = RollPoint.Action, Matches = AbilityMoveModules.IsEventGrant },
      new Kind { Name = "기술 단계 배율", Point = RollPoint.Action, Matches = s => StageOf(s) != null && StageOf(s) != "능력치" },
      new Kind { Name = "능력치 배율", Point = RollPoint.Order, Matches = s => StageOf(s) == "능력치" },
      new Kind { Name = "추격 효과 부여", Point = RollPoint.Selection, Matches = s => PursuitModules.Classify(s) == "추격 효과 부여" },
      new Kind { Name = "우선도", Point = RollPoint.Order, Matches = PriorityEffectRuntime.Matches },
      new Kind { Name = "먼저 행동", Point = RollPoint.Order, Matches = EffectModules.IsActFirst },
      new Kind { Name = "추가행동", Point = RollPoint.Order, Matches = s => { ExtraActionGrant g; int every; return ExtraActionRuntime.TryParse(s, out g, out every); } }
    };

    // 판정 지점 표(보고·검사용)
    public static IEnumerable<KeyValuePair<string, RollPoint>> PointTable()
    {
      return Kinds.Select(k => new KeyValuePair<string, RollPoint>(k.Name, k.Point));
    }

    // 이 효과 문장(확률 접두어 없음)을 상태 조건 확률 포텐셜이 다룰 수 있는가
    public static bool IsRollable(string sentence)
    {
      return Find(sentence) != null;
    }

    public static RollPoint? PointOf(string sentence)
    {
      var kind = Find(sentence);
      return kind == null ? (RollPoint?)null : kind.Point;
    }

    static Kind Find(string sentence)
    {
      return Kinds.FirstOrDefault(k => k.Matches(sentence));
    }

    static string StageOf(string sentence)
    {
      ScalarEffect scalar;
      if (EffectModules.TryScalar(sentence, out scalar) || AbilityCriticalFieldModules.TryFieldScalar(sentence, out scalar))
        return scalar.Stage;
      return null;
    }

    sealed class Candidate
    {
      public Potential Potential;
      public int Index;
      public string Full = "";   // 확률 접두어 포함
      public string Body = "";   // 확률 접두어 없음
      public Kind Kind;
    }

    static List<Candidate> Candidates(Fighter owner, Fighter opponent, Rules rules, RollPoint point)
    {
      var list = new List<Candidate>();
      if (owner == null || owner.Data == null || owner.HP <= 0 || !owner.OnField)
        return list;
      foreach (var p in (owner.Data.potentials ?? new Potential[0]).Where(x => x != null))
      {
        if (!PotentialModifiers.Continuous(p, owner, opponent, rules))
          continue;
        string condition, effect;
        PotentialLibrary.Split(p.description ?? "", out condition, out effect);
        string text = effect.Length > 0 ? effect : p.description ?? "";
        int index = 0;
        foreach (string raw in Regex.Split(EffectNormalizer.NormalizeLines(text).Replace("\r", ""), @"\n|(?<=[.．。])\s+"))
        {
          string line = raw.Trim();
          if (line.Length == 0)
            continue;
          int number = index++;
          string probability, body;
          if (!EffectNormalizer.TryProbabilityPrefix(line, out probability, out body))
            continue;
          if (!PotentialProbability.Rate(line).HasValue)
            continue;
          var kind = Find(body);
          if (kind == null || kind.Point != point)
            continue;
          if (AbilityModules.Ignored(p, owner, opponent ?? PotentialQueue.OpponentOf(owner), body))
            continue;
          list.Add(new Candidate { Potential = p, Index = number, Full = line, Body = body, Kind = kind });
        }
      }

      return list;
    }

    static string LabelOf(Fighter owner, Potential p)
    {
      return p.id == AbilityModules.Slot ? "〔" + p.name + "〕" : "『" + p.name + "』";
    }

    static string KeyOf(Fighter owner, Candidate c)
    {
      return PotentialKeys.For(owner.Data, c.Potential) + "#roll" + c.Index;
    }

    // 선택 직후(추격 효과 부여). commands는 양쪽 행동 선택
    public static void Selection(Database db, Fighter[] f, BattleCommand[] commands, Rules rules, Func<double> rng, List<string> log)
    {
      Run(RollPoint.Selection, db, f, commands, null, rules, rng, log);
    }

    // 행동 순서 결정 직전(우선도·먼저 행동·능력치 배율·추가행동)
    public static void Order(Database db, Fighter[] f, BattleCommand[] commands, Rules rules, Func<double> rng, List<string> log)
    {
      Run(RollPoint.Order, db, f, commands, null, rules, rng, log);
    }

    // 행동 하나의 판정 직전(필중·관통·위력/대미지/명중 배율). actor 쪽 행동이다
    public static void Action(Database db, Fighter[] f, int actor, Move move, Rules rules, Func<double> rng, List<string> log)
    {
      Run(RollPoint.Action, db, f, null, new ActionContext { Actor = actor, Move = move }, rules, rng, log);
    }

    sealed class ActionContext
    {
      public int Actor;
      public Move Move;
    }

    static void Run(RollPoint point, Database db, Fighter[] f, BattleCommand[] commands, ActionContext action, Rules rules, Func<double> rng, List<string> log)
    {
      if (f == null || f.Length != 2 || rng == null)
        return;
      for (int side = 0; side < 2; side++)
      {
        var owner = f[side];
        var opponent = f[1 - side];
        var candidates = Candidates(owner, opponent, rules, point).Where(c => Relevant(db, c, side, f, commands, action, rules)).ToList();
        if (candidates.Count == 0)
          continue;
        var rolled = PotentialProbability.Resolve(candidates.Select(c => c.Full).ToArray(), rng);
        for (int i = 0; i < candidates.Count; i++)
        {
          var c = candidates[i];
          string label = LabelOf(owner, c.Potential);
          if (!rolled[i])
          {
            if (log != null)
              log.Add(owner.Data.name + ": " + label + " 확률 판정 실패 (" + c.Body + ")");
            continue;
          }

          Apply(c, owner, opponent, side, rules, log);
        }
      }
    }

    // 지금 쓸 수 있는 효과만 굴린다(쓰일 수 없는 효과를 굴리면 중확률 보장이 어긋난다)
    static bool Relevant(Database db, Candidate c, int side, Fighter[] f, BattleCommand[] commands, ActionContext action, Rules rules)
    {
      var owner = f[side];
      string kind = c.Kind.Name;
      if (c.Kind.Point == RollPoint.Selection)
      {
        var command = commands == null || side >= commands.Length ? null : commands[side];
        if (command == null || command.IsSwitch || command.Move == null)
          return false;
        var move = Mechanics.ActionMove(db, owner, command.Move);
        return move != null && PursuitModules.GrantsMove(c.Body, owner, move);
      }

      if (c.Kind.Point == RollPoint.Order)
      {
        if (kind == "추가행동")
        {
          ExtraActionGrant grant;
          int every;
          ExtraActionRuntime.TryParse(c.Body, out grant, out every);
          return !(every > 0 && (rules == null || rules.Turn <= 0 || rules.Turn % every != 0));
        }

        var command = commands == null || side >= commands.Length ? null : commands[side];
        return command != null && !command.IsSwitch && command.Move != null;
      }

      if (action == null || action.Move == null)
        return false;
      bool attacker = side == action.Actor;
      var actor = f[action.Actor];
      if (kind == "기술 판정 부여")
        return attacker;
      ScalarEffect scalar;
      if (!EffectModules.TryScalar(c.Body, out scalar) && !AbilityCriticalFieldModules.TryFieldScalar(c.Body, out scalar))
        return false;
      bool couldMatch = new[] { .5, 1, 2 }.Any(t => scalar.Condition.Matches(action.Move, actor, t));
      if (!couldMatch)
        return false;
      if (AbilityCriticalFieldModules.FieldSubject(scalar.Subject))
        return attacker;
      bool self = scalar.Subject == "자신" || scalar.Subject.StartsWith("아군");
      if (scalar.Stage == "받는 대미지")
        return attacker ? !self : self;
      // 위력·명중·주는 대미지: 건 쪽이 공격 측이어야 자신 대상, 방어 측이어야 상대 대상
      return attacker ? self : !self;
    }

    static void Apply(Candidate c, Fighter owner, Fighter opponent, int side, Rules rules, List<string> log)
    {
      string label = LabelOf(owner, c.Potential);
      string sentence = c.Body.Trim();
      if (!sentence.EndsWith(".") && !sentence.EndsWith("。"))
        sentence += ".";
      if (log != null)
        log.Add(owner.Data.name + ": " + label + " 상태 조건 성립 · 확률 판정 성공 — " + c.Body);
      switch (c.Kind.Name)
      {
        case "기술 판정 부여":
        case "기술 단계 배율":
          PotentialModifiers.Add(owner, new ActiveModifier { Key = KeyOf(owner, c), Text = sentence, Source = label, Turns = -1, Action = true, Rolled = true });
          break;
        case "능력치 배율":
        case "추격 효과 부여":
          PotentialModifiers.Add(owner, new ActiveModifier { Key = KeyOf(owner, c), Text = sentence, Source = label, Turns = 1, Rolled = true });
          break;
        case "우선도":
          PriorityEffectRuntime.ApplyLine(sentence, owner, opponent, "상태 조건", label, log == null ? (Action<string>)null : log.Add);
          break;
        case "먼저 행동":
          owner.Conditions.ActFirst = Math.Max(owner.Conditions.ActFirst, EffectModules.ActFirstTier(sentence));
          break;
        case "추가행동":
          AddExtraAction(c, owner, side, rules, log);
          break;
      }
    }

    static void AddExtraAction(Candidate c, Fighter owner, int side, Rules rules, List<string> log)
    {
      var scope = EffectScope.Current;
      ExtraActionGrant grant;
      int every;
      if (scope == null || !ExtraActionRuntime.TryParse(c.Body, out grant, out every))
        return;
      grant.Side = side;
      grant.Owner = owner;
      grant.Source = LabelOf(owner, c.Potential);
      scope.ExtraActions.Add(grant);
      owner.Conditions.PendingAction = true;
      if (log != null)
        log.Add(owner.Data.name + ": 추가행동을 얻었다 (" + (grant.First ? "바로 다음" : "턴 마지막") + ")");
    }

    // 추격 판정(PursuitModules.Judge)이 읽는, 판정에 성공한 추격 효과 부여 문장
    public static IEnumerable<string> RolledSentences(Fighter owner, string keyword)
    {
      return PotentialModifiers.EventSentences(owner, keyword)
        .Where(text => owner.Conditions.Modifiers.Any(m => m != null && m.Rolled && m.Text == text));
    }
  }
}
