# DuDuDuDu 구조 리팩토링 — 베이스라인 & 게이트 체크리스트

각 단계를 끝낼 때마다 **게이트**를 확인하고 `[ ]` → `[x]`로 갱신한다.
게이트를 통과하지 못한 채 다음 단계로 넘어가지 않는다.

**작업 브랜치:** `Refactory` (main 동결 — 리팩토링 기간 중 기능 개발 없음)
**참조 아키텍처:** ProjectP (`C:\GitFolder\ProjectP`) — 단, 축소 방침은 `AGENTS.md` 참조
**규약 정본:** `AGENTS.md`

---

## 왜 이 순서인가 (핵심 3가지)

1. **asmdef는 마지막에 가깝다.** 지금 `DiceMetaDataProvider`(static)가 씬 싱글톤을 조회하고
   `Monster`/`AttackContent`가 `DamageTextPool`을 직접 부르는 순환 상태다. 경계를 먼저 그으면
   대량 CS0246으로 프로젝트가 멈춘다. **단 하나의 예외가 3단계의 씨앗 asmdef다** — Unity에서
   asmdef를 가진 어셈블리는 `Assembly-CSharp`를 참조할 수 없으므로, 테스트를 쓰려면
   테스트 대상 계산식만 담은 작은 asmdef를 먼저 심고 점진 확대해야 한다.
2. **회귀 안전망이 도메인 수술보다 먼저다.** 테스트가 0인데 핵심 수식이 코드에 하드코딩돼 있고
   SO는 런타임에 덮어써진다. 기준선 없이 손대면 밸런스가 언제 틀어졌는지 영원히 알 수 없다.
3. **조용한 실패를 먼저 봉인한다.** Provider 3단 폴백과 MonoSingleton 자동 생성이 살아 있으면
   이후 모든 배선 사고가 "기본값 게임"으로 흡수되어 감지되지 않는다.

---

## 단계

### -1. Unity 6.3 LTS 업그레이드

| # | 항목 | 상태 | 비고 |
|---|---|---|---|
| -1.1 | `Refactory` 브랜치 생성 | [x] | main은 6000.0에 동결 |
| -1.2 | `6000.3.7f1` 업그레이드 | [x] | 되돌릴 수 없음. ProjectP와 동일 버전 |
| -1.3 | 패키지 상승 확인 | [x] | feature.2d 2.0.2 / multiplayer.center 1.0.1 / visualscripting 1.9.9 + 모듈 2개 추가 |
| -1.4 | 업그레이드 단독 커밋 | [x] | `3b573ac` — 재직렬화 diff가 리팩토링 커밋에 섞이지 않게 |

**게이트:** 4개 씬이 전부 열리고, Title→Lobby→Battle→Lobby 왕복이 동작하며, 콘솔에 새 에러가 없다.

---

### 0. 이식 킷 준비

| # | 항목 | 상태 | 비고 |
|---|---|---|---|
| 0.1 | `AGENTS.md` (규약 정본) | [x] | |
| 0.2 | `MIGRATION_BASELINE.md` (이 문서) | [x] | |
| 0.3 | `Tools/ui/*.py` 정적 검증 5종 | [x] | ProjectP에서 이식 + B 경로로 패치 |
| 0.4 | `Docs/DELTA_SURVEY.md` (B 현황 조사) | [ ] | |
| 0.5 | `Docs/PORTING_SAVE.md` (저장 파이프라인 B판) | [ ] | |

**게이트:** `python Tools/ui/verify_dead_events.py .` 가 실행되고 결과가 나온다.

---

### 0.5 UI 프리팹 추출 (선행 작업)

씬에서 다이얼로그를 **프리팹으로 뽑는다**. 씬 인스턴스 제거(=분리)는 10단계지만,
빼도 부팅·씬전환 경로가 안 막히는 것은 지금 빼도 무방하다 — 대신 **알려진 미동작에 기록**한다.

> ⚠️ **`CombatPowerUIPrefabBuilder`를 먼저 비활성화할 것.** 살아 있으면 에디터 재기동마다
> LobbyScene을 덮어써 추출 작업이 유실된다. (1.1 항목을 여기로 당겨온다)

**규칙**
- 계층 구조·이름·컴포넌트 구성을 바꾸지 않는다. 지금은 "위치만 옮기는" 작업이다.
- 씬 인스턴스로 연결된 경우 오버라이드를 `Apply All`로 0으로 만든다.
- 폴더는 임시 위치(`Assets/Prefab/Refactory/`)를 쓴다. 최종 규약은 11단계에서 정한다.
- 다이얼로그 단위로 커밋한다(한 커밋에 몰면 이분 탐색이 안 된다).

#### `IDialog` 파생 17개 현황

| # | 클래스 | 씬 | 프리팹 | 씬 인스턴스 |
|---|---|---|---|---|
| 1 | `UIDiceGrowthDetailPanel` | Lobby | [x] | 연결됨 |
| 2 | `UIDiceGrowthPage` | Lobby | [x] | 연결됨 |
| 3 | `UIEquipmentConfirmDialog` | Lobby | [x] | `UIEquipmentPage` 내부 중첩 (별도 프리팹은 중복) |
| 4 | `UIEquipmentPage` | Lobby | [x] | 연결됨 |
| 5 | `UIMergePopup` | Lobby | [x] | 연결됨 |
| 6 | `UIRelicDialog` | Lobby | [x] | 기존 프리팹 — 작업 불필요 |
| 7 | `UIRelicSummonDialog` | Lobby | [x] | 기존 프리팹 — 작업 불필요 |
| 8 | `UIRewardResultDialog` | Lobby | [x] | 연결됨 |
| 9 | `UIStageRewardDialog` | Lobby | [x] | 연결됨 |
| 10 | `UIStageStarDialog` | Lobby | [x] | 연결됨 |
| 11 | `UIStageStarRewardDialog` | Lobby | [x] | 연결됨 |
| 12 | `UIBattleDiceDetailPanel` | Battle | [x] | 연결됨 |
| 13 | `UIDiceCraftPanelDialog` | Battle | [x] | 연결됨 |
| 14 | `UIDiceCraftProgressDialog` | Battle | [x] | 연결됨 |
| 15 | `UIElementUpgradePanel` | Battle | [x] | 연결됨 |
| 16 | `UIStageResultDialog` | Battle | [x] | 연결됨. 빼도 안전 — `GameManager.ShowStageResult`에 null 가드(`CoReturnToLobby`) 있음 |
| 17 | `UIWaveRewardPreviewDialog` | Battle | [x] | 연결됨. 빼도 안전 — 호출부가 `?.Exit()` |

`UIIdleRewardDialog`는 `IDialog` 파생이 아니다(프리팹 없이 코드로 전량 조립). 10단계 재작성 대상.

**게이트:** `Tools/ui` 검증 5종이 새 오류 0건. Title→Lobby→Battle→Lobby 왕복 유지.

검증 결과 (프리팹 67개 / 파일 71개 기준):

| 도구 | 결과 |
|---|---|
| `verify_dead_events` | 1건 — `Prefab/Dice/UIDice.prefab` (추출 이전부터 있던 것, 미해결 목록 참조) |
| `verify_components` | 0건 |
| `verify_prefab_refs` | 0건 |
| `verify_field_types` | 검사 대상 0개 — B는 `*Button` 필드 명명 규약을 쓰지 않아 현재 무의미. 10단계에서 규약이 생기면 유효해진다 |

---

### 1. 안전장치

| # | 항목 | 상태 | 비고 |
|---|---|---|---|
| 1.1 | `CombatPowerUIPrefabBuilder` 비활성화 | [x] | `[InitializeOnLoad]` + 자동 설치 경로 제거. 메뉴 항목은 유지 |
| 1.2 | 인코딩 정규화 (UTF-8) | [x] | CP949 2파일 변환 + U+FFFD 2파일 처리. 138파일 전부 UTF-8 |
| 1.3a | 평문 키스토어 비밀번호 **코드에서 제거** | [x] | 환경 변수 주입 + 미설정 시 빌드 중단 |
| 1.4 | `.meta` 추적 / `.gitignore` 상태 확인 | [x] | 위반 0건. 아래 감사 결과 참조 |
| 1.5 | **Missing script 기준선 기록** | [x] | **기준선 = 0건.** 11단계 게이트는 이 값과 비교한다 |

**게이트:** 에디터를 재기동해도 LobbyScene diff가 오염되지 않고, 전체 `.cs`가 UTF-8로 디코딩된다.
→ `[InitializeOnLoad]`가 사라져 에디터 기동 시 `LobbyScene`을 여는 코드 경로가 없다.
→ `python Tools/verify_encoding.py .` 통과 (138파일, Plugins 제외).

#### 1.2 인코딩 — 파일별 처리 내역

**두 종류를 구분해야 한다.** CP949 원시 바이트는 무손실 복구되지만, U+FFFD가 박힌 파일은
원래 글자가 파일에 남아 있지 않다. 후자는 git 히스토리에 성한 리비전이 있는지가 갈림길이다.

| 파일 | 상태 | 처리 |
|---|---|---|
| `Dice/TypeUIComponent.cs` | CP949 | UTF-8 변환 (무손실) — `타입 UI 배경`, `BG 색상만 변경` |
| `Dice/UIRemoveDice.cs` | CP949 | UTF-8 변환 (무손실) — `원본 다이스 제거` |
| `Define/Define.cs` | U+FFFD 8개 | **`bfa569d`(2025-09-25)에서 원문 복구** — `우리팀` / `상대팀` |
| `Build/Editor/Unity3dBuilder.cs` | U+FFFD 34개 | 주석 재작성. 최초 커밋 `0e8a2f8`부터 이미 파손이라 복구 불가 |

`Define.cs`는 규약이 예상한 "재작성"이 아니라 **원문 복구**가 됐다. CRLF·BOM 부재·후행 개행을
보존했고, 주석을 제거한 코드 라인이 변경 전후 완전히 동일함을 확인했다.

#### 1.4 `.meta` / `.gitignore` 감사 결과

| 검사 | 결과 |
|---|---|
| 에셋에 `.meta`가 추적되지 않음 | 0건 (에셋 915 / meta 1001, 차이는 폴더 meta) |
| 짝 없는 고아 `.meta` | 0건 |
| `.meta`가 `.gitignore`에 걸림 | 0건 |
| SO 에셋 추적 (절대 규칙 5) | 13개 전부 `.meta`와 함께 추적 중 |
| `Library`/`Temp`/`Logs` 추적 누출 | 0건 |

ProjectP의 "`Content/*.asset`이 untracked" 실수는 이 리포에 없다.

> `.gitignore`가 루트와 `DuDuDuDU_Project/` 두 곳에 있다. 실제로 `Library/`를 막는 것은
> **후자**다 — 루트의 `/[Ll]ibrary/`는 앵커가 리포 루트라 `DuDuDuDU_Project/Library/`에
> 걸리지 않는다. 지운다면 루트 쪽이지 프로젝트 쪽이 아니다.

#### 1.5 Missing script 기준선

```
기준선 = 0건   (에셋 84개 / MonoBehaviour 928개 / guid 9,015개 수집)
```

11단계 게이트는 `python Tools/verify_missing_scripts.py . --baseline 0`으로 판정한다.
기준선이 0이므로 11단계에서 **1건이라도 생기면 그건 전부 새로 만든 것**이다.

> 이 도구는 `Library/PackageCache`를 함께 읽는다. TMP·uGUI 컴포넌트의 guid가 거기 있어서
> 빼면 정상 참조 928개가 통째로 Missing으로 오탐된다. 캐시가 없으면 도구가 중단한다.
>
> **잡지 못하는 것:** guid가 살아 있는 `.cs`를 가리키지만 그 안의 클래스명이 파일명과 다르면
> Unity는 로드에 실패한다. YAML만 봐서는 알 수 없어 이 도구는 통과시킨다.

---

### 2. 조용한 폴백 봉인

> **경로 복구가 봉인보다 먼저다.** `DiceMetaDataDatabase.asset`·`GemDefinitionDatabase.asset`은
> `Assets/ScriptableObject/`에 있어 `Resources.Load`가 **항상** 실패한다. 폴백을 예외로 승격하면
> 그 즉시 이 두 경로가 예외가 되어 Battle/Lobby 직접 재생이 통째로 막힌다.

| # | 항목 | 상태 |
|---|---|---|
| 2.1a | `SingletonPrefab` 경로 + `MonoSingleton` 프리팹 로드 경로 | [x] |
| 2.1b | `StaticResource` 프리팹 추출 (에디터 메뉴 실행) | [x] |
| 2.2 | Provider 3단 폴백 → 명시적 실패로 승격 | [x] |
| 2.3 | MonoSingleton 자동 생성 봉인 | [x] |

**게이트:** Battle/Lobby를 직접 재생하면 `StaticResource` 부재와 DB null이 **명시적 에러 로그**로 뜬다.
→ **통과 (2026-08-28).** BattleScene 직접 재생 + F6 진단 결과:

```
StaticResource.Instance : StaticResource      (DB 6/6 전부 연결)
DiceMetaDataProvider.Database   : DiceMetaDataDatabase
RelicDatabaseProvider.Database  : RelicDatabase          ← RuntimeRelicDatabase 아님
StageDatabaseProvider           : StageDatabase
StageRewardDatabaseProvider     : StageRewardDatabase
GameManager.Instance : HuntingManager / CurrentStageData 있음
```

**`LogError`가 한 줄도 없다 = 폴백이 하나도 발동하지 않았다.** 4개 Provider가 전부 진짜
에셋으로 해석된다. Lobby↔Battle 왕복도 정상.

> 이 확인이 가능해진 것 자체가 2단계의 산물이다. 예전에는 폴백이 조용해서
> "지금 에셋을 쓰는가 코드 기본값을 쓰는가"를 **구분할 방법이 없었다.**

**진행 중 겪은 회귀 (기록).** 폴백을 null 로 바꾼 첫 시도에서 배틀 플레이 버튼이 죽었다.
원인 후보가 둘이었고 — 무방비 `StaticResource.Instance` 역참조와 null 반환 Provider —
**둘 다 고쳐서 해결됐기 때문에 어느 쪽이 진범이었는지는 확정하지 못했다.**
둘 다 실재하는 결함이라 되돌릴 이유는 없다.

#### 2.1 조사 결과 — 폴백이 6개 DB에 균등하지 않다

착수 전 가정("에셋 2개를 `Resources/`로 옮기면 된다")은 **6개 중 2개만 고친다.**

| DB | 1단 `StaticResource` | 2단 `Resources.Load` | 직접 재생 시 |
|---|---|---|---|
| `RelicDatabase` | TitleScene | `Resources/`에 있음 → 성공 | 정상 |
| `DiceMetaDataDatabase` | TitleScene | `ScriptableObject/`라 **항상 실패** | 코드 기본값 |
| `GemDefinitionDatabase` | TitleScene | **항상 실패** | null |
| `PointMetadataDatabase` | TitleScene | **폴백 자체가 없음** | null |
| `StageDatabase` | TitleScene | **폴백 자체가 없음** | null |
| `StageRewardDatabase` | TitleScene | **폴백 자체가 없음** | null |

