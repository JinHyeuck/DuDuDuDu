# 멤버십 (광고제거권 · 고기 멤버십) — 구현 기록

> **상태: 적용 완료 (2026-10-06).** TitleScene 부터 재생해 확인 — 로비 좌상단 입구 → 창, 고기 멤버십 구매 →
> "잔여 30일"·입장료 "무료"·소탕 "멤버십 무제한", 광고제거권 구매 → 카드에 "보유 중", 둘 다 보유해도 입구 유지,
> 잔여 2.5일 → 입구 D-3 배지·재구매 버튼, 무료 유저 하루 한도 72 → 0 이면 소탕 버튼 꺼짐.

기획 정본: `Docs/BMDesign.md` 4·5장(상품), `Docs/ShopPackageDesign.md` 2.2·5장(화면). 레퍼런스: 사용자 스샷(VIP 상점).

## 무엇이 어디에 있나
| 것 | 자리 |
|---|---|
| 규칙(기간·연장·D-n·입장료·소탕 한도) | `Scripts/Core/MembershipRules.cs` · 테스트 `Tests/EditMode/MembershipRulesTests.cs` |
| 상태(만료 시각·오늘 소탕 수) | `SaveState.Membership` · 소유자 `Scripts/Save/EntitlementManager.cs` (광고제거권과 같은 소유자) |
| 수치(가격·기간·재구매 구간·무료 소탕 한도) | `ShopDatabase.Membership` (SO) |
| 창 | `Scripts/Shop/UIMembershipDialog.cs` → `Prefab/UI/UIMembershipDialog.prefab` |
| 로비 입구 | `Scripts/Shop/UIMembershipLobbyButton.cs` → `Prefab/Lobby/UIMembershipLobbyButton.prefab`, LobbyScene 좌상단 |
| 굽기·설치 | 메뉴 `OJ/개발/멤버십/창 굽기`, `OJ/개발/멤버십/로비 입구 설치`(입장 버튼 "x5" 글자도 잇는다) |

## 결정
| 항목 | 적용 | 근거·상태 |
|---|---|---|
| 가격·기간 | 광고제거권 3,000원 영구 / 고기 멤버십 5,000원 30일 | BMDesign 3장 표 · 확정 |
| 무료 소탕 한도 | **새로 도입.** 멤버십 없으면 하루 72회(로컬 날짜 리셋), 멤버십이면 무제한 | 사용자 결정 2026-10-06. 72 = `SweepEconomy.DailySweepClears`(고기 유입으로 하루에 돌 수 있는 양). BMDesign 5.3 의 "Day 3 부터 체증" 곡선은 미정이라 고정값 |
| 입장료 면제 | 멤버십이면 입장 고기 0, 입장 버튼 글자 "무료" | BMDesign 5.2-2 · 확정. 면제 입장은 고기를 안 쓰므로 시즌 패스 포인트도 안 오른다(패스는 "쓴 만큼") |
| 재구매 | 만료 3일 전부터 가격 버튼이 다시 보이고, 사면 만료일 뒤로 30일이 붙는다 | ShopPackageDesign 5.2-4 · 확정 |
| 산 뒤 카드 | **내리지 않는다.** 광고제거권은 가격 버튼 자리에 "보유 중" / 고기 멤버십은 "잔여 n일" | 사용자 피드백 2026-10-06 — 기획서 5.2-3("카드를 내린다")을 뒤집음 · 확정 |
| 입구 | 로비 좌상단(메뉴 버튼 아래). D-n 배지는 만료 3일 전부터. **숨기지 않는다** | 사용자 지정(좌상단) · 사용자 피드백 2026-10-06 — 기획서 2.2("2종 보유 시 숨김")를 뒤집음 · 확정 |
| 광고제거권 효과 | 플래그(`AdFree`)만 켠다. 지금 보는 곳은 보상 라운드 "바로 전액 받기" 하나 | 광고 SDK 0줄 — 강제 광고가 아직 없다 |
| 혜택 문구 | 광고제거권 1줄, 고기 멤버십 2줄 | 5.2-1·2 · 확정. 레퍼런스처럼 늘리지 않는다 |
| 광고 아이콘 | 파란 판 + "AD" + 빨간 사선을 조립 | 프로젝트에 광고 그림이 없다 · **그림이 오면 교체** |
| 결제 | 없음 — `ShopPurchaseManager.TryBuyCashProduct`(테스트 결제) 를 지난 뒤 권리 지급 | IAP 0줄 |
| 환불 안내 문구 | 안 넣음 | ShopPackageDesign 10장 "스토어 정책 확인 후 확정" · **확인 필요** |

## 남은 일
1. 환불 안내 문구 확정되면 창 하단에 추가
2. 광고 아이콘·멤버십 엠블럼 정식 그림
3. 무료 소탕 한도 곡선(BMDesign 5.3 — 진도 연동 여부) 확정되면 `MembershipRules.SweepsLeftToday` 에 반영
