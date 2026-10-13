# 지시서 H: 파티 구조 — 다수 소속 + 출전 선택

브랜치 `task/H-party-roster` · `-Visual` 필요 · 선행: A~G 병합 뒤. 먼저 `2026-10-14-parallel-rules.md`, `2026-10-14-user-decisions.md`(「구조·운영」 절), `PROJECT_HANDOFF.md`의 데이터 모델·팀 보관함·편집기 절, `DataModels.cs`, `ProjectStore.cs`, `TeamRules.cs`, `StartUI.cs`, `DataEditor.cs`를 읽는다.

## 목표 (사용자 결정)
- 파티(팀) 데이터에 **트레이너 여러 명 + 포켓몬 여러 마리**(6마리 제한 없음)를 소속시킨다. 같은 트레이너·포켓몬이 여러 파티에 속할 수 있다(참조 또는 복사 — 기존 저장 형식을 보고 단순한 쪽, 보고에 적기).
- **배틀마다 출전 선택**: 배틀 시작 전에 파티에서 출전 트레이너 1명과 포켓몬(최대 6)을 고르는 화면(픽셀 UI). 편성 검사(`TeamRules`)는 **출전 구성**에 대해 한다.
- 폼 데이터(지시서 I)는 파티 안의 다른 포켓몬 데이터로 둔다 — 이번에는 「출전 후보에서 숨김」 표시(예: `formOf` 필드)만 미리 넣어 둔다.
- 기존 파일(트레이너 1 + 포켓몬 ≤6 구조)은 그대로 열려야 한다(자동 변환: 모두 소속 + 기존 구성 = 기본 출전).
- 샘플: `samples/abaddon-sylph.aabdata.json`을 새 구조로 변환(출전 기본값 = 기존 6체).

## 검사
기존 파일 열기·변환 왕복, 다수 소속 저장·불러오기, 출전 선택 → 배틀 데이터가 고른 구성만, 편성 검사가 출전 구성 기준, 숨김(formOf) 포켓몬은 후보에 없음. UI 캡처(출전 선택 화면, 편집기 파티 화면).

## 범위
`DataModels.cs`, `ProjectStore.cs`, `TeamRules.cs`, 배틀 시작 경로(`StartUI.cs`/`BattleApp.cs` 연결), 새 UI 파일(출전 선택), `DataEditor.cs`(파티 소속 편집), 샘플, 테스트, `PROJECT_HANDOFF.md`.
