# P05 용병 로스터 구현 보고서

## 1. 변경 요약

- Phase: `P05_MERCENARY_ROSTER`
- GitHub Issue: `#18`
- 상태: **구현 완료**
- 기준 계약: `TYCOON_P05_MERCENARY_ROSTER_COMPLETE_DESIGN_v1.0`
- 활성 콘텐츠: `1.0.0-content.3`, manifest contract v2, CSV schema set v3

P05 canonical content `.3`, Save 생성·migration·복구 계약, 용병 조회·필터·정렬·활성 상태 변경,
가상화 로스터, 상세 drawer 4개 탭, 필터·정렬 modal, 오류·오프라인·복구 상태,
한글 TMP font, 수락 스크린샷 8종과 Android Development APK까지 연결했다.

## 2. 변경 파일

| 영역 | 주요 경로 |
|---|---|
| 설계 | `docs/design/TYCOON_P05_MERCENARY_ROSTER_COMPLETE_DESIGN_v1.0.md` |
| 콘텐츠 | `client-unity/Assets/StreamingAssets/Content/1.0.0-content.3`, `scripts/generate_p05_content.py` |
| Save·Application | `Runtime/Application/Mercenaries`, `Runtime/Application/Profiles/P05NewGameFactory.cs` |
| Domain·Infrastructure | `Runtime/Domain/Mercenaries`, `Runtime/Infrastructure/Mercenaries` |
| UI | `Runtime/Presentation/Mercenaries`, `Scenes/Bootstrap.unity` |
| 생성 자산 | `ContentGenerated/P05Mercenary`, `Assets/TextMesh Pro` |
| 에디터·빌드 | `P05MercenaryRosterSetup.cs`, `P05GeneratedAssetMarker.cs` |
| 테스트 | `P05MercenaryTests.cs`, `P05MercenaryRosterTests.cs` |
| CI·훅 | `.githooks/pre-commit`, `.githooks/pre-push`, `scripts/ci/run-ci.sh` |
| 검증 증거 | `docs/reports/captures/P05` |

## 3. 실행 명령

```powershell
python scripts/generate_canonical_content.py --check
python scripts/generate_p04_content.py --check
python scripts/generate_p05_content.py --check
python scripts/validate_content.py

& <UNITY_EDITOR>\Editor\Unity.exe -batchmode -quit -projectPath .\client-unity `
  -executeMethod KingdomTycoon.Editor.P05MercenaryRosterSetup.Run

& <UNITY_EDITOR>\Editor\Unity.exe -batchmode -projectPath .\client-unity `
  -runTests -testPlatform EditMode -testResults .\artifacts\test-results\p05-editmode.xml

& <UNITY_EDITOR>\Editor\Unity.exe -batchmode -projectPath .\client-unity `
  -runTests -testPlatform PlayMode -testResults .\artifacts\test-results\p05-playmode.xml

& <UNITY_EDITOR>\Editor\Unity.exe -projectPath .\client-unity `
  -runTests -testPlatform PlayMode `
  -testFilter KingdomTycoon.Tests.PlayMode.P05MercenaryRosterTests.P05_P_011_CaptureEightAcceptanceFixtures `
  -testResults .\artifacts\test-results\p05-captures.xml

& <UNITY_EDITOR>\Editor\Unity.exe -batchmode -quit -projectPath .\client-unity `
  -executeMethod KingdomTycoon.Editor.P05MercenaryRosterSetup.Build

