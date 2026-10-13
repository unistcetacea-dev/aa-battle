using System;
using System.Linq;
using System.Collections.Generic;

namespace AABattle {
 // 남은 특성 모듈화 4차의 판정 지점 도우미: 추가효과 예약 공격 · 얼음의벽 · 어둠안 · 능력 하락 무효 사건.
 // 큰 파일(Mechanics.cs, BattleEngine.cs 등)에는 이 클래스를 부르는 한 줄만 둔다.

 // 「자신의 기술에 「4할:자연발화」의 추가효과를 부여한다」: 기술 데이터의 예약 공격(미래예지 태그)을 그대로 건다.
 public static class DelayedSecondary {
  static HashSet<string> names;
  // 미래예지 태그가 붙은 기술 이름(자연발화·미래예지·파멸의소원)
  public static bool IsDelayedMove(string name) {
   if (names == null) names = new HashSet<string>(MoveCatalog.All.Where(x => x.tags != null && x.tags.Contains("미래예지")).Select(x => x.name));
   return names.Contains((name ?? "").Trim());
  }

  // 상대에게 예약 공격을 건다. 이미 예약 공격이 걸려 있으면 겹치지 않는다(미래예지 기술의 실패 규칙과 같다)
  public static bool Schedule(Fighter attacker, Fighter target, string moveName, Rules rules, List<string> log) {
   var scope = EffectScope.Current;
   var db = scope == null ? null : scope.Db;
   var move = db == null || db.moves == null ? null : db.moves.FirstOrDefault(x => x != null && x.name == moveName.Trim());
   if (move == null) { log.Add("추가효과 「" + moveName + "」: 기술 데이터를 찾을 수 없어 거는 데 실패했다."); return false; }
   if (attacker == null || target == null || target.HP <= 0) return false;
   if ((target.Conditions.DelayedAttacks ?? new List<DelayedAttackState>()).Any(x => x != null && x.Move != null)) {
    log.Add(target.Data.name + ": 이미 예약 공격이 걸려 있어 「" + move.name + "」은 겹치지 않는다.");
    return false;
   }
   int side = scope.Fighters == null ? -1 : Array.IndexOf(scope.Fighters, attacker);
   if (side < 0) side = Math.Max(0, Mechanics.PartySide(attacker, rules));
   Mechanics.ScheduleDelayedMove(attacker, target, move, rules, side, log);
   return true;
  }
 }

 // 얼음의벽(오레마스_기술): 상대 기술의 대미지를 반감하는 진영 효과. 상대가 「불꽃」 기술을 내보내면 해제되고,
 // 리플렉터의 유형이라 깨트리기·장 해제·벽 해제로도 해제된다(SideFieldState.HasScreens/ClearScreens).
 // 리플렉터·빛의장막과 겹쳐도 대미지는 한 번만 반감한다(Mechanics.DamageFactor).
 public static class IceWallRules {
  public const int DefaultTurns = 4;   // 기술 「얼음의벽」은 4T
  public static bool Deploy(StateEffectContext c) {
   var side = StateTicks.Side(c.Target, c.Rules);
   if (side == null) return false;
   if (side.IceWall > 0) { c.Say("얼음의벽 실패: 이미 전개되어 있다."); return false; }
   side.IceWall = c.Turns > 0 ? c.Turns : DefaultTurns;
   c.Say((StateTicks.SideIndex(c.Target, c.Rules) + 1) + "팀 진영: 얼음의벽 " + side.IceWall + "턴");
   return true;
  }
  // 상대가 「불꽃」 기술을 내보내면 그 기술의 대미지를 계산하기 전에 해제된다
  public static void BreakByFire(Move move, Rules rules, int wallSide, List<string> log) {
   if (move == null || rules == null || wallSide < 0 || wallSide > 1 || !Mechanics.TargetsOpponent(move)) return;
   if (!(move.types ?? new string[0]).Contains("불꽃") || rules.Sides[wallSide].IceWall <= 0) return;
   rules.Sides[wallSide].IceWall = 0;
   if (log != null) log.Add((wallSide + 1) + "팀 진영: 「불꽃」 기술 " + move.name + "으로 얼음의벽이 해제되었다.");
  }
 }

 // 사건·상태 판정 지점
 public static class AbilityRemaining4Events {
  public const string IntimidateLabel = "〔위협〕";

  // 피격 전 체력이 가득이었는가(멀티스케일처럼 피격 순간의 체력으로 본다)
  public static bool WasFull(Fighter target, Fighter attacker, int bodyDamage) {
   if (target == null || attacker == null || target.Data == null) return false;
   var scope = EffectScope.Current;
   var rules = scope == null || scope.Rules == null ? new Rules() : scope.Rules;
   return target.HP + Math.Max(0, bodyDamage) >= Mechanics.MaxHP(target, attacker, rules);
  }

  // 능력 하락을 무효화했다: 「상대에 의한 능력 하락을 무효화했을 때」 사건
  public static void DropNullified(Fighter target, Fighter source, Action<string> log) {
   if (target == null || target.HP <= 0 || log == null) return;
   TriggerModules.Raise(new TriggerFact { Kind = "하락 무효" }, target, source, target.Data.name + "이 능력 하락을 무효화했다", log);
  }
  public static void DropNullified(Fighter target, Fighter source, List<string> log) { DropNullified(target, source, (Action<string>)log.Add); }

  // 〔위협〕에 의한 능력 하락을 무효화하는 문장이 있으면 하락을 막고 「〔위협〕을 받았을 때」 사건을 낸다
  public static bool IntimidateImmune(Fighter target, Fighter source, Rules rules, string reason, List<string> log) {
   if (reason != IntimidateLabel || target == null || target.HP <= 0) return false;
   if (!PotentialModifiers.Sentences(target, source, rules ?? new Rules(), "위협").Any(AbilityRemaining4Modules.IsIntimidateImmunity)) return false;
   log.Add(target.Data.name + "의 〔" + AbilityRuntime.Name(target) + "〕: 〔위협〕에 의한 능력 하락을 무효화했다");
   TriggerModules.Raise(new TriggerFact { Kind = "위협 받음" }, target, source, target.Data.name + "이 〔위협〕을 받았다", log.Add);
   return true;
  }

  // 어둠안(은밀): 숨는 곳이 땅속과 별개인 「어둠안」. 자신이 기술을 내보내면 풀린다
  public const string Darkness = "어둠안";
  public static bool EnterDarkness(StateEffectContext c) {
   var f = c.Target;
   if (f == null || f.HP <= 0) return false;
   if (!string.IsNullOrEmpty(f.Conditions.Hidden)) { c.Say("어둠안 실패: 이미 「" + f.Conditions.Hidden + "」 상태다."); return false; }
   f.Conditions.Hidden = Darkness;
   c.Say(f.Data.name + ": 「어둠안」으로 모습을 감췄다");
   return true;
  }
  public static void LeaveDarkness(Fighter a, List<string> log) {
   if (a == null || a.Conditions.Hidden != Darkness) return;
   a.Conditions.Hidden = "";
   if (log != null) log.Add(a.Data.name + ": 행동해서 「어둠안」이 풀렸다");
  }
 }
}
