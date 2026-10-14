# 지시서 O: 「특성·계속 효과」 분류 해체 · 빈칸 합치기 · 겹치는 모듈 정리

브랜치 `task/O-ability-family-split` · 출력 `outputs/candidate-O` · `-Visual` 필요 · **단독 작업**(L·M·H·N 병합 뒤, 다른 작업 없을 때). 먼저 `2026-10-14-parallel-rules.md`, `2026-10-14-user-decisions.md`, `research/tasks/2026-10-14-trigger-basic-split.md`(같은 방식의 선례), `research/potentials/MODULE_OVERLAPS.md`, `research/potentials/MODULE_CATALOG.tsv`(현재 목록), `ModuleCatalog*.cs`, `ModuleCleanupTests.cs`를 읽는다.

## 문제 (Claude 조사)
효과 분류 「특성·계속 효과」에 `ability.*` 모듈 100개가 **출처(특성 작업) 기준**으로 몰려 있다. 편집기에서 찾기 어렵고, 다른 분류와 뜻이 겹치며, 빈칸이어야 할 값이 모듈 여러 개로 쪼개져 있다.

## 할 일
1. **기능별 분류로 재배치** — 새 분류는 꼭 필요한 것만(예: 「급소」, 「특성 조작」), 나머지는 기존 분류(능력 랭크·기술 변형/기술·소지품·방호·회피·체력·날씨/장소·교대·상태·타입·상성·수치 배율·우선도 등)로:
   - 급소: critical.zero/immune/rate/always
   - 능력 랭크: rank.reverse/extra/dropimmune.*/reflect/follow/set/riseagain, intimidate.immune
   - 기술 변형: move.category/power/type/replace, contact.remove, multihit.max, secondary.double/remove.*, movelock, truant, type.move
   - 필중·관통(방호·회피): surehit*, protect.pierce, status.protectpierce, ability.pierce*, floating.hit
   - 소지품: item.*, berry.*, machine.extra
   - 특성 조작: ability.suppress/copy.*/swap/*immune
   - 체력·버팀: indirect.*, sturdy, survive.chance, recoil.*, damage.defer, damage.return, field.hurtothers, field.healtype, guard.restore
   - 날씨·장소: weather.*, hazard.immune, screens.clear
   - 교대: switch.trap*
   - 상태: status.random/reflect/damageimmune/powerimmune, sleep.half, poison.pierce, drain.reverse
   - 타입·상성: type.received/copy/add, item.type
   - 수치 배율: field.scalar.*, fallen.scalarrate, move.accuracy, stats.copyhighest(종족치)
   - 효과 없음: noeffect.multi/outside (편집기에서는 맨 끝 「효과 없음」 분류)
   실제 문장을 보고 최종 결정, 표로 보고.
2. **빈칸으로 합치기** (표준 문장은 **글자 그대로 유지** — 엔진·저장 데이터 불변. 선례: 트리거 「기본 조건」 해체의 `TX` 선택 빈칸 도우미):
   - `field.scalar.공격/방어/특공/특방/속도/명중/회피` → 「{능력}」 빈칸 하나(조사는 받침에 맞게 — N의 조사 수정과 같은 함수).
   - 주체·조건만 다른 쌍: item.ban.self/opponent, secondary.remove.self/opponent, move.fail.opponent/field, surehit/surehit.type/surehit.field, recoil.immune/recoil.condition, sturdy/survive.chance, switch.trap/trapground/traptype 등 → 선택 빈칸으로. 문장이 같은 틀로 안 묶이면 따로 둔다.
3. **다른 분류와 겹치는 모듈 정리**: `MODULE_OVERLAPS.md` + 아래 후보를 확인해, **같은 표준 문장으로 읽히게** 하나로 합친다(한쪽을 다른 쪽의 정규화로 바꾸고 모듈 하나 제거). 동작이 실제로 다르면 합치지 말고 표에 사유.
   - field.scalar.* ↔ 「수치 배율」 모듈 / surehit ↔ 필중 부여 / protect.pierce ↔ 방호 관통 / priority.condition ↔ 「우선도」 / item.take·item.break ↔ 「기술·소지품」 빼앗기·파괴 / 「아군과 교대할 수 있다」·유턴 부여(MODULE_OVERLAPS 확인 결과 반영).
   - 합친 뒤 기존 데이터·샘플·도감 문장이 **같은 결과**로 실행되는지: K 때처럼 정규화 결과 덤프 전후 비교(차이는 합친 모듈의 표준 문장 변경분만, 목록으로 보고).
4. **검사**: 「특성·계속 효과」 분류가 비어 있음, 합친 모듈의 각 선택지가 원래 문장과 글자 그대로 같고 `TryRead`로 복원(`[TestCase]` 대표), `ModuleCleanupTests`(중복·재정규화) 통과. 편집기 캡처로 분류 목록 확인.
5. 문서: `MODULE_OVERLAPS.md` 갱신, `research/potentials/MODULE_CATALOG.tsv` 다시 뽑기(엔진 DLL의 `ModuleCatalog.All`), `PROJECT_HANDOFF.md`(또는 J 이후 주제 파일) 한 절.

## 수정 가능 파일
`ModuleCatalog*.cs`, `Phrases/*.cs`(합치기용 정규화), 합치는 모듈의 실행기 파일(제거·연결), 테스트·표 파일, `MODULE_OVERLAPS.md`, `MODULE_CATALOG.tsv`, 문서.

## 보고
공통 형식 + 분류 재배치 표(모듈 → 새 분류), 합친 빈칸 모듈 표, 합친 겹침 모듈 표(남긴 쪽/없앤 쪽/정규화 줄), 정규화 덤프 비교 결과, 분류별 모듈 수 전후.
