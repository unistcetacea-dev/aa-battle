# 빌드·배포·구조 (PROJECT_HANDOFF.md에서 옮김)

## 다른 컴퓨터에서 시작

비공개 Git 저장소는 `https://github.com/unistcetacea-dev/aa-battle`이다. 다른 컴퓨터에서 같은 GitHub 계정으로 로그인한 뒤 다음 명령으로 복제한다.

```powershell
git clone https://github.com/unistcetacea-dev/aa-battle.git
cd aa-battle
./desktop/build.ps1
```

저장소 루트에서 Codex 작업을 열고, 새 작업의 첫 메시지에서 `README.md`와 `PROJECT_HANDOFF.md`를 먼저 읽도록 요청한다. 빌드 후 `./desktop/verify.ps1`(NUnit 회귀 검사)를 통과시킨 뒤 작업을 계속한다. 빌드에는 .NET SDK 8 이상(`winget install Microsoft.DotNet.SDK.8`)이 필요하다.

작업을 시작하기 전에는 `git pull`, 마친 뒤에는 커밋과 `git push`를 수행해 두 컴퓨터의 소스를 맞춘다. 같은 파일을 두 컴퓨터에서 동시에 편집하지 않는다.

Codex 공유 링크는 특정 시점의 읽기 전용 대화 스냅샷이다. 향후 결정은 이 문서에도 갱신하여 대화 링크 없이도 개발을 이어갈 수 있게 한다.

## 변경 기록

