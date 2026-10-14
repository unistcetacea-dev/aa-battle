# UI·편집 화면·데이터 ID (PROJECT_HANDOFF.md에서 옮김)

## 팀 편집기 통합 UI
- `RetroEditor.cs`: 왼쪽 방향키 메뉴, 흑백 이중 프레임, F6 메뉴 복귀. 스페이스로 메뉴와 선택상자를 조작하며 텍스트 입력 중에는 IME와 커서 조작을 보존한다. BW 글꼴은 직접 그리는 메뉴·설명에, 한글 입력 컨트롤은 글자 깨짐을 피하도록 맑은 고딕에, AA는 HeadKasen에 배정했다.
- 포텐셜 탭 위쪽 목록과 아래쪽 트리거·효과 직접 입력 및 구조 선택기를 통합했다. 템플릿 변경/행 전환/직접 입력 동기화를 화면 회귀 검사에 추가했다. 기존 질문 두 문서는 보존한다.
- 검증: `verify.ps1 -Visual -EditorOnly -OutputPath outputs/candidate`로 계산·편집기 검사와 편집기 렌더링을 수행한다. 전체 배틀 자동 캡처는 기존 경로에서 시간 초과하여 이번 UI 검증에서 별도로 제외했다. 실제 한국어 IME 키보드 조합은 사용자 환경 확인이 남는다.
- 로컬 기본 실행기는 `outputs/desktop/AABattle.exe`, 공유 실행기는 `release/windows/AABattle.exe`다. 둘 다 같은 최종 소스로 갱신한다.

## 변경 기록

## 2026-09-16 편집 화면 연결 수정
- 포켓몬/트레이너 기본 정보 아래에 같은 포텐셜 목록과 트리거·효과 편집기를 배치했다. 별도 포텐셜 메뉴와 개별 포켓몬 좌우 배틀 슬롯 버튼은 제거했다. 타입·특성·기술 검색 버튼은 각 입력란 옆에 있다. 기술 선택의 임의 4개 제한도 제거했다.
- `ConfirmedTriggerCatalog.cs`에 확정 답변의 조건 입력 예문을 등록했다. 따옴표 안의 이름·날씨·능력과 수치 등을 `조건 값`에서 순서대로 바꾼다(`|` 구분). 별도 task로 보류한 규칙은 기존 질문 문서에 유지한다. 이 목록은 입력 지원이며, 기존 엔진의 자연어 조건/효과 자동 실행이 모두 완성되었다는 뜻이 아니다.
- 구조/입력 방식/주체는 현재 값이 표시되는 도트 버튼에서 검색하여 선택한다. 검색창은 SPACE 체크와 한글 검색을 지원하며 필터 변경 때 선택이 다른 항목에 적용되던 지연 이벤트를 없앴다.
- 자체 BW 입력 표시층을 시험했으나 기본 입력창을 안정적으로 대체하지 못해 제거했다. 한글 입력은 기존 맑은 고딕/Windows IME를 유지한다. 메뉴·프레임·목록 제목·구조 선택 버튼은 BW 직접 렌더링을 사용한다.
- 검증: 계산 70개 회귀 검사, 편집기 검사, 템플릿/직접 입력 동기화, 날씨·HP 조건 값 변경, 컨트롤 표시 검사를 통과하고 통합 화면 이미지를 확인했다. 실제 사용자 키보드의 한국어 IME 조합은 자동 검증하지 않았다.

## 2026-09-16 포켓몬 포텐셜 화면 선택 복구
- 좌측 `포켓몬` 메뉴를 열 때 현재 트리 선택이 트레이너여도 첫 포켓몬 레코드를 자동 선택한다. 따라서 포텐셜 표가 `고유·전용·지령` 슬롯으로 남지 않고 포켓몬의 `역할`부터 `PT ②`까지 19종 슬롯을 다시 표시한다.

