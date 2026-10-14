using System;
using System.Linq;
using System.Windows.Forms;

namespace AABattle {
 partial class MainForm {
  // 초기화 버튼: 포켓몬 상태와 장소·진영 상태를 처음으로 되돌린다.
  // 트레이너 상태는 LoadTeams와 같이 트레이너·파티 전원 포텐셜의 해금을 다시 채운다.
  // (선발 등장 효과는 다시 실행하지 않는다.)
  internal TrainerBattleState[] TrainerStatesForTest { get { return trainerStates; } }

  public void ResetBattle() {
   Save();
   for (int i = 0; i < 2; i++) {
    ResetFighter(Fighters[i], true);
    foreach (var reserve in reserves[i]) ResetFighter(reserve, true);
    trainerStates[i] = new TrainerBattleState { Trainer = trainers[i], InitialSize = initialTeamSizes[i] };
    trainerStates[i].RefreshUnlocks();
    foreach (var member in new[] { Fighters[i] }.Concat(reserves[i])) trainerStates[i].RefreshUnlocks(member.Data.potentials);
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