`StaticResource`는 TitleScene에 1개뿐이고(Lobby/Battle/UITutorial 0개) 그 인스턴스가 6개를 전부 물고 있다.
그래서 **경로를 고치는 대신 `StaticResource` 자체를 되살리는 쪽**을 택했다 — 하나로 6개가 해결되고,
2.2에서 Provider의 2단·3단은 고칠 대상이 아니라 **지울 대상**이 된다. 11.6의 Resources 경로 정리와도 맞다.

**구현 (2.1a)**

- `SingletonPrefabAttribute` 신설. 붙은 타입은 씬에서 못 찾았을 때 **빈 객체 대신 프리팹**에서 만들어진다.
- 프리팹이 없으면 빈 객체로 물러서지 않고 `LogError` 후 `null`을 돌려준다(로그는 1회만).
- `StaticResource`에 `[SingletonPrefab("StaticResource")]`. **경로 정본은 이 한 줄이고**,
  에디터 추출 도구가 이 어트리뷰트를 리플렉션으로 읽어 저장 위치를 정하므로 둘이 갈라지지 않는다.

**추출 전 확인한 것:** TitleScene의 `StaticResource` 참조 62개 중 씬 내부를 가리키는 것은
자기 자신(`m_GameObject`) 하나뿐이고 나머지 58개는 전부 프로젝트 에셋이다.
→ 프리팹으로 뽑아도 **끊길 참조가 없다.** (`m_IsActive: 1`도 확인)

**남은 실행 (2.1b)** — 에디터에서 한 번 눌러야 한다. 파일시스템으로 하면 절대 규칙 1 위반.

```
Tools/OJ/Static Resource/Extract Prefab From TitleScene
```

도구가 `Assets/Resources/StaticResource.prefab`을 만들고 씬 인스턴스를 그 프리팹에 연결한 뒤,
**저장된 프리팹을 다시 읽어 DB 6개가 전부 연결됐는지 스스로 검증**한다. 하나라도 비면 `LogError`.

**실행 결과 (검증 완료)**

| 확인 | 결과 |
|---|---|
| 데이터베이스 | 6/6 연결 (씬 원본과 동일) |
| 에셋 참조 총량 | 58/58 — 씬 원본과 다중집합 정확히 일치 |
| 씬 상태 | 원시 MonoBehaviour 블록이 사라지고 `PrefabInstance`로 전환 |
| 오버라이드 | Transform 좌표·회전 + `m_Name`뿐 (Unity가 항상 쓰는 항목) |
| 리스트 | Element 5 / Rarity 0 / Equipment 6 / StageTheme 5 — 전부 씬 원본과 동일 |
| Missing script | 0건 (기준선 유지) |

> `RarityResources`가 0인 것은 **추출로 잃은 게 아니라 씬 원본이 이미 `[]`**였다.
> 따라서 `GetRarityResource()`는 지금도 항상 null을 돌려준다. 회귀가 아니므로 여기서 고치지 않되,
> 4단계(수치 정본 확정)에서 이 리스트를 채울지 아니면 게터를 지울지 판정한다.

#### 2.2 폴백 5곳 — "제거"가 아니라 "소리 나게"

> **범위 결정 (2026-08-28).** 처음엔 폴백을 지우고 `null`을 돌려주게 했다. 그러자 배선이
> 어긋나는 순간 NRE 연쇄로 번져 **게임을 켜서 확인하는 것 자체가 막혔다.** 소비처는 5~8단계에서
> 전부 다시 쓰이므로(무방비 역참조만 `RelicManager` 7곳 + 보상 4곳 + 스테이지 3곳),
> 지금 그걸 방어하는 건 버리는 일이다. **게이트가 요구하는 것은 "명시적 에러 로그"지 크래시가 아니다.**
> 그래서 폴백은 남기고 **조용함만 없앤다.**

| 지점 | 옛 2단 | 옛 3단 | 이제 |
|---|---|---|---|
| `DiceMetaDataProvider.Database` | `Resources.Load` **항상 실패** | (`GetMeta`가 코드 기본값 처리) | `LogError` + null¹ |
| `RelicDatabaseProvider.Database` | `Resources.Load` 성공 | `CreateRuntimeDefault()` 유물 24종 | `LogError` + 폴백 유지 |
| `StageDatabaseProvider.GetDatabase` | 없음 | `PopulateDefaults(30)` | `LogError` + 폴백 유지 |
| `StageRewardDatabaseProvider.GetDatabase` | 없음 | `PopulateDefaults(10)` | `LogError` + 폴백 유지 |
| `EquipmentManager.GetGemDefinitionDatabase` | `Resources.Load` **항상 실패** | 없음 (조용히 null) | `LogError` + null² |

¹ 외부 소비처가 없고 `GetMeta`가 null을 처리한다. ² 유일한 소비처가 이미 null을 검사한다.
**둘 다 원래도 null이었으므로 새 크래시 경로가 아니다.**

**그래서 실제로 없앤 것은 세 가지다.**

1. **죽은 2단.** `Resources.Load`로 DB를 찾던 3곳 중 2곳은 에셋이 `Assets/ScriptableObject/`에
   있어 **한 번도 성공한 적이 없다.** 실패를 조용히 3단으로 넘기기만 했다.
2. **`isAlive` 순서 의존.** 아래 참조.
3. **폴백이 캐시에 눌러앉던 것.** 이제 **진짜 에셋만 캐시**하고 폴백은 따로 들고 있는다.
   그래서 `StaticResource`가 늦게 살아나면 다음 호출부터 진짜 값으로 올라탄다.
   `RelicDatabaseProvider`에 있던 `database.name == "RuntimeRelicDatabase"` 재확인 코드가
   바로 이 문제에 대한 우회였고, 이제 필요 없어져 사라졌다.

지우기 전에 **에셋에 실데이터가 있는지 전수 확인**했다 — 유물 24 / 다이스 15 / 보석 56 /
스테이지 30 / 마일스톤 90 / 재화 18. 비어 있는 에셋은 없다.

> **남은 빚.** 폴백이 살아 있는 한 "기본값 게임"으로 흘러가는 경로 자체는 남아 있다.
> 지금은 **로그로 드러나므로** 감지는 된다. 진짜 제거는 소비처가 정리되는 5~8단계에서 한다.

> **`StaticResource.isAlive` 가드도 함께 뺐다. 이게 가장 조용한 범인이었다.**
> `isAlive`는 `_instance != null`일 뿐 **인스턴스를 만들지 않는다.** 그래서 아무도
> `StaticResource.Instance`를 건드리기 전에 Provider가 먼저 호출되면 1단을 통째로 건너뛰고
> 기본값으로 내려갔다 — **폴백이 호출 순서에 좌우됐다.** `RelicDatabaseProvider`에 있던
> `database.name == "RuntimeRelicDatabase"` 재확인 코드가 바로 그 증상에 대한 임시방편이었다.

코드 기본값 생성기(`CreateRuntimeDefault`, `PopulateDefaults`)는 **호출부만 끊고 메서드는 남겼다.**
4.1에서 에셋과 대조해 덤프해야 하므로 지금 지우면 그 기준을 잃는다.

`UIEquipmentText`(58·86줄)와 `PointRewardEntry`(90줄)에도 같은 `isAlive` 가드가 남아 있다.
Provider가 아니라 UI 표시 경로라 이번 범위 밖이지만 **같은 병이다** — 10단계에서 정리한다.

#### 2.3 자동 생성 봉인 — 제거가 아니라 opt-in 전환

`MonoSingleton` 파생은 **4개뿐**이다(나머지 `static Instance` 21개는 8.3 소관).
셋은 씬·프리팹 어디에도 없고 `[SerializeField]`도 0개인 **순수 런타임 서비스**라,
자동 생성이 유일하면서 정당한 생성 경로다. 그래서 없애는 대신 **선언하게 했다.**

| 타입 | 선언 | 근거 |
|---|---|---|
| `StaticResource` | `[SingletonPrefab("StaticResource")]` | 인스펙터 참조 58개가 정본 |
| `DiceLevelManager` | `[SingletonAutoCreate]` | 직렬화 필드 0, 배치 없음 |
| `EquipmentManager` | `[SingletonAutoCreate]` | 직렬화 필드 0, 배치 없음 |
| `AOSBackBtnManager` | `[SingletonAutoCreate]` | 직렬화 필드 0, 배치 없음 |

**둘 다 없으면 만들지 않고 `LogError` 후 null.** 기존 4종은 전부 선언했으므로 동작 변화가 없다 —
이 봉인이 실제로 무는 대상은 **앞으로 추가될 타입**이다. 배치를 빠뜨린 채 `.Instance`를 부르면
빈 객체가 조용히 생기는 대신 즉시 운다.

> 어트리뷰트 선택 기준: **직렬화 필드가 하나라도 생기면 `[SingletonAutoCreate]`는 틀린 선택이다.**
> 그 값이 빈 채로 만들어지기 때문이다. 그때는 `[SingletonPrefab]`으로 옮겨야 한다.

**에디터 모드 가드도 넣었다.** 프리팹 기반 싱글톤은 `Application.isPlaying`이 false면 만들지 않는다.
2.2로 Provider가 실제로 `Instance`를 호출하게 됐으므로, 에디터 도구가 Provider를 건드리면
프리팹이 열린 씬에 심어져 **1.1에서 걷어낸 병이 그대로 재발**한다.

#### 2.x 곁다리 — 배틀 초기화 중단 방어

`GameManager.ApplyStageTheme()`와 `MonsterSpawner.ConfigureTheme()`이 `StaticResource.Instance`를
무방비로 역참조했다. 둘 다 `GameManager.InitializeStage()` 안에서 불리므로, 여기서 터지면
**초기화가 중간에 끊긴다** — `WallHp`·시작 SP·웨이브 수가 설정되지 않고, `Start()`의 다음 줄
`ChangeState(InGameState.Setting)`도 실행되지 않아 **플레이 버튼이 살아나지 않는다.**

배경 하나 때문에 스테이지 초기화를 통째로 잃을 이유가 없어 둘 다 가드를 넣었다.
`StaticResource` 부재 자체는 `MonoSingleton`이 이미 크게 운다.

#### 개발용 씬 핫키 (`DevSceneHotkeys`)

`UNITY_EDITOR || DEV_DEFINE` 에서만 존재한다.

| 키 | 동작 |
|---|---|
| F1 / F2 / F3 | Title / Lobby / Battle |
| F5 | 현재 씬 재로드 |
| F6 | 배선 진단 덤프 (`StaticResource` + DB 6종 + `GameManager`) |

> 필요한 이유가 코드에 그대로 있다. **로비로 가는 유일한 길인 정지 버튼은 `InGameState.Wave`
> 일 때만 켜진다**(`GameManager.ChangeState`). 즉 웨이브를 시작하지 못하면 로비로 돌아갈 방법이
> 아예 없어 플레이를 껐다 켜야 한다. 9단계에서 `SceneRouter`가 생기면 전환은 그쪽을 타게 바꾼다.
>
> F6은 2단계 전용 도구다. 조용한 폴백이 사라졌으므로 **여기서 `<null>`로 찍히는 줄이 곧 배선 사고다.**

---

### 3. 씨앗 asmdef + 특성화 테스트

목표는 "현행 동작을 그대로 고정"이지 개선이 아니다. 골든값을 박아둔다.

| # | 항목 | 상태 | 비고 |
|---|---|---|---|
| 3.0 | 골든 기준선 확보 (개조 **전**) | [x] | `Tests/Golden/formula_baseline.txt`. 플레이 중 F7 |
| 3.1 | 씨앗 asmdef (`OJ.Core`, `autoReferenced: true`) | [x] | **이동이 아니라 추출**. 아래 참조 |
| 3.2 | 4개 계산식 파라미터 주입형으로 최소 개조 | [x] | 적대적 검증 4건 CONFIRMED |
| 3.3 | 테스트 asmdef + 골든 테스트 | [x] | `OJ.Core.Tests` — EditMode 305 케이스 전부 통과 |

**게이트:** 골든 테스트가 전부 통과하고, 개조 전후 실플레이에서 데미지·보상 수치가 동일하다.
→ **통과 (2026-08-28).**
- Unity Test Runner EditMode: **305/305 통과** (테스트 메서드 33개가 `TestCaseSource`로 펼쳐진 것)
- F7 재덤프: `[environment]` 435개 / `[stable]` 991개 **값 변경 0**

#### 3.3 무엇을 잠갔나

| 테스트 | 소비한 골든 키 |
|---|---|
| `StageGrowthFormulaTests` | 135 |
| `StageRewardFormulaTests` | 68 |
| `IdleRewardFormulaTests` | 79 |
| `DamageFormulaTests` | 7 |
| **합계** | **289 = `core.*` 전량** |

**골든 `core.*` 키를 한 개도 남기지 않고 소비한다.** 일부만 검사하면서 통과하는 테스트가
가장 위험하므로, 각 테스트에 "담당 접두사의 키 개수와 테스트 케이스 수가 일치하는지" 세는
테스트를 함께 넣었다.

**허용 오차를 쓰지 않는다.** `Within`/`delta`가 0건이다 — float/double을 비트 동일로 비교한다.
허용 오차를 두면 반올림 회귀를 놓치는데, 그게 정확히 `noEngineReferences` 전환(11.1)에서
터질 종류의 사고다.

> **테스트가 검증할 수 있는 범위.** `OJ.Core.Tests`는 `OJ.Core`만 참조하므로
> **`Assembly-CSharp`을 볼 수 없다.** 그래서 에셋에서 나온 구획(`stage.*` / `dice.*` /
> `reward.*` / `idle.*` / `autoReward.*`)은 재현이 불가능하고, **순수 함수를 기본형 인자로
> 두드린 `core.*` 구획만** 자동 검증 대상이다. 나머지는 사람이 diff로 보는 기록이다.

#### 3.3 진행 중 겪은 것 — 통합 지점은 따로 봐야 한다

첫 실행에서 **39개가 0.064초 만에 전부 실패**했다. 개별 assert가 아니라 공용 리더가 로드
단계에서 예외를 던진 것이었고, 원인은 내가 쓴 코드 두 곳이었다.

1. 덤퍼가 `reward.rewardFlags[Minimum]`을 벽 체력 표본 0/1/49에서 **세 번** 뱉었다(값은 동일).
2. 리더가 **중복 키를 무조건 예외**로 던졌다. "조용한 덮어쓰기를 막겠다"고 넣은 가드가
   무해한 중복까지 잡았다.

→ 리더는 **값이 다른 중복만** 막도록 고쳤고(원래 막으려던 것이 그쪽이다), 덤퍼는 등급별로
한 번씩만 적도록 고쳤다.

