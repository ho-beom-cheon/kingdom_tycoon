# P06 이동·전투·AI 구현 보고서

## 1. 변경 요약

- Phase: `P06_MOVEMENT_COMBAT_AI`
- GitHub Issue: `#20`
- 상태: **구현 완료**
- 기준 계약: `TYCOON_P06_MOVEMENT_COMBAT_AI_COMPLETE_DESIGN_v1.1`
- 활성 콘텐츠: `1.0.0-content.4`, 66 tables, Save v1 유지

P06 canonical content `.4`, P05→P06 Save migration과 transient 복구, 10 Hz 결정론 시뮬레이션,
SplitMix64 RNG, grid path/cache, 고정소수점 이동, 전투·타깃 선택·17-state autonomy,
R01 사냥 시작·귀환 원자 커밋, Region uGUI/TMP 화면, Addressables, 수락 캡처 8종과
Android Development APK를 연결했다.

## 2. 변경 파일

| 영역 | 주요 경로 |
|---|---|
| 설계 | `docs/design/TYCOON_P06_MOVEMENT_COMBAT_AI_COMPLETE_DESIGN_v1.1.md` |
| 콘텐츠 | `client-unity/Assets/StreamingAssets/Content/1.0.0-content.4`, `scripts/generate_p06_content.py` |
| Save·Application | `Runtime/Application/Combat`, `Runtime/Application/Profiles/P06NewGameFactory.cs` |
| Domain·Infrastructure | `Runtime/Domain/Combat`, `Runtime/Infrastructure/Combat`, `P05ToP06ContentMigration.cs` |
| UI | `Runtime/Presentation/Combat`, `Scenes/Region.unity` |
| 생성 자산 | `ContentGenerated/P06Combat`, `Content-P06-Combat-v1` Addressables group |
| 에디터·빌드 | `P06CombatSetup.cs`, `P06GeneratedAssetMarker.cs` |
| 테스트 | `P06CombatTests.cs`, `P06RegionCombatTests.cs`, `docs/goldens/P06` |
| CI·훅 | `.githooks/pre-commit`, `.githooks/pre-push`, `scripts/ci/run-ci.sh` |
| 검증 증거 | `docs/reports/captures/P06` |

## 3. 실행 명령

```powershell
python scripts/generate_p06_content.py --check
python scripts/validate_content.py

& <UNITY_EDITOR>\Editor\Unity.exe -batchmode -nographics -quit -projectPath .\client-unity `
  -executeMethod KingdomTycoon.Editor.P06CombatSetup.Run

& <UNITY_EDITOR>\Editor\Unity.exe -batchmode -nographics -projectPath .\client-unity `
  -runTests -testPlatform EditMode -testResults .\artifacts\p06-editmode-final.xml

& <UNITY_EDITOR>\Editor\Unity.exe -batchmode -nographics -projectPath .\client-unity `
  -runTests -testPlatform PlayMode -testResults .\artifacts\p06-playmode-final-3.xml

& <UNITY_EDITOR>\Editor\Unity.exe -batchmode -quit -projectPath .\client-unity `
  -executeMethod KingdomTycoon.Editor.P06CaptureGenerator.Run

& <UNITY_EDITOR>\Editor\Unity.exe -batchmode -nographics -quit -projectPath .\client-unity `
  -executeMethod KingdomTycoon.Editor.P06AndroidBuilder.Build

