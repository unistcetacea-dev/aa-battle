# 지시서: 칼데아 2차 — 확률 상시 포텐셜 발동 · 이름 지정 포텐셜 무효화 · 「~할 수 있다」 일반 처리

작성: Claude · 구현: Claude Sonnet(서브에이전트) · 기준: `1071ea4` · 브랜치: `task/chaldea-phase2`

먼저 `AGENTS.md`(「포텐셜」 절), `PROJECT_HANDOFF.md`의 칼데아·전용 유형·유대 절, `research/samples/chaldea/MODULE_GAPS.md`(상단 제안 + 해당 묶음), `research/samples/abaddon/MODULE_GAPS.md`, `research/potentials/EXCLUSIVE_TYPES.md`를 읽는다. 판정 도구 `PotentialJudge`·`ChaldeaJudgeTool`(Explicit 검사)을 그대로 쓴다.

## 할 일 (순서대로)
1. **버그: 확률이 붙은 상태·상시 포텐셜이 실제로 발동하지 않는다.** 『대○저격』『대○박격』『대○반격』『대○추격』『선』류처럼 「상태 조건(적진에 X가 있을 때 등) + 확률(저확률·중확률·드물게·N할) + 효과」인 포텐셜은 표준 문장·실행기는 있지만 굴릴 사건이 없어 돌지 않는다(`ContinuousSentences`가 확률 접두어 문장을 건너뜀).
   - 효과가 걸리는 시점(필중 = 기술 명중 판정, 박격 = 대미지 계산, 반격 = 턴 마지막 추가행동, 추격 부여 = 기술 사용, 선 = 행동 순서 결정, 대○회피/내성은 확률 없음)에 **그때마다 확률을 굴리는** 공용 방법을 만든다(효과 종류별 판정 지점 표 한 곳). 이름별 분기 금지.
   - 회피·내성·격 템플릿(`EditorTemplate.CounterOptions`)과 샘플 파티(`samples/abaddon-sylph.aabdata.json`)에 들어 있는 것이 실제로 발동하는지 검사(난수 고정: 성공·실패 두 갈래, 상태 조건 불충족 시 굴리지 않음).
   - 판정 도구가 「표준 문장·실행기 있음」만으로 ✅를 주지 않도록, 확률+상시 꼴은 실제 판정 지점이 연결됐는지도 보게 고친다.
2. **이름 지정 포텐셜 무효화** (칼데아 84행·20명, 아바돈 A3): `상대의 『투쟁본능』을 무효화한다` 꼴. MODULE_GAPS의 제안 문장대로 모듈(표준 문장 + 실행기 + Normalize/Classify + `ModuleCatalog`) — 무효화된 포텐셜은 발동하지 않음(트리거·상시 모두). 프라이멀 헌트·와일드 헌트(아바돈 묶음 B)도 이걸로 동작하게.
3. **「~할 수 있다」 선택 효과 일반 처리** (칼데아 30행): MODULE_GAPS 제안대로 큐에서 「할 수 있다」를 직설형으로 바꿔 실행기를 다시 찾고, 기존 선택 확인(`OptionalEffectPrompt`)을 거친다. 이미 개별 처리된 「할 수 있다」 모듈과 이중 확인이 생기지 않게.
4. 시간이 남으면 칼데아 MODULE_GAPS의 「기능확장·추가행동 변형」(45행) 중 기존 `FunctionExtensions`로 바로 되는 것.
5. 끝나면 `ChaldeaJudgeTool`을 다시 돌려 `POTENTIALS.tsv`·`MODULE_GAPS.md` 집계를 갱신한다.

## 제외 (사용자 답 대기 — 손대지 말 것)
기합 확률 공식, 「상대의 공격 이외의 대미지를 무시한다」 해석, 천부의 재능·역할 임명, 새 상태(번개구름·아이스번·도우미), 뜨거운 유대 「++」, 전용 유형(영격·마검·버서크·범프업·페이스메이커·메가진화·스토킹·삼점방호), 전투속행(대미지 지연).

## 검사
- 묶음마다 동작 `[TestCase]`(문장 전체 비교·개수 고정 금지). `ModuleCoverageTests` 통과.
- `powershell -NoProfile -ExecutionPolicy Bypass -File ./desktop/verify.ps1 -OutputPath outputs/candidate`.

## 작업 방식
구현·검사·문서·병합까지 한 번에: verify 통과 → fetch·최신 `origin/main` 위로 rebase → verify 재실행 → `main` fast-forward 병합·push + 브랜치 push → `powershell -NoProfile -ExecutionPolicy Bypass -File ./desktop/build.ps1 -OutputPath outputs/desktop`(잠겨 있으면 보고). 묶음마다 커밋. 커밋 끝에 `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. `PROJECT_HANDOFF.md`에는 절 하나.

## 보고 (한국어, AGENTS.md 형식)
병합된 main 커밋 · 파일당 한 줄 이유 · verify 통과 줄 · 묶음별 결과(전후 행 수) · 확률 판정 지점 표 · 새 모듈 표 · 사용자 판단 필요 · 범위 밖 발견.
