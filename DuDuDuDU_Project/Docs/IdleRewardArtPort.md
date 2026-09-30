# 방치(자동 전투) 보상 UI — 아트 적용 기록 (작업 중)

> **상태: 측정·결정 끝, 코드 미작성 (2026-10-01).** 터미널 세션으로 옮겨 이어서 한다.
> 고기축제는 완전히 제외한다(PSD 의 '고기전투' 그룹 · 시안의 탭은 옛 레이아웃).

## 원본
| 무엇 | 경로 |
|---|---|
| **PSD (정본)** | `Art/0PSD/고기패스와자동전투보상.psd` → 그룹 `자동전투보상/자동전투` 만 쓴다 (`레이아웃` 하위 그룹은 주석) |
| 시안(참고) | `Art/Layout/Offline_Reward_Layout(_1).png` — 탭이 있는 옛 레이아웃. **PSD 가 우선** |
| 현재 코드 | `Assets/Scripts/IdleReward/UIIdleRewardDialog.cs` (코드로 조립) · 굽기 `Editor/IdleRewardPrefabBaker.cs` · 프리팹 `Prefab/Refactory/LobbyScene/UIIdleRewardDialog.prefab` (손 수정 없음 — 스프라이트는 돼지 아이콘 1개뿐) |

PSD 글자 크기 환산: **PSD size / 2.5167 = pt** (113.25→45, 88.09→35, 75.50→30).
좌표는 PSD 레이어 bbox(보이는 픽셀) — Image 크기는 여기에 **투명 여백 x 배율**을 더한다(`Tools/ui/PORTING.md`).

## PSD 실측 (그룹 '자동전투', 캔버스 1080x1920)
| 요소 | PSD 레이어 | bbox [x,y,w,h] | 리소스 / 배율 | 비고 |
|---|---|---|---|---|
| 뒤 딤 | 사각형 1643 | 전체 | 검정, 불투명도 204/255 (≈0.8) | |
| 윗 그림 배경 | 레이어 2072 | 62,0,958x408 | OfflineReward_Banner_1/2 x2 (둘 다 대조 오차 50+ — 캐릭터가 별도 레이어라 배경만 맞춰 봐야 함, **미확정**) | |
| 캐릭터 | Ch_Front_0 복사 | 157,55,315x322 | 헌터 그림 — 스프라이트 미확인 | |
| 슬라임 | 레이어 2071 | 711,176,184x165 | 미확인 | |
| 해골 | 그룹 해골(5장) | 567~740, 93~341 | 미확인 | |
| 팝업 판 | 레이어 664 복사 3 | 75,359,924x1173 | Ui_Popup_Bg x4 (여백 3px→+12) | |
| 안쪽 판 | 레이어 557 복사 48 | 109,480,857x829 | Ui_Popup_SmallBox x4, #636481 (여백 6px→+24) | |
| 윗 장식 띠 | 레이어 105 복사 | 126,494,830x55 | 미확인(모서리 장식) | |
| 제목 | 자동 전투 보상 | 402,407,269x44 | 45pt 흰색 | |
| 스테이지 이름 | 어두운 숲속 | 411,515,208x34 | 35pt 흰색 | 실제 값 `StageData.GetStageDisplayName(idx)` |
| 시간당 칸 좌 | 레이어 557 복사 54 | 157,569,361x62 | SmallBox x4, #45465f | 골드 아이콘 [167,572,56x56], "+200/시간" [265,581,162x37] 35pt |
| 시간당 칸 우 | 레이어 557 복사 55 | 550,572,361x62 | SmallBox x4, #45465f | 보석 아이콘 [560,574,64x58], "+10/시간" [676,584,141x37] 35pt |
| 구분선 | 레이어 2066 | 131,646,820x5 | #7b7c96 단색 | |
| (도형) | 사각형 1624 | 40,649,949x111 | 색 못 읽음(벡터) — 합성 기준 이미지로 확인 필요 | |
| 보상 상자 | 레이어 557 복사 53 | 132,680,817x512 | Ui_offlineRewardBox x4 (가로 여백 15px→+60) | |
| 한도 문구 | 이미 한도 시간이… | 282,704,498x30 | 30pt 흰색 | |
| 아이템 칸 | 레이어 1633 복사 32~41 | 열 x 168/314/466/612/762, 행 y 785/935, 3행은 1085 에서 잘림(1173) | Itme_Slot_0 x3 (보이는 45px→135) | 간격 ≈148 x 150 |
| 소탕 횟수 문구 | 빠른 소탕 횟수… | 360,1230,323x37 | 35pt #2b2c3f | |
| 소탕 버튼 | Big_Btn_Green 복사 5 | 178,1341,331x145 | (시안상 노랑) Big_Btn_Yellow x3 | "소탕" [312,1366] 35pt, 띠 [196,1410,297x39] 불투명도 77, "10" [323,1416] 35pt, 아이콘 Gem_GamePlay [275,1409,39x42] |
| 받기 버튼 | Big_Btn_Green 복사 4 | 580,1341,331x145 | Big_Btn_Green x3 | "받기" [715,1386] 35pt |
| 닫기 | 레이어 32 복사 18 / 662 복사 2 | 855,326,112x111 / 873,340,73x73 | 바탕 Btn_Gray 계열(다른 창 닫기 7곳이 Btn_Gray) · X 아이콘 **스프라이트 못 찾음**(Assets 전수 대조 실패) | |

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
| 소탕 버튼 | 기존 스테이지 소탕 창 열기 (`GameContainer.UI.Show<UISweepCountDialog>()`, 잠금 조건 `SweepRules.IsUnlocked`) | 사용자 선택 · 확정 |
| 소탕 버튼 비용 표시 | PSD 는 보석 10 — 실제 소탕은 회당 고기 5(`SweepRules.StaminaCostPerSweep`) → **고기 아이콘 + 5** 로 표시 | 추정 · 확인 필요 |
| "빠른 소탕 횟수 : 3/3회" | 하루 횟수 규칙이 없다 → **"소탕 가능 : N회"**(`SweepRules.ResolveMaxCount(보유 고기)`) | 추정 · 확인 필요 |
| 시간당 보상 | `StageRewardCalculator.BuildAutoBattleRewards(stage, 1회, seed)` 의 골드 · 보석(FreeGem) | 1회/시간(`SecondsPerAutoBattleClear`) · 추정 |
| 한도 문구 | 한도(24h) 도달 시 "이미 한도 시간이 되었습니다. HH:MM:SS", 아니면 누적 시간 | 추정 |

## 남은 일
1. 미확인 스프라이트: 윗 그림(배경·캐릭터·슬라임·해골), 윗 장식 띠, 닫기 X 아이콘 → PSD 에서 뽑거나(`FromPsd/` 임시) 사용자에게 파일명 확인
2. `UIIdleRewardDialog.Create` 를 위 좌표로 재작성(`UITowerUIFactory` 같은 스프라이트 헬퍼 필요 — 공용으로 뺄지 판단)
3. 굽기 → 고정 워크트리 Unity(`--project-path` 필수: 다른 세션 에디터가 떠 있을 수 있음) → TitleScene 부터 재생 → 로비 방치 보상 버튼
4. 새 화면이라 무거운 검증까지: `Tools/ui/unity/run.py render_prefab` · `screen_check`, `Tools/ui/art/compare.py` 로 기준 합성 이미지와 대조
