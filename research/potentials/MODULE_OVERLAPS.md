# 뜻이 겹치는 모듈 목록 (조사만, 코드 변경 없음)

작성: 지시서 K 3단계. 정확히 같은 표준 문장을 가진 모듈은 없다(`ModuleCleanupTests.NoTwoModulesShareTheSameDefaultSentence`). 여기는 **문장은 다르지만 뜻·실행 경로가 겹칠 수 있는 쌍**이다. 합치면 동작이 바뀔 수 있어 제안만 적는다. 「확인」은 코드를 직접 읽어 확인한 것, 「추정」은 이름·문장만 보고 판단한 것이다.

## 지시서 O 결과 (합친 쌍과 남긴 쌍)

합친 방식은 선택 빈칸이다(`ModuleCatalog.Abilities.Groups.cs`의 `AbilityChoice`). 선택값이 원래 표준 문장의 글자를 그대로 만들어 엔진·저장 데이터는 바뀌지 않는다.

| 쌍 | 처리 | 남은 ID / 없앤 ID | 선택 빈칸 |
|---|---|---|---|
| 임의교대 한다 ↔ 할 수 있다 | 합침 | `effect.switch.voluntary` / `abaddon.switch.optional` | 교대방식: 한다 · 할 수 있다 |
| 유턴 부여 기술 ↔ 공격기술 | 합침 | `effect.switch.pivot` / `abaddon.pivot.attack` | 유턴대상: 기술 · 공격기술 (「공격기술에」와 「기술에」 구분은 문장 글자로 보존) |
| 조건 우선도 +N ↔ -0.5 | 합침 | `ability.priority.condition` / `ability.priority.lag` | 우선도방식: 올린다 · -0.5 |
| `ability.field.scalar.*` ↔ 「수치 배율」 | 합치지 않음 | 능력 7개만 빈칸 하나로(`ability.field.scalar.stats`) | 「수치 배율」은 기간 있는 일회 효과 실행기, 필드 전원 배율은 특성의 계속 효과 실행기(`Classify` 종류가 다르다). 하나로 읽으면 실행 경로가 바뀐다 |
| `ability.surehit*` ↔ 필중 부여 | 이미 한 곳 | 필중 부여 모듈이 따로 없다(사건 부여는 같은 표준 문장 공유) | 해당 없음 |
| `ability.protect.pierce` ↔ 방호 관통 | 합치지 않음 | 「방어」 기술 관통(상대 기술 판정)과 상대 방호 포텐셜 무시(포텐셜 판정)는 대상이 다르다 | 해당 없음 |
| `ability.priority.condition` ↔ `effect.priority.올린다` | 합치지 않음 | 후자는 「기술 조건」 없이 주체의 우선도 전체를 바꾸는 효과 실행기. 기술 조건을 받는 실행기는 `AbilityMoveModules`뿐이다 | 해당 없음 |
| `ability.item.take/break` ↔ 「기술·소지품」 | 합치지 않음 | 대응하는 빼앗기·파괴 효과 모듈이 없다(`effect.item.break`는 「자신의 소지품이 부서진다」, `.food`는 나무열매·주얼 한정) | 해당 없음 |

정규화 결과 덤프 비교(도감·특성·소지품 효과 문장 3888줄, 정규화 문장·`Classify` 종류·읽을 수 있는 모듈 유무)는 합치기 전후 차이 0줄이다.

## 병렬 작업에서 생긴 의심 후보

