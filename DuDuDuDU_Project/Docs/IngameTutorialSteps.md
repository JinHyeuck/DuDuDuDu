# 인게임 첫 튜토리얼 — 스텝 정의 (초안)

> 상태: **초안.** 하단 "결정 필요" 항목이 정해지기 전에는 구현하지 않는다.
> 대상: 클라 첫 오픈 시 진입하는 튜토리얼 전용 스테이지 1판.

## 가이드 종류

| 종류 | 입력 | 진행 |
|---|---|---|
| **Dim** (딤드) | 구멍(타깃 영역) 안만 입력 허용. 나머지는 차단 | 완료 조건 충족 시 다음 스텝 |
| **Finger** (손가락) | 전부 허용. 타깃에 손가락 애니만 | 완료 조건 충족 시 다음 스텝 |
| **Talk** (말풍선) | 화면 탭으로 넘김. 필요하면 `timeScale = 0` | 탭 1회 |
| **Wait** | 가이드 없음 | 게임 이벤트 대기 |

Dim 구멍은 **여러 개**를 지원해야 한다 — 머지 스텝은 칸 두 개를 동시에 열어야 한다.

## 스텝 enum

번호를 연번으로 정리했다(원안 1_1 → 2). 값은 **세이브에 들어가지 않으므로**(아래 "중단 처리") 자유롭게 바꿔도 된다.

```csharp
public enum TutorialStep
{
    None = 0,

    // ── 1웨이브 전: 소환 · 머지 ─────────────
    IntroGoal,          // Talk  "몬스터가 아래 벽까지 오면 져요"
    Summon,             // Dim   소환 버튼. SP 를 다 쓰면 완료 (6회)
    Merge,              // Dim   노멀 2개 칸. 드래그 머지 → 2성 번개
    StartWave1,         // Dim   웨이브 시작 버튼

    // ── 1웨이브 ────────────────────────────
    Wave1,              // Wait  웨이브 종료
    Wave1Reward,        // Talk  "웨이브를 깨면 SP 를 받아요"  ← 7번 SP 의 출처

    // ── 2웨이브 전: 현상금 ─────────────────
    BountyOpen,         // Dim   현상금 띠 → 선택 창 열기
    BountySelect,       // Dim   1단계 슬롯 선택
    StartWave2,         // Dim   웨이브 시작 (SP 남겨둔 채)

    // ── 2웨이브: 되돌리기 ──────────────────
    Wave2Crisis,        // Wait  몬스터 3마리 등장 후 정지
    RewindTalk,         // Talk  "현상금 몬스터가 세다! 되돌려서 보강하자"
    Rewind,             // Dim   되돌리기 버튼 → 확인 창 "되돌릴게요"
    SpendSp,            // Dim   소환 버튼. SP 다 쓰면 완료
    StartWave2Retry,    // Dim   웨이브 시작

    // ── 2웨이브(재): 딜미터기 ──────────────
    DamageMeterTip,     // Talk  딜미터기 말풍선. 정지 안 함 (비강제)
    Wave2Retry,         // Wait  웨이브 종료 + 1단계 현상금 처치 확인

    // ── 3웨이브 전: 진화(변신) ─────────────
    EvolveGift,         // Talk  "보스가 온다!" → 4성 번개 + 강화석 10 지급
                        //       "4성이 되면 변신할 수 있어요" (실제 규칙 고지)
    EvolveSelect,       // Dim   4성 번개 칸 탭 → 상세 패널 열림
    EvolveConfirm,      // Dim   진화 버튼 → Time 다이스
    EvolveClose,        // Dim   상세 패널 닫기

    // ── 손 놓기 ────────────────────────────
    SummonHint,         // Finger 소환 버튼. 소환 1회 이상 또는 웨이브 시작 시 완료
    BountyHint,         // Finger 현상금 2단계. 선택 또는 웨이브 시작 시 완료
    StartWave3,         // Finger 웨이브 시작

    // ── 보스 ───────────────────────────────
    BossWave,           // Wait  보스 처치
    Complete,           // 완료 플래그 저장 → 로비
}
```

## 스텝별 완료 조건과 훅

