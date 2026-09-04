# Project: DuDuDuDu (주사위 머지 디펜스)

Unity 6.3 LTS(`6000.3.7f1`) 기반 모바일 게임. 6x4 보드에 SP로 주사위를 소환·머지해
자동 발사로 내려오는 몬스터를 막는다. 씬은 Title → Lobby → Battle.

> **대규모 구조 리팩토링 진행 중.**
> 참조 아키텍처는 ProjectP(`C:\GitFolder\ProjectP`). 단, **구조를 통째로 베끼지 않는다** —
> B 규모에 맞게 축소한 목표가 아래에 적혀 있고, 그것이 정본이다.
> 진행 상황·게이트: **`MIGRATION_BASELINE.md`**. 브랜치: `Refactory` (main은 동결).

---

## 확정된 결정 (되묻지 말 것)

| # | 결정 | 결과 |
|---|---|---|
| 1 | Unity 6000.3.7f1로 업그레이드 완료 | 되돌릴 수 없음. 6000.0으로 못 내려감 |
| 2 | **기존 유저 세이브 버림** | PlayerPrefs 10키 마이그레이션 불필요. enum 리네임 금지 규약 **해제** |
| 3 | **밸런스 수치를 SO로 승격** | 코드 fallback이 asset을 덮는 현 구조를 뒤집는다 |

2번 때문에 `PointType`/`DiceType` enum 이름을 자유롭게 정리해도 된다.
단 **스키마 버전 필드는 처음부터 넣는다** — 버리는 건 과거 세이브지 앞으로의 마이그레이션 능력이 아니다.

---

## Stack

| 항목 | 채택 | 비고 |
|---|---|---|
| UI | uGUI | UI Toolkit 사용 금지 |
| 비동기 | UniTask | 현재 `Assets/Plugins/UniTask` 벤더링. 신규 Coroutine 작성 금지 |
| DI | VContainer | 8단계에서 도입 |
| JSON | Newtonsoft.Json | 7단계에서 패키지 추가. `JsonUtility` 사용 금지 |
| 큰 숫자 | **불필요** | 재화가 `int`/`long`. BigDouble 도입하지 말 것 |
| 트위닝 | 없음 | 필요해지면 그때 결정 |

**끌고 오지 않는다**: Addressables, URP, Spine, A* Pathfinding, Entities, BreakInfinity, 뒤끝(TheBackend).
B의 게임성과 무관하고, 넣으면 빌드 시간·용량·관리 부담만 영구히 는다.

---

## 목표 구조 — ProjectP의 축소판

ProjectP는 asmdef 6계층(`Core/Domain/Infrastructure/Presentation/Scenes/App`)이지만,
**B는 138파일이므로 그 계층을 그대로 만들면 references 유지비만 남는다.**

```
OJ.Core   ← 순수 C#. 상태·규칙·계약. noEngineReferences: true 로 컴파일러가 강제
OJ.Game   ← 나머지 전부 (MonoBehaviour, UI, 씬, 컴포지션 루트)
```

2개로 시작하고, 파일이 늘어 경계가 아파질 때 쪼갠다. 처음부터 6개로 시작하지 않는다.

### `OJ.Core` 규칙

ProjectP의 `Game.Core`는 BigDouble 때문에 UnityEngine을 열어뒀고 "순수성은 규약으로만 강제된다"고
스스로 인정한다. **B는 BigDouble이 없으므로 `noEngineReferences: true`를 걸어 컴파일러로 강제한다.**
여기서만은 ProjectP를 따라가지 않는다.

### 네임스페이스

루트는 기존 `OJ`를 유지하고 `Assets/Scripts` 아래 폴더 경로를 그대로 붙인다 —
`Scripts/Equipment` → `OJ.Equipment`, `Scripts/SceneFlow` → `OJ.SceneFlow`.
어셈블리 이름(`OJ.Game`)은 네임스페이스에 들어가지 않는다. 네임스페이스·클래스명
변경은 GUID에 영향이 없어 안전하다 (반면 **파일명 변경은 GUID가 바뀌므로 위험**).

**예외 두 건이 있고, 둘 다 이름이 이름을 가리는 문제다.**

| 위치 | 네임스페이스 | 이유 |
|---|---|---|
| `Scripts/Editor` | `OJ.EditorTools` | `namespace OJ.Editor` 안에서는 `Editor`가 네임스페이스로 먼저 잡혀 `UnityEditor.Editor`를 가린다(CS0118). 폴더명은 Unity 관례상 `Editor`로 둔다 |
| `Scripts/Define.cs` | `OJ` (루트) | `DiceType`·`Rarity`·`PointType` 등 전 계층이 쓰는 열거형이다. 하위 네임스페이스로 내리면 149개 파일 전부가 `using`을 달아야 한다 |

