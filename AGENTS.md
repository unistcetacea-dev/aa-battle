# AA BATTLE 작업 안내

Windows C# WinForms 프로그램이다. 현재 상태는 `PROJECT_HANDOFF.md`, 빌드는 `desktop/build.ps1`을 참고한다. 웹 프로토타입을 주 배포 대상으로 착각하지 않는다.

- UI: `BattleApp.cs`, `DataEditor.cs`, `CatalogUI.cs`. 계산: `BattleEngine.cs`, `Mechanics.cs`.
- 계산 규칙은 기존 시트 기준을 유지한다. 포켓몬 입력 스탯은 종족값이며 실능력치와 구분한다.
- 시스템과 도감은 내장 Pokemon BW 폰트, AA는 HeadKasen을 사용한다. 흑백 도트 UI를 유지한다.
- 포텐셜 도감은 독립된 트리거 조건 / 효과 목록이다. 중복은 정규화하여 합치되 확률·횟수·배율·대상·지속시간의 차이는 보존한다. 출처와 원문은 내부 자료에 남긴다.
- 수집 JSON 전체를 컨텍스트로 읽지 말고 이름 검색이나 집계로 필요한 항목만 찾는다. 자연어 자동 분리를 검증된 엔진 규칙으로 취급하지 않는다.
- C# 변경 후 `./desktop/verify.ps1`로 빌드와 회귀 검사를 실행한다. UI 변경은 `./desktop/verify.ps1 -Visual`로 생성한 도감 이미지를 확인한다.
- EXE가 사용 중이면 `./desktop/build.ps1 -OutputPath outputs/candidate`로 별도 빌드하고 실제 실행 경로를 안내한다.
- 동작 변경은 관련 회귀 검사와 인계 문서를 갱신한다. 산출물·임시 파일·비밀은 Git에서 제외한다.
- 사용자 요청 범위와 연결된 수정만 한다. 변경 이유, 테스트 결과, 실행 파일 위치를 짧게 보고한다.