$env:JAVA_HOME='<JDK_25>'
$env:PATH="$env:JAVA_HOME\bin;$env:PATH"
.\gradlew.bat test
```

## 4. 테스트 결과

- P06 package: 66 tables, manifest SHA-256 `d541f924b230eb9b21e1e2a46fe9c5f30d72d75eddacef8cb2c05bd620151fb5`
- Localization CSV SHA-256: `215286bfe6b80a2ed19dc794af1bdc4d6d31e57bbcfc84a8e7b1be760dd47442`
- START/RECALL requestHash: `97f7c3d1...` / `027b3f13...`
- A_STAR_TIE: cost `60`, expanded `7`, insertions `10`, 동일 키 cache hit `expanded=0`
- BUILD_DERIVED SOAK_30M: `18,000 ticks`, `439 encounters`, `878 kills`, SHA-256 `999e3e084b1cd94f5706833383813ee9558fd8d92888c43252d795eda3a7c7bd`
- Unity EditMode: `63 passed / 0 failed`
- Unity PlayMode: `15 passed / 0 failed / 1 skipped`; batch에서 제외된 렌더 캡처는 별도 generator로 8개 생성 성공
- Git hook content gate: canonical/P04/P05/P06 package와 validator 모두 통과
- Server Gradle: **미실행**. 로컬에는 Java 17만 있고 프로젝트 toolchain은 Java 25이므로 `Cannot find a Java installation ... languageVersion=25`에서 중단됐다. CI workflow는 Java 25를 설치하도록 구성돼 있다.
- Android Development APK: `51,303,113 bytes` (`48.93 MiB`)
- APK SHA-256: `f819287385528fa9a14abefb2c667846a3737082f304712beb56d97718dcc7bd`

## 5. 수동 검증

- Bootstrap→Kingdom→Region 전환 후 영속 AppRoot·Input System EventSystem 단일성 확인
- 활동·IDLE_TOWN 용병 1~4명으로 R01 시작, revision +1과 `TRAVEL_TO_REGION` 저장 확인
- out-of-range 이동, 거리 경계, stable target tie, 전투 종료와 귀환 revision +1 확인
- 로딩·콘텐츠·빈 상태·오류·잠김·오프라인 6개 UI 상태와 64px 입력 영역 확인
- 16:9 및 20:9 RenderTexture 출력, 한글 TMP 표시와 화면 clip 부재 확인

| 캡처 | 해상도 | SHA-256 |
|---|---:|---|
| [R01 시작](captures/P06/p06_01_region_start.png) | 1920×1080 | `6417b4989fb55d4fee8d3d1682929d2c3700bc224786794d42b3fa96e500745d` |
| [경로](captures/P06/p06_02_path.png) | 1920×1080 | `afc5e24d8cdd05227b56351129c6aff0718f2d9fc79abe86fa49f1fe7231085e` |
| [전투](captures/P06/p06_03_combat.png) | 1920×1080 | `88503935d5450f5b340a89078a080d32060e9f13e24deaef0215a59825c4794d` |
| [가디언 도발](captures/P06/p06_04_taunt.png) | 1920×1080 | `6f52da71b1a9003310b9f9c673316d2029f332fc8f92de206b9a1228a5fc4868` |
| [성직자 회복](captures/P06/p06_05_heal.png) | 1920×1080 | `830804ec909964cc3723d079bc4857d20c660ce148c4fddd8c97219550050fa5` |
| [귀환](captures/P06/p06_06_recall.png) | 1920×1080 | `48c9b8de9f77b9cc83e24196d9219c0d1dce3d1abf402143e4d27ac5067dc772` |
| [오류](captures/P06/p06_07_error.png) | 1920×1080 | `db008b33937828a1bf33bfb33a14406997bc821f78a5b5df1edb19d1478fb536` |
| [20:9 Safe Area](captures/P06/p06_08_20x9.png) | 2400×1080 | `4325f8685748e922d0cf80f0d9599b3d39cacd907681f3381bd8cbcf76830aed` |

## 6. 명세와 차이

- 원본 manifest의 `combat_ai_profiles.csv.status` descriptor가 `SAFE_INT`와 `STATUS`로 중복돼 있었다. 원본 72,812-byte SHA를 먼저 검증한 뒤 잘못된 `SAFE_INT` descriptor 한 개만 제거해 실행 manifest를 만들었다. CSV bytes는 변경하지 않았다.
- manifest가 추가한 `POSITIVE_INT`, `SOFT_SENTINEL_EMPTY`를 공용 importer가 인식하도록 확장했다.
- A_STAR_TIE의 명시적 path·cost·expanded·insertions 골든을 우선했다. 내부 구현은 reverse distance field로 정확한 최단거리를 구하고 N/E/S/W forward frontier를 재현한다. 문서의 Manhattan heap 서술과 골든 step 사이 충돌은 실행 골든을 권위로 해석했다.
- 프로젝트 고정 버전은 `6000.3.20f1 (c9ba695d4f07)`로 유지했다. 로컬 검증은 설치된 `6000.4.11f1`에서 수행했고 Unity가 자동 변경한 package lock·ProjectVersion·ProjectSettings drift는 제거했다.
- P07 소유인 potion 소비·inventory mutation·loot settlement는 P06에서 수행하지 않는다. P06의 사냥 보상은 계약대로 개인 골드·기여도·기록만 원자 커밋한다.

## 7. 잔여 위험

- 고정 에디터 `6000.3.20f1` 및 Java 25가 함께 설치된 CI/빌드 머신에서 Unity·server 전체 회귀를 재현해야 한다.
- 개발 APK는 `48.93 MiB`다. 릴리스 전 managed stripping, 텍스처 압축, Addressables 중복과 symbol 산출물을 점검해야 한다.
- 실제 Android 단말에서 Safe Area, pause/background, thermal throttling, 30분 연속 사냥과 저사양 프레임을 물리 검증해야 한다.
- 현재 Region world는 기능 검증용 generated placeholder visual이다. 최종 아트·애니메이션·VFX 교체는 주소 계약을 유지한 채 후속 제작이 필요하다.

## 8. 다음 Phase 진입 가능 여부

P06 콘텐츠·Save migration·결정론 시뮬레이션·Region UI·자동 테스트·8개 캡처·Android 개발 빌드 기준으로 **P07 진입 가능**이다.
P07 v1.1 상세 설계서가 이미 전달되어 있고 `IMPLEMENTATION_READY` 계약을 확인한 뒤 바로 전리품·인벤토리·장비 구현으로 이어갈 수 있다.
