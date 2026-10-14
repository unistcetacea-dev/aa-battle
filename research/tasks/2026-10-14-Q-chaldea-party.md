# 지시서 Q: 샘플 파티 — 피니스 칼데아

브랜치 `task/Q-chaldea-party` · 출력 `outputs/candidate-Q` · `-Visual` · 먼저 `AGENTS.md`, `PROJECT_HANDOFF.md`(목차) → `docs/handoff/samples.md`(실프 샘플 작업 방식), `ui.md`(파티 구조 H: 다수 소속·출전 선택), `potentials.md`·`trainer.md`의 「사용자 결정」, 실프 지시서 `research/tasks/2026-10-14-abaddon-sample-party.md`(같은 방식)를 읽는다.

## 자료
`C:\Users\115\Downloads\AA_battle\research\samples\chaldea\*.txt`(28명 시트 원문 + 슈트 2개, Git에 없음 — 원래 체크아웃에서 읽기만; 없으면 `https://wiki.tunaground.net/doku.php?id=아바돈_<이름>&do=export_raw`로 받는다). `research/samples/chaldea/POTENTIALS.tsv`(추출·판정표)를 최대한 재사용한다. `<html>…</html>` 아스키아트는 스크립트로 다룬다(원문을 컨텍스트에 통째로 읽지 않음).

## 사용자 결정
- 파티 하나(「피니스 칼데아」)에 **트레이너 유키나리** + **포켓몬 28명 전원**을 후보로 넣는다(H의 다수 소속 구조). 기본 출전은 6명(임의로 고르고 보고).
- **유키나리의 고유 포텐셜(트레이너 고유)은 넣지 않는다.** 다른 시트에 자질 평가가 있는 인물은 트레이너로도 추가하고 그 고유 포텐셜은 넣는다(목록 보고).
- **체력 종족치는 모두 100**(연재작이라 비공개). 나머지 능력치는 시트 등급을 실프와 같은 환산(엔진 `SpeciesStatRanks` 범위 가운데 값)으로.
- 슬롯·포텐셜·기술·특성·소지품은 **마지막 성장 단계** 기준. 「칼데아 보유 슈트」「판매한 슈트」 문서는 이번에 넣지 않는다.
- 전용 포텐셜은 포켓몬에 슬롯 「전용」(트레이너 포텐셜로 취급), 아스키아트는 마지막 단계 AA, 성별은 AA 기호(♂/♀)로, 없으면 「없음」.
- 포텐셜 문장은 엔진이 읽는 표준 문장으로(판정표의 정규화 결과 활용). 읽히지 않는 것은 원문을 `raw`에 두고 그대로 넣는다(빼지 않는다).

## 할 일
1. 변환 스크립트(scratchpad)로 28명 → 편집기 저장 형식 JSON(`samples/finis-chaldea.aabdata.json`). 실프 샘플 파일 형식을 그대로 따른다.
2. 기술·특성·소지품 이름을 데이터와 맞춘다(표기 차이는 맞추고 목록 보고, 데이터에 없는 것은 원문 그대로 두고 보고 — 데이터 추가는 하지 않음).
3. 편집기에서 열리는지, 출전 선택에서 6명을 골라 배틀 1T가 예외 없이 진행되는지 검사 1개(실프 샘플 검사와 같은 방식). `TeamRules` 결과는 보고(위반이 있어도 고치지 말고 보고).
4. `docs/handoff/samples.md`에 절 하나.

## 검사·병합
`powershell -NoProfile -ExecutionPolicy Bypass -File ./desktop/verify.ps1 -Visual -OutputPath outputs/candidate-Q` → fetch·rebase → verify → `git push origin task/Q-chaldea-party:main` + 브랜치. 원본 시트 txt는 커밋하지 않는다.

## 보고
병합 커밋 · 파일 이유 · verify 줄 · 인원/트레이너 목록 · 능력치 표(체력 제외 요약) · 이름 맞춘 목록·데이터에 없는 것 · 읽히지 않은 포텐셜 수(인물별) · `TeamRules` 결과 · 사용자 판단 필요.
