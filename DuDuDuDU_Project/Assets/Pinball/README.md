# Pinball Seed-Table Toolkit

결과를 먼저 뽑고, 그 결과로 떨어지는 **진짜 물리 궤적**을 재생하는 핀볼 보상 시스템.

파라미터 하나하나의 의미와 증상별 진단표는 **[PARAMETERS.md](PARAMETERS.md)** 를 보세요.

## 설치

`Pinball/` 폴더를 `Assets/` 아래 아무 데나 넣으면 끝.
`Editor/` 폴더명이 그대로여야 에디터 코드가 빌드에서 제외됩니다.
asmdef 는 넣지 않았습니다. 프로젝트에 asmdef 체계가 있다면 Runtime 하나, Editor 하나(Editor 플랫폼 전용, Runtime 참조)를 추가하세요.

## 5분 시작

1. `Tools > Pinball > Create Default Board` — 검증된 기본 판 생성
2. `Assets > Create > Pinball > Seed Table` — 빈 테이블 생성
3. `Tools > Pinball > Sim Lab` — 창을 열고 Board / Seed Table 지정
4. **Edit Board** 탭에서 핀을 마우스로 배치
5. **Bake** 탭 > **Run Quick Test** — 2,000판을 돌려 판이 멀쩡한지 즉시 확인
6. 합격이 뜨면 **Bake**
7. **Preview** 탭에서 궤적을 눈으로 확인

8. **Prefab** 탭 > **Bake UI Prefab** — 배치한 판을 UI 캔버스용 프리팹으로 굽기
9. 프리팹을 씬의 Canvas 아래로 드래그하고, `PinballPlayback` 의 Board View 에 루트를 연결

런타임:

```csharp
// 한 발
int ballId = playback.PlayForSlot(2);

// 버튼 OnClick 에 직접 연결할 때는 이쪽 (void 버전)
playback.Shoot(2);

// 여러 발 — 0.35초 간격으로 순차 발사
playback.PlayForSlots(new[] { 2, 0, 4, 1, 2 }, 0.35f);

playback.OnHit += (ballId, pegIndex, count, strength) => {
    // pegIndex >= 0 이면 그 번호의 핀, -1 이면 벽.
    // 핀 스케일 펀치는 BoardView 가 알아서 하고, 여기선 사운드/파티클만 붙이면 된다.
    audio.pitch = 1f + count * 0.03f;
    audio.PlayOneShot(hitSfx, strength);
};

// 구슬 하나가 착지할 때마다
playback.OnLanded += (ballId, slot) => ShowReward(slot, boardView.SlotCenter(slot));

// 정보가 더 필요하면 이쪽 (같은 시점에 호출된다)
playback.OnBallLanded += r => Debug.Log($"{r.launchIndex}번째 구슬 -> {r.slot}번 칸 (seed {r.seed})");

playback.OnAllLanded += () => resultPopup.Show();   // 전부 끝났을 때 한 번
```

## 동시에 여러 구슬

`PlayForSlot` 을 연달아 호출해도 **이미 굴러가던 구슬은 그대로 둡니다.**
구슬마다 독립된 궤적·시계·충돌 커서를 갖고, 화면상의 구슬은 프리팹의 `Ball` 을 원본으로 복제·풀링합니다.

`OnAllLanded` 는 **대기 중인 발사와 굴러가는 구슬이 모두 소진됐을 때** 한 번 호출됩니다.
`PlayForSlots` 로 10발을 예약하면 마지막 구슬이 착지할 때 딱 한 번 옵니다.
한 발씩 띄엄띄엄 쏘면 매번 비워질 때마다 호출되는데, 그게 정상 동작입니다.

> 시뮬은 구슬 하나를 기준으로 계산된 것이라 **구슬끼리는 충돌하지 않습니다.**
> 겹쳐 보이는 건 연출상의 문제일 뿐 결과에는 영향이 없습니다.
> 거슬리면 발사 간격을 0.3초 이상 주면 거의 눈에 띄지 않습니다.

## 특수 핀

핀의 `specialTag` 를 0 이 아닌 값으로 주면 특수 핀이 됩니다.
Sim Lab > Edit Board 에서 Move 도구로 핀을 클릭하면 **선택된 핀** 패널이 뜨고, 거기서 지정합니다.
캔버스에서는 노란 이중 원으로 표시되고, 프리팹에서는 `Peg_12_Special1` 처럼 이름이 붙습니다.

같은 값을 여러 핀에 주면 한 그룹입니다(황금핀 전부 1번, 폭탄핀 전부 2번 하는 식).

```csharp
playback.OnSpecialHit += (ballId, pegIndex, tag, countForThisBall) => {
    goldCounter += 1;
    PlayGoldFx(boardView.pegs[pegIndex].anchoredPosition);
};

playback.OnBallLanded += r => Debug.Log($"이 구슬이 특수핀 {r.specialHits}회 적중");
playback.OnAllLanded  += () => Debug.Log($"이번 판 총 {playback.SessionSpecialHits}회");
```

