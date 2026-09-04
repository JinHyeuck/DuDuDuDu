using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;
using OJ.DI;
using OJ.Hunting;
using OJ.Point;
using OJ.Save;
using OJ.SceneFlow;
using OJ.Utils;

namespace OJ.Dice
{
    /// <summary>
    /// 주사위 레벨. (MIGRATION_BASELINE 8.3a — 두 번째 전환 대상)
    ///
    /// <b><see cref="MonoSingleton{T}"/> 를 뗐다.</b> 그 베이스는 <c>Instance</c> 게터가
    /// <b>조회가 아니라 생성 트리거</b>라 <c>FindObjectOfType</c> → 실패 시 <c>new GameObject</c>
    /// → <c>AddComponent</c> → 그 자리에서 <c>Awake</c> 동기 실행까지 한다. 즉 누가 언제 처음
    /// 읽느냐에 따라 초기화 시점이 달라졌다. 그래서 <c>TitleSceneController</c> 에
    /// <c>_ = DiceLevelManager.Instance;</c> 같은 <b>값을 버리는 접근</b>이 순서를 강제하려고
    /// 들어가 있었다. 컨테이너가 만들면 그 줄이 필요 없어진다.
    ///
    /// <b>첫 생성자 주입 사례다.</b> <c>PointManager</c> 를 <c>Instance</c> 로 붙잡지 않고
    /// 생성자로 받는다. 덕분에 <c>TryLevelUp</c> 의 <c>PointManager.Instance == null</c> 검사가
    /// 사라졌다 — 없을 수가 없기 때문이다. 없으면 컨테이너가 <b>앱 시작 시점에</b> 터진다.
    /// 결제 시점에 조용히 <c>false</c> 를 돌려주는 것보다 낫다.
    /// </summary>
    // 7.5: ISaveOnApplicationLifecycle 을 뗐다. 이 매니저는 더 이상 자기 저장을 하지 않는다 —
    // 저장은 SaveService 가 통합 파일 하나로 한다. 다시 붙이면 앱이 멈출 때마다 같은 파일을
    // 매니저 수만큼 중복해서 쓰게 된다. ISaveStateOwner 는 그대로다(자기 몫을 읽고 쓰는 역할).
    //
    // IL2CPP 스트리핑 대비. 이유는 GameContainer 주석 참고 — 에디터에서는 안 드러난다.
    [Preserve]
    public sealed class DiceLevelManager : ISaveStateOwner
    {
        /// <summary>
        /// 과도기 다리. 대입은 <see cref="GameContainer"/> 에서만 한다.
        /// 호출부가 70곳(24개 파일)이라 한 번에 못 바꾼다.
        /// </summary>
        public static DiceLevelManager Instance { get; internal set; }

        private readonly Dictionary<DiceType, int> levels = new Dictionary<DiceType, int>();

        private readonly PointManager points;

        public event Action<DiceType, int> OnDiceLevelChanged;

        public DiceLevelManager(PointManager points)
        {
            this.points = points;

            // 7.5: 구 LoadAll 이 PlayerPrefs 를 읽으면서 <b>겸사겸사</b> 하던 초기화를 여기로
            // 끌어올렸다. 통합 세이브는 파일이 없으면 ReadFrom 을 아예 부르지 않는다
            // (SaveService.TryLoadAll 이 owners 루프 전에 return 한다). 초기화를 로드 경로 안에
            // 남겨 둔 채 구 경로만 지웠다면 <b>신규 설치에서 levels 가 영영 비어 있게 된다.</b>
            InitializeLevels();
        }

