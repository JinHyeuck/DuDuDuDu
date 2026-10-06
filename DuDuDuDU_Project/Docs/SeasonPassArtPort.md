# 시즌 패스 UI — 아트 적용 기록

> **상태: 적용 완료, 확인 필요 항목 남음 (2026-10-06).** TitleScene 부터 재생해 로비 입구 → 창 열림,
> 글자 부족 0 · 버튼 가려짐 0, 프리미엄 활성화 → 글자 노란색, 일괄 수령 → 전부 완료, 뒤로가기 → 닫힘.

## 원본
| 무엇 | 경로 |
|---|---|
| **PSD (정본)** | `Art/0PSD/패스.psd` → 첫 그룹만 쓴다 (`패스레이아웃` 그룹은 주석, 꺼져 있음) |
| 시안(참고) | `Art/Layout/Pass_Layout.png` — PSD 위에 주석을 얹은 것 |
| 그림 | `Art/Pass/*.png` → `Assets/Resources/Art/Pass/` 로 복사. 윗 그림은 PSD 에서 뽑아 `Pass/FromPsd/FromPsd_PassHero.png` (임시) |
| 코드 | `Assets/Scripts/SeasonPass/UISeasonPassDialog.cs` (`Create`) · `UISeasonPassLevelRow.cs` · `UISeasonPassSlot.cs` · 굽기 메뉴 `OJ/개발/시즌패스/창 굽기` → `Prefab/UI/UISeasonPassDialog.prefab` (**다시 구우면 프리팹 손 수정은 사라진다**) |

PSD 글자 크기 = 글자 레이어 size x transform 배율. 색은 PSD `FillColor` (A,R,G,B 순).
좌표는 PSD 레이어 bbox(보이는 픽셀) — Image 크기는 여기에 **투명 여백 x 배율**을 더한다(`Tools/ui/PORTING.md`).

## PSD 실측 (캔버스 1080x1920)
| 요소 | bbox / 위치 | 리소스 / 배율 | 비고 |
|---|---|---|---|
| 바탕 | 전체 | #4e323e | 무료 트랙 색과 같다 |
| 유료 열 | x 536~, y 657~ | #ffc600 | |
| 무늬 | 중심 (253,942)(261.5,1470)(845.5,942)(853.5,1470) | Pass_Pattern x2, 틴트 #d98faf / #3d1300 | 그림 알파가 이미 15/255. 목록과 같이 안 움직인다 |
| 윗 그림 | -6,-18, 1080x481 | **FromPsd_PassHero** (임시) | 주석은 1090x481. 가장자리가 바탕색으로 번진다 |
| 제목 "시즌 패스" | 글자 38,47~387,127 | 95pt #ffc600 | |
| 시즌 이름 | 글자 39,145~404,184 | 45pt 흰색 | 실제 값 `season.displayName` |
| 남은 기간 칸 | 30,327, 361x62 | Ui_Popup_SmallBox x4, 검정 80% | 시계 Pass_Clork x3 (37,323), 글자 40pt |
| 레벨 띠 | y 421~559 | 검정 5 + #3d3f67 5 + #1d1c30 | 단색 Image 셋 |
| 포인트 칸 | 30,458, 361x62 | SmallBox x4, 검정 80% | 레벨 뱃지 Pass_LevelNumber x3 (14,441) 40pt · 번개 Gem_Thunder x1.6 · "50 / 1000" 40pt |
| 프리미엄 버튼 | 642,440, 386x102 | Pass_premium_Btn x3, **가로 9슬라이스 (56,0,19,0)** | 왕관 x2 (681,472), "프리미엄 활성화" 40pt. 누르는 자리는 `raycastPadding` 으로 보이는 만큼 |
| 트랙 머리 | y 559~676, 경계 검은 선 x 532~538 | Pass_Free / Pass_premium 9슬라이스 x1 (테두리 6) | "무료" 45pt #efbed3 · 유료는 왕관 x3 (663,587) 만 |
| 보상 칸 | 무료 x 152~332 · 유료 735~915, y 738 + 282n | Itme_Slot x4 (180x180 보이는) | 아이콘 x2.5 (칸 중심보다 18 위), 수량 43pt (44.5 아래) |
| 레벨 번호 | x 461~611, 칸 중심보다 15 아래 | Pass_LevelNumber x5 | 60pt |
| 목록 영역 | y 676~1714 | 스크롤 | |
| 하단 띠 | y 1714~ | 검정 5 + #3d3f67 5 + #1d1c30 | |
| 뒤로 | 5,1739, 164x169 | Btn_Gray x5 + Icon_Back x4 | 무한의 탑과 같다 |
| 일괄 수령 | 716,1751, 331x145 | Big_Btn_Green x4 | 글자 **45pt** (PSD 글자 레이어). 시안 주석은 37pt |

기준 합성 이미지 다시 만들기 (`pip install aggdraw scipy` 필요):
```python
from psd_tools import PSDImage
g = list(PSDImage.open("Art/0PSD/패스.psd"))[0]
g.composite(viewport=(0, 0, 1080, 1920)).save("pass_psd_ref.png")
```

## 결정표
| 항목 | 적용 | 근거·상태 |
|---|---|---|
| 기준 | PSD | PORTING.md 규칙 · 확정 |
| 창 형태 | 화면 전체, 열기 연출 `Page` | 이전은 가운데 팝업. PSD 가 전체 화면 · 확정 |
| 포인트 게이지 | 없앰 — 칸 안에 "현재 / 레벨당" 글자만 | PSD 에 게이지가 없다 · 확정 |
| 칸 바탕 상태 | 받을 수 있음 = 초록(Itme_Slot_1) · 유료이고 닿았는데 안 삼 = 주황(Itme_Slot_5) · 그 밖 = 회색(Itme_Slot_0) | PSD 의 1·2레벨 주황, 3레벨 초록, 4레벨 회색에서 역산 · **추정** |
| 받은 칸 | 바탕·아이콘 어둡게 + 수량 자리에 "완료" | PSD 에 없음. 처음엔 칸 가운데에 얹었는데 수량과 겹쳐 안 읽혀서 옮김 · **추정** |
| 못 닿은 레벨 번호 | 뱃지를 어둡게 | PSD 에 없음(전부 밝음) · **추정** |
| 프리미엄 산 뒤 | 글자 "프리미엄 적용 중", 색 #ffe400, 버튼 그림 그대로 | PSD 주석 "구매시 노란글씨로 ffe400" · 문구는 기존 유지 · **추정** |
| 유료 머리 글자 | 안 씀(왕관만) | PSD 의 '프리미엄' 글자 레이어가 꺼져 있다 · 확정 |
| 일괄 수령 글자 | 45pt | PSD 글자 레이어 우선 (주석 37pt) · **확인 필요** |
| 잠김 표시("잠김" 글자) | 없앰 | PSD 에 없음. 주황 바탕 + 프리미엄 버튼이 대신 말한다 · 추정 |

## 남은 일
1. 확인 필요 항목(결정표 '추정') 사용자 확인
2. `FromPsd_PassHero` → 정식 파일이 오면 교체 후 다시 굽기
3. 상단 그림·띠가 1920 보다 긴 화면에서 어떻게 보일지(지금은 중앙 기준 좌표)
