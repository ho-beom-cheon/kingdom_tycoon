# P04 왕국 시설 구현 보고서

## 1. 변경 요약

- Phase: `P04_KINGDOM_FACILITIES`
- GitHub Issue: `#16`
- 상태: **구현 완료**
- 기준 계약: P04 v1.0 + v1.0.1 + v1.0.2 정정 부록
- 활성 콘텐츠: `1.0.0-content.2`, manifest contract v2, CSV schema set v3

P04 콘텐츠 패키지, 신규 게임/프로필 생성, P03→P04 signature migration, 시설 건설·업그레이드·수령,
관리 NPC 배치·해제, 저장 복구·READY 정규화, 왕국 uGUI 화면, 에디터 생성 자산과 Android 개발 빌드까지 연결했다.

## 2. 변경 파일

| 영역 | 주요 경로 |
|---|---|
| 설계 | `docs/design/TYCOON_P04_KINGDOM_FACILITIES_COMPLETE_DESIGN_v1.0.2_CORRECTION_APPENDIX.md` |
| 콘텐츠 | `client-unity/Assets/StreamingAssets/Content/1.0.0-content.1`, `1.0.0-content.2` |
| 생성기 | `scripts/generate_p04_content.py`, `scripts/generate_canonical_content.py` |
| 런타임 | `Runtime/Application`, `Runtime/Domain/Facilities`, `Runtime/Infrastructure/Facilities` |
| Save | `LocalProfileLocator`, `SingleProfileCreator`, `SaveDocumentValidator` |
| UI | `Runtime/Presentation/Kingdom`, `Scenes/Kingdom.unity` |
| 자산 | `ContentGenerated/Kingdom/P04`, `Content-P04-Kingdom-v1` Addressables 그룹 |
| 에디터/빌드 | `P04KingdomAssetGenerator.cs`, `P04PlayerBuild.cs` |
| 테스트 | `P04FacilityTests.cs`, `P04KingdomScreenTests.cs` |
| CI/훅 | `.githooks/pre-commit`, `.githooks/pre-push`, `scripts/ci/run-ci.sh` |

## 3. 실행 명령

```powershell
python scripts/generate_canonical_content.py --check
python scripts/generate_p04_content.py --check
python scripts/validate_content.py

& <UNITY_EDITOR>\Editor\Unity.exe -batchmode -projectPath .\client-unity `
  -executeMethod KingdomTycoon.Editor.P04KingdomAssetGenerator.GenerateAndVerify

& <UNITY_EDITOR>\Editor\Unity.exe -batchmode -projectPath .\client-unity `
  -runTests -testPlatform EditMode -testResults .\artifacts\test-results\p04-editmode-final2.xml

& <UNITY_EDITOR>\Editor\Unity.exe -batchmode -projectPath .\client-unity `
  -runTests -testPlatform PlayMode -testResults .\artifacts\test-results\p04-playmode.xml

& <UNITY_EDITOR>\Editor\Unity.exe -batchmode -quit -projectPath .\client-unity `
  -executeMethod KingdomTycoon.Editor.P04PlayerBuild.BuildAndroid

$env:JAVA_HOME='<JDK_25>'
$env:PATH="$env:JAVA_HOME\bin;$env:PATH"
& 'C:\Program Files\Git\bin\bash.exe' scripts/ci/run-ci.sh
```

## 4. 테스트 결과

- P03 canonical package: 60 tables, manifest SHA-256 `7b385938bcd120988abb4157401e535a0cf3c09c146cb60f859819efebadfa8b`
- P04 package: 62 tables, manifest SHA-256 `668a4d4084d903e0a1dfb6394ed62287f3dc3a3f3c1fcc1d2d81d4f2af537c63`
- requestHash RFC8785 SHA-256 golden 5종: 일치
- Unity EditMode: `27 passed / 0 failed`
- Unity PlayMode: `2 passed / 0 failed`
- Server JUnit/Testcontainers PostgreSQL 회귀: `10 passed`, Gradle `BUILD SUCCESSFUL`
- 공유 CI: `4/4 PASSED`
- Android Development APK: 빌드 성공, `44,165,340 bytes` (`42.12 MiB`)
- APK SHA-256: `94c81750ea22a3b657a74850ea0d279b500bcd08267eb7edb93234b18e191042`

## 5. 수동 검증

- Bootstrap → Kingdom 씬 전환과 영속 `AppRoot` 유지 확인
- `.2` 콘텐츠 로드 후 8개 시설 View 바인딩 확인
- FAC_STORE 건설 → 시간 경과 → READY → 수령 → STOPPED → NPC 배치 ACTIVE → 해제 STOPPED 저장/재로드 확인
- 8개 prefab의 `BaseSprite/StateOverlay/StoppedIcon/Label/HitTarget` hierarchy와 13개 Addressables address 확인
- 물리 Android 단말 설치, 16:9~20:9 Safe Area, 한글 TMP glyph는 아직 수동 단말 검증하지 않음

## 6. 명세와 차이

- 프로젝트 고정 버전은 `6000.3.20f1 (c9ba695d4f07)`로 유지했다. 로컬에 해당 에디터가 없어 실제 Unity 테스트와 APK 빌드는 설치된 `6000.4.11f1`에서 수행했고, 자동 변경된 package lock과 ProjectVersion은 커밋 범위에서 제거했다.
- P04의 서버 원장·HTTP, 실제 생산/제작/치료 보상, 모집·상점 기능은 설계대로 후속 Phase로 남겼다.
- 물리 단말 스크린샷과 배포 서명은 P04 자동 검증 범위에 포함하지 않았다.

## 7. 남은 위험

- 정확한 고정 에디터 `6000.3.20f1`에서 EditMode/PlayMode/Android 빌드를 CI 또는 별도 빌드 머신에서 한 번 더 재현해야 한다.
- Development APK는 `42.12 MiB`이며, 릴리스 전 managed stripping·심볼 분리·텍스처 압축·Addressables 중복 분석으로 추가 최적화할 수 있다.
- 한글 표시용 TMP font asset과 Android Safe Area/뒤로가기 우선순위는 실제 단말에서 확인해야 한다.
- `.creating` 프로필 스테이징은 같은 볼륨의 atomic directory move를 사용하지만, 모바일 파일시스템 전원 차단 내구성은 실제 단말 fault-injection 검증이 남아 있다.

## 8. 다음 Phase 진입 가능 여부

P04 기능·자동 테스트·Android 개발 빌드 기준으로는 **P05 진입 가능**이다. 다만 저장소에는 P05 용병 로스터의 완전 상세 설계가 없으므로,
사용자 지침에 따라 P05 구현을 시작하기 전에 ChatGPT에서 확정한 상세 설계서를 전달받아야 한다.