**폴더명을 정할 때 그 안의 타입명과 겹치지 않게 할 것.** `Scripts/Bullet`을
`OJ.Bullet`으로 만들면 `class Bullet`(Hunting)이 다른 네임스페이스에서 가려진다 —
그래서 11.3에서 `Scripts/Dice`로 흡수했다.

---

## ProjectP에서 가져오지 않는 것 (오버엔지니어링)

실제 코드를 읽고 판정한 목록이다. "좋아 보인다"는 이유로 되살리지 말 것.

| 대상 | 이유 |
|---|---|
| 6계층 asmdef | **ProjectP 자신이 근거다.** 아래 절 참조 |
| 802줄 단일 `RootLifetimeScope` | ProjectP 스스로 "단일 실패점이자 최대 결합점"이라 기록. 콘텐츠별 Installer로 분할 |
| `I{X}Context` 싱글톤 12종 | `ShowAsync<T>()`가 인스턴스를 안 돌려주는 대가로 생긴 우회 계층. **B는 `ShowAsync<T>(param)`이 인스턴스를 반환하게 설계해 이 12종을 아예 만들지 않는다** |
| `DialogCatalog`의 `GetComponent` 선형 역매핑 | 등재 누락이 런타임 예외로만 드러남. 명시적 키 + 등재 검증 테스트로 |
| SaveSlice 6분할 / FNV-1a 해시 / revision CAS / ServerFlushCoordinator | 전부 뒤끝 서버 제약에서 나온 구조. B엔 네트워크 코드가 0줄 |
| 도메인 이벤트 35종 → dirty 3등급 | 로컬/서버 주기 분리 때문에 생긴 복잡도. **dirty 플래그 1개 + 주기 flush + pause/quit flush면 충분** |
| 고정 패스프레이즈 AES | 치트 방어는 못 하면서 "패스프레이즈 건드리면 전 세이브 손상 오판" 실패 모드만 추가 |
| PokaUi + 프리팹 생성기 11개 | 이식 비용(런타임 3어셈블리 + 폰트 + 스프라이트 + 디자인 좌표계)이 크고, B 로비 UI는 이미 만들어져 있다 |
| Boot + Main 2단 씬 | ProjectP의 Main 씬은 오브젝트 1개짜리. 서버 로그인이 없는 B는 Boot 하나로 충분 |
| `BackButtonHandler` 신설 | B의 `AOSBackBtnManager`가 이미 Stack + Escape 폴링을 한다. **새로 만들지 말고 `DialogBase`가 기존 스택에 push/pop 하도록 잇는다** |

**가져오는 것**: `Tools/ui/*.py` 정적 검증 5종(이미 이식 완료), `IUIService`/`DialogBase`/`UICanvasLayout`의
설계 아이디어(코드가 아니라 개념), `MIGRATION_BASELINE.md`의 Phase 게이트 방식.

---

### 6계층 asmdef 를 안 가져오는 진짜 이유 (2026-09-02 조사)

원래 근거는 "138파일에 과함" — **파일 수였고, 그건 약하다.** 지금 이미 190개고 계속 는다.
파일 수를 근거로 두면 300개가 되는 날 판단이 뒤집힌다. 실제로 ProjectP 를 읽고 나온
근거는 셋이며 **파일 수와 무관하게 유효하다.**

**1. asmdef 가 실제로 막는 벽이 ProjectP 에도 딱 하나다.**
`Game.Presentation.asmdef` 의 references 25개에 `Game.Infrastructure` 가 없다 —
UI 가 세이브·PlayerPrefs·씬로더·서버를 직접 못 만진다는 그 한 줄이 6계층의 전부다.
나머지 경계는 **소스 규약이 한다**: `Presentation` 은 `Game.Domain` 을 참조할 수 있는데
346파일 중 19개만 쓰고 나머지는 `Game.Core` 인터페이스를 주입받는다.

**2. 우리에겐 그 하나의 벽조차 막을 대상이 없다.**
ProjectP `Infrastructure` 31개 중 18개가 뒤끝(백엔드)이다. 우리는 네트워크 코드가 0줄이다.

**3. 6계층은 우리가 문제 삼은 UI/로직 혼재를 <b>전혀 풀지 않는다.</b>**
`Presentation` 346개 안에 UI 174개가 그대로 들어 있다 — 오히려 우리보다 섞여 있다.
그걸 가른 것은 asmdef 가 아니라 **폴더**다.

