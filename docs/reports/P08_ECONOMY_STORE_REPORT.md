# P08 경제·상점·자율 거래 구현 보고서

## 1. 완료 요약

- Phase: `P08_ECONOMY_STORE`
- GitHub Issue: `#28`
- 상태: **구현·자동 검증·Android 개발 빌드 완료**
- 기준 계약: `TYCOON_P08_ECONOMY_STORE_COMPLETE_DESIGN_v1.0`
- 활성 콘텐츠: `1.0.0-content.6`, 75 tables, Save v1 content.6 schema
- 다음 단계 진입: **가능**. P09 상세 설계가 구현 준비 상태인지 먼저 확인한다.

P08은 개인 골드와 왕국 금고, 상점 재고·가격 정책, 판매·구매·시스템 보급 명령,
거래 원장·operation journal, 사냥 귀환 후 자율 판매·회복 물약 확보·장비 개선 구매를 하나의
저장 계약에 연결한다. Kingdom 화면에는 상점 진입점과 6개 상태를 갖는 모바일 UI를 추가했다.

## 2. 구현 전 계약 기계 검증 보정

설계의 의미 규칙은 그대로 채택했고, 재현 불가능한 production bytes 두 곳만 생성기에서 명시적으로 보정했다.

| ID | 발견 내용 | 적용 규칙 |
|---|---|---|
| P08-M01 | 한국어 localization 원문 한 행에 literal comma가 있어 raw CSV column 수가 달라짐 | RFC 4180 writer로 해당 field를 인용하고 생성 결과 hash를 고정 |
| P08-M02 | 7단계 Save schema composition의 실제 결과가 문서 기재 62,977 bytes/hash와 불일치 | 재현 결과 62,967 bytes, SHA-256 `6a776d04768d6d92a23177efe61c9a2359760840d57caefcc57e1b08b1f31980`를 registry에 등록 |

기존 계약의 P08-C01~C05 의미 보정, command DTO, 가격 bps, journal type, migration 의미는 변경하지 않았다.

## 3. 주요 구현

| 영역 | 경로 및 내용 |
|---|---|
| 콘텐츠 | `StreamingAssets/Content/1.0.0-content.6`, `scripts/generate_p08_content.py` |
| Save | content.6 schema, schema registry, P07→P08 migration, P08 new-game template |
| Domain·Application | wallet, stock, pricing, ledger, command/result DTO, quote·replay 계약 |
| Infrastructure | 원자 판매·구매·정책·보급, 귀환 이벤트 자율 거래, deterministic operation ID |
| 기존 시스템 연동 | P07 legacy 판매 차단, loot content version 갱신, equipment canonical ID 검증 보정 |
| UI | `Runtime/Presentation/Store`, 24개 가상 행, 64 px 최소 터치, 6개 화면 상태 |
| 생성 자산 | `ContentGenerated/P08Store`, Addressables `Content-P08-Store-v1`, 12개 내부 자산 |
| Scene | `Kingdom.unity` 상점 화면과 facility drawer 진입 버튼 |
| Editor·Android | setup/verifier/capture generator, `P08AndroidBuilder.Build` |
| 테스트 | `P08EconomyTests.cs`, `P08StoreScreenTests.cs`, P05·P07 회귀 보강 |
| CI/CD | `scripts/ci/p08.sh`, repository pipeline 연결, P08 증빙 업로드 |

상점 UI는 P08에서 유료 에셋을 도입하지 않고 목재·석재·불빛·금색 강조 팔레트로 계층을 정리했다.
선택·활성·품절·잠김·오류 상태의 색 대비, 확인 전 가격·재고 정보, 20:9 Safe Area를 우선했다.

## 4. 콘텐츠·Save 계약

- content manifest SHA-256: `9d34821afd6bfaba3dbbf3b4d570ea1519b1d20b47a0075f1e54eb22d65eb8f8`
- localization CSV SHA-256: `38a0884460a1d8d49aec952e9d1ebadc721a43d29c77494ce9698093702fb8bc`
- content.6 Save schema SHA-256: `6a776d04768d6d92a23177efe61c9a2359760840d57caefcc57e1b08b1f31980`
- package fingerprint: `18ee50a41dc195b509a484d58c5441d1c94ad8caf1da6d17f060ea4f48e2c6fc`
- 생성 테이블: 75개
- P08 command·migration golden: `docs/goldens/P08`

