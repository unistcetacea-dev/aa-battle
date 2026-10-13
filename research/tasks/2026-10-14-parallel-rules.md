# 병렬 작업 공통 규칙 (지시서 A~E)

작성: Claude · 구현: ChatGPT(Codex, 여러 모델 동시) · 기준 커밋: `0427ff7`

지시서 A~D는 **동시에** 진행된다. E는 A~D가 모두 병합된 뒤 단독으로 한다. 모든 지시서는 이 파일과 `AGENTS.md`, `research/tasks/2026-10-14-user-decisions.md`(사용자 결정 — 바꾸지 말 것)를 먼저 읽는다.

## 브랜치와 병합
1. `main`(=`0427ff7` 이상)에서 지시서에 적힌 `task/<이름>` 브랜치를 만든다.
2. 구현 → `powershell -NoProfile -ExecutionPolicy Bypass -File ./desktop/verify.ps1 -OutputPath outputs/candidate-<이름>`(UI 변경이 있으면 `-Visual`) 통과.
3. `git fetch origin` → `git rebase origin/main`. 다른 묶음이 먼저 병합했을 수 있다. **충돌은 양쪽 내용을 모두 살려** 해결한다(한쪽을 지우지 않는다). 특히 공용 파일의 등록 줄은 둘 다 남긴다.
4. verify 다시 실행 → 통과하면 `git push origin task/<이름>:main`(거절되면 3부터 다시) + 브랜치 push.
5. 실패하거나 판단이 필요하면 브랜치만 push하고 보고한다.
- 출력 폴더는 묶음마다 다르게(`outputs/candidate-<이름>`). `outputs/desktop` 빌드는 하지 않는다(Claude가 마지막에 한다).
- 실행 정책 때문에 `./desktop/verify.ps1` 직접 실행이 막히면 위처럼 `powershell -NoProfile -ExecutionPolicy Bypass -File`로 실행한다.

## 파일 규칙 (충돌 방지)
- 새 기능은 **새 파일**에 둔다(지시서에 파일 이름 예시가 있다).
- 공용 파일에는 **연결 줄만** 추가한다(한두 줄, 기존 줄 수정·정렬·재배치 금지):
  `EffectModules.Normalize.cs`, `EffectModules.Classify.cs`, `TriggerModules*.cs`, `ModuleCatalog*.cs`, `PotentialEvents.cs`, `PotentialEvents.Register.cs`, `PotentialEvents.Queue.cs`, `BattleEngine.Turn.cs`, `PROJECT_HANDOFF.md`(절 하나만 끝에 추가).
- 지시서의 「수정 가능 파일」 밖은 고치지 않는다. 필요하면 보고에 적는다.
- 큰 파일(40KB 이상)에 코드를 늘리지 않는다.

## 코드 규칙 (요약 — 자세한 것은 AGENTS.md)
- 대상 `net48`, C# 최신 문법. `System.Text.Json`·`Span`·기본 인터페이스 메서드 금지. JSON은 `JavaScriptSerializer`.
- 엔진(`desktop/AABattle.Engine`)에 WinForms·System.Drawing 금지. 규칙 판단은 엔진에.
- UI는 `PixelControls.cs`의 픽셀 컨트롤만. Windows 기본 `Label`·`TextBox`·`MessageBox` 금지.
- 새 코드는 여러 줄로 읽기 쉽게, 짧은 한국어 주석.
- **포텐셜·특성 이름별 `if` 금지.** 효과는 표준 문장 + 실행기 + `EffectModules.Normalize/Classify` 연결 + `ModuleCatalog` 등록. 등록을 빠뜨리면 `ModuleCoverageTests`가 실패한다.
- 표준 문장을 새로 만들 때 「…시, …」 꼴은 `RuleText.Split`이 앞부분을 트리거로 잘라내므로 피한다.
- Bash heredoc은 C# 문자열의 역슬래시를 깨뜨릴 수 있다. 코드 편집은 편집 도구로.
- Python 출력은 `PYTHONIOENCODING=utf-8`.

## 검사 규칙
- 기능별 `[Test]`/`[TestCase]`를 `desktop/AABattle.Tests`에 추가. 실행 코드 안에 검사를 넣지 않는다.
- 동작 검사만. 단순 존재 확인·개수 고정·문장 전체 비교 금지. UI 리플렉션 검사는 `[Category("UI")]`.
- 기존 검사는 고치지 않는다(지시서가 바뀐 동작을 명시한 경우만 예외 — 보고에 적는다).

## 보고 (한국어, 짧게)
병합된 main 커밋(또는 브랜치) · 파일당 한 줄 이유 · verify 통과 줄 그대로 · 지시서와 다르게 한 점 · 사용자 판단 필요(원문과 함께) · 범위 밖 발견. **애매하면 추측하지 말고 「사용자 판단 필요」에 남기고 다음 항목으로 넘어간다.**