**그래서 트리거를 파일 수에서 포트 개수로 바꿔 적는다.**

> `OJ.Core` 의 public 포트 인터페이스가 20개를 넘고, UI 가 그 포트만으로 도메인에
> 닿을 수 있게 되면, 그때 `OJ.Game.Presentation` 을 잘라 인프라 참조를 컴파일러로
> 끊는다. **그 전에는 자를 것이 없다.**

지금 격차는 계층 수가 아니라 **포트 개수**다 — `OJ.Core` 의 public interface 는
`IClock` **1개**이고 ProjectP `Game.Core` 는 **169개**다. ProjectP 가치의 본체는
asmdef 6개가 아니라 "Core 에 포트를 모으고 바깥이 안쪽을 향해 구현하는 역전"이고,
그것은 어셈블리 2개로도 표현된다. 우리가 못 하는 것은 계층을 안 나눠서가 아니라
**역전할 포트가 없어서**다. 그 증거가 UI 파일 48개 안의 `.Instance` 호출 **262회**다.

### 씬별 컴포지션 — 우리 방식이 ProjectP 보다 견고하다 (바꾸지 말 것)

ProjectP `Game.Scenes` 는 씬 YAML 에 `LifetimeScope` 를 배치하고 **로드하는 쪽이**
`LifetimeScope.EnqueueParent(_rootScope)` 로 부모를 밀어 넣는다. 그래서 라우터를 안 타는
경로 — **에디터에서 씬을 직접 재생하는, 개발 중 가장 흔한 경로** — 에서 부모가 안 붙고,
`App/AppEntry.cs:27-33` 에 그것을 위한 특수 분기가 생겼다.

우리 `BattleScope` 는 (a) `FindParent() => GameContainer.Root` 로 부모를 스스로 찾고
(b) 씬에 배치하지 않고 `sceneLoaded` 에서 코드로 만든다. 그래서 그 구멍이 없고,
생성 시점이 **모든 `Awake` 뒤·모든 `Start` 앞**으로 공짜로 고정된다.

부수적으로 `Game.Scenes` 는 참조 그래프의 완전한 잎이다 — 참조자가 `Game.App` 하나뿐인데
그 참조는 코드 사용처가 0인 죽은 줄이고, 격리 탓에 테스트가 Scenes 를 타입으로 못 본다.

> **폴더 재배치(`{Feature}/UI/` 분리, 58파일)는 검토했고 하지 않기로 했다 (2026-09-02).**
> 기능 우선 구조가 원래 그랬고, 바꿔서 얻는 것이 탐색 편의뿐이다. 다시 꺼낼 때
> 필요한 것은 위 트리거 조건이지 폴더 취향이 아니다.

---

## 절대 규칙 (Unity 함정)

어기면 조용히 깨지고 되돌릴 수 없다.

1. **Unity 에디터가 켜진 상태에서 파일시스템으로 에셋을 옮기지 않는다.**
   에디터 내 이동 또는 `AssetDatabase.MoveAsset` 스크립트만 허용.
2. **`.meta` 없이 에셋을 옮기지 않는다.** GUID가 재발급되면 프리팹·씬 참조가 전멸하고 복구가 안 된다.
3. **`.unity` / `.prefab` YAML을 직접 텍스트 편집하지 않는다.**
4. **파일명 변경은 GUID를 바꾼다.** 클래스명·네임스페이스 변경은 안전하지만 파일명은 아니다.
5. **SO 에셋은 반드시 `.meta`와 함께 git 추적한다.**
   ProjectP는 `Content/*.asset`이 untracked라 덮어쓰면 복구가 안 된다 — 그 실수를 반복하지 않는다.
6. **대량 쓰기 전에 백업하고, 쓴 뒤 읽어서 검증한다.**
7. **`Object.Instantiate`로 만든 오브젝트에는 `[Inject]`가 채워지지 않는다.**
   DI 도입 후에는 `IObjectResolver.Instantiate` 또는 팩토리를 거친다. 안 그러면 컴파일도 콘솔도
   조용한 채 null이 된다.
8. **필드 주입은 `Awake` 이후에 채워진다.** B는 `Awake` 초기화가 많아 8단계에서 시점 이동이 필요하다.

---

## 이 코드베이스의 알려진 함정

리팩토링 중 계속 부딪히게 되므로 미리 알아둘 것.

- **조용한 폴백 3단**: `StaticResource`가 TitleScene에만 배치돼 있어 Lobby/Battle을 직접 재생하면
  MonoSingleton이 빈 인스턴스를 자동 생성한다. Provider들이 `StaticResource → Resources.Load → 코드 기본값`
  순으로 조용히 내려간다. **배선 사고가 전부 "기본값 게임"으로 흡수된다.**
