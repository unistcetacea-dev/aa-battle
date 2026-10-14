using System.Linq;
using System.Text.RegularExpressions;

namespace AABattle
{
  public static class ForcedCounterModules
  {
    public const string Kind = "격 강제 발동";
    public const string RoleActivationKind = "특성 발동";
    const string TurnGuaranteeKey = "@forced-counter-this-turn";
    public const string BodySentence = "아군의 『격』이 반드시 발동한다.";
    public const string Sentence = "T종료시까지 " + BodySentence;
    const string Body = "아군의 『격』이 반드시 발동한다";
    static readonly Regex Duration = new Regex(@"^(?:(?:\d+)\s*T\s*(?:간|동안)|T\s*종료\s*시\s*까지)\s*,?\s*");

    static string Clean(string text)
    {
      return Regex.Replace(PotentialParts.Clean(text ?? ""), @"\s+", " ").Trim().TrimEnd('.', '。', '．');
    }

    public static string Classify(string text)
    {
      return Clean(Duration.Replace(Clean(text), "", 1)) == Body ? Kind : "";
    }

    public static bool TryParseTrigger(string text, out TriggerSpec spec)
    {
      spec = null;
      if (!Regex.IsMatch(Clean(text), @"^상대의 『역할』이 발동했을 때$"))
        return false;
      spec = new TriggerSpec { Module = RoleActivationKind, Names = new[] { "역할" }, Opponent = true };
      return true;
    }

    public static string NormalizeTrigger(string text)
    {
      return TryParseTrigger(text, out _) ? "상대의 『역할』이 발동했을 때" : null;
    }

    public static string CanonicalTrigger(TriggerSpec spec)
    {
      return spec != null && spec.Module == RoleActivationKind && spec.Opponent
        && spec.Names != null && spec.Names.Contains("역할")
        ? "상대의 『역할』이 발동했을 때"
        : null;
    }

    public static bool MatchesTrigger(TriggerSpec spec, TriggerFact fact)
    {
      return IsRoleActivationSpec(spec)
        && fact != null && fact.Kind == RoleActivationKind && fact.Method == "역할" && !fact.Self;
    }

    public static bool IsRoleActivationSpec(TriggerSpec spec)
    {
      return spec != null && spec.Module == RoleActivationKind && spec.Opponent
        && spec.Names != null && spec.Names.Contains("역할");
    }

    public static bool IsRoleActivationFact(TriggerFact fact)
    {
      return fact != null && fact.Kind == RoleActivationKind && fact.Method == "역할";
    }

    public static void Register()
    {
      PotentialExecutors.Register(Kind, c => Classify(c.Line).Length > 0, Force);
    }

    static void MarkSuccessfulRoleActivation(EffectScope scope, EffectCall call)
    {
      var evt = call == null ? null : call.Event;
      if (scope == null || call == null || call.Source == null || evt == null || evt.Fact == null
        || evt.Fact.Kind != RoleActivationKind || evt.Fact.Method != "역할")
        return;
      var state = PotentialQueue.StateOf(scope, call.Source);
      if (state != null)
        state.Usage.Turn[TurnGuaranteeKey] = 1;
    }

    public static bool GuaranteesThisTurn(EffectScope scope, PotentialInstance source)
    {
      if (scope == null || source == null || source.Slot != "격" || !IsSupported(source.Name))
        return false;
      var state = PotentialQueue.StateOf(scope, source);
      return state != null && state.Usage.Turn.ContainsKey(TurnGuaranteeKey);
    }

    public static void GuaranteePlan(EffectScope scope, PotentialInstance source, ref bool probabilityOk)
    {
      if (GuaranteesThisTurn(scope, source))
        probabilityOk = true;
    }

    public static void GuaranteeRoll(Fighter owner, Potential potential, ref bool rolled)
    {
      GuaranteePlan(EffectScope.Current, new PotentialInstance
      {
        Owner = owner, Slot = potential.id, Name = potential.name
      }, ref rolled);
    }

    static EffectOutcome Force(EffectCall call)
    {
      var scope = EffectScope.Current;
      if (scope == null || call.Owner == null)
        return EffectOutcome.Pending;
      MarkSuccessfulRoleActivation(scope, call);
      call.Say(call.Owner.Data.name + ": 이 턴 동안 조건을 충족한 아군 『격』의 확률을 보장한다.");
      return EffectOutcome.Success;
    }

    static bool IsSupported(string name)
    {
      return Regex.IsMatch(name ?? "", @"(?:저격|추격|박격)$");
    }
  }
}
