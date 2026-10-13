using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 // 특성 모듈화 3차의 사건 효과 모듈(실행기). 사건(PotentialEvent.Fact)의 정보를 읽는 것들이다.
 //  랭크 지정      자신의 「공격」 랭크를 「+6」으로 한다.
 //  타입 복사      자신의 타입을 상대와 같은 타입으로 한다.
 //  받은 기술 타입 자신의 타입을 받은 기술의 타입으로 한다.
 //  상태이상 반사  상대를 자신과 같은 상태이상으로 한다.
 //  랭크 따라가기  자신의 능력을 상대가 올린 만큼 올린다.
 //  받은 대미지 반격 상대에게 자신이 마지막으로 받은 대미지만큼 고정 대미지를 준다.
 public static class AbilityEventModules {
  public const string CopyType = "자신의 타입을 상대와 같은 타입으로 한다";
  public const string TakenType = "자신의 타입을 받은 기술의 타입으로 한다";
  public const string MirrorStatus = "상대를 자신과 같은 상태이상으로 한다";
  public const string FollowRise = "자신의 능력을 상대가 올린 만큼 올린다";
  static readonly Regex RandomStatus = new Regex(@"^상대를 「(?<s>[^」]+)」 중 무작위 상태이상으로 한다$");
  public const string RestoreGuard = "자신의 1번만 효과의 사용 횟수를 복구한다";
  public const string ClearScreens = "전체 필드의 「벽」을 해제한다";
  public const string ReturnDamage = "상대에게 자신이 마지막으로 받은 대미지만큼 고정 대미지를 준다";
  static readonly Regex SetRank = new Regex(@"^자신의 「(?<st>[^」]+)」 랭크를 「(?<n>[+-]?\d)」(?:으로|로) 한다$");

  static string Clean(string text) {
   return Regex.Replace(PotentialParts.Clean(text ?? ""), @"\s+", " ").Trim().TrimEnd('.', '。', '．').Trim();
  }
  public static string Normalize(string text) {
   string s = Clean(text);
   if (s.Length == 0) return null;
   // 수집 원문: 「능력변화:공격」을 「＋6」으로 한다
   var m = Regex.Match(s, @"^자신의 「능력변화\s*:\s*(?<st>[^」]+)」을 「(?<n>[+-]?\d)」(?:으로|로) 한다$");
   if (m.Success && Classify("자신의 「" + m.Groups["st"].Value + "」 랭크를 「" + m.Groups["n"].Value + "」으로 한다").Length > 0)
    return "자신의 「" + m.Groups["st"].Value + "」 랭크를 「" + m.Groups["n"].Value + "」" + EffectModules.Ro(m.Groups["n"].Value) + " 한다.";
   if (Classify(s).Length > 0) return s + ".";
   return null;
  }
  public static string Classify(string sentence) {
   string s = Clean(sentence);
   if (s == CopyType) return "타입 복사";
   if (s == TakenType) return "받은 기술 타입으로 변화";
   if (s == MirrorStatus) return "상태이상 반사";
   if (s == FollowRise) return "랭크 따라가기";
   if (s == ReturnDamage) return "받은 대미지 반격";
   if (s == ClearScreens) return "전체 필드 벽 해제";
   if (s == RestoreGuard) return "1번만 효과 복구";
   var random = RandomStatus.Match(s);
   if (random.Success && random.Groups["s"].Value.Split('/').Length > 1 && random.Groups["s"].Value.Split('/').All(x => Mechanics.Statuses.Contains(x.Trim()) && x.Trim() != "정상")) return "무작위 상태이상 부여";
   var m = SetRank.Match(s);
   if (m.Success && EffectModules.StatIndex(m.Groups["st"].Value) >= 1 && EffectModules.StatIndex(m.Groups["st"].Value) <= 5) return "랭크 지정";
   return "";
  }

  static void SetTypes(Fighter f, string[] types, List<string> log, string label) {
   if (f.Conditions.OriginalTypes == null) f.Conditions.OriginalTypes = f.Data.types.ToArray();
   f.Data.types = types.ToArray();
   if (log != null) log.Add(f.Data.name + ": 타입이 「" + string.Join("/", types) + "」로 바뀌었다 (" + label + ")");
  }

  public static void Register() {
   PotentialExecutors.Register("랭크 지정", c => SetRank.IsMatch(Clean(c.Line)), c => {
    var m = SetRank.Match(Clean(c.Line));
    var target = c.Owner;
    if (target == null || target.HP <= 0) return EffectOutcome.Failed;
    int index = EffectModules.StatIndex(m.Groups["st"].Value), value = Math.Max(-6, Math.Min(6, int.Parse(m.Groups["n"].Value)));
    if (target.Stages[index] == value) return EffectOutcome.Failed;
    int before = target.Stages[index];
    target.Stages[index] = value;
    c.Say(target.Data.name + ": " + Engine.Names[index] + " " + before + "→" + value + " (" + c.Source.Label + ")");
    StateWatch.CheckCurrent(c.Say);
    return EffectOutcome.Success;
   });
   PotentialExecutors.Register("타입 복사", c => Clean(c.Line) == CopyType, c => {
    var self = c.Owner; var other = c.Opponent;
    if (self == null || other == null || self.HP <= 0 || other.HP <= 0) return EffectOutcome.Failed;
    var types = other.Data.types ?? new string[0];
    if (types.Length == 0 || self.Data.types.Length == types.Length && self.Data.types.All(types.Contains)) return EffectOutcome.Failed;
    SetTypes(self, types, c.Log, c.Source.Label);
    return EffectOutcome.Success;
   });
   PotentialExecutors.Register("받은 기술 타입으로 변화", c => Clean(c.Line) == TakenType, c => {
    var self = c.Owner;
    var move = c.Event.Fact == null ? c.Event.Move : c.Event.Fact.Move ?? c.Event.Move;
    if (self == null || self.HP <= 0 || move == null || move.types == null || move.types.Length == 0) return EffectOutcome.Failed;
    if (self.Data.types.Length == move.types.Length && self.Data.types.All(move.types.Contains)) return EffectOutcome.Failed;
    SetTypes(self, move.types, c.Log, c.Source.Label);
    return EffectOutcome.Success;
   });
   PotentialExecutors.Register("상태이상 반사", c => Clean(c.Line) == MirrorStatus, c => {
    var self = c.Owner; var other = c.Opponent;
    string status = c.Event.Fact == null ? "" : c.Event.Fact.Method;
    if (self == null || other == null || other.HP <= 0 || !Mechanics.Statuses.Contains(status) || status == "정상") return EffectOutcome.Failed;
    var log = c.Log ?? new List<string>();
    return Mechanics.ApplyStatus(other, self, status, c.Rules ?? new Rules(), c.Rng ?? (() => .5), log) ? EffectOutcome.Success : EffectOutcome.Failed;
   });
   PotentialExecutors.Register("랭크 따라가기", c => Clean(c.Line) == FollowRise, c => {
    var self = c.Owner; var fact = c.Event.Fact;
    if (self == null || self.HP <= 0 || fact == null || fact.Kind != "랭크" || fact.Delta <= 0 || fact.Stat < 1 || fact.Stat > 5) return EffectOutcome.Failed;
    return Mechanics.ChangeStage(self, c.Opponent, fact.Stat, fact.Delta, c.Rules ?? new Rules(), c.Source.Label, c.Log ?? new List<string>()) ? EffectOutcome.Success : EffectOutcome.Failed;
   });
   PotentialExecutors.Register("무작위 상태이상 부여", c => RandomStatus.IsMatch(Clean(c.Line)) && Classify(c.Line).Length > 0, c => {
    var m = RandomStatus.Match(Clean(c.Line));
    var choices = m.Groups["s"].Value.Split('/').Select(x => x.Trim()).ToArray();
    var other = c.Opponent;
    if (c.Owner == null || other == null || other.HP <= 0) return EffectOutcome.Failed;
    var rng = c.Rng ?? (() => .5);
    string status = choices[Math.Min(choices.Length - 1, (int)(rng() * choices.Length))];
    return Mechanics.ApplyStatus(other, c.Owner, status, c.Rules ?? new Rules(), rng, c.Log ?? new List<string>()) ? EffectOutcome.Success : EffectOutcome.Failed;
   });
   PotentialExecutors.Register("1번만 효과 복구", c => Clean(c.Line) == RestoreGuard, c => {
    var self = c.Owner;
    if (self == null || self.HP <= 0 || self.Conditions.GuardUsed.Count == 0) return EffectOutcome.Failed;
    self.Conditions.GuardUsed.Clear();
    c.Say(self.Data.name + ": 1번만 쓸 수 있는 방호의 사용 횟수가 복구되었다 (" + c.Source.Label + ")");
    return EffectOutcome.Success;
   });
   PotentialExecutors.Register("전체 필드 벽 해제", c => Clean(c.Line) == ClearScreens, c => {
    var rules = c.Rules;
    if (rules == null) return EffectOutcome.Failed;
    bool any = false;
    for (int side = 0; side < 2; side++) {
     var s = rules.Sides[side];
     if (s.Reflect > 0 || s.LightScreen > 0) { s.Reflect = 0; s.LightScreen = 0; any = true; c.Say((side + 1) + "팀 진영의 벽이 해제되었다 (" + c.Source.Label + ")"); }
    }
    return any ? EffectOutcome.Success : EffectOutcome.Failed;
   });
   PotentialExecutors.Register("받은 대미지 반격", c => Clean(c.Line) == ReturnDamage, c => {
    var self = c.Owner; var other = c.Opponent;
    int amount = self == null ? 0 : self.Conditions.LastDamageTaken;
    if (other == null || other.HP <= 0 || amount <= 0) return EffectOutcome.Failed;
    int before = other.HP;
    Mechanics.Hurt(other, self, c.Rules ?? new Rules(), amount, c.Source.Label, c.Log ?? new List<string>());
    return other.HP < before ? EffectOutcome.Success : EffectOutcome.Failed;
   });
  }
 }
}
