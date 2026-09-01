using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;
using OJ.DI;
using OJ.Save;

namespace OJ.Point
{
    /// <summary>
    /// 재화 보유량. (MIGRATION_BASELINE 8.3a — 첫 번째 전환 대상)
    ///
    /// <b>MonoBehaviour 가 아니다.</b> 이 클래스는 씬·프리팹 어디에도 배치돼 있지 않았고
    /// (GUID 전수 확인) 다른 매니저를 하나도 붙잡지 않는다. A무리 12개 중 의존이 0인
    /// 유일한 매니저라 전환 순서를 여기서 시작했다.
    ///
    /// <b>없어진 것과 어디로 갔는지.</b>
    /// <list type="bullet">
    /// <item><c>Bootstrap</c>(<c>RuntimeInitializeOnLoadMethod</c>) → <see cref="GameContainer"/>.
    ///   <b>반드시 지워야 했다.</b> 남겨 두면 컨테이너가 만든 인스턴스와 부트스트랩이 만든
    ///   인스턴스가 각각 별개의 재화 저장소가 되고, 마지막에 저장한 쪽이 이긴다.</item>
    /// <item><c>Awake</c> 의 <c>LoadAll()</c> → 생성자의 <see cref="InitializePoints"/>.
    ///   7.5 에서 구 키 저장소를 읽던 로드 경로를 지우면서 그 안에 섞여 있던 <b>초기화만</b> 살려
    ///   생성자로 끌어올렸다. 저장된 진행도는 <see cref="ReadFrom"/> 이 그 위에 덮는다.
    ///   <b>초기화를 다시 로드 경로 안으로 넣지 말 것</b> — 세이브 파일이 없는 첫 실행에서는
    ///   <see cref="ReadFrom"/> 이 아예 불리지 않아서, 그러면 신규 설치가 초기화되지 않은
    ///   상태로 시작한다.</item>
    /// <item><c>DontDestroyOnLoad</c> → 컨테이너 수명. 루트 스코프가 씬을 넘어 산다.</item>
    /// <item><c>OnApplicationPause</c> / <c>OnApplicationQuit</c> → <see cref="SaveService"/>.
    ///   <b>7.5 이후 이 매니저는 <c>ISaveOnApplicationLifecycle</c> 이 아니다.</b> 앱이 멈출 때
    ///   파일을 쓰는 것은 <see cref="SaveService"/> 하나뿐이고, 이 매니저는 자기 몫을
    ///   <see cref="WriteTo"/> 로 넘길 뿐이다. 여기에 수명주기 인터페이스를 다시 붙이면
    ///   앱이 멈출 때마다 같은 파일이 한 번 더 쓰인다.</item>
    /// </list>
    ///
    /// <b><see cref="Instance"/> 는 과도기 다리다.</b> 호출부가 70곳(16개 파일)이라 한 번에
    /// 못 바꾼다. 대신 <b>대입하는 곳을 하나로 못 박았다</b> — <see cref="GameContainer"/> 만
    /// 쓰는 <c>internal set</c> 이다. 인스턴스를 만드는 경로가 컨테이너 하나뿐이므로
    /// "두 인스턴스가 생겨 마지막에 저장한 쪽이 이긴다"는 사고는 일어날 수 없다.
    /// 호출부를 주입으로 옮기고 나면 이 속성은 사라진다.
    /// </summary>
    // IL2CPP 스트리핑 대비. 이유는 GameContainer 주석 참고 — 에디터에서는 안 드러난다.
    [Preserve]
    public sealed class PointManager : ISaveStateOwner
    {
        /// <summary>
        /// 과도기 다리. <b>대입은 <see cref="GameContainer"/> 에서만 한다.</b>
        /// 다른 곳에서 대입하면 그 순간 인스턴스가 둘이 되고 재화가 조용히 사라진다.
        /// </summary>
        public static PointManager Instance { get; internal set; }

        public event Action<PointType, int> OnPointChanged;

        private readonly Dictionary<PointType, int> points = new Dictionary<PointType, int>();

