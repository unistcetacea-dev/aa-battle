# 지시서 J: HANDOFF 주제별 분리 + Releases zip 배포

브랜치 `task/J-handoff-release` · 다른 작업과 동시에 해도 되지만 `PROJECT_HANDOFF.md`를 통째로 옮기므로 **진행 중인 다른 작업이 없을 때** 한다. 먼저 `2026-10-14-user-decisions.md`(「구조·운영」), `desktop/publish.ps1`, `desktop/AABattle.UI/Updater.cs`·`UpdateLogic.cs`·`StartFormUpdate.cs`, `release/windows/`를 읽는다.

## 1. HANDOFF 분리 (사용자 결정: 주제별)
- `docs/handoff/` 아래 주제별 파일로 나눈다: 기술, 특성, 포텐셜(트리거·효과 모듈·ModuleCatalog), 소지품, 상태, 배틀 진행(턴·교대·추격·대미지), 트레이너(지시·지령·자질·유대·역할), 데이터·편집기·UI, 배포·빌드, 샘플·조사 자료. 날짜별 작업 기록은 각 주제 파일 끝의 「변경 기록」으로.
- `PROJECT_HANDOFF.md`는 **목차 + 현재 핵심 규칙 요약**(10KB 이하)만 남긴다.
- 내용 삭제 금지(옮기기만). 옮긴 뒤 원문 줄 수 합계가 거의 같은지 확인해 보고.
- `AGENTS.md`의 `PROJECT_HANDOFF.md` 언급을 「`PROJECT_HANDOFF.md`(목차) → `docs/handoff/<주제>.md`」로 고치고, 「동작 변경은 해당 주제 파일을 갱신」으로 바꾼다.

## 2. 배포를 GitHub Releases zip으로 (사용자 결정)
- `publish.ps1`: 빌드 → `AABattle-<버전>.zip`(EXE 2개 + README + 필요한 데이터) 생성 → `gh release create v<버전> <zip> --notes <변경 내용>`(gh가 없으면 zip만 만들고 수동 업로드 안내 출력). `release/windows`로 복사하지 않는다.
- 업데이트 기능: `https://api.github.com/repos/unistcetacea-dev/aa-battle/releases/latest`에서 태그(버전)와 zip 자산 URL을 읽어 비교 → zip 받기 → 임시 폴더에 풀기 → 기존 교체 스크립트로 EXE 교체(사용자 데이터 보존, Zone.Identifier 제거). `version.txt` 방식 제거.
- `release/windows/`의 EXE·version.txt는 저장소에서 삭제(README는 「Releases에서 zip을 받으세요」 안내로 남기거나 루트 README에 한 줄). 기존 기록의 EXE를 지우는 기록 재작성은 하지 않는다.
- `AGENTS.md`의 배포 줄: 「배포는 `desktop/publish.ps1`(GitHub Releases에 zip 업로드)」.

## 검사
버전 비교·Releases JSON 해석(`JavaScriptSerializer`, 자산 이름 선택) `[TestCase]`, zip 해제 경로·교체 스크립트 따옴표 검사. 네트워크 검사 금지. `-Visual`로 시작 화면 알림 확인.

## 보고
공통 형식 + 분리된 파일 목록과 크기, 첫 배포 방법(사용자가 할 일: `gh` 설치·로그인 또는 웹 업로드).
