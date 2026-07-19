# P13 모집 시스템 구현 및 검증 보고서

- 관련 이슈: [#39](https://github.com/ho-beom-cheon/kingdom_tycoon/issues/39)
- 구현 브랜치: `codex/issue-39-p13-recruitment`
- 기준 설계: `docs/design/TYCOON_P13_RECRUITMENT_COMPLETE_DESIGN_v1.0.md`
- 게임 버전: `1.0.0-p13`
- 콘텐츠 버전: `1.0.0-content.11`
- Save 스키마: `save.content.11`
- 설계 SHA-256: `416775320aadaeaf8c97bb4bb9561abc8e9777a4d803d3014748b1b697ee3a52`

## 1. 완결성 결론

P13은 주점 후보 갱신·잠금·고용과 특별 모집·S 이상 10회 보장·SS 80회 하드 천장·직업 확률 상승 보장을 하나의 모집 계약으로 완결했다. 모든 용병은 모집 결과 스냅샷에서 새 UUIDv7 인스턴스로 생성되며, 고용 골드·프리미엄 지갑 캐시·원장·모집 기록·이벤트·작업 저널이 동일 Save 리비전에서 원자적으로 반영된다.

개발 빌드는 `DevelopmentRecruitmentGateway`의 `MOCK_ONLY` 권위를 사용한다. 상용 특별 모집은 P00 서버의 계정 지갑·content release·소환 천장·idempotency 트랜잭션을 권위로 삼고 `IRecruitmentGateway` 뒤에서 교체한다. 결제 상품 판매, 환불, 확률 공시 운영 화면은 P13 런타임 범위가 아니다.

설계 완결성 검토 결과 구현을 막는 미해결 항목은 없다.

## 2. 구현 범위

- 91개 테이블의 `content.11` 패키지와 schema set 9 생성
- 주점 레벨별 후보 수 3/4/4/5, 잠금 수 1/1/2/2, 무료 주기와 유료 갱신 비용 데이터화
- C/B/A 고용비 배율과 A/S/SS 특별 모집 확률·복수 천장·직업 확률 상승 데이터화
- P12 → P13 Save 마이그레이션, 신규 게임 템플릿, schema registry 연결
- 후보 생성, 잠금, 유료/무료 갱신, 정원 검증, 로스터 고용 구현
- 특별 모집 영수증, premium wallet cache, pity/featured 상태, 모집 기록과 이벤트 구현
- request hash, expected revision, 중복 operation replay, 원장/저널 digest 일치 구현
- 왕실 모집소 화면, 주점/특별/기록 탭, 천장 게이지, 16:9·20:9 대응 구현
- 생성 자산 fingerprint 검증, GPU 캡처 생성기, Android ARM64 IL2CPP 빌더 구현
- Git Hook, 로컬 CI, GitHub Actions에 P13 게이트와 계약 증거 업로드 추가

## 3. 구현 중 발견하고 수정한 계약 결함

| 결함 | 영향 | 수정 |
|---|---|---|
| content manifest importer가 schema set 9를 거부 | content.11 부트 실패 | v2/schema set 2~9를 명시적으로 허용 |
| 모집 작업의 `resultPayload`를 Save validator가 거부 | 초기 후보 생성 저장 실패 | 커밋된 P13 replayable command를 허용하고 상태 조건 괄호 정정 |
| 주점 골드 원장과 작업 저널 digest/operation type 불일치 | 유료 갱신·고용 저장 거부 | 최종 result digest로 원장을 조정하고 P13 transaction mapping 추가 |
| 비활성 화면 루트와 런타임 버튼 listener 미복원 | HUD 모집 버튼과 탭이 동작하지 않음 | presenter 루트 활성화와 `OnEnable` binding 복원 |
| 기록 탭 렌더가 다시 기록 이벤트를 발행 | StackOverflow | 탭 클릭 알림과 렌더 상태 반영 분리 |
| sprite 없는 Filled Image 사용 | 천장 값과 무관하게 게이지가 가득 참 | RectTransform anchor 기반 진행도로 교체 |

## 4. 검증 결과

| 검증 | 결과 |
|---|---|
| `python scripts/generate_p13_content.py --check` | 91/91 테이블 통과 |
| `scripts/ci/p13.sh` | 통과 |
| P13 EditMode | 7/7 통과, 2.040초 |
| P13 PlayMode | 2/2 통과, 2.378초 |
| 전체 `scripts/ci/run-ci.sh` | 통과, P04~P13 게이트와 Unity 정책 포함 |
| PostgreSQL 서버 통합 테스트 | 10/10 통과, wallet/content/reward/summon 원자성 포함 |
| Android Development | ARM64 IL2CPP 성공, 54,353,635 bytes |

전체 CI는 Temurin JDK 25와 Unity `6000.3.20f1`에서 실행했다. Android 로그는 `Build Finished, Result: Success`를 기록했다.

## 5. 렌더 증거

| 파일 | 검증 장면 | bytes | SHA-256 |
|---|---|---:|---|
| `p13_01_tavern_candidates.png` | 주점 후보·잠금·고용 | 112,025 | `bbf35cb27ea0cd77dd1202f9e3d8281063c01e578ebab66ecdaed113bbd7e000` |
| `p13_02_special_pity.png` | 특별 모집·빈 천장 트랙 | 116,827 | `90e29aadd126b69a1c3440ff4168cd4d2932b6ac4d2c578e7e3a52587c39999e` |
| `p13_03_rate_up_near_pity.png` | 전사 확률 상승·S 9/10 | 118,391 | `2848576073e17fccee017de63baf6716c524f5142e7ec31cce78f0d8e504b3c4` |
| `p13_04_history.png` | 영수증 경계 모집 기록 | 104,799 | `c66e9f61b1d0ad33c083e025b95d2676b9f802572bed0cb0f21ec6b9032485bd` |
| `p13_05_20x9.png` | 20:9 레이아웃 | 119,460 | `93f3aaee0268e6a40ec26624b8582f39ce9c9c924bc4505f7b6ade78321984c2` |

캡처는 `docs/reports/captures/P13/`에 저장했다. GPU RenderTexture가 필요한 캡처만 그래픽 배치 모드로 실행하고, 테스트와 Android 빌드는 무그래픽 배치 모드로 실행했다.

## 6. Android 산출물

- 로컬 경로: `client-unity/Builds/Android/KingdomTycoon-P13-Development.apk`
- 크기: `54,353,635 bytes`
- SHA-256: `c850a9806a72a2f561a827d36d7a57d8a74def295c3e5687fae6280fdca814d3`
- Git 포함 여부: 제외. CI/CD는 빌드 산출물을 artifact로 전달하고 저장소에는 소스·계약·증거만 보존한다.

## 7. 다음 단계

공식 다음 단계는 P14 레이드다. P12 지역 개방, P11 랭크, P06 전투, P13 모집 결과를 입력으로 사용해 레이드 참여 자격·부위 전투·보상·최초 토벌에 따른 R05 개방을 하나의 최종 설계로 확정한 뒤 구현한다. 현재 저장소 자료만으로 P14 최종 설계를 작성할 수 있어 외부 상세 설계 보완은 필수 조건이 아니다.
