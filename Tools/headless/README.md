# 헤드리스 EditMode 러너

Unity 에디터를 **열지 않고** EditMode 테스트를 돌린다. 리포 루트에서 한 줄:

```
Tools\headless\run-tests.cmd
```

종료 코드: `0` 전부 통과 / `1` 테스트 실패 / `2` 컴파일 실패 / `3` 러너 오류.

```
Tools\headless\run-tests.cmd --filter "StageGrowth"     # 정규식으로 골라 돌리기
Tools\headless\run-tests.cmd --list                     # 테스트 이름만 나열
Tools\headless\run-tests.cmd --xml out\nunit.xml        # NUnit3 결과 XML 저장
Tools\headless\run-tests.cmd --rebuild --verbose        # 강제 재컴파일 + 스택 트레이스
Tools\headless\run-tests.cmd --min-tests 496            # 이보다 적게 수집되면 실패
```

첫 실행은 러너를 빌드하느라 ~10초, 그다음부터는 **1초대**다.
어셈블리 전부를 다시 컴파일하는 `--rebuild` 는 5~6초. 변경이 없으면 컴파일을 건너뛴다.

---

## 조용한 초록불을 막는 장치

이 도구에서 가장 위험한 고장은 "터지는 것"이 아니라 **적게 돌고 통과라고 하는 것**이다.
세 겹으로 막는다.

1. **0건은 통과가 아니다.** 필터 오타나 빌드 사고로 한 건도 안 돌면 종료 코드 1 이다.
   `--list` 도 마찬가지다.
2. **스위트오류를 따로 센다.** 픽스처 생성자나 `TestCaseSource` 가 터져 테스트가 아예
   생성되지 않으면 NUnit 이 그 자리에 오류를 남기는데, 그걸 실패로 집계한다.
   (어셈블리에 `[TestFixture]` 가 하나도 없는 경우도 여기서 걸린다.)
3. **`minTests` 로 개수를 고정한다.** 위 둘로도 부족하다 — 테스트 파일 하나가 통째로
   사라지면 496개가 323개로 줄지만 남은 것은 전부 통과라 초록불이 나온다. 그래서
   `headless.config.json` 의 테스트 어셈블리마다 기대 개수를 커밋해 두고 대조한다.
   적게 수집되면 실패다. 테스트를 의도적으로 줄였다면 **같은 커밋에서 이 숫자도 낮춰라** —
   그래야 리뷰에 드러난다.

`--filter` 를 준 실행은 개수가 줄어드는 것이 정상이므로 3번은 건너뛴다(1번은 그대로 산다).
`--min-tests <n>` 로 그때그때 하한을 줄 수도 있다.

---

## 동작

1. `Assets/Scripts/Core/*.cs` 를 **Unity 가 들고 있는 Roslyn**(`Editor/Data/DotNetSdkRoslyn/csc.dll`)으로
   컴파일해 `OJ.Core.dll` 을 만든다.
2. `Assets/Tests/EditMode/*.cs` 를 같은 컴파일러로 컴파일해 `OJ.Core.Tests.dll` 을 만든다.
3. `Assets/Scripts` 의 나머지(= Unity 의 `Assembly-CSharp`)를 컴파일한다. **테스트는 아니고
   컴파일만 본다.** 아래 절 참고.
4. `OJ.Headless.TestHost.exe` 를 **Unity 의 Mono**(`MonoBleedingEdge/bin/mono.exe`)로 띄워
   NUnit 이 테스트를 수집·실행하게 한다.
5. NUnit3 결과 XML 을 읽어 통과/실패를 요약한다.

산출물은 전부 `Tools/headless/.build/` 아래 — `.gitignore` 에 등록돼 있다.
이 폴더는 `Assets/` 바깥이라 `.meta` 가 생기지 않고 Unity 가 건드리지 않는다.

### `Assembly-CSharp` 을 컴파일하는 이유

이게 없을 때 러너는 **게임 코드의 컴파일 오류를 통째로 못 봤다.** 실측된 사고 두 가지:

- `using UnityEngine.SceneManagement;` 누락이 러너를 통과했다.
- 5.2 에서 `EquipmentManager` 의 위임 코드에 변이를 심어도 러너는 0건을 잡았다.

