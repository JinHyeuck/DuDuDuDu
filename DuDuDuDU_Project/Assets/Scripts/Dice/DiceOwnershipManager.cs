using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;
using OJ.DI;
using OJ.Point;
using OJ.Save;

namespace OJ.Dice
{
    /// <summary>
    /// 다이스를 영구히 보유하는가. (다이스 언락)
    ///
    /// <b>이 게임에 없던 개념이다.</b> 예전에는 특수·킹 다이스를 판 안에서 진화로 만들었다
    /// 판이 끝나면 잃었고, 영구히 남는 다이스 상태는 레벨뿐이었다. 그래서 무한의 탑의
    /// 다이스 잠금이 "보유" 대신 "층 진행" 을 기준으로 삼을 수밖에 없었고, 그건 기획서가
    /// 말한 것과 다른 규칙이라 아예 꺼 두었다(<c>TowerProgressManager.DiceLockEnabled</c>).
    /// 이 매니저가 그 기준을 제자리로 돌린다.
    ///
    /// <b><see cref="DiceLevelManager"/> 와 나란히 두고 합치지 않는다.</b> 레벨은
    /// "얼마나 센가", 보유는 "쓸 수 있는가" 로 축이 다르다. 합쳐서 <c>WriteTo</c> 에
    /// 보유 조건을 걸면 <b>언락 전에 올려 둔 레벨이 저장에서 사라진다</b> — 스크롤은 언락과
    /// 무관하게 쌓이므로 그 상태는 실재한다. 그래서 <c>DiceLevelManager</c> 는 한 줄도 건드리지
    /// 않았고, 미보유 다이스의 강화는 <b>UI 에서만</b> 막는다.
    ///
    /// <b>기본 다이스는 상태를 갖지 않는다.</b> <see cref="IsOwned"/> 가 티어만 보고 즉시
    /// <c>true</c> 를 준다. 그 규칙은 이미 <c>TowerProgressManager.IsDiceUnlocked</c> 와
    /// <c>DiceUnlockDatabase.Validate</c> 에 있고, 세이브에까지 적으면 정본이 셋이 된다.
    /// 덤으로 <b>세이브가 비거나 깨져도 기본 5종은 살아 있다.</b>
    /// </summary>
    // IL2CPP 스트리핑 대비. 이유는 GameContainer 주석 참고 — 에디터에서는 안 드러난다.
    [Preserve]
    public sealed class DiceOwnershipManager : ISaveStateOwner
    {
        /// <summary>
        /// 과도기 다리. <b>대입은 <see cref="GameContainer"/> 에서만 한다.</b>
        /// 주입 창구가 있는 곳(<c>MergeSystem</c> 등)은 이것을 쓰지 않는다.
        /// </summary>
        public static DiceOwnershipManager Instance { get; internal set; }

        private readonly HashSet<DiceType> owned = new HashSet<DiceType>();

        private readonly PointManager points;

        /// <summary>보유가 바뀌었다. 로비 목록과 언락 팝업이 구독한다.</summary>
        public event Action<DiceType> OnOwnershipChanged;

        public DiceOwnershipManager(PointManager points)
        {
            this.points = points;

            // 깔 것이 없다. 기본 다이스는 상태가 아니라 규칙이고, 나머지는 전부 미보유가 시작이다.
            // 그래도 여기서 세워 둔다 — 세이브 파일이 없으면 ReadFrom 이 아예 불리지 않으므로
            // (SaveService.TryLoadAll 이 owners 루프 전에 return 한다) 신규 설치에서
            // isLoaded 가 영영 false 로 남으면 WriteTo 가 매번 건너뛴다.
            isLoaded = true;
        }

        /// <summary>
        /// <see cref="WriteTo"/> 의 안전장치. <c>DiceLevelManager</c> 의 같은 이름 필드와 같은 역할인데
        /// <b>결과가 더 나쁘다</b> — 잃은 레벨은 스크롤로 되찾지만, 잃은 보유는 재화를 다시 내야 한다.
        /// </summary>
        private bool isLoaded;

        // ── 판정 ────────────────────────────────────────────────────────

        /// <summary>
        /// 이 다이스를 쓸 수 있는가.
        ///
        /// <b>여기서 에셋을 건드리지 않는다.</b> 티어 판정(<see cref="DiceEvolution.GetTier"/>)은
        /// <c>DiceType</c> 의 숫자 구간만 보는 순수 함수다. 그래서 이 메서드는 씬이 하나도 없는
        /// <c>BeforeSceneLoad</c> 에서 불려도 안전하다 — <c>DiceUnlockDatabaseProvider</c> 를
        /// 여기서 부르면 그 시점에 <c>StaticResource</c> 가 없어 거짓 LogError 가 찍히고,
        /// 그 로그가 잠겨 나중의 진짜 사고를 삼킨다.
        /// </summary>
        public bool IsOwned(DiceType diceType)
        {
            if (diceType == DiceType.Max)
                return false;

            if (DiceEvolution.GetTier(diceType) == DiceTier.Base)
                return true;

            return owned.Contains(diceType);
        }

