# 지시서 N: 작은 수정 묶음 (마침표·조사·유턴 구분·상태변화 해제 범위)

브랜치 `task/N-small-fixes` · 출력 `outputs/candidate-N` · `-Visual` 필요 · **병렬 작업 중 가장 나중에 병합한다**(마침표 통일이 다른 작업의 기대 문장과 겹친다 — rebase 뒤 전체 verify로 확인). 먼저 `2026-10-14-parallel-rules.md`, `2026-10-14-user-decisions.md`(「K 후속」 절), `research/potentials/MODULE_OVERLAPS.md`, `desktop/AABattle.Engine/Phrases/PhrasePipeline.cs`, `desktop/AABattle.Tests/ModuleCleanupTests.cs`를 읽는다.

## 할 일 (사용자 결정)
1. **효과 표준 문장 끝 마침표를 항상 붙인다**: 정규화·`ModuleCatalog` 기본값·`Fill` 결과가 경로에 따라 마침표가 붙거나 빠지는 불일치를 통일(항상 「.」). 소비자(실행기·분류기)는 이미 마침표를 떼고 읽으므로 동작은 같아야 한다.
   - `ModuleCleanupTests`의 「끝 마침표 무시」 비교를 **엄격 비교**로 바꾸고 통과시킨다.
   - 마침표 없는 기대값을 쓰는 기존 검사·표 파일은 기대값만 갱신(입력·의미 유지, 보고에 건수).
   - 트리거 문장은 대상 아님(효과만).
2. **조사 오류**: `ability.field.scalar.*`(「「방어」을」 등)와 `effect.potential.grant`의 기본 문장 조사를 받침에 맞게 고친다(정규화가 고쳐 쓰던 예외를 `ModuleCleanupTests` 예외 목록에서 제거). 받침 판별 공용 함수가 있으면 재사용.
3. **유턴 부여 구분**: `IsPivotGrant` 등 유턴 부여 판정이 「자신의 공격기술에 「유턴」 효과를 부여」(공격기술만)와 「자신의 기술에 …」(변화기술 포함)를 구분하게 한다. 변화기술 사용 시 공격기술 한정 부여는 교대하지 않음 검사.
4. **「모든 상태변화를 해제한다」는 상태이상을 포함하지 않는다**: `ExclusiveModules`의 상태변화 전체 해제 실행기가 상태이상(`Fighter.Status`)을 건드리지 않는지 확인·수정 + 검사.
5. **회귀 검사 1개(사용자 확인 사항)**: 「「X」 타입 대미지」는 타입 상성표만 적용하고, 타입 기술 무효화 특성(타오르는불꽃·저수·축전 등 「「X」 기술을 무효화한다」)에는 **무효화되지 않는다**(무효화는 기술에만). 예: 상대 특성 타오르는불꽃 + 「불꽃」 타입 대미지 → 대미지 받음. 지금 코드는 이미 그렇다 — 검사만 추가.
6. `MODULE_OVERLAPS.md`에 3·4 처리 결과 표시.
7. **특성 검색 입력 (UI, `-Visual`)**: 편집기에서 특성을 지정하는 곳을 기술 조건 입력(`MoveSearch.cs`의 `MoveDexSearch`·`MoveSearchForm`·`MoveConditionSlotEditor`)과 같은 방식으로 바꾼다.
   - 모듈 빈칸 `ModuleSlotKind.Ability`(지금은 특성 이름 전체가 든 긴 콤보): 입력 칸 + 「특성 찾기」 단추 → 특성 도감과 같은 검색(이름·효과 문장으로 거름, 효과 문장 미리보기)으로 고르는 `AbilitySlotEditor`. 여러 개(「/」) 입력이 되는 칸은 여러 개 선택.
   - 포켓몬 편집의 「특성」 칸(`DataEditor.PickAbility`, 지금 `SearchToggleForm`)도 같은 특성 찾기 창을 쓰게 통일.
   - 검색 로직은 특성 도감(`CatalogUI`의 `RefreshAbilityDex` 류)과 공용으로(`AbilityDexSearch`, 새 파일 `AbilitySearch.cs`). 기술 검색과 공통 부분이 있으면 묶어도 된다.
   - 같은 방식이 자연스러우면 `ModuleSlotKind.Item`(소지품)도 같은 틀로(선택 — 했으면 보고).
   - 검사 `[Category("UI")]`: 특성 찾기로 고른 값이 빈칸·표준 문장에 들어가고 `TryRead`로 복원. 캡처 추가·확인.

## 수정 가능 파일
`Phrases/*.cs`, `ModuleCatalog*.cs`(기본값 문장), 유턴 부여 판정 파일, `ExclusiveModules*.cs`, 테스트·표 파일(기대값 갱신), `MODULE_OVERLAPS.md`, `PROJECT_HANDOFF.md`(한두 줄), UI: 새 `AbilitySearch.cs`, `ModuleSlotEditors.cs`·`ModuleBuilder.cs`(특성 빈칸 연결), `DataEditor.cs`의 `PickAbility`(한두 줄), `CatalogUI.cs`(검색 공용화).

## 검사
엄격 재정규화 검사 통과, 조사 예외 제거 후 통과, 유턴 구분, 상태이상 유지.