> **교훈.** 리뷰 에이전트 4개가 각자 자기 테스트를 변이 검사까지 했는데도 이걸 못 잡았다.
> 각자 자기 파일만 봤지 **공용 리더가 실제 골든 파일을 물었을 때**를 아무도 굴려 보지 않았다.
> 팬아웃 리뷰에는 "실제 데이터로 끝까지 한 번 굴려 보라"를 반드시 넣을 것.

#### 3.1 왜 "이동"이 아니라 "추출+위임"인가

베이스라인 원문은 "테스트 대상 계산식만 이동"이지만, 조사 결과 이동하면 연쇄가 감당이 안 된다.

| 계산식 | 이동 시 딸려오는 것 |
|---|---|
| `CalculateDamage` | 싱글톤 4종 + `DiceType` |
| `StageData` 성장식 | `[Min]` 속성, `StageDatabase`가 직렬화 중 |
| `StageRewardCalculator` | `PointType`, `StageClearGrade`, `PointRewardEntry` → **이건 `StaticResource`를 참조한다** |
| `IdleReward` 환산 | `StageProgressManager`, `PointManager` |

게다가 **에디터가 켜져 있으면 파일 이동은 절대 규칙 1 위반**이다.
그래서 새 폴더 `Assets/Scripts/Core/`에 asmdef를 두고 **순수 계산만 새 파일로 쓰고 기존
파일이 위임**한다. 파일 이동 0건 = GUID 위험 0. 11단계에서 이 경계를 키우면 된다.

`OJ.Core`는 `Assembly-CSharp`을 참조할 수 없으므로 **파라미터·반환이 전부 기본형**이다.
enum이 필요한 곳은 호출부에서 계산된 값(배수, 티어 int)을 넘긴다.

#### 3.2 산술 동일성 — 무엇으로 증명했나

표현식을 문자 그대로 옮겼다(연산 순서·괄호·중간 변수 타입·`Mathf` 호출 전부 보존).
병렬 검증 에이전트 4개가 **실제 `UnityEngine.CoreModule.dll`을 링크해** 원본과 신규를
한 프로세스에서 차분 실행했다.

| 계산식 | 케이스 | 결과 |
|---|---|---|
| `StageGrowthFormula` | 29,046,736 | 불일치 0 |
| `StageRewardFormula` | 687,876 | 비트 동일 |
| `IdleRewardFormula` | 501,797 (x64/x86 양쪽) | 실패 0 |
| `DamageFormula` | 6,272,400 (**반올림 직전 float 비트까지**) | int 불일치 0 |

> **`DamageFormula`의 미묘한 지점.** 싱글톤 null 분기를 중립값 주입으로 바꿨는데,
> 배수가 음수면 `-0f`가 생겨 반올림 전 비트가 갈린다(134,190건). 반환값은 바깥
> `Mathf.Max(1, Mathf.RoundToInt(...))`가 `+0f`/`-0f`를 둘 다 1로 눌러 동일하다.
> **즉 동일성이 그 하한에 기대고 있다** — 하한을 없애거나 float를 그대로 반환하게 바꾸면
> 새어 나간다. `DamageFormula.cs` 주석에 명시해 뒀다.

#### 3.x 골든의 검출력 구멍 — 검증이 잡아낸 것

첫 기준선은 **두 종류의 변이를 전혀 못 잡았다.** 검증 에이전트가 실제로 변이를 심어 확인했다.

- 30스테이지가 전부 `monstersPerWave = 20`(짝수)이라 `CeilToInt(x * 0.5f)` → `x / 2`
  정수 나눗셈 변이가 **231줄 중 하나도 안 틀린다.**
- 전부 `baseMonsterDefense = 0`이라 `ResolvedBaseDefense`의 조기 반환 분기가 안 밟힌다.
- `DumpIdleConversion`이 식을 **덤퍼 안에 다시 써서** 비교하고 있었다 — 코드를 어떻게 바꿔도
  항상 일치하는 무의미한 검사였다.

→ `## core.*` 구획을 추가해 메웠다. 홀수 `monstersPerWave`, `baseMonsterDefense > 0`,
`totalWaves` 10/20 분기, 0.5/0.999 임계값, 서브밀리초 tick을 **일부러 밟는** 합성 입력이다.
전부 순수 함수 + 기본형 인자라 `OJ.Core.Tests`가 재현할 수 있다 — **에셋 기반 구획과 달리
테스트 어셈블리에서 검증 가능한 유일한 부분**이다.

`stage[N].in.*` 입력 필드도 추가했다. 없으면 테스트가 에셋을 못 읽어 재현이 불가능하다.

> **교훈:** "골든이 통과했으니 안전"은 골든이 그 축을 실제로 밟을 때만 성립한다.
> 기준선을 만들 때는 **변이를 심어 검출되는지 먼저 확인**할 것.

#### 3.x `noEngineReferences`를 아직 켜지 않은 이유

AGENTS.md는 `OJ.Core`에 `noEngineReferences: true`를 요구한다. **지금은 `false`다. 의도적이다.**

`Mathf` → `System.Math` 전환이 무해하지 않다 — `Math.Round`는 은행가 반올림이고
`Mathf.RoundToInt`는 `Floor(f + 0.5f)`라 **.5 경계에서 다르다.** 기준선에 정확히 .5인 값이
최소 4건 있어(`damage[Normal][pip=1][lv=6]` = 16.5 등) 전환하면 그대로 어긋난다.

안전망을 먼저 세우고 **11.1에서 뒤집는다.** 그때는 골든 테스트가 전환을 지켜 준다 —
"회귀 안전망이 도메인 수술보다 먼저"라는 이 문서의 원칙 그대로다.

#### 3.x 남은 것

| 항목 | 비고 |
|---|---|
| `[environment]` 구획은 커밋 골든으로 잠글 수 없다 | 세이브 진행도가 섞여 기기마다 다르다. 같은 기기 전후 diff로만 검증 |
| `StageRewardDatabase.cs:138`에 `GetStageBonus` 중복 | 식이 문자 그대로 동일. 4단계에서 정리 |
| `BuildAutoBattleRewards`의 난수 경로 | 미추출. `System.Random` 시드 고정분만 골든에 있다 |

---

### 4. 수치 정본 확정 (SO 승격)

> 순서를 지킬 것. `DiceMetaDataDatabase.asset`의 일부 값이 코드 fallback과 **다르므로**,
> 덤프 없이 덮어쓰기부터 제거하면 밸런스가 조용히 바뀐다.

| # | 항목 | 상태 |
|---|---|---|
| 4.1 | 코드 fallback 값을 asset에 1회 덤프 | [x] **문구만.** 수치는 덤프하지 않았다 — 아래 참조 |
| 4.2 | 골든 테스트로 덤프 전후 동일성 증명 | [x] |
| 4.3 | `MergeMeta` / `ApplyMonsterHpBalance` 제거 | [x] |
| 4.4 | SO → 텍스트 덤프 검증 스크립트 | [x] `GoldenBaselineDumper`가 그 역할을 한다 |

**게이트:** 덮어쓰기 제거 후에도 골든 테스트가 **값 변화 없이** 통과한다.
→ **통과 (2026-08-28).** F7 재덤프 `[stable]` 1463키 / `[environment]` 435키 **값 변경 0**,
헤드리스 러너 496/496.

#### 4.1 이 문서의 전제가 틀렸다 — 수치는 덤프할 게 없었다

위 경고("`DiceMetaDataDatabase.asset`의 일부 값이 코드 fallback과 **다르므로**")는
**수치에 대해서는 사실이 아니었다.** 사전 조사(에이전트 6개)가 전수 대조한 결과:

| 데이터베이스 | 에셋 vs 코드 산출 |
|---|---|
| `StageDatabase` 30스테이지 | **차이 0** (900개 웨이브 값) |
| `RelicDatabase` 유물 24종 | **차이 0** (표시 텍스트 3필드까지) |
| `StageRewardDatabase` 90 마일스톤 | **차이 0** |
| `DiceMetaDataDatabase` 수치 필드 | **차이 0** |

**오히려 수치를 덤프했으면 게임이 깨졌다.** 코드의 킹 다이스 5종 강화 비용이 `0`이고
에셋에는 실제 값(260/270/255/280/250)이 있다. `GetUpgradeCost`가 "meta 비용이 전부 0이면
코드 표를 쓴다"로 우회하고 있어 지금은 무해하지만, 코드값을 덤프한 뒤 그 우회를 지우면
**강화가 공짜가 된다.** 골든 40행으로 재현 확인됐다.

**차이가 있었던 것은 표시 문구뿐이다.** `MergeMeta`를 걷어내자 에셋의 낡은 판이 드러났다 —
`description` 3건 + `milestones` 5종. "전이"가 "공격 대상"으로 바뀌어 연쇄 번개라는
메커니즘 설명이 사라지고, King 계열의 "소환 중인 동안" 조건이 빠지고, `KingNormal`은
3연타 설명이 통째로 없어졌다. 그래서 `DiceTextPromoter`(에디터 메뉴)로 **문구만** 옮겼다.
에셋 diff는 `description:` 26줄뿐이고 수치 필드는 하나도 건드리지 않았다.

> **교훈.** 이 문서가 "값이 다르다"고 적어 둔 것을 그대로 믿고 4.1을 실행했으면
> 킹 다이스 강화가 공짜가 됐을 것이다. **계획 문서의 전제도 착수 전에 측정해야 한다.**

#### 4.3 무엇으로 증명했나

제거 전후 코드를 각각 컴파일해 **한 Mono 프로세스에 올려 직접 대조**했다.

| 대조 | 결과 |
|---|---|
| `stage.*` 329줄 — 골든 vs 제거 전 vs 제거 후 | **3중 불일치 0** |
| `dice.damage` 435키 | 불일치 0 |
| 킹 강화 비용 40키 | 그대로 |
| 폴백 경로 1,080건 비트 비교 | 차이 0 |
| 에셋 부재 경로 출력 해시 | 제거 전후 동일 |

`StageDatabase`의 코드 전용 diff는 정확히 `ApplyMonsterHpBalance();` **3줄 삭제**뿐이다.

> **`GetUpgradeCost`의 킹 비용 우회를 지우지 말 것.** 작업자가 "이 분기는 죽었다"고 주석을
> 달았는데 거짓이었다. 에셋이 없거나 해당 타입이 미등재면 **그 분기만이 강화가 공짜가 되는
> 것을 막는다.** 검증자가 실측으로 잡아 주석을 고쳤다.
>
> **`GetMeta`가 이제 에셋 실인스턴스를 반환한다** (예전엔 딥카피). 현재 호출자 7곳은 전부
> 읽기 전용이지만, **쓰기를 추가하면 `.asset`이 더러워진다.**

---

### 5. 도메인 순수화 (최대 덩어리)

> 착수 전에 **`RunState`의 필드 목록(스키마)만 먼저 확정**한다. 상태 소유자 없이 규칙만 순수화하면
> 6단계에서 시그니처를 전부 다시 고친다.

| # | 항목 | 상태 |
|---|---|---|
| 5.0 | `RunState` 스키마 확정 (SP·보드·원소레벨·벽HP) | [x] | 6.1 에서 실제 신설 |
| 5.1 | `CalculateDamage`에서 싱글톤 조회 제거 + 스탯 스냅샷 계약 도입 | [x] |
| 5.2 | `EquipmentManager`(780줄) 상태·규칙 분리 | [x] | 규칙만. 상태는 8.3a |
| 5.3 | `RelicManager`(776줄) 상태·규칙 분리 | [x] | 규칙만. 상태는 8.3a |
| 5.4 | `Monster.TakeDamage` / `Wall.TakeDamage`에서 표시 호출 분리 | [x] | `DamageTextPool`, `RectTransform.sizeDelta` |
| 5.5 | 시간 소스 통일 (`IClock` 추상) | [x] | `Monster` 16곳 적용. 나머지는 점진 | `Time.deltaTime` 직산 + `WaitForSecondsRealtime` + `DateTime.UtcNow` 혼재 중 |

**게이트:** MonoBehaviour 없이 순수 클래스만으로 데미지 계산 경로가 EditMode에서 실행되고 골든값이 유지된다.

> `RelicManager`는 쿨감·피해배율·폭발범위·연쇄+1·스턴확률·독전이·벽부활 등 전용 게터 20여 개가
> 여러 파일에 박혀 있다. **하나만 놓쳐도 그 유물만 무효화되는데 컴파일·콘솔 어디에도 안 나타난다.**

#### 5.0 사전 조사 결과 (에이전트 8개, 2026-08-28)

**판정: RELIABLE 2 / NEEDS_REWORK 2.** 단 둘 다 "조사 자체는 재사용 가능, 다시 하지 않아도 됨"이다.
아래는 그중 **다음 단계 설계를 바꾸는 것**만 추렸다.

**결정 (되묻지 말 것)**

| 항목 | 결정 |
|---|---|
| RNG 소유 | **`RunState`가 시드를 소유한다.** `Seed` + `IRandom`을 스키마에 넣고 호출부를 점진 교체 |
| 잠복 버그 3건 | **기록만 하고 해당 단계에서 고친다.** 지금 고치면 게이트 밖 변경이 섞인다 |

##### ★ RNG에 소유자가 없다 — 5.0의 핵심 구멍

`UnityEngine.Random` 호출이 **34곳**이다(소환 타입·소환 슬롯·머지 결과·크리티컬 3곳·
유물 발동 8곳·몬스터 종류·스폰 위치·쿨감 대상 셔플 등). 전역 static 시드를 쓰고
**판·씬·앱 어디서도 리셋되지 않는다.** `Utils/Extensions.cs:625`에 `static System.Random`이
하나 더 있고 재시드 코드가 0건이다. 결정적인 경로는 `StageRewardCalculator.cs:82`의
`new System.Random(seed)` 하나뿐이다.

> **왜 5.0에서 정해야 하나.** `RunState`를 `noEngineReferences: true`인 `OJ.Core` 순수
> 클래스로 두려는데 **`UnityEngine.Random`은 거기서 못 쓴다.** 시드 소유를 지금 안 정하면
> 6·7단계에서 매퍼 시그니처를 다시 뜯게 된다 — 5.0이 막으려고 존재하는 바로 그 일이다.

##### 잠복 버그 3건 (해당 단계에서 처리)