## 2026-09-16 반응형 배틀·편집 화면
- 배틀 창의 카드·기술 버튼·AA 영역은 창의 현재 클라이언트 크기로 다시 배치하며, 작은 중간 크기에서도 음수 영역이 생기지 않도록 최소 크기와 프레임 그리기를 방어한다.
- 포켓몬 편집기의 기본 정보·6개 종족값 입력 영역은 충분한 높이를 확보해 기본 화면에서 스크롤을 거의 사용하지 않는다. 기술 입력란은 입력 줄 수에 따라 최대 범위까지 높이를 늘리고, 포텐셜 표는 읽기 쉬운 행 높이를 유지하면서 포텐셜 행 수에 따라 통합 작업 영역을 확장한다. 화면 작업 영역을 넘는 내용은 기존 스크롤로 접근한다.
- 통합 포텐셜 편집기는 왼쪽에 포텐셜 목록, 오른쪽에 선택 행의 트리거·효과 편집을 둔다. 오른쪽 폭이 좁으면 트리거 구조와 효과 구조를 세로로 재배치해 컨트롤이 눌리지 않게 하고, 넓은 창에서는 2열 구조를 유지한다.
- 기술·포텐셜을 추가하거나 현재 포켓몬을 바꿀 때 레이아웃을 예약 갱신하고, 화면 작업 영역 안에서만 편집기 창을 자동 확장한다. 관련 검증은 `verify.ps1 -Visual -EditorOnly`에 포함한다.

## 2026-10-12 게임풍 UI와 픽셀 컨트롤
- Pokemon BW 폰트는 16px 격자(125 단위 = 1px)로 그려진 도트 폰트다. `Theme.UI`는 크기를 16·32·48px로만 맞춘다(`Theme.Snap`). 이전의 18·20·22px 렌더링이 글자를 뭉개던 원인이다.
- Windows 기본 컨트롤(TextBox·NumericUpDown·ComboBox 편집부·CheckBox·DataGridView 셀·TreeView)은 GDI로 이 OTF를 그리면서 글자를 깨뜨린다. `desktop/PixelControls.cs`의 `PixelTextBox`(한글 IME 조합 직접 처리), `PixelNumber`, `PixelComboBox`, `PixelCheckBox`, `PixelCheckedListBox`, `GameDialog`(MessageBox 대체), `PixelTree`, 표 셀 편집기 `PixelGridEditor`로 교체했다. 새 UI에서 기본 컨트롤이나 `MessageBox`를 직접 쓰지 않는다.
- 공통 테마: 둥근 2px 게임 창 테두리와 디더 그림자(`Theme.Frame`), ▶ 픽셀 커서(`Theme.Cursor`), HP 태그·디더 단계 HP바(`Theme.HpBar`), 픽셀 탭(`Theme.Tabs`). 흑백만 사용한다.
- 시작 화면은 타이틀·1P/2P 팀 창·키보드 메뉴(`GameMenu`, ↑↓/Enter)로 바꿨다. 배틀 메시지 창은 글자가 한 자씩 나오고 ▼가 깜빡이며, HP는 서서히 줄어든다. 캡처·자체검사 모드에서는 `Theme.Instant`로 애니메이션을 끈다.
- 편집기와 편집용 대화창은 `Theme.Enlarge`로 라벨·버튼 32px, 입력칸·표·트리·상세 설명 24px로 키운다(24px은 GDI+ 격자 맞춤에서 깨지지 않음을 확인). 배틀·시작 화면은 16/32px 그대로다. 포텐셜 효과 구조 패널은 트리거/효과 구조를 위아래로 쌓아 각자 전체 폭을 쓴다.
- 편집기는 왼쪽 메뉴를 유일한 탐색으로 쓰고 탭 머리글과 중복 툴바 버튼(상태·날씨, 효과 구현 방식)을 숨겼다.
- 검증: `verify.ps1 -Visual -OutputPath outputs/ui-work` 통과.