        /// <summary>
        /// 만들어지는 즉시 <b>초기 상태를 갖춘다.</b> 예전 <c>Awake</c> 의 <c>LoadAll()</c> 자리다.
        ///
        /// 컨테이너가 이 클래스를 만드는 시점은 <c>BeforeSceneLoad</c> 라, 어떤 씬 오브젝트의
        /// <c>Awake</c> 보다도 먼저다. 즉 <see cref="Instance"/> 를 읽는 UI 들이 도는 시점에는
        /// 이미 초기화가 끝나 있다. 저장된 진행도는 컨테이너가 그 뒤에
        /// <c>SaveService.TryLoadAll</c> 로 덮는다.
        /// </summary>
        public PointManager()
        {
            InitializePoints();
        }

        /// <summary>
        /// 재화 18종을 0 으로 채우고 <see cref="isLoaded"/> 를 세운다.
        ///
        /// <b>왜 로드 경로가 아니라 생성자인가.</b> 세이브 파일이 없는 첫 실행에서는
        /// <see cref="ReadFrom"/> 이 <b>한 번도 불리지 않는다</b> — <c>SaveService.TryLoadAll</c> 이
        /// 파일이 없으면 owners 루프에 닿기 전에 돌아간다. 그래서 초기화가 로드 경로 안에 있으면
        /// 신규 설치에서 통째로 건너뛰어지고, <see cref="isLoaded"/> 가 영원히 false 로 남아
        /// <see cref="WriteTo"/> 까지 막힌다 — <b>빈 재화 맵이 파일에 굳는다.</b>
        /// </summary>
        private void InitializePoints()
        {
            points.Clear();

            foreach (PointType pointType in Enum.GetValues(typeof(PointType)))
            {
                if (pointType == PointType.Max)
                    continue;

                // 0 을 미리 넣어 둔다. Get 이 없는 키를 0 으로 돌려주므로 읽는 값은 같지만,
                // 구 LoadAll 이 만들던 "키가 전부 있는 맵"과 모양을 맞춰 둔다 —
                // points 를 직접 훑는 코드가 나중에 생겨도 재화가 빠져 보이지 않는다.
                points[pointType] = 0;
            }

            isLoaded = true;
        }

        public int Get(PointType pointType)
        {
            if (points.TryGetValue(pointType, out int value))
                return value;

            return 0;
        }

        public void Set(PointType pointType, int value, bool saveNow = true)
        {
            if (pointType == PointType.Max)
                return;

            int clamped = Mathf.Max(0, value);
            points[pointType] = clamped;
            OnPointChanged?.Invoke(pointType, clamped);

            if (saveNow)
                Save();
        }

        public void Add(PointType pointType, int amount, bool saveNow = true)
        {
            if (amount <= 0)
                return;

            Set(pointType, Get(pointType) + amount, saveNow);
        }

        public bool TrySpend(PointType pointType, int amount, bool saveNow = true)
        {
            if (amount < 0)
                return false;

            if (Get(pointType) < amount)
                return false;

            Set(pointType, Get(pointType) - amount, saveNow);
            return true;
        }

        public bool CanAfford(IReadOnlyDictionary<PointType, int> costs)
        {
            foreach (var pair in costs)
            {
                if (Get(pair.Key) < pair.Value)
                    return false;
            }

            return true;
        }

        public bool TrySpend(IReadOnlyDictionary<PointType, int> costs)
        {
            // 단일 오버로드(위)는 amount < 0 을 막는데 이쪽은 막지 않고 있었다.
            // 비용이 음수면 CanAfford 를 통과하고(보유량 < 음수 는 항상 거짓)
            // Get - (음수) = 증가가 되어 <b>재화가 발급된다.</b>
            // TrySpendUpgrade / TrySpendEquipmentUpgrade 가 전부 이 경로를 타므로
            // 데이터 테이블에 음수 비용이 하나 들어가면 그대로 뚫린다.
            foreach (var pair in costs)
            {
                if (pair.Value < 0)
                    return false;
            }

            if (!CanAfford(costs))
                return false;

            foreach (var pair in costs)
            {
                Set(pair.Key, Get(pair.Key) - pair.Value, false);
            }

            SaveAll();
            return true;
        }

