# P17 밸런스·최적화·RC 구현 증적

> 기준 이슈: #47
> 구현 브랜치: `codex/issue-47-p17-rc`
> 결과: 로컬 RC 완료 (`RC_CANDIDATE_DEVICE_QA_REQUIRED`)

## 1. 변경 요약

- 사용자 노출 문구를 한국어로 통일하고 P15 왕국 보고·튜토리얼의 내부 ID와 영어 출시 문구를 제거했다.
- 왕국 시설 8종과 전투 직업·몬스터·상태 아이콘을 내부 제작 픽셀 자산으로 교체했다.
- 16:9와 20:9에서 안전 영역, 64px 터치 대상, 버튼 겹침, 보조 프로세스 왕복을 자동 검증한다.
- P17 밸런스·성능 프록시, 자산 라이선스·비용, 한국어 글꼴, Android RC 설정을 CI 계약으로 추가했다.
- Android 제품명 `킹덤 타이쿤`, application identifier `com.kingdomtycoon.game`, `1.0.0-rc.1`, ARM64 IL2CPP 비개발 빌드를 고정했다.
- P04 이후 화면 통합을 기존 프리팹 재사용 방식으로 바꿔 P07~P14 Unity file ID 재생성 노이즈를 제거했다.
- TMP 폐기 예정 줄바꿈 API를 `TextWrappingModes.Normal`로 교체해 C# 빌드 경고를 제거했다.

## 2. 주요 변경 파일

- `client-unity/Assets/KingdomTycoon/Editor/P17ReleaseReadinessSetup.cs`
- `client-unity/Assets/KingdomTycoon/Editor/P04KingdomAssetGenerator.cs`
- `client-unity/Assets/KingdomTycoon/Editor/P06CombatSetup.cs`
- `client-unity/Assets/KingdomTycoon/Editor/P15OfflineTutorialSetup.cs`
- `client-unity/Assets/KingdomTycoon/Tests/EditMode/P17ReleaseReadinessTests.cs`
- `client-unity/Assets/KingdomTycoon/Tests/PlayMode/P17ReleaseReadinessScreenTests.cs`
- `scripts/ci/p17.sh`
- `docs/reports/captures/P17/`

## 3. 실행 명령

```powershell
Unity.exe -batchmode -nographics -projectPath client-unity -executeMethod KingdomTycoon.Editor.P17ReleaseReadinessSetup.Run -quit
Unity.exe -batchmode -projectPath client-unity -executeMethod KingdomTycoon.Editor.P17CaptureGenerator.Run -quit
Unity.exe -batchmode -nographics -projectPath client-unity -runTests -testPlatform EditMode -testResults artifacts/p17-editmode-final.xml
Unity.exe -batchmode -nographics -projectPath client-unity -runTests -testPlatform PlayMode -testResults artifacts/p17-playmode-final.xml
Unity.exe -batchmode -nographics -projectPath client-unity -executeMethod KingdomTycoon.Editor.P17AndroidBuilder.Build -quit

$env:JAVA_HOME='C:\Users\cjs41\IdeaProjects\kingdom_tycoon\.tools\jdk25-extracted\jdk-25.0.3+9'
.\gradlew.bat :server-api:clean :server-api:test --console=plain
"C:\Program Files\Git\bin\bash.exe" scripts/ci/run-ci.sh
```

## 4. 자동 검증 결과

| 검증 | 결과 |
|---|---|
| P17 설계 완결성 | PASS, 구현 차단 `UNRESOLVED` 없음 |
| P17 EditMode 집중 테스트 | 4/4 PASS |
| Unity EditMode 전체 | 136/136 PASS |
| P17 PlayMode 집중 테스트 | 3/3 PASS |
| Unity PlayMode 전체 | 43 PASS, 1 의도적 Skip, 0 Fail |
| PostgreSQL 18 Testcontainers 서버 테스트 | 13/13 PASS |
| 전체 저장소 CI | PASS |
| 활성 자산 대장 | 72행, 상업 이용·수정 허용, 실제 집행액 0원 |
| 16:9·20:9 출시 캡처 | 6/6 생성 및 육안 확인 |
| Android ARM64 IL2CPP RC | PASS |
| C# 폐기 API 경고 | 0건 |
| TMP Font Asset 누락 경고 | 0건 |
| Android Localization App Info 경고 | 0건 |

성능 프록시는 용병 16명, 전투 엔티티 60개, 인벤토리 표시 행 100개를 1,000회 반복해 두 실행의 checksum 일치와 3초 미만을 확인했다. 최종 측정은 0.002초 미만이었다. 이 값은 결정성·계산량 회귀 게이트이며 실제 Android 프레임 승인 수치로 사용하지 않는다.

## 5. Android RC 산출물

- 경로: `client-unity/Builds/Android/KingdomTycoon-1.0.0-rc.1.apk`
- application identifier: `com.kingdomtycoon.game`
- 버전/코드: `1.0.0-rc.1` / `1000001`
- 설정: ARM64, IL2CPP, Development=false, script debugging=false, High stripping
- 크기: `48,059,444 bytes` (`45.83 MiB`, 80MiB 제한 통과)
- SHA-256: `4D520BC612831F64E37048FD87BF988BE23B3BA120241C7AB4BFC3DC26C27510`

APK는 검증 산출물이며 Git에 포함하지 않는다.

## 6. 반복 검증에서 발견하고 해결한 문제

1. GPU 없는 캡처가 단색 회색인데 파일 크기만으로 성공 처리되던 문제를 색상 다양성 검사로 차단했다.
2. P04 장면 재생성 뒤 P08 상점 화면이 사라져 PlayMode 6건이 실패했다. P08~P15 화면을 의존 순서대로 재통합해 전체 PlayMode를 다시 통과했다.
3. 모든 후속 프리팹을 다시 생성하면 기능 변화 없이 file ID가 바뀌었다. 기존 프리팹을 재사용하고 장면 통합만 수행하도록 준비 경로를 분리했다.
4. 첫 RC는 Android 패키지 식별자 누락으로 실패했다. 설계·코드·CI에 `com.kingdomtycoon.game`을 고정해 재빌드했다.
5. RC 로그의 TMP 폐기 API 경고 18줄을 9개 호출부에서 제거하고 경고 0건으로 다시 빌드했다.

## 7. 데이터·Save·API·자산 영향

- DB migration: 없음. 적용된 Flyway migration checksum 변경 없음.
- Save schema/content version: 변경 없음. 기존 content.13과 Save schema 11 유지.
- 서버 API: 변경 없음.
- 사용자 표시 언어: 한국어. 코드 식별자·로그·CSV ID만 영어 유지.
- 신규 유료 자산: 없음. 실제 집행액 0원.
- 외부 게임은 품질 방향 참고만 사용했으며 시각 자산·문구·레이아웃을 복제하지 않았다.

## 8. 출시 전 남은 승인

- 실제 Android 기기에서 10분 이상 전투·메뉴 왕복 프로파일링: `DEVICE_QA_REQUIRED`
- 발열·메모리·60fps 목표 및 저사양 30fps 모드 체감 승인: `DEVICE_QA_REQUIRED`
- 스토어 서명 키 보관, 서명 빌드, 업로드: `OPS_LATER`
- 유료 자산 도입은 RC 체감 평가 후 별도 이슈와 사용자 승인으로만 진행한다.

P17의 코드·로컬 RC·자동 QA는 완료됐다. P18 기능 단계는 정의하지 않으며 다음 작업은 기기 QA와 출시 운영 승인이다.