## 2026-10-12 안정 데이터 ID (지시서 E)
- `PokemonRecord.id`·`TrainerRecord.id`(`pk-`/`tr-` + 12자리). 팀은 `trainerId`·`memberIds`만 저장하고 사본을 저장하지 않는다. 저장 형식은 `aa-battle-data-v2`.
- `AABattle.Engine/ProjectStore.cs`: `Parse`(읽기 → 빈 목록 보정 → id 부여·중복 id 재부여 → 옛 형식 이전 → 팀 연결), `Serialize`(id 부여 → 팀 참조 동기화 → 팀 사본 없이 직렬화). 불러온 뒤 `team.trainer`·`team.members`는 프로젝트 목록의 같은 객체이므로 원본 수정이 팀에 바로 반영된다. 기존 UI 코드는 그대로 `team.members`를 쓴다. 편집기 열기·저장, 시작 화면의 파일 팀 불러오기, 편집기 가져오기/내보내기가 이 경로를 쓴다.
- 옛 파일(v1, 팀이 사본 보유) 이전: 사본의 id → 같은 이름(내용이 같은 것 우선, 한 팀 안에서 중복 연결 없음) 순서로 원본에 연결한다. 원본이 없으면 사본을 포켓몬/트레이너 목록에 추가한다. 원본이 그 뒤 수정되었으면 팀은 수정된 원본을 따른다(의도된 동작). 다음 저장부터 v2로 기록된다.
- `Pokemon.id` = 레코드 id(이전에는 모두 `"editor"`). 파티 동일성은 `Mechanics.SameMember`(둘 다 id가 있으면 id, 아니면 이름)와 `Mechanics.MemberKey`로 통일했다: `SetParties`, 지령 대상 파티, 교대 후보·선택, 배틀 화면 대기 멤버 갱신·교체·분석. 같은 종족(같은 이름) 두 마리가 파티에서 하나로 합쳐지던 문제가 해결된다.
- 트리거 답변 36번(이름으로 확인)은 아직 이름 기준이다. ID 기준으로 바꿀지는 미정.
- 병합 규칙 갱신(`AGENTS.md`): 구현 측이 verify 통과 후 rebase·ff 병합·push, Claude는 병합 뒤 리뷰.
- 범위 밖 발견: `verify -Visual`의 `flow.teams.png`·`states.png`·`implementations.png`는 편집기 메뉴 순서 변경 뒤 탭 번호(`SelectTabForTest(6~8)`)가 어긋나 다른 탭을 찍는다.
- 검증: `ProjectStoreTests`(참조 저장, 원본 수정 반영, v1 이전·같은 이름 내용 매칭·원본 없는 사본, 중복 id, 배틀 id, 같은 종족 파티 구분) 포함 109개 통과.

