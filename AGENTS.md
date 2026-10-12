# AA BATTLE 작업 안내

Windows C# WinForms 프로그램이다. 현재 상태는 `PROJECT_HANDOFF.md`, 빌드는 `desktop/build.ps1`을 참고한다. 웹 프로토타입을 주 배포 대상으로 착각하지 않는다.

## 공동 개발
- Claude가 설계·지시서·리뷰를, ChatGPT(Codex)가 지시서대로 구현을 맡는다. 지시서에 적힌 파일·범위만 수정하고, 범위 밖 발견은 고치지 말고 보고한다.
- 작업마다 `main`에서 `task/<작업명>` 브랜치를 만들어 커밋한다. verify(UI 변경은 `-Visual` 포함)가 모두 통과하면 최신 `origin/main` 위로 rebase하고 verify를 다시 실행한 뒤 `main`에 fast-forward 병합하고 push한다. 실패하거나 판단이 필요하면 브랜치만 push하고 보고한다. Claude는 병합 뒤에 리뷰하고, 문제가 있으면 수정 지시서를 낸다.
- 완료 보고는 이 형식으로 짧게 쓴다: 브랜치/커밋 해시 · 바꾼 파일과 이유(파일당 한 줄) · verify 결과(PASS 줄 그대로) · 지시서와 다르게 한 점 · 범위 밖 발견 사항.

## 코드
- 빌드는 .NET SDK 8 이상, 대상은 .NET Framework 4.8(`net48`), C#은 최신 문법(`LangVersion=latest`)이다. 단 런타임 기능이 필요한 것(기본 인터페이스 메서드, `Span` 기반 API, `System.Text.Json` 등)은 쓰지 않는다. JSON은 기존 `JavaScriptSerializer`를 쓴다.
- 솔루션은 `desktop/AABattle.sln`이다. 프로젝트 폴더에 `.cs` 파일을 두면 자동 포함된다. 짧은 한국어 주석은 유지하되, 새 코드는 한 줄 압축을 피하고 읽을 수 있게 여러 줄로 쓴다. 한 파일이 커지면(약 40KB 이상) 주제별 파일이나 `partial` 클래스로 나눈다.
- `desktop/AABattle.Engine`: 계산·규칙·데이터 모델. UI 참조가 없으며 WinForms·System.Drawing을 쓰면 빌드가 실패한다. 계산 `BattleEngine.cs`(Engine)·`Mechanics.cs`·`MechanicsStates.cs`(상태변화), 데이터 `BattleData.cs`·`DataModels.cs`·`ConditionState.cs`, 트리거 `TriggerStructures.cs`·`RuntimeTriggers.cs`·`EntryTriggers.cs`, 효과 `EffectRules.cs`, 편성 `TeamRules.cs`·`TrainerAccessRules.cs`, 포텐셜 `PotentialParts.cs`·`PotentialLibrary.cs`·`EditorTemplates.cs`.
- `desktop/AABattle.UI`: 화면. 배틀 `BattleApp.cs`(MainForm)·`BattleWidgets.cs`·`BattleDialogs.cs`, 테마 `Theme.cs`, 컨트롤 `PixelControls.cs`, 편집기 `DataEditor.cs`·`RetroEditor.cs`·`CatalogUI.cs`·`EffectBuilder.cs`, 시작 `StartUI.cs`. 규칙 판단은 UI에 두지 않고 엔진에 둔다.
- 시스템과 도감은 내장 Pokemon BW 폰트, AA는 HeadKasen을 사용한다. 흑백 도트 UI를 유지한다. 도트 폰트는 16·24·32·48px로만 쓰고(`Theme.UI`, 편집기는 `Theme.Enlarge`), 입력·선택·표·대화창은 `PixelControls.cs`의 픽셀 컨트롤을 쓴다. Windows 기본 컨트롤(`Label`, `TextBox`, `NumericUpDown` 등)과 `MessageBox`는 글자를 깨뜨리므로 쓰지 않는다.

## 규칙의 기준
- 우선순위: 사용자 결정 > 참치백과 오레마스 문서(`오레마스_시스템`, `오레마스_세계관_4대자질`, `오레마스_포텐셜일람`과 하위 문서) > 기존 시트 > 실기(Showdown). 문서는 `doku.php?id=<문서명>&do=export_raw`로 받는다.
- 사용자 결정(문서와 다른 부분): 지시 급소 보정은 지시 A +1·AA +2·AAA +3·S +4. PT ①은 통솔 A 또는 육성 A. 통솔 A의 필드형성 권한은 아직 다루지 않는다(이계 《필드》와 지형을 구분하는 구조 유지).
- 잠듦은 Showdown 5세대 기준(1~3회, 깨는 턴 행동, 교대 시 시작값으로 리셋), 잠자기는 파티 잠듦 2체 제한 예외.
- 상태이상(`Fighter.Status` 한 칸: 화상·독·맹독·마비·잠듦·얼음·동상)과 상태변화(`ConditionState`, 겹칠 수 있음)를 구분한다. 이미 걸린 상태변화를 다시 걸면 기술이든 포텐셜이든 실패하고 남은 턴은 유지한다(`Mechanics.HasCondition`·`AlreadyHas`). 상태이상은 이미 있으면 실패하고, 「무시하고」가 있을 때만 면역을 관통한다. 상태 편집 창의 직접 입력은 이 규칙의 예외다. 타입 변경(악인·침수·송전)은 같은 상태면 실패, 다른 타입 변경이면 마지막 것으로 바뀐다. 복수 타입 기술의 상성은 타입별 배율의 곱, 자속은 하나라도 일치하면 1회(확정 30).
- 효과 발동 타이밍은 `research/potentials/TIMING_RULES.md`(사용자 제공)를 따른다. 행동 순서: 가장 먼저 > 상대보다 먼저 > 우선도 > 속도(같은 등급끼리는 속도). 같은 타이밍의 포켓몬 효과는 속도 순, 트레이너 효과는 동시. 턴 종료는 대미지 → 드레인 → 회복 → 상태 해제 → 교대. 반사는 방호 포텐셜로 취급한다. 트레이너 포텐셜의 방호(『버텨라！』 포함)는 트레이너 포텐셜에서 나온 관통이나 「트레이너 방호」를 명시한 관통만 뚫는다.
- 소지품 성능의 기준은 참치백과 「아바돈_소지품」(`research/items/abaddon-items.txt`)과 오레마스 시스템의 소지품 규칙이다. 문장이 모듈로 읽히는 소지품은 슬롯 「소지품」 포텐셜로 실행하고(`ItemRuntime`), 나머지는 전용 코드로 처리한다.
- 포켓몬 입력 스탯은 종족값이며 실능력치와 구분한다. 포켓몬 기술 목록은 4개보다 많을 수 있다. 기능확장 = 기술 목록에 없는 기술을 내보내는 것(`FunctionExtensions`, `Move.extension`).
- 트레이너 고유 포텐셜은 트레이너 자신의 포텐셜이며 파티 전체에 작용한다. 그 트리거·효과의 「아군」은 장소에 있는 자기 포켓몬(포켓몬 포텐셜의 「자신」 관점)으로 읽는다.

