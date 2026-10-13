# 지시서 K: 모듈 정리 — 정규화 규칙 재배치 · 검사 표 파일화 · 중복/재정규화 검사

브랜치 `task/K-module-cleanup` · **단독 작업**(진행 중인 다른 작업이 없을 때, E·F·G 뒤 / H 전) · 먼저 `2026-10-14-parallel-rules.md`, `AGENTS.md`의 「포텐셜」 절을 읽는다.

## 원칙
**동작을 바꾸지 않는 구조 정리다.** 기존 검사는 모두 그대로 통과해야 한다(검사 파일을 표로 옮기는 경우만 예외, 같은 입력·기대값 유지). 표준 문장·실행기·판정은 바꾸지 않는다.

## 1. 정규화 규칙을 기능별로 재배치
- 작업 차수·출처 이름의 파일을 **기능별 파일**로 다시 묶는다: `AbilityPhrases.cs`, `AbilityPhrasesRemaining.cs`, `AbilityRewrites3.cs`, `AbilityRewrites4.cs`, `AbilityRemaining4Modules.cs`, `AbilityRemaining4Events.cs`, `TriggerRemaining4.cs`, `ChaldeaModules.cs`, `ChaldeaTriggers.cs`(그 밖에 이름에 차수·출처가 들어간 파일이 있으면 포함, 목록 보고).
- 새 묶음 예: `Phrases/Rank.cs`(능력·랭크), `Phrases/Switch.cs`(교대·인계), `Phrases/Items.cs`, `Phrases/Chance.cs`(확률·HP 비례), `Phrases/Timing.cs`(턴 조건·시점), `Phrases/Damage.cs`, `Phrases/Potentials.cs`(무효화·부여·선택형), 트리거는 `TriggerPhrases/*.cs`. 실제 내용을 보고 정한다.
- 규칙 하나마다 **출처 꼬리표**(주석 한 줄: `// 출처: 특성 4차 / 칼데아 / 소지품 …`)를 남긴다.
- **적용 순서를 보존**한다: 지금 정규화 체인(특성 → 소지품 → 칼데아 → 일반 등)의 순서가 결과에 영향을 주는 규칙은 재배치 후에도 같은 순서로 실행되게 한다. 순서 의존 규칙에는 주석으로 이유를 적는다. 순서를 한 곳(예: `PhrasePipeline.cs`)에 목록으로 둔다.
- 클래스·메서드 이름이 바뀌어 다른 코드가 참조하면 함께 고친다(동작 동일).

## 2. 정규화 검사 표 파일화
- 「입력 문장 → 기대 표준 문장」 꼴의 정규화 검사(`EffectModuleTests`, `TriggerModuleTests`, `ChaldeaPhraseTests`, 특성 정규화 검사 등의 `[TestCase(입력, 기대)]`)를 **표 파일**(`desktop/AABattle.Tests/Data/phrases-effect.tsv`, `phrases-trigger.tsv`, 열: 입력 · 기대 · 출처)로 옮기고, `[TestCaseSource]` 검사 하나가 표를 읽게 한다.
- 동작 검사(배틀 진행·실행기 결과)는 옮기지 않는다. 정규화 왕복만.
- 옮긴 건수와 줄어든 검사 코드 줄 수를 보고.

## 3. 새 검사 2개
- **재정규화 불변**: `ModuleCatalog`의 모든 효과·트리거 모듈 기본값 문장과 표 파일의 모든 기대 문장이 **한 번 더 정규화해도 그대로**인지(`Normalize(x) == x`). 실패하면 그 규칙을 고친다(이건 버그 수정으로 허용 — 보고에 목록).
- **중복 모듈 없음**: `ModuleCatalog`에서 서로 다른 모듈이 **같은 표준 문장**(기본값 `Fill` 결과)이나 같은 `Classify` 종류를 이유 없이 공유하면 실패. 의도된 공유는 코드에 사유와 함께 예외 목록.

## 4. 문서
`AGENTS.md`의 「포텐셜」 절에 한 줄: 「정규화 규칙은 기능별 `Phrases/` 파일에 두고 출처를 주석으로 남긴다. 새 정규화 검사는 표 파일에 한 줄 추가한다.」 `PROJECT_HANDOFF.md`(또는 J 이후면 해당 주제 파일)에 새 파일 구조 표.

## 검사·병합
`powershell -NoProfile -ExecutionPolicy Bypass -File ./desktop/verify.ps1 -OutputPath outputs/candidate-K` — 검사 수는 줄어도 되지만 **실패 0**, 옮기기 전후로 같은 입력이 같은 기대값을 내는지 확인. 단계마다 커밋(1 → 2 → 3). fetch·rebase → verify 재실행 → `git push origin task/K-module-cleanup:main`.

## 보고
공통 형식 + 파일 이동 표(원래 파일 → 새 파일), 순서 의존 규칙 목록, 재정규화 검사로 고친 규칙, 중복 예외 목록, 검사 수 전후.
