using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle
{
  // 트레이너 포텐셜 이력 한 건(발동했거나 받은 것). 재사용 권한과 재발동이 이 목록에서 고른다
  public sealed class BondEntry
  {
    public string Key = "", Name = "", Uses = "";
    public PotentialInstance Template; // 재발동에 쓰는 원본(Owner만 바꿔 쓴다)
  }

  // 『유대』 포텐셜: 지시 랭크 상승(상시) · 포텐셜 재사용 권한 · 자신 대상 재발동.
  // 판정 시점은 「T 개시 시」: 엔진은 지시·지령 처리 직후, 교대·기술 전에 이 타이밍을 낸다
  public static class BondModules
  {
    public const string ExecutorPrefix = "유대: ";
    public const string KindRise = "지시 랭크 상승";
    public const string KindRegain = "트레이너 포텐셜 재사용 권한";
    public const string KindReplay = "트레이너 포텐셜 재발동";
    public const string RegainSentence = "포텐셜을 다시 받을 수 있다";
    public const string ReplaySentence = "자신을 대상으로 포텐셜을 다시 발동한다";
    const string Prefix = @"(?<p>(?:저확률|중확률|드물게)(?:로)?\s+)?";
    static readonly Regex Rise = new Regex(@"^필드에\s*(?:있는\s*한|있을\s*때)\s*,?\s*트레이너의\s*「?지시」?(?:를|을)\s*「?(?<n>\d+)」?\s*랭크\s*올린다$");
    static readonly Regex Regain = new Regex("^" + Prefix + @"(?:자신이\s*)?포텐셜을\s*다시\s*받을\s*수\s*있다$");
    static readonly Regex Replay = new Regex("^" + Prefix + @"자신을\s*대상으로\s*포텐셜을\s*다시\s*발동한다$");

    static string Clean(string text)
    {
      return Regex.Replace(PotentialParts.Clean(text ?? ""), @"\s+", " ").Trim().TrimEnd('.', '。', '．');
    }

    public static string Normalize(string text)
    {
      string s = Clean(text);
      var m = Rise.Match(s);
      if (m.Success)
        return RiseSentence(int.Parse(m.Groups["n"].Value));
      m = Regain.Match(s);
      if (m.Success)
        return Probability(m) + RegainSentence + ".";
      m = Replay.Match(s);
      if (m.Success)
        return Probability(m) + ReplaySentence + ".";
      return null;
    }

    public static string RiseSentence(int amount)
    {
      return "필드에 있는 한 트레이너의 지시를 「" + amount + "」랭크 올린다.";
    }

    static string Probability(Match m)
    {
      string p = m.Groups["p"].Value.Trim();
      return p.Length == 0 ? "" : p.EndsWith("로") || p == "드물게" ? p + " " : p + "로 ";
    }

    public static string Classify(string text)
    {
      string s = Clean(text);
      return Rise.IsMatch(s) ? KindRise : Regain.IsMatch(s) ? KindRegain : Replay.IsMatch(s) ? KindReplay : "";
    }

    static bool IsRegain(string line)
    {
      return Regain.IsMatch(Clean(line));
    }

    static bool IsReplay(string line)
    {
      return Replay.IsMatch(Clean(line));
    }

    static IEnumerable<string> Lines(string description)
    {
      foreach (string raw in (description ?? "").Replace("\r", "").Split('\n'))
        foreach (string sentence in Regex.Split(raw, @"(?<=[.．。])\s+"))
        {
          string text = sentence.Trim();
          if (text.Length == 0)
            continue;
          yield return text;
          int comma = text.IndexOf(", ", StringComparison.Ordinal);
          if (comma > 0)
            yield return text.Substring(comma + 2); // 「T 개시 시, …」처럼 트리거가 앞에 붙은 줄
        }
    }

    // 이 포텐셜들이 필드에 있는 동안 트레이너의 지시를 올리는 랭크 수
    public static int CommandRise(IEnumerable<Potential> potentials)
    {
      int total = 0;
      foreach (var potential in (potentials ?? Enumerable.Empty<Potential>()).Where(x => x != null))
      {
        foreach (string line in Lines(potential.description))
        {
          var m = Rise.Match(Clean(line));
          if (!m.Success)
            continue;
          total += int.Parse(m.Groups["n"].Value);
          break;
        }
      }

      return total;
    }

    // 큐의 확률 판정에서 뺄 문장: 유대의 재사용/재발동 문장은 실행기가 따로 굴린다
    public static string WithoutOwnRoll(string text)
    {
      var kept = new List<string>();
      foreach (string raw in (text ?? "").Replace("\r", "").Split('\n'))
      {
        var parts = Regex.Split(raw, @"(?<=[.．。])\s+").Where(x => !IsRegain(x) && !IsReplay(x));
        kept.Add(string.Join(" ", parts));
      }

      return string.Join("\n", kept);
    }

    static double? OwnRate(PotentialInstance source, Func<string, bool> pick)
    {
      foreach (string raw in (source.Effect ?? "").Replace("\r", "").Split('\n'))
        foreach (string sentence in Regex.Split(raw, @"(?<=[.．。])\s+"))
          if (pick(sentence))
            return PotentialProbability.Rate(sentence);
      return null;
    }

    // ---- 이력 ----
    static string FighterKey(Fighter f)
    {
      var data = f == null ? null : f.Data;
      return data == null ? "" : !string.IsNullOrEmpty(data.id) ? data.id : data.name;
    }

    public static void Record(TrainerBattleState state, PotentialInstance source, Fighter receiver)
    {
      if (state == null || source == null || receiver == null)
        return;
      var list = state.Usage.Bond;
      string who = FighterKey(receiver);
      List<BondEntry> mine;
      if (!list.TryGetValue(who, out mine))
        list[who] = mine = new List<BondEntry>();
      if (mine.Any(x => x.Key == source.Key))
        return;
      mine.Add(new BondEntry
      {
        Key = source.Key,
        Name = source.Name,
        Uses = source.Uses ?? "",
        Template = source
      });
    }

    // 이 포켓몬이 발동했거나 받은 이력
    public static List<BondEntry> Own(TrainerBattleState state, Fighter owner)
    {
      List<BondEntry> mine;
      return state != null && state.Usage.Bond.TryGetValue(FighterKey(owner), out mine) ? mine.ToList() : new List<BondEntry>();
    }

    // 파티 전체(대기·빈사 포함)의 이력. 같은 포텐셜은 한 번만
    public static List<BondEntry> Party(TrainerBattleState state)
    {
      var result = new List<BondEntry>();
      if (state == null)
        return result;
      foreach (var entry in state.Usage.Bond.Values.SelectMany(x => x))
        if (!result.Any(x => x.Key == entry.Key))
          result.Add(entry);
      return result;
    }

    // 다시 받을 수 있는 것: 횟수 제한(배틀 단위)이 있고 한 번 이상 쓴 것
    public static List<BondEntry> Regainable(TrainerBattleState state, Fighter owner)
    {
      return Own(state, owner).Where(x => Spent(state, x)).ToList();
    }

    static bool Spent(TrainerBattleState state, BondEntry entry)
    {
      int count;
      bool perTurn;
      if (!PotentialUsage.TryLimit(entry.Uses, out count, out perTurn) || perTurn)
        return false;
      int used;
      return state.Usage.Battle.TryGetValue(entry.Key, out used) && used > 0;
    }

    public static string Describe(BondEntry entry)
    {
      return "『" + entry.Name + "』" + (entry.Uses.Length > 0 ? " (" + entry.Uses + ")" : "");
    }

    static int Choose(Rules rules, Fighter owner, List<BondEntry> entries)
    {
      if (rules == null || rules.ChooseBondPotential == null)
        return 0;
      return rules.ChooseBondPotential(owner, entries.Select(Describe).ToArray());
    }

    // ---- 실행기 ----
    public static void Register()
    {
      PotentialExecutors.Register(ExecutorPrefix + "지시 랭크", c => Classify(c.Line) == KindRise, c => EffectOutcome.Success);
      PotentialExecutors.Register(ExecutorPrefix + "재사용 권한", c => Classify(c.Line) == KindRegain, Regain_);
      PotentialExecutors.Register(ExecutorPrefix + "재발동", c => Classify(c.Line) == KindReplay, Replay_);
    }

    static bool Rolled(EffectCall call, Func<string, bool> pick)
    {
      double? rate = OwnRate(call.Source, pick);
      if (!rate.HasValue)
        return true;
      var rng = call.Rng ?? (() => .5);
      if (rng() < rate.Value)
        return true;
      call.Say(call.Source.Label + " · 확률 판정 실패");
      return false;
    }

    static EffectOutcome Regain_(EffectCall call)
    {
      var scope = EffectScope.Current;
      var state = PotentialQueue.StateOf(scope, call.Source);
      if (!Rolled(call, IsRegain))
        return EffectOutcome.Failed;
      var entries = Regainable(state, call.Owner);
      if (entries.Count == 0)
      {
        call.Say(call.Source.Label + " · 다시 받을 트레이너 포텐셜이 없다.");
        return EffectOutcome.Failed;
      }

      int index = Choose(call.Rules, call.Owner, entries);
      if (index < 0 || index >= entries.Count)
      {
        call.Say(call.Source.Label + " · 사용하지 않았다.");
        return EffectOutcome.Failed;
      }

      var picked = entries[index];
      int used = state.Usage.Battle[picked.Key] - 1;
      if (used <= 0)
        state.Usage.Battle.Remove(picked.Key);
      else
        state.Usage.Battle[picked.Key] = used;
      call.Say(call.Owner.Data.name + ": " + call.Source.Label + " — TRAINER 『" + picked.Name + "』를 다시 쓸 수 있다 (사용 횟수 1회 회복)");
      return EffectOutcome.Success;
    }

    static EffectOutcome Replay_(EffectCall call)
    {
      var scope = EffectScope.Current;
      var state = PotentialQueue.StateOf(scope, call.Source);
      if (!Rolled(call, IsReplay))
        return EffectOutcome.Failed;
      var entries = Party(state);
      if (entries.Count == 0)
      {
        call.Say(call.Source.Label + " · 다시 발동할 트레이너 포텐셜이 없다.");
        return EffectOutcome.Failed;
      }

      int index = Choose(call.Rules, call.Owner, entries);
      if (index < 0 || index >= entries.Count)
      {
        call.Say(call.Source.Label + " · 사용하지 않았다.");
        return EffectOutcome.Failed;
      }

      var picked = entries[index];
      call.Say(call.Owner.Data.name + ": " + call.Source.Label + " — TRAINER 『" + picked.Name + "』를 자신을 대상으로 다시 발동한다");
      PotentialQueue.Replay(scope, picked.Template, call.Owner, call.Event, call.Log);
      return EffectOutcome.Success;
    }
  }
}