## 포텐셜
- 포텐셜 도감은 독립된 트리거 조건 / 효과 목록이다. 같은 뜻만 합치고(`PotentialParts.TriggerKey`, `EffectKey`), 합친 결과는 `research/potentials/TRIGGER_MERGES.md`, `EFFECT_MERGES.md`에 남긴다.
- 합치지 않는 차이: 「공격」(공격기술 한정)과 「기술」(변화기술 포함), 주체(자신/아군/상대), 「있을 때」(상태)와 「되었을 때」(사건), 「할 때」와 「했을 때」, 확률·횟수·배율·대상·지속시간·타입·범위·랭크, 「할 수 있다」와 「한다」. 빈 괄호 「」·「??」는 타입 자리 표시로 보존한다.
- 효과의 기준은 편집기 구조화 효과 모듈의 표준 문장이다(`EffectModules`: 수치 배율·능력 랭크·종족치·최대 HP 1/N·상태 부여·상태이상 치유·우선도·교대 계열·데이터 해석, 타입·상성은 `TypeRules`). 수집 원문의 같은 뜻 변형은 `EffectModules.Normalize`에서 표준 문장으로 바꾸고, 실행기·효과 안내는 표준 문장만 해석한다. 새 표현은 정규화 규칙(+`EffectModuleTests`)으로, 새 기능은 편집기 모듈과 실행기를 함께 추가한다.
- 트리거의 기준은 `TriggerModules`의 표준 문장이다(사건: 등장·교대·퇴장·타이밍·피격·명중·급소·쓰러뜨림·기술 사용·상태 됨/해제·체력 변화·회복·날씨/장소 변화·랭크, 상태: 상태·체력·날씨·장소). 상태·체력·랭크·날씨 사건은 `StateWatch`가 전후 비교로 낸다. 수집 변형은 `TriggerModules.Normalize`로 바꾸고, 엔진은 라벨 문자열 대신 사건(`TriggerFact`)을 내서 `TriggerModules.Matches`로 대조한다. 새 트리거 표현은 정규화(+`TriggerModuleTests`), 새 트리거 종류는 모듈·사건 지점·편집기(`TriggerBuilder.cs`)를 함께 추가한다. 사건 절과 함께 쓴 상태 절은 AND(확정 48).
- 효과 실행은 데이터 표로 한다. 상태 부여 문장(「X」상태로 한다 / 상태가 된다)은 `StateEffects.Table`이 상태 이름 → 엔진 함수로 연결한다. 새 상태를 구현하면 이 표에 한 줄을 추가하고(별칭·기본 대상 포함), 문장별 `if` 분기를 새로 만들지 않는다. 추가 조건이 붙은 포텐셜과 해석하지 못한 문장은 실행하지 않고 "효과 판정 대기"로 남긴다.
- 출처와 원문은 내부 자료에 남긴다. 수집 JSON 전체를 컨텍스트로 읽지 말고 이름 검색이나 집계로 필요한 항목만 찾는다. 자연어 자동 분리를 검증된 엔진 규칙으로 취급하지 않는다.

## 검증과 보고
- C# 변경 후 `./desktop/verify.ps1 -OutputPath outputs/candidate`로 빌드와 회귀 검사(NUnit, `desktop/AABattle.Tests`)를 실행한다. UI 변경은 `-Visual`을 붙여 생성된 캡처 이미지를 확인한다.
- EXE가 사용 중이면 `./desktop/build.ps1 -OutputPath outputs/candidate`로 별도 빌드하고 실제 실행 경로를 안내한다.
- 동작 변경은 관련 회귀 검사와 `PROJECT_HANDOFF.md`를 갱신한다. 검사는 `AABattle.Tests`에 기능별 `[Test]`로 추가하고, 실행 코드 안에 새 검사를 넣지 않는다. 버그 수정이나 규칙이 갈리는 지점만 검사하고, 단순 존재 확인·데이터 개수 고정·문장 전체 비교는 넣지 않는다. 같은 모양의 검사는 `[TestCase]`로 묶는다. UI 리플렉션 검사는 `[Category("UI")]`를 붙인다. 산출물·임시 파일·비밀은 Git에서 제외한다.
- 사용자 요청 범위와 연결된 수정만 한다. 변경 이유, 테스트 결과, 실행 파일 위치를 짧게 보고한다.
