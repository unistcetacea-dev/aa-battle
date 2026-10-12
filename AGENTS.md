# AA BATTLE 작업 안내

Windows C# WinForms 프로그램이다. 현재 상태는 `PROJECT_HANDOFF.md`, 빌드는 `desktop/build.ps1`을 참고한다. 웹 프로토타입을 주 배포 대상으로 착각하지 않는다.

## 공동 개발
- Claude가 설계·지시서·리뷰를, ChatGPT(Codex)가 지시서대로 구현을 맡는다. 지시서에 적힌 파일·범위만 수정하고, 범위 밖 발견은 고치지 말고 보고한다.
- 작업마다 `main`에서 `task/<작업명>` 브랜치를 만들어 커밋·push한다. `main`에는 직접 커밋하지 않는다. 병합은 리뷰 후 Claude가 한다.
- 완료 보고는 이 형식으로 짧게 쓴다: 브랜치/커밋 해시 · 바꾼 파일과 이유(파일당 한 줄) · verify 결과(PASS 줄 그대로) · 지시서와 다르게 한 점 · 범위 밖 발견 사항.

## 코드
- 빌드는 .NET SDK 8 이상, 대상은 .NET Framework 4.8(`net48`), C#은 최신 문법(`LangVersion=latest`)이다. 단 런타임 기능이 필요한 것(기본 인터페이스 메서드, `Span` 기반 API, `System.Text.Json` 등)은 쓰지 않는다. JSON은 기존 `JavaScriptSerializer`를 쓴다.
- 솔루션은 `desktop/AABattle.sln`이다. 새 `.cs` 파일은 `desktop/`에 두면 `AABattle.Core`에 자동 포함된다. 짧은 한국어 주석은 유지하되, 새 코드는 한 줄 압축을 피하고 읽을 수 있게 여러 줄로 쓴다.
- UI: `BattleApp.cs`, `DataEditor.cs`, `RetroEditor.cs`, `CatalogUI.cs`, `StartUI.cs`. 계산: `BattleEngine.cs`, `Mechanics.cs`. 편성 규칙: `TeamRules.cs`. 포텐셜 부품: `PotentialParts.cs`.
- 시스템과 도감은 내장 Pokemon BW 폰트, AA는 HeadKasen을 사용한다. 흑백 도트 UI를 유지한다. 도트 폰트는 16·24·32·48px로만 쓰고(`Theme.UI`, 편집기는 `Theme.Enlarge`), 입력·선택·표·대화창은 `PixelControls.cs`의 픽셀 컨트롤을 쓴다. Windows 기본 컨트롤(`Label`, `TextBox`, `NumericUpDown` 등)과 `MessageBox`는 글자를 깨뜨리므로 쓰지 않는다.

## 규칙의 기준
- 우선순위: 사용자 결정 > 참치백과 오레마스 문서(`오레마스_시스템`, `오레마스_세계관_4대자질`, `오레마스_포텐셜일람`과 하위 문서) > 기존 시트 > 실기(Showdown). 문서는 `doku.php?id=<문서명>&do=export_raw`로 받는다.
- 사용자 결정(문서와 다른 부분): 지시 급소 보정은 지시 A +1·AA +2·AAA +3·S +4. PT ①은 통솔 A 또는 육성 A. 통솔 A의 필드형성 권한은 아직 다루지 않는다(이계 《필드》와 지형을 구분하는 구조 유지).
- 잠듦은 Showdown 5세대 기준(1~3회, 깨는 턴 행동, 교대 시 시작값으로 리셋), 잠자기는 파티 잠듦 2체 제한 예외.
- 포켓몬 입력 스탯은 종족값이며 실능력치와 구분한다.

## 포텐셜
- 포텐셜 도감은 독립된 트리거 조건 / 효과 목록이다. 같은 뜻만 합치고(`PotentialParts.TriggerKey`, `EffectKey`), 합친 결과는 `research/potentials/TRIGGER_MERGES.md`, `EFFECT_MERGES.md`에 남긴다.
- 합치지 않는 차이: 「공격」(공격기술 한정)과 「기술」(변화기술 포함), 주체(자신/아군/상대), 「있을 때」(상태)와 「되었을 때」(사건), 「할 때」와 「했을 때」, 확률·횟수·배율·대상·지속시간·타입·범위·랭크, 「할 수 있다」와 「한다」. 빈 괄호 「」·「??」는 타입 자리 표시로 보존한다.
- 출처와 원문은 내부 자료에 남긴다. 수집 JSON 전체를 컨텍스트로 읽지 말고 이름 검색이나 집계로 필요한 항목만 찾는다. 자연어 자동 분리를 검증된 엔진 규칙으로 취급하지 않는다.

## 검증과 보고
- C# 변경 후 `./desktop/verify.ps1 -OutputPath outputs/candidate`로 빌드와 회귀 검사(NUnit, `desktop/AABattle.Tests`)를 실행한다. UI 변경은 `-Visual`을 붙여 생성된 캡처 이미지를 확인한다.
- EXE가 사용 중이면 `./desktop/build.ps1 -OutputPath outputs/candidate`로 별도 빌드하고 실제 실행 경로를 안내한다.
- 동작 변경은 관련 회귀 검사와 `PROJECT_HANDOFF.md`를 갱신한다. 검사는 `AABattle.Tests`에 기능별 `[Test]`로 추가하고, 실행 코드 안에 새 검사를 넣지 않는다. 버그 수정이나 규칙이 갈리는 지점만 검사하고, 단순 존재 확인·데이터 개수 고정·문장 전체 비교는 넣지 않는다. 같은 모양의 검사는 `[TestCase]`로 묶는다. UI 리플렉션 검사는 `[Category("UI")]`를 붙인다. 산출물·임시 파일·비밀은 Git에서 제외한다.
- 사용자 요청 범위와 연결된 수정만 한다. 변경 이유, 테스트 결과, 실행 파일 위치를 짧게 보고한다.
