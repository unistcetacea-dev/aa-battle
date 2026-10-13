# 지시서: 오레마스 전용 포텐셜 유형 — 템플릿 + 구현

작성: Claude · 구현: Claude Sonnet(서브에이전트) · 기준: 최신 `origin/main`(배틀 성별·옛 전용 삭제 작업 병합 뒤) · 브랜치: `task/exclusive-templates`
병렬 작업: 칼데아 포텐셜(`task/chaldea-potentials`, 엔진 모듈), 샘플 파티(`task/abaddon-sample-party`). 새 모듈은 새 파일, 공용 파일은 연결 줄만.

먼저 `AGENTS.md`(「포텐셜」 절), `PROJECT_HANDOFF.md`의 「포텐셜 편집 창 분리 · 성별 · 전용 포텐셜」 절과 그 뒤 절, `desktop/AABattle.UI/PotentialEditWindow.cs`(종류·이름 선택, `ApplyTemplate`), `desktop/AABattle.Engine/EditorTemplates.cs`(`FixedOptions`·`CounterOptions`·`RolePotentialOption`)를 읽는다.

## 자료
참치백과 「오레마스_전용포텐셜」 원문: `research/potentials/oremas-exclusive-potentials.txt`(약 60개 유형: 필중추미·강화추미·확장추미·삼각효과·대삼각·확장삼각·마검·선행유턴·귀신유턴·버서크·영격·페이스메이커·텔레포터·스토킹·역선회·강화관통·강행돌파·추가반동·메가진화·삼점방호·강체술·고속설치·범프업·속사·아라가미탄 …). 원본 `https://wiki.tunaground.net/doku.php?id=오레마스_전용포텐셜`.

## 사용자 요구
1. **편집 템플릿**: 포켓몬 편집에서 슬롯 「전용」 포텐셜을 편집할 때, 이름 선택에 **「범용」**을 고르면 위 유형 목록이 나오고, 유형 이름을 누르면 그 유형의 **사용 제한(1/시·선행 등)·트리거·효과가 로드**된다.
   - **포텐셜 이름은 유형 이름이 아니다**(사용자: 「이름이 막 『삼각』 이러는 건 아니야」). 유형을 골라도 이름 칸은 바꾸지 않는다(비어 있으면 비워 둔 채, 사용자가 직접 입력). 유형은 템플릿 선택일 뿐 저장 데이터에 남기지 않는다(필요하면 `raw`나 메모 칸에만).
   - 유형 문장의 자리 표시(「○」 타입, 「○○」 기술·설치물, 「기능확장：○○」)는 빈칸 채우기(`ModuleBuilder`의 슬롯)로 이어지게 하거나, 로드 후 효과 칸에서 바로 고칠 수 있게 표시한다. 자리 표시가 남은 채로 확인하면 「채워야 할 칸」 안내.
   - 설명·해설 문장(「어태커에게 가장 흔한…」, 「(※…)」 주석, 습득 조건 「공or특공이 AA 이상」)은 효과에 넣지 않는다. 습득 조건은 템플릿 설명(편집 창 도움말 줄)으로만 보여 준다.
   - 「일반적인 형태일 뿐 변형이 많다」 — 로드 뒤 자유롭게 고칠 수 있어야 한다.
2. **구현**: 각 유형의 효과 문장이 엔진에서 실제로 동작하게 한다.
   - 먼저 엔진 코드로 판정(`TriggerModules`/`EffectModules` Normalize·Classify, 실행기 존재): ✅ 동작 / 🟡 정규화로 해결 / 🔴 새 모듈 필요 / ❓ 사용자 판단. 표로 `research/potentials/EXCLUSIVE_TYPES.md`에.
   - 🟡 전부, 🔴는 새 모듈(표준 문장 + 실행기 + Normalize/Classify + `ModuleCatalog` 등록 + 검사)로 구현한다. 이름별 `if` 금지. 엔진 구조를 크게 바꿔야 하는 것(예: 메가진화 = 폼 변경, 아직 범위 밖)은 구현하지 말고 표에 사유·제안 문장을 남긴다.
   - 칼데아 작업이 같은 기능의 모듈을 먼저 병합했을 수 있다 — rebase 뒤 중복 모듈이 생기면 하나로 합친다(표준 문장이 같으면 같은 모듈).
   - 원칙: 「할 수 있다」 = 선택 확인, 트레이너 포텐셜 방호/관통 규칙(전용은 트레이너 포텐셜), 기능확장 = `FunctionExtensions`, 추격·유턴 부여는 기존 `PursuitModules`/유턴 부여 모듈.

## 검사
- 템플릿: 「범용」 → 유형 선택 → 사용 제한·트리거·효과가 채워지고 이름은 그대로(UI 검사 `[Category("UI")]`). 템플릿으로 채운 문장이 모두 모듈로 읽히거나(✅/🟡) 표에 🔴로 등록된 것임을 확인하는 검사.
- 새 모듈마다 동작 `[TestCase]` 묶음.
- `powershell -NoProfile -ExecutionPolicy Bypass -File ./desktop/verify.ps1 -Visual -OutputPath outputs/candidate-exclusive`, 편집 창에서 범용 유형을 고른 캡처 추가·확인.

## 작업 방식
구현·검사·문서·병합까지 한 번에: verify 통과 → fetch·최신 `origin/main` 위로 rebase(충돌은 양쪽 살려 해결) → verify 재실행 → `git push origin task/exclusive-templates:main`(거절되면 다시) + 브랜치 push. `outputs/desktop` 빌드는 하지 않는다. 커밋 끝에 `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. `PROJECT_HANDOFF.md`에는 절 하나. 원문 파일 `research/potentials/oremas-exclusive-potentials.txt`도 커밋한다.

## 보고 (한국어, AGENTS.md 형식)
병합된 main 커밋 · 파일당 한 줄 이유 · verify 통과 줄 · 유형별 판정 집계(전후) · 새 모듈 표 · 구현하지 않은 유형과 사유 · 캡처 확인 · 사용자 판단 필요(원문) · 범위 밖 발견.
