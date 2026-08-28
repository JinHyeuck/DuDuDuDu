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
| -1.4 | 업그레이드 단독 커밋 | [ ] | 재직렬화 diff가 리팩토링 커밋에 섞이지 않게 |

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
| 1.1 | `CombatPowerUIPrefabBuilder` 비활성화 | [ ] | 0.5로 당겨짐 |
| 1.2 | 인코딩 정규화 (UTF-8) | [ ] | CP949 2파일 변환 + U+FFFD 2파일 주석 재작성 |
| 1.3 | 평문 키스토어 비밀번호 제거 | [ ] | 파일 수정 + **히스토리 재작성 + 비밀번호 로테이션** |
| 1.4 | `.meta` 추적 / `.gitignore` 상태 확인 | [ ] | |
| 1.5 | **Missing script 기준선 기록** | [ ] | 11단계 게이트("Missing 0건")를 판정하려면 현재 값이 필요하다 |

**게이트:** 에디터를 재기동해도 LobbyScene diff가 오염되지 않고, 전체 `.cs`가 UTF-8로 디코딩된다.

---

### 2. 조용한 폴백 봉인

> **경로 복구가 봉인보다 먼저다.** `DiceMetaDataDatabase.asset`·`GemDefinitionDatabase.asset`은
> `Assets/ScriptableObject/`에 있어 `Resources.Load`가 **항상** 실패한다. 폴백을 예외로 승격하면
> 그 즉시 이 두 경로가 예외가 되어 Battle/Lobby 직접 재생이 통째로 막힌다.

| # | 항목 | 상태 |
|---|---|---|
| 2.1 | 실패하는 Resources 경로 복구 (에셋 이동 또는 StaticResource 전 씬 배치) | [ ] |
| 2.2 | Provider 3단 폴백 → 명시적 실패로 승격 | [ ] |
| 2.3 | MonoSingleton 자동 생성 봉인 | [ ] |

**게이트:** Battle/Lobby를 직접 재생하면 `StaticResource` 부재와 DB null이 **명시적 에러 로그**로 뜬다.

---

### 3. 씨앗 asmdef + 특성화 테스트

목표는 "현행 동작을 그대로 고정"이지 개선이 아니다. 골든값을 박아둔다.

| # | 항목 | 상태 | 비고 |
|---|---|---|---|
| 3.1 | 씨앗 asmdef 1개 (`autoReferenced: true`) | [ ] | 테스트 대상 계산식만 이동. **`Assembly-CSharp`는 asmdef에서 참조 불가** |
| 3.2 | 4개 계산식 파라미터 주입형으로 최소 개조 | [ ] | `CalculateDamage` / `StageData` 성장식 / `StageRewardCalculator` / `IdleReward` 환산 |
| 3.3 | 테스트 asmdef + 골든 테스트 | [ ] | |

**게이트:** 골든 테스트가 전부 통과하고, 개조 전후 실플레이에서 데미지·보상 수치가 동일하다.

---

### 4. 수치 정본 확정 (SO 승격)

> 순서를 지킬 것. `DiceMetaDataDatabase.asset`의 일부 값이 코드 fallback과 **다르므로**,
> 덤프 없이 덮어쓰기부터 제거하면 밸런스가 조용히 바뀐다.

| # | 항목 | 상태 |
|---|---|---|
| 4.1 | 코드 fallback 값을 asset에 **1회 덤프** (asset == 코드 상태 만들기) | [ ] |
| 4.2 | 골든 테스트로 덤프 전후 동일성 증명 | [ ] |
| 4.3 | `MergeMeta` / `ApplyMonsterHpBalance` 제거 | [ ] |
| 4.4 | SO → 텍스트 덤프 검증 스크립트 | [ ] | `.asset` YAML은 리뷰가 안 되므로 |

**게이트:** 덮어쓰기 제거 후에도 골든 테스트가 **값 변화 없이** 통과한다.

---

### 5. 도메인 순수화 (최대 덩어리)

> 착수 전에 **`RunState`의 필드 목록(스키마)만 먼저 확정**한다. 상태 소유자 없이 규칙만 순수화하면
> 6단계에서 시그니처를 전부 다시 고친다.

