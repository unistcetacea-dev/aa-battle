# 옛 구조 가정 점검표 (지시서 S, 2026-10-15)

방법: 지시서의 「바뀐 구조」 항목마다 grep·코드 읽기로 옛 가정을 찾았다. 파일:줄은 점검 시점(87af84d 기준)이다.
심각도: 예외 / 잘못된 동작 / 표시만 / 타 작업(진행 중인 브랜치가 다룸) / 문제없음(확인 완료).
상태 열은 이 브랜치에서 고쳤는지(수정) 남겼는지(보류)를 적는다.

## 찾은 것

| # | 위치 | 예전 가정 | 지금 구조 | 증상·재현 | 심각도 | 상태 |
|---|---|---|---|---|---|---|
| 1 | BattleDialogs.cs:21 `DirectiveForm` 생성자(교대 대상 목록), BattleApp.cs:36 `Battle()`, BattleEngine.Turn.cs:99~116 | 대기석 목록은 모두 낼 수 있는 포켓몬 | 쓰러진 포켓몬도 대기석에 남는다(죽어내밀기 때 나간 쓰러진 포켓몬이 `reserves` 뒤에 붙음) | 행동 선택 창에서 빈사 포켓몬을 교대 대상으로 고르면 엔진이 거르지 않고 HP 0인 포켓몬이 장소에 나옴. 재현: 대기석 HP 0 + `BattleCommand.SwitchIn`으로 `Engine.Resolve` | 잘못된 동작 | 수정 |
| 2 | BattleApp.cs:39 `directiveButtons[i].Subtext` | 「대기 N」 = 낼 수 있는 대기 수 | 쓰러진 포켓몬 포함 | 대기 수가 실제 교대 가능 수보다 많이 표시 | 표시만 | 수정 |
| 3 | BattleApp.cs:25 초기화(reset) 버튼 | 트레이너 상태는 `new TrainerBattleState{Trainer,InitialSize}`로 충분 | `LoadTeams`가 `RefreshUnlocks`(트레이너·파티 전원 포텐셜의 해금)를 채우고, 룰에는 봉인·물놀이·진흙놀이·날씨 잠금·마지막 쓰러진 포켓몬 기록이 있다 | 초기화 뒤 대기석 포켓몬 포텐셜이 주던 PT/지령 해금이 사라지고, 봉인 기록·날씨 잠금이 남음. 재현: 해금 포텐셜을 가진 대기석 포켓몬이 있는 팀을 불러와 초기화 | 잘못된 동작 | 수정 |
| 4 | StateEffectsMore.cs:104 상태 「봉인」 | 포켓몬 기술 목록 = `Data.moves` | 배틀 중 기술 목록은 `Fighter.Moves`(4개 초과 가능, 배틀 창 편집이 `Moves`만 바꿈) | 배틀 창에서 기술을 바꾸면 봉인 후보가 옛 기술 목록 | 잘못된 동작 | 수정 |
| 5 | TriggerStructures.cs:96 `RankBand`(트리거 「트레이너 자질」) | 자질 등급 = 글자 비교 | 사용자 결정: 육성 EX = AA (`TrainerAccessRules.Band`) | 「통솔/육성/… AA」 트리거가 EX 트레이너(샘플: 마히토 육성 EX, 유키나리 능력 EX)에서 불발 | 잘못된 동작 | 수정 |
| 6 | RoleModules.cs:93~99 `Rank`(자질 랭크 올림·내림) | EX를 알지 못해 인덱스 −1 → 랭크 그대로 | EX = AA | 「자질을 N랭크 올린다」가 EX 트레이너에서 아무 일도 안 함(성공으로 로그만 남음) | 잘못된 동작 | 수정 |
| 7 | StartUI.cs:83 시작 화면 팀 창 「포켓몬 N체」 | 팀 = 출전 구성 | 팀 = 파티 후보(칼데아 28체) + 출전 선택(H) | 후보 전체 수와 공 아이콘 12개가 나와 출전 수로 오해 | 표시만 | 수정 |
| 8 | BenchEvents.cs:121, SwitchRuntime.cs:67·75 | 로그 문자열(「토큰 + 트리거 충족」)로 교대 판단 | `TriggerFact`·포텐셜 큐 | 다른 포켓몬·지난 T 로그가 섞임(b4048a9에서 일부 수정) | 타 작업 | 보류: `task/fix-entry-switch-onfield`가 같은 파일을 고치는 중 |
| 9 | docs/handoff/build-release.md:72~75 (옛 `version.txt` 업데이트 절) | raw `version.txt` + 저장소 EXE | GitHub Releases zip (같은 파일 87~89줄) | 문서가 서로 모순 | 표시만 | 수정(옛 절에 폐기 표시) |

