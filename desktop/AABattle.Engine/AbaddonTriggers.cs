// 실프 인헨스먼츠(MODULE_GAPS 묶음 A)에서 필요한 트리거 모듈.
//  사건: 능력 반전 · 자신이 상대의 능력을 내림 · 타입 바뀜 · 『전용』을 받음 · 『전용』을 받아 장소에 나옴
//  상태: 능력 반전한 T · 타입 바뀐 T · 『전용』을 받은 T · 기술의 우선도 · 종족치 같은 등급
// 「…한 T」 상태는 같은 사건이 일어나는 순간에도 성립한 것으로 본다(한 번 일어나는 효과를 그 순간에 실행).
// 연결: TriggerModules.TryParse · Canonical · Matches · Holds(TriggerSpec) · EventModules/StateModules
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AABattle
{
  public static class AbaddonTriggers
  {
    public const string InvertEvent = "능력 반전";
    public const string InvertTurn = "능력 반전 T";
    public const string LoweredEvent = "상대 능력 내림";
    public const string PriorityState = "기술 우선도";
    public const string TypeChangedEvent = "타입 바뀜";
    public const string TypeChangedTurn = "타입 바뀜 T";
    public const string ExclusiveEvent = "전용 받음";
    public const string ExclusiveTurn = "전용 받음 T";
    public const string ExclusiveEntry = "전용 받고 등장";
    public const string SameGrade = "종족치 동일 등급";

    // TriggerFact.Kind 값
    public const string InvertFact = "능력 반전";
    public const string TypeFact = "타입 바뀜";
    public const string ExclusiveFact = "전용 받음";

    public static readonly string[] EventNames = { InvertEvent, LoweredEvent, TypeChangedEvent, ExclusiveEvent, ExclusiveEntry };
    public static readonly string[] StateNames = { InvertTurn, PriorityState, TypeChangedTurn, ExclusiveTurn, SameGrade };

    static readonly string[] StatNames = { "", "공격", "방어", "특공", "특방", "속도", "명중", "회피" };

    static readonly Regex Invert = new Regex(@"^(?<s>자신|상대)의\s*능력(?:\s*변화)?(?:이|가)\s*반전(?:했을|되었을|됐을)\s*때$");
    static readonly Regex InvertT = new Regex(@"^(?<s>자신|상대)의\s*능력(?:\s*변화)?(?:이|가)\s*반전(?:한|된)\s*T(?:\s*일\s*때)?$");
    static readonly Regex Lowered = new Regex(@"^자신이\s*상대의\s*(?<st>「[^」]+」|능력(?:치)?)(?:을|를)\s*(?<big>크게\s*)?내렸을\s*때$");
    static readonly Regex Priority = new Regex(@"^자신의\s*기술(?:의\s*우선도가|이\s*우선도)\s*「(?<n>[+＋\-]?\d+)」\s*이상\s*일\s*때$");
    static readonly Regex TypeChanged = new Regex(@"^(?<s>자신|상대)의\s*타입이\s*(?:바뀌었을|바뀐|변했을|변화했을)\s*때$");
    static readonly Regex TypeBecame = new Regex(@"^(?<s>자신|상대)의\s*타입이\s*(?<t>(?:「[^」]+」)+)(?:이|가)\s*(?:되었을|됐을)\s*때$");
    static readonly Regex TypeChangedT = new Regex(@"^(?<s>자신|상대)의\s*타입이\s*바뀐\s*T(?:\s*일\s*때)?$");
    static readonly Regex TypeBecameT = new Regex(@"^(?<s>자신|상대)의\s*타입이\s*(?<t>(?:「[^」]+」)+)(?:이|가)\s*된\s*T(?:\s*일\s*때)?$");
    static readonly Regex Received = new Regex(@"^『전용』(?:을|를)\s*받았을\s*때$");
    static readonly Regex ReceivedT = new Regex(@"^『전용』(?:을|를)\s*받은\s*T(?:\s*일\s*때)?$");
    static readonly Regex ReceivedEntry = new Regex(@"^『전용』(?:을|를)\s*받아\s*(?:장소|필드)에\s*(?:나올|나왔을)\s*때$");
    static readonly Regex Grade = new Regex(@"^(?<s>자신|상대)의\s*「(?<st>체력|공격|방어|특공|특방|속도)」(?:의)?\s*종족치가\s*(?:자신|상대)?(?:과|와)?\s*(?:같은\s*등급|동랭크|동등급|동일\s*등급)(?:일\s*때|일때)$");

    public static bool IsMine(TriggerSpec spec)
    {
      return spec != null && (EventNames.Contains(spec.Module) || StateNames.Contains(spec.Module));
    }

    static string Particle(string word, string withBatchim, string without)
    {
      if (string.IsNullOrEmpty(word))
        return without;
      char ch = word[word.Length - 1];
      if (ch == '」' && word.Length > 1)
        ch = word[word.Length - 2];
      return ch >= '가' && ch <= '힣' && (ch - '가') % 28 != 0 ? withBatchim : without;
    }

    // 「공격」「방어/특방」「전능력치」 → 능력 번호 목록(빈 목록 = 임의의 능력). 모르는 말이면 null
    static int[] ReadStats(string raw)
    {
      string text = (raw ?? "").Trim('「', '」', ' ');
      if (text.Length == 0 || text.StartsWith("능력"))
        return new int[0];
      if (text == "전능력치" || text == "전능력")
        return new[] { 1, 2, 3, 4, 5 };
      var list = new List<int>();
      foreach (string part in text.Split('/'))
      {
        int index = Array.IndexOf(StatNames, part.Trim());
        if (index <= 0)
          return null;
        if (!list.Contains(index))
          list.Add(index);
      }

      return list.ToArray();
    }

    static string StatPhrase(int[] stats)
    {
      if (stats == null || stats.Length == 0)
        return "능력";
      if (stats.SequenceEqual(new[] { 1, 2, 3, 4, 5 }))
        return "「전능력치」";
      return "「" + string.Join("/", stats.Select(i => StatNames[i])) + "」";
    }

    static string[] ReadTypes(string raw)
    {
      var types = Regex.Matches(raw ?? "", "「([^」]+)」").Cast<Match>().SelectMany(m => m.Groups[1].Value.Split('/')).Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
      return types.Length > 0 && types.All(MoveCondition.TypeNames.Contains) ? types : null;
    }

    public static bool TryParse(string text, out TriggerSpec spec)
    {
      spec = null;
      string t = (text ?? "").Trim();
      Match m = Invert.Match(t);
      if (m.Success)
      {
        spec = new TriggerSpec { Module = InvertEvent, Subject = m.Groups["s"].Value, Opponent = m.Groups["s"].Value == "상대" };
        return true;
      }

      m = InvertT.Match(t);
      if (m.Success)
      {
        spec = new TriggerSpec { Module = InvertTurn, Subject = m.Groups["s"].Value, Opponent = m.Groups["s"].Value == "상대" };
        return true;
      }

      m = Lowered.Match(t);
      if (m.Success)
      {
        var stats = ReadStats(m.Groups["st"].Value);
        if (stats == null)
          return false;
        spec = new TriggerSpec { Module = LoweredEvent, Subject = "상대", Opponent = true, Stats = stats, Big = m.Groups["big"].Success };
        return true;
      }

      m = Priority.Match(t);
      if (m.Success)
      {
        int n = int.Parse(m.Groups["n"].Value.Replace('＋', '+'));
        if (n < -7 || n > 7)
          return false;
        spec = new TriggerSpec { Module = PriorityState, Top = n };
        return true;
      }

      foreach (var entry in new[] { Tuple.Create(TypeChanged, TypeChangedEvent, false), Tuple.Create(TypeChangedT, TypeChangedTurn, false), Tuple.Create(TypeBecame, TypeChangedEvent, true), Tuple.Create(TypeBecameT, TypeChangedTurn, true) })
      {
        m = entry.Item1.Match(t);
        if (!m.Success)
          continue;
        string[] types = new string[0];
        if (entry.Item3)
        {
          types = ReadTypes(m.Groups["t"].Value);
          if (types == null)
            return false;
        }

        spec = new TriggerSpec { Module = entry.Item2, Subject = m.Groups["s"].Value, Opponent = m.Groups["s"].Value == "상대", Names = types };
        return true;
      }

      if (Received.IsMatch(t))
      {
        spec = new TriggerSpec { Module = ExclusiveEvent };
        return true;
      }

      if (ReceivedT.IsMatch(t))
      {
        spec = new TriggerSpec { Module = ExclusiveTurn };
        return true;
      }

      if (ReceivedEntry.IsMatch(t))
      {
        spec = new TriggerSpec { Module = ExclusiveEntry };
        return true;
      }

      m = Grade.Match(t);
      if (m.Success)
      {
        spec = new TriggerSpec { Module = SameGrade, Subject = m.Groups["s"].Value, Opponent = m.Groups["s"].Value == "상대", Names = new[] { m.Groups["st"].Value } };
        return true;
      }

      return false;
    }

    public static string Canonical(TriggerSpec s)
    {
      string owner = s.Subject == "상대" ? "상대" : "자신";
      switch (s.Module)
      {
        case InvertEvent:
          return owner + "의 능력이 반전했을 때";
        case InvertTurn:
          return owner + "의 능력이 반전한 T";
        case LoweredEvent:
          string stat = StatPhrase(s.Stats);
          return "자신이 상대의 " + stat + Particle(stat, "을", "를") + " " + (s.Big ? "크게 " : "") + "내렸을 때";
        case PriorityState:
          return "자신의 기술의 우선도가 「" + (s.Top > 0 ? "+" : "") + s.Top + "」 이상일 때";
        case TypeChangedEvent:
          return s.Names.Length == 0 ? owner + "의 타입이 바뀌었을 때" : owner + "의 타입이 " + string.Concat(s.Names.Select(x => "「" + x + "」")) + Particle(s.Names.Last(), "이", "가") + " 되었을 때";
        case TypeChangedTurn:
          return s.Names.Length == 0 ? owner + "의 타입이 바뀐 T" : owner + "의 타입이 " + string.Concat(s.Names.Select(x => "「" + x + "」")) + Particle(s.Names.Last(), "이", "가") + " 된 T";
        case ExclusiveEvent:
          return "『전용』을 받았을 때";
        case ExclusiveTurn:
          return "『전용』을 받은 T";
        case ExclusiveEntry:
          return "『전용』을 받아 장소에 나올 때";
        case SameGrade:
          return owner + "의 「" + s.Names[0] + "」 종족치가 " + (owner == "상대" ? "자신" : "상대") + "과 같은 등급일 때";
      }

      return "";
    }

    static IEnumerable<TriggerFact> Facts(Fighter owner)
    {
      return (owner == null || owner.Conditions == null ? null : owner.Conditions.TurnFacts) ?? new List<TriggerFact>();
    }

    static bool SubjectIsOwner(TriggerSpec spec)
    {
      return spec.Subject != "상대";
    }

    static bool TypesMatch(TriggerSpec spec, TriggerFact fact)
    {
      if (spec.Names.Length == 0)
        return true;
      var now = (fact.Method ?? "").Split('/');
      return spec.Names.All(now.Contains);
    }

    static bool IsExclusiveFact(TriggerFact f)
    {
      return f != null && f.Kind == ExclusiveFact && f.Self;
    }

    // 사건 대조. 「…한 T」 상태도 같은 사건의 순간에 성립한다
    public static bool Matches(TriggerSpec s, TriggerFact f, Fighter owner)
    {
      if (s == null || f == null)
        return false;
      switch (s.Module)
      {
        case InvertEvent:
        case InvertTurn:
          return f.Kind == InvertFact && f.Self == SubjectIsOwner(s);
        case LoweredEvent:
          if (f.Kind != "랭크" || f.Self || f.Delta >= 0 || !f.ByOpponent)
            return false;
          if (s.Big && -f.Delta < 2)
            return false;
          return s.Stats.Length == 0 || s.Stats.Contains(f.Stat);
        case TypeChangedEvent:
        case TypeChangedTurn:
          return f.Kind == TypeFact && f.Self == SubjectIsOwner(s) && TypesMatch(s, f);
        case ExclusiveEvent:
        case ExclusiveTurn:
          return IsExclusiveFact(f);
        case ExclusiveEntry:
          return f.Kind == "등장" && f.Self && Facts(owner).Any(IsExclusiveFact);
      }

      return false;
    }

    // 지금 성립하는가(상태 모듈)
    public static bool Holds(TriggerSpec s, Fighter owner, Fighter opponent, Rules rules)
    {
      if (s == null || owner == null)
        return false;
      switch (s.Module)
      {
        case InvertTurn:
          return Facts(owner).Any(x => x != null && x.Kind == InvertFact && x.Self == SubjectIsOwner(s));
        case TypeChangedTurn:
          return Facts(owner).Any(x => x != null && x.Kind == TypeFact && x.Self == SubjectIsOwner(s) && TypesMatch(s, x));
        case ExclusiveTurn:
          return Facts(owner).Any(IsExclusiveFact);
        case PriorityState:
          return AbaddonRuntime.MovePriorityAtLeast(owner, opponent, rules, s.Top);
        case SameGrade:
          return SameSpeciesGrade(s, owner, opponent);
      }

      return false;
    }

    static readonly int[] GradeStarts = { 1, 40, 60, 90, 110, 140, 170, 200 };

    static int Tier(int value)
    {
      for (int i = GradeStarts.Length - 1; i >= 0; i--)
        if (value >= GradeStarts[i])
          return i;
      return 0;
    }

    static bool SameSpeciesGrade(TriggerSpec s, Fighter owner, Fighter opponent)
    {
      int index = SpeciesGradeTrigger.StatIndex(s.Names[0]);
      if (index < 0 || opponent == null || opponent.Data == null || owner.Data == null)
        return false;
      var a = owner.Data.@base;
      var b = opponent.Data.@base;
      if (a == null || b == null || a.Length <= index || b.Length <= index)
        return false;
      return Tier(a[index]) == Tier(b[index]);
    }

    // ===== 사건 발생 =====
    // 타입이 바뀐 것을 전후 비교로 알아낸다(StateWatch.Check가 포켓몬마다 부른다)
    public static void TypesChanged(Fighter f, Fighter other, string before, Action<string> log)
    {
      if (f == null || f.Data == null || log == null)
        return;
      string now = string.Join("/", f.Data.types ?? new string[0]);
      if (before == null || before == now)
        return;
      TriggerModules.Raise(new TriggerFact { Kind = TypeFact, Method = now }, f, other, f.Data.name + "의 타입이 바뀌었다 (" + before + " → " + now + ")", log);
    }

    public static void ResetTypes(Fighter f)
    {
      if (f != null && f.Data != null)
        f.Conditions.WatchTypes = string.Join("/", f.Data.types ?? new string[0]);
    }

    // 트레이너가 이 포켓몬에게 전용 포텐셜을 발동했다
    public static void ExclusiveUsed(PotentialInstance source, Action<string> log)
    {
      if (source == null || source.Pokemon == null || source.Pokemon.id != TeamRules.ExclusiveSlot || source.Owner == null)
        return;
      var owner = source.Owner;
      TriggerModules.Raise(new TriggerFact { Kind = ExclusiveFact }, owner, PotentialQueue.OpponentOf(owner), owner.Data.name + ": 『" + source.Name + "』(전용)을 받았다", log);
    }
  }

  public static partial class ModuleCatalog
  {
    static void RegisterAbaddonTriggers(List<ModuleDefinition> modules)
    {
      var subject = S("주체", ModuleSlotKind.Subject, "상대", "자신", "상대");
      TriggerBinding subjectBinding = TB(subject, (s, v) => { s.Subject = v; s.Opponent = v == "상대"; }, s => s.Subject);
      T(modules, "invert.event", AbaddonTriggers.InvertEvent, "능력 반전됨", "체력·상태", "{주체}의 능력이 반전했을 때",
        () => new TriggerSpec { Module = AbaddonTriggers.InvertEvent, Subject = "상대", Opponent = true }, subjectBinding);
      T(modules, "invert.turn", AbaddonTriggers.InvertTurn, "능력 반전이 일어난 T", "체력·상태", "{주체}의 능력이 반전한 T",
        () => new TriggerSpec { Module = AbaddonTriggers.InvertTurn, Subject = "상대", Opponent = true }, subjectBinding);
      foreach (bool big in new[] { false, true })
      {
        bool b = big;
        var stats = TRankStats();
        T(modules, "lowered." + b, AbaddonTriggers.LoweredEvent, b ? "상대 능력을 크게 내림" : "상대 능력을 내림", "체력·상태",
          "자신이 상대의 {능력}을(를) " + (b ? "크게 " : "") + "내렸을 때",
          () => new TriggerSpec { Module = AbaddonTriggers.LoweredEvent, Subject = "상대", Opponent = true, Big = b, Stats = new[] { 2 } }, stats);
      }

      var priority = S("우선도", ModuleSlotKind.Count, "1");
      priority.Minimum = 0;
      priority.Maximum = 7;
      T(modules, "priority.atleast", AbaddonTriggers.PriorityState, "기술 우선도 조건", "턴·행동", "자신의 기술의 우선도가 「+{우선도}」 이상일 때",
        () => new TriggerSpec { Module = AbaddonTriggers.PriorityState, Top = 1 },
        TB(priority, (s, v) => s.Top = int.Parse(v), s => s.Top.ToString()));
      foreach (bool turn in new[] { false, true })
      {
        bool isTurn = turn;
        T(modules, "typechanged." + isTurn, isTurn ? AbaddonTriggers.TypeChangedTurn : AbaddonTriggers.TypeChangedEvent, isTurn ? "타입이 바뀐 T" : "타입이 바뀜", "체력·상태",
          isTurn ? "{주체}의 타입이 바뀐 T" : "{주체}의 타입이 바뀌었을 때",
          () => new TriggerSpec { Module = isTurn ? AbaddonTriggers.TypeChangedTurn : AbaddonTriggers.TypeChangedEvent, Subject = "상대", Opponent = true }, subjectBinding);
        T(modules, "typebecame." + isTurn, isTurn ? AbaddonTriggers.TypeChangedTurn : AbaddonTriggers.TypeChangedEvent, isTurn ? "지정 타입이 된 T" : "지정 타입이 됨", "체력·상태",
          isTurn ? "{주체}의 타입이 「{타입}」이 된 T" : "{주체}의 타입이 「{타입}」이 되었을 때",
          () => new TriggerSpec { Module = isTurn ? AbaddonTriggers.TypeChangedTurn : AbaddonTriggers.TypeChangedEvent, Subject = "상대", Opponent = true },
          subjectBinding, TB(S("타입", ModuleSlotKind.Type, "불꽃"), (s, v) => s.Names = v.Split('/'), s => string.Join("/", s.Names)));
      }

      TF(modules, "exclusive.received", AbaddonTriggers.ExclusiveEvent, "『전용』을 받음", "역할·트레이너", "『전용』을 받았을 때");
      TF(modules, "exclusive.received.turn", AbaddonTriggers.ExclusiveTurn, "『전용』을 받은 T", "역할·트레이너", "『전용』을 받은 T");
      TF(modules, "exclusive.received.entry", AbaddonTriggers.ExclusiveEntry, "『전용』을 받아 등장", "역할·트레이너", "『전용』을 받아 장소에 나올 때");
      T(modules, "species.samegrade", AbaddonTriggers.SameGrade, "종족치 같은 등급", "종족치", "상대의 「{능력}」 종족치가 자신과 같은 등급일 때",
        () => new TriggerSpec { Module = AbaddonTriggers.SameGrade, Subject = "상대", Opponent = true, Names = new[] { "속도" } },
        TB(S("능력", ModuleSlotKind.Stats, "속도", "체력", "공격", "방어", "특공", "특방", "속도"), (s, v) => s.Names = new[] { v }, s => s.Names[0]));
    }
  }
}