- **`Resources.Load`가 항상 실패하는 경로**: `DiceMetaDataDatabase.asset`, `GemDefinitionDatabase.asset`이
  `Assets/ScriptableObject/`에 있어 Resources 규약 밖이다. 폴백을 예외로 승격하기 **전에** 경로를 먼저 고쳐야 한다.
- ~~**`CombatPowerUIPrefabBuilder`**: `[InitializeOnLoad]`로 에디터 기동마다 LobbyScene을 덮어쓰고 저장한다.~~
  **1.1에서 해소.** 자동 설치 경로를 제거하고 메뉴 항목(`Tools/OJ/Combat Power/…`)만 남겼다.
  씬을 건드리는 에디터 훅은 이제 전 프로젝트에 없다.
- **`IDialog`는 인터페이스가 아니라 MonoBehaviour 베이스 클래스다.** ProjectP의 `IDialog`/`DialogBase`와
  이름·개념이 모두 충돌하므로 리네임 계획이 필요하다.
- **`UIIdleRewardDialog`는 규약 밖이다.** `IDialog`를 상속하지 않고 프리팹 없이 코드로 전량 조립하며
  폰트를 씬의 아무 `TMP_Text`에서 훔쳐 쓰고 Escape를 자체 처리한다. 10단계 재작성 대상.
- **수치 정본이 코드다.** `DiceMetaDataProvider.MergeMeta`가 asset을 코드 fallback으로 덮고,
  `StageDatabase.ApplyMonsterHpBalance`가 `GetStage` 호출마다 HP를 재계산한다. 결정 3번으로 뒤집는다.
- ~~**인코딩 파손 4파일**~~ **1.2에서 해소.** 138파일 전부 UTF-8이며 `Tools/verify_encoding.py`가 지킨다.
  `Define.cs`는 규약이 예상한 재작성이 아니라 `bfa569d`에서 **원문 복구**됐다.
  → **U+FFFD를 만나면 재작성하기 전에 `git log --follow`로 성한 리비전을 먼저 찾을 것.**
- **평문 자격증명**: `Unity3dBuilder.cs`의 상수는 **1.3a에서 제거**(환경 변수 `OJ_KEYSTORE_PASS` /
  `OJ_KEYSTORE_ALIAS_PASS`로 주입, 미설정 시 빌드 중단). 다만 **노출은 아직 안 닫혔다** —
  히스토리에 남아 있고 `Keystore/osw.keystore`까지 추적 중이며 GitHub에 푸시된 상태다.
  **비밀번호 로테이션이 선행 조치다.** 상세는 `MIGRATION_BASELINE.md` 하단 전용 절.

---

## 검증 도구

에디터를 열지 않고 무음 오류를 잡는다. 전부 리포 루트에서 `python <경로> .` 로 실행.

**커밋 전에는 이거 하나면 된다.**

```
Toolserify-all.cmd            # 인코딩 + 네임스페이스 + Missing + 테스트 3273개
Toolserify-all.cmd --quick    # 테스트는 빼고 (몇 초)
```

넷을 따로 기억해서 돌려야 하면 아무도 안 돌린다. 하나라도 실패하면 종료 코드가 0이 아니다.
`.github/workflows/verify.yml` 은 이 중 **Unity 없이 도는 둘**(인코딩·네임스페이스)만 돌린다 —
나머지 둘은 `Library/PackageCache` 와 설치된 Unity 가 있어야 해서 러너에서 못 돈다.

**UI 프리팹 (`Tools/ui/`)**

| 도구 | 잡는 것 |
|---|---|
| `verify_dead_events.py` | 프리팹 버튼에 남은 옛 persistent `onClick`. **같은 프리팹 안을 가리키면 살아서 실행된다** |
| `verify_components.py` | script guid와 직렬화 필드 불일치 (예: Grid 필드에 Horizontal guid → 셀 높이 0 → 클릭 전멸) |
| `verify_field_types.py` | `*Button` 필드가 실제로 Button이 아닌 다른 컴포넌트를 가리키는 경우 |
| `verify_prefab_refs.py` | 프리팹이 **다른 프리팹의 컴포넌트**를 직접 참조 (복제 시 원본에 남은 참조) |
| `layout_rect.py` | RectTransform 실제 사각형 계산 — "화면 밖/겹침"을 눈대중 대신 숫자로 |

**프로젝트 무결성 (`Tools/`)**

