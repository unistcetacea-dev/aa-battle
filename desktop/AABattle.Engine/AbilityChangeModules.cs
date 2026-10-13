using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AABattle {
 // 현재 특성의 교환·복사·보호. 등장 효과는 특성을 얻은 순간 실행하지 않는다.
 public static class AbilityChangeModules {
  public const string Swap = "자신과 상대의 특성을 교환한다";
  public const string CopyOpponent = "상대의 특성을 복사한다";
  public const string CopyFaintedAlly = "직전에 빈사한 아군의 특성을 복사한다";
  public const string ImmuneChange = "자신의 특성은 변화되지 않는다";
  public const string ImmuneSuppress = "자신의 특성은 무효화되지 않는다";
  public const string ImmuneBypass = "자신의 특성은 관통되지 않는다";

  static string Clean(string text) {
   return Regex.Replace(PotentialParts.Clean(text ?? ""), @"\s+", " ").Trim().TrimEnd('.', '。', '．').Trim()
    .Replace("〔특성〕", "특성");
  }
  public static string Normalize(string text) {
   string line = Clean(text);
   if (line == Swap || Regex.IsMatch(line, @"^자신과 상대의 특성을 (?:뒤바꾼다|교체한다)$")) return Swap + ".";
   if (line == CopyOpponent || Regex.IsMatch(line, @"^자신의 특성을 상대와 같은 특성(?:으로|로) (?:변화시킨다|변경한다|한다)$")) return CopyOpponent + ".";
   if (line == CopyFaintedAlly) return CopyFaintedAlly + ".";
   if (Regex.IsMatch(line, @"^(?:이|자신의) 특성은 (?:변화되지|변화하지|변경되지) 않는다$")) return ImmuneChange + ".";
   if (Regex.IsMatch(line, @"^(?:이|자신의) 특성은 무효화되지 않는다$")) return ImmuneSuppress + ".";
   if (Regex.IsMatch(line, @"^(?:이|자신의) 특성은 관통되지 않는다$")) return ImmuneBypass + ".";
   return null;
  }
  public static string Classify(string text) {
   string line = Clean(text);
   if (line == Swap) return "특성 교환";
   if (line == CopyOpponent || line == CopyFaintedAlly) return "특성 복사";
   if (line == ImmuneChange) return "특성 변경 불가";
   if (line == ImmuneSuppress) return "특성 무효화 불가";
   if (line == ImmuneBypass) return "특성 관통 불가";
   return "";
  }

  // 슬롯이 제거된 동안에도 현재 특성의 보호 문장을 조회한다.
  static bool Protected(Fighter owner, Fighter other, Rules rules, string sentence) {
   if (owner == null) return false;
   rules = rules ?? (EffectScope.Current == null ? new Rules() : EffectScope.Current.Rules);
   return PotentialModifiers.Sentences(owner, other, rules, "특성", AbilityModules.UnsuppressedPotentials(owner), true)
    .Any(line => Clean(line) == sentence);
  }
  public static bool CannotChange(Fighter owner, Fighter other = null, Rules rules = null) {
   return Protected(owner, other, rules, ImmuneChange);
  }
  public static bool CannotSuppress(Fighter owner, Fighter other = null, Rules rules = null) {
   return Protected(owner, other, rules, ImmuneSuppress);
  }
  public static bool CannotIgnore(Fighter owner, Fighter other = null, Rules rules = null) {
   return Protected(owner, other, rules, ImmuneBypass);
  }

  public static void Refresh(List<string> log) {
   var scope = EffectScope.Current;
   if (scope != null) AbilityRuntime.RefreshSuppression(scope.Fighters, log == null ? (Action<string>)null : log.Add);
  }
  public static EffectOutcome Exchange(Fighter owner, Fighter other, Rules rules, List<string> log, string source) {
   if (owner == null || other == null || owner.HP <= 0 || other.HP <= 0 || ReferenceEquals(owner, other)) return EffectOutcome.Failed;
   string first = AbilityRuntime.Name(owner), second = AbilityRuntime.Name(other);
   if (first == second || CannotChange(owner, other, rules) || CannotChange(other, owner, rules)) {
    if (log != null) log.Add(source + ": 특성을 교환할 수 없다");
    return EffectOutcome.Failed;
   }
   // 두 특성을 모두 검사한 뒤 변경하므로 한쪽만 바뀌는 교환은 없다.
   MiscModules.ChangeAbility(owner, second, 0, log, source, false);
   MiscModules.ChangeAbility(other, first, 0, log, source, false);
   Refresh(log);
   return EffectOutcome.Success;
  }
  public static EffectOutcome Copy(Fighter owner, string ability, Rules rules, List<string> log, string source) {
   if (owner == null || owner.HP <= 0 || string.IsNullOrEmpty(ability) || AbilityRuntime.Name(owner) == ability) return EffectOutcome.Failed;
   return MiscModules.ChangeAbility(owner, ability, 0, log, source) ? EffectOutcome.Success : EffectOutcome.Failed;
  }
  static EffectOutcome CopyPredecessor(EffectCall call) {
   if (call.Owner == null || call.Rules == null) return EffectOutcome.Failed;
   int side = Mechanics.PartySide(call.Owner, call.Rules);
   if (side < 0 || side > 1 || call.Rules.LastFaintedAbility == null || call.Rules.LastFaintedMember == null
    || call.Rules.LastFaintedAbility.Length <= side || call.Rules.LastFaintedMember.Length <= side
    || string.IsNullOrEmpty(call.Rules.LastFaintedMember[side])) return EffectOutcome.Failed;
   if (call.Rules.LastFaintedMember[side] == Mechanics.MemberKey(call.Owner.Data)) return EffectOutcome.Failed;
   return Copy(call.Owner, call.Rules.LastFaintedAbility[side], call.Rules, call.Log, call.Source.Label);
  }
  public static void Register() {
   PotentialExecutors.Register("특성 교환", call => Clean(call.Line) == Swap,
    call => Exchange(call.Owner, call.Opponent, call.Rules, call.Log, call.Source.Label));
   PotentialExecutors.Register("특성 복사", call => Clean(call.Line) == CopyOpponent || Clean(call.Line) == CopyFaintedAlly,
    call => Clean(call.Line) == CopyFaintedAlly ? CopyPredecessor(call) : Copy(call.Owner, AbilityRuntime.Name(call.Opponent), call.Rules, call.Log, call.Source.Label));
  }
 }
}