        public bool TrySpendUpgrade(DiceType diceType, int goldCost, int scrollCost)
        {
            var costs = new Dictionary<PointType, int>
            {
                { PointType.Gold, goldCost },
                { ToScrollType(diceType), scrollCost }
            };

            return TrySpend(costs);
        }

        public bool TrySpendEquipmentUpgrade(EquipmentType equipmentType, int goldCost, int scrollCost)
        {
            var costs = new Dictionary<PointType, int>
            {
                { PointType.Gold, goldCost },
                { ToEquipmentScrollType(equipmentType), scrollCost }
            };

            return TrySpend(costs);
        }

        public static PointType ToScrollType(DiceType diceType)
        {
            switch (diceType)
            {
                case DiceType.Normal:
                    return PointType.NormalScroll;
                case DiceType.Fire:
                    return PointType.FireScroll;
                case DiceType.Ice:
                    return PointType.IceScroll;
                case DiceType.Poison:
                    return PointType.PoisonScroll;
                case DiceType.Thunder:
                    return PointType.ThunderScroll;
                case DiceType.KingNormal:
                    return PointType.MythicScroll;
                case DiceType.KingFire:
                    return PointType.MythicScroll;
                case DiceType.KingIce:
                    return PointType.MythicScroll;
                case DiceType.KingPoison:
                    return PointType.MythicScroll;
                case DiceType.KingThunder:
                    return PointType.MythicScroll;
                case DiceType.Tornado:
                    return PointType.SpecialDiceCore;
                case DiceType.Stun:
                    return PointType.SpecialDiceCore;
                case DiceType.ArmorBreak:
                    return PointType.SpecialDiceCore;
                case DiceType.Wind:
                    return PointType.SpecialDiceCore;
                case DiceType.Time:
                    return PointType.SpecialDiceCore;
                default:
                    throw new ArgumentOutOfRangeException(nameof(diceType), diceType, "Unsupported dice type.");
            }
        }

        public static PointType ToEquipmentScrollType(EquipmentType equipmentType)
        {
            switch (equipmentType)
            {
                case EquipmentType.Weapon:
                    return PointType.WeaponScroll;
                case EquipmentType.Helmet:
                    return PointType.HelmetScroll;
                case EquipmentType.Armor:
                    return PointType.ArmorScroll;
                case EquipmentType.Ring:
                    return PointType.RingScroll;
                case EquipmentType.Shoes:
                    return PointType.ShoesScroll;
                case EquipmentType.Necklace:
                    return PointType.NecklaceScroll;
                default:
                    throw new ArgumentOutOfRangeException(nameof(equipmentType), equipmentType, "Unsupported equipment type.");
            }
        }

        /// <summary>
        /// 초기 상태를 갖췄는가. <see cref="SaveAll"/> / <see cref="WriteTo"/> 의 안전장치다.
        ///
        /// <b>왜 필요한가.</b> <see cref="WriteTo"/> 는 인메모리 <c>points</c> 를 기준으로
        /// 세이브의 재화 맵을 <b>통째로 갈아 끼운다.</b> 그런데 <see cref="Get"/> 은 키가 없으면
        /// 0 을 준다. 그래서 아무것도 갖추기 전에 한 번 쓰이면 <b>재화 전부가 0 으로 지워진다.</b>
        /// 되돌릴 수 없다.
        ///
        /// <b>7.5 에서 이 플래그를 세우는 자리가 옮겨졌다.</b> 예전에는 구 키 저장소를 읽던
        /// <c>LoadAll()</c> 이 세웠는데 그 메서드가 사라졌다. 지금은 생성자
        /// (<see cref="InitializePoints"/>)가 세운다 — 가드의 뜻이 "아직 아무것도 <b>읽지</b>
        /// 않았다"가 아니라 "아직 아무것도 <b>갖추지</b> 않았다"이기 때문이다. 초기화가 끝나면
        /// 그 상태가 곧 정본이고(파일이 있으면 <see cref="ReadFrom"/> 이 그 위에 덮는다),
        /// 여기서 세우지 않으면 영원히 false 로 남아 빈 재화 맵이 파일에 굳는다.
        ///
        /// 그래도 가드 자체는 남겨 둔다. 초기화 방식을 바꿀 때마다 "아무도 닿기 전에 초기화가
        /// 끝나 있다"는 <b>암묵적</b> 보장이 소리 없이 사라지는데, 이 가드는 그 사고를
        /// <b>데이터가 아니라 로그로</b> 드러낸다.
        /// </summary>
        private bool isLoaded;