        /// <summary>
        /// 모든 주사위를 시작 레벨로 깔고 <see cref="isLoaded"/> 를 세운다.
        ///
        /// <b>시작 레벨은 0 이 아니라 1 이다.</b> 구 <c>LoadAll</c> 이 PlayerPrefs 기본값으로 1 을
        /// 주던 자리이고, <see cref="ReadFrom"/> 도 세이브에 없는 주사위를 같은 이유로 1 로 깐다.
        /// 0 으로 두면 강화 비용 조회가 없는 테이블 행을 찾는다.
        ///
        /// 세이브가 있으면 <see cref="ReadFrom"/> 이 이 위를 덮는다. 즉 여기 값은
        /// <b>파일이 없을 때의 정본</b>이다.
        /// </summary>
        private void InitializeLevels()
        {
            levels.Clear();

            foreach (DiceType diceType in Enum.GetValues(typeof(DiceType)))
            {
                if (diceType == DiceType.Max)
                    continue;
                levels[diceType] = 1;
            }

            // 초기화가 끝났으니 이 상태가 곧 정본이다. 여기서 세우지 않으면 WriteTo 가 영원히
            // 건너뛰어 <b>빈 DiceLevels 가 파일에 굳는다</b> — 예전에는 구 LoadAll 이 세워 줬다.
            isLoaded = true;
        }

        public int GetLevel(DiceType diceType)
        {
            if (levels.TryGetValue(diceType, out int level))
                return level;
            return 1;
        }

        public void SetLevel(DiceType diceType, int level, bool saveNow = true)
        {
            if (diceType == DiceType.Max)
                return;

            int clamped = Mathf.Max(1, level);
            levels[diceType] = clamped;
            OnDiceLevelChanged?.Invoke(diceType, clamped);

            if (saveNow)
            {
                // 거래 시점 저장이다. 강화가 끝난 그 자리에서 즉시 쓴다 — 구 코드가
                // PlayerPrefs.Save() 로 디스크까지 밀어 넣던 이유와 같다. 통합 세이브는
                // 파일 전체를 한 번에 쓰므로 주사위 하나만 따로 밀어 넣는 호출은 없다.
                Save();
            }
        }

        public bool TryLevelUp(DiceType diceType)
        {
            int currentLevel = GetLevel(diceType);
            var cost = DiceMetaDataProvider.GetUpgradeCost(diceType, currentLevel);

            var costs = new Dictionary<PointType, int>
            {
                { PointType.Gold, cost.goldCost },
                { PointManager.ToScrollType(diceType), cost.scrollCost }
            };

            if (!points.TrySpend(costs))
                return false;

            SetLevel(diceType, currentLevel + 1);
            return true;
        }

        public (int goldCost, int scrollCost) GetNextUpgradeCost(DiceType diceType)
        {
            return DiceMetaDataProvider.GetUpgradeCost(diceType, GetLevel(diceType));
        }

        /// <summary>
        /// <see cref="InitializeLevels"/> 가 돌았는가. <see cref="WriteTo"/> 의 안전장치다.
        ///
        /// <see cref="WriteTo"/> 는 인메모리 <c>levels</c> 를 기준으로 세이브의 주사위 맵을
        /// <b>통째로 갈아 끼우는데</b>, <see cref="GetLevel"/> 은 키가 없으면 1 을 준다.
        /// 초기화 전에 <see cref="WriteTo"/> 가 한 번 불리면 "전 주사위 1레벨"이라는
        /// 그럴듯한 세이브가 만들어져 <b>원본을 덮는다.</b>
        ///
        /// 7.5 이후 생성자가 무조건 초기화를 마치므로 사실상 항상 true 다. 그래도 남겨 둔다 —
        /// 생성자에 초기화보다 앞서 저장을 부를 수 있는 경로(주입된 의존이 이벤트를 쏘는 등)가
        /// 생기면 그때 이 가드가 잡는다. 같은 이유로 <c>PointManager</c> 에도 같은 가드를 뒀다.
        /// </summary>
        private bool isLoaded;

