using System.Collections.Generic;

namespace OJ.Core
{
    /// <summary>
    /// 미션·업적이 세는 <b>행동</b>. 일일 미션과 업적이 같은 목록을 쓴다.
    ///
    /// <b>이름이 그대로 세이브 키가 된다</b>(<see cref="MissionRules.CounterKey(MissionAction)"/>).
    /// <c>PointType</c> 을 이름으로 저장하는 것과 같은 이유다 — 정수로 저장하면 가운데에
    /// 항목 하나를 끼워 넣는 순간 저장된 값이 조용히 다른 행동을 가리킨다.
    /// <b>항목을 지우거나 이름을 바꾸면 그 카운터는 0 부터 다시 시작한다.</b> 일일분은
    /// 어차피 자정에 지워지므로 사고가 아니지만, 업적의 누적 카운트는 되돌릴 수 없다.
    ///
    /// <b>인게임 행동과 로비 행동이 섞여 있다.</b> 가르지 않은 것은 세는 쪽이 같기 때문이다 —
    /// 둘 다 <c>MissionManager.Notify</c> 한 곳으로 들어온다. 어디서 났는지는 호출부가 알지
    /// 이 열거형이 알 일이 아니다.
    /// </summary>
    public enum MissionAction
    {
        /// <summary>접속. 하루 한 번만 오른다(일일 리셋과 같은 주기라 n 은 1 뿐이다).</summary>
        Login = 0,

        /// <summary>전투 입장. 본편 스테이지와 무한의 탑이 해당한다 — 소탕·핀볼은 아니다.</summary>
        GamePlay,

        /// <summary>핀볼 발사. <b>배율만큼 오른다</b> — x100 한 발은 100 이다.</summary>
        PinballShot,

        /// <summary>현금 결제. SDK 가 붙기 전까지는 테스트 결제 경로가 올린다.</summary>
        IapPurchase,

        /// <summary>다이스 레벨업. 하위 키로 <c>Normal</c>/<c>Rare</c>/<c>Mythic</c> 이 함께 오른다.</summary>
        DiceLevelUp,

        /// <summary>장비 레벨업. 6부위를 가리지 않는다.</summary>
        EquipmentLevelUp,

        /// <summary>보석 뽑기. <b>뽑은 개수만큼 오른다</b> — 10연차 한 번은 10 이다.</summary>
        GemDraw,

        /// <summary>
        /// 보석 합성. <b>결과 보석 1개당 1 이다</b>(버튼 1회가 아니다).
        /// 하위 키는 <b>재료</b>의 등급이다 — 레어 4개를 합치면 <c>GemMerge:Rare</c> 가 오른다.
        /// </summary>
        GemMerge,

        /// <summary>리워드 광고를 끝까지 봤다. 중간에 닫은 것은 오르지 않는다.</summary>
        AdWatch,

        /// <summary>적 처치. 전투에서 실제로 죽은 것만 — 소탕은 몬스터를 만들지 않으므로 오르지 않는다.</summary>
        MonsterKill,

        /// <summary>상점 구매. 현금 결제와 보석 뽑기는 각자 제 항목이 있어 여기 오지 않는다.</summary>
        ShopPurchase,

        /// <summary>현상금 처치. <see cref="MonsterKill"/> 도 함께 오른다 — 현상금도 적이다.</summary>
        BountyKill,

        /// <summary>전투 중 다이스 머지. 진화·교환은 아니다.</summary>
        DiceMerge,
    }

    /// <summary>
    /// 미션·업적의 순수 규칙. <b>목록과 값을 인자로 받는 <c>static</c> 뿐이다</b> —
    /// SO 도 매니저도 참조하지 않아야 헤드리스 러너에서 돈다.
    /// </summary>
    public static class MissionRules
    {
        /// <summary>
        /// 카운터 키의 구분자. 세이브에 그대로 나가므로 하위 키에 이 글자를 쓰면 안 된다
        /// (<see cref="IsValidSubKey"/> 가 막는다).
        /// </summary>
        public const char SubKeySeparator = ':';

        /// <summary>
        /// 행동 → 키 문자열. <b>미리 만들어 둔다.</b>
        ///
        /// <c>Enum.ToString()</c> 은 리플렉션을 타고 매번 새 문자열을 만든다. 적 처치는
        /// 한 웨이브에 수백 번 들어오는 경로라, 거기서 문자열을 새로 찍으면 전투 중에
        /// 쓰레기가 쌓인다 — 미션이 프레임을 갉아먹을 이유가 없다.
        /// </summary>
        private static readonly string[] ActionKeys = BuildActionKeys();

        private static string[] BuildActionKeys()
        {
            var values = (MissionAction[])System.Enum.GetValues(typeof(MissionAction));

            int max = 0;
            for (int i = 0; i < values.Length; i++)
            {
                int value = (int)values[i];
                if (value > max)
                    max = value;
            }

            var keys = new string[max + 1];
            for (int i = 0; i < values.Length; i++)
                keys[(int)values[i]] = values[i].ToString();

            return keys;
        }

        /// <summary>하위 키가 없는 카운터 키. 일일 미션은 전부 이쪽을 본다.</summary>
        public static string CounterKey(MissionAction action)
        {
            int index = (int)action;
            if (index >= 0 && index < ActionKeys.Length && ActionKeys[index] != null)
                return ActionKeys[index];

            // 표에 없는 값이 들어왔다. 열거형 밖의 정수를 캐스팅해 넘긴 경우뿐이고,
            // 그때도 키 하나는 돌려줘야 카운트가 조용히 사라지지 않는다.
            return action.ToString();
        }

