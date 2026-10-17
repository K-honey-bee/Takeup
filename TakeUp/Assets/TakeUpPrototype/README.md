# TakeUp — 선사시대 생존 프로토타입

## 시작

1. Unity Hub에서 이 프로젝트 폴더 `Takeup/TakeUp`을 Unity **6000.3.9f1**로 엽니다.
2. `Assets/TakeUpPrototype/Scenes/PrehistoricPrototype.unity`를 엽니다.
   상단 메뉴 **TakeUp > Open Prototype Scene**으로도 열 수 있습니다.
3. Play를 누릅니다. 플레이어는 황금색 원, 적은 붉은 마름모로 표시됩니다.

## 조작과 진행

- WASD / 방향키 / 게임패드 왼쪽 스틱: 상하좌우 이동. 대각선 속도는 정규화됩니다.
- 범위 안에서 가장 가까운 적을 향해 돌을 자동으로 던집니다.
- 적을 처치하면 녹색 경험치가 떨어집니다. 가까이 가면 끌어당겨 수집합니다.
- 레벨업마다 전투가 정지하고 증강 3개 중 하나를 클릭하거나 숫자 1/2/3으로 선택합니다.
- 큰 경험치를 한 번에 획득해 여러 레벨이 오르면 선택을 순서대로 모두 받습니다.
- ESC: 일시정지 / 재개. 사망 또는 일시정지 화면에서 다시 시작할 수 있습니다.
- 30초마다 웨이브가 강화됩니다. 2웨이브부터 빠른 적, 3웨이브부터 큰 적이 등장합니다.
- 180초 생존하면 **보스 진입 준비** 화면이 표시됩니다. 실제 보스 전투는 아직 없습니다.

## 이미지와 게임 수치 바꾸기 — Inspector

**TakeUp > Select Gameplay Settings** 또는
`Assets/TakeUpPrototype/Data/PrehistoricSettings.asset`을 선택합니다.

이미지를 Assets 안으로 가져온 뒤 Texture Type을 **Sprite (2D and UI)**로 바꾸고 Apply합니다.
도트는 Filter Mode **Point**, Compression **None**으로 설정하면 선명합니다.

| 설정 | 바꾸는 내용 |
| --- | --- |
| Player Sprite / Tint / Size | 플레이어 그림, 색, 월드 크기 |
| Stone Sprite / Size | 돌 투사체 그림과 크기 |
| Spear Sprite / Size | 창 투사체 그림과 가로·세로 크기. 원본은 오른쪽을 향하도록 제작 |
| Experience Sprite / Tint | 경험치 아이템 그림과 색 |
| Ground Sprite / Tint | 맵 배경. 배경 전체 크기로 늘려 표시 |
| Enemies > 각 항목 > Sprite / Tint / Size | 몬스터 종류별 그림, 색, 크기 |
| Enemies > First Wave / Spawn Weight | 등장 웨이브, 상대적 등장 비율 |
| Enemies > Health / Speed / Damage / Experience | 체력, 이동 속도, 접촉 피해, 경험치 |
| Augments > 각 항목 > Icon / Title / Description | 증강 카드 그림, 이름, 설명 |
| Augments > Kind / Amount / Max Stacks | 강화 종류, 증가량, 최대 획득 횟수 |
| Stage Duration / Wave Duration | 생존 목표 시간 / 웨이브 간격 |
| Max Alive Enemies | 동시에 존재할 수 있는 몬스터 상한 |

원본 그림의 색을 그대로 쓰려면 해당 **Tint를 흰색**으로 설정하세요.
플레이어·몬스터 그림은 비율을 유지하며 Size에 맞춰집니다. 이 프로토타입은 단일 스프라이트와 좌우 반전을 사용하며 방향별 걷기 애니메이션은 아직 없습니다.
설정 변경은 Play를 종료하고 다시 시작해 확인하세요. Play 중 설정 에셋을 바꾸면 그 값이 저장될 수 있습니다.

## 구조

- `SurvivalSettings.cs`: 인스펙터에서 조절하는 데이터. 시대별 설정 에셋으로 확장 가능.
- `SurvivalGame.cs`: 이동, 자동 공격, 웨이브, 적 추적, 경험치, 증강, 게임 상태.
- `SurvivalHud.cs`: 한국어 HUD, 선택 카드, 일시정지와 결과 화면.
- `Editor/PrototypeSetup.cs`: 설정과 씬 생성, 메뉴, 개발 빌드.
- `SurvivalSmokeTest.cs`: `--takeup-smoke` 실행 인수가 있을 때만 실행되는 통합 점검.

몬스터·투사체·경험치는 Play 중 생성됩니다. 편집 모드에 보이지 않는 것이 정상입니다.
기본 SampleScene은 보존되어 있으며 프로토타입 씬을 빌드 목록 첫 번째에 추가했습니다.
적은 화면 바깥에서 생성되어 추적하며 플레이어만 맵 테두리 안으로 제한됩니다.
충돌은 원형 거리와 투사체 이동 구간을 사용합니다. 현재 지형 장애물과 길찾기는 없습니다.
최대 적 100개, 경험치 250개 이후에는 가까운 경험치에 수량을 합쳐 보존합니다.

## 현재 범위와 다음 단계

이번 범위는 **이동 → 돌 공격 → 몬스터 처치 → 경험치 → 증강 → 창 해금 → 생존 완료**입니다.
시대 전환, 저장, 상점, 보스 전투, 최종 아트·사운드는 포함하지 않았습니다.

첫 문어 보스 계획: 빨강·파랑·노랑·흰색의 표시 순서를 기억하고 해당 바위 뒤에 차례대로 숨기,
촉수 휘두르기, 양쪽 촉수 치기, 이동 속도를 떨어뜨리는 잉크 장판.
후속 구현 시 BossReady 상태에서 보스 씬 또는 보스 전투 상태로 연결하면 됩니다.