P07 Save는 기존 content.5 schema로 계속 읽을 수 있고, P07→P08 migration이 economy 기본 구조를 추가한다.
P08 명령은 `expectedRevision → requestHash/replay → domain validation → mutation → ledger → journal → save`
순서를 따르며 성공 명령만 revision을 증가시킨다. 같은 operation ID와 같은 payload는 replay하고,
payload가 바뀌면 거부한다. 거래 실패 중간 상태는 저장하지 않는다.

## 5. 경제·자율 거래 규칙

- 판매: 소유권·town 상태·수량·상점 capacity를 검사한 뒤 inventory 제거, stock 추가, 개인 골드 지급을 원자 반영한다.
- 구매: quote context, 가격, 재고, 개인 골드, inventory capacity를 다시 검사하고 개인 골드 차감과 왕국 금고 적립을 함께 반영한다.
- 가격 정책: `LOW`, `STANDARD`, `HIGH`를 integer bps로 계산해 부동소수점 차이를 제거한다.
- 보급: 최초 상점 개장과 누적 구매 8건마다 deterministic epoch operation으로 target 미만 재고를 채운다.
- 자율 거래: 귀환 후 불필요 전리품 판매 → 회복 물약 보충 → 적격 장비 개선 구매 순서를 사용한다.
- 한 cycle은 최대 48개 명령이며, 동일 cycle ID 재호출은 이미 적용된 명령 수만 반환한다.
- 6개 성향 규칙을 모두 적용하고, 16명 동시 정산 회귀에서 음수 골드·재고와 revision drift가 없음을 검증했다.
- ledger는 저장 크기 상한을 위해 최대 200개 항목 계약을 유지한다.

서버 API와 PostgreSQL schema 변경은 없다. P08 경제는 현재 로컬 Save 권위이며 기존 서버 wallet·ledger 기반선과
충돌하지 않는다. 서버 권위 전환은 별도 Phase에서 API 계약과 migration을 확정해야 한다.

## 6. 자동 검증 결과

| 검증 | 결과 |
|---|---|
| `python scripts/generate_p08_content.py --check` | 75 tables 및 4개 hash 통과 |
| `bash scripts/ci/p08.sh` | P08 콘텐츠·entrypoint·12 captures gate 통과 |
| `bash scripts/ci/run-ci.sh` | hook/content/P07/P08/server/Unity policy 전체 통과 |
| Unity EditMode 전체 | **80 passed / 0 failed / 0 skipped** |
| Unity PlayMode 전체 | **22 passed / 0 failed / 1 skipped** |
| Server Java 25 + PostgreSQL 18 integration | **10 passed / 0 failed** |
| Android build | Unity `6000.3.20f1`, IL2CPP ARM64, exit code 0 |

PlayMode skip 1건은 batch 환경에서 실행하지 않는 기존 P05 rendered-capture fixture다. P08 캡처는
GPU RenderTexture 전용 generator로 별도 생성하고 실제 픽셀을 확인했다. 통합 CI에서 발견한 P07 검사기의
후속 registry 비호환은 P07 entry 보존 여부를 검사하도록 보정해 `.5`와 `.6`을 함께 검증한다.

주요 재현 명령:

```powershell
python scripts/generate_p08_content.py --check
& 'C:\Program Files\Git\bin\bash.exe' scripts/ci/p08.sh
& 'C:\Program Files\Git\bin\bash.exe' scripts/ci/run-ci.sh
```

Unity 테스트·캡처·빌드는 프로젝트 고정 에디터 `6000.3.20f1 (c9ba695d4f07)`에서 실행했다.

## 7. Android 개발 빌드

- Entry: `KingdomTycoon.Editor.P08AndroidBuilder.Build`
- Output: `client-unity/Builds/Android/KingdomTycoon-P08-Development.apk`
- Target: Development, IL2CPP, ARM64
- Size: `52,140,797 bytes` (`49.73 MiB`)
- SHA-256: `96e7fabf7b94519ff2566cc33296ddab36b48742c7508365ae51436735a1bb4b`

