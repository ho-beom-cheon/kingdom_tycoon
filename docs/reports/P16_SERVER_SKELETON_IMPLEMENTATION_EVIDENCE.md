# P16 서버 골격 구현 증적

> 기준 이슈: #45
> 구현 브랜치: `codex/issue-45-p16-server-skeleton`
> 결과: 완료 (`PASS`)

## 1. 변경 요약

- 기존 V001~V017 PostgreSQL 스키마를 유지하고 개발 세션, 지갑 조회, 특별 모집 REST API를 추가했다.
- `traceId`, UTC 서버 시각, 한국어 오류 메시지, Bearer 개발 세션, `Idempotency-Key` 공통 계약을 구현했다.
- Unity 특별 모집 게이트웨이를 주입 가능하게 바꾸고 `MOCK_ONLY`와 비동기 `SERVER` 어댑터를 명시적으로 분리했다.
- SERVER 모드 timeout·HTTP 오류·응답 오류가 로컬 모집으로 대체되지 않도록 했다.
- P16 정적 CI 게이트를 공용 파이프라인에 연결했다.

## 2. 주요 변경 파일

- `server-api/src/main/java/com/kingdomtycoon/server/api/`
- `server-api/src/main/java/com/kingdomtycoon/server/session/DevelopmentSessionService.java`
- `server-api/src/main/java/com/kingdomtycoon/server/summon/SummonReceiptQueryService.java`
- `client-unity/Assets/KingdomTycoon/Runtime/Infrastructure/Recruitment/P16ServerRecruitmentGateway.cs`
- `client-unity/Assets/KingdomTycoon/Runtime/Infrastructure/Recruitment/RecruitmentInfrastructure.cs`
- `server-api/src/test/java/com/kingdomtycoon/server/P16ServerApiIntegrationTest.java`
- `client-unity/Assets/KingdomTycoon/Tests/EditMode/P16ServerAdapterTests.cs`
- `scripts/ci/p16.sh`

## 3. 실행 명령

```powershell
$env:JAVA_HOME='C:\Users\cjs41\IdeaProjects\kingdom_tycoon\.tools\jdk25-extracted\jdk-25.0.3+9'
.\gradlew.bat :server-api:test --rerun-tasks

Unity.exe -batchmode -nographics -projectPath client-unity -runTests -testPlatform EditMode -testResults artifacts/p16-editmode.xml
Unity.exe -batchmode -nographics -projectPath client-unity -runTests -testPlatform PlayMode -testResults artifacts/p16-playmode.xml
Unity.exe -batchmode -nographics -quit -projectPath client-unity -executeMethod KingdomTycoon.Editor.P15AndroidBuilder.Build -logFile artifacts/p16-android-build.log

"C:\Program Files\Git\bin\bash.exe" scripts/ci/p16.sh
```

## 4. 자동 검증 결과

| 검증 | 결과 |
|---|---|
| PostgreSQL 18.4 Testcontainers 서버 테스트 | 13/13 PASS |
| P16 API 세션·인증·지갑·모집·멱등 테스트 | 3/3 PASS |
| Unity EditMode | 132/132 PASS |
| Unity PlayMode | 40 PASS, 1 의도적 Skip, 0 Fail |
| P16 정적 CI gate | PASS |
| Android ARM64 IL2CPP | PASS |

Android 산출물:

- 경로: `client-unity/Builds/Android/KingdomTycoon-P15-Development.apk`
- 크기: 76,429,963 bytes
- SHA-256: `6DAAA9AF902FACAC47D07115FD51D7BA115DC9D678019718E2D0E6EEA713112F`

파일명은 기존 P15 빌더를 재사용했지만 P16 소스가 포함된 최신 APK다. 빌드 산출물은 Git에 포함하지 않는다.

## 5. 수동·계약 검증

- 동일 개발 기기는 같은 외부 player UUID를 재사용하고 새 token 발급 시 이전 token을 폐기한다.
- 무인증·폐기 token은 지갑 API에서 401로 거부된다.
- 동일 모집 멱등 키와 payload는 같은 receipt를 반환하고 원장·모집 transaction은 한 건만 남는다.
- 같은 멱등 키의 다른 payload는 409 `IDEMPOTENCY_CONFLICT`다.
- Unity HTTP 어댑터는 operation UUID를 멱등 키로 보내고 서버 wallet/pity/result를 검증한 뒤 Save에 반영한다.

## 6. 데이터·Save·API 영향

- DB migration: 없음. 적용된 Flyway checksum 변경 없음.
- Save schema/content version: 변경 없음.
- API: `/api/v1/dev/sessions`, `/api/v1/players/me/wallet`, `/api/v1/players/me/recruitments/special` 추가.
- 플레이어 표시 언어: 한국어 유지. 내부 식별자와 오류 code만 영어 사용.

## 7. 남은 위험과 P17 이관

- 실제 플랫폼 로그인/JWT, 운영 배포, rate limit은 `OPS_LATER`다.
- Android 빌드 로그에 일부 TMP Label의 Font Asset 미지정 경고와 Android Localization App Info 미설정 경고가 남았다. P17에서 화면 전체 Font 연결과 Android localization metadata를 검증한다.
- APK가 P15 기준선보다 커졌으므로 P17에서 build report 기준 크기·managed stripping·불필요 preload를 분석한다.
- 실제 서버의 운영 content release는 아직 빈 bootstrap release다. P16은 서버 골격 단계이며, 운영 데이터 배포는 승인된 콘텐츠 배포 절차로 별도 수행한다.

## 8. 다음 단계 진입 여부

P16 필수 계약과 검증은 완료되었다. P17 밸런스·최적화·전체 QA·시각 일관성·Android RC 단계로 진입 가능하다.