        /// <summary>
        /// 재화 가격. 살 수 없으면 0 이다 — <b>0 은 "공짜" 가 아니다.</b>
        /// 에셋을 깨우므로 씬이 선 뒤에만 부를 것.
        /// </summary>
        public int GetPrice(DiceType diceType)
        {
            if (diceType == DiceType.Max || DiceEvolution.GetTier(diceType) == DiceTier.Base)
                return 0;

            return DiceUnlockDatabaseProvider.Database.GetPrice(diceType);
        }

        /// <summary>
        /// 언락에 쓰는 재화. <b>강화에 쓰는 재화와 같은 것이다</b> —
        /// 특수는 <c>SpecialDiceCore</c>, 킹은 <c>MythicScroll</c>.
        /// 매핑을 SO 에 다시 적지 않는 이유가 이 한 줄이다.
        /// </summary>
        public static PointType GetPriceCurrency(DiceType diceType)
        {
            return PointManager.ToScrollType(diceType);
        }

        /// <summary>지금 살 수 있는가. 버튼의 활성 여부에 쓴다.</summary>
        public bool CanAfford(DiceType diceType)
        {
            int price = GetPrice(diceType);
            if (price <= 0)
                return false;

            return points.Get(GetPriceCurrency(diceType)) >= price;
        }

        // ── 획득 ────────────────────────────────────────────────────────

        /// <summary>
        /// 경로 1 — 재화로 연다.
        ///
        /// 차감과 등재가 <b>한 번의 저장으로</b> 끝나야 한다. 재화만 빠지고 앱이 죽으면
        /// 그건 재화를 버린 것과 같으므로, 차감은 <c>saveNow: false</c> 로 하고
        /// <see cref="Grant"/> 가 파일을 한 번 쓴다.
        /// </summary>
        public bool TryUnlockWithPoints(DiceType diceType)
        {
            if (IsOwned(diceType))
                return false;

            int price = GetPrice(diceType);
            if (price <= 0)
                return false;

            if (!points.TrySpend(GetPriceCurrency(diceType), price, saveNow: false))
                return false;

            Grant(diceType);
            return true;
        }

        /// <summary>
        /// 경로 2 — 컨텐츠가 준다. <b>중복 환급의 유일한 자리다.</b>
        ///
        /// 별의 시련·스테이지 클리어·무한의 탑 셋이 각자 환급을 짜면 환급액의 정본이 셋이 되고,
        /// 가격을 고칠 때 한 곳만 고치게 된다.
        ///
        /// <b>재화를 여기서 넣지 않는다.</b> <paramref name="refundInto"/> 에 담기만 하고
        /// 호출부가 자기 보상 목록에 합쳐 <c>PointRewardUtility.GrantRewards</c> 로 한 번에 넣는다 —
        /// 지급 경로가 둘이 되면 이중 지급을 막을 자리가 없어진다. 세 컨텐츠 모두 이미
        /// "리스트를 만들고 자기가 지급한다" 는 모양이라 새 규약도 아니다.
        /// </summary>
        /// <returns>이번에 <b>새로</b> 얻었으면 true. "해금!" 연출의 유일한 근거다.</returns>
        public bool GrantFromContent(DiceType diceType, List<PointRewardEntry> refundInto)
        {
            return GrantFromContent(diceType, refundInto, out _);
        }

        /// <summary>
        /// 위와 같되 <b>화면이 그릴 것</b>까지 알려 준다.
        ///
        /// 지급이 끝나면 <c>IsOwned</c> 는 두 경우 모두 참이라, 처음 얻은 것인지
        /// 재화로 돌아온 것인지 <b>화면이 스스로 구별할 수 없다.</b> 아는 것은 여기뿐이다.
        /// </summary>
        public bool GrantFromContent(DiceType diceType, List<PointRewardEntry> refundInto, out DiceRewardView view)
        {
            int price = GetPrice(diceType);

            if (ShouldGrant(diceType, IsOwned(diceType), price, refundInto))
            {
                Grant(diceType);
                view = DiceRewardView.NewlyOwned(diceType);
                return true;
            }

            view = DiceRewardView.Refunded(diceType, GetPriceCurrency(diceType), price);
            return false;
        }

