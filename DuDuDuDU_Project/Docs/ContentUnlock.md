# 컨텐츠 해금

> 상태: **구현됨.** 코드는 `Assets/Scripts/ContentUnlock/`, 규칙은 `OJ.Core.ContentUnlockRules`.
> 정본 수치: `Assets/ScriptableObject/ContentUnlockDatabase.asset` (없으면 `ContentUnlockRules.BuildDefaults`).
> 순서 합의: 2026-10-13.

인게임 튜토리얼(Docs/IngameTutorialSteps.md)이 전투 시스템(소환·머지·현상금·되돌리기·속성강화·진화)을
전부 가르치므로, 해금 대상은 **로비 컨텐츠뿐**이다.

## 알리는 방법 3단계

| 방식 | 하는 것 | 언제 쓰나 |
|---|---|---|
| **Guide** | 해금 연출 → 손가락으로 입구를 **실제로 누르게** 하고 → 열린 화면 위에서 한 마디 | 안 하면 성장이 막히는 것, 화면이 복잡해 혼자 못 쓰는 것 |
| **Announce** | 입구가 튀어나오고(0→1.3→1) 딤드로 그 자리만 연 채 "무엇이 열렸다" 를 말한다 | 있는 줄 몰라 놓칠 것 |
| **Quiet** | 입구만 나타난다. 알림은 입구 자신의 레드닷 | "받기" 버튼 하나뿐인 것 |

원칙:
1. **한 로비 진입에 가이드는 하나.** 대기열은 가이드에서 끊기고 나머지는 다음 진입에 나온다.
   그래서 같은 클리어 안에서는 **연출을 가이드보다 표에서 앞에** 둔다.
2. **결제 상품은 가이드하지 않는다.** 연출까지만.
3. **받기를 쓰기보다 먼저 연다** (고기 축제 → 소탕).

## 순서표

| 클리어 | 컨텐츠 | 방식 | 비고 |
|---|---|---|---|
| 0 (튜토리얼 직후) | 다이스 | Guide | 튜토리얼 보상 Time 다이스를 보러 간다 |
| 1 | 스테이지 보상 | Quiet | |
| 1 | 장비 | Guide | |
| 2 | 별의 축제 | Quiet | 별이 쌓인 뒤 열어야 바로 받을 게 있다 |
| 2 | 방치 보상 | Announce | 리텐션 장치라 일찍 |
| 3 | 고기 축제 | Announce | 받기 |
| 3 | 소탕 | Guide | 쓰기 |
| 3 | 특별한 7일 | Announce | 가이드 뒤라 **다음 진입**에 나온다 |
| 4 | 퀘스트 | Announce | 미션은 전부 열어 둔다(숨기지 않는다) |
| 4 | 핀볼 | Guide | |
| 5 | 멤버십 | Quiet | 결제 상품 |
| 5 | 상점 (보석 뽑기) | Guide | |
| 5 | 시즌 패스 | Announce | 결제 상품. 상점 가이드 뒤라 다음 진입 |
| 7 | 유물 | Guide | |
| 10 | 무한의 탑 | Guide | 킹 다이스 해금처 |

## 구조

| 파일 | 역할 |
|---|---|
| `OJ.Core.ContentUnlockRules` | 열림·보임·대기열·옛 세이브 채우기·표 검사·기본표. 헤드리스 테스트 대상 |
| `ContentUnlockDatabase` (SO) | 표를 들기만 한다. `StaticResource.ContentUnlockDatabase` |
| `ContentUnlockManager` | 루트 서비스. **연출을 봤는가**만 저장한다. 열림은 매번 `StageProgressManager.GetLastClearedStageIndex` 로 계산 |
| `LobbyContentGate` | 로비 입구 컴포넌트를 찾아 숨기고(`SetActive`), 하단 탭은 잠근다(흐림 + 자물쇠). `LobbySceneController.Awake` 가 코드로 붙인다 |
| `LobbyUnlockDirector` | 로비 `Start` 에서 대기열을 돈다. 화면은 튜토리얼의 `UITutorialOverlay` 를 그대로 쓴다 |

**열린 것과 보이는 것은 다르다.** Announce/Guide 는 열려도 연출을 끝까지 보기 전에는 입구가 숨는다 —
연출이 입구를 튀어나오게 하는데, 이미 보이면 무엇이 새로 생겼는지 알 수 없다. 가이드 도중에 앱을 끄면
기록이 안 남아 다음 진입에 처음부터 다시 나온다.

**잠긴 탭을 누르면** 탭을 넘기지 않고 "N스테이지를 클리어하면 열려요" 알림 창(`UIConfirmDialog`, 버튼 하나)을 띄운다.

**특별한 7일 자동 팝업**은 연출이 남은 진입에서는 쉰다(소비하지 않으므로 다음 진입에 뜬다).
숨은 입구는 `Start` 가 안 돌아 자동 팝업도 안 뜬다.

## 세이브

`SaveState.ContentUnlock` — `initialized`, `announced`(ContentId 이름, 오름차순).

- **이 기능보다 먼저 생긴 세이브**(`initialized` 없음)는 이미 열린 것을 본 것으로 채운다 —
  안 그러면 10스테이지를 깬 사람에게 연출 열 개가 쏟아진다. 단 **한 판도 안 깬 세이브는 채우지 않는다**
  (튜토리얼만 마친 신규 유저라 다이스 가이드를 받아야 한다).
- 채우기는 표(SO)가 필요해 `ReadFrom` 이 아니라 **처음 물을 때** 한다. 그 전에 저장이 오면
  `initialized=false` 를 그대로 다시 쓴다 — `SaveService` 의 왕복 검사가 읽은 것과 쓴 것을 문자열로 대조한다.
- `ContentId` 이름을 바꾸면 그 연출이 이미 본 사람에게 다시 뜬다.

## 개발

- 에셋: `OJ/개발/컨텐츠 해금/데이터베이스 에셋 만들기` (이미 있으면 검사만). 검사: `…/데이터베이스 검사`.
- DEV 치트 → 해금 연출 기록 초기화: 열린 것들의 연출이 다음 로비 진입에 다시 나온다.
- 숫자를 바꾸면 `ContentUnlockRulesTests.DefaultsMatchAgreedOrder` 와 이 문서를 같이 고친다.
