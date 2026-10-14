# 샘플·조사 자료 (PROJECT_HANDOFF.md에서 옮김)

## 실프 인헨스먼츠 남은 모듈 (지시서 F)
- 새 파일: `AbaddonModules.cs`(효과 표준 문장·정규화·실행기·편집기 등록), `AbaddonTriggers.cs`(트리거), `AbaddonRuntime.cs`(기술 변형·위력 약화·치유 봉인·최초 우선도·회복 반납·아이스번), `StateEffectsAbaddon.cs`(번개구름).
- 타입을 단 대미지는 상성을 받는다: 「상대에게 최대 HP의 1/N 「X」 타입 대미지를 준다.」(칼데아 대미지 변형도 이 문장으로 모은다).
- 「…한 T」 상태는 같은 사건의 순간에도 성립한다(한 번 일어나는 효과가 그 순간 실행). `ConditionState.Repay`(턴 끝 반납)와 `WatchTypes`(타입 변화 비교)가 새로 생겼다.
- 최초 접촉 기술 우선도 「올릴 수 있다」는 손해가 없어 선택 확인 없이 항상 적용한다. 「필드에 없어도 발동」은 문장만 인식한다(대기석 발동은 미구현).
- 검사: `AbaddonGapTests`.

## 변경 기록

## 2026-10-14 샘플 파티: 아바돈 기업 탐사대(실프 인헨스먼츠)
- 지시서 `research/tasks/2026-10-14-abaddon-sample-party.md`, 브랜치 `task/abaddon-sample-party`. 원문 시트는 `research/samples/abaddon/*.txt`, 모듈로 못 만든 것 정리는 `research/samples/abaddon/MODULE_GAPS.md`.
- 데이터: `samples/abaddon-sylph.aabdata.json`(저장 형식 그대로). 편집기 「열기」 또는 시작 화면 팀 파일 불러오기로 연다. 트레이너 마히토(지시 B · 육성 EX · 통솔 D+(원문 D+(E+)) · 능력 AA+, 고유 『뻗어채내는 주령의 손』 + 전개 지령) + 포켓몬 6체(마히토·오버홀·삼각두·루나틱·이리스·로보코), 팀 1개. 전용 포텐셜 7개는 각 포켓몬 슬롯 「전용」. 『너를 죽이기위한 작품』은 사용자 결정으로 제외.
- 종족값: 체력 100(로보코 150), 나머지는 시트 등급을 `SpeciesStatRanks` 범위의 가운데 값으로 환산(E-1 E18 E+37 / D-40 D48 D+57 / C-60 C72 C+87 / B-90 B98 B+107 / A-110 A122 A+137 / AA-140 AA152 AA+167 / AAA-170 AAA182 AAA+197 / S200). 레벨 200(기존 샘플과 동일). 성별은 AA 끝의 ♂/♀.
- 슬롯은 시트 순서가 `PokemonSlotNames` 순서와 같음을 이용해 배정(역할·분류·이명·종족 ①~④·속별·유대·선·회피·내성·격·범용·부수·특권). 무위전변은 특권, 도살자는 발동 금지로 저장.
- 수정: ① `EditorBridge.ToPokemon`이 기술을 4개로 자르던 것 제거(4개 제한 해제가 누락되어 있었다). ② 포텐셜 편집 창이 상대 타입이 빈 회피·내성·격 행을 열 때 이름(대투회피 등)에서 타입을 알아내 템플릿 이름 선택지를 불러온다. ③ `EffectModules.Normalize`: 「기술의 우선도를 +N한다」 → 「기술 우선도를 N 올린다.」(대의 선).
- 검사: `AbaddonSampleTests`(불러오기·변환·`TeamRules` 위반 없음, 템플릿 편집 창 불러오기, 6명 선봉 1턴), `PotentialEditorWindowTests.SavedCounterPotentialsReload…`, `EffectModuleTests.PriorityPlusVariant…`.
## 2026-10-14 피니스 칼데아 포텐셜 판정 (task/chaldea-potentials)
- 자료: `research/samples/chaldea/POTENTIALS.tsv`(30개 시트의 포텐셜·특성·기술 3010줄과 엔진 판정), `MODULE_GAPS.md`(남은 묶음). 지시서 `research/tasks/2026-10-14-chaldea-potentials.md`.
- 판정 도구: `PotentialJudge`(Engine, 트리거 모듈 + 효과 실행기/모듈 존재 여부), `SheetJudge`(특성은 도감·`AbilityModules.Runnable`, 기술은 `MoveRuntime.Plan`), `ChaldeaJudgeTool`(Tests, Explicit: 환경변수 CHALDEA_IN/OUT으로 시트 TSV를 판정).
- 정규화: `ChaldeaModules`(효과: 필중·추격+필중·위력 강화+추격/방어 관통·방어 등의 기술 관통·방호 한꺼번에 관통·우선도 +N·공격 이외 대미지·상태이상/소지품 보호·기합·배화/반감/능력 괄호쌍), `ChaldeaTriggers`(명중·급소·교대·빈사로 했을 때·죽어내밀기·속도 비교·트레이너 이름·공격하지 않음). 새 모듈은 없고 기존 표준 문장으로 바꾼다.
- 한계: 판정은 표준 문장·실행기 존재 기준이다. 상태 조건 + 확률 효과는 발동 사건이 없어 실제로는 돌지 않는다(MODULE_GAPS 상단).
