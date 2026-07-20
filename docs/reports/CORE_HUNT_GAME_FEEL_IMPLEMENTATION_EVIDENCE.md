# 상시 사냥 게임 체감 구현 증적

- 상태: `IMPLEMENTED / VERIFIED`
- 기준 설계: `docs/design/TYCOON_CORE_HUNT_GAME_FEEL_COMPLETE_DESIGN_v1.0.md`
- 완결성 검토: `docs/reviews/CORE_HUNT_GAME_FEEL_COMPLETENESS_REVIEW_v1.0.md`
- 관련 이슈: `#56 기능: 상시 사냥 전투·보상 피드백과 게임 체감 완성`
- 구현 브랜치: `codex/issue-56-hunt-game-feel`
- 선행 기준선: `origin/codex/issue-54-world-hunt-loop`
- 검증일: 2026-07-21 KST

## 1. 구현 결과

상시 사냥의 기존 Save·전투·경제 판정을 바꾸지 않고, 커밋된 조회 모델의 리비전 차이를 화면 전용 사건으로 변환했다. 화면은 다음 흐름을 한 장소에서 계속 보여 준다.

`타격 → 스킬 → 처치 → 재등장 → 전리품/현상금 → 귀환/정산 → 스킬/장비 성장`

구현된 가시화 항목은 다음과 같다.

- 일반/스킬 피해 숫자, 타격 섬광, 펄스, 용병 돌진
- 처치와 몬스터 재등장 문구
- 최대 3행으로 제한되고 재사용되는 전투·보상 소식
- 용병 카드의 체력·가방·스킬 총 레벨·최고 강화 수치
- 사냥터별 `안정/주의/위험/고위험/극한` 배지와 canonical 전리품 이름
- 외부 음원 파일 없이 한 번 생성해 재사용하는 짧은 절차형 효과음
- 효과음 켬/끔과 모션 보통/감소 로컬 설정
- 모션 감소 상태에서도 유지되는 피해 숫자·색상·한국어 상태 문구

## 2. 계약 보존

- Save content 버전과 JSON Schema를 변경하지 않았다.
- 전투 피해, 드롭, 정산, 자동 성장 판정 로직을 변경하지 않았다.
- 첫 조회는 기준선만 저장하고 사건을 만들지 않는다.
- 같은 리비전은 중복 피드백을 만들지 않는다.
- 사냥터 위험도와 전리품은 하드코딩된 별도 표가 아니라 기존 canonical 콘텐츠에서 파생한다.
- 화면에는 영문 상태 코드와 콘텐츠 ID를 노출하지 않는다.

## 3. 자동 검증

### Unity EditMode

실행 명령의 핵심 조건:

```text
-batchmode -nographics -runTests -testPlatform editmode
-testFilter KingdomTycoon.Tests.EditMode.WorldHuntFeedbackTests;KingdomTycoon.Tests.EditMode.ContinuousHuntTests
```

결과: `12 passed / 0 failed`, 4.543초.

검증 범위:

- 기준선·같은 리비전 중복 억제
- 일반/스킬 피해, 처치, 재등장 대상 귀속
- 전리품·현상금·귀환·정산·스킬·장비 성장 사건
- 지역별 한국어 위험도·대표 전리품 파생
- 피드백 시간·음량·최대 행 정책의 유효성 검사

### Unity PlayMode

실행 명령의 핵심 조건:

```text
-batchmode -runTests -testPlatform playmode
-testFilter KingdomTycoon.Tests.PlayMode.ContinuousHuntScreenTests
```

결과: `6 passed / 0 failed`, 7.805초.

검증 범위:

- 실제 시간 진행 뒤 피해 피드백 활성화
- 최근 피드 최대 3행 제한
- 한국어 지역 차이·성장 정보 표시
- 효과음·모션 감소 버튼 상태 전환
- 1920×1080 및 2400×1080에서 활성 버튼 64px 이상과 형제 버튼 무겹침
- 기존 상시 사냥 배치·월드 드래그·화면 진입 회귀

자동 검증은 사용자 게임 창을 띄우지 않는 별도 배치 프로세스로 수행했다. 설치된 Unity 6000.4가 만든 패키지·프로젝트 버전 자동 변경은 검증 후 모두 제거했으며, 저장소의 6000.3.20f1 고정 계약은 유지했다.

## 4. CI 검증

- `scripts/ci/hunt-game-feel.sh`: 통과
- `scripts/ci/run-ci.sh`: 통과, 33초
- 콘텐츠 생성·해시·Save Schema 검증: 통과
- P07~P17 및 기존 상시 사냥·자동 성장·월드 사냥 게이트: 통과
- 서버 PostgreSQL 통합 테스트를 포함한 Gradle `clean test`: 통과
- Unity 프로젝트 버전·직렬화·메타 파일 정책: 통과
- `git diff --check`: 통과

## 5. 반복 검증에서 수정한 결함

첫 PlayMode 실행은 효과음 컴포넌트가 화면 갱신 시점에 지연 추가되어 배치 환경에서 `MissingComponentException`을 냈다. `RequireComponent(AudioSource)`와 초기화 시점 고정으로 의존성을 명시한 뒤 전체 PlayMode 6건이 통과했다. 테스트 기대 문구도 최종 설계의 실제 한국어 계약인 `장비 최고 +N`과 canonical 첫 지역 전리품에 맞춰 교정했다.

## 6. 완료 판정

최종 설계의 필수 항목과 자동 인수 조건을 모두 구현하고 검증했다. 외부 아트·상용 음원 에셋을 씌우기 전 단계에서, 상시 사냥의 핵심 행동과 보상·성장 결과를 게임 동작처럼 읽을 수 있는 임시 표현 계층이 준비됐다.
