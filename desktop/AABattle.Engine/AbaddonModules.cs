// 실프 인헨스먼츠(MODULE_GAPS 묶음 A)의 효과 모듈: 표준 문장 · 정규화 · 분류 · 실행기.
//  상대의 능력 변화를 반전한다.                         랭크 부호 반전(할 수 있다 선택형 포함) + 반전 사건
//  상대에게 최대 HP의 1/8 「악」 타입 대미지를 준다.      타입을 단 대미지는 타입 상성을 받는다(사용자 결정)
//  상대의 기술을 「독」 타입으로 한다.                   상대가 내보내는 기술의 타입 변환(계속 효과)
//  상대의 능력 하락 1랭크마다 상대 기술의 위력을 약화(0.9배)한다.
//  상대의 「상태이상」은 치유되지 않는다.
//  자신의 「기술:푸른불꽃」 기술에 「접촉」 효과를 부여한다.
//  아군이 장소에 나와 최초로 내보내는 「접촉」 기술의 우선도를 2 올릴 수 있다.
//  자진의 포켓몬의 체력을 가득 회복한다. / 회복한 체력만큼 턴 끝에 대미지를 입는다(빈사 불가). / 이 포텐셜은 자신이 필드에 없어도 발동한다.
// 연결: EffectModules.NormalizeBody · ClassifySentence · PotentialExecutors · ModuleCatalog
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AABattle
{
  public static class AbaddonModules
  {
    public const string KindInvert = "능력 반전";
    public const string KindFoeType = "상대 기술 타입 변경";
    public const string KindLowerPower = "하락 비례 위력";
    public const string KindSeal = "상태이상 치유 봉인";
    public const string KindContact = "기술 접촉 부여";
    public const string KindFirst = "최초 기술 우선도";
    public const string KindFullHeal = "체력 가득 회복";
    public const string KindRepay = "회복 반납";
    public const string KindOffField = "필드 밖 발동";
    public const string RepaySentence = "회복한 체력만큼 턴 끝에 대미지를 입는다(빈사 불가).";
    public const string FullHealSentence = "자진의 포켓몬의 체력을 가득 회복한다.";
    public const string OffFieldSentence = "이 포텐셜은 자신이 필드에 없어도 발동한다.";
    public const string OptionalSwitchSentence = "아군과 임의교대할 수 있다.";
    public const string AttackPivotSentence = "자신의 공격기술에 「유턴」 효과를 부여한다.";

    const string Cond = @"(?:「[^」]+」)*";
    static readonly Regex Duration = new Regex(@"^(?:(?<n>\d+)T\s*(?:간|동안)|T\s*종료\s*시\s*까지)\s*");
    static readonly Regex Invert = new Regex(@"^(?<t>자신|상대)의 능력 변화를 반전(?<o>한다|할 수 있다)$");
    static readonly Regex FoeType = new Regex(@"^상대의 (?<c>" + Cond + @")\s*기술을 「(?<ty>[^」]+)」 타입으로 한다$");
    static readonly Regex Lower = new Regex(@"^상대의 능력 하락 1랭크마다 상대 기술의 위력을 약화\((?<r>[0-9.]+)배\)한다$");
    static readonly Regex Seal = new Regex(@"^(?<t>자신|상대)의 「상태이상」은 치유되지 않는다$");
    static readonly Regex Contact = new Regex(@"^자신의 (?<c>" + Cond + @")\s*기술에 「접촉」 효과를 부여한다$");
    static readonly Regex First = new Regex(@"^아군이 장소에 나와 최초로 내보내는 (?<c>" + Cond + @")\s*기술의 우선도를 (?<n>[1-7]) 올릴 수 있다$");

    static string Clean(string text)
    {
      return Regex.Replace(PotentialParts.Clean(text ?? ""), @"\s+", " ").Trim().TrimEnd('.', '。', '．').Trim();
    }

    // 「무릎차기」처럼 기술 이름이면 「기술:무릎차기」, 「접촉기술」은 「접촉」, 「다크」는 「악」
    static string ConvertCondition(string raw)
    {
      var tokens = Regex.Matches(raw ?? "", "「([^」]*)」").Cast<Match>().Select(m => m.Groups[1].Value.Trim()).ToList();
      var result = new List<string>();
      foreach (string token in tokens)
      {
        string t = token;
        if (t == "다크")
          t = "악";
        if (t.EndsWith("기술") && t.Length > 2)
          t = t.Substring(0, t.Length - 2);
        if (!t.StartsWith("기술:") && MoveNames.Known(t))
          t = "기술:" + t;
        result.Add("「" + t + "」");
      }

      MoveCondition condition;
      string text = string.Concat(result);
      return MoveCondition.TryParse(text, out condition) ? (condition.Empty ? "" : condition.Text + " ") : null;
    }

    // 수집 원문의 변형 → 표준 문장. 아니면 null
    public static string Normalize(string text)
    {
      string s = Clean(text);
      if (s.Length == 0)
        return null;
      Match m;
      m = Regex.Match(s, @"^(?<t>자신|상대)의 능력 변화를 반전(?<o>한다|할 수 있다)$");
      if (m.Success)
        return m.Groups["t"].Value + "의 능력 변화를 반전" + m.Groups["o"].Value + ".";
      m = Regex.Match(s, @"^상대(?:가|의) 내보내는 (?<c>(?:「[^」]+」)*)\s*기술(?:이|을) 「(?<ty>[^」]+)」\s*타입(?:이 된다|으로 (?:한다|바꾼다))$");
      if (m.Success && MoveCondition.TypeNames.Contains(m.Groups["ty"].Value))
      {
        string c = ConvertCondition(m.Groups["c"].Value);
        if (c != null)
          return "상대의 " + c + "기술을 「" + m.Groups["ty"].Value + "」 타입으로 한다.";
      }

      m = Regex.Match(s, @"^상대 능력의 「-」에 비례해,? 상대 기술의 위력을 (?:완화|약화)한다(?:\s*\(\s*-1\s*=\s*(?<r>[0-9.]+)배.*\))?$");
      if (m.Success)
        return "상대의 능력 하락 1랭크마다 상대 기술의 위력을 약화(" + (m.Groups["r"].Success ? m.Groups["r"].Value : "0.9") + "배)한다.";
      m = Regex.Match(s, @"^(?<t>자신|상대)의 「상태이상」(?:이|은) (?:해제되지|치유되지) 않는다$");
      if (m.Success)
        return m.Groups["t"].Value + "의 「상태이상」은 치유되지 않는다.";
      m = Regex.Match(s, @"^「(?<n>[^」]+)」(?:이|가) 「접촉(?:기술)?」(?:이|가) 되어,? 상대의 방호 ?특성을 관통한다$");
      if (m.Success)
      {
        string c = ConvertCondition("「" + m.Groups["n"].Value + "」");
        if (c != null)
          return "자신의 " + c + "기술에 「접촉」 효과를 부여한다. 자신의 " + c + "기술은 상대의 〔방호 특성〕을 관통한다";
      }

      m = Regex.Match(s, @"^「(?<n>[^」]+)」의 (?:반동 )?(?:대미지|데미지)(?:를|을) 받지 않는다$");
      if (m.Success)
      {
        string c = ConvertCondition("「" + m.Groups["n"].Value + "」");
        if (c != null)
          return "자신의 " + c + "기술의 반동 대미지를 받지 않는다.";
      }

      m = Regex.Match(s, @"^자신의 (?<c>(?:「[^」]+」)+)의 타입을 「(?<ty>[^」]+)」\s*타입으로 한다$");
      if (m.Success)
      {
        string type = m.Groups["ty"].Value == "다크" ? "악" : m.Groups["ty"].Value;
        string c = ConvertCondition(m.Groups["c"].Value);
        if (c != null && MoveCondition.TypeNames.Contains(type))
          return "자신의 " + c + "기술의 타입을 「" + type + "」" + EffectModules.Ro(type) + " 한다.";
      }

      m = Regex.Match(s, @"^아군(?:이|의) (?:필드|장소)에 나와 최초로 내보내는 (?<c>(?:「[^」]+」)+)\s*기술의 우선도를 [+＋「]*(?<n>[1-7])」?\s*(?:올릴 ?수 ?있다|할 ?수 ?있다)$");
      if (m.Success)
      {
        string c = ConvertCondition(m.Groups["c"].Value);
        if (c != null)
          return "아군이 장소에 나와 최초로 내보내는 " + c + "기술의 우선도를 " + m.Groups["n"].Value + " 올릴 수 있다.";
      }

      if (Regex.IsMatch(s, @"^자신과 같은 타입의 기술의 (?:데미지|대미지)를 (?:반감한다|약화\(0\.5배\)한다)$"))
        return "자신이 받는 「자신과 같은 타입」 기술의 대미지를 약화(0.5배)한다.";
      if (Regex.IsMatch(s, @"^자진의 포켓몬의 체력을 가득(?:으로)? 회복한다$"))
        return FullHealSentence;
      if (Regex.IsMatch(s, @"^(?:T\s*종료\s*시|T종료시)\s*회복한 체력을 (?:데미지|대미지)(?:로) 받는다$") || Regex.IsMatch(s, @"^회복한 체력만큼 턴 끝에 대미지를 입는다\(빈사 불가\)$"))
        return RepaySentence;
      if (Regex.IsMatch(s, @"^이 포텐셜은 자신이 필드에 없어도 발동(?:한다|할 수 있다)$"))
        return OffFieldSentence;
      if (Regex.IsMatch(s, @"^아군과 (?:임의)?교대(?:할| ?해 (?:필드|장소)에 나올) 수 있다$"))
        return OptionalSwitchSentence;
      if (Regex.IsMatch(s, @"^자신의 공격기술에 「유턴」 효과를 부여한다$"))
        return AttackPivotSentence;
      return null;
    }

    public static string Classify(string text)
    {
      string s = Clean(text);
      if (Invert.IsMatch(s))
        return KindInvert;
      var foe = FoeType.Match(s);
      MoveCondition condition;
      if (foe.Success && MoveCondition.TypeNames.Contains(foe.Groups["ty"].Value) && MoveCondition.TryParse(foe.Groups["c"].Value, out condition))
        return KindFoeType;
      if (Lower.IsMatch(s))
        return KindLowerPower;
      if (Seal.IsMatch(s))
        return KindSeal;
      var contact = Contact.Match(s);
      if (contact.Success && MoveCondition.TryParse(contact.Groups["c"].Value, out condition))
        return KindContact;
      var first = First.Match(s);
      if (first.Success && MoveCondition.TryParse(first.Groups["c"].Value, out condition))
        return KindFirst;
      if (s + "." == FullHealSentence)
        return KindFullHeal;
      if (s + "." == RepaySentence)
        return KindRepay;
      if (s + "." == OffFieldSentence)
        return KindOffField;
      return "";
    }

    // 사건으로 걸린 계속 효과의 종류
    static bool IsPersistent(string kind)
    {
      return kind == KindFoeType || kind == KindLowerPower || kind == KindSeal || kind == KindContact || kind == KindFirst;
    }

    static string StripDuration(string line, out int turns)
    {
      turns = -1;
      string s = Clean(line);
      var m = Duration.Match(s);
      if (!m.Success)
        return s;
      turns = m.Groups["n"].Success ? int.Parse(m.Groups["n"].Value) : 1;
      return s.Substring(m.Length);
    }

    [ThreadStatic]
    static Fighter lastHealed;
    [ThreadStatic]
    static int lastHealAmount;

    public static void Register()
    {
      PotentialExecutors.Register("능력 반전", c => Invert.IsMatch(Clean(c.Line)), RunInvert);
      PotentialExecutors.Register("실프 계속 효과", c => { int t; return IsPersistent(Classify(StripDuration(c.Line, out t))); }, RunPersistent);
      PotentialExecutors.Register("체력 가득 회복", c => Classify(c.Line) == KindFullHeal, RunFullHeal);
      PotentialExecutors.Register("회복 반납", c => Classify(c.Line) == KindRepay, RunRepay);
      PotentialExecutors.Register("필드 밖 발동", c => Classify(c.Line) == KindOffField, c => EffectOutcome.Success);
    }

    static EffectOutcome RunInvert(EffectCall c)
    {
      var m = Invert.Match(Clean(c.Line));
      var target = m.Groups["t"].Value == "상대" ? c.Opponent : c.Owner;
      if (target == null || target.HP <= 0)
        return EffectOutcome.Failed;
      bool any = false;
      for (int i = 1; i <= 5; i++)
        any |= target.Stages[i] != 0;
      any |= target.AccuracyStage != 0 || target.EvasionStage != 0;
      if (!any)
        {
          c.Say(target.Data.name + ": 반전할 능력 변화가 없다");
          return EffectOutcome.Failed;
        }

      for (int i = 1; i <= 5; i++)
        target.Stages[i] = -target.Stages[i];
      target.AccuracyStage = -target.AccuracyStage;
      target.EvasionStage = -target.EvasionStage;
      c.Say(target.Data.name + ": 능력 변화가 반전했다 (" + c.Source.Label + ")");
      bool byOther = !ReferenceEquals(target, c.Owner);
      target.Conditions.StageByOpponent = byOther;
      StateWatch.CheckCurrent(c.Say);
      target.Conditions.StageByOpponent = false;
      var other = byOther ? c.Owner : c.Opponent;
      TriggerModules.Raise(new TriggerFact { Kind = AbaddonTriggers.InvertFact }, target, other, target.Data.name + "의 능력이 반전했다", c.Say);
      return EffectOutcome.Success;
    }

    static EffectOutcome RunPersistent(EffectCall c)
    {
      int turns;
      string body = StripDuration(c.Line, out turns);
      var owner = c.Owner;
      if (owner == null || owner.HP <= 0)
        return EffectOutcome.Failed;
      PotentialModifiers.Add(owner, new ActiveModifier { Key = c.Source.Key, Text = body + ".", Source = c.Source.Label, Turns = turns, Trainer = c.Source.IsTrainerPotential });
      c.Say(owner.Data.name + ": " + body + (turns > 0 ? " (" + turns + "T)" : " (필드를 떠날 때까지)"));
      return EffectOutcome.Success;
    }

    static EffectOutcome RunFullHeal(EffectCall c)
    {
      var scope = EffectScope.Current;
      var rules = c.Rules ?? new Rules();
      Fighter target = c.Owner;
      if (scope != null && scope.Fighters != null && c.Owner != null)
      {
        int side = Mechanics.PartySide(c.Owner, rules);
        if (side < 0)
          side = Array.IndexOf(scope.Fighters, c.Owner);
        if (side >= 0 && side < scope.Fighters.Length && scope.Fighters[side] != null)
          target = scope.Fighters[side];
      }

      if (target == null || target.HP <= 0)
        return EffectOutcome.Failed;
      var other = c.Opponent ?? target;
      int max = Mechanics.MaxHP(target, other, rules);
      int gain = Mechanics.Heal(target, other, rules, max - target.HP, c.Source.Label, c.Log ?? new List<string>());
      if (gain <= 0)
        return EffectOutcome.Failed;
      lastHealed = target;
      lastHealAmount = gain;
      return EffectOutcome.Success;
    }

    static EffectOutcome RunRepay(EffectCall c)
    {
      if (lastHealed == null || lastHealAmount <= 0)
      {
        c.Say(c.Source.Label + ": 회복한 체력이 없어 반납할 것이 없다");
        return EffectOutcome.Failed;
      }

      lastHealed.Conditions.Repay += lastHealAmount;
      c.Say(lastHealed.Data.name + ": 턴 끝에 " + lastHealAmount + "의 대미지를 입는다 (빈사 불가)");
      lastHealed = null;
      lastHealAmount = 0;
      return EffectOutcome.Success;
    }
  }

  public static partial class ModuleCatalog
  {
    static void RegisterAbaddonEffects(List<ModuleDefinition> modules)
    {
      foreach (string option in new[] { "한다", "할 수 있다" })
        CoreAdd(modules, "abaddon.invert." + option, "능력 변화 반전" + (option == "한다" ? "" : "(선택)"), AbaddonModules.KindInvert, "능력 랭크",
          "{대상}의 능력 변화를 반전" + option + ".", S("대상", ModuleSlotKind.Subject, "상대", "상대", "자신"));
      CoreAdd(modules, "abaddon.type.foe-move", "상대 기술을 지정 타입으로", AbaddonModules.KindFoeType, "타입·상성",
        "상대의 {조건} 기술을 「{타입}」 타입으로 한다.", S("조건", ModuleSlotKind.MoveCondition, ""), S("타입", ModuleSlotKind.Type, "독"));
      CoreAdd(modules, "abaddon.scalar.rank-lowered", "능력 하락에 비례해 위력 약화", AbaddonModules.KindLowerPower, "수치 배율",
        "상대의 능력 하락 1랭크마다 상대 기술의 위력을 약화({배율}배)한다.", S("배율", ModuleSlotKind.Multiplier, "0.9"));
      CoreAdd(modules, "abaddon.cure.seal", "상태이상 치유 봉인", AbaddonModules.KindSeal, "상태",
        "{대상}의 「상태이상」은 치유되지 않는다.", S("대상", ModuleSlotKind.Subject, "상대", "상대", "자신"));
      CoreAdd(modules, "abaddon.move.contact-grant", "기술에 접촉 효과 부여", AbaddonModules.KindContact, "기술 변형",
        "자신의 {조건} 기술에 「접촉」 효과를 부여한다.", S("조건", ModuleSlotKind.MoveCondition, "「기술:푸른불꽃」"));
      CoreAdd(modules, "abaddon.priority.first", "최초로 내보내는 기술의 우선도", AbaddonModules.KindFirst, "우선도",
        "아군이 장소에 나와 최초로 내보내는 {조건} 기술의 우선도를 {우선도} 올릴 수 있다.", S("조건", ModuleSlotKind.MoveCondition, "「접촉」"),
        CoreNumber("우선도", ModuleSlotKind.Count, "2", 7));
      CoreAdd(modules, "abaddon.heal.full", "자진 포켓몬 체력 가득 회복", AbaddonModules.KindFullHeal, "체력", AbaddonModules.FullHealSentence);
      CoreAdd(modules, "abaddon.heal.repay", "회복한 체력 반납", AbaddonModules.KindRepay, "체력", AbaddonModules.RepaySentence);
      CoreAdd(modules, "abaddon.switch.optional", "아군과 임의교대(선택)", "교대 효과", "교대", AbaddonModules.OptionalSwitchSentence);
      CoreAdd(modules, "abaddon.pivot.attack", "공격기술에 유턴 효과 부여", "유턴 효과", "교대", AbaddonModules.AttackPivotSentence);
      CoreAdd(modules, "abaddon.offfield", "필드 밖 발동 가능", AbaddonModules.KindOffField, "사용 조건", AbaddonModules.OffFieldSentence);
    }
  }
}
