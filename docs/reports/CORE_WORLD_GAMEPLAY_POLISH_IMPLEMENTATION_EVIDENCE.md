# 게임형 왕국 월드·캐릭터 상호작용 구현 증적

- 상태: `IMPLEMENTED / VERIFIED`
- 기준일: 2026-07-21 KST
- 관련 이슈: `#62`
- 설계: `docs/design/TYCOON_CORE_WORLD_GAMEPLAY_POLISH_COMPLETE_DESIGN_v1.0.md`
- 완결성 검토: `docs/reviews/CORE_WORLD_GAMEPLAY_POLISH_COMPLETENESS_REVIEW_v1.0.md`

## 1. 구현 결과

| 영역 | 구현 결과 |
|---|---|
| 카메라·월드 | 논리 좌표를 `x + y×0.16`, `y×0.74`로 투영하고 시작 배율을 0.92로 고정했다. |
| 왕국·사냥터 | 왕국을 중앙에 두고 R01~R04를 사분면, R05를 북쪽 외곽에 배치했다. 모든 지역은 관문부터 이어지는 3층 도로를 가진다. |
| 사냥터 표현 | 큰 사각 카드 대신 CC0 바닥 타일 5개를 겹친 불규칙 지형과 작은 한국어 표지판을 사용한다. |
| 왕국 표현 | 광장, 안길, 성벽, 5개 고유 시설 스프라이트, 이름표와 터치 영역을 구성했다. |
| 건물 상호작용 | 주점·상점·대장간·치료소·모험가 길드가 시설 설명과 2개 기능 버튼을 열고 기존 게임 화면으로 이동한다. |
| 용병 상호작용 | 월드 용병을 누르면 실제 용병 상세와 상시 사냥 DTO를 결합한 상태·장비·활동 모달이 열린다. |
| 시각 품질 | 시설 5종을 64×64, 최대 8색, Point/Clamp 규칙의 결정적 픽셀 자산으로 추가했다. |
| 겹침 방지 | 왕국 이용 중인 용병을 타원형 생활 슬롯에 분산하고 터치 영역 간 비겹침을 PlayMode로 검증한다. |

## 2. 데이터와 권한 경계

- 건물 탭과 캐릭터 상세 조회는 Save를 변경하지 않는다.
- 캐릭터 정보는 `MercenaryRosterService.GetDetail`을 사용한다.
- 체력·가방·최근 피해·강화·스킬·순환·수익은 `ContinuousHuntGameService` 조회값만 표시한다.
- 장비 변경은 새 명령을 만들지 않고 기존 가방 화면으로 이동한다.
- Save Schema, Migration, contentVersion, 서버 API는 변경하지 않았다.
- 신규 외부 자산과 유료 자산은 추가하지 않았다.

## 3. 반복 검수와 수정

1. 첫 렌더에서 주점 대기 용병의 이름표가 겹치는 문제를 확인했다.
2. 용병 목적지를 시설 주변의 결정적 생활 슬롯으로 분리했다.
3. 활성 용병 터치 영역을 전수 비교하는 `TownMercenaryTouchTargetsUseSeparatedLivingPositions` 검증을 추가했다.
4. 기존 시각 게이트가 내부 Sprite 21개를 고정 기대해 실패한 문제를 26개 계약으로 갱신했다.
5. 최종 1080×1920 월드 렌더와 캐릭터 상세 렌더를 다시 생성해 중앙 왕국, 연결 도로, 사냥터 입구, 건물, 용병, 장비 모달의 표시를 확인했다.

로컬 렌더 증적:

- `artifacts/mobile-living-world-portrait.png`
- `artifacts/world-gameplay-polish-character-detail.png`

렌더 파일은 CI 산출물이며 저장소에는 커밋하지 않는다.

## 4. 자동 검증 결과

| 검증 | 결과 |
|---|---|
| 게임형 월드 전용 정적 게이트 | `PASS` |
| Unity 전용 EditMode | 10/10 통과 |
| Unity 전용 PlayMode | 14/14 통과 |
| Unity 전체 EditMode | 158/158 통과 |
| Unity 전체 PlayMode | 57 통과, 0 실패, 1 제외 |
| 콘텐츠·Save 검증 | `CONTENT VALIDATION PASSED` |
| Gradle 서버 테스트 | 13개 통합 테스트 통과 |
| 저장소 전체 CI | `Pipeline passed in 32s` |
| `git diff --check` | 통과 |

전체 PlayMode의 제외 1건은 기존 `P05_P_011_CaptureEightAcceptanceFixtures`다. 이 검증은 비배치 Game View 렌더가 필요하므로 배치 실행에서 의도적으로 제외된다. 이번 월드와 캐릭터 상세 캡처는 그래픽 장치가 있는 배치 실행에서 각각 통과했다.

로컬에는 프로젝트 고정 버전 6000.3.20f1 대신 Unity 6000.4.11f1만 설치되어 있어 실제 Unity 검증은 6000.4.11f1로 수행했다. Unity가 자동 변경한 Package·ProjectSettings는 모두 복구했으며 저장소의 6000.3.20f1 고정 계약은 유지했다. 서버 CI는 프로젝트가 요구하는 Temurin JDK 25.0.3을 로컬 검증 경로에서 사용했다.

## 5. 저작권·에셋 판정

참고작에서는 중앙 마을 관찰, 연결된 사냥 동선, 건물 직접 진입, 캐릭터 상세 정보 구조라는 제품 원칙만 학습했다. 타사 이미지, 프레임, 명칭, 좌표, 수치와 실루엣은 반입하거나 복제하지 않았다. 결과 화면은 기존 등록 CC0 환경과 저장소 내부 생성 픽셀 자산만 사용한다.

판정: `PASS`

## 6. 최종 판정

- 구현: `COMPLETE`
- 설계 일치: `PASS`
- Save 호환: `PASS`
- 한국어 UI: `PASS`
- 모바일 터치·겹침: `PASS`
- 회귀 검증: `PASS`
- 추가 상세 설계 필요: `없음`
