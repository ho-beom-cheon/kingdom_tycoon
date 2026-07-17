# UI/UX 디자인 시스템

## 시각 방향

따뜻한 다크 판타지 픽셀아트.

- 왕국: 목재, 석재, 불빛, 생활감
- 사냥터: 저채도 위험 환경
- 장비·재료·성장 보상: 높은 대비와 채도
- 월드는 픽셀아트, UI 텍스트는 고해상도

## 화면

- 모바일 가로
- 기준 1920×1080
- 16:9~20:9
- Safe Area
- 월드 관찰을 가리지 않는 Drawer 중심

## 폰트

- 본문·수치·버튼: Pretendard
- 대체: Noto Sans KR
- 짧은 지역명·보스 경고·승급: Galmuri 계열 선택 사용
- 폰트 파일은 패키지에 포함하지 않음
- 라이선스 등록 후 프로젝트에 추가

## 내비게이션

하단 최대 6개:

1. 왕국
2. 용병
3. 제작
4. 지역
5. 모집
6. 메뉴

잠긴 기능은 초반에 숨기고 개방 직전 진행도를 보여준다.

## UI 계층

```text
AppRoot
├─ WorldCanvas
├─ HudCanvas
├─ ScreenCanvas
├─ DrawerCanvas
├─ ModalCanvas
├─ ToastCanvas
├─ TutorialCanvas
└─ DebugCanvas
```

모달은 동시에 하나만 허용한다.

## 공통 상태

- Loading
- Content
- Empty
- Error
- Locked
- Offline

빈 화면은 원인과 다음 행동을 함께 제공한다.

## 접근성

- 색상 + 아이콘·형태
- 텍스트 크기 기본/크게
- 화면 흔들림 단계
- 진동 끄기
- 빠른 점멸 최소화
- 효과음 없이 상태 인지 가능
