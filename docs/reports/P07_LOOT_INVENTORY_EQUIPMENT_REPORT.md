# P07 전리품·인벤토리·장비 구현 보고서

## 1. 변경 요약

- Phase: `P07_LOOT_INVENTORY_EQUIPMENT`
- GitHub Issue: `#22`
- 상태: **구현 완료**
- 기준 계약: `TYCOON_P07_LOOT_INVENTORY_EQUIPMENT_COMPLETE_DESIGN_v1.1`
- 활성 콘텐츠: `1.0.0-content.5`, 68 tables, Save v1 조건부 schema

P07은 전투 종료 시점의 단일 전리품 정산, 전역 인벤토리와 용병별 포션 소유권, 장비 인스턴스·장착 링크,
직업별 장비 보정, 자동 장착·자동 판매 정책, 수동 이동·장착·판매 명령을 구현한다. 모든 영속 변경은
expected revision, request hash, journal, RFC 8785 결과 digest와 원자 저장 계약을 따른다.

## 2. 주요 구현

| 영역 | 경로 및 내용 |
|---|---|
| 설계 | `docs/design/TYCOON_P07_LOOT_INVENTORY_EQUIPMENT_COMPLETE_DESIGN_v1.1.md` |
| 콘텐츠 | `StreamingAssets/Content/1.0.0-content.5`, `scripts/generate_p07_content.py` |
| Save | `save.content.5.schema.json`, schema registry, P06→P07 migration/new-game golden |
| Domain·Application | `Runtime/Domain/Inventory`, `Runtime/Application/Inventory` |
| Infrastructure | `Runtime/Infrastructure/Inventory`, 전투 terminal settlement 연동 |
| UI | `Runtime/Presentation/Inventory`, `Scenes/Inventory.unity` |
| 생성 자산 | `ContentGenerated/P07Inventory`, `Content-P07-Inventory-v1` Addressables group 27개 |
| Editor·Android | `P07InventorySetup.cs`, `P07AndroidBuilder.Build`, capture generator |
| 테스트 | `P07InventoryTests.cs`, `P07InventoryScreenTests.cs` |
| CI·훅 | `scripts/ci/p07.sh`, `run-ci.sh`, pre-commit, pre-push, GitHub Actions evidence upload |

## 3. 콘텐츠·Save 계약 검증

- 설계 문서 SHA-256: `52f4717e5b9a11204c0389d21adc32d99046dc7a25c2cba440c518791f000a3c`
- 원본 manifest: 74,609 bytes, SHA-256 `69f79179b2dac1f57d2e59bc079426434d0ae975d24cd2a86efebdd7963ee866`
- 실행 manifest: 74,538 bytes, SHA-256 `692922c62883a7da9520b7d9bc586b451f5b94b7461e3909dbca23e1cfcecef4`
- localization CSV SHA-256: `a0a784d762d2fda6710480050843f6cbfa539c27cf67801f7b3296db8919db10`
- content.5 Save schema: 53,081 bytes, SHA-256 `d306ae8c05d11f9225bfb988db3d3388f56177807b176ed7e44caa9ee6bc8392`
- `.4` Save는 P07 전용 field를 거부하고 `.5` Save는 확장된 정책·장비 구조를 요구한다.
- P06→P07 migration은 정책 기본값과 용병별 소형 회복 포션 2개를 추가하고 transient autonomy를 복구한다.

원본 manifest에는 `combat_ai_profiles.status` field에 `SAFE_INT`와 `STATUS` descriptor가 중복돼 있었다.
Generator는 원본 bytes/hash를 먼저 검증한 후 P06과 동일한 보정 규칙으로 잘못된 `SAFE_INT` 한 건만 제거한다.
CSV bytes와 의미 값은 변경하지 않으며 원본·실행 manifest hash를 모두 고정한다.

## 4. 자동 검증 결과

- P07 package gate: 68 tables 및 manifest/localization/Save hash 통과
- 전체 content validator: Save schema 40 definitions·2 outputs 및 참조 무결성 통과
- Unity EditMode: **72 passed / 0 failed**
- Unity PlayMode: **18 passed / 0 failed / 1 skipped**
  - skip 1건은 batch 환경에서 실행하지 않는 기존 캡처 테스트이며 별도 GPU capture generator로 검증했다.
- 30분 논리 soak: 18,000 ticks, deterministic 재실행 일치, encounter persistent write 0,
  hunt terminal당 settlement journal/write 정확히 1회, warm-up 이후 memory growth 8 MiB 이하