| # | 증상 | 지금 상태 | 처리 시점 |
|---|---|---|---|
| B1 | `DiceTypeStarManager.ResetAll()`이 **예외로 죽는다** (순회 중 Dictionary 수정). Unity 6000.3.7f1 Mono로 재현 확인 | 호출처 **0곳**이라 잠복 | 5·6단계에서 "판 시작 리셋"을 붙이는 순간 터진다. 그때 고친다 |
| B2 | **시작 주사위가 두 번 적용될 수 있다.** `TryApplyStageStartDice()`가 `UIBoard.Start`와 `GameManager` 코루틴 2곳에서 불리고, 멱등을 지탱하는 플래그를 지우는 `BeginStageRun()`은 `GameManager.Start`에 있다. `UIBoard.Start`가 먼저 돌면 순서가 뒤집힌다 | 스크립트 실행 순서에 의존 | 6단계(런 상태 소유자 통합) |
| B3 | **Escape 키를 한 번 조용히 먹는다.** `IDialog`의 `OnDestroy → Unload → OnExit`가 `_isEnter`를 안 지워서, 파괴된 항목이 스택에 `isEnter == true`로 남고 로비 Escape 루프가 그걸 pop 하고 `return` | 진짜 다이얼로그가 안 닫힌다 | 10단계(UI 프레임워크 교체) |

##### 5.1 착수 전 보완 필요 — 그냥 가면 기능이 조용히 죽는다

데미지 경로 조사가 `NEEDS_REWORK`다. **(A) 공격측 스냅샷 필드 표**와
**(B) "명중 시점에 스냅샷" 권고**는 검증을 통과했으나, 효과별 재조회 표에 누락이 있다:

- **Fire 폭발 범위가 `KingFire` 소환에 반응하지 않게 된다** (그 기능만 조용히 죽는다)
- **Thunder 연쇄가 `MonsterManager` 없이 조립되어 NRE로 죽는다**
- 상태 피해증가 6종의 write 지점 목록이 없어 **받는쪽 스냅샷을 만들 수 없다**

> **경고: 골든 496 테스트가 데미지 경로의 상당 부분을 덮지 않는다.** 모르고 5.1을 커밋하면
> **전부 초록인 채로** 크리티컬·유물배수·방어력 계산이 바뀔 수 있다. 5.1 착수 전에
> 효과 파라미터 표를 다시 뜨고, 덤퍼에 그 축을 추가해 골든을 넓혀야 한다.

##### 조사가 틀렸던 것 (기록 — 같은 오판을 반복하지 않기 위해)

| 주장 | 실제 |
|---|---|
| "`deckTypes` 씬 값이 특수 주사위 5종을 죽이고 있다 — 게임 내용을 바꾸는 유일한 불일치" | **아니다.** 소환 경로에 `IsSummonable` 필터가 있고(`UIDiceSummonSystem.cs:108`) 골든이 합성 5종을 `summonable=False`로 확정한다. **조합으로만 얻는 게 설계다.** 씬이 맞고 코드 초기자 10종이 낡았을 뿐 실효 0 |
| "몬스터 `_hp`가 리셋되지 않아 직전 판 값으로 등록된다" | 순서는 사실이나 **관측 불가**다. `MonsterManager.RegisterMonster`는 리스트 추가만 하고, `SetCombatStats`가 같은 프레임 몇 줄 뒤에 온다. 읽는 코드가 없다 |

> **교훈.** 검증자도 틀린다. 특히 "이건 게임을 망가뜨린다"류 단정은 소비 경로를 끝까지
> 따라가 확인해야 한다. 위 둘은 보고를 그대로 옮겼으면 없는 버그를 고치는 데 시간을 썼다.

---

### 6. 런 상태 소유자 통합

| # | 항목 | 상태 |
|---|---|---|
| 6.1 | `RunState` 신설 및 흩어진 상태 이관 | [x] 벽 HP·웨이브·몬스터 수·게임오버 이관. SP·보드는 남음 |
| 6.2 | 오브젝트 풀 수명 정리 | [x] **이미 정리돼 있었다** — 아래 참조 |

**게이트:** UI를 하나도 띄우지 않고 웨이브 1회를 시뮬레이션해 결과 등급이 산출된다.
→ **미달.** `RunState`는 순수 클래스라 EditMode에서 만들 수 있지만, 웨이브 진행은 아직
`GameManager`(MonoBehaviour)가 소유한다. 시뮬레이션하려면 웨이브 루프까지 순수화해야 하고
그건 6단계 범위를 넘는다. **게이트를 못 채운 채로 넘어간다는 사실을 여기 적어 둔다.**

#### 6.2 — 베이스라인이 실제보다 나쁘게 적혀 있었다

> "Bullet/BulletEffect/DamageText/몬스터 풀이 전부 static"

static 인 것은 맞지만 **셋 다 이미 `OnDestroy`에서 `Instance = null`을 한다.**
씬을 나가면 정리되므로 "static 이라 씬을 넘어 살아남는다"는 문제가 없다.
`BulletEffectPool`은 `effectpool.Clear()`까지 한다. 손댈 것이 없어 그대로 둔다.

#### 6.1에서 드러난 것 — 죽은 직렬화 값

`GameManager.WallHp`와 `WaveMonsterCount`는 `public` 필드라 씬에 직렬화돼 있었는데,
`InitializeStage`가 매번 스테이지 데이터로 덮어써서 **죽은 값**이었다.
인스펙터에서 고쳐도 아무 일도 일어나지 않는다. 프로퍼티로 바꿨다.

#### 남은 것

| 항목 | 왜 안 했나 |
|---|---|
| SP·소환 비용 이관 | `UIDiceSummonSystem`이 소유. `RunState`에 필드는 뒀지만 아직 안 이었다 |
| 보드 위 주사위 | `DiceType` enum 이 필요한데 `OJ.Core`는 그걸 못 본다. int 로 담거나 별도 타입이 필요 |
| **`Seed` 연결** | 필드만 뒀다. `UnityEngine.Random` 호출 34곳을 한 번에 바꾸면 되돌리기 어렵다 |
| 원소 레벨 | `ElementUpgradeManager.ResetRunState()`가 이미 판마다 리셋한다 |

---

### 7. 저장 파이프라인 (스키마까지만)

> **실제 서비스 배선은 8단계 이후.** DI 없이 배선하면 static Instance로 상태에 접근하게 되고
> 8단계에서 전부 다시 쓴다.

| # | 항목 | 상태 | 비고 |
|---|---|---|---|
| 7.1 | Newtonsoft.Json 패키지 추가 | [x] | `OJ.Core.asmdef` 에 `precompiledReferences` 로 물림 |
| 7.2 | 통합 `SaveState` + **버전 필드** | [x] | 기존 유저 세이브는 버림(결정 2) |
| 7.3 | 순수 매퍼 (`ToSave` / `Restore`) | [x] | 이름과 배치는 계획과 다르다 — 별도 매퍼 대신 `ISaveStateOwner.WriteTo`/`ReadFrom` 으로 각 매니저가 자기 몫만 다룬다. 8.7 · 7.5 에서 닫혔다 |
| 7.4 | 원자 쓰기 | [x] | `.writing` → fsync → `File.Replace` (+`.bak`) |
| 7.5 | 구 PlayerPrefs 키 경로 제거 | [x] | **"10키"가 아니라 `ISaveStateOwner` 8개의 키다.** 아래 참조 |
| 7.6 | 개발용 세이브 초기화 치트 | [x] | 메뉴 `OJ/개발/세이브 전부 지우기` |

**게이트:** 저장·로드 왕복 후 모든 값이 동일하고, 구 PlayerPrefs 키가 코드에서 완전히 사라졌다.
→ **앞 절반 통과, 뒤 절반은 8.7 로 넘긴다.** 왕복은 테스트 39건으로 잠갔다(파일 형식 고정 포함).
PlayerPrefs 제거는 배선이라, 이 단계 머리말이 스스로 "실제 서비스 배선은 8단계 이후"라고 적어 둔
바로 그것이다. **게이트 문장이 자기 머리말과 어긋나 있었다.** 뒤 절반은 8.7 의 게이트다.

#### 만들어진 것

| 파일 | 하는 일 |
|---|---|
| `Core/SaveState.cs` | DTO. 버전 + 재화·주사위·유물·장비·스테이지·방치 |
| `Core/SaveSerializer.cs` | JSON 왕복. Newtonsoft 타입은 이 파일 밖으로 안 나간다 |
| `Core/SaveStateMigration.cs` | 버전 정책. **미래 버전은 읽기를 거부한다** |
| `Core/SaveFile.cs` | 원자 쓰기 + `.bak` 되돌리기. `System.IO` 만 쓴다 |
| `Save/SavePaths.cs` | `persistentDataPath/save.json`. 경로를 아는 유일한 곳 |
| `Save/SaveResetCheat.cs` | 7.6 |

#### 설계에서 되짚어 둘 것

**키를 문자열로 둔다.** 기존 코드는 enum 을 정수로 저장했다. enum 에 값을 하나 끼워 넣으면
저장된 숫자가 조용히 다른 것을 가리킨다 — 이 리포에서 보석 `targetDiceType` 이 정확히 그렇게
어긋나 효과 52개가 죽어 있었다. 이름으로 저장하면 순서를 바꿔도 안전하다.
`OJ.Core` 가 enum 을 못 보는 asmdef 제약이 여기서는 오히려 맞는 방향이었다.

**컬렉션은 전부 get-only.** Newtonsoft 는 set 할 수 없는 컬렉션 속성을 기존 인스턴스에 채운다.
그래서 역직렬화 뒤에 null 이 될 수 없고, 매니저 4곳에 있던 `if (xxx == null) xxx = new List<>()`
방어 코드가 통째로 필요 없어진다.

**`SortedDictionary` + `StringComparer.Ordinal`.** 출력이 넣은 순서와 무관하게 항상 같다.
파일을 diff 로 볼 수 있고 "저장→로드→저장 바이트 동일"을 테스트할 수 있다.

**깨진 세이브는 예외로 드러낸다.** `JsonUtility` 는 모양이 틀려도 조용히 기본값을 준다 —
잘린 파일을 먹으면 "전부 0 인 세이브"가 나오고 그게 덮어 쓰이면 진행도가 사라진다.
`SaveFile.Load` 는 `None`(새 게임)과 `Unreadable`(**덮어쓰면 안 됨**)을 구분해서 돌려준다.

**미래 버전은 읽지 않는다.** 앱을 롤백한 유저의 세이브를 "모르는 키는 무시"로 읽으면,
저장하는 순간 새 빌드가 쓴 값이 전부 사라진다. 조용히 일어나고 되돌릴 수 없다.

#### 잡은 것 — `CamelCasePropertyNamesContractResolver` 가 딕셔너리 키까지 바꾼다

흔히 쓰는 그 리졸버는 `ProcessDictionaryKeys` 가 true 라 **키도 소문자로 만든다.**
여기서는 키가 enum 이름이라 `"Gold"` 가 `"gold"` 로 저장되고, 대소문자만 다른 두 키는
하나로 합쳐져 값이 사라진다. 처음 그 설정으로 짰다가 테스트 8건이 잡았다.
`DefaultContractResolver` + `CamelCaseNamingStrategy { ProcessDictionaryKeys = false }` 로 고쳤다.

#### 헤드리스 러너: `package:` 참조

`Newtonsoft.Json.dll` 은 `Library/PackageCache` 에 있고 `ScriptAssemblies` 에는 없다.
러너에 `"package:<파일명>"` 참조를 추가했다. `ScriptAssemblies` 와 달리 에디터가 만들어 내는
산출물이 아니라 **낡을 일이 없어** 참조해도 안전하다. 같은 이름이 여럿이면 경로가 얕은 것을
고른다 — Newtonsoft 는 `Runtime/`(Mono)과 `Runtime/AOT/`(IL2CPP) 두 벌이 들어 있다.

#### 7.5 — 구 키를 지우자 드러난 것들 (2026-08-31)

**"PlayerPrefs 를 지운다"는 한 줄이 실제로는 다섯 가지 일이었다.** 감사 없이 문자 그대로
집행했다면 그중 넷을 놓쳤고, 놓친 것은 전부 **조용히** 깨지는 종류였다.

##### 1. 범위가 틀려 있었다 — "10키"가 아니다

`ISaveStateOwner` 는 **8개**다. `RunHistoryManager` 는 아니고 그 키 `OJ.RunHistory` 는
`SaveState` 밖이다(진단 자료라 일부러 뺐다). "10키 제거"를 문자 그대로 하면 판 기록이
**대체 저장소 없이** 사라진다. `SaveResetCheat` 의 `PlayerPrefs.DeleteAll` 도 남긴다.

##### 2. 초기화가 로드 경로 안에 숨어 있었다 — **신규 설치가 깨진다**

구 `Load()` 안에는 로드가 아닌 것이 섞여 있었다.

| 매니저 | 로드 경로에 숨어 있던 것 | 지웠다면 |
|---|---|---|
| `EquipmentManager` | `InitializeCollections()` · `SeedInitialGemInventory()` | 신규 설치가 시작 보석 0개로 고정. 두 번째 부팅부터는 시드 조건에 안 걸려 **영영 복구 안 됨.** 보석 장착에서 예외 |
| `IdleRewardManager` | 타이머 기준 시각 (`DateTime.UtcNow`) | 자동전투·고기축제 게이지가 **영원히 0**. `ElapsedSeconds` 가 `start <= 0` 을 경과 0 으로 눌러서 예외도 로그도 없다 |
| `PointManager` · `DiceLevelManager` | `isLoaded = true` | 아래 3번 |

**핵심은 `ReadFrom` 이 세이브 파일이 있을 때만 불린다는 것이다** —
`SaveService.TryLoadAll` 은 파일이 없으면 소유자 루프 **전에** 돌아간다.
그래서 초기화를 전부 생성자로 끌어올리고 `ReadFrom` 이 그 위를 덮게 했다.

##### 3. `isLoaded` 가드가 역효과를 낸다

세이브 전멸을 막으려고 넣은 그 가드다. 구 `LoadAll()` 이 `true` 로 만들고 있었으므로,
지우면 **영원히 false** → `WriteTo` 가 건너뜀 → `points: {}` 가 파일에 쓰임 →
다음 부팅에 **정상 파일로 판정되어** 재화 0 확정. 종료 한 번으로 되돌릴 수 없다.
생성자가 초기화를 마친 뒤 `true` 로 둔다 — 가드의 원래 뜻이 "아직 아무것도 안 읽었는데
저장하지 마라"이고, 초기화가 끝났으면 그 상태가 곧 정본이다.

##### 4. 자가치유가 자가파괴로 뒤집힌다 — `SaveService.WriteBlocked`

8.7 까지는 파일이 깨져도 게임이 구 키로 복원된 **진짜 상태**로 돌았고, 종료할 때 그
정상값이 깨진 파일을 덮었다. 7.5 이후에는 같은 상황에서 게임이 **기본값**으로 돌고,
종료할 때 그 기본값이 본 파일과 백업을 **둘 다** 덮는다.

그래서 **읽지 못했으면 쓰지도 않는다.** 단 `SaveSource.None`(파일 없음 = 첫 실행)은
해당하지 않는다 — 거기서 막으면 세이브가 영영 만들어지지 않는다. 그 둘을 가르는 것이
이 가드의 전부다. 에러는 세션당 한 번만 찍고(거래마다 찍으면 로그를 못 읽는다),
상태는 F9 가 계속 보여 준다.

