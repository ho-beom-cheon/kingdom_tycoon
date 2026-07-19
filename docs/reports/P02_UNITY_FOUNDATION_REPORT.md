# P02 Unity 기반 구현 보고서

## 1. 결론

- 상태: **구현 및 로컬 검증 완료**
- 기준 이슈: [#12 클라이언트: P02 Unity 기반과 공통 AppRoot 구축](https://github.com/ho-beom-cheon/kingdom_tycoon/issues/12)
- 작업 브랜치: `codex/issue-12-p02-unity-foundation`
- Unity: `6000.3.20f1 (c9ba695d4f07)`
- EditMode: 3 passed, 0 failed, 0 skipped
- PlayMode: 1 passed, 0 failed, 0 skipped
- 저장소 CI: 전체 통과, 서버 통합 테스트 10건 포함

P02 완료 조건인 AppRoot, 서비스 생명주기, 씬 흐름, Input, Localization, Addressables 로컬 설정, 공통 UI Root, EditMode/PlayMode 테스트 기반을 실제 Unity 프로젝트로 구축했다.

## 2. 버전 선택과 근거

2026-07-17 작업 시점의 Unity 6.3 LTS 최신 패치는 `6000.3.20f1`이며 릴리스 changeset은 `c9ba695d4f07`이다. 이 값을 `client-unity/ProjectSettings/ProjectVersion.txt`에 정확히 고정했다.

- Unity 릴리스: <https://unity.com/releases/editor/whats-new/6000.3.20f1>
- Unity CLI 사용법: <https://docs.unity.com/en-us/unity-cli/use-unity-cli>
- Java 25 로컬 검증 런타임: Eclipse Temurin `25.0.3+9`
- Temurin 릴리스: <https://adoptium.net/temurin/releases?version=25>

렌더 파이프라인의 최종 선택은 설계대로 미확정 상태를 유지했다. P02 부트스트랩은 `2D (Built-In Render Pipeline)` 공식 최소 템플릿으로 생성했으며, Built-in과 URP 2D의 최종 결정은 별도 수직 슬라이스에서 수행한다.

## 3. 패키지 잠금

| 패키지 | 버전 | 용도 |
|---|---:|---|
| `com.unity.addressables` | `3.1.0` | 로컬 카탈로그와 Localization 자산 그룹 |
| `com.unity.feature.2d` | `2.0.2` | Unity 2D 기능 묶음 |
| `com.unity.2d.pixel-perfect` | `5.1.1` | Pixel Perfect Camera |
| `com.unity.2d.tilemap` | `1.0.0` | 2D Tilemap |
| `com.unity.inputsystem` | `1.19.0` | 런타임 입력과 uGUI EventSystem |
| `com.unity.localization` | `1.5.12` | 한국어·영어 Locale 및 UI String Table |
| `com.unity.test-framework` | `1.6.0` | EditMode·PlayMode 테스트 |
| `com.unity.ugui` | `2.0.0` | uGUI와 TextMeshPro |

직접 의존성과 전이 의존성은 `Packages/manifest.json`과 `Packages/packages-lock.json`에 함께 고정했다.

## 4. 구현 구조

### AppRoot와 서비스

- `AppRoot`는 Bootstrap 씬에서 한 번 생성되고 `DontDestroyOnLoad`로 유지된다.
- 중복 AppRoot는 즉시 제거된다.
- `ServiceRegistry`는 타입별 단일 등록, 초기화 순서, 역순 종료를 보장한다.
- 현재 등록 서비스는 Input, Localization, Scene Flow다.
- MonoBehaviour는 조립과 Unity 수명주기만 담당하고 서비스 계약은 일반 C# 객체로 분리했다.

### 씬

- `Bootstrap`: AppRoot, 공통 UI Root, Input System EventSystem
- `Kingdom`: Pixel Perfect Camera와 TextMeshPro 샘플 화면
- `Region`: 다음 기능 단계용 빈 진입 씬
- `Raid`: 다음 기능 단계용 빈 진입 씬

Bootstrap은 시작 시 Scene Flow 서비스를 통해 Kingdom을 로드한다. PlayMode 테스트는 이후 Region으로 다시 이동해 AppRoot가 같은 인스턴스로 유지되는지 검증한다.

### 공통 UI Root

`docs/15_UI_UX_DESIGN_SYSTEM.md`의 계층을 반영했다.

- `WorldCanvas`
- `HudCanvas`
- `ScreenCanvas`
- `DrawerCanvas`
- `ModalCanvas`
- `ToastCanvas`
- `TutorialCanvas`
- `DebugCanvas`

각 Canvas는 1920×1080 기준 `Scale With Screen Size`를 사용하고 `SafeAreaFitter`를 가진다.

### Input·Localization·Addressables

- 공식 2D 템플릿 Input Actions를 `Assets/KingdomTycoon/Config/KingdomTycoon.inputactions`로 이동해 프로젝트 입력 기준으로 사용한다.
- 한국어와 영어 Locale을 만들고 한국어를 기본 Locale로 지정했다.
- `UI` String Table에 `screen.kingdom.title`의 한국어·영어 값을 추가했다.
- Addressables 기본 로컬 프로필과 Localization 전용 그룹을 생성했다.

### Assembly Definition

- `KingdomTycoon.Runtime`
- `KingdomTycoon.Editor`
- `KingdomTycoon.EditModeTests`
- `KingdomTycoon.PlayModeTests`

런타임, Editor 자동 구성, EditMode 테스트, PlayMode 테스트의 컴파일 경계를 분리했다.

## 5. CI 호환성 변경

Unity 6.3은 Visible Meta Files 값을 `ProjectSettings/VersionControlSettings.asset`에 기록한다. 기존 CI는 구형 `EditorSettings.asset` 위치만 검사했으므로 다음과 같이 보완했다.

- 구형 `m_ExternalVersionControlSupport`와 신규 `m_Mode` 형식을 모두 허용
- Unity `6000.3.20f1` 정확한 버전 확인
- changeset `c9ba695d4f07` 확인
- Force Text, Assets, manifest, `.meta` 누락 검사 유지

GitHub Actions에서는 Unity 라이선스가 필요한 Editor 테스트를 실행하지 않고 정적 프로젝트 정책만 검증한다. EditMode와 PlayMode는 로컬 또는 Unity 라이선스가 준비된 전용 러너에서 같은 명령으로 실행한다.

## 6. 실행 및 검증 명령

### 프로젝트 자동 구성

```powershell
& <UNITY_EDITOR>/Editor/Unity.exe `
  -batchmode -nographics -quit `
  -projectPath <REPOSITORY>/client-unity `
  -executeMethod KingdomTycoon.Editor.P02ProjectSetup.Configure
```

### EditMode

```powershell
& <UNITY_EDITOR>/Editor/Unity.exe `
  -batchmode -nographics `
  -projectPath <REPOSITORY>/client-unity `
  -runTests -testPlatform EditMode `
  -testResults <RESULTS>/editmode-results.xml
```

결과: `3 passed / 0 failed / 0 skipped`

### PlayMode

```powershell
& <UNITY_EDITOR>/Editor/Unity.exe `
  -batchmode -nographics `
  -projectPath <REPOSITORY>/client-unity `
  -runTests -testPlatform PlayMode `
  -testResults <RESULTS>/playmode-results.xml
```

결과: `1 passed / 0 failed / 0 skipped`

### Android Build Support

```powershell
& <UNITY_EDITOR>/Editor/Unity.exe `
  -batchmode -nographics -quit `
  -projectPath <REPOSITORY>/client-unity `
  -executeMethod KingdomTycoon.Editor.P02ProjectSetup.VerifyAndroidSupport
```

결과: Unity 내부 `BuildPipeline.IsBuildTargetSupported(Android)`가 `true`를 반환했다.

추가 확인:

- OpenJDK `17.0.18+8`
- Android Debug Bridge `36.0.0-13206524`
- Android NDK `r27c (27.2.12479018)`

### 저장소 전체 CI

```powershell
$env:JAVA_HOME = '<TEMURIN_25>'
$env:PATH = "$env:JAVA_HOME\bin;$env:PATH"
& 'C:\Program Files\Git\bin\bash.exe' scripts/ci/run-ci.sh
```

결과:

- Git Hook syntax: passed
- Content data validation: passed
- Server clean test: passed
- 서버 통합 테스트: 10 passed
- Unity project policy: passed
- 전체 소요 시간: 11초

## 7. 영향 분석

### Save·데이터·API

- Save 객체와 Migration을 변경하지 않았다.
- CSV 원본과 검증 규칙을 변경하지 않았다.
- 서버 API와 DB 계약을 변경하지 않았다.
- P03에서 사용할 런타임 저장소 인터페이스를 임의로 선행 구현하지 않았다.

### 성능

- PlayMode 부트스트랩 흐름 테스트는 약 0.71초에 완료됐다.
- 실제 모바일 프레임·메모리·로딩 성능 측정은 콘텐츠가 없는 P02 범위에 포함하지 않았다.
- Pixel Perfect Camera와 공통 Canvas 계층만 구성했으며 무거운 런타임 생성 로직은 추가하지 않았다.

## 8. 남은 위험과 다음 단계

### P02 잔여 위험

- Built-in과 URP 2D의 최종 선택은 아직 OPEN이다.
- GitHub 호스티드 러너에는 Unity 라이선스가 없으므로 Unity 실행 테스트는 전용 러너 구성 전까지 로컬 증거를 사용한다.
- 로컬 에디터는 관리자 권한 없는 사용자 경로에 설치했다. 다른 개발 환경에서는 Unity Hub 또는 Unity CLI로 같은 에디터·Android 모듈을 설치해야 한다.

### P03 진입 판단

Unity 기반은 P03 구현을 시작할 수 있는 상태다. 다만 다음 상세 설계가 확정되기 전에는 P03 Save·CSV 구현을 시작하면 안 된다.

- Save 객체의 정확한 구조와 필수·선택 필드
- Save schema version과 Migration 단계
- 원본·백업·임시 파일의 원자적 저장 및 복구 우선순위
- 손상·부분 기록·미래 버전 Save 처리 계약
- CSV field domain과 단위·범위·정밀도
- 빈 값·`null`·`0`·음수 등 sentinel 의미
- tagged-union discriminator와 variant별 허용 필드
- 알 수 없는 tag·필드의 실패 정책

제공된 DB 인터페이스 설계는 서버 저장과 콘텐츠 릴리스 기준을 보강하지만, 위 클라이언트 Save·CSV 상세 계약을 대체하지 않는다.