| # | 항목 | 상태 |
|---|---|---|
| 5.0 | `RunState` 스키마 확정 (SP·보드·원소레벨·벽HP) | [ ] |
| 5.1 | `CalculateDamage`에서 싱글톤 조회 제거 + 스탯 스냅샷 계약 도입 | [ ] |
| 5.2 | `EquipmentManager`(780줄) 상태·규칙 분리 | [ ] |
| 5.3 | `RelicManager`(776줄) 상태·규칙 분리 | [ ] |
| 5.4 | `Monster.TakeDamage` / `Wall.TakeDamage`에서 표시 호출 분리 | [ ] | `DamageTextPool`, `RectTransform.sizeDelta` |
| 5.5 | 시간 소스 통일 (`IClock` 추상) | [ ] | `Time.deltaTime` 직산 + `WaitForSecondsRealtime` + `DateTime.UtcNow` 혼재 중 |

**게이트:** MonoBehaviour 없이 순수 클래스만으로 데미지 계산 경로가 EditMode에서 실행되고 골든값이 유지된다.

> `RelicManager`는 쿨감·피해배율·폭발범위·연쇄+1·스턴확률·독전이·벽부활 등 전용 게터 20여 개가
> 여러 파일에 박혀 있다. **하나만 놓쳐도 그 유물만 무효화되는데 컴파일·콘솔 어디에도 안 나타난다.**

---

### 6. 런 상태 소유자 통합

| # | 항목 | 상태 |
|---|---|---|
| 6.1 | `RunState` 신설 및 흩어진 상태 이관 | [ ] |
| 6.2 | 오브젝트 풀 수명 정리 | [ ] | Bullet/BulletEffect/DamageText/몬스터 풀이 전부 static |

**게이트:** UI를 하나도 띄우지 않고 웨이브 1회를 시뮬레이션해 결과 등급이 산출된다.

---

### 7. 저장 파이프라인 (스키마까지만)

> **실제 서비스 배선은 8단계 이후.** DI 없이 배선하면 static Instance로 상태에 접근하게 되고
> 8단계에서 전부 다시 쓴다.

| # | 항목 | 상태 | 비고 |
|---|---|---|---|
| 7.1 | Newtonsoft.Json 패키지 추가 | [ ] | |
| 7.2 | 통합 `SaveState` + **버전 필드** | [ ] | 기존 유저 세이브는 버림(결정 2) |
| 7.3 | 순수 매퍼 (`ToSave` / `Restore`) | [ ] | |
| 7.4 | 원자 쓰기 | [ ] | `.writing` → fsync → `File.Replace` (+`.bak`) |
| 7.5 | 구 PlayerPrefs 10키 경로 제거 | [ ] | |
| 7.6 | 개발용 세이브 초기화 치트 | [ ] | |

**게이트:** 저장·로드 왕복 후 모든 값이 동일하고, 구 PlayerPrefs 키가 코드에서 완전히 사라졌다.

---

### 8. DI 도입 + 부트스트랩

| # | 항목 | 상태 |
|---|---|---|
| 8.1 | VContainer 패키지 + `VCONTAINER_UNITASK_INTEGRATION` 심볼 (전 플랫폼) | [ ] |
| 8.2 | `RootLifetimeScope` (콘텐츠별 Installer 분할) | [ ] |
| 8.3 | static Instance 21개 + MonoSingleton 청산 | [ ] |
| 8.4 | `RuntimeInitializeOnLoadMethod` 부트스트랩 3계보 통합 | [ ] |
| 8.5 | `Object.Instantiate` 35곳 → `IObjectResolver.Instantiate` / 팩토리 | [ ] |
| 8.6 | `Awake` 초기화 → 주입 이후로 이동 | [ ] |
| 8.7 | 7단계 저장 서비스 실배선 | [ ] |
| 8.8 | IL2CPP 스트리핑 대응 (`link.xml` / `[Preserve]`) | [ ] | **에디터에서는 통과하고 실기 빌드에서만 터진다** |

**게이트:** static Instance가 0(또는 문서화된 예외만), 씬 직접 재생 시 침묵 대신 명시적 실패, 골든 테스트 계속 통과.

> 두 경로(싱글톤/DI) 공존 기간이 길면 **같은 상태의 인스턴스가 2개 생겨 마지막에 저장한 쪽이 이긴다.**
> 재화·장비·유물 진행도가 조용히 사라지는 형태로 나타난다. 공존 기간을 최대한 짧게.

---

### 9. 씬 흐름 재구성

> `UICanvasLayout`과 `FadeView`는 이 단계보다 **앞서** 필요하다(`SceneRouter`가 의존).
> 10단계에서 다시 만들지 않도록 여기서 확정한다.