## 확인했으나 문제없던 것 (옛 가정이 이미 정리됨)

| 항목 | 확인 내용 |
|---|---|
| 기술 4개 고정 | `BattleWidgets.MoveCard`는 쪽 넘김(`page*4`), `BattleApp.RefreshView`·`Battle()`는 `Math.Min(Selected, Length-1)`, 엔진은 `Fighter.Moves`를 쓴다. `BattleDialogs.cs:53` 편집 창은 4칸 뒤 기술을 `Skip(4)`로 보존. `BattleApp.cs:20`의 `Take(4)`는 시작 전 기본 데모 포켓몬 초기화뿐. `Data.moves`는 `StateEffectsMore.cs:104`(항목 4)가 유일 |
| 팀 6체 고정 | `PartyRosterForm`이 후보(칼데아 28)에서 1~6체를 고르고 `LoadTeams`는 선택된 수로 `initialTeamSizes`·`InitialSize`를 채운다. `ProjectStore`의 `Take(6)`은 기본 출전 목록(`defaultMemberIds`)뿐. `TeamRules.Check`는 출전 구성(`members`)만 본다. 인덱스 0~5 하드코딩 없음 |
| 트레이너 쪽 「전용」 슬롯 | 전용은 포켓몬 쪽(`Pokemon.id == "전용"`)으로 판정(`PotentialEvents.cs:76`, `AbaddonTriggers.cs:331`, `PotentialProtection.cs`), `ProjectStore.cs:67`이 트레이너의 옛 전용을 제거 |
| 레벨 상한 | 전투 창·편집기 레벨 입력 1~999, 엔진에 200/255 상한 없음(샘플 최대 279) |
| 업데이트/배포 | `Updater`/`UpdateLogic`은 Releases zip 방식, `version.txt`·`release/windows` EXE 참조 코드 없음(항목 9는 문서만) |
| 성별 「없음」 | `Genders.Normalize`가 빈 값을 「없음」으로 바꾸고 `FailModules`가 같은 함수로 비교. 칼데아 샘플의 「없음」 6체 정상 |
| 팀 편성 규칙 | `TeamRules.Check`는 출전 구성만 검사(후보 전체를 합산하지 않음), 등급 비교는 `TrainerAccessRules.RankAtLeast`(EX 처리 포함) |
| 정규화 전 문장 직접 비교 | 남은 마침표 문장 비교(`ItemRuntime`·`AbilitySentenceTable`)는 `Rewrite`/정규화 입력 쪽 표이고 실행기는 표준 문장만 해석 |

## 사용자 판단 필요

- 초기화 버튼은 배틀 시작 시의 등장 효과(`LoadTeams`의 선발 등장 트리거)를 다시 실행하지 않는다. 지금은 그대로 두었고(수정 3은 상태 찌꺼기만 정리), 초기화가 「처음 상태로 되돌린다」인지 「선발 등장부터 다시」인지 결정이 필요하다.
- 스모크 검사(두 샘플 × 모든 트레이너 × 모든 선봉, 10T 이상)는 사용자 결정으로 이번에 보류했다.
