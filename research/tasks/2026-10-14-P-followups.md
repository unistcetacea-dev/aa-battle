# 지시서 P: 작은 후속 묶음 (저주 관통 · 우선도 모듈 합치기 · 검사 수 점검)

브랜치 `task/P-followups` · 출력 `outputs/candidate-P` · 먼저 `AGENTS.md`, `PROJECT_HANDOFF.md`(목차) → `docs/handoff/potentials.md`·`conditions.md`·`battle.md`의 「사용자 결정」과 L·O 절을 읽는다.

## 할 일
1. **『아름다운 육식수』 저주 무효화 관통** (사용자 결정: 진행): 「상대의 「저주」대미지가 무효화되지 않는다」 = **자신이 건 저주**의 턴 끝 대미지는 매직가드·「공격 이외 대미지 무시/무효」(초근성 포함)·확률 면역으로 막히지 않는다.
   - 저주에 **건 쪽**을 기록(`ConditionState.Curse`가 bool뿐이면 건 쪽 참조를 추가, 배턴터치 인계 시 유지).
   - 간접 대미지 면역 판정(`Mechanics.Hurt` 등)에 「대미지 원천이 저주이고, 건 쪽이 이 효과를 가짐」이면 면역을 건너뛰는 연결. 표준 문장 + 정규화 + `ModuleCatalog` 등록.
   - 검사: 육식수 보유자가 건 저주 → 매직가드 상대도 대미지, 다른 포켓몬이 건 저주 → 막힘.
2. **우선도 모듈 하나로** (사용자 결정): 기본 `effect.priority.올린다`에 기술 조건 빈칸을 추가하고 `ability.priority.condition`(선택 빈칸 「올린다/−0.5」 포함)과 하나로 합친다. 표준 문장이 둘로 갈리면 정규화로 하나에 모으고, 도감 효과 문장 정규화 덤프 전후 비교(차이는 합친 문장만, 목록 보고).
3. **검사 수 점검**: O에서 `[TestCase]`/`TestCaseSource` 케이스를 늘렸는데도 전체 수가 2743으로 같았다. `dotnet test --list-tests`로 실제 실행 목록을 뽑아 **누락된 검사(같은 이름 중복으로 합쳐짐, TestCaseSource가 비어 있음, Explicit/Category 필터로 빠짐 등)**를 찾고 원인을 보고. 실제로 빠지는 검사가 있으면 고친다(검사 기대값은 바꾸지 않음).

## 검사·병합
`powershell -NoProfile -ExecutionPolicy Bypass -File ./desktop/verify.ps1 -OutputPath outputs/candidate-P` → fetch·rebase → verify → `git push origin task/P-followups:main` + 브랜치. 동작 변경은 해당 `docs/handoff/<주제>.md` 갱신.

## 보고
공통 형식 + 3번 원인과 검사 수 전후.
