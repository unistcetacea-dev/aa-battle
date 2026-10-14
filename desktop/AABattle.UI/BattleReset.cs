using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;

namespace AABattle {
 partial class MainForm {
  // 초기화 버튼: 포켓몬 상태와 장소·진영 상태를 처음으로 되돌린다.
  // 트레이너 상태는 LoadTeams와 같이 트레이너·파티 전원 포텐셜의 해금을 다시 채운다.
  // (선발 등장 효과는 다시 실행하지 않는다.)
  internal TrainerBattleState[] TrainerStatesForTest { get { return trainerStates; } }

  internal List<Fighter>[] ReservesForTest { get { return reserves; } }

  // 트레이너 사용 이력을 비운 새 상태. LoadTeams와 같이 트레이너·파티 전원 포텐셜의 해금을 다시 채운다
  void NewTrainerState(int side) {
   var state = new TrainerBattleState { Trainer = trainers[side], InitialSize = initialTeamSizes[side] };
   state.RefreshUnlocks();
   foreach (var member in new[] { Fighters[side] }.Concat(reserves[side])) state.RefreshUnlocks(member.Data.potentials);
   trainerStates[side] = state;
   // 지시 C·통솔 명중 보정은 포켓몬에 쌓여 있으므로(랭크 초기화로 지워졌거나 옛 트레이너 값) 새 트레이너 기준으로 다시 맞춘다
   foreach (var member in new[] { Fighters[side] }.Concat(reserves[side])) TrainerAccessRules.ApplyTrainerPassives(member, trainers[side], state);
  }

  public void ResetBattle() {
   Save();
   for (int i = 0; i < 2; i++) {
    ResetFighter(Fighters[i], true);
    foreach (var reserve in reserves[i]) ResetFighter(reserve, true);
    NewTrainerState(i);
    selections[i] = new TurnSelection();
   }
   Rules.Weather = "없음";
   Rules.Terrain = "없음";
   Rules.TrickRoom = Rules.Gravity = 0;
   Rules.WaterSport = Rules.MudSport = 0;
   Rules.WeatherLockOwner = "";
   Rules.WeatherLockMembers = new string[0];
   Rules.SealedMoves.Clear();
   Rules.LastFaintedAbility = new string[2];
   Rules.LastFaintedMember = new string[2];
   Rules.Turn = 1;
   foreach (var side in Rules.Sides) side.Clear();
   turn = 1;
   log = "배틀을 초기화했습니다.";
   RefreshView();
  }
 }
}