| 스텝 | 완료 조건 | 붙일 곳 (현 코드) |
|---|---|---|
| Summon / SpendSp | SP < 소환비용 | `BattlePointManager.OnBattlePointChanged(SummonPoint, …)` |
| Merge | 지정 2칸 머지 성공 | `MergeSystem.TryMerge` 성공 경로 — **이벤트 없음, 신설 필요** |
| StartWave* | `inGameState == Wave` 진입 | `GameManager.ChangeState` — **이벤트 없음, 신설 필요** |
| Wave1 / Wave2Retry | 웨이브 종료 → Setting 복귀 | 같은 `ChangeState` 이벤트 |
| BountySelect | 선택 등급 == 1 | `BountyManager.OnChanged` |
| Wave2Crisis | 웨이브 내 스폰 수 ≥ 3 | 스포너 — **카운트 이벤트 신설 필요** |
| Rewind | 되돌리기 완료 → Setting 복귀 | `WaveRewindManager.TryRewind` 성공 후 `ChangeState(Setting)` |
| Wave2Retry 의 현상금 처치 | 이번 웨이브 현상금 처치됨 | `BountyManager.OnWaveResolved` + 처치/퇴각 구분 |
| EvolveGift | 탭 (지급은 진입 시 1회) | 지급: `BattlePointManager.Add(EnhanceStone, 10)` + 보드에 4성 번개 배치 |
| EvolveSelect | 지정 칸 상세 패널 열림 | `UIBattleDiceDetailPanel` 열림 — **이벤트 없음, 신설 필요** |
| EvolveConfirm | 진화 성공 | `MergeSystem.TryEvolve` 성공 경로 — **이벤트 없음, 신설 필요** (머지 이벤트와 함께) |
| EvolveClose | 상세 패널 닫힘 | 같은 패널 이벤트 |
| BossWave | 스테이지 클리어 | `GameManager.ClearStage` |

이벤트 신설이 필요한 곳은 넷(머지·진화 성공, 상태 전환, 스폰 수, 상세 패널 열림/닫힘)이다. 튜토리얼 디렉터가 `.Instance` 를 새로 늘리지 않도록, 배틀 스코프에서 주입받는 형태로 만든다.

## 튜토리얼 중 강제로 고정해야 하는 것

전부 지금은 랜덤이다. "튜토리얼 활성 시 결과를 덮어쓰는 스크립트" 한 경로로 모은다.

| 대상 | 현재 | 고정값 |
|---|---|---|
| 소환 타입 | `UIDiceSummonSystem.cs:203` `Random.Range` | 노멀, 노멀, 노멀, 불, 얼음, 독 (순서 고정) |
| 소환 칸 | `GetRandomEmptySlot` | 지정 칸. **Merge 의 딤드 구멍이 칸 좌표를 알아야 하므로 필수** |
| 소환 별 | `RelicManager.RollSummonStar` | 1성 고정 |
| 쌍둥이 소환 | `RelicManager.TrySpawnTwinDice` | 끔 |
| 머지 결과 | `MergeSystem.cs:122` `GetRandomMergedType` | 번개(Thunder) |
| 머지 보험 | `RelicManager.ApplyMergeInsurance` | 끔 |
| 초기 SP | 스테이지 값 | 6회 소환분과 **정확히** 일치 (`summonsPerCostIncrease` 로 비용이 오르는 것 반영) |
| 1웨이브 보상 SP | 스테이지 값 | SpendSp 에서 쓸 양 |
| 되돌리기 | 횟수 차감, 광고 가능 | 무료·무차감 (`RewindSource` 에 Tutorial 추가 검토) |
| 4성 번개 배치 칸 | — | 지정 빈 칸. EvolveSelect 딤드 구멍 좌표 |
| 강화석 | 웨이브 클리어당 +1, 현상금 보상 | EvolveGift 에서 **보유량을 진화 비용(`DiceEvolution.BaseEvolveCost` = 10)으로 맞춘다.** +10 이 아니라 "맞춤"이라 앞 웨이브 수급이 바뀌어도 진화 후 0 이 된다 |
| Time 다이스 보유 | `TryEvolve` 가 대상 보유를 검사 | 미보유면 진화가 **조용히 실패**한다. EvolveGift 에서 영구 지급 (아래 "확정") |