> `specialTag` 는 물리에 영향이 없지만, **시드별 적중 횟수가 테이블에 기록되므로 바꾸면 재베이크해야 합니다.**
> 그래서 LayoutHash 에 포함되어 있습니다.

### 태그별 적중 확률 지정

`specialHitPolicy` 를 `Declared` 로 바꾸면 **태그마다 "한 번이라도 맞을 확률"** 을 지정할 수 있습니다.
서로 합이 1일 필요 없습니다. 각각 독립적인 목표값입니다.

```
태그 1 (황금핀 2개) → 30%
태그 2 (폭탄핀 1개) → 10%
```

동작 방식은 이렇습니다. 슬롯 안의 시드들을 **적중 패턴**(어느 태그를 맞았는지의 조합)으로 묶고,
각 패턴이 뽑힐 가중치를 IPF(반복 비례 조정)로 계산합니다. 자연 분포에서 출발해 태그별
주변확률이 목표에 닿을 때까지 스케일링을 반복합니다.

적중 '횟수'로 풀을 쪼개지 않는 게 핵심입니다. "태그1을 맞는다"는 요구가 패턴 {1} 과 {1,2}
양쪽으로 분산될 수 있어서, 풀이 말라붙지 않습니다.

기본 판(중앙 범퍼 2개를 각각 태그 1·2로) 실측:

| | 자연 적중률 | 목표 30% 설정 시 달성 | 최악 재사용 배율 |
|---|---|---|---|
| slot 0 | 33.0 / 13.9 % | 30.0 / 30.0 % | 2.2x |
| slot 2 | 29.9 / 29.5 % | 30.0 / 30.0 % | 1.0x |
| slot 4 | 10.5 / 28.6 % | 30.0 / 30.0 % | 3.0x |

목표에 정확히 도달합니다. 재사용 배율은 "그 패턴의 시드가 자연 빈도의 몇 배로 자주 나오는가"인데,
자연 적중률에서 멀수록 커집니다. **4배를 넘으면 유저가 반복을 알아챕니다.**

Bake 탭의 **특수 핀 목표 달성 검증**에서 목표와 달성치, 재사용 배율을 확인할 수 있습니다.

### 한계

목표를 자연 적중률에서 너무 멀리 잡으면 달성이 안 됩니다. 어떤 슬롯에서 그 태그를 맞는 궤적이
아예 없으면 그 슬롯에서는 확률을 올릴 수 없고, 반대로 100% 맞으면 내릴 수 없습니다.

해결은 확률표가 아니라 **판 쪽**입니다. 핀을 키우거나 구슬이 지나는 길목으로 옮기면
자연 적중률이 올라가고, 그러면 목표를 여유 있게 잡을 수 있습니다.

그리고 슬롯 확률은 **언제나 우선**합니다. 패턴을 못 맞추면 경고를 남기고 패턴만 포기하며,
착지 칸은 반드시 지킵니다. 지급 확률이 표기와 어긋나면 안 되기 때문입니다.

### 착지 콜백 두 가지

| | |
|---|---|
| `OnLanded(ballId, slot)` | 간단한 용도. 어느 칸에 떨어졌는지만 필요할 때 |
| `OnBallLanded(BallResult)` | `launchIndex`(이번 판에서 몇 번째), `seed`, `pegHits`, `specialHits`, `flightSeconds` 까지 |

`ballId` 는 컴포넌트가 켜진 뒤로 계속 증가하는 고유 번호라 두 번째 판에서는 10, 11, 12... 가 됩니다.
"이번 판의 몇 번째 구슬"이 필요하면 `BallResult.launchIndex` 를 쓰세요. 판마다 0부터 다시 셉니다.

`seed` 가 들어있는 이유는 버그 재현용입니다. 이상한 궤적이 나왔을 때 로그의 시드를
Sim Lab > Preview 탭에 그대로 입력하면 같은 궤적이 그대로 재생됩니다.

유용한 API:

| | |
|---|---|
| `IsBusy` | 굴러가는 구슬이나 대기 중인 발사가 있는지 |
| `ActiveBallCount` / `PendingLaunchCount` | 현재 개수 |
| `StopAll()` | 대기 취소 + 구슬 전부 회수 (씬 전환 시) |

> ⚠️ **버튼 OnClick 에는 `PlayForSlot` 이 아니라 `Shoot` 을 연결하세요.**
> UnityEvent 는 "인자 1개 + 반환값 있는 메서드"를 호출하지 못합니다.
> 에디터 드롭다운에는 `PlayForSlot` 도 보이지만, 누르는 순간
> `ArgumentException: method return type is incompatible` 로 터집니다.

## UI 프리팹

**Prefab** 탭에서 배치한 판을 그대로 프리팹으로 굽습니다. 생성되는 구조:

