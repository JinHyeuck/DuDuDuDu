# 방치(자동 전투) 보상 UI — 아트 적용 기록 (작업 중)

> **상태: 적용 완료, 확인 필요 항목 남음 (2026-10-01).** TitleScene 부터 재생해 로비 버튼 → 창 열림,
> 버튼 3개 가려짐 0, 받기 → 보상 결과창.
> 고기축제는 완전히 제외한다(PSD 의 '고기전투' 그룹 · 시안의 탭은 옛 레이아웃).

## 원본
| 무엇 | 경로 |
|---|---|
| **PSD (정본)** | `Art/0PSD/고기패스와자동전투보상.psd` → 그룹 `자동전투보상/자동전투` 만 쓴다 (`레이아웃` 하위 그룹은 주석) |
| 시안(참고) | `Art/Layout/Offline_Reward_Layout(_1).png` — 탭이 있는 옛 레이아웃. **PSD 가 우선** |
| 코드 | `Assets/Scripts/IdleReward/UIIdleRewardDialog.cs` (`Build` 가 PSD 좌표로 조립) · 굽기 메뉴 `OJ/개발/방치보상 창 프리팹 굽기` → `Prefab/Refactory/LobbyScene/UIIdleRewardDialog.prefab` (**다시 구우면 프리팹 손 수정은 사라진다**) |

PSD 글자 크기 환산: **PSD size / 2.5167 = pt** (113.25→45, 88.09→35, 75.50→30).
좌표는 PSD 레이어 bbox(보이는 픽셀) — Image 크기는 여기에 **투명 여백 x 배율**을 더한다(`Tools/ui/PORTING.md`).

## PSD 실측 (그룹 '자동전투', 캔버스 1080x1920)
| 요소 | PSD 레이어 | bbox [x,y,w,h] | 리소스 / 배율 | 비고 |
|---|---|---|---|---|
| 뒤 딤 | 사각형 1643 | 전체 | 검정, 불투명도 204/255 (≈0.8) | |
| 윗 그림 배경 | 레이어 2072 | 62,0,958x408 | OfflineReward_Banner_1 x2 | |
| 캐릭터 | Ch_Front_0 복사 | 157,55,315x322 | **FromPsd_Hunter** (임시) | |
| 슬라임 | 레이어 2071 | 711,176,184x165 | **FromPsd_Slime** (임시) | |
| 해골 | 그룹 해골(5장) | 567~740, 93~341 | **FromPsd_Skeleton** (5장 합침, 임시) | |
| 팝업 판 | 레이어 664 복사 3 | 75,359,924x1173 | Ui_Popup_Bg x4 (여백 3px→+12) | |
| 안쪽 판 | 레이어 557 복사 48 | 109,480,857x829 | Ui_Popup_SmallBox x4, #636481 (여백 6px→+24) | |
| 윗 장식 띠 | 레이어 105 복사 | 126,494,830x55 | **FromPsd_TopDecor** (임시) | |
| 제목 | 자동 전투 보상 | 402,407,269x44 | 45pt 흰색 | |
| 스테이지 이름 | 어두운 숲속 | 411,515,208x34 | 35pt 흰색 | 실제 값 `StageData.GetStageDisplayName(idx)` |
| 시간당 칸 좌 | 레이어 557 복사 54 | 157,569,361x62 | SmallBox x4, #45465f | 골드 아이콘 [167,572,56x56], "+200/시간" [265,581,162x37] 35pt |
| 시간당 칸 우 | 레이어 557 복사 55 | 550,572,361x62 | SmallBox x4, #45465f | 보석 아이콘 [560,574,64x58], "+10/시간" [676,584,141x37] 35pt |
| 구분선 | 레이어 2066 | 131,646,820x5 | #7b7c96 단색 | |
| (도형) | 사각형 1624 | 40,649,949x111 | 색 못 읽음(벡터) — 합성 기준 이미지로 확인 필요 | |
| 보상 상자 | 레이어 557 복사 53 | 132,680,817x512 | Ui_offlineRewardBox x4 (가로 여백 15px→+60) | 윗 밝은 띠는 **스프라이트 자체**(위 20px). 9슬라이스 테두리는 대략 (19,4,20,20) — 세부는 사용자가 조정 |
| 한도 문구 | 이미 한도 시간이… | 282,704,498x30 | 30pt 흰색 | |
| 아이템 칸 | 레이어 1633 복사 32~41 | 열 x 168/314/466/612/762, 행 y 785/935, 3행은 1085 에서 잘림(1173) | 찬 칸 Itme_Slot_3 x3 · 빈 칸 Itme_Slot_0 x3 + 색 0.82 | 간격 ≈148 x 150, 최소 15칸, 스크롤 |
| 받기 버튼 | Big_Btn_Green 복사 4·5 자리 합침 | 178~911 x 1341~1486 | Big_Btn_Green x3, 757x169 | 소탕 버튼을 빼고 두 자리를 한 버튼이 차지 (사용자 지시) |
| 닫기 | 레이어 32 복사 18 / 662 복사 2 | 855,326,112x111 | **FromPsd_CloseButton** (바탕+X 합침, 임시) | |

기준 합성 이미지 다시 만들기 (그룹 '자동전투' 만, 주석 제외):
```python
from psd_tools import PSDImage
psd = PSDImage.open("Art/0PSD/고기패스와자동전투보상.psd")
auto = [l for l in [g for g in psd if g.name == "자동전투보상"][0] if l.name == "자동전투"][0]
auto.composite(viewport=(0,0,1080,1920), layer_filter=lambda L: L.name != "레이아웃" and L.visible).save("idle_psd_ref.png")
```

## 결정표
| 항목 | 적용 | 근거·상태 |
|---|---|---|
| 기준 | PSD | 사용자 지시 · 확정 |
| 고기축제 | 완전 제외 | 사용자 지시 · 확정 |
| 소탕 | **이 창에서 소탕 UX 전부 뺀다** (소탕 버튼·소탕 횟수 문구) | 사용자 지시 · 확정 (2026-10-01) |
| 시간당 보상 | 왼쪽 골드 `GetGuaranteedNormalGold(stage)`, 오른쪽 **핀볼 티켓** `PinballTicketPerClear`(1) | 자동 전투 보상에 보석이 없다(골드·속성 주문서·장비 주문서·핀볼 티켓뿐) → PSD 보석 자리에 티켓 · 추정 · 확인 필요 |
| 핀볼 티켓 아이콘 | `PointMetadataDatabase.asset` 의 PinballTicket(6) 아이콘이 **비어 있다**(`fileID: 0`) · 프로젝트에 티켓 그림 없음 → 아이콘을 숨긴다 | 리소스 필요 |
| 스테이지 이름 | "N. 이름" (`StageData.GetStageDisplayName`) | PSD 는 이름만 · 추정 |
| 임시 그림 `FromPsd/` | PSD 에서 뽑은 그림 5장 (`Assets/Resources/Art/OffLineReward/FromPsd/`) | 정식 파일이 오면 교체 |
| 한도 문구 | 한도(24h) 도달 시 "이미 한도 시간이 되었습니다. HH:MM:SS", 아니면 누적 시간 | 추정 |

## 남은 일
1. 확인 필요 항목(결정표 '추정') 사용자 확인
2. 핀볼 티켓 아이콘 등록(`PointMetadataDatabase` PinballTicket) — 등록되면 코드 수정 없이 뜬다
3. `FromPsd/` 임시 그림 → 정식 파일 교체 후 다시 굽기
4. 9슬라이스 세부(Ui_offlineRewardBox 등) — 사용자