## 2026-10-14 포텐셜 편집 창 분리 · 기술 조건 입력 · 성별 · 전용 포텐셜
- 지시서 `research/tasks/2026-10-14-potential-editor-window.md`, 브랜치 `task/potential-editor-window`. 이 절이 위의 「오른쪽 트리거·효과 편집 패널」 서술(154·218행 등)을 대체한다.
- 포켓몬·트레이너 화면에는 포텐셜 표만 남는다(`DataEditor.cs`). 표는 보기 전용이다(「발동 금지」 토글만 직접 바뀌고 한 번 저장한다). 행을 고르고 「편집」(또는 더블클릭)을 누르면 별도 창 `PotentialEditWindow`(`PotentialEditWindow.cs`)가 뜬다. 「새 포텐셜 +」도 같은 창이며 확인해야 목록에 들어간다.
- 편집 창은 `PotentialDraft`(행 값 사본)만 고친다: 종류·이름(고정 선택지가 있는 종류는 고르는 칸, 아니면 직접 입력)·상대 타입(타입별·계약의 특권만)·해제 권한·발동 금지·트리거/효과 글자 칸·같은 트리거/효과 빈칸 채우기 화면. 「확인」은 `PotentialEditing.cs`의 `CommitPotentialRow`가 표에 한 번 쓰고 한 번 저장한다(역할 이름을 바꾸면 짝 `부수` 행도 맞춘다). 「취소」·창 닫기는 사본을 버린다. 빈칸 채우기 화면은 편집기가 하나만 만들어 「포텐셜 도감」 탭(숨김)에 보관하다가 창이 열릴 때 옮겨 쓰고 닫으면 돌려놓는다(`AttachBuilders`/`ReleaseBuilders`; `triggerBuilder`·`effectBuilder` 필드는 그대로).
- 다시 로딩되던 원인: 표 이름 셀 직접 입력이 `CellValueChanged → PotentialChanged`에서 글자마다 `GridChanged`(=`SaveCurrent`로 표 전체를 레코드 목록에 다시 쓰고 트리 노드 글자 갱신) + `LoadPotentialStructureSettings`(트리거·효과 빈칸 채우기 화면 다시 불러오기)·`RefreshInline`을 불렀고, `SelectionChanged`/`CurrentCellChanged`도 구조 화면을 다시 불러왔다. 이제 표 셀은 읽기 전용이고 그 경로를 지웠다.
- 기술 조건 빈칸(`MoveSearch.cs`의 `MoveConditionSlotEditor`, 트리거·효과 공통): 「분류」(콤보: 공격·변화·물리·특수·접촉·타입 등 `MoveCondition`이 읽는 토큰) / 「기술 이름」(직접 입력 + 「기술 찾기」 창 `MoveSearchForm`). 이름 칸에 적은 말 중 기술 데이터에 있는 이름은 `「기술:이름」`으로 바뀐다. 표준 문장 형식은 그대로다. 다시 열 때 토큰 하나(또는 빈칸)면 분류, 여러 토큰이나 `「기술:…」`면 이름 방식으로 복원한다. 검색 규칙은 기술 도감 탭과 같은 `MoveDexSearch.Filter`를 쓴다. 한계: 후보는 입력 칸 아래 목록이 아니라 「기술 찾기」 창에서 걸러진다.
- 성별: `PokemonRecord.gender`(수컷/암컷/없음, 기본 없음)·`Pokemon.gender`·편집 화면 「성별 · 아인종」 선택 칸. 저장·불러오기·`EditorBridge.ToPokemon`·`ClonePokemon`에 연결했다. 배틀 화면의 포켓몬 편집(`FighterForm`)에도 「성별」 선택 칸이 있다(`Fighter.Data.gender`, 내장 도감 포켓몬은 기본 없음; 팀 보관함은 `PokemonRecord.gender`로 왕복하고 배틀 쪽 편집은 팀 기록에 되쓰지 않는다). 엔진 판정 `FailModules`의 「상대와 성별이 같으면 실패한다」(헤롱헤롱·유혹 2개 기술 문장)는 같거나 둘 중 하나라도 없음이면 실패한다(`Genders.SameOrMissing`). 내장 도감 포켓몬은 성별이 없어 이 두 기술이 실패한다. 성별을 보는 그 밖의 엔진 문장은 없다(투쟁심은 도감에서 타입으로 변경됨, 「트레이너 성별」은 상시 플레이버).
- 전용 포텐셜: 포켓몬 편집에서 슬롯 `전용`으로 단다(`EditorTemplate.PokemonEditSlotNames`). 실행 때는 포켓몬 포텐셜(`Potential.id=="전용"`)로 들어가 그 포켓몬에만 작용하고, 트레이너 포텐셜 판정은 `PotentialInstance.IsTrainerPotential`(트레이너 기록이거나 `전용`)과 `PotentialProtection.Lines`가 본다: 전용의 방호는 「트레이너 방호」, 전용에서 나온 관통은 트레이너 방호도 뚫는다(포켓몬 포텐셜의 관통은 못 뚫음). 로그는 `TRAINER 『이름』`, 사건으로 건 배율은 `ActiveModifier.Trainer`. 개수 제한(`TeamRules.ExclusiveLimit`)은 파티 포켓몬의 전용만 합산한다. 데이터 해석은 `AnalysisForm.PublicPotentials`가 계속 숨긴다. 트레이너 쪽 옛 `전용` 행은 불러올 때(`ProjectStore.Normalize`) 버린다(다음 저장 때 파일에서도 사라진다). 트레이너 슬롯 목록(`TrainerSlotNames`·`TrainerEditSlotNames`)에서 전용을 뺐고, 시트 출력의 트레이너 「【전용 포텐셜】」 절은 포켓몬 시트의 「【전용 포텐셜】」로 옮겼다.
- 검사: `PotentialEditorWindowTests`(UI: 확인/취소 반영, 입력 중 표·트리·행 개체 불변, 표 읽기 전용, 템플릿 채움, 새 포텐셜, 전용 선택지·성별 칸, 기술 조건 분류/이름 동일 문장·복원), `GenderAndExclusiveTests`(성별 세 갈래·왕복, 전용 방호/관통, 파티 합산(포켓몬만), 옛 트레이너 전용 로드 시 삭제, 해석 비공개), `FighterFormGenderTests`(배틀 편집 성별 칸 → 왕복 → 헤롱헤롱 판정). 캡처: `catalog.png.window.png`·`.window-move.png`(편집 창)·`.pokemon.png`·`.potentials.png`·`.trainer.png`(표만 남은 편집 화면).

