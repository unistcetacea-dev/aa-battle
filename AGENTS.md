# AA BATTLE 작업 안내

Windows C# WinForms 프로그램이다. 현재 상태는 `PROJECT_HANDOFF.md`, 빌드는 `desktop/build.ps1`을 참고한다. 웹 프로토타입을 주 배포 대상으로 착각하지 않는다.

## 공동 개발
- Claude가 설계·지시서·리뷰를, ChatGPT(Codex)가 지시서대로 구현을 맡는다. 지시서에 적힌 파일·범위만 수정하고, 범위 밖 발견은 고치지 말고 보고한다.
- 작업마다 `main`에서 `task/<작업명>` 브랜치를 만들어 커밋·push한다. `main`에는 직접 커밋하지 않는다. 병합은 리뷰 후 Claude가 한다.
- 완료 보고는 이 형식으로 짧게 쓴다: 브랜치/커밋 해시 · 바꾼 파일과 이유(파일당 한 줄) · verify 결과(PASS 줄 그대로) · 지시서와 다르게 한 점 · 범위 밖 발견 사항.

## 코드
- 컴파일러는 .NET Framework 4의 `csc.exe`라서 C# 5 문법만 쓴다. 금지: `?.`, `$""` 보간 문자열, `=>` 식 본문 멤버, 자동 속성 초기화, `out var`, 튜플, `nameof`, 패턴 매칭, 로컬 함수, `using static`.
- 기존 스타일(한 줄로 압축된 메서드, 짧은 한국어 주석)을 따른다. 새 `.cs` 파일은 `desktop/build.ps1`의 `$sources`에 추가한다.
- UI: `BattleApp.cs`, `DataEditor.cs`, `RetroEditor.cs`, `CatalogUI.cs`, `StartUI.cs`. 계산: `BattleEngine.cs`, `Mechanics.cs`. 편성 규칙: `TeamRules.cs`. 포텐셜 부품: `PotentialParts.cs`.
- 시스템과 도감은 내장 Pokemon BW 폰트, AA는 HeadKasen을 사용한다. 흑백 도트 UI를 유지한다. 도트 폰트는 16·24·32·48px로만 쓰고(`Theme.UI`, 편집기는 `Theme.Enlarge`), 입력·선택·표·대화창은 `PixelControls.cs`의 픽셀 컨트롤을 쓴다. Windows 기본 컨트롤(`Label`, `TextBox`, `NumericUpDown` 등)과 `MessageBox`는 글자를 깨뜨리므로 쓰지 않는다.

## 규칙의 기준
- 우선순위: 사용자 결정 > 참치백과 오레마스 문서(`오레마스_시스템`, `오레마스_세계관_4대자질`, `오레마스_포텐셜일람`과 하위 문서) > 기존 시트 > 실기(Showdown). 문서는 `doku.php?id=<문서명>&do=export_raw`로 받는다.
- 사용자 결정(문서와 다른 부분): 지시 급소 보정은 지시 A +1·AA +2·AAA +3·S +4. PT ①은 통솔 A 또는 육성 A. 통솔 A의 필드형성 권한은 아직 다루지 않는다(이계 《필드》와 지형을 구분하는 구조 유지).
- 포켓몬 입력 스탯은 종족값이며 실능력치와 구분한다.

## 포텐셜
- 포텐셜 도감은 독립된 트리거 조건 / 효과 목록이다. 같은 뜻만 합치고(`PotentialParts.TriggerKey`, `EffectKey`), 합친 결과는 `research/potentials/TRIGGER_MERGES.md`, `EFFECT_MERGES.md`에 남긴다.
- 합치지 않는 차이: 「공격」(공격기술 한정)과 「기술」(변화기술 포함), 주체(자신/아군/상대), 「있을 때」(상태)와 「되었을 때」(사건), 「할 때」와 「했을 때」, 확률·횟수·배율·대상·지속시간·타입·범위·랭크, 「할 수 있다」와 「한다」. 빈 괄호 「」·「??」는 타입 자리 표시로 보존한다.
- 출처와 원문은 내부 자료에 남긴다. 수집 JSON 전체를 컨텍스트로 읽지 말고 이름 검색이나 집계로 필요한 항목만 찾는다. 자연어 자동 분리를 검증된 엔진 규칙으로 취급하지 않는다.

## 검증과 보고
- C# 변경 후 `./desktop/verify.ps1 -OutputPath outputs/candidate`로 빌드와 회귀 검사를 실행한다. UI 변경은 `-Visual`을 붙여 생성된 캡처 이미지를 확인한다.
- EXE가 사용 중이면 `./desktop/build.ps1 -OutputPath outputs/candidate`로 별도 빌드하고 실제 실행 경로를 안내한다.
- 동작 변경은 관련 회귀 검사와 `PROJECT_HANDOFF.md`를 갱신한다. 산출물·임시 파일·비밀은 Git에서 제외한다.
- 사용자 요청 범위와 연결된 수정만 한다. 변경 이유, 테스트 결과, 실행 파일 위치를 짧게 보고한다.