```
PinballBoard_View          (RectTransform + PinballBoardView)
  Background               판 전체 크기 Image
  Slots/Slot_0..N          보상 칸 영역 — 아이콘·수량을 자식으로 붙이는 자리
  Walls/Wall_N             회전된 막대 Image
  Dividers/Divider_N       자동 생성 칸막이
  Pegs/Peg_N               ★ 인덱스가 board.pegs 와 1:1 대응
  LaunchPoint              스프링 아트를 붙이는 빈 마커
  Ball
```

좌표 규약은 **모든 자식이 anchor (0,0) + pivot (0.5,0.5)** 입니다.
그래서 `anchoredPosition` 이 곧 "판 좌하단으로부터의 픽셀 좌표"가 되고,
보드 좌표 → UI 좌표 변환이 `pixelsPerUnit` 곱셈 하나로 끝납니다.

`Peg_N` 의 인덱스가 보존되는 게 핵심입니다. 시뮬이 "몇 스텝에 몇 번 핀에 맞았는지"를
기록하기 때문에, 재생 중 그 핀만 정확히 튀어오르게 할 수 있습니다.

스프라이트와 색은 보드 에셋에 저장되므로 **다시 구워도 유지됩니다.**
다만 프리팹에 직접 붙인 자식 오브젝트는 재베이크 시 사라지니, 장식은 씬에 올린 인스턴스의
`Slot_N` 아래에 붙이세요.

## Quick Test 가 핵심입니다

Unity 에디터에서 판을 눈으로 보면 멀쩡해 보여도, 실제로는 구슬이 발사 레인에 갇혀서
**모든 시드가 폐기되는** 상태일 수 있습니다. 육안으로는 절대 안 보입니다.

Quick Test 는 2,000판을 1초 안에 돌려서 폐기율·슬롯별 표본·핀 충돌 수·비행 시간·니어미스 비율을 내고,
기준 미달이면 **무엇을 만져야 하는지 문장으로** 알려줍니다. 판을 고칠 때마다 돌리세요.

## 기본 판 검증 결과

8,000 시드 실측 (`gravity -26`, `drag 0.45`, `launchSpeed 27`):

| 항목 | 값 |
|---|---|
| 폐기율 | 13.6% |
| 슬롯별 natural 분포 | 28.1 / 14.5 / 13.1 / 13.5 / 30.8 % |
| 평균 핀 충돌 | 17.2회 |
| 평균 비행 시간 | 3.9초 |
| 니어미스 궤적 | 19.3% |

natural 분포가 declared 확률(20/22/16/22/20)과 다른 건 **정상**입니다.
지급은 declared 를 따르고, 시드 풀은 연출 소스로만 씁니다.

## 결정론 주의사항

- Unity PhysX/Box2D 는 **쓰지 않습니다.** 플랫폼·버전 간 결정론을 보장하지 않아서 시드 테이블이 특정 기기에서만 깨집니다.
- `UnityEngine.Random`, `System.Random` 도 시뮬 안에서는 금지. `PinballRng` 만이 무작위성의 원천입니다.
- `Time.deltaTime` 은 시뮬에 개입하지 않습니다. 렌더 보간에만 씁니다.
- Burst 를 적용한다면 반드시 `[BurstCompile(FloatMode = FloatMode.Strict)]`. 기본값 `Fast` 는 FMA 재결합을 허용해 결과가 갈립니다.
- 그래도 불안하면 `float` → Q16.16 고정소수점으로 바꾸면 완전히 안전해집니다. `PinballSimulator` 만 갈아끼우면 됩니다.

## 판을 수정했다면

시드 테이블이 즉시 무효가 됩니다. Sim Lab 이 빨간 경고를 띄우고, `PinballPlayback` 도 실행 시 에러를 냅니다.
`PinballSeedTableTests` 를 CI 에 걸어두면 재베이크를 잊었을 때 빌드가 실패합니다.
겉보기엔 멀쩡하게 굴러가기 때문에, 이 테스트 없이는 확률이 조용히 틀어진 채로 라이브에 나갑니다.

## 파일

```
Runtime/
  PinballRng.cs              결정론 RNG (xorshift32 + murmur finalizer)
  PinballBoard.cs            판 데이터 SO. 모든 필드에 툴팁, 칸막이 물리 자동 생성, 레이아웃 해시
  PinballSimulator.cs        헤드리스 시뮬. 인스턴스 상태 없음 = 스레드 안전
  SeedTable.cs               베이크된 시드 풀 에셋
  SeedBag.cs                 셔플백 — 같은 궤적 연속 재등장 방지
  PinballPlayback.cs         슬롯 -> 시드 -> 재생
Editor/
  PinballSimWindow.cs        Sim Lab: 상태, 레이아웃, 베이크/프리뷰 패널
  PinballSimWindow.Canvas.cs Sim Lab: 좌표 변환과 그리기
  PinballSimWindow.Edit.cs   Sim Lab: 마우스 편집 도구와 생성기
  SeedTableBaker.cs          Parallel 베이커 + Verify + Quick Test 진단
  PinballBoardFactory.cs     기본 판 + 재사용 가능한 핀 패턴 생성기
  PinballSeedTableTests.cs   CI 안전장치
```