**진화 성급 조건(4성)은 튜토리얼에서도 낮추지 않는다.** 판에서만 다른 규칙을 가르치면 일반 판에서 버그로 오해한다.
그래서 4성은 만들게 하지 않고 **지급**하며, 지급 시 말풍선으로 실제 규칙을 알린다.

**속성강화는 이 튜토리얼에서 뺐다.** 진화와 같은 강화석을 써서 한 판에 둘 다 가르치면 길고 수지가 꼬인다.
일반 판에서 처음 강화석이 강화 비용 이상 모였을 때 손가락 가이드로 가르친다(별도 문서).

유물 효과는 하나하나 끄기보다 **튜토리얼 판에서는 유물 적용 자체를 건너뛰는** 편이 싸다(첫 오픈이라 보유 유물이 없을 수도 있지만, 그것에 기대지 않는다).

## 수치 조건 (튜토리얼 스테이지 데이터)

- **Wave2Crisis 가 실제 위기여야 한다.** 1단계 현상금 몬스터가 SP 미사용 보드로는 벽까지 오는 수준.
- **Wave2Retry 는 반드시 현상금을 잡아야 한다.** 못 잡으면 2단계가 해금되지 않아 BountyHint 가 막힌다.
- **StartWave3 이후는 비강제라 유저가 아무것도 안 해도 이겨야 한다.** 보스 HP 를 그 기준으로 잡는다.
- **단, 변신한 Time 다이스가 눈에 띄게 일해야 한다.** "변신해서 이겼다"가 이 판의 끝 기억이다.
  보스 웨이브 딜미터기 1위가 Time 이 되도록 보스 HP·Time 기여를 맞춘다.
- 웨이브는 총 3개(1 / 2 / 보스). 원안의 "웨이브 실행 → 종료"가 이 셋에 대응한다.

## 중단 처리

- 튜토리얼 판 도중 앱 종료 → **다음 실행 시 튜토리얼을 처음부터.** 스텝을 세이브에 쓰지 않는다.
- 완료 플래그는 `Complete` 에서 로비로 넘어가기 직전에 한 번만 저장.
- 플레이는 TitleScene 부터 들어오므로 진입 판정은 Title → Battle 라우팅에서 한다.

## 결정 필요

1. **되돌리기 남은 횟수 표시** — 튜토리얼에서 설명할지, 숨길지.
2. **딜미터기(DamageMeterTip)** — 넣을지, 뺄지. 강제 구간이 길어서 첫 번째 삭제 후보.
3. **스킵 버튼** — 둘지. 둔다면 완료 플래그만 세우고 로비로.
4. **튜토리얼 판 보상** — 일반 스테이지 보상을 줄지, 고정 보상을 줄지. 스태미나 소모 여부.
5. **"상위 현상금"** — 2단계로 확정해도 되는지.
## 확정

- **스테이지 14 는 KingThunder(신화석 다이스)가 받는다.** 탑 140층에서 옮겼다. Time 의 진화 대상이라
  튜토리얼에서 받은 Time → KingThunder 로 길이 이어진다. **탑 140층 해금 자리는 비었다.**

- **변신 대상은 번개 → Time.** 머지 스텝의 2성 번개와 계통이 이어진다.
- **Time 은 튜토리얼 보상으로 영구 해금한다.** `DiceUnlockDatabase` 에 `tutorialReward` 경로를 추가했고
  Time 이 그것 하나를 갖는다. 원래 경로(스테이지 14)는 뺐다 — 모든 유저가 이미 보유하므로
  그 자리는 언제나 환급만 나온다. 가격(360)은 환급액이라 남겼다.
  - 조회: `DiceUnlockDatabase.GetTutorialUnlock()`
  - 검사: 튜토리얼 보상은 **하나**, **특수 다이스**여야 한다(`Validate`)
  - "획득 미션" 진행 버튼은 튜토리얼 경로를 갈 곳으로 치지 않는다(`HasRevisitableSource`)
- **지급 시점은 EvolveGift 진입 시.** `TryEvolve` 가 대상 보유를 보므로 진화 전에 보유여야 한다.
  지급은 `DiceOwnershipManager.GrantFromContent` 로 한다(이미 보유면 가격만큼 환급 — 세이브 초기화 경로).
  "변신한 다이스를 얻었다" 연출은 판 끝(`Complete`)에서 보여 준다.
