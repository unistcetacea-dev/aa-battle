# 지시서: 배포본 자동 업데이트 (Git 저장소에서 받기)

작성: Claude · 구현: Claude Sonnet(서브에이전트, 별도 worktree) · 기준: 최신 `origin/main` · 브랜치: `task/git-updater`
같은 시간에 다른 작업(`task/potential-editor-window` — `DataEditor.cs`·`CatalogUI.cs`·`RetroEditor.cs`·`ModuleBuilder.cs` 등 편집기 UI, `task/move-misc` — 엔진 기술)이 병렬로 돈다. 새 코드는 **새 파일**에 두고, 기존 UI 파일에는 연결 줄만 넣는다.

먼저 `AGENTS.md`(UI 규칙: 픽셀 컨트롤·도트 폰트, `MessageBox` 금지), `desktop/build.ps1`, `release/windows/README.txt`, `desktop/AABattle.UI/StartUI.cs`를 읽는다.

## 사용자 요구
배포한 뒤에 **다른 사람이 Git 저장소(GitHub `unistcetacea-dev/aa-battle`, 공개)에서 업데이트를 받을 수 있게** 한다. 받는 사람 PC에는 git·.NET SDK가 없다고 가정한다(EXE만 있음).

## 설계
1. **배포 위치 = 저장소의 `release/windows/`** (이미 Git에 EXE가 올라가 있다). GitHub Releases는 쓰지 않는다.
   - `release/windows/version.txt` 추가: 한 줄 버전(예: `2026.10.14.1`) + 다음 줄부터 짧은 변경 내용(선택).
   - 프로그램은 자기 버전을 어셈블리 정보(빌드 시 `version.txt`에서 넣거나, `build.ps1`이 `-Version` 인자로 넣음)로 안다.
2. **확인**: 시작 화면(`StartUI`)에서 백그라운드로 `https://raw.githubusercontent.com/unistcetacea-dev/aa-battle/main/release/windows/version.txt`를 받아 비교(.NET Framework 4.8: `HttpWebRequest`/`WebClient`, TLS 1.2 명시). 네트워크 실패는 조용히 무시(시작을 막지 않음). 새 버전이면 시작 화면에 픽셀 컨트롤로 「새 버전 X — 업데이트」 버튼/알림.
3. **받기·교체**: 버튼을 누르면 `release/windows/AABattle.exe`·`AABattleDataEditor.exe`를 raw URL에서 임시 폴더로 받고(진행 표시), 크기·PE 헤더 정도 확인 후, 실행 중인 EXE는 직접 덮어쓸 수 없으므로 작은 교체 스크립트(배치 파일 또는 `cmd /c` — 프로세스 종료 대기 → 이동 → 재실행)를 띄우고 프로그램을 종료한다. 이전 EXE는 `*.old.exe`로 한 번만 남기고 다음 실행 때 지운다. 사용자 데이터 파일(팀·저장 데이터)은 건드리지 않는다.
   - 다운로드 경고(Mark of the Web)는 받은 파일의 `Zone.Identifier` 스트림을 지운다.
   - 저장소 경로·URL은 상수 한 곳(새 파일)에 둔다.
4. **배포 절차(개발 쪽)**: `desktop/publish.ps1`(새) — `build.ps1`로 빌드 → `release/windows/`에 EXE 복사 → `version.txt` 버전 올림(날짜 + 같은 날 번호) → README의 「빌드 기준」 날짜 갱신. 커밋·push는 하지 않는다(사람/Claude가 확인 후 커밋). `AGENTS.md`의 「검증과 보고」에 배포 한 줄 추가: 「배포는 `desktop/publish.ps1` 후 `release/windows`를 커밋·push한다(받는 쪽 프로그램이 이 폴더에서 업데이트를 받는다).」
5. 엔진에는 넣지 않는다(UI 쪽 새 파일, 예: `desktop/AABattle.UI/Updater.cs`). 버전 비교·`version.txt` 해석 같은 순수 로직은 테스트할 수 있게 UI 참조 없는 작은 클래스로.

## 검사
- 버전 비교·`version.txt` 해석 `[TestCase]`(같음·높음·낮음·형식 오류·빈 파일).
- 네트워크를 쓰는 검사는 넣지 않는다. 교체 스크립트 문자열 생성은 경로에 공백·한글이 있어도 따옴표가 맞는지 검사.
- `powershell -NoProfile -ExecutionPolicy Bypass -File ./desktop/verify.ps1 -Visual -OutputPath outputs/candidate-updater`, 시작 화면 캡처에서 알림이 깨지지 않는지 확인(검사 중에는 네트워크 확인을 끄는 스위치를 두고 「새 버전」 상태를 강제로 그려 캡처).
- 수동 확인: 로컬에서 `publish.ps1`를 실행해 `release/windows`가 갱신되는지만 보고, 그 변경은 **커밋하지 않는다**(되돌림).

## 작업 방식
구현·검사·문서·병합까지 한 번에: verify 통과 → `git fetch origin` → 최신 `origin/main` 위로 rebase(충돌은 양쪽 내용을 모두 살려 해결) → verify 재실행 → `git push origin task/git-updater:main`(거절되면 다시 fetch·rebase·verify) + 브랜치 push. `outputs/desktop` 빌드는 하지 않는다. 원래 체크아웃(`C:\Users\115\Downloads\AA_battle`)은 건드리지 않는다. 커밋 끝에 `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. `PROJECT_HANDOFF.md`에는 절 하나만.

## 보고 (한국어, AGENTS.md 형식)
병합된 main 커밋 · 파일당 한 줄 이유 · verify 통과 줄 · 업데이트 흐름 설명(확인 → 받기 → 교체) · 배포 절차 · 캡처 확인 결과 · 지시서와 다르게 한 점 · 범위 밖 발견(예: EXE를 Git에 계속 올릴 때 저장소 크기).
