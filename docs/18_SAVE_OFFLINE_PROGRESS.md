# 저장·오프라인 진행

## 로컬 저장

필수:

- saveVersion
- contentVersion
- profile
- kingdom
- mercenaries
- facilities
- inventory
- regions
- recruitmentMockState
- tutorial
- settings
- lastSaveUtc
- checksum

## 저장 안전성

- 임시 파일 작성 후 원자적 교체
- 최근 3개 백업
- 로드 실패 시 이전 백업 시도
- Save Migration
- Development Build에서 저장 진단 화면

## 오프라인 정산

1.0 기준 최대 8시간(TUNABLE).

정산 대상:

- 일반 사냥 경험치·현상금·재료
- 포션 소비
- 시설 생산
- NPC 숙련
- 부상 회복
- 승급 심사 시간

제한:

- 레이드 자동 실행 없음
- 특별 모집 없음
- 최초 발견·최고 품질은 보수적 처리
- 오프라인 사망 대신 위험도에 따른 효율 감소

## 서버 전환

클라우드 세이브는 `ISaveRepository` 교체로 연결한다. 프리미엄 재화와 모집 상태는 로컬 Save를 권한 원본으로 사용하지 않는다.