        /// <summary>
        /// 위 메서드의 <b>결정 규칙 전부</b>. 상태를 바꾸지 않고, 보유 여부와 가격을 인자로 받는다.
        ///
        /// <b>왜 떼어 냈나.</b> 실제 지급은 <see cref="Grant"/> → <c>GameContainer.SaveService</c> 로
        /// 이어지는데, 그 타입을 건드리는 순간 VContainer 가 딸려 와 헤드리스 EditMode 러너에서
        /// <c>TypeLoadException</c> 이 난다. 러너의 탐색 경로에 <c>Library/ScriptAssemblies</c> 를
        /// 더하면 풀리지만, 그러면 <b>에디터가 만든 낡은 DLL 이 갓 빌드한 것을 이길 수 있어</b>
        /// 이 러너가 존재하는 이유 자체가 흔들린다. 규칙을 상태 밖으로 꺼내는 편이 싸고 안전하다.
        ///
        /// 결정표는 넷이다 — 줄 수 없는 것(Max·기본)은 거짓, 미보유는 지급, 보유는 환급,
        /// 보유인데 가격이 없으면 사고다.
        /// </summary>
        /// <returns>지급해야 하면 true. false 면 <paramref name="refundInto"/> 에 환급이 담겼거나 줄 것이 없다.</returns>
        public static bool ShouldGrant(DiceType diceType, bool alreadyOwned, int price, List<PointRewardEntry> refundInto)
        {
            if (diceType == DiceType.Max)
                return false;

            // 기본 다이스는 줄 것이 없다. 여기 오면 데이터가 잘못된 것이고,
            // DiceUnlockDatabase.Validate 가 에디터에서 먼저 잡는다.
            if (DiceEvolution.GetTier(diceType) == DiceTier.Base)
                return false;

            if (!alreadyOwned)
                return true;

            if (price <= 0)
            {
                // 환급할 금액을 모른다 = 유저가 아무것도 못 받는다. 조용히 넘기면
                // "보상을 받았는데 아무 일도 없었다"가 되므로 소리를 낸다.
                Debug.LogError(
                    "[DiceOwnership] " + diceType + " 이(가) 이미 보유 상태인데 가격이 없어 환급하지 못했다. " +
                    "DiceUnlockDatabase 에 그 줄의 price 가 빠졌다.");
                return false;
            }

            refundInto?.Add(new PointRewardEntry(GetPriceCurrency(diceType), price));
            return false;
        }

        /// <summary>
        /// 보유 목록에 올리고 즉시 저장한다.
        ///
        /// <b>거래 시점 저장이다.</b> 여기서 쓰지 않으면 앱이 백그라운드로 갈 때까지 보유가
        /// 메모리에만 있고, 모바일에서 OS 가 프로세스를 죽이는 것은 일상이다. 재화를 내고
        /// 얻은 다이스가 사라지는 것은 재화가 사라진 것과 같다.
        /// </summary>
        private void Grant(DiceType diceType)
        {
            if (!owned.Add(diceType))
                return;

            Save();
            OnOwnershipChanged?.Invoke(diceType);
        }

        // ── 저장 ────────────────────────────────────────────────────────

        /// <summary>이 매니저가 소유한 영구 상태를 <paramref name="state"/> 에 쓴다.</summary>
        public void WriteTo(OJ.Core.SaveState state)
        {
            if (!isLoaded)
            {
                // 초기화 전에 쓰면 "아무것도 보유 안 함" 이라는 그럴듯한 세이브가 원본을 덮는다.
                // 던지지는 않는다 — 종료 경로에서도 불리는데 여기서 예외가 나면 뒤이어 모을
                // 다른 매니저의 상태까지 같이 날아간다.
                Debug.LogError(
                    "[DiceOwnershipManager] 초기화 전에 WriteTo 가 불렸다. 쓰기를 건너뛴다 — " +
                    "그대로 진행하면 보유한 다이스가 전부 지워진다.");
                return;
            }

            // 이 맵의 주인은 이 매니저뿐이라 통째로 갈아 끼운다.
            state.OwnedDice.Clear();

            foreach (DiceType diceType in owned)
                state.OwnedDice[diceType.ToString()] = 1;
        }

        /// <summary>영구 상태를 <paramref name="state"/> 에서 읽어 온다.</summary>
        public void ReadFrom(OJ.Core.SaveState state)
        {
            owned.Clear();

            foreach (var entry in state.OwnedDice)
            {
                // 지금 enum 에 없는 이름이 세이브에 남아 있을 수 있다. 그 한 줄 때문에 예외가 나면
                // 멀쩡한 나머지 보유까지 같이 잃으므로 조용히 버린다.
                if (!Enum.TryParse(entry.Key, out DiceType diceType))
                    continue;

                // TryParse 는 "999" 같은 정수 문자열도 통과시켜 정의되지 않은 값을 만들어 낸다.
                if (!Enum.IsDefined(typeof(DiceType), diceType) || diceType == DiceType.Max)
                    continue;

                // 기본 다이스는 규칙으로 보유다. 옛 세이브나 손댄 파일에 들어 있어도 담지 않는다 —
                // 담으면 WriteTo 가 그것을 다시 뱉어 "정본이 셋" 상태가 파일에 굳는다.
                if (DiceEvolution.GetTier(diceType) == DiceTier.Base)
                    continue;

                if (entry.Value <= 0)
                    continue;

                owned.Add(diceType);
            }

            isLoaded = true;
        }

        /// <summary>
        /// 거래 시점 저장.
        ///
        /// <c>?.</c> 가 필요하다 — <b>매니저 생성자가 도는 시점에는 SaveService 가 아직 없다</b>
        /// (컨테이너가 매니저를 다 만든 뒤에 해석한다). 생성자에서 간접적으로 이리 오는 경로가
        /// 생기면 조용히 건너뛰는 것이 맞다. 어차피 직후의 TryLoadAll 이 상태를 덮는다.
        /// </summary>
        private void Save() => GameContainer.SaveService?.SaveAll();
    }
}