## 2026-10-14 파티 후보와 배틀 출전 선택 (task/H-party-roster)
- `TeamRecord`는 트레이너 ID 목록과 포켓몬 후보 목록을 참조한다. 트레이너·포켓몬은 여러 파티에서 공유하며 원본 수정이 각 팀에 반영된다. 기존 `trainerId`/`trainer`만 가진 파일은 단일 소속으로 이전한다.
- `defaultTrainerId`와 `defaultMemberIds`는 배틀마다 출전 선택 화면을 열 때 초기 선택으로 쓴다. 화면에서 트레이너 1명과 후보 포켓몬 최대 6체를 고른 뒤 그 구성만 `TeamRules.Check`, VS 연출, 배틀에 전달한다. `PokemonRecord.formOf`가 지정된 폼 데이터는 후보에서 숨긴다.
- 편집기의 `팀 데이터` 화면은 여러 트레이너와 포켓몬 후보를 파티에 연결한다. 샘플 `samples/abaddon-sylph.aabdata.json`은 기존 6체를 기본 출전으로 저장한다.
- 검사: `PartyRosterTests`의 다중 파티 참조·기본 출전 저장 복원과 출전 선택·폼 숨김·선택 구성 편성 검사. `verify.ps1 -Visual`은 `outputs/candidate-H/flow.roster.png`(출전 선택) 및 `flow.setup.png`(파티 편집)도 생성한다.
## 2026-10-14 N 작은 수정 묶음 (task/N-small-fixes)
- 효과 표준문장 끝 마침표 및 조사 정정, 특성 검색창 공용화/슬롯 입력, 상태변화 해제 시 상태이상 유지와 타입 대미지 특성 무효화 비적용 회귀를 반영했다. 두 정규화 진입점 연결은 사용자 승인 범위다.
- 전체 검증: 통과!  - 실패:     0, 통과:  2735, 건너뜀:     0, 전체:  2735, 기간: 1 m 15 s - AABattle.Tests.dll (net48); -Visual 완료. 공격기술 한정 유턴 런타임 연결은 M에서 처리한다.

## 사용자 결정

원본: `research/tasks/2026-10-14-user-decisions.md`

### 구조·운영 (2026-10-14 대화) (일부)
- **폼 변경** = 특정 포켓몬을 지정해 **다른 포켓몬 데이터로 바뀐다**(종족치·타입·특성·포텐셜 등 데이터 통째 교체, 대상 데이터를 지정). 「폼이 바뀌었을 때」 같은 **변화 사건 트리거**도 생긴다.
- **파티 데이터 구조 변경(예정)**: 파티 데이터에 포켓몬(폼 데이터 포함)과 트레이너를 **여러 명 소속**시켜 두고, 배틀마다 **누가 출전할지 고른다**(트레이너·포켓몬이 한 파티에 다수 소속되는 경우 대비). 샘플(실프·칼데아)은 이 구조의 파티로 넣는다.