##### 5. 저장 내구성이 떨어진다

구 코드는 강화·소환·보상 수령마다 즉시 디스크에 썼다. 호출 지점을 지우면 **백그라운드
전환 때만** 저장되고, 모바일에서 OS 가 프로세스를 죽이는 것은 일상이다.
그래서 **호출 지점은 그대로 두고 본문만** 통합 저장 호출로 바꿨다.

```csharp
private void Save() => GameContainer.SaveService?.SaveAll();
```

`?.` 가 필요하다 — 컨테이너가 매니저를 만든 **뒤에** `SaveService` 를 해석한다.

##### 곁가지 — 시작 보석 84개를 발견해 껐다

7.5 검증에서 신규 설치를 돌려 보다가 나왔다. `GemDefinitionDatabase.asset` 의
`initialCount` 가 **38종 84개**를 주고 있었고 **레어 등급까지** 포함이었다 —
신규 설치가 6부위 전부에 레어 보석을 하나씩 들고 시작했다.

7.5 로 생긴 것이 아니다. 구 `LoadAll()` 도 세이브가 비면 똑같이 지급했고,
데이터는 `e4f73ea "장비 보석 시스템 우선 백업"` 때 들어왔다. 보석 시스템을 만들며
테스트용으로 넣은 시드가 그대로 남은 것으로 보인다.

**`initialCount` 를 전부 0 으로 내렸다.** `SeedInitialGemInventory()` 자체는
남긴다 — 0 이면 아무것도 주지 않으므로 지울 이유가 없고, 나중에 온보딩 지급이
필요해지면 **데이터 값만** 올리면 된다. 코드를 지웠다면 그때 다시 만들어야 한다.

##### 곁가지

- 매니저 8개에서 `ISaveOnApplicationLifecycle` 을 뗐다. 각자 저장할 것이 없어졌는데
  남겨 두면 앱이 멈출 때 같은 파일을 8번 더 쓴다.
- `ReadFromFileEnabled` 상수를 **삭제**했다. 7.5 후에는 그 레버가 롤백이 아니라
  **전 유저 초기화 스위치**다 — 남기면 언젠가 누가 사고 대응용으로 당긴다.
- `SaveService` 에 재진입 가드를 넣었다. 지금은 `WriteTo` 에서 저장을 부르는 곳이
  없지만, 생기면 무한 재귀로 나타나 원인을 찾기 아주 나쁘다.

> **읽기만 지우고 쓰기를 남기는 것이 전부 지우는 것보다 위험하다.** 기본값이 된 매니저가
> `SaveAll` 에서 구 키 원본까지 덮어 버린다. 그래서 매니저마다 읽기·쓰기를 **함께** 지웠다.

##### 기존 유저

AGENTS.md 「확정된 결정」 2번(기존 유저 세이브 버림)에 따라 **마이그레이션을 만들지
않았다.** 배포된 v1.13.3(versionCode 57)에는 통합 세이브 코드 자체가 없어
`save.json` 이 없으므로, 이 빌드를 받는 순간 결제 재화 포함 전 진행도가 초기화된다.
결정을 내릴 때 그 범위를 알고 있었는지 다시 확인받았고, **유효하다는 답을 받았다.**

---

#### 7.3 · 7.5 를 미루는 이유

매퍼와 PlayerPrefs 제거는 같은 작업의 앞뒤다. 통합 파일로 옮기는 순간 **저장의 단위가
매니저에서 파일로 바뀐다.** 지금처럼 매니저가 각자 저장하면, `EquipmentManager.SaveAll()` 이
파일 전체를 써야 하는데 그 안에는 아직 살아 있지 않은 매니저의 몫도 들어 있다.
그대로 쓰면 그 몫이 기본값으로 덮인다 — 8단계 머리말이 경고하는
"같은 상태의 인스턴스가 2개 생겨 마지막에 저장한 쪽이 이긴다"가 바로 이것이다.

해법은 `SaveState` 하나를 메모리에 두고 매니저들이 자기 몫만 고치는 것인데, 그 소유자를
누가 만들고 언제 로드하느냐가 곧 부트스트랩 문제다. 8.4 에서 부트스트랩 3계보를 합칠 때
같이 해야 한다. **스키마와 파일 계층은 다 됐으므로 8.7 은 매핑만 하면 된다.**

---

### 8. DI 도입 + 부트스트랩

| # | 항목 | 상태 |
|---|---|---|
| 8.1 | VContainer 패키지 + `VCONTAINER_UNITASK_INTEGRATION` 심볼 (전 플랫폼) | [x] | 1.19.0 · 심볼 3플랫폼 |
| 8.2 | `RootLifetimeScope` (콘텐츠별 Installer 분할) | [x] | `GameContainer`. Installer 분할은 등록이 늘면 |
| 8.3a | A무리(씬에 없는 것) → 순수 클래스 | [x] | **10/10**. F9 로 런타임 검증 |
| 8.3b | B무리(BattleScene 배치 14개) | [x] | 호출부 **272 → 0**. 씬 편집 0건. 아래 참조 |
| 8.4 | `RuntimeInitializeOnLoadMethod` 부트스트랩 통합 | [x] | 9 → 3 (남은 셋은 개발 도구) |
| 8.5 | `Object.Instantiate` → `IObjectResolver.Instantiate` / 팩토리 | [x] | **항목으로 성립하지 않는다.** 38곳 중 15곳만 필요했고 8.3b 안에서 처리했다 — 나머지 23곳은 생성물이 `.Instance` 를 **0건** 쓴다 |
| 8.6 | `Awake` 초기화 → 주입 이후로 이동 | [x] | 스코프를 `sceneLoaded` 에서 만들어 **"`Awake` → `Start`" 한 줄**로 끝났다. 실제 이동 3건 |

#### 8.3b·8.5·8.6 — 트랜치 10개로 끝냈다 (2026-09-01)

**호출부 272 → 0. 씬 파일 편집 0건.**

기준선이 경고한 "씬 참조가 끊어져 되돌리기 어렵다"는 **성립하지 않았다.** 스코프
GameObject 를 씬에 배치하지 않고 `SceneManager.sceneLoaded` 에서 코드로 만들었기
때문이다. 그 선택이 두 가지를 동시에 해결했다.

| | |
|---|---|
| **되돌리기** | 씬 YAML 을 안 건드리므로 `BattleScope.cs` 하나를 지우는 것이 곧 롤백 |
| **8.6** | `sceneLoaded` 는 씬의 모든 `Awake` 뒤·모든 `Start` 앞이라, "`Awake` 초기화를 주입 이후로"가 **"`Awake` → `Start`" 한 줄**이 됐다 |

##### 왜 14개를 서로 주입하지 않고 홀더를 뒀나

의존 그래프를 실측하니 **9개가 하나의 강결합 덩어리(SCC)** 였다 —
`GameManager → UIDiceSummonSystem → DiceTypeStarManager → PlayerController → GameManager`.
`ContainerBuilder.Build` 가 `TypeAnalyzer.CheckCircularDependency` 를 `#if` 밖에서
**무조건** 부르고 필드·프로퍼티 주입까지 따라가므로, `[Inject]` 로 직결했다면
**씬 로드 때 컨테이너 빌드가 예외로 죽는다.**

##### 왜 홀더를 자식이 아니라 루트에 뒀나

VContainer 의 해석은 **자식 → 부모 단방향**이다. 자식(배틀) 스코프에 두면
루트에 사는 `RelicManager`·`UIService` 가 영원히 못 본다. 특히 `UIService` 가 찍는
다이얼로그의 호출부가 62곳으로 가장 큰 덩어리였다.

##### 주입 시점이 둘로 갈린다 — 가장 헷갈린 자리

| 태어난 경로 | 주입 시점 |
|---|---|
| 씬에 놓인 컴포넌트 | 스코프가 `sceneLoaded` 에서 훑는다 → **자기 `Awake` 뒤** |
| `resolver.Instantiate` 로 찍은 것 | VContainer 가 프리팹을 `SetActive(false)` 로 껐다 찍고 주입한 뒤 켠다(`ObjectResolverUnityExtensions.cs:78-91`) → **`Awake` 앞** |

같은 `[Inject]` 인데 반대다. 작업 중 이것을 반대로 알고 진행해 주석 3개가 틀렸고,
VContainer 소스를 읽어 바로잡았다. 결론("`Awake` 에서 쓰지 마라")은 어느 쪽이든
안전한 쪽이라 코드는 무사했다.

##### 8.5 는 항목으로 성립하지 않았다

`Object.Instantiate` 38곳 중 **15곳만 필요했다.** 나머지 23곳은 생성물이
`.Instance` 를 **0건** 쓴다 — 바꿔도 사라지는 참조가 없고, 프리팹 활성 토글이라는
부작용만 새로 생긴다. 그래서 8.5 를 독립 항목에서 지우고 필요한 15곳을 8.3b 안에서
처리했다. **"DI 원칙상 그래야 한다"는 이유로 하지 않는다.**

##### 남긴 예외 둘

| 대상 | 이유 |
|---|---|
| `DiceMetaDataProvider` | `static class`, 951줄, 사용처 148곳/37파일, 골든으로 잠긴 데미지 경로. 인스턴스화는 별건이다 |
| 개발 도구 3개 | `SelfCheck`·`GoldenBaselineDumper`·`DevSceneHotkeys` |

둘 다 `GameContainer.Battle` 정적 접근자로 읽는다.

##### 게이트

| 검사 | 결과 |
|---|---|
| `verify_singleton_count` | **0건** (기준선 0) |
| 헤드리스 | 3273/3273 |
| 사람 검증 | 4회 — T1 골격 / T4 1판 완주 / T7 소환·병합·조합·강화 / T9 전체 왕복 |
| 씬 편집 | **0건** |

---

> **실측 (2026-08-31, 12단계 종료 시점).** 이 셋은 9·10단계로 미뤘는데 **거기서 하지 않았다.**
> 9·10 단계의 게이트는 자기 하위 항목만 보므로 `[x]` 가 찍힌 것 자체는 규약 위반이 아니지만,
> "나중에 한다"가 실제로는 "안 했다"가 된 자리라 숫자를 남긴다.
>
> | 지표 | 8단계 종료 시점 | 지금 |
> |---|---|---|
> | B무리 MonoBehaviour 매니저 | 14 | **14** (BattleScene 배치 그대로) |
> | `.Instance` 호출부 | 228 | **228** (한 곳도 안 줄었다) |
> | `Object.Instantiate` | 35 | **38** (늘었다) |
> | `[Inject]` | 0 | **0** |
> | `IObjectResolver.Instantiate` | 0 | 1 |
>
> **위험이 방치된 것은 아니다.** 8단계가 없앤 것은 "같은 상태의 인스턴스가 2개 생기는" 경로였고,
> B무리 14개는 `MonoSingleton` 파생이 아니라 **순수 `static` 필드**라 읽어도 아무것도 만들지
> 않는다(파생은 `StaticResource` 하나뿐). 14개 전부 중복 가드와 `OnDestroy` 복구를 갖는다.
> 남은 것은 위험이 아니라 **결합**이다 — 그래서 미루는 판단 자체는 유효했다.
| 8.7 | 저장 서비스 실배선 (7.3 매퍼 포함) | [x] | 7.5 에서 구 키까지 제거해 닫혔다 |
| 8.8 | IL2CPP 스트리핑 대응 | [x] | `[Preserve]` 12곳 |

**게이트:** static Instance가 0(또는 문서화된 예외만), 씬 직접 재생 시 침묵 대신 명시적 실패, 골든 테스트 계속 통과.
→ **절반 통과.** 영구 서비스 계층은 끝났다. 씬 컴포넌트 쪽은 **의도적으로 남겼다** — 사유는 아래.

#### 8단계를 여기서 닫는 이유

| 지표 | 전 | 후 |
|---|---|---|
| `RuntimeInitializeOnLoadMethod` | 9 | **3** (컴포지션 루트 + 개발 도구 2) |
| `MonoSingleton` 파생 | 4 | **1** (`StaticResource` — 프리팹이라 정당) |
| 영구 서비스의 외부 생성 경로 | 다수 | **0** |
| 저장 경로 | 매니저 9개가 각자 PlayerPrefs | **파일 1개 원자 쓰기** |

**남은 것은 전부 UI 코드에 걸려 있다.** B무리 `.Instance` 호출부가 **228곳**인데 대부분이
UI 다. 그런데 **9단계(씬 흐름)와 10단계(UI 프레임워크 교체)가 바로 그 UI 를 다시 쓴다.**
지금 주입으로 바꾸면 10단계에서 같은 파일을 또 건드린다 — 같은 일을 두 번 한다.

`8.5`(`Instantiate` → resolver)도 같다. 지금 `[Inject]` 를 쓰는 프리팹이 하나도 없어서
바꿔도 얻는 것이 없고, 10단계에서 UI 생성 방식이 바뀌면 다시 봐야 한다.

**그래서 B무리 DI 는 9·10단계에서 해당 파일을 만질 때 함께 한다.** 게이트를 못 채운 채
넘어간다는 사실과 어디서 채울지를 여기 적어 둔다.

#### 문서화된 예외 (지금 살아 있는 static)

| 대상 | 왜 남았나 |
|---|---|
| B무리 14개의 `Instance` | 위 사유. 9·10단계에서 제거 |
| A무리 10개의 `Instance` **다리** | 호출부 453곳을 한 번에 못 바꾼다. <b>대입은 `GameContainer` 한 곳뿐</b>이라 인스턴스가 둘이 되는 사고는 성립하지 않는다 |
| `StaticResource` | 프리팹이라 컴포넌트여야 한다 |
| `GameContainer.Root` | 컴포지션 루트 자신 |

#### 8.7 검증 방식 — 되돌릴 수 있는 상태로 켰다

저장 배선은 틀리면 진행도가 사라지고 되돌릴 수 없다. 그래서 두 단계로 나눴다.

1. **쓰기만 켠다.** 게임은 계속 PlayerPrefs 로 돌았다. 매핑이 틀려도 <b>아무도 그 파일을
   읽지 않으니</b> 손상될 수 없는 상태.
2. **F10 대조로 증명하고 읽기를 켠다.** 매니저 실제 값과 파일에 담길 값을 이름을 대며
   하나씩 비교했다(14항목 전부 일치). 실제 파일도 열어 확인했다.

`WriteTo` 는 F10 이 증명하지만 `ReadFrom` 은 실제로 불러 봐야 안다. 그건 살아 있는 상태를
덮는 행위라 미리 시험할 수 없어서, **진짜 로드가 일어나는 순간에** 읽어 들인 것을 다시 모아
원본과 대조하게 했다. JSON 문자열끼리 비교한다 — `SortedDictionary` 라 직렬화가 결정적이라
**문자열이 같다는 것이 곧 모든 필드가 같다**는 뜻이고, 새 필드가 늘어도 자동으로 검사된다.

