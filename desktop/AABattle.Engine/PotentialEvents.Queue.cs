// 사건 정렬·실행 계획과 포텐셜 공통 큐
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle
{
  public static partial class PotentialQueue
  {
    public const int MaxDepth = 8;
    // 효과 사이 종속: 앞 효과가 대가인 연결(대가 실패 → 뒤 효과 실패)과, 앞 효과 성공이 필요한 「이때」·「이 효과로」
    public static readonly string[] CostConnectors =
    {
      "소비해서",
      "소비해",
      "소비하여",
      "소비하고",
      "파괴해서",
      "파괴해",
      "파괴하여",
      "파괴하고",
      "버리고",
      "버려서",
      "잃고",
      "지불하고",
      "지불해"
    };
    public static readonly string[] FollowUpStarts =
    {
      "이때",
      "이 때",
      "이 효과로",
      "이효과로"
    };
    // 「임의의」 대상 선택(사용자 선택). 테스트에서 바꿔 끼울 수 있다. 기본은 첫 후보
    public static Func<PotentialInstance, List<Fighter>, Fighter> ChooseTarget = (source, candidates) => candidates.FirstOrDefault();
    [ThreadStatic]
    static int nextEvent;
    [ThreadStatic]
    static Frame current;
    sealed class Frame
    {
      public int ChainId, Depth;
      public HashSet<string> ChainFired;
      public Frame Parent;
    }

    public static Fighter OpponentOf(Fighter owner)
    {
      var scope = EffectScope.Current;
      return scope == null ? null : scope.Opponent(owner);
    }

    // 사건 묶음을 발동 순서(소유 포켓몬의 현재 속도 내림차순, 동속이면 rng)로 처리한다
    public static bool Active
    {
      get
      {
        return EffectScope.Current != null;
      }
    }

    public static void Raise(params PotentialEvent[] events)
    {
      var scope = EffectScope.Current;
      var list = (events ?? new PotentialEvent[0]).Where(x => x != null).ToList();
      if (scope != null)
        foreach (var x in list)
          if (x.Owner == null && x.Trainer != null && scope.States != null)
            for (int i = 0; i < scope.States.Length && i < scope.Fighters.Length; i++)
              if (scope.States[i] != null && ReferenceEquals(scope.States[i].Trainer, x.Trainer))
                x.Owner = scope.Fighters[i];
      list = list.Where(x => x.Owner != null).ToList();
      if (scope == null || list.Count == 0)
        return;
      var rng = scope.Rng ?? (() => .5);
      var keyed = list.Select(e => new { e, speed = Speed(scope, e.Owner), tie = list.Count > 1 ? rng() : 0 }).ToList();
      foreach (var item in keyed.OrderByDescending(x => x.speed).ThenBy(x => x.tie))
        Process(scope, item.e);
    }

    static int Speed(EffectScope scope, Fighter owner)
    {
      var other = scope.Opponent(owner) ?? owner;
      try
      {
        return Engine.Effective(owner, other, 5, scope.Rules);
      }
      catch
      {
        return 0;
      }
    }

    // 사건에 맞는 포텐셜을 찾는다(기존 매칭: 절 하나라도 성립). 추가 절이 있으면 Extra=true
    public static List<PotentialInstance> Candidates(PotentialEvent e, out List<bool> extra)
    {
      var result = new List<PotentialInstance>();
      extra = new List<bool>();
      if (e.Trainer != null)
      {
        foreach (var record in (e.Trainer.potentials ?? new List<PotentialRecord>()).Where(p => p != null && p.enabled && p.slot == "고유" && p.name.Length > 0))
        {
          var clauses = TrainerTriggers.ClausesOf(record).ToArray();
          if (!clauses.Any(e.Matches))
            continue;
          result.Add(new PotentialInstance { Owner = e.Owner, Trainer = e.Trainer, Record = record, Key = PotentialKeys.For(e.Trainer, record), Name = record.name, Slot = record.slot, Uses = record.uses, Activation = record.activation, Effect = record.effect ?? "", Description = record.effect ?? "", Clauses = clauses });
          extra.Add(false);
        }

        return result;
      }

      foreach (var p in (e.Owner.Data.potentials ?? new Potential[0]).Where(x => x != null && (e.SlotOnly.Length == 0 || x.id == e.SlotOnly)))
      {
        var clauses = TriggerStructures.Clauses(p).ToArray();
        if (!clauses.Any(e.Matches))
          continue;
        if (PotentialNullify.IsNullified(e.Owner, p, e.Other ?? OpponentOf(e.Owner), EffectScope.Current == null ? null : EffectScope.Current.Rules))
          continue;
        string condition, effect;
        PotentialLibrary.Split(p.description ?? "", out condition, out effect);
        if (effect.Length == 0)
          effect = p.description ?? "";
        result.Add(new PotentialInstance { Owner = e.Owner, Pokemon = p, Key = PotentialKeys.For(e.Owner.Data, p), Name = p.name, Slot = p.id ?? "", Uses = p.uses, Activation = p.activation, Effect = effect, Description = p.description ?? "", Clauses = clauses });
        extra.Add(Extra(e, clauses));
      }

      return result;
    }

    // 확정 48: 여러 절은 AND. 사건과 맞지 않는 절은 지금 성립하는 상태 조건이어야 한다(등장 계열 두 사건은 OR)
    static bool Extra(PotentialEvent e, string[] clauses)
    {
      var scope = EffectScope.Current;
      var matched = clauses.FirstOrDefault(e.Matches);
      foreach (var clause in clauses.Where(x => !e.Matches(x)))
      {
        if (matched != null && TriggerModules.Alternative(matched, clause))
          continue;
        if (!TriggerModules.Holds(clause, e.Owner, e.Other ?? (scope == null ? null : scope.Opponent(e.Owner)), scope == null ? null : scope.Rules))
          return true;
      }

      return false;
    }

    static void Process(EffectScope scope, PotentialEvent e)
    {
      e.Id = ++nextEvent;
      var parent = current;
      e.ChainId = parent == null ? e.Id : parent.ChainId;
      e.Depth = parent == null ? 0 : parent.Depth + 1;
      var chainFired = parent == null ? new HashSet<string>() : parent.ChainFired;
      var log = e.Log;
      if (e.Announce && e.Message.Length > 0)
        Say(log, e.Message);
      if (e.Depth > MaxDepth)
      {
        Say(log, "포텐셜 연쇄가 " + MaxDepth + "단계를 넘어 중단했다 — " + e.Message);
        return;
      }

      List<bool> extra;
      var candidates = Candidates(e, out extra);
      var plans = new List<Plan>();
      var seen = new HashSet<string>();
      for (int i = 0; i < candidates.Count; i++)
      {
        var source = candidates[i];
        if (!seen.Add(source.Key))
          continue; // a. 같은 사건에서 1회
        if (chainFired.Contains(source.Key))
        {
          Say(log, source.Label + " — 같은 연쇄에서 이미 발동해 다시 발동하지 않는다.");
          continue;
        }

        plans.Add(BuildPlan(scope, e, source, extra[i]));
      }

      // c. 확률: 같은 소유자·같은 사건 묶음으로 1회 판정(중확률 보장 포함)
      var rolling = plans.Where(p => p.Executable).ToList();
      var rolled = PotentialProbability.Resolve(rolling.Select(p => BondModules.WithoutOwnRoll(p.Source.Description) + " " + p.Source.Activation).ToArray(), scope.Rng);
      for (int i = 0; i < rolling.Count; i++)
      {
        double? rate = PotentialProbability.Rate(BondModules.WithoutOwnRoll(rolling[i].Source.Description) + " " + rolling[i].Source.Activation);
        rolling[i].ProbabilityOk = !rate.HasValue || rolled[i];
      }

      foreach (var plan in plans)
        Fire(scope, e, plan, chainFired);
    }

    sealed class Plan
    {
      public PotentialInstance Source;
      public bool Extra, Executable, ProbabilityOk = true, Optional, Declared;
      public List<Step> Steps = new List<Step>();
    }

    sealed class Step
    {
      public string Line;
      public PotentialExecutor Executor;
      public bool AfterCost, NeedsSuccess;
    }

    static Plan BuildPlan(EffectScope scope, PotentialEvent e, PotentialInstance source, bool extra)
    {
      var plan = new Plan
      {
        Source = source,
        Extra = extra
      };
      if (extra)
        return plan;
      foreach (var step in Split(source.Effect))
      {
        var call = new EffectCall
        {
          Source = source,
          Event = e,
          Line = step.Line,
          Rules = scope.Rules,
          Rng = scope.Rng
        };
        step.Executor = PotentialExecutors.Find(call);
        plan.Steps.Add(step);
      }

      // 실행할 수 있는 효과가 하나도 없거나 다른 단계가 맡는 효과뿐이면 예전처럼 로그만 남긴다
      plan.Executable = plan.Steps.Any(s => s.Executor != null && !IsDeferred(s.Executor));
      // 유대의 재사용 선택은 목록 창이 맡으므로 별도의 확인 창을 띄우지 않는다
      plan.Optional = Regex.IsMatch(PotentialParts.Clean(source.Effect), @"수\s*있다") && !plan.Steps.Any(s => s.Executor != null && s.Executor.Name.StartsWith(BondModules.ExecutorPrefix)) || plan.Steps.Any(s => s.Executor != null && s.Executor.Name == BuiltIn.AnalysisName);
      return plan;
    }

    static bool IsDeferred(PotentialExecutor executor)
    {
      return executor.Name == BuiltIn.SwitchName;
    }

    // 효과 문장을 독립 효과 목록으로 나눈다: 줄바꿈 → 문장 끝 → 대가 연결어
    static List<Step> Split(string effect)
    {
      var steps = new List<Step>();
      effect = Regex.Replace(effect ?? "", @"[.．。]\s*(이 대미지로는 빈사로 할 수 없다)", " $1");
      foreach (string raw in (effect ?? "").Replace("\r", "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
      {
        // 수집 원문 변형도 편집기 모듈 표준 문장으로 바꾼 뒤 실행기에 넘긴다
        string normalized = EffectNormalizer.Normalize(raw.Trim());
        if (normalized.Length == 0)
          normalized = raw.Trim();
        foreach (string sentence in Regex.Split(normalized, @"(?<=[.．。])\s+"))
        {
          string text = sentence.Trim();
          if (text.Length == 0)
            continue;
          // 확률은 큐가 포텐셜당 1회 판정하므로 실행기에는 확률 접두어를 뗀 문장을 넘긴다
          string probability, body;
          if (EffectNormalizer.TryProbabilityPrefix(text, out probability, out body))
            text = body.Trim();
          string starter = FollowUpStarts.FirstOrDefault(x => text.StartsWith(x));
          bool follow = starter != null;
          if (follow)
            text = text.Substring(starter.Length).TrimStart(' ', ',', '，');
          var pieces = SplitCost(text);
          for (int i = 0; i < pieces.Count; i++)
            steps.Add(new Step { Line = pieces[i], AfterCost = i > 0, NeedsSuccess = i == 0 && follow && steps.Count > 0 });
        }
      }

      return steps;
    }

    static List<string> SplitCost(string text)
    {
      foreach (string connector in CostConnectors)
      {
        int index = text.IndexOf(connector, StringComparison.Ordinal);
        if (index <= 0)
          continue;
        string cost = text.Substring(0, index + connector.Length).Trim().TrimEnd(',', '，');
        string rest = text.Substring(index + connector.Length).Trim().TrimStart(',', '，').Trim();
        if (rest.Length == 0)
          break;
        return new List<string>
        {
          cost,
          rest
        };
      }

      return new List<string>
      {
        text
      };
    }

    static void Fire(EffectScope scope, PotentialEvent e, Plan plan, HashSet<string> chainFired)
    {
      var source = plan.Source;
      var log = e.Log;
      string head = source.Label + " " + (e.Label.Length > 0 ? e.Label + " " : "") + "트리거 충족";
      if (!plan.Executable)
      {
        // 실행기가 없거나 추가 조건이 있으면 예전과 같은 대기 로그(교대 런타임은 이 로그를 읽는다)
        Say(log, e.PendingText != null ? e.PendingText(source, plan.Extra) : head + (plan.Extra ? " · 추가 조건 확인 필요" : "") + " · 효과 판정 대기: " + source.Effect);
        return;
      }

      var usage = Usage(scope, source);
      bool preInput = (source.Activation ?? "").Trim() == "선행";
      bool declared = preInput && usage != null && usage.Declared.Contains(source.Key);
      if (preInput && !declared)
      {
        Say(log, head + " · 선행입력을 선언하지 않아 발동하지 않는다.");
        return;
      }

      // b. 사용 제한
      if (!declared && usage != null && !usage.CanUse(source.Key, source.Uses))
      {
        Say(log, head + " · 사용 횟수(" + source.Uses + ")를 이미 썼다.");
        return;
      }

      if (!declared && usage == null && PotentialUsageLimited(source))
      {
        Say(log, head + " · 효과 판정 대기(사용 이력을 알 수 없음): " + source.Effect);
        return;
      }

      // c. 확률
      if (!plan.ProbabilityOk)
      {
        Say(log, head + " · 확률 판정 실패");
        return;
      }

      // d. 할 수 있다
      if (plan.Optional)
      {
        var prompt = scope.Rules == null ? null : scope.Rules.OptionalEffectPrompt;
        if (prompt == null)
        {
          Say(log, head + " · 효과 판정 대기: " + source.Effect);
          return;
        }

        if (!prompt(source.Owner, e.Other ?? OpponentOf(source.Owner), source.AsPotential()))
        {
          Say(log, head + " · 사용하지 않았다.");
          return;
        }
      }

      // e. 소비(이후 효과가 실패해도 돌려주지 않는다)
      if (!declared && usage != null)
        usage.Use(source.Key, source.Uses);
      RecordTrainerUse(scope, source);
      Say(log, head + " · 효과 적용: " + source.Effect);
      // 사용 제한이 있는 선행입력은 한 번 발동하면 이번 T의 선언이 끝난다(추가행동 등으로 같은 사건이 다시 와도 또 발동하지 않는다)
      if (declared && usage != null && !string.IsNullOrWhiteSpace(source.Uses))
        usage.Declared.Remove(source.Key);
      if (source.Slot == AbilityModules.Slot && source.Owner != null)
        AnsweredTriggers.AbilityActivated(source.Owner, Regex.Replace(source.Name, @" \(\d+\)$", ""), x => Say(log, x));
      chainFired.Add(source.Key);
      // f. 실행. 실행 중 생긴 사건은 같은 연쇄, 깊이+1로 즉시 처리된다
      var saved = current;
      current = new Frame
      {
        ChainId = e.ChainId,
        Depth = e.Depth,
        ChainFired = chainFired,
        Parent = saved
      };
      try
      {
        EffectOutcome previous = EffectOutcome.Success;
        foreach (var step in plan.Steps)
        {
          EffectOutcome outcome;
          if (step.AfterCost && previous != EffectOutcome.Success)
            outcome = Blocked(log, source, step, previous, "대가");
          else if (step.NeedsSuccess && previous != EffectOutcome.Success)
            outcome = Blocked(log, source, step, previous, "앞 효과");
          else if (step.Executor == null)
          {
            Say(log, source.Label + " 효과 판정 대기: " + step.Line);
            outcome = EffectOutcome.Pending;
          }
          else
          {
            var lines = new List<string>();
            var call = new EffectCall
            {
              Source = source,
              Event = e,
              Line = step.Line,
              Rules = scope.Rules,
              Rng = scope.Rng,
              Log = lines
            };
            outcome = step.Executor.Run(call);
            foreach (string line in lines)
              Say(log, line);
            if (outcome == EffectOutcome.Pending)
              Say(log, source.Label + " 효과 판정 대기: " + step.Line);
          }

          previous = outcome;
        }
      }
      finally
      {
        current = saved;
      }

      // 소지품의 「1번만」: 발동하면 소지품이 사라진다
      if (source.Pokemon != null && source.Pokemon.id == ItemRuntime.Slot && source.Uses == "1/시")
        ItemRuntime.Lose(source.Owner, "소비", x => Say(log, x));
    }

    static bool PotentialUsageLimited(PotentialInstance source)
    {
      int count;
      bool perTurn;
      return PotentialUsage.TryLimit(source.Uses, out count, out perTurn);
    }

    static EffectOutcome Blocked(Action<string> log, PotentialInstance source, Step step, EffectOutcome previous, string reason)
    {
      if (previous == EffectOutcome.Failed)
      {
        Say(log, source.Label + " " + reason + "가 실패해 실행하지 않는다: " + step.Line);
        return EffectOutcome.Failed;
      }

      Say(log, source.Label + " 효과 판정 대기(" + reason + " 결과 대기): " + step.Line);
      return EffectOutcome.Pending;
    }

    static PotentialUsage Usage(EffectScope scope, PotentialInstance source)
    {
      if (scope.States == null)
        return null;
      int side = SideOf(scope, source);
      return side < 0 || side >= scope.States.Length || scope.States[side] == null ? null : scope.States[side].Usage;
    }

    static int SideOf(EffectScope scope, PotentialInstance source)
    {
      if (source.IsTrainer && scope.States != null)
        for (int i = 0; i < scope.States.Length; i++)
          if (scope.States[i] != null && ReferenceEquals(scope.States[i].Trainer, source.Trainer))
            return i;
      if (scope.Fighters != null)
        for (int i = 0; i < scope.Fighters.Length; i++)
          if (ReferenceEquals(scope.Fighters[i], source.Owner))
            return i;
      return source.Owner == null ? -1 : Mechanics.PartySide(source.Owner, scope.Rules);
    }

    // 효과의 대상 문자열을 장소 기준으로 해석한다(03·04)
    public static List<Fighter> Targets(EffectCall call, string subject)
    {
      var scope = EffectScope.Current;
      string text = PotentialParts.Clean(subject ?? "").Replace(" ", "");
      var owner = call.Owner;
      var opponent = call.Opponent;
      bool bench = text.Contains("대기") || text.Contains("파티"), fainted = text.Contains("빈사");
      Func<Fighter, bool> alive = f => f != null && (fainted || f.HP > 0);
      var pool = new List<Fighter>();
      if (text.StartsWith("상대"))
      {
        if (alive(opponent) && (opponent.OnField || bench))
          pool.Add(opponent);
        if (bench)
          pool.AddRange(Party(scope, opponent).Where(alive));
      }
      else
      {
        if (alive(owner))
          pool.Add(owner);
        if (bench)
          pool.AddRange(Party(scope, owner).Where(alive));
      }

      pool = pool.Distinct().ToList();
      if (text.Contains("임의") && pool.Count > 1)
      {
        var chosen = ChooseTarget(call.Source, pool);
        return chosen == null ? new List<Fighter>() : new List<Fighter>
        {
          chosen
        };
      }

      if ((text.Contains("랜덤") || text.Contains("무작위")) && pool.Count > 1)
      {
        var rng = call.Rng ?? (() => .5);
        return new List<Fighter>
        {
          pool[Math.Min(pool.Count - 1, (int)(rng() * pool.Count))]
        };
      }

      if (text.Contains("1체") && pool.Count > 1)
        return new List<Fighter>
        {
          pool[0]
        };
      return pool;
    }

    static IEnumerable<Fighter> Party(EffectScope scope, Fighter member)
    {
      if (scope == null || scope.Rules == null || member == null)
        return Enumerable.Empty<Fighter>();
      int side = Mechanics.PartySide(member, scope.Rules);
      return side < 0 ? Enumerable.Empty<Fighter>() : (scope.Rules.Parties[side] ?? new Fighter[0]).Where(x => x != null && !Mechanics.SameMember(x, member));
    }

    static void Say(Action<string> log, string text)
    {
      if (log != null)
        log(text);
    }
  }
}
