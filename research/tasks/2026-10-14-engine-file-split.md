# 지시서 A: 큰 엔진 파일 나누기 (+ 강제교대 행동 칸 버그 1건)

작성: Claude · 구현: ChatGPT(Codex) · 기준 커밋: `5a23e11` · 브랜치: `task/engine-file-split`
순서: 이 지시서 → 지시서 B(`2026-10-14-module-schema-editor.md`).

먼저 `AGENTS.md`(「한 파일이 커지면(약 40KB 이상) 주제별 파일이나 `partial` 클래스로 나눈다」, 「새 코드는 한 줄 압축을 피하고 읽을 수 있게」)와 `PROJECT_HANDOFF.md` 최근 절을 읽는다.

## 목표
동작을 바꾸지 않는 순수 구조 정리다. 40KB를 넘는 엔진 파일을 주제별 `partial` 파일로 나눈다.
- 대상(현재 크기): `BattleEngine.cs` 약 58KB, `EffectModules.cs` 약 55KB, `PotentialEvents.cs` 약 52KB. 작업 시점에 `desktop/AABattle.Engine`·`desktop/AABattle.UI`에서 40KB를 넘는 파일을 다시 확인해 모두 포함한다(`Mechanics.cs`는 약 39KB — 넘었으면 포함).
- 나누는 기준(예시, 실제 내용 보고 정한다):
  - `BattleEngine` → 규칙/데이터 클래스(`Rules`·`SideFieldState`·`Damage`·`TrainerBattleState` 등), 대미지 계산(`Calculate`·`CalculateDelayed`·능력치), 턴 진행(`ResolveTurn`), 교대 처리(`RequestSwitch`·`SwitchNow`·`ResolveTriggeredSwitches`·강제교대), 지령, 엔진 자체 검사(`Test`).
  - `EffectModules` → 기술 조건(`MoveCondition`), 수치 배율, 랭크·1/N·종족치, 장소·설치물·교대, 분류(`Classify`)·정규화(`Normalize`).
  - `PotentialEvents` → 사건·인스턴스 모델, 큐(`PotentialQueue`), 기본 실행기 등록(`BuiltIn.Register`)을 묶음별로.
- 한 줄로 압축된 긴 메서드(특히 `ResolveTurn`의 행동 루프)는 옮기는 김에 줄을 나눠 읽을 수 있게 다시 쓴다. 논리는 바꾸지 않는다(문 단위로 그대로 펼치기만). 펼칠 때 실수가 없도록 한 덩어리씩 옮기고 그때마다 검사를 돌린다.
- 파일 이름은 `BattleEngine.Turn.cs`처럼 주제가 보이게. 새 파일 맨 위에 그 파일이 맡는 것을 한국어 주석 한두 줄로.

## 버그 수정 1건 (Codex가 배턴·추격 작업에서 범위 밖으로 보고)
- 증상: 강제교대로 들어온 포켓몬이 나간 포켓몬이 고른 기술을 그 T에 사용한다(행동 칸이 이어짐).
- 기대 동작: 강제교대로 들어온 포켓몬은 그 T에 행동하지 않는다(실기 기준, 문서에 다른 규칙이 없으면). 문서(`오레마스_시스템`)에서 강제교대 후 행동 규칙을 확인하고, 다르면 문서를 따른다.
- 검사 1개 추가(강제교대 뒤 들어온 포켓몬의 행동 없음).

## 검사·보고
- 버그 수정 외에는 모든 기존 검사가 그대로 통과해야 한다(검사 수정 금지). `./desktop/verify.ps1 -OutputPath outputs/candidate`. UI 파일을 나눴으면 `-Visual`.
- AGENTS.md 절차대로 rebase·병합·push. 보고: 브랜치/커밋 · 나눈 파일 표(원래 파일 → 새 파일들, 각 크기) · verify PASS 줄 · 버그 수정 내용 · 범위 밖 발견.
