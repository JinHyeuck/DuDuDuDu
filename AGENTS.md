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

루트는 기존 `OJ`를 유지하고 계층을 붙인다 — `OJ.Core.Equipment`, `OJ.Game.UI` 등.
폴더 위치와 일치시킨다. 네임스페이스·클래스명 변경은 GUID에 영향이 없어 안전하다
(반면 **파일명 변경은 GUID가 바뀌므로 위험**).

---

## ProjectP에서 가져오지 않는 것 (오버엔지니어링)

실제 코드를 읽고 판정한 목록이다. "좋아 보인다"는 이유로 되살리지 말 것.

| 대상 | 이유 |
|---|---|
| 6계층 asmdef | 138파일에 과함. `Core`/`Game` 2개로 시작 |
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
- **`CombatPowerUIPrefabBuilder`**: `[InitializeOnLoad]`로 에디터 기동마다 LobbyScene을 덮어쓰고 저장한다.
  1단계에서 비활성화한다.
- **`IDialog`는 인터페이스가 아니라 MonoBehaviour 베이스 클래스다.** ProjectP의 `IDialog`/`DialogBase`와
  이름·개념이 모두 충돌하므로 리네임 계획이 필요하다.
- **`UIIdleRewardDialog`는 규약 밖이다.** `IDialog`를 상속하지 않고 프리팹 없이 코드로 전량 조립하며
  폰트를 씬의 아무 `TMP_Text`에서 훔쳐 쓰고 Escape를 자체 처리한다. 10단계 재작성 대상.
- **수치 정본이 코드다.** `DiceMetaDataProvider.MergeMeta`가 asset을 코드 fallback으로 덮고,
  `StageDatabase.ApplyMonsterHpBalance`가 `GetStage` 호출마다 HP를 재계산한다. 결정 3번으로 뒤집는다.
- **인코딩 파손 4파일**: `Dice/TypeUIComponent.cs`, `Dice/UIRemoveDice.cs`가 CP949 원시 바이트,
  `Define/Define.cs`, `Build/Editor/Unity3dBuilder.cs`는 이미 U+FFFD로 한글 주석이 파괴됨(주석 재작성만이 해결책).
- **평문 자격증명**: `Build/Editor/Unity3dBuilder.cs` 27~30줄에 키스토어 비밀번호가 상수로 박혀 있다.
  제거 + **비밀번호 로테이션**이 필요하다(이미 커밋됐으므로 파일 수정만으로는 사라지지 않는다).

---

## 검증 도구

에디터를 열지 않고 프리팹의 무음 오류를 잡는다. 전부 `python Tools/ui/<이름>.py .` 로 실행.

| 도구 | 잡는 것 |
|---|---|
| `verify_dead_events.py` | 프리팹 버튼에 남은 옛 persistent `onClick`. **같은 프리팹 안을 가리키면 살아서 실행된다** |
| `verify_components.py` | script guid와 직렬화 필드 불일치 (예: Grid 필드에 Horizontal guid → 셀 높이 0 → 클릭 전멸) |
| `verify_field_types.py` | `*Button` 필드가 실제로 Button이 아닌 다른 컴포넌트를 가리키는 경우 |
| `verify_prefab_refs.py` | 프리팹이 **다른 프리팹의 컴포넌트**를 직접 참조 (복제 시 원본에 남은 참조) |
| `layout_rect.py` | RectTransform 실제 사각형 계산 — "화면 밖/겹침"을 눈대중 대신 숫자로 |

이 종류는 **컴파일도 콘솔도 조용해서 눌러 보기 전에는 드러나지 않는다.**
프리팹을 만지는 단계(추출·10단계·12단계)에서는 커밋 전에 돌린다.

---

## 작업 방식

- 단계와 게이트는 `MIGRATION_BASELINE.md`가 정본이다. 게이트를 통과하기 전에 다음 단계로 넘어가지 않는다.
- **asmdef는 리팩토링의 출발점이 아니라 도메인 순수화의 결과물이다.** 순환 의존을 끊기 전에 경계를 그으면
  대량 CS0246으로 프로젝트가 멈춘다.
- 씬에서 다이얼로그를 빼면 `MIGRATION_BASELINE.md`의 **알려진 미동작** 목록에 한 줄 추가한다.
  나중에 "원래 죽어 있던 건가, 방금 내가 죽인 건가"를 구분하기 위한 것이다.
- ProjectP를 조사할 필요는 없다. 필요한 내용은 이 문서와 `Docs/`에 있다.
