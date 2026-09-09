# AA BATTLE

아스키아트 기반 창작 포켓몬 배틀 시뮬레이터의 Windows 테스트 빌드입니다. 포텐셜 실행기를 추가할 수 있도록 트리거와 효과를 분리한 데이터 구조를 준비하고 있습니다.

## Windows 빌드

Windows PowerShell에서 다음 명령을 실행합니다.

```powershell
./desktop/build.ps1
```

결과물은 `outputs/desktop/AABattle.exe`와 `AABattleDataEditor.exe`입니다. 출력 폴더는 Git에서 제외되며 EXE에는 HeadKasen과 Pokemon BW 폰트, 시트 참조 데이터가 포함됩니다.

검증:

```powershell
./outputs/desktop/AABattle.exe --self-test battle-test.txt
./outputs/desktop/AABattleDataEditor.exe --self-test editor-test.txt
```

## 현재 기능

- 좌우 포켓몬 AA, 기술 선택, 대미지 계산, HP와 턴 처리
- 20타입 상성 및 무효, 주요 상태이상·상태변화, 날씨·필드·중력·트릭룸
- 원본 슈퍼 마개조 계산기 Ver 3.617의 능력치 및 대미지 계산 기반
- 같은 프로그램 안의 트레이너·포켓몬·포텐셜 데이터 편집기
- 포켓몬 포텐셜 입력 시 19종의 포텐셜 종류를 선택하고, 『역할』『선』『범용』은 정해진 목록에서 선택
- 트레이너와 포켓몬을 왼쪽/오른쪽 배틀 슬롯으로 즉시 불러오기
- 기술과 함께 4식 1개·지령 1개 선언, 시합 중 사용 이력과 효과 처리
- 대기 포켓몬 교대와 『돌아와!』의 상대 행동 후 교대
- `.aabdata.json` 저장 및 불러오기

팀 편집기의 여섯 능력치는 종족값입니다. 배틀 슬롯 적용 시 개체값 31, 노력치 0, 성격 무보정을 사용한 뒤 원본 시트의 레벨 차 보정을 적용합니다.

트레이너를 배틀 진영에 적용하면 그 트레이너가 가진 활성 지령이 선언 목록에 나타납니다. 4식과 지령은 모두 선택 사항이며 각 항목은 원칙적으로 1시합 1회입니다. 그 밖의 포텐셜 데이터는 전달되지만 효과 실행기는 아직 활성화하지 않았습니다. 수집한 포텐셜 연구 데이터와 실행 모델은 `research/potentials`에 있습니다.

## 주요 파일

- `desktop/BattleEngine.cs`: 능력치, 대미지, 턴 처리
- `desktop/Mechanics.cs`: 타입, 상태, 날씨와 필드 규칙
- `desktop/BattleApp.cs`: 배틀 UI
- `desktop/DataEditor.cs`: 통합 데이터 편집기와 배틀 슬롯 변환
- `desktop/MechanicsTests.cs`: 회귀 검사
- `lib/reference-data.json`: 시트에서 추출한 포켓몬·기술·상성 데이터
- `PROJECT_HANDOFF.md`: 다른 컴퓨터나 새 Codex 작업을 위한 현재 상태

웹 프로토타입 소스도 저장소에 남아 있지만 현재 배포 대상은 네이티브 Windows EXE입니다.