| # | 쌍 | 같은 뜻인가 | 실행 경로 | 합칠 때 제안 |
|---|---|---|---|---|
| 1 (O에서 합침) | `abaddon.switch.optional` 「아군과 임의교대할 수 있다.」 ↔ `effect.switch.voluntary` 「아군과 임의교대한다.」 | 「할 수 있다」(선택)와 「한다」(강제)는 AGENTS의 「합치지 않는 차이」다. 선택 여부만 다르고 교대 자체는 같다. 확인: 두 모듈 모두 `Family = 교대 효과`. | 확인: `SwitchRuntime.IsSwitchEffect`가 문장에 「교대」와 「아군/임의교대」가 있고 「교대할수없다」가 없으면 둘 다 교대 효과로 읽는다. 선택형을 묻는 쪽(`OptionalEffectPrompt`)은 `할 수 있다` 판별이 따로 있는지 추정(미확인). | 표준 문장은 기존 `아군과 임의교대한다.`를 그대로 두고, 선택형은 「할 수 있다」 변형으로 같은 모듈의 선택 슬롯(한다/할 수 있다)으로 흡수. 실프 쪽은 정규화만 남기고 모듈 제거. |
| 2 (O에서 합침) | `abaddon.pivot.attack` 「자신의 공격기술에 「유턴」 효과를 부여한다.」 ↔ `effect.switch.pivot` 「자신의 기술에 「유턴」 효과를 부여한다.」 | 대상이 「공격기술」 한정이냐 「기술」 전체냐만 다르다(변화기술에 유턴을 줄지). | 처리: `PivotGrantRules`가 공격기술 한정 문장의 대상 조건을 보존한다. 실행 연결은 M에서 현재 기술을 넘겨 판정한다. | 모듈은 합치지 않는다. 공격기술 한정은 변화기술에서 교대를 요청하지 않고, 전체 기술 문장은 변화기술도 허용한다. |
| 3 | `abaddon.heal.full` 「자진의 포켓몬의 체력을 가득 회복한다.」 ↔ `effect.hp.회복` 「자신의 체력을 최대 HP의 1/N 회복한다.」 | 「가득」은 N=1(최대 HP의 1/1)과 결과가 같다. 다만 「자진의 포켓몬」(자기 편 포켓몬 중 선택, 필드 밖 포함 추정)이 대상 문구라 주체가 다르다. | 확인: 실프는 `RunFullHeal` 전용 실행기, 기존은 `FractionEffect`(`TryFraction`) 경로. 두 경로. | 대상 「자진의 포켓몬」의 의미가 확정될 때까지 보류. 확정되면 `1/1` 회복 + 대상 슬롯으로 `effect.hp.회복`에 합치는 안. |
| 4 | `ExclusiveModules`의 「상대의(자신의) 모든 상태변화를 해제한다.」 ↔ 기존 「자신의 「X」 상태를 해제한다」·`StateCure`(치유) | AGENTS 규칙상 「치유」는 상태이상(혼란 포함)만, 그 밖의 상태변화는 「해제」다. 「모든 상태변화」는 「해제」의 전체형이라 의미는 일관된다. | 처리: `ReleaseAllConditions`는 `ConditionState`만 해제하며 `Fighter.Status`(화상·독 등)는 보존한다. | 사용자 결정대로 상태이상은 포함하지 않는다. 상태변화 전체 해제 실행기는 별도 경로로 유지한다. |
| 5 | 필중·관통 부여 경로 세 개: ExclusiveModules(사건으로 얻는 필중·방어 관통, `PotentialEvents.Exclusive.cs`가 `ActiveModifier`로 부여) / `StateRolls`(상태 조건+확률 효과, `RollPoint.Action`에서 필중·관통 부여) / 상시 필중(`AbilityMoveModules` 「자신의 기술은 필중이다」, `ability.surehit*`) | 같은 효과(필중·관통)를 「항상」「사건 뒤 이번 행동」「확률 판정 뒤 이번 판정」 세 시점으로 건다. 문장은 `AbilityMoveModules`의 표준 문장을 공유(확인: ExclusiveModules 주석). | 확인: 문장은 하나이고 건너가는 길만 셋이다. 표준 문장 중복은 없다. | 합칠 필요 낮음. 부여 시점(상시/사건/확률)을 트리거·확률 쪽에서 구분하므로 효과 문장은 이미 하나. 정리 대상은 실행 코드 세 곳이 같은 `ActiveModifier`로 모이는지 확인하는 것(추정). |
| 6 | 추가행동·추가공격 변형: `effect.extra.*`(any/same/opponent/condition/reduced, 각 periodic) ↔ 버서크(`exclusive.berserk.attack` 「이때 가장 먼저 같은 기술에 의한 추가공격을 한다」) ↔ `ability.machine.extra` ↔ 칼데아 `T의 최후에 …에 의한 추가행동권` | 사용자 결정: 추가행동 ≠ 추가공격(추가공격 = 방금 낸 공격을 한 번 더). 그래서 버서크는 별도가 맞다. `effect.extra.same`(같은 기술에 의한 추가행동)과 비슷해 보이나 「추가공격」은 기술 선택 없이 직전 공격을 반복한다. | 확인: 정규화 쪽은 `PhraseRules.Cost*`·`Pre*ExtraActionPosition`이 서로 다른 표준 문장으로 보낸다. 실행기는 `ExtraActions`(추가행동)와 버서크 전용으로 분리(추정). | 합치지 않는다(사용자 결정 유지). 대신 `Classify` 종류 이름만 「추가행동」 계열로 맞추는 정도. |
| 7 | `SpeciesValueModules` 「자신의 「X」 종족치를 N 올린다」(수치 가산, 퇴장 시 복원) ↔ `effect.species.raise` 「종족치를 「N」로 하여 1랭크 올린다」·`effect.species.value`(값으로 한다) | 둘 다 종족치를 바꾸지만 단위가 다르다: 값(+N) vs 등급 랭크. 같은 `Classify` 종류 「종족치 변경」을 공유(확인: 테스트 출력). | 확인: 가산은 장소에 나올 때 한 번 적용하고 퇴장 시 복원(`SpeciesValueModules` 주석), 랭크/값 변경은 `SpeciesEffect` 경로. 둘 다 `SpeciesEffect`를 쓰는지는 추정. | 단위가 다르므로 모듈은 유지하되 `effect.species.value.add`라는 이름을 「가산」으로 구분 표시. 합칠 필요 없음. |
| 8 | `PotentialNullify` 「상대의 『X』을 무효화한다」(이름 지정 포텐셜 무효화) ↔ 방호 관통 `effect.guard.pierce.pokemon/trainer`(「자신의 기술이 상대의 방호 포텐셜을 무시한다」) | 방향이 다르다: 무효화는 상대 포텐셜을 **끈다**(발동 자체가 안 됨, 장소에 있는 동안), 관통은 **내 기술이 방호를 통과**한다(그 행동에 한함). 뜻이 겹치는 곳은 「상대 방호 포텐셜을 무효화한다」(칼데아 문장)이다. | 확인: 칼데아의 「상대 포켓몬과 상대 트레이너의 방호 포텐셜을 무효화한다」는 `PhraseRules.IgnoreGuardPotential`이 관통 문장 두 줄로 바꿔 관통 경로로 보낸다. 이름 지정 무효화는 별도 `PotentialNullify` 경로. | 합치지 않는다. 칼데아 변형은 이미 관통으로 정규화되고 있고, 이름 지정 무효화는 별개 기능. 「효과 종류별 무효화」(`ByEffectKind`)와 방호 관통의 경계만 문서화. |