컴파일만 하고 테스트는 하지 않는다(`"test": false`). 즉 **"무기 골드가 조용히 투구 골드가 된다"
같은 의미 변이는 여전히 못 잡는다** — 그건 테스트의 몫이다. 여기서 닫는 것은 "컴파일조차 안 되는
코드가 초록불로 통과하는" 구멍뿐이다.

소스 집합은 Unity 자신의 컴파일과 파일 단위까지 같다(`Assets/Scripts` 에서 `Core/`, `Editor/`,
`*/Editor/*` 를 뺀 134개). `Library/Bee/artifacts/*.dag/Assembly-CSharp.rsp` 와 대조해 확인했다.

### `Library/ScriptAssemblies` 를 (거의) 쓰지 않는 이유

거기 있는 DLL 은 Unity 에디터가 **포커스를 받아야** 갱신된다. 그걸 쓰면 결국 사람이
에디터를 클릭해야 하므로 이 도구의 존재 이유가 없어진다. 그래서 게임 코드는 전부 소스에서
직접 컴파일한다. `OJ.Core` 와 `OJ.Core.Tests` 는 이 폴더를 아예 쳐다보지 않는다.

예외는 **여기서만 얻을 수 있는 것**이다:

- `Library/PackageCache` 의 `nunit.framework.dll` — Unity 가 컴파일한 것이 아니라 패키지에 그대로
  들어 있는 바이너리다. 에디터 포커스와 무관하다.
- `Assembly-CSharp` 이 참조하는 패키지 어셈블리(UniTask, TextMeshPro, uGUI …) — 이건 Unity 밖에서
  만들 수 없고 설치본에도 없다. `references` 의 `"scriptAssemblies"` 로 명시적으로 끌어 쓴다.

**대가**: 패키지 목록이 바뀌면 에디터를 한 번 열어 이 폴더를 갱신해야 한다. 반대로 게임 코드는
여전히 소스에서 컴파일되므로 "내 수정이 반영되지 않는다"는 문제는 생기지 않는다. `Library/` 가
통째로 없으면 러너가 그 사실을 명시하며 종료 코드 3 으로 멈춘다(조용히 건너뛰지 않는다).

---

## 왜 Mono 에서 실행하는가 — 이 도구의 핵심

Unity 에디터는 EditMode 테스트를 **Mono** 에서 돌린다. Mono 의 JIT 는 float 식의 중간 결과를
매 연산마다 float 로 접지 않고 더 높은 정밀도로 들고 가다가 대입 시점에 한 번 접는다.
C# 명세가 허용하는 동작이다. CoreCLR(.NET)은 연산마다 접는다.

같은 IL 인데도 답이 갈린다:

```
1f + (7f * 0.145f) + (7f * 7f * 0.015f)
  Mono    -> 2.75f       -> Mathf.RoundToInt(2 * 2.75f)      = 6
  CoreCLR -> 2.7499998f  -> Mathf.RoundToInt(2 * 2.7499998f) = 5
```

`StageGrowthFormulaTests.MonsterHpKeepsLeftToRightFloatAssociation` 이 정확히 이 지점을 밟는다.
컴파일만 똑같이 하고 .NET 에서 실행하면 이 테스트가 **통과해 버려서** 에디터 결과(실패)와 어긋난다.
그래서 실행은 반드시 Unity 의 Mono 에서 한다.

`--coreclr` 를 주면 .NET 런타임에서 돌릴 수 있다. 진단용이다 —
위 차이 때문에 에디터와 결과가 갈릴 수 있으니 판정에 쓰지 말 것.

---

## 어셈블리를 추가할 때

C# 을 고칠 필요 없다. `headless.config.json` 의 `assemblies` 에 항목만 넣는다.
위에서부터 순서대로 컴파일되고, `references` 에 앞선 어셈블리 이름을 쓸 수 있다.

```json
{
  "name": "OJ.Game.Tests",
  "sources": [ "DuDuDuDU_Project/Assets/Tests/EditMode/Game" ],
  "references": [ "OJ.Core", "OJ.Game", "nunit" ],
  "defines": [ "UNITY_EDITOR_ONLY_COMPILATION" ],
  "test": true,
  "minTests": 120
}
```

