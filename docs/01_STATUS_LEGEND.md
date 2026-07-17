# 상태 표기와 변경 통제

## 상태

| 상태 | 처리 |
|---|---|
| CONFIRMED | 1.0 구현 필수 |
| TUNABLE | 데이터로 구현하고 테스트로 조정 |
| DEFERRED | 인터페이스도 필요하지 않으면 구현하지 않음 |
| OPS_LATER | 연결 지점만 분리하고 운영 구현은 보류 |
| REFERENCE_ONLY | 아이디어 참고만 가능 |
| REJECTED | 구현 금지 |
| UNRESOLVED | 보고 후 결정 전까지 보류 |

## 변경 통제

구조적 변경은 다음을 남긴다.

- 변경 이유
- 영향 시스템
- Save/API/Data 호환성
- 1.0 일정 영향
- 대안
- 결정자와 결정일

밸런스 값 변경은 CSV와 `balance_parameters.csv`에서 수행하고 코드 변경을 요구하지 않아야 한다.