## 카탈로그 전체에서 계열이 다른 모듈이 같은 `Classify` 종류를 쓰는 곳

`ModuleCleanupTests.CrossFamilyKinds`에 사유와 함께 둔 목록(ID 앞 두 마디가 다른 모듈끼리 같은 분류 종류를 공유). 위 1·2번이 실제 중복 후보, 나머지는 같은 사건·상태의 서식 변형(추정)이다.

| 분류 종류 | 모듈 |
|---|---|
| E:교대 효과 | effect.switch.voluntary, abaddon.switch.optional (위 1번) |
| E:유턴 효과 | effect.switch.pivot, abaddon.pivot.attack (위 2번) |
| E:타입 추가 | effect.type.add.*, ability.type.add (특성과 효과의 같은 모듈 추정) |
| E:수치 배율 | effect.scalar.*, ability.fallen.scalarrate |
| TriggerEvent:상태 됨 | trigger.role.changed, trigger.state.상태 됨 |
| TriggerEvent:소지품 소실 | trigger.item.stolen, trigger.itemloss.* |
| TriggerEvent:타이밍 | trigger.timing.*, trigger.exclusive.normal-switch-last |
| TriggerState:장소 | trigger.terrain, trigger.room |
| TriggerEvent:장소 변화 | trigger.terrainchange, trigger.roomchange.* |
| TriggerState:상시 | trigger.always, trigger.onfield |
| TriggerState:생존 | trigger.named.생존, trigger.last |
| TriggerState:소지품 | trigger.item.exists/absent, trigger.held.소지품 |
| TriggerEvent:타입 바뀜 / 타입 바뀜 T | trigger.typechanged.*, trigger.typebecame.* |

## 정규화가 기본값을 고쳐 쓰는 모듈 (재정규화 검사에서 나온 것)

- `ability.field.scalar.방어/속도/회피`: 기본값 문장의 조사가 「「방어」을」인데 정규화는 「를」로 고친다. 카탈로그 서식 쪽 조사 처리 버그 후보(동작 변경이라 고치지 않음).
- `effect.potential.grant`: 기본값 「『…』를」, 정규화는 「을」(받침 규칙).
- `trigger.entry.both`: 기본값이 두 줄이라 정규화가 한 줄로 합친다.
- `trigger.label.29`: 옛 표기(나무열매를 소모했을 때)를 표준(「나무열매」를 사용했을 때)으로 바꾼다.
- 끝 마침표: 약 30개 효과 기본값이 마침표를 단 채로 정규화되면 마침표가 빠진다(종족치·지형·교대·상태 부여·추가행동 계열). 의미는 같고 소비자가 마침표를 떼고 읽는다. 표준을 「마침표 있음」으로 통일할지는 사용자 판단 필요.