## 2026-10-12 SDK 프로젝트 / NUnit 회귀 검사
- 빌드를 .NET Framework 4 `csc.exe` 직접 호출에서 SDK 스타일 프로젝트로 바꿨다. 대상은 `net48`(윈도우 기본 탑재)이라 실행에 추가 설치가 필요 없고, 빌드에는 .NET SDK 8 이상이 필요하다. `LangVersion=latest`로 최신 C# 문법을 쓸 수 있다.
- 구조: `desktop/AABattle.sln` → `AABattle.Core`(desktop/*.cs 전체와 내장 리소스를 담은 라이브러리), `AABattle`·`AABattleDataEditor`(진입점만 있는 실행 파일), `AABattle.Tests`(NUnit). 공통 설정은 `desktop/Directory.Build.props`. 새 `.cs` 파일은 `desktop/`에 두면 자동 포함된다.
- 회귀 검사는 `AABattle.Tests`로 옮겼다. 한 덩어리였던 `MechanicsTests.Run`(338건)은 기능별 `[Test]` 20개와 UI 검사 15개(`[Category("UI")]`, `MechanicsUiTests`)로 나눴다. 실패하면 해당 테스트 이름과 검사 문구가 함께 표시된다. 전체 45개 테스트, 약 10초.
- 두 EXE의 `--self-test` 인자는 없앴다. 편집기 자체 검사는 `EditorTests`로 옮겼다. `Engine.Test`가 `CatalogTests`를 중복 실행하던 것(약 12초)을 없앴다.
- 깨지기 쉬운 고정 개수 검사(내장 정의 4151개, 역할 42개·범용 6개·선 4개·상대 타입 17개 등)는 존재·내용 검사로 완화했다. 64행과 같은 수면 상한 검사가 `ChargeAndTypeSwap`에 중복돼 있던 것을 지웠다.
- 아직 실행 코드 안에 남은 작은 `Test()` 메서드(`EffectBuilder`, `EntryTriggers`, `CatalogUI` 등)는 `SuiteTests`에서 호출한다. 이후 작업에서 점진적으로 `AABattle.Tests`로 옮긴다.
- `verify.ps1`: 빌드 → `dotnet test` → (`-Visual`) 화면 캡처. `-Fast`는 UI 검사를 건너뛴다.

## 2026-10-12 엔진/UI 분리와 파일 분할
- `AABattle.Core`를 `AABattle.Engine`(계산·규칙·데이터 모델·내장 리소스, UI 참조 없음)과 `AABattle.UI`(WinForms 화면)로 나눴다. UI 파일 안에 있던 순수 로직(`EffectNormalizer`·`EffectStructures`·`EditorTemplate`·`EditorBridge`·`PotentialLibrary`·`BattleStateReport`·`RuleBook`·`TurnSelection`)과 저장 데이터 모델(`EditorProject`·`TrainerRecord`·`PokemonRecord`·`TeamRecord` 등)은 엔진으로 옮겼다. 내용은 바꾸지 않았다.
- 큰 파일을 클래스 단위로 나눴다. 엔진: `BattleData`·`TrainerAccessRules`·`TriggerStructures`·`RuntimeTriggers`·`PriorityEffectRuntime`·`ConditionState`·`MechanicsStates`(`Mechanics`는 `partial`). UI: `Theme`·`BattleWidgets`·`BattleDialogs`. `CatalogTests`는 `AABattle.Tests`로 옮겼다.
- 검증: `verify.ps1 -Visual` 통과(테스트 45개), 배틀·편집기 캡처 동일.

## 2026-10-14 큰 엔진 파일 분리·강제교대 행동 취소·인형사

- 지시서 A(`research/tasks/2026-10-14-engine-file-split.md`), 기준 main `01749c5`, 브랜치 `task/engine-file-split`. 후속 지시서 B(모듈 스키마 편집기)는 이번 작업에 포함하지 않았다.
- 엔진·UI의 모든 `.cs` 파일을 다시 확인했다. 40,000바이트 초과는 아래 원본 세 파일뿐이었으며 UI에는 없었다. 구조 분리 뒤에도 전체 파일이 40,000바이트 이하다. Mechanics.cs는 기준을 넘지 않아 수정하지 않았다.
- 압축된 문장을 펼치고 책임별 partial로 이동했다. 원래 공개 클래스·메서드, 필드 초기화 순서, 기본 실행기 등록 순서는 유지했다. Roslyn으로 이동 전후 메서드·필드 1,862개의 코드 토큰 SHA-256을 비교하여 일치를 확인했다(서명 식별자의 공백은 제외). 기존 검사는 수정하지 않았고 세 파일을 각각 옮긴 뒤 전체 1,191개 검사를 실행해 모두 통과했다.

| 원본(분리 전 바이트) | 최종 파일 | 바이트 | 책임 |
| --- | --- | ---: | --- |
| BattleEngine.cs (57,732) | BattleEngine.cs | 1,469 | Resolve 진입점·문맥 복원 |
| 〃 | BattleEngine.Models.cs | 7,522 | 규칙·명령·결과·진영 모델 |
| 〃 | BattleEngine.Damage.cs | 6,586 | 능력치·일반/예약 대미지 |
| 〃 | BattleEngine.Turn.cs | 36,667 | 턴 진행·행동 루프 |
| 〃 | BattleEngine.Switch.cs | 9,521 | 교대·퇴장·등장 |
| 〃 | BattleEngine.Orders.cs | 2,907 | 지령·트레이너 지정 효과 |
| 〃 | BattleEngine.Checks.cs | 12,425 | 기존 자체 검사 |
| EffectModules.cs (54,850) | EffectModules.cs | 7,089 | 공통 어휘·정규화 진입점·캐시 |
| 〃 | MoveCondition.cs | 11,428 | 기술 조건 |
| 〃 | EffectModels.cs | 2,188 | 효과 모델 |
| 〃 | EffectModules.Normalize.cs | 14,454 | 수집 문장 표준화 |
| 〃 | EffectModules.Scalar.cs | 3,833 | 수치 배율 |
| 〃 | EffectModules.Stats.cs | 12,508 | 종족치·랭크·HP 비율·치유 |
| 〃 | EffectModules.Place.cs | 7,183 | 장소·설치물·교대·면역 |
| 〃 | EffectModules.Classify.cs | 5,722 | 문장 분류 |
| PotentialEvents.cs (51,737) | PotentialEvents.cs | 11,014 | 사건·실행기 모델·사용 이력 |
| 〃 | PotentialEvents.Queue.cs | 19,599 | 큐·실행 계획·선택 발동 |
| 〃 | PotentialEvents.Register.cs | 18,021 | 기본 실행기 등록 |
| 〃 | PotentialEvents.Calculations.cs | 12,156 | 랭크·배율·종족치·HP 실행 |

- 강제교대 버그: 기술을 고른 원래 Fighter를 보존하고 일반 행동 칸에서 같은 참조인지 확인한다. 교대해 들어온 포켓몬은 이전 선택 기술을 사용하지 않는다. 추가행동은 기존 소유자 판정 경로를 그대로 쓴다. [오레마스 시스템의 추가효과 풀죽음 절](https://wiki.tunaground.net/doku.php?id=오레마스_시스템&do=export_raw)은 강제교대 후 상대의 원래 행동권이 없어진다고 설명한다. [Showdown battle-actions.ts의 switchIn](https://github.com/smogon/pokemon-showdown/blob/master/sim/battle-actions.ts)도 이전 포켓몬의 선택 행동을 cancelAction으로 취소한다. 조기 강제교대 후 들어온 포켓몬이 공격하지 않는 회귀 검사 1개를 추가했다.
- 사용자 확정 인형사: ‘개입’은 자신의 퇴장·등장 양쪽이며 기존 Normal/Return/Pivot/Potential 임의교대만 해당한다. 표준 문장 `자신이 나가거나 들어오는 임의교대의 「능력변화」를 인계할 수 있다.`를 BatonModules가 해석한다. AbilityModules는 원문을 이 문장으로 바꿀 뿐 이름별 실행 분기는 없다. 같은 문장인 일반 포텐셜도 같은 경로를 쓴다. 인계 항목은 기존 능력 랭크·명중·회피·C이며 상태변화는 이 문장으로 인계하지 않는다.
- 퇴장 측이 거부하면 등장 측의 인계 권한도 확인한다. 하나가 수락하면 한 번만 인계한다. 등장 측은 장소에 나온 것으로 가정한 복사본에서 현재 특성·화학변화가스 무효화를 확인하며 실제 대기 데이터는 수정하지 않는다. 강제교대·빈사 교대에는 인형사 인계가 없다. 프롬프트가 없으면 자동 수락하지 않는다.
- 기존 OptionalEffectPrompt 화면은 데이터 해석 전용이었으므로 BattleApp에 이 양방향 인계 문장만 선택 확인하는 연결을 추가했다. 기존 GameDialog를 사용하고 분석 이력·분석 창을 건드리지 않는다. 〔인형사〕 발동 로그와 특성 발동 사건도 연결했다. 퇴장 측 사건은 인계 값 저장 전, 등장 측 사건은 실제 등장·인계 적용 후에 발생한다. 별도 리뷰에서 사건 누락을 재현한 뒤 수정했으며 반응 포텐셜이 실제 실행되는 네 경우를 검사했다.
- 검사: EngineFileSplitTests 18개(강제교대 1, 인형사 양쪽 수락/거부·임의교대 범위·강제/빈사 제외·가스 무효화·발동 반응·화면 선택). 새 동작 검사는 실패를 확인한 뒤 통과시켰다. 상태 도감 집계는 자동 564/부분 자동 1/수동 22, 인형사 보류는 해결했다.
- 최종 verify: `./desktop/verify.ps1 -Visual -OutputPath outputs/candidate` — 빌드 경고/오류 0, 전체 회귀 1,197개 통과. 인계 선택 캡처는 `outputs/candidate/puppet-choice.png`, 배틀·도감·진행 캡처도 확인했다. 최신 origin/main 위로 rebase 뒤 같은 명령을 재실행하고 ff 병합·push한다.
- 범위 밖: 다른 선택형 효과의 기존 데이터 해석 전용 UI 연결은 일반화하지 않았다. 모듈 스키마 편집기와 폼 변경 등 기존 보류는 후속 작업이다.

## 2026-10-14 배포본 자동 업데이트 (Git 저장소에서 받기)
- **폐기됨**: 이 절의 `version.txt`·`release/windows` EXE 방식은 GitHub Releases zip 방식으로 대체되었다(뒤쪽 「배포」 항목이 현재 기준).
- 배포 위치는 저장소의 `release/windows/`. `version.txt`(첫 줄 버전, 다음 줄부터 변경 내용)가 기준이고, `desktop/build.ps1`이 이 버전을 어셈블리 버전으로 넣는다.
- 시작 화면(`StartFormUpdate.cs`)이 백그라운드로 raw URL의 `version.txt`를 받아 비교하고(실패는 조용히 무시), 새 버전이면 오른쪽 아래에 「새 버전 X — 업데이트」 버튼을 낸다. 인자를 준 실행(검사·캡처)은 네트워크를 쓰지 않는다.
- 버튼: `Updater.cs`가 EXE 2개를 임시 폴더로 받아(크기·PE 헤더 확인, Zone.Identifier 제거) 교체 배치 파일을 띄우고 종료한다. 배치 파일이 종료 대기 → `*.old.exe`로 보관 → 이동 → 재실행하고, 다음 실행 때 `*.old.exe`를 지운다. 저장소·URL 상수와 순수 로직(버전 비교·해석·스크립트 생성)은 `UpdateLogic.cs`.
- 배포는 `desktop/publish.ps1`(빌드 → 복사 → 버전 올림 → README 날짜). 커밋·push는 사람이 `release/windows`를 확인한 뒤 한다. 검사는 `UpdaterTests`.

## 사용자 결정

원본: `research/tasks/2026-10-14-user-decisions.md`

### 구조·운영 (2026-10-14 대화) (일부)
- **PROJECT_HANDOFF.md 분리**: **주제별**(기술·특성·포텐셜·소지품·UI·편집기·배틀 진행 등)로 나누고 본문은 색인·현재 규칙 요약만.
- **배포**: 시간이 걸려도 됨. 요구: (1) **EXE가 Git 기록에 계속 쌓이지 않는 구조**, (2) 받는 사람은 **zip 하나 받아 압축을 푸는 방식**. → GitHub Releases에 zip 첨부(저장소 기록에 안 쌓임), `release/windows` EXE는 저장소에서 제거, 업데이트 기능은 최신 Release의 zip을 받아 교체하도록 변경(사용자 확인 완료 — 이 방식으로 진행).

## 2026-10-14 배포를 GitHub Releases zip으로 (task/J-handoff-release)
- `desktop/publish.ps1`: 빌드(`build.ps1 -Version`) → `outputs/publish/AABattle-<버전>.zip`(EXE 2개 + 함께 쓰는 DLL 2개(UI·Engine, .config 포함) + `desktop/dist-readme.txt` 기반 README. 업데이트는 EXE·DLL 4개를 교체) → `gh release create v<버전>`. `gh`가 없거나 `-NoUpload`면 zip만 만들고 수동 업로드 안내를 출력한다. 버전 기본값은 오늘 날짜 + 같은 날 번호(로컬 태그·Release 태그 참고). `release/windows`로 복사하지 않는다.
- 업데이트(`Updater`/`UpdateLogic`): `api.github.com/repos/unistcetacea-dev/aa-battle/releases/latest`의 `tag_name`(앞의 v 제거)을 내 버전과 비교, `AABattle-*.zip` 자산을 받아 임시 폴더에 풀고(루트 또는 폴더 한 겹 아래 고정 이름만) PE 검사 후 기존 교체 스크립트로 교체한다. `version.txt` 방식은 제거했다. 프로그램 버전은 빌드 때 `-p:Version`으로 들어간다.
- `release/windows`의 EXE·`version.txt`는 저장소에서 삭제했고 README만 남겼다(과거 기록의 EXE는 재작성하지 않는다).
- 첫 배포: `gh` 설치·`gh auth login` 후 `./desktop/publish.ps1 -Notes "변경 내용"`, 또는 `-NoUpload`로 zip을 만들어 Releases 웹 화면에서 직접 올린다. 첫 Release가 올라가기 전에는 이미 배포된 옛 EXE가 새 방식을 모른다(옛 EXE는 raw `version.txt`를 본다).
