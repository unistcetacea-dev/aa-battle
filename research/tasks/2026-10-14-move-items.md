# 지시서: 기술 — 소지품 기술 묶음

작성: Claude · 구현: Claude Sonnet(서브에이전트, 별도 worktree) · 기준 커밋: `bc7e6ca` · 브랜치: `task/move-items`
같은 시간에 다른 두 작업(`task/potential-editor-window` — 편집기 UI·성별·전용 포텐셜, `task/move-misc` — 아픔나누기·소란·참기·날씨 회복)이 병렬로 돈다. 충돌을 줄이려고 새 코드는 **새 파일**에 두고, 공용 파일(`MoveRuntime.cs`, `MoveModules.cs`, `EffectModules*.cs`, `PotentialEvents*.cs`, `ModuleCatalog*.cs`, `PROJECT_HANDOFF.md`)에는 등록·연결 줄만 넣는다.

먼저 `AGENTS.md`(특히 「규칙의 기준」의 기술·소지품 문장), `PROJECT_HANDOFF.md`의 기술 효과(`MoveRuntime`)·소지품(`ItemRuntime`)·모듈 목록 절, `research/items/abaddon-items.txt`의 소지품 규칙, `MoveRuntime.cs`의 `Dedicated` 목록을 읽는다. 오레마스 문서 `오레마스_기술`·`오레마스_시스템`(소지품 절)은 `https://wiki.tunaground.net/doku.php?id=<문서명>&do=export_raw`로 직접 읽는다.

## 원칙 (사용자 결정, 바꾸지 말 것)
- 기술 효과의 기준은 문서의 기술 설명 문장 → 오레마스 문서 → 실기(Showdown 5세대 우선). `MoveRuntime`이 표준 문장으로 바꿔 실행하고, 기존 모듈로 안 되면 **새 모듈**(표준 문장 + 실행기 + `EffectModules` Normalize/Classify + `ModuleCatalog` 등록)을 만든다. 기술 이름별 `if`는 문장으로 표현할 수 없을 때만(`MoveRuntime.Dedicated`).
- 변화기술 효과 = 성공했을 때, 공격기술 효과 = 명중 후.
- 빼앗기 규칙(이미 결정): 자신이 빈손이면 빼앗고, 소지품이 있으면 상대 것을 파괴(`상대의 소지품을 빼앗는다.` 모듈 재사용 여부를 기술 문장과 비교해 결정 — 기술 문장이 다르게 말하면 문장대로).
- 상대가 「끈적끈적」류 특성·소지품 보호 효과를 가지면 기존 보호 모듈을 따른다.

## 대상 (기술 데이터에서 문장 확인)
탁쳐서떨구기, 도둑질, 트릭, 바꿔치기, 내던지기, 자연의은혜, 배의뭉치(데이터 이름 확인) + 같은 문장 구조의 소지품 관련 기술(예: 소지품 유무로 위력이 변하는 것, 소지품을 소모·사용하는 것)을 기술 데이터 전체에서 찾아 함께 처리.
- 내던지기·자연의은혜처럼 소지품 종류별 위력·타입·효과가 필요한 것은 소지품 데이터(`abaddon-items.txt`)에 그 값이 있는지 확인하고, 없으면 실기 표를 엔진 데이터 표 한 곳(새 파일)에 둔다.
- 결정할 수 없는 것은 「사용자 판단 필요」에 원문과 함께 남기고 넘어간다.

## 검사
- 기술마다 동작 `[TestCase]` 묶음: 빼앗기 두 갈래, 교환(양쪽 소지품 바뀜·한쪽 빈손), 떨구기 위력 보정과 소지품 제거, 소지품 없음·보호 효과일 때 실패/무효.
- 기존 `ModuleCoverageTests` 통과(새 모듈 등록).
- `powershell -NoProfile -ExecutionPolicy Bypass -File ./desktop/verify.ps1 -OutputPath outputs/candidate-items`(병렬 작업과 출력 폴더를 나눈다).

## 작업 방식
구현·검사·문서·병합까지 한 번에: verify 통과 → 최신 `origin/main` 위로 rebase(다른 병렬 작업이 먼저 병합됐을 수 있다. 충돌은 양쪽 내용을 모두 살려 해결) → verify 재실행 → `main` fast-forward 병합 → `main`과 브랜치 push. `outputs/desktop` 빌드는 하지 않는다(마지막 작업 뒤 Claude가 한다). 커밋 끝에 `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. `PROJECT_HANDOFF.md`에는 절 하나만 추가.

## 보고 (한국어, AGENTS.md 형식)
병합된 main 커밋 · 파일당 한 줄 이유 · verify 통과 줄 · 기술별 처리 표(문장 → 모듈/전용 코드) · 새 모듈 표 · 사용자 판단 필요(원문) · 범위 밖 발견.
