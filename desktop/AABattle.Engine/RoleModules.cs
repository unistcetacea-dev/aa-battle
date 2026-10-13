using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle
{
  public static class RoleModules
  {
    public const string EventModule = "상태 됨";
    public const string EffectKind = "역할 임명";
    public const string AptitudeKind = "트레이너 자질 변화";
    static readonly Regex RoleChange = new Regex(@"^(?:자신이\s*)?(?<roles>(?:『[^』]+』\s*)+)\s*(?:이|가)\s*(?:되었을\s*때|되면)$");
    static readonly Regex Appointment = new Regex(@"^(?:자신을\s*)?『(?<role>[^』]+)』(?:로|으로)\s*임명(?:할\s*수\s*있다|한다)$");
    static readonly Regex Aptitude = new Regex(@"^트레이너의\s*「(?<aptitude>지시|육성|통솔|능력)」(?:을|를|이|가)?\s*(?<n>[1-3])\s*랭크\s*(?<dir>올린다|내린다|오른다|내린다|상승한다|하락한다)$");

    static string Clean(string text) => Regex.Replace(PotentialParts.Clean(text ?? ""), @"\s+", " ").Trim().TrimEnd('.', '。', '．');
    static string[] RoleNames(string raw) => Regex.Matches(raw ?? "", "『([^』]+)』").Cast<Match>().Select(x => x.Groups[1].Value).ToArray();
    static string Particle(string role) { char c = (role ?? "").LastOrDefault(); return c >= '가' && c <= '힣' && (c - '가') % 28 == 0 ? "가" : "이"; }
    public static string[] Choices => EditorTemplate.RolePotentials.Select(x => x.name).ToArray();
    public static string TriggerChoice(string role) => "『" + role + "』" + Particle(role);

    public static string NormalizeTrigger(string text)
    {
      string value = Clean(text);
      var m = RoleChange.Match(value);
      return m.Success ? "자신이 " + string.Join("", RoleNames(m.Groups["roles"].Value).Select(x => "『" + x + "』")) + Particle(RoleNames(m.Groups["roles"].Value).Last()) + " 되었을 때" : null;
    }

    public static bool TryParse(string text, out TriggerSpec spec)
    {
      var m = RoleChange.Match(Clean(text));
      var names = m.Success ? RoleNames(m.Groups["roles"].Value) : new string[0];
      spec = names.Length == 0 ? null : new TriggerSpec { Module = EventModule, Subject = "역할", Names = names };
      return spec != null;
    }

    public static bool IsRoleSpec(TriggerSpec spec) => spec != null && spec.Module == EventModule && spec.Subject == "역할";

    public static string Canonical(TriggerSpec spec)
    {
      return !IsRoleSpec(spec) || spec.Names.Length == 0 ? null : "자신이 " + string.Join("", spec.Names.Select(x => "『" + x + "』")) + Particle(spec.Names.Last()) + " 되었을 때";
    }

    public static bool Matches(TriggerSpec spec, TriggerFact fact)
    {
      return IsRoleSpec(spec) && fact != null && fact.Kind == EventModule && fact.Self && spec.Names.Any(x => x == fact.Method);
    }

    public static string NormalizeEffect(string text)
    {
      string value = Clean(text);
      var m = Appointment.Match(value);
      if (m.Success) return "자신을 『" + m.Groups["role"].Value + "』로 임명할 수 있다.";
      m = Aptitude.Match(value);
      if (m.Success) return "트레이너의 「" + m.Groups["aptitude"].Value + "」을 " + m.Groups["n"].Value + "랭크 " + (m.Groups["dir"].Value == "올린다" || m.Groups["dir"].Value == "오른다" || m.Groups["dir"].Value == "상승한다" ? "올린다." : "내린다.");
      return null;
    }

    public static string ClassifyEffect(string text)
    {
      string value = Clean(text);
      if (Appointment.IsMatch(value)) return EffectKind;
      if (Aptitude.IsMatch(value)) return AptitudeKind;
      return "";
    }

    public static void Register()
    {
      PotentialExecutors.Register("역할 임명", c => ClassifyEffect(c.Line) == EffectKind, Assign);
      PotentialExecutors.Register("트레이너 자질 변화", c => ClassifyEffect(c.Line) == AptitudeKind, ChangeAptitude);
    }

    static EffectOutcome Assign(EffectCall call)
    {
      var m = Regex.Match(Clean(call.Line), @"^자신을\s*『(?<role>[^』]+)』로\s*임명(?:할\s*수\s*있다|한다)$");
      string role = m.Success ? m.Groups["role"].Value : "";
      var owner = call.Owner;
      if (role.Length == 0 || owner == null || owner.HP <= 0 || owner.Data == null) return EffectOutcome.Failed;
      var template = EditorTemplate.Role(role);
      if (template == null) return EffectOutcome.Pending;
      var potentials = (owner.Data.potentials ?? new Potential[0]).ToList();
      var existing = potentials.FirstOrDefault(x => x != null && x.id == "역할" && x.name == role);
      if (existing == null)
      {
        potentials.Add(new Potential { id = "역할", name = role, description = template.effect ?? "", triggers = new[] { TriggerStructures.EntryLabel } });
        owner.Data.potentials = potentials.ToArray();
      }
      TriggerModules.Raise(new TriggerFact { Kind = EventModule, Method = role }, owner, call.Event == null ? null : call.Event.Other, owner.Data.name + "이 『" + role + "』이 되었다", call.Log == null ? null : call.Log.Add);
      return existing == null ? EffectOutcome.Success : EffectOutcome.Failed;
    }

    static string Rank(string rank, int delta)
    {
      string[] bands = { "E", "D", "C", "B", "A", "AA", "AAA", "S" };
      string value = new string((rank ?? "").ToUpperInvariant().Where(c => c >= 'A' && c <= 'Z').ToArray());
      int index = Array.IndexOf(bands, value);
      return index < 0 ? rank : bands[Math.Max(0, Math.Min(bands.Length - 1, index + delta))];
    }

    public static string AdjustedRank(string rank, string aptitude, TrainerBattleState state)
    {
      if (state == null || state.Usage == null) return rank;
      int delta;
      return state.Usage.Battle.TryGetValue("@aptitude:" + aptitude, out delta) ? Rank(rank, delta) : rank;
    }

    static EffectOutcome ChangeAptitude(EffectCall call)
    {
      var m = Regex.Match(Clean(call.Line), @"^트레이너의\s*「(?<aptitude>지시|육성|통솔|능력)」(?:을|를)\s*(?<n>[1-3])랭크\s*(?<dir>올린다|내린다)$");
      var scope = EffectScope.Current;
      int side = call.Owner == null ? -1 : Mechanics.PartySide(call.Owner, call.Rules);
      var state = scope == null || side < 0 || scope.States == null || side >= scope.States.Length ? null : scope.States[side];
      var trainer = state == null ? null : state.Trainer;
      if (!m.Success || trainer == null) return EffectOutcome.Pending;
      int delta = int.Parse(m.Groups["n"].Value) * (m.Groups["dir"].Value == "올린다" ? 1 : -1);
      string name = m.Groups["aptitude"].Value;
      int prior;
      state.Usage.Battle.TryGetValue("@aptitude:" + name, out prior);
      int next = Math.Max(-7, Math.Min(7, prior + delta));
      if (next == prior) return EffectOutcome.Failed;
      state.Usage.Battle["@aptitude:" + name] = next;
      string current = name == "지시" ? trainer.command.rank : name == "육성" ? trainer.training.rank : name == "통솔" ? trainer.leadership.rank : trainer.ability.rank;
      call.Say("트레이너 「" + name + "」이 " + AdjustedRank(current, name, state) + "이 되었다.");
      if (scope.Fighters != null)
        foreach (var fighter in scope.Fighters.Where(x => x != null && x.HP > 0 && Mechanics.PartySide(x, call.Rules) == side))
          TrainerAccessRules.ApplyTrainerPassives(fighter, trainer, state, call.Say);
      return EffectOutcome.Success;
    }

  }
}
