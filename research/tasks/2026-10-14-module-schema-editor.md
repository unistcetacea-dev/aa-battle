# 지시서 B: 모듈 입력 통일 (모듈 목록 + 빈칸 채우기 편집기)

작성: Claude · 구현: ChatGPT(Codex) · 선행: 지시서 A(`2026-10-14-engine-file-split.md`) 병합 뒤 · 브랜치: `task/module-schema-editor`

먼저 `AGENTS.md`, `PROJECT_HANDOFF.md`의 트리거 모듈·효과 모듈·소지품·기술·특성 절들, `desktop/AABattle.UI/TriggerBuilder.cs`, `desktop/AABattle.UI/EffectBuilder.cs`를 읽는다.

## 문제
- 엔진에는 트리거·효과 모듈이 많이 늘었다(소지품·기술·특성 작업에서 새로 생긴 것 포함: 무효화 트리거, 급소 무효, 반드시 버팀, 간접 대미지 무효, 추가효과, 드레인 강화, 조이기, 모으기 생략, 인계, 추격, 벽 해제, 소지품 빼앗기 등). 그런데 효과 편집기(`EffectBuilder`)는 종류마다 손으로 짠 `if` 분기라 2차 이후 새 모듈은 입력할 수 없다. 트리거 편집기(`TriggerBuilder`)도 모듈마다 입력 방식이 제각각이다.
- 사용자 요구: 새 모듈을 넣을 때 **「상대의 「X」 기술을 무효화했을 때」처럼 문장 틀에 빈칸을 채우는 방식**으로 모두 입력할 수 있게 전반적으로 통일한다.

## 목표
1. **엔진: 모듈 목록(스키마)** — `desktop/AABattle.Engine`에 새 파일(예: `ModuleCatalog.cs` + 묶음별 등록 파일)을 만든다.
   - 모듈 하나 = 이름 · 종류(트리거 사건 / 트리거 상태 / 효과) · 분류(편집기 묶음) · **문장 틀**(예: `상대의 {기술조건} 기술을 무효화했을 때`) · 빈칸 목록.
   - 빈칸 종류는 공용으로 정의한다: 주체(자신/상대/아군…), 기술 조건(`MoveCondition` 토큰: 타입·분류·태그·「기술:이름」·위력 이하·접촉 등), 능력(단일·복수), 랭크 수, 배율, 분모 N, 상태 이름(`StateEffects.Table`), 날씨·지형·룸, 설치물, 소지품 이름, 특성 이름, 기술 이름, 타입, 기간 T, 확률(N할·드물게 등), 횟수, 체력 비율, 비교(이상/이하/높을 때) 등. 각 빈칸은 선택지 목록(엔진 데이터에서 가져옴)과 기본값을 가진다.
   - 각 모듈은 `Fill(빈칸 값) → 표준 문장`과 `TryRead(표준 문장) → 빈칸 값`을 가진다. 표준 문장은 지금 엔진이 읽는 표준 문장과 **글자 그대로 같아야** 한다(새 문장 형식을 만들지 않는다).
   - 등록 대상: `TriggerModules`가 인식하는 모든 사건·상태 모듈, `EffectModules.Classify`가 돌려주는 모든 효과 종류(`MoveModules`·`MiscModules`·`SecondaryEffects`·`StateCure`·`FunctionExtensions`·`TypeRules`·`AbilityPhrases*`·`Ability*Modules`·`BatonModules`·`PursuitModules`·`FailModules`·`VariablePower` 등 포함). 기술 문장 전용(편집기에서 쓸 일 없는 것)은 등록하지 않아도 되지만 목록을 보고에 적는다.
2. **편집기: 빈칸 채우기 화면** — `TriggerBuilder`와 `EffectBuilder`를 모듈 목록 기반의 같은 화면 구조로 바꾼다.
   - 분류 → 모듈 선택 → 빈칸마다 입력 칸(선택은 `PixelControls`의 픽셀 콤보·숫자 입력, Windows 기본 컨트롤 금지) → 미리보기(표준 문장) → 추가. 기존 문장을 고칠 때는 `TryRead`로 빈칸을 채워 다시 연다.
   - 기존 편집기에서 되던 입력(현재 종류·트리거 전부)은 새 화면에서도 같은 문장이 나와야 한다. 손으로 짠 분기는 모듈 목록으로 옮긴 뒤 지운다.
   - 화면 배치·글꼴 규칙은 `AGENTS.md`(도트 폰트 16·24·32·48px, `Theme.UI`/`Theme.Enlarge`)를 따른다.

## 검사
- 모듈 목록 전체에 대한 `[TestCaseSource]`: 모든 등록 모듈에 대해 기본값으로 `Fill` → (효과는) `EffectModules.Classify`가 그 종류를 돌려줌 / (트리거는) `TriggerModules.IsModule`·`Parse`가 그 모듈 → `TryRead(Fill(값)) == 값`(왕복).
- **누락 검사**: `EffectModules.Classify`가 낼 수 있는 종류 이름과 트리거 모듈 이름 목록이 모듈 목록에 모두 있는지(편집기 전용 제외 목록은 코드에 이유와 함께 명시).
- 기존 UI 검사(`TriggerBuilderUiTests` 등)는 새 화면에 맞게 고치되, 같은 입력이 같은 문장을 낸다는 의도는 유지. UI 리플렉션 검사는 `[Category("UI")]`.
- `./desktop/verify.ps1 -Visual -OutputPath outputs/candidate`, 트리거·효과 편집기 캡처를 직접 열어 확인한다.

## 범위
- 수정해도 되는 파일: 새 엔진 파일(모듈 목록), 표준 문장 상수를 공유하려고 손대는 모듈 파일(문장 형식 변경 금지), `TriggerBuilder.cs`, `EffectBuilder.cs`, 편집기 관련 UI 파일, 테스트, `PROJECT_HANDOFF.md`.
- 엔진 동작(판정·실행)은 바꾸지 않는다. 새 효과·트리거를 만들지 않는다.

## 보고 (AGENTS.md 형식)
브랜치/커밋 · 파일당 한 줄 이유 · verify PASS 줄 · 등록 모듈 수(트리거 사건/상태/효과) · 등록하지 않은 것과 이유 · 빈칸 종류 표 · 편집기 캡처 확인 결과 · 지시서와 다르게 한 점 · 범위 밖 발견.