| # | 항목 | 상태 |
|---|---|---|
| 9.1 | `UICanvasLayout` + `FadeView` (10단계에서 당겨옴) | [ ] |
| 9.2 | 캔버스/해상도 규격 통일 | [ ] | 로비 1080x1920 Overlay vs 배틀 Canvas 2개(하나는 ScreenSpaceCamera 800x600) |
| 9.3 | `SceneId` enum + 빌드 인덱스 정합 | [ ] |
| 9.4 | `SceneRouter` + 전환 게이트 (연타 차단) | [ ] |
| 9.5 | PlayerPrefs 왕복으로 스테이지 넘기던 방식 → 전이 홀더 | [ ] |

**게이트:** Boot→Lobby→Battle→Lobby 왕복이 페이드와 함께 동작하고, 전환 버튼 연타가 차단된다.

---

### 10. UI 프레임워크 교체

> **10단계 착수 시점에 "생성기 대상 / 수작업 대상"을 먼저 분류한다.** 12단계 생성기가
> 여기서 손으로 만든 프리팹을 덮어쓰면 작업이 날아간다.

| # | 항목 | 상태 |
|---|---|---|
| 10.1 | `IUIService` / `UIService` — **`ShowAsync<T>(param)`이 인스턴스를 반환하게** | [ ] |
| 10.2 | `DialogBase` (B의 `IDialog` 리네임 포함) | [ ] |
| 10.3 | `DialogCatalog` — 명시적 키 + 등재 검증 테스트 | [ ] |
| 10.4 | 다이얼로그 17개 이관 + 씬 인스턴스 제거 | [ ] |
| 10.5 | `UIIdleRewardDialog` 재작성 | [ ] |
| 10.6 | 백키를 기존 `AOSBackBtnManager` 스택에 통합 | [ ] |
| 10.7 | 한글 폰트 SDF 문자셋 확인 | [ ] | 문자셋이 다르면 한글이 조용히 □로 뜬다 |

**게이트:**
- 다이얼로그 17개가 전부 카탈로그 경유로 열리고 닫힌다
- 씬에 상주하는 다이얼로그 인스턴스가 0
- **오프너·컨트롤러의 씬 참조가 전부 카탈로그 경유로 대체됐고 `FindFirstObjectByType` 폴백이 0건**
- 백키가 단일 스택으로 동작

> "씬 인스턴스 0"만으로는 부족하다. 참조하던 `[SerializeField]`가 **Missing이 아니라 None**이 되어
> 콘솔 무음으로 죽는 경로를 못 잡는다.

---

### 11. 폴더 규약 + asmdef 확정

| # | 항목 | 상태 |
|---|---|---|
| 11.1 | `OJ.Core` 분리 (`noEngineReferences: true`) | [ ] |
| 11.2 | `OJ.Game` 분리 | [ ] |
| 11.3 | 폴더 재배치 (`AssetDatabase.MoveAsset` 경유) | [ ] |
| 11.4 | 네임스페이스 재부여 (`OJ.*` 계층) | [ ] |
| 11.5 | `Prefab` / `Prefabs` 폴더 통합 | [ ] |
| 11.6 | Resources 경로 문자열 23곳 정리 | [ ] | 아트를 옮기면 컴파일 통과 상태로 런타임 null이 된다 |
| 11.7 | UniTask 벤더링 → UPM 전환 검토 | [ ] | 전환 시 타입 중복(CS0433) 주의 |

**게이트:** asmdef가 순환 없이 컴파일되고, 프리팹·씬의 Missing script가 **1.5 기준선 대비 증가 0건**.

---

### 12. 에디터 자동화

| # | 항목 | 상태 |
|---|---|---|
| 12.1 | 프리팹 생성기 (10단계에서 분류한 대상만) | [ ] |
| 12.2 | 정적 검증 CI 편입 | [ ] |

**게이트:** 생성기를 연속 2회 실행해도 결과가 동일(멱등)하고, 카탈로그에 중복 엔트리가 생기지 않는다.

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

## 미해결 / 확인 필요

| 항목 | 비고 |
|---|---|
| `UIDice.prefab`의 살아 있는 옛 `OnClick()` | `verify_dead_events.py` 검출. 대상이 같은 프리팹 안이라 **실제로 실행된다** |
| `OnClick_Pause` 죽은 코드 | "일시정지인가 로비 복귀인가" 기획 확인 후 정리 |
| `BuildEnvironmentSelectAsset` | 서버 URL·GameChat 채널 GUID 하드코딩, 런타임 소비처 0 (유령 자산) |
| `Text (TMP).prefab` | `Assets/Prefab/Equipment/`의 고아 자산 |
| 프리팹 폴더에 섞인 `.controller` / `.anim` | 11.5에서 정리 |
