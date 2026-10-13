# 지시서 G: 칼데아 3차 — 남은 묶음

브랜치 `task/G-chaldea-phase3` · 출력 `outputs/candidate-G` · **A~D 병합 뒤, E·F와 동시에** 진행. 먼저 `2026-10-14-parallel-rules.md`, `2026-10-14-user-decisions.md`, `PROJECT_HANDOFF.md`의 칼데아 1·2차 절과 A~D 절, `research/samples/chaldea/MODULE_GAPS.md`, `POTENTIALS.tsv`를 읽는다. 판정은 `ChaldeaJudgeTool`(Explicit 검사)로 한다. 원본 시트 `research/samples/chaldea/*.txt`는 커밋하지 않는다(로컬에 없으면 `https://wiki.tunaground.net/doku.php?id=아바돈_<이름>&do=export_raw`로 받는다).

## 충돌 방지 (중요)
- E(『전투속행』)가 **대미지 적용 지점과 `BattleEngine.Turn.cs`**, F(실프 남은 모듈)가 능력 반전·타입 변경·상태(번개구름·아이스번)를 동시에 한다. G는 이들을 하지 않고, 대미지 적용 지점·`BattleEngine.Turn.cs`는 **고치지 않는다**(연결 한 줄이 꼭 필요하면 보고).
- 대상: MODULE_GAPS에서 **F·E·이미 끝난 것 밖**의 묶음.

## 할 일 (행 수가 많은 순서, 묶음마다 커밋)
1. **사용 조건 수식문** (`이 포텐셜은 …` 꼴, 약 39행): 「이 포텐셜은 자신이 필드에 없어도 발동할 수 있다」「이 포텐셜은 공격 명중시에 사용할 수 있다」「…할 때만 사용할 수 있다」 등. 포텐셜 단위 사용 조건으로 읽는 공용 모듈(문장 → 조건 플래그)을 만들고 큐 판정에 연결.
2. **대미지 문장 변형** (약 36행): 같은 뜻인데 표기만 다른 대미지 문장(「데미지」/「대미지」, 「1/N의 데미지를 준다/입힌다」, 타입 표기 대미지 등)을 기존 대미지 모듈 표준 문장으로 정규화. **타입을 단 대미지는 상성을 받는다**(사용자 결정) — F가 만드는 「「X」 타입 대미지」 표준 문장과 같은 형식을 쓰고, F가 먼저 병합했으면 그 모듈을 재사용, 아니면 같은 표준 문장으로 만들고 rebase 때 하나로 합친다.
3. **선택 효과(「~할 수 있다」) 나머지** (약 24행): 2차의 `OptionalForm`/`FindExecutor`로 아직 안 읽히는 것. D의 턴 조건 앞머리 처리 이후 남은 것 위주.
4. **기능확장·추가행동 변형 나머지** (약 28행). 추가행동·추가공격은 **트리거를 만족한 즉시**(사용자 결정), 추가공격 = 방금 낸 공격을 한 번 더.
5. **포텐셜 부여·대체** (약 42행, 「『X』를 부여한다」「『X』가 『Y』로 바뀐다」 등): 배틀 중 포텐셜을 추가·교체하는 공용 모듈. 역할 관련은 B의 역할 임명 모듈을 재사용. 범위가 크면 이 묶음은 설계안을 MODULE_GAPS에 적고 쉬운 것만 구현.
6. 그 밖의 「기타 개별 문장」(약 169행) 중 기존 모듈 정규화 한 줄로 되는 것.
7. 끝나면 `ChaldeaJudgeTool`로 `POTENTIALS.tsv`·`MODULE_GAPS.md` 갱신, 집계 전후를 보고.

## 하지 않는 것
전투속행(E), 능력 반전·타입 변경 사건·번개구름·아이스번(F), 메가진화·폼 변경(나중), 「상대가 「인간」일 때」류 분류 조건(데이터에 분류 칸 없음 — 보고만).

## 수정 가능 파일
`ChaldeaModules.cs`, `ChaldeaTriggers.cs`, `OptionalForm.cs`, 새 파일(예: `UseConditionModules.cs`, `PotentialGrantModules.cs`), 공용 파일(연결 줄만), 테스트, chaldea 판정 파일, `PROJECT_HANDOFF.md`(절 하나).

## 검사
묶음마다 동작 `[TestCase]`(대표 원문 → 기대 동작). 정규화는 재정규화해도 그대로인지(idempotent) 검사 포함.
