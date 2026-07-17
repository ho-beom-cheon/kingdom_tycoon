# Unity 아키텍처

## 버전·패키지

- Unity 6.3 LTS latest patch at bootstrap, exact pin in `ProjectVersion.txt`
- Input System
- TextMeshPro
- Localization
- Addressables(local catalog)
- Unity Test Framework
- 2D Tilemap
- 2D Pixel Perfect
- Sprite Atlas

## 렌더링

- Built-in 또는 URP 2D 중 버티컬 슬라이스에서 결정
- 무료 픽셀 에셋 호환성과 모바일 성능을 우선
- 1.0은 고급 동적 조명에 의존하지 않음

## 계층

```text
Presentation
  Screen / View / Presenter / UI Component
Application
  UseCase / Command / Query / DTO
Domain
  Mercenary / Combat / Inventory / Facility / Economy / Region / Recruitment
Infrastructure
  Save / Network / Catalog / Localization / AnalyticsAdapter
```

## 씬

권장:

- Bootstrap
- Kingdom
- Region
- Raid

UI는 공통 AppRoot를 유지하고 씬 간 서비스 수명주기를 명시한다.

## 데이터

```text
CSV/JSON Source
→ Validator
→ Importer
→ Runtime Catalog/ScriptableObject
→ Domain Query
```

## 이벤트

도메인 이벤트 예:

- MercenaryReturned
- ItemObtained
- EquipmentChanged
- FacilityProductionStopped
- PromotionAvailable
- RegionUnlocked
- RaidCleared

UI는 Polling보다 이벤트 기반 업데이트를 사용한다.

## 경로 탐색

1.0 활동 용병 16명 기준으로 타일 기반 A*를 직접 구현하거나 Unity 호환 무료 솔루션을 검토한다.

- 요청 큐
- 경로 캐시
- 프레임 분산
- 막힘 재탐색
- 단순 로컬 회피

고가 경로 탐색 에셋은 실제 병목 확인 후 구매한다.
