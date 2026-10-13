# 지시서: 배틀 화면 성별 입력 · 트레이너 쪽 옛 전용 포텐셜 삭제

작성: Claude · 구현: Claude Sonnet(서브에이전트, 별도 worktree) · 기준: 최신 `origin/main`(`b1b81a7` 이후) · 브랜치: `task/gender-battle-legacy`
병렬 작업: 샘플 파티(`task/abaddon-sample-party`, 원래 체크아웃 — `DataEditor.cs`·샘플 데이터), 칼데아 포텐셜(`task/chaldea-potentials`, 엔진 모듈). 변경은 작게, 연결 줄 위주로.

먼저 `AGENTS.md`, `PROJECT_HANDOFF.md`의 최근 절 「포텐셜 편집 창 분리 · 성별 · 전용 포텐셜」을 읽는다.

## 사용자 결정
1. **배틀 화면에도 성별 입력**: 배틀의 포켓몬 편집 화면(`FighterForm`, 실제 파일 확인)에 성별(수컷/암컷/없음) 픽셀 선택 칸을 넣는다. 아인종 표시가 있으면 그 옆. 내장 도감에서 고른 포켓몬은 기본 「없음」. 팀 저장·불러오기(팀 보관함 형식)에도 성별이 왕복되는지 확인·연결.
2. **트레이너 쪽 옛 「전용」 포텐셜은 날린다**: 불러올 때 트레이너 포텐셜 중 슬롯 「전용」인 행은 버린다(다음 저장 때 사라짐). 그와 함께 남은 호환 코드를 지운다 — `TeamRules`의 트레이너 쪽 전용 합산, 편집 창·목록의 「포켓몬 편집으로 옮기세요」 안내, 트레이너 쪽 전용 관련 분기(데이터 해석·시트 출력 `TrainerText`의 【전용 포텐셜】 등은 포켓몬 쪽 전용을 보여 주도록 바꾸거나, 의미가 없으면 지운다 — 보고). `TeamRules`의 자체 검사(`trainer.potentials.Add(... slot="전용")` 류)도 포켓몬 쪽으로 바꾼다.

## 검사
- 성별: 배틀 화면에서 고른 성별이 팀 저장·불러오기 왕복, 헤롱헤롱 판정까지 이어짐(한 가지).
- 옛 전용: 트레이너 「전용」 행이 있는 데이터를 불러오면 사라지고, 개수 제한은 포켓몬 쪽 전용만 셈.
- `powershell -NoProfile -ExecutionPolicy Bypass -File ./desktop/verify.ps1 -Visual -OutputPath outputs/candidate-gender`, 배틀 포켓몬 편집 화면 캡처 확인(없으면 캡처 추가).

## 작업 방식
구현·검사·문서·병합까지 한 번에: verify 통과 → fetch·최신 `origin/main` 위로 rebase(충돌은 양쪽 살려 해결) → verify 재실행 → `git push origin task/gender-battle-legacy:main`(거절되면 다시) + 브랜치 push. `outputs/desktop` 빌드는 하지 않는다. 원래 체크아웃은 건드리지 않는다. 커밋 끝에 `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. `PROJECT_HANDOFF.md`의 해당 절을 고친다.

## 보고 (한국어, AGENTS.md 형식)
병합된 main 커밋 · 파일당 한 줄 이유 · verify 통과 줄 · 지운 호환 코드 목록 · 캡처 확인 · 지시서와 다르게 한 점 · 범위 밖 발견.
