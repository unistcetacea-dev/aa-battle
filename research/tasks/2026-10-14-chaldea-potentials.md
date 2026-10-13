# 지시서: 피니스 칼데아 포텐셜 — 구현 여부 판단 · 미구현 구현 · 모듈화 준비

작성: Claude · 구현: Claude Sonnet(서브에이전트, 별도 worktree) · 기준: 최신 `origin/main` · 브랜치: `task/chaldea-potentials`
같은 시간에 다른 작업(편집기 UI·성별·전용 포텐셜 `task/potential-editor-window`, 이어서 실프 인헨스먼츠 샘플 파티 `task/abaddon-sample-party`)이 돈다. 새 모듈은 **새 파일**(예: `ChaldeaModules*.cs`, `ModuleCatalog.Chaldea.cs`)에 두고, 공용 파일(`EffectModules*.cs`, `TriggerModules*.cs`, `PotentialEvents*.cs`, `ModuleCatalog*.cs`, `PROJECT_HANDOFF.md`)에는 연결 줄만.

먼저 `AGENTS.md`(「포텐셜」 절 전체: 합치지 않는 차이, 표준 문장, `StateEffects.Table`, 트레이너 포텐셜 규칙), `PROJECT_HANDOFF.md`의 트리거·효과 모듈·ModuleCatalog 절, `research/potentials/TIMING_RULES.md`를 읽는다. 문서 해석이 필요하면 `research/samples/` 아래 캐시나 오레마스 문서를 본다.

## 자료
참치백과 「기업 탐사대/피니스 칼데아」 캐릭터 시트 30개 원문(DokuWiki raw, `<html>…</html>` 안은 아스키아트 — **읽지 말고 걸러낸다**): `research/samples/chaldea/*.txt` (유키나리 … 모리어티 28명 + `칼데아_슈트.txt`·`판매_슈트.txt`). 한 시트에 성장 단계(「초기」 등)가 여러 개 있을 수 있다 — **가장 마지막 단계 기준**으로 판단하되, 앞 단계에만 있는 포텐셜도 목록에 넣는다.

## 사용자 요구
데이터를 옮기는 것보다 **포텐셜 구현 여부 판단 → 안 되는 것 구현 → 모듈화 준비**가 목적이다. 파티 데이터 파일은 만들지 않는다.

## 할 일
1. **추출**: 30개 시트에서 포텐셜(『이름』… 문장, 【고유 포텐셜】·「X 전용 포텐셜」 블록, 슈트 포텐셜)·특성·오리지널 기술 문장을 뽑아 `research/samples/chaldea/POTENTIALS.tsv`(캐릭터·단계·종류(포켓몬/고유/전용/슈트/특성/기술)·이름·원문)로. 추출 스크립트는 scratchpad에 두고 결과만 저장소에.
2. **판정**: 각 문장을 엔진 기준으로 분류한다 — 엔진 코드로 판정(작은 콘솔/테스트 도우미로 `TriggerModules.Normalize/Parse`, `EffectModules.Normalize/Classify`, 실행기 존재 여부, 기존 포텐셜 도감의 같은 이름 항목을 돌려 본다. 자연어 감으로 판정하지 않는다).
   - ✅ 자동: 트리거·효과 모두 표준 문장으로 읽히고 실행기 있음
   - 🟡 정규화만 필요: 기존 모듈 뜻인데 표현만 다름 → `Normalize` 한 줄(+정규화 검사)로 해결
   - 🔴 새 모듈 필요: 기존 모듈로 표현 불가
   - ⚪ 효과 없음: 다인전 전용·싱글에 대상 없음·배틀 밖(`AbilityNoEffectModules` 표준 문장)
   - ❓ 사용자 판단 필요: 뜻이 애매(원문과 함께)
3. **구현**: 🟡 전부, 🔴 중 **여러 캐릭터에 겹치거나 기존 판정 지점에 붙이기 쉬운 것부터** 새 모듈(표준 문장 + 실행기 + Normalize/Classify + `ModuleCatalog` 등록 + 검사)로 구현한다. 이름별 `if` 금지. 판정 지점이 엔진 구조를 크게 바꿔야 하는 것은 구현하지 말고 4로.
4. **모듈화 준비**: 남은 🔴·❓를 `research/samples/chaldea/MODULE_GAPS.md`에 묶음별로: 묶음 이름 · 해당 포텐셜(캐릭터/이름/원문) · 제안 표준 문장(트리거/효과) · 판정 지점 · 필요한 엔진 변경 · 검사 아이디어 · 난이도. 다음 지시서 초안으로 바로 쓸 수 있게.
5. 기존 포텐셜 도감(`research/potentials/potential-catalog.json` 등)에 같은 이름이 있으면 대조해 표에 적는다(수집 JSON 전체를 읽지 말고 이름 검색).

## 원칙 (사용자 결정, 바꾸지 말 것)
- 우선순위: 사용자 결정 > 오레마스 문서 > 기존 시트 > 실기.
- 트레이너 고유 포텐셜의 「아군」 = 장소에 있는 자기 포켓몬. 전용 포텐셜은 포켓몬에 달리는 트레이너 포텐셜.
- 「할 수 있다」는 선택 확인(`OptionalEffectPrompt`). 확률 표기(저확률·중확률·드물게·N할)는 기존 확률 모듈.
- 다인전 전용은 효과 없음.

## 검사
- 새 모듈·정규화마다 `[TestCase]` 묶음(동작 검사, 문장 전체 비교·개수 고정 금지).
- `ModuleCoverageTests` 통과.
- `powershell -NoProfile -ExecutionPolicy Bypass -File ./desktop/verify.ps1 -OutputPath outputs/candidate-chaldea`.

## 작업 방식
구현·검사·문서·병합까지 한 번에: verify 통과 → fetch·최신 `origin/main` 위로 rebase(충돌은 양쪽 살려 해결) → verify 재실행 → `git push origin task/chaldea-potentials:main`(거절되면 다시) + 브랜치 push. `outputs/desktop` 빌드는 하지 않는다. 원래 체크아웃(`C:\Users\115\Downloads\AA_battle`)은 건드리지 않는다. 커밋 끝에 `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. `PROJECT_HANDOFF.md`에는 절 하나. 양이 많으니 묶음마다 커밋하고, 시간이 모자라면 구현을 줄이고 판정표·MODULE_GAPS를 우선 완성한다.

## 보고 (한국어, AGENTS.md 형식)
병합된 main 커밋 · 파일당 한 줄 이유 · verify 통과 줄 · 판정 집계(✅/🟡/🔴/⚪/❓ 개수, 구현 전후) · 이번에 구현한 모듈 표 · 남은 묶음 요약(MODULE_GAPS 상위 10개) · 사용자 판단 필요(원문) · 범위 밖 발견.
