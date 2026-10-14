# AA BATTLE 인수인계 (목차)

가상의 창작 포켓몬이 포텐셜을 사용해 싸우는 Windows C# WinForms 배틀 시뮬레이터. 이 파일은 목차와 핵심 규칙 요약만 둔다. 상세 내용과 날짜별 작업 기록은 `docs/handoff/<주제>.md`에 있다(원문은 삭제하지 않고 옮겼다). 동작을 바꾸면 해당 주제 파일을 갱신한다.

## 목차

| 파일 | 내용 |
| --- | --- |
| `docs/handoff/overview.md` | 목표, 현재 상태, 다음 구현 순서 |
| `docs/handoff/battle.md` | 배틀 진행: 계산 규칙, 턴·교대·추격·대미지 지연, 타이밍·행동 순서, 방호·회피, 필드효과 |
| `docs/handoff/potentials.md` | 포텐셜 연구, 트리거·효과 모듈화, 정규화, `ModuleCatalog`, 칼데아·전용 유형 |
| `docs/handoff/moves.md` | 기술 효과 모듈(`MoveModules`/`MoveRuntime`), 연속 타격, 기능확장 |
| `docs/handoff/abilities.md` | 특성 모듈화(`AbilityModules`), 전용 코드 특성 |
| `docs/handoff/items.md` | 소지품 성능과 모듈화 |
| `docs/handoff/conditions.md` | 상태이상·상태변화, 잠듦·앙코르 등 개별 상태 |
| `docs/handoff/trainer.md` | 트레이너: 지시·지령·4대자질·유대·역할·추적/조사대원 |
| `docs/handoff/ui.md` | 팀 편집기·배틀 화면·픽셀 컨트롤·데이터 ID·파티 후보 |
| `docs/handoff/build-release.md` | 빌드·검증·프로젝트 구조·배포·업데이트 |
| `docs/handoff/samples.md` | 샘플 파티(실프 인헨스먼츠)·조사 자료 |

각 주제 파일은 앞에 현재 규칙, 끝의 「변경 기록」에 날짜별 기록이 있고, 사용자 결정은 「사용자 결정」 항목에 있다. 날짜별 지시서·결정 원본은 `research/tasks/`에 있다.

## 핵심 규칙 요약

- 빌드: `desktop/AABattle.sln`, .NET SDK 8 이상, 대상 `net48`. 검증은 `desktop/verify.ps1 -OutputPath outputs/candidate`(UI 변경은 `-Visual`). 배포는 `desktop/publish.ps1`(GitHub Releases zip).
- 구조: `AABattle.Engine`(계산·규칙, UI 참조 금지), `AABattle.UI`(화면), `AABattle.Tests`(NUnit). 규칙 판단은 엔진에 둔다.
- 규칙 기준 우선순위: 사용자 결정 > 참치백과 오레마스 문서 > 기존 시트 > 실기(Showdown).
- 상태이상(`Fighter.Status` 한 칸)과 상태변화(`ConditionState`, 겹침 가능)를 구분한다. 이미 있는 상태변화·상태이상을 다시 걸면 실패하고 남은 턴은 유지한다.
- 포텐셜·특성·소지품·기술 효과는 표준 문장(트리거 모듈 + 효과 모듈)으로 표현해 같은 실행기로 돌린다. 새 표현은 정규화 규칙 + 테스트, 새 기능은 실행기 + `ModuleCatalog` 등록(누락은 `ModuleCoverageTests`가 잡는다).
- 효과 발동 순서: 가장 먼저 > 상대보다 먼저 > 우선도 > 속도. 턴 종료는 대미지 → 드레인 → 회복 → 상태 해제 → 교대.
- 포켓몬 입력 스탯은 종족값(실능력치와 구분). 기능확장은 기술 목록 밖 기술을 내보내는 것.
- UI: 흑백 도트, 시스템·도감은 Pokemon BW 폰트, AA는 HeadKasen, 입력은 `PixelControls.cs` 픽셀 컨트롤만(Windows 기본 컨트롤·`MessageBox` 금지).
- 해석이 불확실한 항목은 추측하지 않고 「사용자 판단 필요」로 보고한다.