        /// <summary>이 매니저가 소유한 영구 상태를 <paramref name="state"/> 에 쓴다.</summary>
        public void WriteTo(OJ.Core.SaveState state)
        {
            if (!isLoaded)
            {
                // GetLevel 은 키가 없으면 1 을 주므로, 초기화 전에 쓰면 "전 주사위 1레벨"이라는
                // 그럴듯한 세이브가 만들어져 원본을 덮는다.
                // 던지지는 않는다 — 종료 경로에서도 불리는데 여기서 예외가 나면
                // 뒤이어 모을 다른 매니저의 상태까지 같이 날아간다.
                Debug.LogError(
                    "[DiceLevelManager] 초기화 전에 WriteTo 가 불렸다. 쓰기를 건너뛴다 — " +
                    "그대로 진행하면 모든 주사위가 1레벨로 덮인다.");
                return;
            }

            // 이 맵의 주인은 이 매니저뿐이라 통째로 갈아 끼운다. 남겨 두면 enum 에서 빠진
            // 주사위 이름이 세이브에 계속 눌러앉는다.
            state.DiceLevels.Clear();

            foreach (DiceType diceType in Enum.GetValues(typeof(DiceType)))
            {
                if (diceType == DiceType.Max)
                    continue;

                // InitializeLevels 와 같은 집합이다. 거르는 것은 Max 뿐이고, GetLevel 이 키 없는
                // 주사위에도 1 을 주므로 레벨을 한 번도 올리지 않은 주사위까지 빠짐없이 나간다.
                state.DiceLevels[diceType.ToString()] = GetLevel(diceType);
            }
        }

        /// <summary>영구 상태를 <paramref name="state"/> 에서 읽어 온다.</summary>
        public void ReadFrom(OJ.Core.SaveState state)
        {
            levels.Clear();

            // 세이브에 없는 주사위를 먼저 1 로 깔아 둔다. InitializeLevels 와 같은 자리다 —
            // 주사위의 시작 레벨은 0 이 아니라 1 이라, 빠뜨리면 나중에 추가된 주사위가
            // 옛 세이브에서 0레벨로 살아난다.
            //
            // 생성자가 이미 깔아 뒀지만 여기서 또 깐다. 바로 위 levels.Clear() 가 그것을
            // 지우기 때문이다 — 이 줄을 빼면 세이브에 없는 주사위가 딕셔너리에서 통째로 사라진다.
            foreach (DiceType diceType in Enum.GetValues(typeof(DiceType)))
            {
                if (diceType == DiceType.Max)
                    continue;
                levels[diceType] = 1;
            }

            foreach (var entry in state.DiceLevels)
            {
                // 지금 enum 에 없는 이름이 세이브에 남아 있을 수 있다. 그 한 줄 때문에 예외가 나면
                // 멀쩡한 나머지 주사위 레벨까지 같이 잃으므로 조용히 버린다.
                if (!Enum.TryParse(entry.Key, out DiceType diceType))
                    continue;

                // TryParse 는 "999" 같은 정수 문자열도 통과시켜 정의되지 않은 값을 만들어 낸다.
                // Max 는 SetLevel·WriteTo 가 처음부터 취급하지 않는 자리 표시자라 같이 거른다.
                if (!Enum.IsDefined(typeof(DiceType), diceType) || diceType == DiceType.Max)
                    continue;

                // 구 PlayerPrefs 로드의 Mathf.Max(1, ...) 를 그대로 옮긴 것이다. GetLevel 은
                // 저장된 값을 검사 없이 돌려주므로, 손상된 세이브의 0 이나 음수를 여기서
                // 막지 않으면 강화 비용 조회가 없는 테이블 행을 찾는다.
                levels[diceType] = Mathf.Max(1, entry.Value);
            }

            isLoaded = true;
        }

        /// <summary>
        /// 거래 시점 저장.
        ///
        /// 7.5: PlayerPrefs 대신 통합 세이브를 쓴다. <b>호출 지점을 그대로 두는 것이 중요하다</b> —
        /// 여기서 즉시 저장하지 않으면 앱이 백그라운드로 갈 때까지 진행도가 메모리에만 남고,
        /// 모바일에서 OS 가 프로세스를 죽이는 것은 일상이다. 강화 직후 앱이 죽었는데 레벨이
        /// 돌아가 있는 것은 재화가 사라진 것과 같다.
        ///
        /// <c>?.</c> 가 필요하다. <b>매니저 생성자가 도는 시점에는 SaveService 가 아직 없다</b> —
        /// 컨테이너가 매니저를 다 만든 뒤에 해석한다. 생성자에서 간접적으로 이리 오는 경로가
        /// 생기면 조용히 건너뛰는 것이 맞다. 어차피 직후의 TryLoadAll 이 상태를 덮는다.
        /// </summary>
        private void Save() => GameContainer.SaveService?.SaveAll();
    }
}
