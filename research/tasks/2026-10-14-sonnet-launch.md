# Sonnet(Claude 서브에이전트) 실행 메모 — 다음 세션의 Claude용

Codex 토큰이 떨어지면 Claude가 아래처럼 Sonnet 서브에이전트로 돌린다(사용자 선호: 지시서 → Sonnet이 구현·검사·병합·push까지 한 번에, Claude는 보고만 리뷰).

## 순서
1. A·C·D 마무리(`2026-10-14-ACD-finish.md`) — Codex가 하던 중. 남았으면 Sonnet으로.
2. E·F·G 동시(각 worktree): `E-battle-continuation`, `F-abaddon-gaps`, `G-chaldea-phase3`.
3. (완료: E·F·G·K) 다음: L·M·H·N 병렬(Codex 명령) → O(특성·계속 효과 해체, 단독) → I(폼 변경). J(HANDOFF 분리·Releases 배포)는 다른 작업이 없을 때 단독.

## 서브에이전트 실행 (Agent 도구: model sonnet, isolation worktree, 백그라운드)
프롬프트 틀:
```
You are implementing a task in the C# WinForms repo AA_battle, working in your own git worktree. First `git fetch origin` and base branch task/<X> on origin/main.
Read AGENTS.md, research/tasks/2026-10-14-parallel-rules.md, research/tasks/2026-10-14-user-decisions.md, then follow the brief research/tasks/2026-10-14-<X>.md exactly.
Other agents run in parallel (list them). Follow the file rules (new files first, shared files = connection lines only, keep both sides on conflicts).
Do the whole job in one pass: implement, verify (`powershell -NoProfile -ExecutionPolicy Bypass -File ./desktop/verify.ps1 -OutputPath outputs/candidate-<X>`, add -Visual for UI and Read the png captures), fetch + rebase onto origin/main, re-verify, `git push origin task/<X>:main` (retry if rejected), push the branch. Do NOT touch the main checkout. Commit messages end with "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>".
Bash heredocs can corrupt backslashes in C# strings — prefer Edit/Write tools. Python stdout needs PYTHONIOENCODING=utf-8. Don't stop for ambiguity; record it under 「사용자 판단 필요」. If budget runs low, merge finished bundles first.
Final answer: the report in Korean in the format of parallel-rules.md.
```
- 사용량 한도로 끊기면 SendMessage로 「중단 지점부터 이어서」 재개.
- 끝나면 Claude가 main을 pull하고 `powershell -NoProfile -ExecutionPolicy Bypass -File ./desktop/build.ps1 -OutputPath outputs/desktop`.
