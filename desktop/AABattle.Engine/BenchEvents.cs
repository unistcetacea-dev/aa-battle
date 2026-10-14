// 대기석에서 볼 수 있는 사건과 대기 포켓몬의 교대 효과.
using System;
using System.Collections.Generic;
using System.Linq;

namespace AABattle
{
  public static class BenchEvents
  {
    public static bool MayObserve(Potential potential)
    {
      if (potential == null)
        return false;
      var use = UseConditionModules.Of(potential.description);
      return use.OffField || use.PartyWide || SwitchEffectRuntime.IsSwitchEffect(potential);
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
          PendingText = (source, extra) => "『" + source.Name + "』 " + TriggerStructures.Normalize(label) + " 트리거 충족" + (extra ? " · 추가 조건 확인 필요" : "") + " · 효과 판정 대기: " + source.Description
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

    public static IEnumerable<Potential> TriggeredSwitches(Fighter holder, IEnumerable<string> lines)
    {
      if (holder == null || holder.Data == null || holder.HP <= 0 || holder.OnField)
        return Enumerable.Empty<Potential>();
      return (holder.Data.potentials ?? new Potential[0]).Where(p => SwitchEffectRuntime.IsSwitchEffect(p) && SwitchEffectRuntime.Mentioned(lines, p.name));
    }
  }
}
