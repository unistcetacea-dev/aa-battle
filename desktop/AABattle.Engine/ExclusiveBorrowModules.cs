using System.Linq;
using System.Text.RegularExpressions;

namespace AABattle
{
  public static class ExclusiveBorrowModules
  {
    public const string Sentence = "아군의 『전용』을 자신의 『전용』으로 사용한다.";
    const string Kind = "전용 빌려쓰기";

    static string Clean(string text)
    {
      return Regex.Replace(PotentialParts.Clean(text ?? ""), @"\s+", " ").Trim().TrimEnd('.', '。', '．');
    }

    public static string Normalize(string text)
    {
      string value = Clean(text);
      if (value == Clean(Sentence)) return Sentence;
      if (Regex.IsMatch(value, @"^아군의 『전용』을 자신의 『전용』으로 사용할 수 있다$")) return Sentence;
      return null;
    }

    public static string Classify(string text)
    {
      return Clean(text) == Clean(Sentence) ? Kind : "";
    }

    public static void Register()
    {
      PotentialExecutors.Register(Kind, c => Classify(c.Line).Length > 0, Borrow);
    }

    static EffectOutcome Borrow(EffectCall call)
    {
      var scope = EffectScope.Current;
      var state = PotentialQueue.StateOf(scope, call.Source);
      if (scope == null || state == null || call.Owner == null || call.Rules == null)
        return EffectOutcome.Pending;

      int side = Mechanics.PartySide(call.Owner, call.Rules);
      if (side < 0 || side >= call.Rules.Parties.Length)
        return EffectOutcome.Failed;

      var choices = (call.Rules.Parties[side] ?? new Fighter[0])
        .Where(member => member != null && !Mechanics.SameMember(member, call.Owner) && member.Data != null)
        .SelectMany(member => (member.Data.potentials ?? new Potential[0])
          .Where(potential => potential != null && potential.id == TeamRules.ExclusiveSlot)
          .Select(potential => new PotentialInstance
          {
            Owner = member,
            Pokemon = potential,
            Key = PotentialKeys.For(member.Data, potential),
            Name = potential.name ?? "",
            Slot = potential.id ?? "",
            Uses = potential.uses ?? "",
            Activation = potential.activation ?? "",
            Effect = potential.description ?? "",
            Description = potential.description ?? "",
            Clauses = TriggerStructures.Clauses(potential).ToArray()
          }))
        .Where(potential => potential.Name.Length > 0 && state.Usage.CanUse(potential.Key, potential.Uses))
        .ToList();

      if (choices.Count == 0)
      {
        call.Say(call.Owner.Data.name + ": 빌려 쓸 수 있는 『전용』이 없다.");
        return EffectOutcome.Failed;
      }

      int index = call.Rules.ChooseBondPotential == null
        ? 0
        : call.Rules.ChooseBondPotential(call.Owner, choices.Select(p => BondModules.Describe(new BondEntry { Name = p.Name, Uses = p.Uses })).ToArray());
      if (index < 0 || index >= choices.Count)
      {
        call.Say(call.Owner.Data.name + ": 『전용』을 빌리지 않았다.");
        return EffectOutcome.Failed;
      }

      var picked = choices[index];
      state.Usage.Use(picked.Key, picked.Uses);
      call.Say(call.Owner.Data.name + ": 아군의 TRAINER 『" + picked.Name + "』을 빌려 자신의 전용으로 발동한다.");
      PotentialQueue.Replay(scope, picked, call.Owner, call.Event, call.Log);
      return EffectOutcome.Success;
    }
  }
}
