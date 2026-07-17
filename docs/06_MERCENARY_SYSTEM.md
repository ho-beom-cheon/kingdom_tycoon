# 용병 시스템

## 용병 데이터

- 고유 Instance ID
- 이름·외형 Seed
- 기본 등급 C~SS
- 직업
- 성장 랭크
- 레벨·경험치
- 능력치·성장 Seed
- 특성·성격
- 개인 골드
- 장비 4부위
- 포션 인벤토리
- 현재 상태·지역·행동 이유
- 전투·수집·레이드 기록
- 승급 진행도

## 활동과 보유

- 시작 활동 인원: 4
- 1.0 최대 활동: 16
- 1.0 최대 보유: 24
- 숙소와 왕국 단계가 슬롯을 제한한다.
- 비활동 용병은 회복, 승급 심사, 훈련 대기만 수행하며 일반 사냥 수익을 만들지 않는다.

## 상태 머신

```text
IdleTown
Prepare
TravelToRegion
FindTarget
Combat
Loot
ContinueDecision
ReturnTown
SellLoot
Heal
BuyConsumables
EvaluateEquipment
BuyEquipment
PromotionReady
PromotionProcess
Injured
RaidReady
```

## 자율 행동

용병이 수행:

- 사냥 지역 선택
- 몬스터 목표 선택
- 전투
- 포션 사용
- 위험 시 귀환
- 장비 비교·착용
- 재료·장비 판매
- 포션·장비·치료 구매

플레이어가 수행:

- 지역 출입 정책
- 시설·생산·재고 정책
- 가격 정책
- 승급 승인과 왕국 지원 재료
- 레이드 파티·부위 목표
- 희귀 장비 보호·자동 판매 정책

## 장비 평가

직업별 가중치로 `EquipmentScore`를 계산한다.

```text
Score = BasePower
      + PrimaryStat × JobWeight
      + SecondaryStat × JobWeight
      + Survivability × RiskWeight
      + RefineOptionValue
      + SetOrBossValue
      - PricePenalty
```

용병은 새 장비 점수가 현재 장비보다 `upgradeThreshold` 이상이고 구매 가능할 때 교체한다. 희귀 보호 설정된 장비는 자동 판매하지 않는다.

## 귀환 판단

다음 중 하나:

- 체력 비율이 성격별 기준 미만
- 포션 부족
- 인벤토리 90% 이상
- 장비 내구도 임계치 미만(내구도 기능을 활성화할 경우)
- 지역 예상 생존 점수 부족
- 플레이어 강제 귀환

1.0 기본은 장비 내구도를 비활성화해 관리 부담을 줄인다.