- UnityEngine 모듈과 BCL 참조는 항상 자동으로 들어간다. `references` 에는 그 밖의 것만 적는다.
- `test: true` 인 어셈블리만 실행 대상이다. `false` 면 **컴파일만** 한다.
- `minTests` 는 그 어셈블리에서 수집돼야 하는 최소 개수다(0 이면 검사 안 함).
  에디터에서 한 번 돌려 나온 실측값을 넣어라.

`references` 에 쓸 수 있는 것:

| 값 | 뜻 |
|---|---|
| `"nunit"` | PackageCache 의 `nunit.framework.dll` |
| `"unityEditor"` | `UnityEditor.dll` + `UnityEditor.*Module.dll`. 런타임 코드라도 `#if UNITY_EDITOR` 블록이 있으면 필요하다 |
| `"scriptAssemblies"` | `Library/ScriptAssemblies` 의 DLL 전부. 이 설정이 소스에서 빌드하는 어셈블리와 `excludeReferences` 는 빠진다 |
| 그 밖의 이름 | 이 설정에서 **먼저** 빌드된 어셈블리, 또는 실제 DLL 경로 |

그 밖의 어셈블리 옵션:

- `"unsafe": true` → `-unsafe+`. `unsafe` 블록이 있으면 필수다(없으면 CS0227).
  Unity 쪽 대응은 asmdef 의 `allowUnsafeCode` 또는 Player Settings 의 "Allow 'unsafe' Code".
- `"exclude"` → 파일 이름(`"*.Editor.cs"`) 또는 `sources` 기준 상대 경로(`"*/Editor/*"`, `"Core/*"`)로
  소스를 뺀다. `*` 는 `/` 도 넘어가므로 `"Core/*"` 가 하위 폴더까지 다 걷어낸다. 다만
  `"*/Editor/*"` 는 앞에 폴더가 하나는 있어야 하니 최상위 `Editor/` 는 `"Editor/*"` 로 따로 적는다.
- `"excludeReferences"` → `"scriptAssemblies"` 가 끌어온 DLL 을 이름으로 뺀다.

경고는 Unity 와 같은 목록(`0169 0649 0282 1701 1702`)을 억제한다. `[SerializeField]` 필드마다 나는
CS0649 를 막지 않으면 진짜 오류가 수백 줄 경고에 파묻힌다.

### Unity 를 올렸을 때

`unityVersion` 이 `"auto"` 라서 `ProjectSettings/ProjectVersion.txt` 를 따라간다 — 보통 손댈 것이 없다.
다만 `defines` 는 Unity 6000.3.7f1 이 만들어 준 `OJ.Core.csproj` 의 `DefineConstants` 를 그대로 박아 둔
값이다. 버전이 올라 심볼이 바뀌면 그 csproj 에서 다시 뽑아 넣는다.

설치 경로가 특이하면 `UNITY_EDITOR_ROOT` 환경변수로 직접 지정할 수 있다.

---

## 골든 기준선 (`OJ_GOLDEN_BASELINE`)

`Assets/Tests/EditMode/GoldenBaseline.cs` 는 원래 `Application.dataPath` 로 기준선 파일을 찾는데,
그 API 는 에디터/플레이어 밖에서 못 쓴다. 그래서 환경변수 `OJ_GOLDEN_BASELINE` 가 있으면
그 경로를 먼저 쓰도록 고쳤다. 러너가 `headless.config.json` 의 `environment` 값을 넘긴다.

Unity 안에서는 이 변수가 없으므로 예전 경로 계산이 그대로 쓰인다 — **에디터 동작은 바뀌지 않았다.**

---

## 요구 사항

- .NET SDK 8 이상 (러너 자체를 빌드한다)
- Unity 6000.3.7f1 설치본 (Roslyn, Mono, 엔진 어셈블리, BCL 참조 어셈블리를 여기서 가져온다)
- 프로젝트의 `Library/PackageCache` 또는 UPM 전역 캐시에 `com.unity.ext.nunit`
- 프로젝트의 `Library/ScriptAssemblies` — `Assembly-CSharp` 이 참조하는 패키지 DLL 이 여기 있다.
  갓 클론한 리포처럼 `Library/` 가 없으면 에디터를 **한 번** 열어 만들어야 한다.

그 뒤로는 Unity 를 **실행할** 필요가 없다. 게임 코드는 언제나 소스에서 다시 컴파일되므로
에디터를 열지 않아도 방금 고친 코드가 검사된다.
