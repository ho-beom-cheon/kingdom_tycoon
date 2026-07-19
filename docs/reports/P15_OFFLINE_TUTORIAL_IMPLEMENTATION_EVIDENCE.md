# P15 오프라인 정산·튜토리얼·UX 통합 구현 증거

- 기준 설계: `docs/design/TYCOON_P15_OFFLINE_TUTORIAL_COMPLETE_DESIGN_v1.0.md`
- 구현 브랜치: `codex/issue-43-p15-offline-tutorial`
- 관련 이슈: `#43`
- 게임/콘텐츠/Save: `1.0.0-p15` / `1.0.0-content.13` / schema set `11`
- 작성일: 2026-07-19

## 1. 완결성 검토 결과

설계의 미결정 항목은 구현 전에 모두 닫았다. 오프라인 한도 8시간, 최소 60초, 시각 역행 허용 2초, 최근 이력 20개, 정산 순서 6종, 튜토리얼 10단계, 목표 시간 25분, 보상 1회 지급, 명령 재실행 멱등성, 원자적 Save 반영을 고정했다.

P15는 서버 시뮬레이션이나 틱 재생을 추가하지 않는다. 저장된 마지막 신뢰 시각과 현재 신뢰 시각 사이의 구간을 정수 요약식으로 정산하고, 레이드·특수 고용·강화·재련은 오프라인 대상에서 제외한다.

## 2. 구현 범위

- 콘텐츠 패키지 `.13`: 기존 95개 정규 CSV를 유지하면서 오프라인 규칙·튜토리얼 단계·튜토리얼 지급·런타임 설정을 P15 계약으로 확정
- Save schema set `11`: 오프라인 이력, 정산 상태, 튜토리얼 진행·수령·명령 영수증, P15 작업 저널 타입 추가
- 마이그레이션: P14 Save를 불변 입력으로 받아 P15 오프라인·튜토리얼 구조를 결정적으로 생성
- 런타임: 사냥, 물약, 시설, NPC 숙련, 부상 회복, 승급 심사의 고정 순서 정산과 8시간 상한 구현
- 안정성: settlement ID 중복 방지, operation hash 검증, revision CAS, 결과 payload 기반 재실행, 지급 ID 기반 1회 지급 구현
- 튜토리얼: 단계 완료, 개별 건너뛰기, 전체 종료, 다음 단계 전이와 완료 상태 구현
- UI: 오프라인 보고·10단계 여정·다음 명령의 3열 허브, 16:9/20:9, 최소 64px 터치 영역, 정상·상한·역행·진행·완료 상태 구현
- 진입: Bootstrap 서비스 등록, P15 새 게임 템플릿, HUD `왕국 보고` 버튼, 화면 열기/닫기 연결

## 3. 자동 검증

### 콘텐츠·계약

```text
python scripts/generate_p15_content.py --check
p15-content: verified 95 tables
manifest-sha256: 31ff9b71202bac7990fb6582583ccc7d5649cdf6fb0f869e02896938b3dcf377
save-schema-sha256: 47f160d56262975029fdfcb26ca5938b5a6298c00a734116647c31d70a471d49
package-fingerprint: e2ff0175a6131c3b1b5a77f672244d662c47752c45bb92c69662c5d8def2b1ca
```

### Unity EditMode

```text
필터: KingdomTycoon.Tests.EditMode.P15OfflineTutorialTests
결과: 5 passed / 0 failed
```

검증 항목은 95개 테이블/Save 계약, P14→P15 불변 마이그레이션, 8시간 상한·시각 역행, 6종 정산 순서·중복 방지, 튜토리얼 지급·재실행·전체 종료다.

### Unity PlayMode

```text
필터: KingdomTycoon.Tests.PlayMode.P15OfflineTutorialScreenTests
결과: 2 passed / 0 failed
```

Bootstrap→Kingdom 실제 장면 전환 뒤 HUD 진입, 화면 열기·진행·닫기, 1920×1080과 2400×1080의 64px 터치 영역을 검증했다.

전체 EditMode 실행에서는 이전 단계의 활성 콘텐츠 버전을 고정 문자열로 비교하는 P11~P14 테스트 4개가 `.13` 활성화로 실패했다. P15 신규 테스트 5개는 초기 교정 후 전부 통과했으며, 단계별 CI는 각 단계 필터를 독립 실행하는 기존 계약을 유지한다.

## 4. 렌더 증거

`docs/reports/captures/P15/`에 GPU 렌더 PNG 7장을 생성했다.

1. `p15_01_new_install.png`
2. `p15_02_offline_1h.png`
3. `p15_03_offline_8h.png`
4. `p15_04_tutorial_active.png`
5. `p15_05_tutorial_complete.png`
6. `p15_06_clock_rollback.png`
7. `p15_07_20x9.png`

헤드리스 Null Graphics 결과는 증거로 인정하지 않고 폐기했으며, 그래픽 장치가 활성화된 Unity 배치 실행으로 다시 생성한 뒤 실제 PNG를 열어 정보 위계·한글 렌더·상태 색·20:9 레이아웃을 확인했다.

## 5. Android 실행 산출물

Unity `6000.4.11f1` 로컬 설치본과 Android 모듈을 사용해 최종 소스 기준 ARM64 IL2CPP Development APK 빌드를 완료했다. 결과는 `Success`, 크기는 `58,907,400 bytes`다. 저장소의 공식 에디터 핀은 `6000.3.20f1`로 유지하며, 로컬 에디터가 자동 갱신한 패키지·ProjectVersion 변경은 커밋 범위에서 제외한다.

산출물 경로:

```text
client-unity/Builds/Android/KingdomTycoon-P15-Development.apk
```

## 6. CI/CD 연결

- `scripts/ci/p15.sh`: 콘텐츠, 마이그레이션, 런타임·UI 진입점, 7개 렌더 증거, 선택적 Unity/Android 실행 게이트
- `scripts/ci/run-ci.sh`: 저장소 공통 CI에 P15 게이트 연결
- `.githooks/pre-commit`, `.githooks/pre-push`: 로컬 훅 진행 출력에 P15 게이트 연결
- `.github/workflows/ci.yml`: P15 감지와 계약·golden·캡처 artifact 업로드 연결

CI는 검증을 자동화하고, 배포는 기존 승인 기반 delivery 경계를 유지한다. P15 구현은 새로운 운영 자격증명이나 상점 배포 권한을 추가하지 않는다.

저장소 공통 `scripts/ci/run-ci.sh` 실행 결과는 25초에 전체 통과했다. P07~P15 단계 게이트, 콘텐츠/Save 검증, PostgreSQL 18 기반 서버 통합 테스트 10건, Unity 버전·Force Text·Visible Meta Files·누락 meta 정책을 모두 확인했다.
