// 대기석에서 볼 수 있는 사건과 대기 포켓몬의 교대 효과.
using System;
using System.Collections.Generic;
using System.Linq;

namespace AABattle
{
  public static class BenchEvents
  {
    // 대기석 관측 트리거 로그에 붙는 표지. 보유자 이름이 함께 붙어 다른 포켓몬의 같은 이름 포텐셜과 섞이지 않는다
    public const string BenchMark = " · 대기석 ";

    public static string BenchTag(Fighter bench)
    {
      return BenchMark + bench.Data.name;
    }

    // 대기석에서 판정하는 교대는 「교대해 필드에 나올 수 있다」 진입형뿐이다(「아군과 교대할 수 있다」 둔주류는 장소에 있을 때만)
    public static bool IsEntrySwitch(Potential potential)
    {
      if (potential == null || !SwitchEffectRuntime.IsSwitchEffect(potential))
        return false;
      return PotentialParts.Clean(potential.description ?? "").Replace(" ", "").Contains("필드에나올");
    }

    public static bool MayObserve(Potential potential)
    {
      if (potential == null)
        return false;
      var use = UseConditionModules.Of(potential.description);
      return use.OffField || use.PartyWide || IsEntrySwitch(potential);
    }

    public static bool Matches(string clause, TriggerFact fact, Fighter bench, Fighter fieldAlly, Fighter opponent)
    {
      string text = PotentialParts.Clean(clause ?? "").Replace(" ", "");
      if (fact != null && fact.Kind == "등장" && !fact.Self && text.Contains("『에이스』") && (text.Contains("나왔을때") || text.Contains("나올때")))
        return opponent != null && opponent.HP > 0 && (opponent.Data.potentials ?? new Potential[0]).Any(p => p != null && p.id == "역할" && p.name == "에이스");
      var spec = TriggerModules.Parse(TriggerStructures.Normalize(clause));
      if (spec != null && spec.Module == "타이밍" && spec.FieldTurn > 0)
        return TriggerModules.Matches(spec, fact, fieldAlly);
      return TriggerModules.Matches(spec, fact, bench);
    }

    public static void Dispatch(TriggerFact fact, Fighter actor, Fighter other, string message, Action<string> log)
    {
      var scope = EffectScope.Current;
      if (scope == null || scope.Rules == null || fact == null || log == null)
        return;
      DispatchFor(fact.For(true, false), actor, other, message, log, scope);
      if (other != null && !ReferenceEquals(actor, other))
        DispatchFor(fact.For(false, fact.Kind == "명중"), other, actor, message, log, scope);
    }

    static void DispatchFor(TriggerFact fact, Fighter fieldAlly, Fighter opponent, string message, Action<string> log, EffectScope scope)
    {
      int side = Mechanics.PartySide(fieldAlly, scope.Rules);
      if (side < 0 || side >= scope.Rules.Parties.Length)
        return;
      foreach (var bench in (scope.Rules.Parties[side] ?? new Fighter[0]).Where(x => x != null && x.HP > 0 && !x.OnField && !Mechanics.SameMember(x, fieldAlly)))
      {
        Func<string, bool> spec = clause => Matches(clause, fact, bench, fieldAlly, opponent);
        var potentials = (bench.Data.potentials ?? new Potential[0]).Where(MayObserve).ToArray();
        var label = potentials.SelectMany(TriggerStructures.Clauses).FirstOrDefault(spec);
        if (label == null)
          continue;
        PotentialQueue.Raise(new PotentialEvent
        {
          Owner = bench,
          Other = opponent,
          Spec = spec,
          Label = TriggerStructures.Normalize(label),
          Message = message ?? "",
          Move = fact.Move,
          Fact = fact,
          Log = log,
          Announce = true,
          BenchObservation = true,
          PendingText = (source, extra) => "『" + source.Name + "』 " + TriggerStructures.Normalize(label) + " 트리거 충족" + BenchTag(bench) + (extra ? " · 추가 조건 확인 필요" : "") + " · 효과 판정 대기: " + source.Description
        });
      }
    }

    public static bool IsBenchSwitch(Fighter holder, Fighter active, Rules rules)
    {
      if (holder == null || active == null || holder.HP <= 0 || holder.OnField || holder.Data == null)
        return false;
      int side = Mechanics.PartySide(active, rules);
      return side >= 0 && (rules.Parties[side] ?? new Fighter[0]).Any(x => x != null && Mechanics.SameMember(x, holder));
    }

    public static bool TryDeclarePreInput(Rules rules, int side, string name, Fighter active, PotentialUsage usage, Action<string> log)
    {
      if (rules == null || side < 0 || side >= rules.Parties.Length || active == null || usage == null || string.IsNullOrEmpty(name))
        return false;
      var holder = (rules.Parties[side] ?? new Fighter[0]).FirstOrDefault(x => x != null && x.HP > 0 && !x.OnField && !Mechanics.SameMember(x, active) &&
        (x.Data.potentials ?? new Potential[0]).Any(p => p != null && p.name == name && MayObserve(p)));
      if (holder == null)
        return false;
      var definition = (holder.Data.potentials ?? new Potential[0]).First(x => x != null && x.name == name && MayObserve(x));
      string key = PotentialKeys.For(holder.Data, definition);
      if (!usage.CanUse(key, definition.uses))
        throw new Exception("『" + name + "』은 이미 사용했습니다.");
      usage.Use(key, definition.uses);
      usage.Declared.Add(key);
      if (log != null)
        log(holder.Data.name + " — 선행입력 『" + name + "』 선언!" + (definition.description ?? ""));
      return true;
    }

    // 보유자 본인이 대기석에서 본 트리거 로그(아직 처리하지 않은 것)의 절대 위치. 없으면 -1
    public static int TriggerLine(Fighter holder, Potential potential, IList<string> log, int start, ISet<int> handled)
    {
      if (holder == null || holder.Data == null || potential == null || string.IsNullOrWhiteSpace(potential.name))
        return -1;
      string token = "『" + potential.name + "』";
      string tag = BenchTag(holder);
      for (int i = Math.Max(0, start); i < log.Count; i++)
      {
        string line = log[i] ?? "";
        if (line.Contains(token) && line.Contains("트리거 충족") && line.Contains(tag) && !handled.Contains(i))
          return i;
      }

      return -1;
    }

    public static IEnumerable<Potential> SwitchPotentials(Fighter holder)
    {
      if (holder == null || holder.Data == null || holder.HP <= 0 || holder.OnField)
        return Enumerable.Empty<Potential>();
      return (holder.Data.potentials ?? new Potential[0]).Where(IsEntrySwitch);
    }
  }
}
