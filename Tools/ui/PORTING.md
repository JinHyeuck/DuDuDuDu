# UI 이식 도구 — 시안 측정(`art/`)과 에디터 검증(`unity/`)

아트 시안(PNG)과 킷 스프라이트로 UI 를 옮길 때 쓴다. 무한의 탑 화면 이식(2026-09-30,
`DuDuDuDU_Project/Docs/TowerUIArtPort.md`)에서 즉석으로 만든 것을 일반화했다.

필요: Python 3 + `pillow` · `numpy` (`python -m pip install pillow numpy`), Unity CLI(`unity`).

## 언제 무엇을

검증 강도는 **기본은 가볍게, 무거운 것은 새 화면을 처음 옮길 때만**이다.

| 단계 | 도구 | 언제 |
|---|---|---|
| 리소스 이름 ↔ 파일 맞추기 | `art/sprite_sheet.py` | 새 화면 |
| 배율·위치 실측 | `art/fit_sprite.py` | 주석 배율이 의심될 때 |
| 패널·버튼 보이는 경계 | `art/scan_edges.py` | 새 화면 |
| 프리팹을 에디터에서 찍기 | `unity/run.py render_prefab` | 새 화면 · 굽기 직후 |
| 캡처 ↔ 시안 나란히 | `art/compare.py` | 새 화면 (작은 수정은 `--crop` 만) |
| 사라진 글자 · 가려진 버튼 | `unity/run.py screen_check` | 새 화면 · 요청 시 |
| 흐름 누르기 | `unity/run.py click <이름>` · `back_key` | 바뀐 흐름만 |

## 측정 원칙 (무한의 탑에서 실제로 겪은 것)

- **전체 시안은 한 번만 연다.** 수치는 표로 적고, 다시 볼 곳은 크롭이나 `scan_edges` 로.
- **시안 주석의 배율을 믿지 말고 잰다.** 프레임 x3 → 실제 x3.5, 불꽃 x2 → x3 이었다.
- **킷 스프라이트에는 투명 여백이 있다.** 잰 것은 보이는 외곽선이므로 Image 크기 = 보이는 크기 + 여백 x 배율.
  `sprite_sheet.py` 의 `visible` 이 여백이다.
- **Filled 이미지는 uGUI 가 스프라이트 여백을 빼고 채운다.** Image 를 여백만큼 키우면 fillAmount 는 진행률 그대로다.
- **9슬라이스 테두리는 사람이 정한다.** `border~` 는 참고용 추정값이다.
- **아이콘을 글자로 쓰지 않는다**(✓ ＋ ▼). 폰트에 없으면 두부나 빈칸이 된다. BM HANNA 는 `·` `×` `—` 가 빈칸이다.

## `unity/` 사용법

```
python Tools/ui/unity/run.py <스크립트> [인자...] [--project <Unity 프로젝트 경로>]
```

| 스크립트 | 인자 | 하는 일 | 모드 |
|---|---|---|---|
| `render_prefab` | 프리팹 경로, 출력 PNG | 빈 씬 1080x1920 캔버스에 띄워 PNG. **열린 씬을 빈 씬으로 바꾼다** | 에디터 |
| `screen_check` | (출력 PNG) | 맨 위 창의 사라진 글자 · 가려진 버튼 + 스크린샷 | 플레이 |
| `click` | 오브젝트 이름 (`press`) | 이름으로 찾아 클릭(레이캐스트 우회) | 플레이 |
| `back_key` | — | `AOSBackBtnManager` 의 Esc 처리와 같은 동작 1회 | 플레이 |

- 플레이 확인은 **TitleScene 부터** 재생한다(다른 씬 직접 재생은 `StaticResource` 폴백 상태다).
- 스크립트는 Assets 밖(여기)에 둔다. Unity `Temp/` 는 에디터를 다시 켜면 비워진다.
- 에디터 모드 스크립트에서 Provider·`StaticResource` 를 깨우지 말 것 — 에러 로그가 한 번만 나오고 잠긴다.
- 스크린샷은 프레임 끝에 저장된다. 같은 호출 안에서 그 파일을 읽지 말 것. ScrollRect 위치 변경도 다음 프레임에 반영된다.