$env:JAVA_HOME='<JDK_25>'
$env:PATH="$env:JAVA_HOME\bin;$env:PATH"
& 'C:\Program Files\Git\bin\bash.exe' scripts/ci/run-ci.sh
```

## 4. 테스트 결과

- P05 package: 62 tables, manifest JCS `67,270 bytes`, SHA-256 `352a947980b09b0dc5d5699a9367bd6e35cfb1296241784d4c52bc80df0a98e1`
- Localization CSV: 806 rows, SHA-256 `60feb5d4c780aaa52fc7808d206d690950883432e2608f95034050c88f5f1e18`
- Save requestHash golden: `8b19c3...`, P05 payload/envelope golden: `46a293...` / `a102...`
- Migration payload/envelope golden: `441d02...` / `ef10...`, schema hash: `451611...`
- Unity EditMode: `54 passed / 0 failed`
- Unity PlayMode 본 테스트: `12 passed / 0 failed / 1 skipped`; batch 환경에서 제외한 캡처 테스트는 별도 실행 `1 passed / 0 failed`
- Server JUnit/Testcontainers PostgreSQL 회귀: `10 passed`, Gradle `BUILD SUCCESSFUL`
- 공유 CI: `4/4 PASSED` (`16s`)
- Android Development APK: `71,216,412 bytes` (`67.92 MiB`)
- APK SHA-256: `51eaefa6374e79211839188690890467e9fcad7f4ee607936f2661e1174983fe`

## 5. 수동 검증

- Bootstrap에서 로스터 화면 생성, 4명·24명 데이터 렌더링 및 최대 16개 카드 가상화 확인
- 이름·등급·역할·상태·파견·정렬 조건, Unicode 20 scalar 검색 제한과 조건 초기화 확인
- 상세 drawer의 개요·장비·능력치·기록 탭, 용병 변경 시 개요 탭 복귀 확인
- 활성 인원 상한의 정확한 차단 사유, toast 위치, 로딩·오류·오프라인·복구 표시 확인
- 16:9 및 20:9 출력, Safe Area와 큰 글자 배율 대응 확인

| 캡처 | 해상도 | SHA-256 |
|---|---:|---|
| [4명 로스터](captures/P05/p05_01_roster_4.png) | 1920×1080 | `2c1761019eb1ce62baaad019ca7b7e8a6b3d05f72fe97665e48007b37110b633` |
| [24명 로스터](captures/P05/p05_02_roster_24.png) | 1920×1080 | `3797596adbbd5bebddf3755bbb9ffa869757bceff390c1fc72cd14690dab0acb` |
| [상세 개요](captures/P05/p05_03_detail_overview.png) | 1920×1080 | `f48659525a335ca2964224271453053e3346eb45d4a24d2b7228d2dccc782f42` |
| [상세 장비](captures/P05/p05_04_detail_equipment.png) | 1920×1080 | `1757d5a9e8615019acd4d8364d59e818d25d558f47402781a5c605814d023b43` |
| [필터](captures/P05/p05_05_filters.png) | 1920×1080 | `1c2bd1907c9b18f1e8b86ef5ade80500376d601568a1f819341ad7abb8a8bda1` |
| [활성 인원 상한](captures/P05/p05_06_active_limit.png) | 1920×1080 | `b874a30856275e6881e83ffb260948fae6fbad73f2ecd23d7bb6894a41dde212` |
| [20:9 Safe Area](captures/P05/p05_07_20x9.png) | 2400×1080 | `211a3e0b4fbadc7cd0d607c99da622de0046577dcdd7f2b15a672169c7c90410` |
| [복구 상태](captures/P05/p05_08_recovery.png) | 1920×1080 | `ba1993cb8566c9dcda43f2ccc92faae3fe6d2b2cbf4d8a364a327542c12a13b9` |

## 6. 명세와 차이

- P05 v1.0의 필수 계약과 수락 기준은 추가 설계 없이 구현했다.
- 프로젝트 고정 버전은 `6000.3.20f1 (c9ba695d4f07)`로 유지했다. 로컬 검증은 설치된 `6000.4.11f1`에서 수행했으며, Unity가 자동 변경한 package lock·ProjectVersion·ProjectSettings drift는 제거했다.
- 한글은 Noto Sans KR 기반 dynamic TMP font를 사용하고 `NotoSansKR-OFL.txt`에 SIL Open Font License를 포함했다.
- 서버 원장·실제 네트워크 동기화·용병 모집은 P05 설계 범위 밖이므로 로컬 Save 계약과 명시적 offline 상태까지만 구현했다.

## 7. 잔여 위험

- 정확한 고정 에디터 `6000.3.20f1`에서 EditMode·PlayMode·Android 빌드를 CI 또는 별도 빌드 머신에서 재현해야 한다.
- Development APK는 `67.92 MiB`다. 릴리스 전 managed stripping, 텍스처 압축, font atlas 및 Addressables 중복 분석이 필요하다.
- 실제 Android 단말의 Safe Area, 뒤로가기 우선순위, 한글 glyph, 저사양 기기의 24명 로스터 스크롤 성능은 물리 단말 검증이 남아 있다.
- 캡처용 디버그 상태 라벨 일부는 극단 비율에서 잘릴 수 있으나 사용자 UI와 입력 영역에는 영향을 주지 않는다.

## 8. 다음 Phase 진입 가능 여부

P05 콘텐츠·Save·UI·자동 테스트·수락 캡처·Android 개발 빌드 기준으로는 **P06 진입 가능**이다.
다만 저장소에는 P06의 완전 상세 설계가 없으므로, 사용자 지침에 따라 P06 구현을 시작하기 전에 ChatGPT에서 확정한 상세 설계서를 전달받아야 한다.