| 도구 | 잡는 것 |
|---|---|
| `verify_encoding.py` | `.cs`가 UTF-8이 아니거나 U+FFFD로 글자가 파괴된 경우. **복구 가능/불가를 구분해 알려준다** |
| `verify_missing_scripts.py` | 씬·프리팹·SO의 Missing script. `--baseline N`으로 기준선 대비 증가를 판정 (현재 기준선 **0**) |
| `verify_namespaces.py` | 폴더와 `namespace` 불일치. 그리고 **폴더명이 타입명과 겹쳐 그 타입을 가리는 경우**(CS0118) — 위 "네임스페이스" 절의 규칙을 검사로 못박은 것이다 |
| `diff_prefab.py` | 프리팹 두 판을 **fileID 를 무시하고** 비교. 굽기 도구가 멱등인지 판정할 때 쓴다. `git diff` 로는 못 한다 — Unity 가 저장마다 fileID 를 새로 발급해 전부 바뀐 것으로 보인다 |

**EditMode 테스트 (`Tools/headless/`)** — Unity를 열지 않고 돌린다.

```
Tools\headless\run-tests.cmd            # 판정용. Mono 에서 돈다
Tools\headless\run-tests.cmd --list     # 수집만
Tools\headless\run-tests.cmd --filter <정규식>
```

웜 1~2초. 종료 코드 0 통과 / 1 실패 / 2 컴파일 실패 / 3 러너 오류.
Unity 설치본의 Roslyn·BCL·엔진 DLL을 쓰고 `Library/ScriptAssemblies`는 **쓰지 않는다** —
그걸 쓰면 에디터가 포커스를 받아야 갱신되어 다시 사람 손이 필요해진다.

> `--coreclr` 옵션은 진단용이다. **판정에 쓰지 말 것** — 아래 이유로 Unity와 답이 갈린다.

---

## 부동소수: Unity는 Mono, 재구현은 오라클이 아니다

**이 프로젝트의 float 산술은 Mono 의미론을 따른다.** Unity는 에디터/EditMode 테스트를 Mono에서
돌리고, Mono의 JIT는 float 식의 중간 결과를 매 연산마다 float로 접지 않고 **더 높은 정밀도로
들고 가다 대입 시점에 한 번 접는다**(C# 명세가 허용한다). CoreCLR은 연산마다 접는다.

```
1f + (7f * 0.145f) + (7f * 7f * 0.015f)     // StageGrowthFormula.MonsterHp(2, 0.145f, 0.015f, 8)
  Mono    → 2.75f       → RoundToInt(2 * 2.75f)      = 6     ← Unity의 실제 동작
  CoreCLR → 2.7499998f  → RoundToInt(2 * 2.7499998f) = 5
```

실제로 이것 때문에 골든 테스트 1개가 틀린 기대값을 갖고 있었다. 엄격 float32로 재구현해
뽑은 값이 5였고, Unity는 6이었다.

**규칙: 기대값을 계산해서 만들지 마라.** 파이썬이든 C#이든 재구현은 Mono의 확장 정밀도를
재현하지 못한다. 값은 **게임이나 Mono 러너가 실제로 내놓은 것**이어야 한다.
그것이 `Tests/Golden/formula_baseline.txt`가 존재하는 이유다.

`StageGrowthFormulaTests.MonsterHpMatchesMonoFloatBehaviour`가 이 경계를 지키는 카나리아다 —
5가 나오면 CoreCLR 의미론으로 돌고 있다는 뜻이다.

이 종류는 **컴파일도 콘솔도 조용해서 눌러 보기 전에는 드러나지 않는다.**
프리팹을 만지는 단계(추출·10단계·12단계)에서는 커밋 전에 돌린다.
`verify_missing_scripts.py`는 `Library/PackageCache`가 있어야 한다(없으면 오탐 방지를 위해 중단).

---

## 작업 방식

- 단계와 게이트는 `MIGRATION_BASELINE.md`가 정본이다. 게이트를 통과하기 전에 다음 단계로 넘어가지 않는다.
- **asmdef는 리팩토링의 출발점이 아니라 도메인 순수화의 결과물이다.** 순환 의존을 끊기 전에 경계를 그으면
  대량 CS0246으로 프로젝트가 멈춘다.
- 씬에서 다이얼로그를 빼면 `MIGRATION_BASELINE.md`의 **알려진 미동작** 목록에 한 줄 추가한다.
  나중에 "원래 죽어 있던 건가, 방금 내가 죽인 건가"를 구분하기 위한 것이다.
- ProjectP를 조사할 필요는 없다. 필요한 내용은 이 문서와 `Docs/`에 있다.
