# Codex 작업 환경

2026-09-09 적용. OpenAI의 [Best practices](https://learn.chatgpt.com/guides/best-practices), [AGENTS.md](https://learn.chatgpt.com/docs/agent-configuration/agents-md), [설정 문서](https://learn.chatgpt.com/docs/config-file/config-reference)를 참고했다.

- 이 컴퓨터: `~/.codex/AGENTS.md`에 간결한 설명, 필요한 컨텍스트만 읽기, 구현 후 검증, 사용자 데이터 보존, 인계 기록 원칙을 넣었다.
- 이 프로젝트: `AGENTS.md`에 파일 위치, 계산 규칙, 글꼴, 포텐셜 구성 방식과 검증 명령을 넣었다. 다른 컴퓨터로 저장소를 옮겨도 함께 사용할 수 있다.
- 개인 및 프로젝트 `config.toml`의 `tool_output_token_limit = 4000`은 도구 출력이 대화 기록에 차지하는 크기를 제한한다. 전체 토큰 사용량을 4,000으로 제한하는 설정은 아니다.
- `desktop/verify.ps1`은 별도 출력 폴더에서 빌드 후 배틀/편집기 회귀 검사를 수행한다. `-Visual`을 붙이면 도감 검색·입력·선택 유지 검사와 화면 캡처도 수행한다. 이미지는 직접 열어 확인한다.
- 기존 개인 설정은 `~/.codex/config.toml.before-harness-20260909.bak`으로 백업했다. 새 작업을 열 때 설정과 AGENTS 지침이 로드된다.

이는 모델의 추론 자체를 보장하는 설정이 아니라, 작업 지침·반복 가능한 검증·상태 기록을 연결한 개발 절차다. 실패가 확인되면 해당 동작의 회귀 검사를 추가하고 관련 지침만 짧게 보완한다.
