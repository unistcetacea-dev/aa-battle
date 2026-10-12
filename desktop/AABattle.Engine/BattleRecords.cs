using System;
using System.Linq;
using System.Collections.Generic;

namespace AABattle {
 // 진영별 배틀 기록: 데이터 해석(확정 42·43)과 「여기다！」 성립. TrainerBattleState에 들어 있어 턴·되돌리기와 함께 복사된다
 public static class BattleRecords {
  // 같은 개체는 교대해도 같은 기록(확정 42)
  public static string Key(Fighter f) { return f == null || f.Data == null ? "" : string.IsNullOrEmpty(f.Data.id) ? f.Data.name ?? "" : f.Data.id; }
  public static bool IsAnalyzed(TrainerBattleState state, Fighter target) { return state != null && target != null && state.Analyzed.Contains(Key(target)); }

  // 해석 기록. 처음 해석했을 때만 「상대의 데이터를 해석했을 때」·「자신의 데이터가 해석되었을 때」 사건을 낸다
  public static bool RecordAnalysis(TrainerBattleState state, Fighter analyst, IEnumerable<Fighter> party, Fighter target, Action<string> log) {
   if (state == null || target == null || IsAnalyzed(state, target)) return false;
   state.Analyzed.Add(Key(target));
   if (log == null) return true;
   if (analyst != null) RuntimeTriggers.Analyzed(analyst, party ?? new[] { analyst }, target, log);
   TriggerModules.Raise(new TriggerFact { Kind = "해석당함" }, target, analyst, target.Data.name + "의 데이터가 해석되었다", log);
   return true;
  }
  public static TrainerBattleState StateOf(Fighter f) {
   var scope = EffectScope.Current;
   int side = f == null ? -1 : f.FieldSide;
   return scope == null || scope.States == null || side < 0 || side >= scope.States.Length ? null : scope.States[side];
  }

  // 「여기다！」: 처음 인원 기준 남은 수(6체 이하 3, 4~5체 2, 그 밖 1) 또는 상대가 『에이스』『탐사대장』. 한 번 성립하면 유지
  public static void UpdateHere(TrainerBattleState state, int initialSize, int living, Fighter opponent, int side, Action<string> log) {
   if (state == null || state.Here) return;
   if (state.InitialSize <= 0) state.InitialSize = Math.Max(1, initialSize);
   if (!RuntimeTriggers.HereNow(state.InitialSize, living, opponent)) return;
   state.Here = true;
   if (log != null) log((side + 1) + "팀 — 「여기다！」 성립");
  }
  public static void UpdateHere(Fighter[] fighters, Rules rules, TrainerBattleState[] states, Action<string> log) {
   if (fighters == null || states == null || rules == null) return;
   for (int side = 0; side < 2 && side < states.Length; side++) {
    var party = rules.Parties[side] != null && rules.Parties[side].Length > 0 ? rules.Parties[side] : new[] { fighters[side] };
    UpdateHere(states[side], party.Length, party.Count(x => x != null && x.HP > 0), fighters[1 - side], side, log);
   }
  }
 }
}