        /// <summary>
        /// 보유량 전체를 <b>지금 즉시</b> 파일에 쓴다.
        ///
        /// 7.5 에서 저장 매체만 구 키 저장소에서 통합 세이브로 바뀌었다. <b>호출 지점은 그대로
        /// 두는 것이 중요하다</b> — 여기서 즉시 저장하지 않으면 앱이 백그라운드로 갈 때까지
        /// 진행도가 메모리에만 남고, 모바일에서 OS 가 프로세스를 죽이는 것은 일상이다.
        /// </summary>
        public void SaveAll()
        {
            if (!isLoaded)
            {
                // 던지지 않고 거른다. 여기서 예외를 내면 종료 경로가 죽어 정상 종료까지 막힌다.
                // 저장을 안 하는 것이 0 으로 덮는 것보다 낫다.
                Debug.LogError(
                    "[PointManager] 초기화 전에 SaveAll 이 불렸다. 저장을 건너뛴다 — " +
                    "그대로 진행하면 재화 전체가 0 으로 덮인다. 컴포지션 루트가 초기화를 빠뜨린 것이다.");
                return;
            }

            Save();
        }

        /// <summary>이 매니저가 소유한 영구 상태를 <paramref name="state"/> 에 쓴다.</summary>
        /// <remarks>
        /// <see cref="SaveAll"/> 와 같은 가드를 건다. 통합 세이브는 <b>읽어 온 상태를 그대로 다시
        /// 쓰는</b> 구조라, 초기화 전에 여기까지 오면 인메모리의 0 이 직전 세이브의 보유량을 덮는다.
        /// 아무것도 건드리지 않고 빠져야 앞서 들어 있던 값이 살아남는다.
        /// </remarks>
        public void WriteTo(OJ.Core.SaveState state)
        {
            if (!isLoaded)
            {
                // SaveAll 과 같은 이유로 던지지 않는다. 종료 경로에서 예외를 내면
                // 나머지 매니저의 저장까지 통째로 날아간다.
                Debug.LogError(
                    "[PointManager] 초기화 전에 WriteTo 가 불렸다. 쓰기를 건너뛴다 — " +
                    "그대로 진행하면 재화 전체가 0 으로 덮인다. 컴포지션 루트가 초기화를 빠뜨린 것이다.");
                return;
            }

            // 이 맵의 주인은 이 매니저 하나뿐이라 통째로 갈아 끼운다. 같은 state 를 재사용해
            // 저장하는 구조에서는, enum 에서 빠진 재화 이름을 아무도 지우지 않아 영원히 남는다.
            state.Points.Clear();

            foreach (PointType pointType in Enum.GetValues(typeof(PointType)))
            {
                if (pointType == PointType.Max)
                    continue;

                // 0 이라고 거르지 않고 enum 전체를 쓴다. 읽는 쪽이 빠진 키를 0 으로 보므로
                // 파일 크기 말고는 차이가 없지만, 내보내는 키 집합이 항상 같아야
                // SaveService 의 로드 후 왕복 대조(JSON 문자열 비교)가 흔들리지 않는다.
                state.Points[pointType.ToString()] = Get(pointType);
            }
        }