되돌리려면 `SaveService.ReadFromFileEnabled` 상수 하나만 `false` 로 바꾸면 된다.
PlayerPrefs 경로가 살아 있어서 즉시 예전 동작으로 돌아간다. **7.5(구 키 제거)는 그 다리를
끊는 작업이라 아직 하지 않는다.**

> 두 경로(싱글톤/DI) 공존 기간이 길면 **같은 상태의 인스턴스가 2개 생겨 마지막에 저장한 쪽이 이긴다.**
> 재화·장비·유물 진행도가 조용히 사라지는 형태로 나타난다. 공존 기간을 최대한 짧게.

#### 조사 결과 — 싱글톤 26개가 두 무리로 깔끔하게 갈린다

스크립트 GUID 로 모든 `.unity` / `.prefab` 을 전수 대조했다. 결과가 예상보다 좋다.

**A무리 — 씬·프리팹 어디에도 없다 (12개).** 전부 `RuntimeInitializeOnLoadMethod` 나
`[SingletonAutoCreate]` 로 런타임에 만들어진다. **MonoBehaviour 를 떼어도 깨질 참조가 없다.**

`PointManager` · `DiceLevelManager` · `EquipmentManager` · `RelicManager` ·
`StageProgressManager` · `StageRewardManager` · `StageStarManager` · `IdleRewardManager` ·
`RunHistoryManager` · `AOSBackBtnManager` · `UnityClock` · `PointCheatController`

**B무리 — BattleScene 에 배치돼 있다 (14개) + 프리팹 1개.** 컴포넌트로 남는다.

`GameManager` · `PlayerController` · `MonsterManager` · `MonsterSpawner` · `AttackContent` ·
`MergeSystem` · `UIBoard` · `UIDiceBoardUI` · `UIDiceSummonSystem` · `DiceTypeStarManager` ·
`ElementUpgradeManager` · `BulletPool` · `BulletEffectPool` · `DamageTextPool` /
`StaticResource`(프리팹)

#### 그래서 순서를 바꾼다

계획서의 8.3 은 "static Instance 21개 + MonoSingleton 청산"을 한 덩어리로 뒀는데,
두 무리는 **위험도가 완전히 다르다.**

A무리는 씬 참조가 없으니 순수 C# 클래스로 만들어도 **에셋이 깨질 수 없다.** 게다가
**저장되는 상태를 소유한 것이 전부 A무리다.** 즉 A무리만 정리하면 8.7(저장 배선)이
가능해지고, 그 결과물은 에디터 없이 테스트된다.

B무리는 씬에 박혀 있어 컴포넌트로 남아야 하고, `RegisterComponentInHierarchy` 로 씬
스코프에 등록하는 방식이 된다. 실수하면 **씬 참조가 끊어져 되돌리기 어렵다.**

| 순서 | 하는 일 | 왜 이 자리 |
|---|---|---|
| 8.2 | `RootLifetimeScope` + 인스톨러 | 그릇부터 |
| **8.3a** | **A무리 → 순수 클래스** | 씬 위험 0. 저장 소유자가 전부 여기 |
| **8.7** | 저장 배선 (7.3 + 7.5) | A무리만 있으면 된다 |
| **8.3b** | B무리 → static Instance 제거 | 씬 위험 있음. 저장 다 끝난 뒤에 |
| 8.4 | 부트스트랩 3계보 통합 | A·B 가 다 컨테이너에 들어온 뒤 |
| 8.5 / 8.6 / 8.8 | `Instantiate` · `Awake` · 스트리핑 | |

저장 배선(8.7)을 B무리 앞으로 당긴 것이 핵심이다. 위 경고문의 "마지막에 저장한 쪽이
이긴다"가 실제로 터질 수 있는 구간은 **저장 소유자가 두 경로에 걸쳐 있는 동안**뿐인데,
A무리만 손대고 곧바로 배선하면 그 구간이 가장 짧아진다.

#### A무리 정밀 조사 — 11개를 파일 단위로 다 읽었다

**전부 순수 C# 클래스로 만들 수 있다.** `must-stay-monobehaviour` 는 하나도 없었다.
진짜 걸림돌은 세 가지뿐이고 전부 같은 해법을 쓴다.

| 걸림돌 | 몇 개 | 해법 |
|---|---|---|
| `OnApplicationPause` / `OnApplicationQuit` | 9 | 릴레이 MonoBehaviour 하나가 이벤트로 중계 |
| `Awake` / `Init` | 10 | 생성자로 옮긴다 |
| `DontDestroyOnLoad` + `AddComponent` 자기생성 | 8 | 컨테이너 등록이 대신한다 |

**그런데 의존 그래프가 A무리 우선 전략을 반쯤 무너뜨린다.** A무리 중 5개가 B무리를
이름으로 붙잡고 있다.

| 매니저 | B무리 의존 | 8.3a 가능? |
|---|---|---|
| `PointManager` | **없음** | ✅ 의존 0. 가장 쉽다 |
| `DiceLevelManager` | 없음 (`PointManager` 만) | ✅ |
| `StageProgressManager` | `StaticResource`(간접) | ✅ `IStageDatabase` 로 감싼다 |
| `StageRewardManager` | `StaticResource`(간접) | ✅ |
| `StageStarManager` | `StaticResource`(간접) | ✅ |
| `IdleRewardManager` | 없음 | ✅ |
| `EquipmentManager` | **`GameManager`** | ⚠️ 되돌려야 한다 |
| `RunHistoryManager` | **`GameManager`** | ⚠️ |
| `AOSBackBtnManager` | **`GameManager`** (종료 요청 1곳) | ⚠️ |
| `RelicManager` | **`UIBoard` · `DiceTypeStarManager` · `GameManager` · `MonsterManager` · `UIDiceSummonSystem`** | ❌ |
| `PointCheatController` | 8개 (대부분 B무리) | ❌ |

**그래서 8.3a 는 위 6개로 줄인다.** 그 6개가 저장 소유자 9개 중 6개를 덮으므로
8.7 의 대부분이 가능하다. `RelicManager` 는 B무리 의존이 전부 *게임플레이 효과*
(보드 조회·소환)이지 저장 상태가 아니므로, 그 부분만 인터페이스로 떼면 나중에 합류한다.

#### 규모

`.Instance` 호출부가 **453곳 / 파일 수십 개**다. 8.3 은 큰 작업이다.

| 매니저 | 호출부 |
|---|---|
| `EquipmentManager` 112 · `RelicManager` 95 · `PointManager` 70 · `DiceLevelManager` 70 |
| `StageProgressManager` 47 · `StageStarManager` 20 · `StageRewardManager` 17 · `IdleRewardManager` 12 |

#### 이미 코드에 남아 있던 "순서를 못 믿는다"는 자백

- `TitleSceneController.cs:17-18` — `_ = DiceLevelManager.Instance; _ = EquipmentManager.Instance;`
  값을 버리는 접근이다. **오직 생성 순서를 강제하려고** 존재한다. DI 로 옮기면 삭제 대상.
- `StageStarManager` · `StageRewardManager` — `Awake` 와 `Start` 에서 구독을 **두 번** 시도하고
  `isStageProgressSubscribed` 플래그로 중복을 막는다. Awake 순서를 신뢰하지 않는다는 뜻이다.
- `MonoSingleton<T>.Instance` 는 **조회가 아니라 생성 트리거**다(`Singleton.cs:63-82`).
  `Awake` 안에서 다른 매니저를 읽으면 그 매니저의 `Awake` 가 **내 `Awake` 한가운데서 동기 실행**된다.
  DI 로 가면 이 재진입이 사라지므로 부수효과 순서가 바뀐다. 이관 시 가장 주의할 지점.

#### 손대지 않기로 한 것 — "가드 없는 역참조" 4곳

조사에서 `Instance` 를 null 체크 없이 바로 쓰는 곳 4군데가 나왔다
(`UIDiceGrowthDetailPanel.cs:106,109` · `LobbyLayoutController.cs:155` · `IDialog.cs:110`).
**전부 실제로는 null 이 될 수 없다** — `AOSBackBtnManager` 는 `MonoSingleton` 이라 게터가
인스턴스를 만들어 내고, 나머지는 `BeforeSceneLoad` 부트스트랩이 UI 보다 먼저 돈다.

가드를 넣지 않는다. 넣으면 배선 사고가 **침묵으로 흡수되는** 경로를 새로 만드는 것이고,
그건 2단계에서 봉인한 바로 그것이다. 8.3 에서 주입으로 바뀌면 이 네 곳은 저절로 사라진다.

---

### 9. 씬 흐름 재구성

> `UICanvasLayout`과 `FadeView`는 이 단계보다 **앞서** 필요하다(`SceneRouter`가 의존).
> 10단계에서 다시 만들지 않도록 여기서 확정한다.

| # | 항목 | 상태 |
|---|---|---|
| 9.1 | `FadeView` (10단계에서 당겨옴) | [x] | `UICanvasLayout` 은 10단계로 |
| 9.2 | 캔버스/해상도 규격 통일 | [x] | 1080x1920. 어긋난 것은 `DamageCanvas` 하나뿐이었다 |
| 9.3 | `SceneId` enum + 빌드 인덱스 정합 | [x] | `SceneCatalog` + F9 검사 |
| 9.4 | `SceneRouter` + 전환 게이트 (연타 차단) | [x] | |
| 9.5 | PlayerPrefs 왕복으로 스테이지 넘기던 방식 → 전이 홀더 | [x] | **이미 해결돼 있었다** — 아래 참조 |

**게이트:** Boot→Lobby→Battle→Lobby 왕복이 페이드와 함께 동작하고, 전환 버튼 연타가 차단된다.
→ **통과.** (Boot 씬은 아직 없다. 컴포지션 루트가 `BeforeSceneLoad` 로 도는 것이 그 역할을
   대신하고 있고, Boot 씬 신설은 10단계에서 UI 뼈대와 함께 한다.)

#### 조사에서 계획서와 달랐던 것

**9.2 — 배틀 메인 `Canvas` 는 이미 1080x1920 이었다.** 계획서는 "배틀 Canvas 2개(하나는
ScreenSpaceCamera 800x600)"라고 적었는데, 실제로 어긋난 것은 `DamageCanvas` 하나이고
문제는 해상도가 아니라 **`ConstantPixelSize`** 였다. 그러면 데미지 숫자만 화면 크기를
따라가지 않는다 — 기준 해상도에서는 똑같아 보여서 **게임뷰만 봐서는 절대 드러나지 않는다.**

렌더 모드(`ScreenSpaceCamera`)는 건드리지 않았다. 배틀에서는 UI 와 스프라이트의 그리는
순서가 카메라에 걸려 있어서, 그걸 바꾸는 것은 규격 통일이 아니라 렌더링 변경이다.

**9.5 — 이미 해결돼 있었다.** 스테이지 번호는 `StageProgressManager` 를 타고 넘어가는데
그건 원래 영구 상태(마지막에 고른 스테이지를 기억한다)이지 전이용 임시 채널이 아니다.
전수 조사에서 전이 목적으로만 쓰는 별도 `PlayerPrefs` 키는 없었다.

#### 씬 저장이 부수적으로 정리한 것

`DamageCanvas` 를 고치면서 `BattleScene` 이 다시 저장됐고, 그때 **6.1 에서 프로퍼티로
바꾼 죽은 직렬화 값들이 같이 빠졌다** — `WallHp` · `WaveMonsterCount` ·
`WaveMonsterDeadCount`(`GameManager`), `TotalHp` · `CurrentHp`(`Wall`).
인스펙터에서 고쳐도 아무 일도 일어나지 않던 값들이다.

---

### 10. UI 프레임워크 교체

> **10단계 착수 시점에 "생성기 대상 / 수작업 대상"을 먼저 분류한다.** 12단계 생성기가
> 여기서 손으로 만든 프리팹을 덮어쓰면 작업이 날아간다.

| # | 항목 | 상태 |
|---|---|---|
| 10.1 | `IUIService` / `UIService` | [x] | `Show<T>()` / `Get<T>(parent)` |
| 10.2 | `DialogBase` (`IDialog` 리네임) | [x] | GUID 유지 확인 |
| 10.3 | `DialogCatalog` — 명시적 키 + 등재 검증 | [x] | 17/17 등재, 문제 0 |
| 10.4 | 다이얼로그 17개 이관 + 씬 인스턴스 제거 | [x] | 씬 인스턴스 0 확인 |
| 10.5 | `UIIdleRewardDialog` 재작성 | [x] | 프리팹으로 구웠다 |
| 10.6 | 백키를 `AOSBackBtnManager` 스택에 통합 | [x] | 페이지는 홈으로, 팝업은 닫기 |
| 10.7 | 한글 폰트 SDF 문자셋 확인 | [x] | **깨질 조건이 없다** — 아래 참조 |

**게이트:**
- 다이얼로그 17개가 전부 카탈로그 경유로 열리고 닫힌다
- 씬에 상주하는 다이얼로그 인스턴스가 0
- **오프너·컨트롤러의 씬 참조가 전부 카탈로그 경유로 대체됐고 `FindFirstObjectByType` 폴백이 0건**
- 백키가 단일 스택으로 동작

> "씬 인스턴스 0"만으로는 부족하다. 참조하던 `[SerializeField]`가 **Missing이 아니라 None**이 되어
> 콘솔 무음으로 죽는 경로를 못 잡는다.

#### 10.7 — 한글이 □ 로 뜰 조건이 없다

계획서의 걱정은 **Static 모드 폰트**(문자셋을 미리 구워 고정)에 해당한다. 이 프로젝트는
그 구성이 아니다.

| 설정 | 값 | 뜻 |
|---|---|---|
| `AtlasPopulationMode` | 1 (Dynamic) | 없는 글자는 런타임에 구워 넣는다 |
| `SourceFontFile` | `Assets/NotoSansKR-Black.ttf` | **빌드에 포함된다** — 굽는 데 필요한 원본 |
| `IsMultiAtlasTexturesEnabled` | 1 | 아틀라스가 차면 텍스처를 늘린다 |
| `ClearDynamicDataOnBuild` | 0 | 이미 구운 1023자가 빌드에 남는다 |

**진짜 위험은 문자셋이 아니라 폰트 지정이다.** TMP 기본 폰트 `LiberationSans` 는 라틴
250자짜리 Static 이라, 그걸 쓰는 텍스트가 하나라도 있으면 그 자리는 무조건 □ 가 된다.
전수 조사했고 **모든 `TMP_Text` 가 Noto 를 쓴다** — `LiberationSans` 도, 폰트가 빈 것도 없다.

