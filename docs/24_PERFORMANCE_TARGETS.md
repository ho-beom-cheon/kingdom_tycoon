# 성능 목표

## 기준

- Android 우선
- 60 FPS 목표, 저사양 30 FPS 허용 모드
- 왕국 활동 용병 16명
- 지역 전투 용병·몬스터 합계 기준 40~60개체

## 프레임 예산

- 메인 Thread 16.6ms 목표
- GC Alloc 지속 발생 최소화
- AI·경로 요청 분산
- UI 전체 Rebuild 금지
- 드롭·이펙트·토스트 Object Pool

## 메모리

- Sprite Atlas
- Addressables 로컬 그룹
- 지역 전환 시 불필요 Asset 해제
- 대형 원본 PSD/Aseprite는 런타임 포함 금지

## 측정

- Unity Profiler
- Memory Profiler
- Frame Debugger
- Android Development Build

## 완료 기준

- 왕국 최대 활동 인원에서 10분 플레이 시 누적 메모리 지속 증가 없음
- 지역 최대 개체에서 입력·UI가 멈추지 않음
- 목록 100개에서 심각한 Scroll 끊김 없음
