# P12 지역 개방·파견 정책 구현 및 검증 보고서

## 1. 결과

- 기준 설계: `docs/design/TYCOON_P12_REGIONS_UNLOCKS_COMPLETE_DESIGN_v1.0.md`
- 구현 상태: `COMPLETE`
- 설계 완결성: `IMPLEMENTATION_READY: YES`, `UNRESOLVED: NONE`
- 구현 브랜치: `codex/issue-37-p12-regions-unlocks`
- 관련 이슈: `#37`
- 기준 Unity: `6000.3.20f1 (c9ba695d4f07)`

P12는 5개 지역의 개방 조건, 수동 파견 정책, 사냥 배치 검증, 사냥 정산 기반 지역 진행도와 다음 지역 자동 개방을 하나의 Save 트랜잭션으로 연결한다. P11 인계 문서의 P12 레이드 표기는 공식 단계 정의와 충돌해 제외했으며, 레이드는 P14 범위로 유지했다.

## 2. 구현 범위

### 콘텐츠·Save

- 활성 콘텐츠 `1.0.0-content.10`, CSV schema set `8`, 총 `88`개 테이블
- `regions.csv`의 5지역과 지역별 조우 프로필 총 `25`행 확정
- 지역 해제·접근 정책·진행도·사냥 권장 정책 CSV 4종 추가
- Save `.10`에 `regions.regionVersion`, `nextEventSequence`, `events` 추가
- `kingdom.regionAccessPolicies`를 R01~R05 정확히 5개로 정규화
- P11 → P12 마이그레이션과 P12 신규 게임 팩토리 구현
- 지역 이벤트 `RegionUnlocked`, `AccessPolicyChanged`, `HuntSettled` 구현

### 런타임

- 왕국 단계, 이전 지역 진행도, 정예 처치, 시설 레벨, 레이드 선행 조건의 결정적 판정
- `SET_REGION_ACCESS_POLICY` 명령의 요청 해시, CAS revision, operation journal, replay 처리
- 잠금/정책/파티 크기/중복/등급/승급 심사/마을 안전 상태를 전투 시작 전에 검증
- 전투 종료 시 전리품·경험치와 같은 저장 트랜잭션에서 지역 진행도·방문·사냥·정예·최고 등급 갱신
- 조건 충족 시 다음 지역 개방과 기본 파견 허용 정책을 같은 커밋으로 반영
- R05는 P14의 `RAID_HYDRA` 클리어 기록이 생기기 전까지 의도대로 잠김

### UI·빌드

- 5지역 왕국 개척 지도, 잠금 이유, 진행도, 권장 파티/물약/가방 여유, 사냥 통계 표시
- 지역별 파견 허용/중지와 추천 파티 사냥 시작 연결
- Bootstrap HUD의 `개척` 진입 버튼과 닫기 동작 연결
- 16:9 및 20:9 터치 영역 최소 64px 검증
- P12 생성 자산 fingerprint, 캡처 생성기, Android ARM64 IL2CPP 빌더 구현

## 3. 결정적 계약 해시

| 계약 | SHA-256 |
|---|---|
| 콘텐츠 manifest | `462fa00c2da8ba98e2be21d9c47e34a5d4c22565e08b6e14385ab9dbc6caea5e` |
| Save `.10` schema | `96a6cf679f2223deaa1613ba3694147081388cb7ecf578cb4c4fd5fa087304c1` |
| 콘텐츠 package fingerprint | `9b617513a3e5ec082101ef4904a12c490f9e60d645184dc1d95ad1c0ce2e2828` |

## 4. 검증 결과

| 검증 | 결과 |
|---|---|
| `python scripts/generate_p12_content.py --check` | 88/88 테이블 통과 |
| `scripts/ci/p12.sh` | 통과 |
| P12 EditMode 테스트 | 8/8 통과 |
| P12 PlayMode 테스트 | 2/2 통과 |
| 저장소 전체 `scripts/ci/run-ci.sh` | 통과 |
| P04~P12 누적 콘텐츠·Save 게이트 | 전체 통과 |
| PostgreSQL 서버 통합 테스트 | 10/10 통과 |
| Unity 메타·직렬화 정책 | 통과 |
| `git diff --check` | 통과 |
| Android ARM64 IL2CPP 개발 APK | 생성 성공 |

전체 CI는 Temurin JDK `25.0.3`과 고정 Unity `6000.3.20f1`을 사용했다. Android SDK의 `adb devices -l` 결과 연결된 기기가 없어 설치·실기 실행은 수행하지 않았고, APK 생성과 해시까지만 검증했다.

## 5. 시각 증빙

| 파일 | 상태 | bytes | SHA-256 |
|---|---|---:|---|
| `p12_01_region_overview.png` | 5지역 개척 개요 | 122,053 | `97e48c1046c38701ab86ce698b32902d3f901cedd4a4fd083d591b9d9e7ded4d` |
| `p12_02_locked_requirements.png` | 잠금·개방 조건 | 121,122 | `e203a82d8f23e3735a3266f97308705d4c9de142029481d0fc83a9356da19752` |
| `p12_03_policy_closed.png` | 파견 중지 정책 | 120,687 | `bda606e1c197d36bd1f3ab8e3cbe6a2f247d2e7f2f81e43dfcbf5db4e3d94a63` |
| `p12_04_progress.png` | 지역 진행도 72% | 119,934 | `461ce69b62e807a8f6f508b5887f33f1271dfbf683619a4191b895a5606be8d8` |
| `p12_05_20x9.png` | 20:9 대응 | 128,345 | `bf6bb4c26932b7463dcd44b17ba71eba5f805a67ce6bd2a3ca51482dedff527a` |

캡처는 `docs/reports/captures/P12/`에 저장했다. `-nographics`에서는 GPU RenderTexture 증빙이 단색이 되므로, 캡처 단계만 로컬 GPU 배치 모드로 실행하고 테스트·빌드는 무그래픽 배치 모드로 실행했다.

## 6. Android 산출물

- 로컬 경로: `client-unity/Builds/Android/KingdomTycoon-P12-Development.apk`
- 크기: `53,896,899 bytes`
- SHA-256: `866df0d4642ed014e5c7a30f34662684efc55f9ba49766ffd485e27e7344d7d3`
- Git 포함 여부: 빌드 산출물 정책에 따라 제외

## 7. 다음 단계

공식 단계 정의상 다음은 P13 모집 시스템이다. P12의 지역·등급·시설·경제 계약을 입력으로 사용하되, 모집 확률·천장·중복 처리·재화 원장·서버 권위·실패 복구를 P13 최종 설계서에서 먼저 확정해야 한다. P14는 레이드, P15는 튜토리얼·오프라인·릴리스 완성 단계로 유지한다.