        /// <summary>영구 상태를 <paramref name="state"/> 에서 읽어 온다.</summary>
        /// <remarks>
        /// 세이브가 아니라 enum 을 돌면서 이름으로 찾는다. 그래서 <b>없어진 재화 이름이 세이브에
        /// 남아 있어도 조용히 무시되고</b>, 새로 생긴 재화는 키가 없어 0 이 된다 —
        /// enum 을 고치는 것만으로 세이브가 깨지지 않게 하려는 것이다.
        /// </remarks>
        public void ReadFrom(OJ.Core.SaveState state)
        {
            points.Clear();

            foreach (PointType pointType in Enum.GetValues(typeof(PointType)))
            {
                if (pointType == PointType.Max)
                    continue;

                // 키가 없으면 out 이 0 을 준다. 처음 보는 재화는 0 개로 시작한다는 뜻이다.
                state.Points.TryGetValue(pointType.ToString(), out int value);

                // 음수 차단. Set 이 항상 Mathf.Max(0, ..) 을 거치므로
                // "보유량은 음수가 아니다"가 나머지 코드의 전제인데, 손으로 고친 세이브나 깨진
                // 파일에서 음수가 들어오면 그 전제가 깨져 이후에 얻은 재화가 빚을 메우는 데
                // 먼저 쓰이고 조용히 사라진다.
                points[pointType] = Mathf.Max(0, value);
            }

            // 생성자가 이미 세워 두므로 지금은 중복이다. 그래도 남긴다 — 이 메서드만 따로
            // 부르는 경로(진단·테스트)가 생겼을 때 "읽었는데 저장이 막혀 있다"는
            // 상태가 만들어지지 않게 하려는 것이다.
            isLoaded = true;
        }

        /// <summary>치트/진단용 전체 초기화. 재화를 전부 0 으로 만들고 그 상태를 즉시 저장한다.</summary>
        /// <remarks>
        /// 7.5 전에는 값을 0 으로 만든 뒤 구 저장소의 키까지 지웠다. 지금은 지울 키가 없다 —
        /// <see cref="WriteTo"/> 가 세이브의 재화 맵을 통째로 갈아 끼우므로 0 을 쓰는 것이 곧
        /// 지우는 것이다. <b>마지막 저장을 빠뜨리면 안 된다</b> — 메모리만 0 이 되고 파일에는 옛
        /// 보유량이 남아, 다음 실행에서 초기화한 줄 알았던 재화가 그대로 되살아난다.
        /// </remarks>
        public void ResetAllForDebug()
        {
            foreach (PointType pointType in Enum.GetValues(typeof(PointType)))
            {
                if (pointType == PointType.Max)
                    continue;

                // 재화마다 저장하지 않는다(saveNow: false). 통합 세이브는 한 번에 파일 전체를
                // 쓰므로 18번 부르면 같은 파일을 18번 쓰고, 중간 상태가 파일에 남는다.
                Set(pointType, 0, false);
            }

            SaveAll();
        }

        // 7.5: 구 키 저장소 대신 통합 세이브를 쓴다. 파일 하나가 정본이라 재화 하나만 따로 쓸 수
        // 없고 그럴 이유도 없어서, 예전의 Save(PointType) 에서 인자가 사라졌다.
        // 호출 지점(Set 의 saveNow, SaveAll)은 그대로 두는 것이 중요하다 — 거래 직후에 쓰지
        // 않으면 앱이 백그라운드로 갈 때까지 진행도가 메모리에만 남는다.
        //
        // ?. 가 필요하다. 매니저 생성자가 도는 시점에는 SaveService 가 아직 없다 —
        // 컨테이너가 매니저들을 만든 뒤에 SaveService 를 해석하기 때문이다. 그 시점에 간접적으로
        // 여기까지 오면 조용히 건너뛰는 것이 맞다. 직후에 TryLoadAll 이 파일을 읽어 덮으므로
        // 그때 저장해 봐야 의미가 없고, 오히려 초기 상태가 파일을 덮을 위험만 있다.
        private void Save()
        {
            OJ.DI.GameContainer.SaveService?.SaveAll();
        }
    }
}