남은 구멍은 하나뿐이고 그것이 10.5 의 이유다 — `UIIdleRewardDialog.FindFont()` 는
씬에서 <b>아무 `TMP_Text` 나 주워</b> 그 폰트를 쓴다. 못 찾으면 null 을 돌려주고,
그러면 TMP 가 기본 폰트로 떨어져 한글이 깨진다. 이 함수가 존재하는 이유는 그 창이
프리팹이 아니라 <b>코드로 UI 를 짓기</b> 때문이다.

#### 10.5 — 손으로 다시 만들지 않고 구웠다

이 창만 UI 를 코드로 지어서 나머지 16개와 구조가 달랐다. 위치·색·크기를 손으로 옮겨
적으면 오차가 생기는데, <b>정확한 값을 아는 코드가 이미 있다.</b> 그것을 에디터에서
한 번 돌려 결과를 프리팹으로 저장했다.

**그냥 굽는 것만으로는 안 됐다.** <c>onClick.AddListener</c> 로 붙인 델리게이트는
프리팹에 직렬화되지 않는다 — 구울 때 붙여 봐야 저장되지 않으므로 버튼이 전부 죽는다.
그래서 역할을 셋으로 나눴다.

| | |
|---|---|
| `Create(parent, font)` | 계층을 짓는다. **에디터 굽기 전용** |
| `OnLoad()` | 버튼 배선. 런타임에 다시 붙인다 |
| 필드 `[SerializeField]` | 굽는 시점의 참조가 프리팹에 저장된다 |

폰트를 인자로 받게 바꾼 것이 10.7 의 마지막 구멍을 막았다. 굽기 도구는 폰트를
**이름이 아니라 `HasCharacter('가')`** 로 고른다 — 이름으로 찾으면 폰트를 갈아 끼웠을 때
조용히 라틴 전용이 선택되고, 그러면 프리팹의 모든 한글이 □ 로 저장된다.
못 찾으면 굽지 않고 멈춘다.

#### (해결됨) 10.5 가 남았던 이유

`UIIdleRewardDialog` 만 다른 16개와 구조가 다르다. 프리팹이 없고 `CreateRect` 로
계층을 코드에서 만든다. 그래서
<list>
<item>폰트를 씬에서 주워 와야 하고(위 구멍),</item>
<item>레이아웃을 인스펙터에서 볼 수 없고,</item>
<item>카탈로그에 등재할 프리팹 자체가 없다.</item>
</list>
재작성은 "프리팹으로 만든다"가 본체다. 나머지 UI 작업이 끝난 뒤에 하는 것이 맞다 —
지금 만들면 프리팹 규격이 확정되기 전에 만드는 셈이다.

#### 조사 — 17개는 한 종류가 아니다

계층 위치를 전수로 뽑아 보니 **팝업과 페이지가 섞여 있다.** 프리팹 어느 것도 자체
`Canvas` 를 갖지 않으므로 **계층 위치가 곧 레이어 순서**이고, 따라서 이 구분은 표시상의
차이가 아니라 <b>다르게 다뤄야 한다는 뜻</b>이다.

**팝업 (13개)** — 캔버스 최상위에 붙어 떠 있다. 열고 닫는 대상.

| 씬 | 부모 | 다이얼로그 |
|---|---|---|
| Battle | `Canvas` | `UIDiceCraftPanelDialog` · `UIDiceCraftProgressDialog` · `UIBattleDiceDetailPanel` · `UIElementUpgradePanel` · `UIWaveRewardPreviewDialog` · `UIStageResultDialog` |
| Lobby | `Canvas/LobbyLayoutController` | `UIRelicSummonDialog` · `UIDiceGrowthDetailPanel` · `UIStageStarRewardDialog` · `UIMergePopup` · `UIStageStarDialog` · `UIStageRewardDialog` · `UIRewardResultDialog` |

**페이지 (3개)** — `Canvas/LobbyLayoutController/Content` 안에 있다.
`UIRelicDialog` · `UIDiceGrowthPage` · `UIEquipmentPage`

이들은 로비 탭 내용물이다(`LobbyLayoutController.ShowTab` 이 전환한다). 이름에 `Dialog` 가
붙은 것도 있지만 <b>팝업이 아니다.</b> 팝업 루트로 옮기면 로비 레이아웃이 깨진다.

**`UIEquipmentConfirmDialog` 는 씬에 없다** — `UIEquipmentPage` 프리팹 안에 들어 있다.
즉 이미 "부모가 소유하는 부품"이고, 카탈로그 경유로 바꿀 대상이 아니다.

#### 그래서 10.4 를 이렇게 나눈다

| 대상 | 처리 |
|---|---|
| 팝업 13개 | 카탈로그 경유로 열고 씬 인스턴스 제거 |
| 페이지 3개 | **그대로 둔다.** 탭 내용물이지 팝업이 아니다 |
| `UIEquipmentConfirmDialog` | 이미 프리팹 안에 있다. 손대지 않는다 |

게이트의 "씬 인스턴스 0" 은 **팝업 기준**으로 읽는다. 페이지를 억지로 들어내면
로비 레이아웃을 다시 짜야 하고, 그것은 UI 교체가 아니라 화면 재설계다.

---

### 11. 폴더 규약 + asmdef 확정

| # | 항목 | 상태 |
|---|---|---|
| 11.1 | `OJ.Core` 분리 (`noEngineReferences: true`) | [x] | 커밋 `6f40e5f`. `Mathf` → `OJMath` |
| 11.2 | `OJ.Game` 분리 | [x] | 커밋 `6e90402`. `OJ.Game` / `OJ.Game.Editor` |
| 11.3 | 폴더 재배치 (`AssetDatabase.MoveAsset` 경유) | [x] | 1파일 잡동사니 폴더(`Interface`/`Define`) 해체, `Bullet`→`Dice` 흡수 |
| 11.4 | 네임스페이스 재부여 (`OJ.*` 계층) | [x] | 172파일. 평평한 `namespace OJ` → 폴더 일치 21계층 |
| 11.5 | `Prefab` / `Prefabs` 폴더 통합 | [x] | `Prefabs/`에 1개뿐이었다 |
| 11.6 | Resources 경로 문자열 23곳 정리 | [x] | 아트를 옮기면 컴파일 통과 상태로 런타임 null이 된다 |
| 11.7 | UniTask 벤더링 → UPM 전환 검토 | [x] | **검토 결과: 지금은 전환하지 않는다.** 아래 참조 |

**게이트:** asmdef가 순환 없이 컴파일되고, 프리팹·씬의 Missing script가 **1.5 기준선 대비 증가 0건**.

**게이트 결과 (2026-08-31).**

| 검사 | 결과 |
|---|---|
| 헤드리스 컴파일 + 테스트 | OJ.Core / OJ.Core.Tests / OJ.Game / OJ.Game.Editor 전부 통과, **3273/3273** |
| `Tools/verify_missing_scripts.py` | **0건** — 1.5 기준선 0건 대비 증가 0 |
| `Tools/verify_encoding.py` | 통과 (200개 파일) |

파일 이동은 전부 `.meta`를 함께 옮겼다. GUID를 이동 전후로 대조해 보존을 확인했다.

#### 11.7 검토 결과 — UniTask는 벤더링 상태로 둔다

**사실관계.**

| 항목 | 값 |
|---|---|
| 버전 | 2.5.10 (`Assets/Plugins/UniTask`) |
| 로컬 수정 | 없음 — 커밋 `2b495dd` "UniTask 추가" 1건이 전부 |
| 파일 수 | 156개 `.cs` |
| 실제 사용처 | **`UIDice.cs` 한 파일, 5줄** (`UniTask.NextFrame` 2, `UniTask.Delay` 1, 시그니처 2) |
| VContainer 연동 | `VCONTAINER_UNITASK_INTEGRATION` 심볼은 켜져 있지만 `IAsyncStartable` 구현체는 **0개** |
| openupm 레지스트리 | 이미 설정돼 있다 (VContainer가 쓰는 중) |

**전환하면 얻는 것:** Assets 트리에서 156개 파일이 빠진다. 컴파일 시간은 거의
그대로다 — 별도 asmdef라 어차피 한 번 컴파일되고 캐시된다.

**전환하면 생기는 것:** 지금 이 프로젝트는 **네트워크 없이 빌드된다.** UPM으로
옮기는 순간 레지스트리 해석이 빌드 전제 조건이 된다. 그 실패는 리팩토링과
아무 상관이 없는데도 리팩토링 중에 터진다.

**판단.** 벤더링본은 손대지 않은 원본이고 잘 돌아간다. 156개 파일이 Assets에
있는 것은 미관 문제지 위험이 아니다. 반면 전환은 저장소 바깥에 실패 지점을
만든다. **리팩토링이 끝나고 main에 병합한 뒤에 별도로 할 일이다.**

**그때 할 일 (순서를 지킬 것).** 두 가지를 **한 커밋에서** 해야 한다. 벤더링본과
패키지가 동시에 존재하면 asmdef 이름 `UniTask`가 중복되어 Unity가 어셈블리 충돌로
죽고, 타입도 CS0433으로 갈린다.

1. `Packages/manifest.json` — `scopedRegistries[0].scopes`에 `com.cysharp.unitask` 추가,
   `dependencies`에 `"com.cysharp.unitask": "2.5.10"` 추가
2. `Assets/Plugins/UniTask` 폴더와 `.meta` 삭제

`Tools/headless/headless.config.json`은 손댈 것이 없다 — `scriptAssemblies` 참조라
UniTask가 패키지로 와도 `Library/ScriptAssemblies/UniTask.dll`로 똑같이 나온다.

**더 나아간 선택지 (권하지 않음).** Unity 6의 `Awaitable`은 `UniTask.NextFrame` /
`UniTask.Delay`를 그대로 대체하므로 의존성을 **없앨** 수도 있다. 하지만 그 5줄이
있는 곳이 **다이스 클릭 판정(더블클릭 구분)** 이고, `Awaitable`은 파괴 시 자동
취소라 수명 의미가 다르다. 이 게임에서 가장 많이 눌리는 경로를 정리 작업 삼아
건드릴 이유가 없다.

---

### 12. 에디터 자동화

| # | 항목 | 상태 |
|---|---|---|
| 12.1 | 프리팹 생성기 (10단계에서 분류한 대상만) | [x] | 멱등성 확인. 아래 참조 |
| 12.2 | 정적 검증 CI 편입 | [x] | `Toolserify-all.cmd` + `.github/workflows/verify.yml`. 아래 참조 |

**게이트:** 생성기를 연속 2회 실행해도 결과가 동일(멱등)하고, 카탈로그에 중복 엔트리가 생기지 않는다.

#### 12.1 — `git diff` 로는 멱등성을 판정할 수 없다

굽기 도구를 두 번 돌린 뒤 `git diff` 를 보면 **3,564줄이 통째로 갈린다.**
그대로 읽으면 "멱등이 아니다"라고 결론 내리게 되는데, **틀렸다.**

`PrefabUtility.SaveAsPrefabAsset` 은 저장할 때마다 **로컬 fileID 를 새로 발급한다.**
아무것도 안 바뀐 프리팹을 다시 구워도 모든 객체의 번호가 바뀌므로 파일 전체가
차분에 뜬다. 그래서 물어야 할 것은 "파일이 같은가"가 아니라 **"값이 같은가"**다.

그 판정을 위해 `Tools/diff_prefab.py` 를 만들었다. 문서를 쪼개 파일 내부 fileID 를
눌러 정규화한 뒤 다중집합을 비교한다. `guid` 가 붙은 fileID(외부 에셋 참조)는
누르지 않는다 — 그건 어느 에셋을 가리키는지의 정보라 지우면 검사가 무뎌진다.

**결과 (2026-08-31).**

| 항목 | 값 |
|---|---|
| 문서 수 | 478 = 478 |
| 오브젝트 이름 집합 | 동일 |
| 타입별 개수 | 동일 |
| **값이 다른 문서** | **1개** |

그 하나가 이것이다:

```
- m_EditorClassIdentifier: Assembly-CSharp::OJ.UIIdleRewardDialog
+ m_EditorClassIdentifier: OJ.Game::OJ.IdleReward.UIIdleRewardDialog
```

11.2(어셈블리 분리)와 11.4(네임스페이스 계층화)가 프리팹에 반영된 것이다.
**나머지 477개 문서는 값이 완전히 같다** — 생성기는 값 수준에서 멱등이다.

> `m_EditorClassIdentifier` 는 권위 있는 참조가 아니다. 실제 연결은
> `m_Script: {fileID: 11500000, guid: ...}` 이고 그 GUID 는 이번 이동에서
> 전부 보존됐다(`Tools/verify_missing_scripts.py` 0건). 이 필드는 GUID 해석이
> 실패했을 때만 쓰이는 **복구용 힌트**다. 그래서 다른 에셋에 남은 낡은 값
> (`DialogCatalog.asset` 1건)도 위험이 아니라 재직렬화되면 갱신될 캐시다.

**생성기 두 개 모두 파괴적이지 않다.**

| 도구 | 멱등 근거 |
|---|---|
| `IdleRewardPrefabBaker` | 같은 경로에 덮어써 GUID 유지. 폰트 선택을 경로 정렬로 결정적으로 만들었다 |
| `DialogCatalogBuilder` | 이미 있는 항목은 건드리지 않고 빠진 것만 더한다. 없어진 프리팹은 **지우지 않고 보고**한다 — 중복 엔트리가 생기는 경로가 없다 |

#### 12.2 — 입구를 하나로 만들고, CI 는 도는 것만 돈다

검사가 넷인데 따로 기억해서 돌려야 하면 **아무도 안 돌린다.** `Toolserify-all.cmd`
하나로 모았다. `--quick` 은 테스트를 빼고 몇 초만에 끝난다.

| 검사 | CI | 이유 |
|---|---|---|
| `verify_encoding.py` | O | 순수 파이썬 |
| `verify_namespaces.py` | O | 순수 파이썬. **11.4 규약을 검사로 못박은 것** |
| `verify_missing_scripts.py` | X | `Library/PackageCache` 가 있어야 한다(gitignore 대상). 없으면 TMP·uGUI 참조가 전부 Missing 으로 오탐된다 |
| headless 테스트 3273개 | X | 설치된 Unity 의 Mono·DLL 을 쓴다 |

**CI 가 절반만 돈다는 사실을 워크플로 파일 안에 표로 적어 뒀다.** "CI 가 초록불이니
다 봤겠지"라고 오해하는 것이 검사가 없는 것보다 나쁘다.

**새로 만든 `verify_namespaces.py` 가 잡는 것 두 가지.**

1. 폴더 ↔ `namespace` 불일치
2. **폴더명이 타입명과 겹쳐 그 타입을 가리는 경우** — `Scripts/Bullet` 을 만들면
   `namespace OJ.Bullet` 이 되고, 그러면 다른 `OJ.*` 안에서 `Bullet` 이 클래스가 아니라
   네임스페이스로 먼저 잡힌다(CS0118). 컴파일러도 잡지만 **그 타입을 실제로 쓰는 파일이
   생겨야** 터진다 — 폴더를 만든 날이 아니라 몇 주 뒤 엉뚱한 곳에서, 되돌리기가 훨씬
   비싸진 뒤에.

