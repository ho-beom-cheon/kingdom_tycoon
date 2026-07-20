# 상시 자동 사냥 핵심 루프 정정 구현 증적

## 1. 변경 요약

- 일반 사냥을 단일 파티 출정에서 용병별 영구 사냥터 배치로 정정했다.
- 같은 사냥터에 여러 용병을 배치하고 각자 독립 상태·체력·가방·결정 시각으로 순환한다.
- 귀환 후 전리품 판매, 회복, 기존 상점 자율 구매, 장비 점검을 거쳐 같은 사냥터로 자동 복귀한다.
- 기존 저장은 `saveVersion=1`, `contentVersion=1.0.0-content.13`을 유지하면서 선택 필드를 원자 보정한다.
- `지역` 내비게이션의 플레이어 표시를 `사냥터`로 바꾸고, 전장 동작과 용병별 배치 카드가 있는 한국어 화면을 연결했다.
- 기존 P12 지역 정책 화면과 P06/P08 회귀 계약은 유지했다.

## 2. 주요 변경 파일

- `Runtime/Infrastructure/Combat/ContinuousHuntGameService.cs`
- `Runtime/Presentation/Combat/ContinuousHuntScreenPresenter.cs`
- `Runtime/Bootstrap/AppRoot.cs`
- `Runtime/Presentation/Navigation/UnifiedNavigationMenu.cs`
- `Resources/Contracts/save.content.13.schema.json`
- `Resources/Contracts/p15-new-game.template.json`
- `scripts/generate_p15_content.py`
- `Tests/EditMode/ContinuousHuntTests.cs`
- `Tests/PlayMode/ContinuousHuntScreenTests.cs`
- `scripts/ci/continuous-hunt.sh`

## 3. 저장 마이그레이션

추가된 용병 자율 행동 필드는 `assignedRegionId`, `autoResume`, `currentHpBps`, `bagFill`, `bagCapacity`, `pendingSaleGold`, `cyclesCompleted`, `earnedGold`다. 스키마에서는 기존 저장 호환을 위해 선택 속성이며, 런타임 부팅 시 누락 값을 채워 한 번 저장한다.

생성기와 다음 산출물을 함께 갱신했다.

- P15 신규 게임 템플릿
- P15 신규 게임/마이그레이션 golden
- Save schema registry SHA-256

## 4. 실행 명령

```powershell
& 'C:\Users\cjs41\Unity\Hub\Editor\6000.3.20f1\Editor\Unity.exe' -batchmode -nographics -projectPath '<worktree>\client-unity' -runTests -testPlatform editmode
& 'C:\Users\cjs41\Unity\Hub\Editor\6000.3.20f1\Editor\Unity.exe' -batchmode -projectPath '<worktree>\client-unity' -runTests -testPlatform playmode

$env:JAVA_HOME='C:\Users\cjs41\IdeaProjects\kingdom_tycoon\.tools\jdk25-extracted\jdk-25.0.3+9'
$env:PATH="$env:JAVA_HOME\bin;$env:PATH"
& 'C:\Program Files\Git\bin\bash.exe' scripts/ci/run-ci.sh
```

## 5. 자동 테스트 결과

| 검증 | 결과 |
|---|---:|
| 신규 EditMode | 3 통과 / 0 실패 |
| 신규 PlayMode | 2 통과 / 0 실패 |
| 전체 EditMode | 139 통과 / 0 실패 |
| 전체 PlayMode | 45 통과 / 0 실패 / 렌더 캡처 1 의도적 생략 |
| 콘텐츠·P07~P17 정적 게이트 | 통과 |
| 신규 상시 사냥 CI 게이트 | 통과 |
| Java 25 + PostgreSQL 18 Testcontainers | 13 통과 / 0 실패 |
| Unity 구조·meta 정책 | 통과 |

전체 PlayMode 최초 실행에서는 신규 테스트가 배치 상태를 남겨 후속 P05 테스트가 영향을 받는 테스트 격리 결함을 발견했다. 신규 테스트 종료 시 전원 귀환·정산하도록 보완한 뒤 전체 45개가 통과했다.

## 6. 수동 확인 항목

- 사냥터 화면에서 용병 두 명을 `초록바람 들판`에 동시 배치
- 두 용병의 상태·체력·가방이 독립 표시되는지 확인
- 상태별 이동, 탐색, 공격 전진, 몬스터 맥동, 귀환 이동 확인
- 배치 해제 후 다른 용병의 사냥이 계속되는지 확인
- 1920×1080, 2400×1080에서 버튼 최소 64px와 같은 패널 내 비겹침 확인
- 화면의 플레이어 노출 문구가 한국어인지 확인

## 7. 명세와 차이

- 자동 스킬 구매는 스킬 소유/레벨 Save 계약이 없어 구현하지 않았다. 기존 P11 성장 화면에서 사용자가 통제한다.
- 장비는 기존 P08 상점 자율 구매·자동 장착을 재사용한다. 자동 강화는 기존 P10 사용자 명령을 유지한다.
- 그래픽은 동작 검증용 도형 기반이다. 향후 에셋을 교체해도 서비스/Save 계약은 변하지 않는다.

## 8. 남은 위험과 다음 단계

- 실제 Android 기기의 Safe Area, 발열, 배터리, 백그라운드 복귀는 기기 QA가 필요하다.
- 자동 스킬 구매와 자동 강화 정책은 Save 계약·가격 정책·실패 보상 규칙을 포함한 별도 최종 설계가 필요하다.
- 상용 에셋 도입 전 현재 동작 템포와 정보 밀도를 사용자 테스트로 조정한다.

다음 단계 진입 판정: **가능**. 상시 자동 사냥 핵심 루프는 구현·회귀 검증됐다.
