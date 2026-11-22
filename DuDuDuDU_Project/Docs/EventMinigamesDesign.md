# 이벤트 미니게임 3종 (낚시 · 보물찾기 · 래프팅)

> **상태: 구현됨 (2026-11-23).** 뚜까펫(비공식 팬 가이드 PDF)의 규칙·점수·보상 방식을 **혼자 하는 형태**로 가져왔다.
> 이름·그림은 따오지 않는다. 합의 과정과 숫자의 근거는 계획 기록 `plans/3-purrfect-seahorse.md` 에 있다.

## 한눈에

| 항목 | 값 |
|---|---|
| 주기 | 3일 × 3종 = 9일. 한국시간 09:00 = UTC 0시 경계(`EventCurrencyRules` 의 UTC 날짜 번호를 그대로 쓴다) |
| 재화 | 핀볼이 주는 `EventCurrency1~3` = 낚싯대 / 곡괭이 / 보트. 열린 이벤트 것만 나와 이벤트당 약 600개 |
| 소모 | 재화를 쓴 만큼이 **승급 미션 B 진행도**다. `EventMinigameManager` 한 곳에서만 쓰고 거기서 `PromotionManager.NotifyEventCurrencySpent` 를 부른다 (`OnPointSpent` 를 구독하지 않는다 — 종료 환급도 소모로 잡힌다) |
| 보상 | 쓴 재화 가치(20원)의 **평균 10배**. 장부 단가는 `EventLedger`(0.1원 단위) |
| 상점 | 이벤트 재화 250개 5,000원은 **하루 1회**(`ShopDatabase`, 에셋 포함) |
| 회차 리셋 | 회차(`dayIndex / 3`)가 바뀌면 점수·진행도를 비운다. 고르지 않은 3택1 상자는 첫 묶음으로 자동 지급. 낚시 도감 기록만 남는다 |
| 랭킹 | **보류** (이벤트별 3일 랭킹을 못 만드는 솔루션). 낚시 골드 코인도 같이 보류 |

## 코드 지도 (`Assets/Scripts/EventMinigame/`)

- 규칙(static, UnityEngine 없음, 난수는 `roll` 인자): `EventMinigameRules` · `FishingRules` · `TreasureHuntRules` · `RaftingRules` · `EventRewardTables` · `EventLedger`(+`EventBundle`) · `EventSeededRandom` · `EventMinigameText`
- 상태·지급·저장: `EventMinigameManager` (`SaveState.EventMinigames`)
- 화면: `UIEventMinigameDialog`(공통) → `UIFishingPage` · `UITreasureHuntPage` · `UIRaftingPage`, 입구 `UIEventMinigameLobbyButton`, 줄 `UIEventMilestoneRow`
- 굽기: 에디터 메뉴 `OJ/개발/이벤트/화면 3종 굽기` → `OJ/개발/다이얼로그 카탈로그/훑어서 갱신` → `OJ/개발/이벤트/로비 입구 설치` (멱등. 연속으로 누르지 말고 스피너가 멈춘 뒤)
- 테스트: `Assets/Tests/EditMode/Game/{EventMinigameRules,FishingRules,TreasureHuntRules,RaftingRules,EventLedger,EventMinigameManager,EventMinigameText}Tests.cs`

## 게임별 규칙

- **낚시**: 표식이 일정 속도로 왕복하는 게이지에서 던지는 순간의 위치가 타이밍(퍼펙트 30% · 굿 50% · 미스 20% — 장부의 평균 가정과 같다). 한 마리를 낚고 **점수가 배율(×1/×10/×100)배**. 등급 일반/희귀/영웅/전설, 낚싯대 레벨(점수 1만/3만/7만에 Lv2~4)이 오를수록 좋은 등급 확률이 오른다(평균 85/95/105/115점). 떡밥은 낚싯대 1개당 0.3%, 10분 행운 +1(최대 4). 도감은 어종별 최고 무게 기록과 "상위 N%" 연출만, 보상 없음. 어종 이름은 **임시**.
- **보물찾기**: 8단계 판(5×5 ~ 8×8, 3스테이지마다 단계 상승), 칸당 곡괭이 1/2/3/4개. 남은 보물 실루엣을 보여 주고 확률은 보여 주지 않는다. 스테이지 클리어당 보상, 열쇠는 3스테이지마다, 상자는 열쇠 4개(12스테이지)마다 — 카피는 상자에만.
- **래프팅**: 룰렛 1/2/3/4/5/무료5/8/20m(가중치 2/20/12/12/22/10/12/10). 무료 칸은 보트 없이 같은 배율로 이어 돈다. 보트 1개당 평균 97/15 ≈ 6.47m. 큰 마일스톤 6곳(1,000/2,500/5,000/10,000/15,000/20,000m)은 가치가 같은 3택1 상자.
- 마지막 마일스톤을 넘긴 진행도는 재화 3개 상당 = 핀볼 1발로 바뀐다(소수점 버림).

## 알려진 사항

- 화면은 시안이 없어 단색 판이다. 그림이 생기면 `UIEventMinigameFactory` 의 색 자리만 바꾸면 된다. 폰트(BM HANNA)에 `×` 글자가 없어 배율은 `x10` 으로 적는다.
- 핀볼 하루 가치는 이 콘텐츠로 약 +29% 늘어난다(10배 보상). 사용자가 추가 콘텐츠로 인정했다.
- 상점 한도는 기기 현지 날짜로 리셋되고 이벤트는 UTC 기준이라 날짜가 어긋날 수 있다("하루 1회"는 지켜진다).
- 이벤트 열림 판정(`EventCurrencyRules.IsEventOpen`)은 아직 항상 true 다. 서버 이벤트 시스템이 서면 날짜 계산만 바꾼다.