        /// <summary>
        /// 하위 키가 붙은 카운터 키. <c>GemMerge:Rare</c> 꼴이다.
        ///
        /// <b>하위 키가 비면 상위 키와 같아진다.</b> 그래서 "전체" 를 가리키는 업적은
        /// 하위 키를 비워 두면 되고, 따로 <c>All</c> 같은 이름을 만들지 않아도 된다.
        /// </summary>
        public static string CounterKey(MissionAction action, string subKey)
        {
            if (string.IsNullOrEmpty(subKey))
                return CounterKey(action);

            return action.ToString() + SubKeySeparator + subKey;
        }

        /// <summary>
        /// 하위 키로 쓸 수 있는 문자열인가. 구분자가 들어 있으면 키가 쪼개져
        /// 다른 카운터와 충돌하므로 데이터 검증에서 막는다.
        /// </summary>
        public static bool IsValidSubKey(string subKey)
        {
            return string.IsNullOrEmpty(subKey) || subKey.IndexOf(SubKeySeparator) < 0;
        }

        /// <summary>달성했는가. <paramref name="required"/> 가 0 이하면 데이터 사고이므로 달성으로 치지 않는다.</summary>
        public static bool IsCleared(int count, int required)
        {
            return required > 0 && count >= required;
        }

        /// <summary>
        /// 진행 바 비율(0~1). <paramref name="required"/> 가 0 이하면 0 이다 —
        /// 나누기를 막는 것이자, 데이터가 비었을 때 바가 꽉 찬 것처럼 보이지 않게 하는 것이다.
        /// </summary>
        public static float Progress(int count, int required)
        {
            if (required <= 0)
                return 0f;

            if (count >= required)
                return 1f;

            return count <= 0 ? 0f : (float)count / required;
        }

        /// <summary>
        /// 업적 한 계열에서 <b>지금 화면에 보일</b> 단계. 전부 받았으면 마지막 단계를 준다
        /// (그 상태가 "완료" 다). 단계가 없으면 -1.
        ///
        /// <b>달성이 아니라 수령을 기준으로 고른다.</b> 달성 기준으로 넘기면 10회를 깬 순간
        /// 100회가 보이고 <b>10회의 보상을 받을 자리가 화면에서 사라진다</b> — 받을 수 없는
        /// 보상이 생긴다. 받고 나서 넘어가면 그 구멍이 없다.
        ///
        /// <paramref name="claimed"/> 는 요구 횟수 오름차순으로 정렬돼 있다고 본다
        /// (<c>AchievementDatabase</c> 가 로딩 때 정렬한다). 목록이 단계 수보다 짧으면
        /// 그 뒤는 받지 않은 것으로 본다.
        /// </summary>
        public static int VisibleTierIndex(int tierCount, IReadOnlyList<bool> claimed)
        {
            if (tierCount <= 0)
                return -1;

            if (claimed == null)
                return 0;

            int limit = claimed.Count < tierCount ? claimed.Count : tierCount;
            for (int i = 0; i < limit; i++)
            {
                if (!claimed[i])
                    return i;
            }

            if (limit < tierCount)
                return limit;

            // 전부 받았다. 마지막 단계를 "완료" 로 보여 준다.
            return tierCount - 1;
        }

        /// <summary>
        /// <see cref="VisibleTierIndex"/> 가 고른 단계가 <b>완료 표시</b>인가.
        /// 마지막 단계까지 전부 받은 상태를 말한다.
        /// </summary>
        public static bool IsSeriesComplete(int tierCount, IReadOnlyList<bool> claimed)
        {
            if (tierCount <= 0 || claimed == null || claimed.Count < tierCount)
                return false;

            for (int i = 0; i < tierCount; i++)
            {
                if (!claimed[i])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 일일 추가 보상의 문턱 중 <paramref name="clearedCount"/> 로 열린 것의 개수.
        /// 문턱 목록은 오름차순이라고 본다.
        ///
        /// <b>수령이 아니라 달성으로 센다.</b> 화면의 게이지는 "오늘 몇 개 깼나"를 보여 주는
        /// 것이지 "몇 개 받았나"가 아니다 — 수령 기준으로 두면 다 깨고도 게이지가 비어 있다.
        /// </summary>
        public static int UnlockedTierCount(int clearedCount, IReadOnlyList<int> thresholds)
        {
            if (thresholds == null)
                return 0;

            int unlocked = 0;
            for (int i = 0; i < thresholds.Count; i++)
            {
                if (thresholds[i] > 0 && clearedCount >= thresholds[i])
                    unlocked++;
            }

            return unlocked;
        }

        /// <summary>
        /// 추가 보상 게이지의 비율(0~1). 마지막 문턱에서 1 이 된다.
        ///
        /// <b>문턱 간격이 고르지 않아도 칸 간격은 고르게 그린다</b>(3·5·7·10 은 2·2·3 이다).
        /// 그래서 비율을 요구 횟수가 아니라 <b>문턱 번째</b>로 계산한다 — 화면의 선물 상자가
        /// 등간격으로 놓이는 것과 게이지가 어긋나지 않는다.
        /// </summary>
        public static float TierGaugeProgress(int clearedCount, IReadOnlyList<int> thresholds)
        {
            if (thresholds == null || thresholds.Count == 0)
                return 0f;

            int unlocked = UnlockedTierCount(clearedCount, thresholds);
            if (unlocked >= thresholds.Count)
                return 1f;

            int previous = unlocked == 0 ? 0 : thresholds[unlocked - 1];
            int next = thresholds[unlocked];
            if (next <= previous)
                return (float)unlocked / thresholds.Count;

            float withinSegment = (float)(clearedCount - previous) / (next - previous);
            if (withinSegment < 0f)
                withinSegment = 0f;
            else if (withinSegment > 1f)
                withinSegment = 1f;

            return (unlocked + withinSegment) / thresholds.Count;
        }
    }
}