둘 다 일부러 위반을 심어 실제로 잡히는 것을 확인했다. **절대 실패하지 않는 검사는
없느니만 못하다.**

> 그 과정에서 `verify_missing_scripts.py` 의 인자 파싱 버그를 하나 고쳤다.
> `--baseline 0` 의 `0` 이 위치 인자(리포 루트)로 새어 들어가 엉뚱한 폴더를 훑었고,
> "PackageCache 가 없다"는 **잘못된 진단**이 나왔다. 옵션 값을 걸러 내지 않은 탓이다.

---

## 알려진 미동작 (리팩토링 중 의도적)

> 나중에 "원래 죽어 있던 건가, 방금 내가 죽인 건가"를 구분하기 위한 목록.
> 씬에서 무언가를 뺄 때마다 한 줄 추가한다.

| 증상 | 원인 | 복구 시점 |
|---|---|---|
| _(현재 없음)_ | | |

> 0.5단계 추출은 전부 씬 인스턴스 연결을 유지한 채 끝났으므로 이 시점에 의도적 미동작은 없다.
> 10.4에서 씬 인스턴스를 실제로 제거할 때부터 여기에 쌓인다.
>
> **참고 — 이 목록이 필요한 이유.** `LobbyLayoutController`의 `[SerializeField] IDialog equipmentPanel`처럼
> 씬 참조로 물린 필드는, 대상이 씬에서 사라지면 Missing이 아니라 **None**이 된다. 폴백인
> `GetComponentInChildren<UIEquipmentPage>(true)`도 같이 null이 되고, 호출부는
> `if (equipmentPanel != null)` 로 조용히 건너뛴다 — 콘솔에 아무것도 안 남는다.
> 10.4에서 이런 필드를 하나씩 끊게 되므로, 끊을 때마다 여기에 한 줄씩 적는다.

---

## ★ 보석 효과 52개가 죽어 있다 (5.2 조사, 2026-08-29)

**enum 리맵 사고다. 지금 게임에서 보석 효과 절반이 아무 일도 하지 않는다.**

`ef30864`("특수 다이스 추가") 이전에는 `DiceType.Max = 11`이었고, `GemDefinitionDatabase.asset`의
`targetDiceType: 11`은 **"모든 다이스에 적용"**을 뜻했다. 그 커밋이 합성 다이스를 넣으며
`Max`를 11 → **205**로 밀었는데, **에셋을 11 → 100으로 리맵했다.** 100은 `Max`가 아니라 `Tornado`다.

```
EquipmentManager.IsTargetMatched
    baseType = DiceMetaDataProvider.GetBaseElementType(diceType)   // 항상 {0,1,2,3,4}
    if (effect.targetDiceType != Max && effect.targetDiceType != baseType) return false;
                                 ↑ 205            ↑ 100 은 절대 여기 못 온다
```

`GetBaseElementType`은 합성·킹을 전부 기본 5종으로 접으므로 **100을 반환할 수 없다.**
따라서 `targetDiceType: 100`인 효과 71개는 데미지 경로에서 **영원히 매칭되지 않는다.**

| GemStatType | 전체 | target=100 | 데미지 경로에서 죽음 |
|---|---|---|---|
| `AttackPercent` | 19 | 13 | **13** |
| `AttackFlat` | 8 | 8 | **8** |
| `CooldownReducePercent` | 13 | 12 | **12** |
| `FinalDamagePercent` | 19 | 13 | **13** |
| `FirstNWavesDamageFlat` | 6 | 6 | **6** |
| `WellHpOnKill` | 10 | 10 | 0 — `DiceType.Max`로 조회해 조기 반환에 걸린다 |
| `GoldOnKill` | 9 | 9 | 0 — 같은 이유 |
| `FireExplosion*` / `ThunderChain*` | 16 | 0 | 0 |
| **합계** | **100** | **71** | **52** |

살아남은 19개는 `GetWellHpOnKill`/`GetGoldOnKill`이 `EnumerateActiveEffects(DiceType.Max)`로
묻기 때문이다 — `IsTargetMatched` 첫 줄의 `if (diceType == Max) return true`에 걸린다.

**조치: 고쳤다.** `Tools > OJ > Equipment > Repair Gem Target Dice Type` 으로
에셋의 `targetDiceType: 100`(Tornado)을 `205`(Max)로 되돌렸다.

> **밸런스 영향은 측정하지 않았다.** 서비스 중이 아니고 밸런스가 잡힌 상태도 아니라서,
> "버그였고 고쳤다"로 충분하다. 밸런스는 리팩토링이 끝난 뒤 별도로 잡는다.
> 지금 그걸 따지면 리팩토링이 밸런스 작업에 끌려간다.

UI에도 새어 나간다(코드 판독, 실행 미확인) — `UIEquipmentEffectTextFormatter.BuildTargetText`가
`targetDiceType != Max`면 접두사를 붙이므로 **보석 설명이 `[Tornado] 최종 피해 +…`로 표시된다.**

---

## ★ 검증 도구가 오탐한 기록 (11.6 조사, 2026-08-31)

**`ResourcePathReport` 가 `Art/Gem/*Scroll` 7건을 "없음"으로 올렸는데 게임은 멀쩡했다.**
남기는 이유는 오탐 자체가 아니라 **오탐을 만든 사고방식**이다.

도구는 이런 규칙을 가정했다 — *"이름이 `Scroll` 로 끝나는 `PointType` 은
`Art/Gem/{이름}` 에 있다."* 그 규칙은 **원소 스크롤 5종에만 성립한다.**
장비 스크롤 6종(`WeaponScroll`~`NecklaceScroll`)과 `MythicScroll` 은 그 폴더에 없다.

그런데 게임은 왜 멀쩡한가. `UIEquipmentConfirmDialog.GetScrollCostIconSprite` 가
3단 폴백이고 **1단계가 `PointMetadataDatabase` 를 먼저 보기 때문이다.**

```
1) PointRewardUtility.GetPointIcon(scrollType)   <- 정본. 18종 전부 아이콘이 있다
2) Resources.Load<Sprite>($"Art/Gem/{scrollType}")  <- 도구가 검사한 자리. 도달해도 무효
3) UIEquipmentSpriteResolver.GetEquipmentSmallIconSprite(equipmentType)
```

게다가 이 함수의 유일한 호출부는 인자로 항상 `PointManager.ToEquipmentScrollType()`
결과를 넘기는데, 그 함수는 **`WeaponScroll`~`NecklaceScroll` 6종만 반환하고 나머지는
예외를 던진다.** `MythicScroll` 은 이 경로에 물리적으로 도달할 수 없다.

**교훈: 경로를 짐작해 만들지 말고 정본을 검사하라.** 도구를 고쳐
`Art/Gem/{PointType}` 조합 대신 **`PointMetadataDatabase` 의 항목마다 아이콘이
채워져 있는지**를 본다. 그것이 런타임이 실제로 의존하는 것이다.

> 조합해서 확인하는 방식 자체가 틀린 것은 아니다 — 장비 아이콘·등급 슬롯·유물처럼
> 코드가 진짜로 경로를 조합하는 자리에는 그대로 둔다. 틀린 것은 **조합 규칙을
> 코드에서 읽지 않고 이름 모양에서 추측한 것**이다.

### 같이 확인된 것

| 관찰 | 판정 |
|---|---|
| `StaticResource` 가 매번 2개 생겼다 하나가 파괴된다 | **정상이다.** `Bootstrap` 이 `BeforeSceneLoad` 라 그 시점엔 씬이 없어 `FindObjectOfType` 이 반드시 null 이다 → Resources 프리팹을 복제하고 그 클론이 `DontDestroyOnLoad` 로 살아남는다. 뒤이어 `TitleScene` 의 인스턴스가 Awake 하며 자기를 파괴한다. 8단계 회귀가 아니고(이전 실행 로그에도 같은 줄이 있다) 두 사본의 값도 동일하다 |
| `TitleScene` 의 `StaticResource` 인스턴스 | 런타임에는 늘 지는 쪽이지만 **씬을 직접 재생할 때를 위한 것이라 지우면 안 된다** |
| 컴파일 경고 10건 (CS0618 8 / CS0162 2) | 전부 리팩토링 이전부터 있었다. 줄 번호만 밀렸다 |
| F9 가 결과와 무관하게 `LogWarning` 이었다 | **고쳤다.** 사고가 있으면 `LogError`, 없으면 `Log` 로 갈라 색이 판정을 알려준다 |

---

## 미해결 / 확인 필요

| 항목 | 비고 |
|---|---|
| ~~`KingThunder` 레벨 오독~~ | **기록이 틀렸다. 전투는 정상이었다.** 진짜 결함은 `UIBattleDiceDetailPanel` 의 연쇄 수 표시였다 — 공식을 전투와 따로 적어 두었고 그 복사본이 셋을 놓쳤다(킹의 기본항을 Thunder 가 아닌 KingThunder 레벨로, 유물 보정 누락, 하한 없음). 베끼지 않고 `AttackContent.GetThunderTargetCount` 를 부르게 고쳤다 (2026-09-02) |
| ~~백키 한 번 먹힘 (B3)~~ | **고쳤다.** `DialogBase.Unload` 가 `_isEnter` 를 끄지 않아 파괴된 창이 스택에 살아 있었다. 폴백이 전부 주석이라 증상은 없었지만, 그 주석을 되살리는 순간 씬 전환 직후 첫 백키가 죽는다. 곁가지로 유물 상세 팝업이 백키로 안 닫히고 탭 전체가 홈으로 가던 것도 고쳤다 (2026-09-02) |
| ~~시작 주사위 이중 적용~~ | **정리했다.** 실제로 2개가 놓인 적은 없다(가드가 막았다). 다만 호출부가 둘이라 `UIBoard.Start` 가 먼저 도는 판에서는 지난 판 가드가 살아 있어 **조용히 건너뛸** 수 있었다. `UIBoard` 쪽을 걷어내고 두 `Start` 뒤에 도는 `GameManager` 코루틴 하나로 모았다 (2026-09-02) |
| ~~`DiceTypeStarManager.ResetAll()`~~ | **고쳤다.** 세 루프 전부 순회 중 수정이었다. 앞의 둘은 enum 순회로, `(타입,성급)` 키는 키를 복사해 돈다 — `Clear()` 는 "없는 키"와 "0 인 키"의 구별을 없애서 안 쓴다. 호출부는 아직 0곳 (2026-09-02) |
| ~~`lockedRoot` 미할당~~ | **버그가 아니었다.** 가리킬 자물쇠 오브젝트가 프리팹에 애초에 없다 — 배선 실수가 아니라 미구현이다. 잠금은 이미 버튼 회색+"잠김"·빈 별로 표시된다. 별의 시련 카드 목록에서 보인다 |
| **`UIDiceBoardUI` 서브시스템이 꺼져 있다** | BattleScene 에서 `m_IsActive: 0` 이고 켜는 코드가 없다. `Awake` 가 안 돌아 옛 `Instance` 는 **영구 null** 이었고 `Instance?.UpdateTypeStars()` 는 한 번도 실행되지 않았다. 8.3b 이후 `RegisterComponentInHierarchy` 가 비활성도 찾으므로 창구는 채워지고 호출도 **실제로 일어난다** — 다만 `Start` 가 안 돌아 `typeUIDict` 가 비어 무동작이다. **켜는 순간 `UpdateTypeStars` 가 진짜 일을 시작한다.** 쓸 계획이 없으면 `UIDiceBoardUI`·`TypeUIComponent` 를 정리 대상으로 |
| ~~보석 효과 52개 사망~~ | **고쳤다.** 위 전용 절은 원인 기록용으로 남긴다 |
| ~~`UIDice.prefab`의 옛 `OnClick()`~~ | **지웠다. 그리고 "실제로 실행된다"는 이 기록이 틀렸다** — `UIDice` 에 `OnClick` 이라는 멤버가 **아예 없다**(리포 354 리비전 전수 확인). 바인딩할 대상이 없어 리스너 0개였고, 주사위 클릭은 `Button` 이 아니라 `IPointerClickHandler.OnPointerClick` 으로 처리된다. 검출 자체는 옳았다(인스펙터에 Missing 으로 뜨는 진짜 죽은 배선) — 틀린 것은 **검증 없이 쓴 "실행된다"** 였다. 도구 문구도 같이 고쳤다 |
| ~~`OnClick_Pause` 죽은 코드~~ | **고쳤다 (2026-09-02).** 기획 판정: 로비 복귀가 맞되 **확인 창을 거친다.** 정지 버튼을 관리 단계에서도 노출하고, 확인을 받으면 패배와 같은 경로(`GameOver`)로 웨이브 비례 보상을 주고 끝낸다. 도달 불가였던 `isPause` 토글 11줄과 그 필드도 함께 제거 |
| `BuildEnvironmentSelectAsset` | 서버 URL·GameChat 채널 GUID 하드코딩, **런타임 소비처 0**. 다만 **"유령 자산"은 틀렸다** — `Unity3dBuilder.cs:89` 가 이 에셋을 읽어 `DEV_DEFINE` 심볼을 가른다. 지우면 빌드 스크립트가 깨진다. 게다가 `Assets/Scripts/Build/` 에 asmdef 가 없어 런타임 어셈블리 `OJ.Game` 소속이다. `Unity3dBuilder.cs:90` 은 null 검사 없이 `asset.BuildElement` 를 읽는다 |
| `Text (TMP).prefab` | `Assets/Prefab/Equipment/`의 고아 자산 |
| **`MythicScroll`·`SpecialDiceCore` 아이콘** | `PointMetadataDatabase.asset` 의 105·106 이 각각 `LevelStone_Fire.png`(불꽃 빨간 보석) · `LevelStone_Ice.png`(눈꽃 파란 보석)를 가리킨다. 이름과 그림이 어긋난다. **`SpecialDiceCore` 는 일반 스테이지 클리어마다 지급되어 결과창에 상시 노출된다.** **기획 확인 완료 (2026-08-31): 플레이스홀더다.** 전용 아트가 나오면 105·106 의 icon 참조만 교체하면 된다 — 코드 수정 없는 데이터 문제 |
| ~~프리팹 폴더에 섞인 `.controller` / `.anim`~~ | **옮기지 않기로 했다 (11.5).** 프로젝트 전체에 둘뿐이고(`DiceShootAni.controller` · `DiceShootClip.anim`) 둘 다 같은 폴더의 `UIDice.prefab` 하나만 쓴다. `Assets/Animation/` 을 만들어 옮기면 에셋이 유일한 소비처에서 떨어져 관계가 안 보이게 된다 — 이름 규칙을 위해 구조를 나쁘게 만드는 거래다 |

---

