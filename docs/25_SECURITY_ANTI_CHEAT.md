# 보안·치팅 경계

## 가정

모바일 클라이언트와 로컬 Save는 변조 가능하다.

## 서버 권한

- 프리미엄 재화
- 특별 모집
- 천장
- 결제
- 이벤트 보상
- 운영 클라우드 Save 버전

## 클라이언트 방어

- Save checksum은 우발 손상 탐지용이며 강한 보안으로 간주하지 않음
- 시간 조작 탐지 로그
- 비정상 오프라인 간격 상한
- 데이터 버전 검사
- Development cheat UI는 Release Build에서 제거

## API

- TLS는 운영 필수
- 토큰 로그 금지
- Rate Limit
- 입력 검증
- 멱등성
- 재화 원장
- 감사 이벤트

## 비밀값

- 저장소 커밋 금지
- GitHub Secrets 또는 운영 Secret Manager
- Android Keystore 별도 보관
