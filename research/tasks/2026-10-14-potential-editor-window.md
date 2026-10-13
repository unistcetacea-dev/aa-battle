# 지시서: 포텐셜 편집 창 분리 · 기술 조건 입력 · 성별 · 전용 포텐셜

작성: Claude · 구현: Claude Sonnet(서브에이전트) · 기준 커밋: `bc7e6ca` · 브랜치: `task/potential-editor-window`

먼저 `AGENTS.md`(UI 규칙: 픽셀 컨트롤만, 도트 폰트 16·24·32·48px, `Theme.Enlarge`, 규칙 판단은 엔진), `PROJECT_HANDOFF.md`의 편집기·모듈 목록 절, `desktop/AABattle.UI/DataEditor.cs`·`RetroEditor.cs`·`ModuleBuilder.cs`·`ModuleSlotEditors.cs`·`TriggerBuilder.cs`·`EffectBuilder.cs`·`CatalogUI.cs`를 읽는다. 지금 화면은 `outputs/candidate/catalog.png.potentials.png`.

## 사용자 요구
1. **포텐셜 이름을 입력할 때마다 다시 로딩된다** → 구조를 바꾼다. 포켓몬(·트레이너) 편집 화면에는 **왼쪽 아래 포텐셜 표만 남기고**, 오른쪽의 트리거·효과 편집 패널은 없앤다. 표에서 행을 고르고 「편집」을 누르면(더블클릭도) **별도 편집 창**이 뜬다. 편집 창에서 이름·슬롯·트리거·효과(빈칸 채우기 `ModuleBuilder`)를 고치고 「확인」을 누를 때만 표에 반영한다(「취소」는 버림). 입력 중에는 표·트리·미리보기를 다시 그리지 않는다.
   - 원인도 찾아 보고한다(예: 이름 셀 `CellValueChanged` → `SaveCurrent`/`LoadCurrent`/트리 갱신 → 표 재생성). 표 셀 직접 입력이 남는다면 그 경로도 입력마다 다시 불러오지 않게 고친다.
2. **기술 조건 빈칸**(`ModuleSlotKind.MoveCondition`, 트리거·효과 공통): 입력 방식을 두 가지로.
   - 분류 선택: 공격 / 변화 / 물리 / 특수 / 접촉 / 타입 등 `MoveCondition`이 이미 읽는 토큰을 콤보로.
   - 자유 입력: 기술 이름 입력 칸 + 기술 도감(기술 창)과 연결된 검색 — 입력하면 기술 데이터에서 후보가 걸러지고 고르면 `「기술:이름」`처럼 `MoveCondition`이 읽는 표준 토큰으로 들어간다. 기술 도감 검색 로직(`CatalogUI`의 `RefreshMoveDex` 류)은 재사용하거나 공용으로 뺀다.
   - 표준 문장 형식은 바꾸지 않는다. 기존 문장을 다시 열면 어느 방식이었는지 `TryRead`로 복원.
3. **성별**: 포켓몬 데이터에 성별(수컷 / 암컷 / 없음, 기본 없음) 칸을 추가하고 편집 화면에 픽셀 선택 칸으로 넣는다. 아인종 체크(`pAin`)는 이미 있으니 성별 옆에 잘 보이게 둔다. 저장·불러오기(`PokemonRecord`, JSON)·배틀 데이터 변환까지 연결한다. 엔진의 성별 판정(`FailModules`의 「상대와 성별이 같으면 실패한다」, 지금은 「성별 데이터가 없어 판정하지 않는다」)을 연결한다: 둘 중 하나라도 「없음」이면 실기처럼 실패, 같으면 실패. 그 밖에 성별을 보는 문장(메로메로 등 「이성」 조건)이 엔진에 있으면 연결하고 목록을 보고.
4. **전용 포텐셜**(사용자 결정): 전용 포텐셜은 **포켓몬 편집 화면에서 그 포켓몬에 단다**. 하지만 **트레이너 포텐셜이다** — 다음을 유지한다.
   - 포켓몬의 포텐셜 목록에 슬롯 「전용」으로 저장하되, 실행 시 출처는 트레이너 포텐셜(트레이너 방호·관통 규칙, 『』 이름, 트레이너 포텐셜 판정 경로)로 다루고 작용 대상은 그 포켓몬.
   - 개수 제한(`TeamRules`의 능력 자질별 `ExclusiveLimit`)은 트레이너의 파티 전체에서 센다.
   - 데이터 해석에서 공개하지 않는 규칙(`AnalysisForm`, 상태 도감 문장) 유지.
   - 기존 데이터의 트레이너 쪽 「전용」 포텐셜은 그대로 불러와 동작해야 한다(삭제·자동 이동 금지). 트레이너 편집 화면의 슬롯 선택지에서 「전용」을 빼고, 남아 있는 것은 「포켓몬 편집으로 옮기세요」 정도로 표시.
   - 현재 엔진이 「전용」을 어떻게 실행하는지 먼저 확인하고 보고(어느 판정이 「트레이너 포텐셜」 여부를 보는지).

## 검사
- 편집 창: 확인 시 반영 / 취소 시 미반영 / 이름 입력 중 표가 다시 만들어지지 않음(행 수·선택 유지) — UI 리플렉션 검사 `[Category("UI")]`.
- 기술 조건: 분류 선택과 기술 이름 검색이 같은 표준 문장을 내고 `TryRead`로 복원.
- 성별: 저장·불러오기 왕복, 「성별이 같으면 실패」 세 갈래(같음·다름·없음).
- 전용: 포켓몬에 단 전용 포텐셜이 트레이너 포텐셜로 판정(트레이너 방호를 포켓몬 관통이 못 뚫는 경로 등 한 가지), 개수 제한이 파티 합산, 해석 비공개.
- `./desktop/verify.ps1 -Visual -OutputPath outputs/candidate`(실행 정책 때문에 `powershell -NoProfile -ExecutionPolicy Bypass -File ...`). 편집 화면·편집 창 캡처를 추가하고 직접 열어 확인(글자 깨짐·잘림·Windows 기본 컨트롤 없음).

## 범위
- 수정: 위 UI 파일들, 새 UI 파일(편집 창), `DataModels.cs`(성별 필드), 배틀 데이터 변환, `FailModules.cs`(성별 판정), `TeamRules.cs`, 전용 포텐셜 실행 연결 지점, 테스트, `PROJECT_HANDOFF.md`. 다른 엔진 판정은 바꾸지 않는다. 40KB 넘는 파일은 나눈다.

## 작업 방식
구현·검사·문서·병합까지 한 번에: verify 통과 → 최신 `origin/main` 위로 rebase → verify 재실행 → `main` fast-forward 병합 → `main`과 브랜치 push → `./desktop/build.ps1 -OutputPath outputs/desktop`(같은 우회 실행, 잠겨 있으면 `outputs/candidate` 경로 보고). 애매하면 멈추지 말고 보고에 남긴다. 커밋 끝에 `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## 보고 (한국어, AGENTS.md 형식)
병합된 main 커밋 · 파일당 한 줄 이유 · verify 통과 줄 · 다시 로딩 원인 · 전용 포텐셜 실행 경로 설명 · 성별을 보는 엔진 문장 목록 · 캡처 확인 결과 · 지시서와 다르게 한 점 · 범위 밖 발견.