APK는 `.gitignore` 대상이라 커밋하지 않는다. Unity Android SDK의 `adb devices` 결과 연결 기기가 없었으므로
설치, 실제 터치, Android Back, pause/background, 기기별 Safe Area는 릴리스 승인 전 실기기에서 확인해야 한다.

## 8. 화면 증빙

12개 캡처는 내부 생성 자산만으로 렌더링했으며 16:9와 20:9를 포함한다.

| Capture | 해상도 | SHA-256 |
|---|---:|---|
| [상점 기본](captures/P08/p08_01_store_content.png) | 1920×1080 | `7f2ddbdbc0fa9f1d0180f1a22922bc667596a1118760bfa2a3f149dc1e5d8557` |
| [전리품 판매](captures/P08/p08_02_sell_loot.png) | 1920×1080 | `05f28f925e32307f42dc5dc761cffa6fc4e4136735c7f774ff1bf947b31c7daa` |
| [장비 비교](captures/P08/p08_03_equipment_compare.png) | 1920×1080 | `4f251848c678e61ad37e76181dc08481a38e886ee6d0859ba8b535b074ecdcc8` |
| [물약 구매](captures/P08/p08_04_potion_buy.png) | 1920×1080 | `44cc423b9cc272c822d5a32c3d4ba987d1f53c53e2d6fbff837317ed3db1ef06` |
| [가격 정책](captures/P08/p08_05_policy.png) | 1920×1080 | `372159ae723a8b9f1016262e67ee52df4bc70a355da55a4e67e8f7ba8ef8a6bb` |
| [거래 결과](captures/P08/p08_06_result.png) | 1920×1080 | `035010df6855a419a21359821ec377d8124ccc9fa8e7763ea530bf4133af3252` |
| [빈 상태](captures/P08/p08_07_empty.png) | 1920×1080 | `09726e2fa38875314f776d8a7c7f57a1f39fcb53510cccb7b04c38868e1fcc5e` |
| [상인 미배치](captures/P08/p08_08_merchant_missing.png) | 1920×1080 | `4b178893703614944dbb4392c12353e924d9ec5967836c6b9400ad59254c48ab` |
| [잠김](captures/P08/p08_09_locked.png) | 1920×1080 | `ca4cc4a55a607e42938a4b9780d722a66181c5d19e32268931ba0573ab2cc1e2` |
| [오류](captures/P08/p08_10_error.png) | 1920×1080 | `f0bf0f78acea2e7e573da14880404df3ef497808509519f20095f33e63753204` |
| [20:9 Safe Area](captures/P08/p08_11_20x9.png) | 2400×1080 | `2a88ed2f00632481a4271aebdb624a6a70dc88f4658edbbb365d510bb7bc6e2a` |
| [자율 정산](captures/P08/p08_12_autonomy.png) | 1920×1080 | `c989f8d94b657b1b0ab90b7c5f1ebe9bd1140bbf1fe866cd76e9c466e35d0ad1` |

## 9. 성능·운영·잔여 위험

- 상품 목록은 24개 row pool을 재사용해 전체 목록 GameObject 생성을 피한다.
- 자율 거래의 cycle당 48 command 상한과 ledger 200 entries 상한으로 한 번의 귀환 처리와 Save 성장을 제한한다.
- UI 자산은 현재 내부 생성 placeholder이므로 상용화 판단 뒤 유료 에셋으로 교체할 수 있다. address와 stable ID를
  고정해 시각 자산 교체가 경제 로직·Save 계약을 바꾸지 않도록 분리했다.
- 실제 중저가 Android 기기의 frame pacing, 메모리, 발열, 터치·Safe Area는 아직 측정하지 않았다.
- P09가 치유·부상·소모품 사용 의미를 확장한다면 P08 potion 구매 우선순위와 P09 상태 경계를 상세 설계에서 고정해야 한다.

## 10. 다음 작업

P08 완료 조건인 경제 루프 무정지, 음수 방지, 원장 일치, 테스트 실패 0, Android 개발 빌드를 충족했다.
다음 구현 지점은 **P09**다. 구현 전에 P09 standalone 상세 설계가 Save migration, P08 자율 구매 경계,
실패 코드, 골든, UI·Android 수용 기준까지 확정했는지 검토하고 부족하면 수정 요청서를 한 번에 작성한다.