- 100-row inventory query: editor reference p95 3 ms 이하, refresh allocation 96 KiB 이하
- UI: 안정 ID 15개 각각 1개, Addressables 27개 중복 0, 모든 Button 64×64 이상
- Git pre-commit: canonical/P04/P05/P06/P07 generator, validator, 금지 경로, Unity meta gate 통과
- `scripts/ci/run-ci.sh`: hook syntax와 content/P07 gate까지 통과. 로컬에는 Java 25 toolchain이 없어 server 단계에서 중단됐다.
  GitHub Actions는 Temurin Java 25를 설치하도록 구성돼 있다.

## 5. Android 빌드

- Entry: `KingdomTycoon.Editor.P07AndroidBuilder.Build`
- Output: `client-unity/Builds/Android/KingdomTycoon-P07-Development.apk`
- Target: Development, IL2CPP, ARM64
- BUILD_DERIVED size: `51,688,870 bytes` (`49.29 MiB`)
- SHA-256: `a796f1a3bf9974ab3a824fc6a86b006fa3c5c3700e86026eebc9ba07293bf5fb`

APK는 `.gitignore` 대상이라 커밋하지 않았다. 실기기 Android Back, Safe Area, pause/background, 한국어 표시와
30분 실제 플레이 smoke는 연결된 Android 기기가 없어 수행하지 않았으며 릴리스 승인 전 수동 확인이 필요하다.

## 6. 화면 증빙

10개 캡처는 GPU RenderTexture 경로로 생성하고 실제 픽셀과 한국어 표시를 육안 확인했다.

| Capture | 해상도 | SHA-256 |
|---|---:|---|
| [아이템](captures/P07/p07_01_items.png) | 1920×1080 | `0bf5369d0f7d947dd4939ea31af5aee65375ba39ff2ad9d85ed104165cf7418f` |
| [장비](captures/P07/p07_02_equipment.png) | 1920×1080 | `dfc719d40084570a1355810115475ecc901d1ff7f7bdd5702ac0ff54be8b21d1` |
| [비교](captures/P07/p07_03_compare.png) | 1920×1080 | `6f243f68838a20b76cc617548102c9512da4d0f829a7d53bf3c6ba08be42c97d` |
| [자동 장착](captures/P07/p07_04_auto_equip.png) | 1920×1080 | `d02b6fe9172943f8fea57fa8e9a69de7cf4f66a228b4764e8b91fb168e22e4ef` |
| [정책](captures/P07/p07_05_policy.png) | 1920×1080 | `ccc86b0cc39cf44a2f2c7251bf569c1ce1f90d87973746423f71b7d6245a2555` |
| [판매 확인](captures/P07/p07_06_sale_confirm.png) | 1920×1080 | `fcf371eb3b1398b240689708b4fa4b09ae14419bf38476564feb9d84fb1995f1` |
| [전리품](captures/P07/p07_07_loot.png) | 1920×1080 | `8938d646cff9c421e03668ef486fcd51eefc8acdf195fd9c4a4f0bc88489f547` |
| [빈 상태](captures/P07/p07_08_empty.png) | 1920×1080 | `af169b16bcb922e97d4b1eff5e3d54d3ec7051cd92c059d497dd1e67a8b381b5` |
| [오류](captures/P07/p07_09_error.png) | 1920×1080 | `eb3cb629b84495266fa29863dda62956efbb0c4a5855db74b7e193553ba29857` |
| [20:9 Safe Area](captures/P07/p07_10_20x9.png) | 2400×1080 | `2fa7880e48771cbb8658e60b9520a7343a50b1a99899881733a2a3a983aa1b6f` |

## 7. 환경 차이와 잔여 위험

- 저장소는 Unity `6000.3.20f1 (c9ba695d4f07)`에 고정돼 있지만 로컬 검증은 설치된 `6000.4.11f1`로 수행했다.
  Unity가 자동 변경한 package lock, ProjectVersion, ProjectSettings, linker drift는 모두 제거했다.
- CI의 일반 runner는 Unity 라이선스를 보관하지 않는다. `P07_RUN_UNITY=1`과 `UNITY_EDITOR`가 설정된 승인된 runner에서
  EditMode·PlayMode·Android build를 동일한 `scripts/ci/p07.sh`로 재현할 수 있다.
- 현재 전리품 생성은 P07 계약 검증용 R01 material과 첫 성직자 무기 흐름을 구현한다. P08 이후 경제·상점 연동 시
  판매 가격·상점 재고와 정책 결과를 같은 journal 경계에서 확장해야 한다.

## 8. 다음 Phase 진입 조건

P07 구현·자동 검증·캡처·Android 개발 빌드 기준선은 완료됐다. 다음 구현 지점은 **P08 경제·상점·거래**다.
다만 현재 저장소에는 P08 구현에 필요한 standalone 상세 설계서가 없으므로, 가격 권위·상점 재고 갱신·구매/판매 명령·
Save field·migration·실패 코드·골든·UI·Android 수용 기준이 확정된 P08 상세 설계서를 먼저 받아야 한다.
