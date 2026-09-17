using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;
using OJ.Point;
using OJ.Save;

namespace OJ.Pinball
{
    /// <summary>
    /// 핀볼의 실행부. 티켓 지불·칸 추첨·특수 핀 게이지·경품 지급을 한 자리에서 한다.
    ///
    /// <b>MonoBehaviour 가 아니다.</b> <c>GameContainer</c> 가 만들고 <c>SaveService</c> 가 저장한다.
    ///
    /// <b>이 판이 어떻게 굴러가는지.</b> 툴킷은 "결과를 먼저 뽑고 그 결과로 떨어지는 궤적을
    /// 재생"한다(<c>Assets/Pinball/README.md</c>). <c>PlayForSlot(slot)</c> 은 칸을 <b>인자로 받고</b>,
    /// 물리는 그 칸으로 가는 예쁜 궤적을 고르는 역할만 한다. 그래서 착지 확률의 정본은
    /// 물리가 아니라 <c>PinballBoard.declaredProbability</c> 이고, 그것을 굴리는 것이 여기다.
    ///
    /// <b>특수 핀은 추첨 대상이 아니다.</b> 재생된 궤적에서 나오는 결과라 횟수를 지정할 수 없다.
    /// 그래서 "n회 맞으면 보상"은 확률이 아니라 <b>판을 넘어 누적되는 게이지</b>로 센다.
    /// </summary>
    [Preserve]
    public sealed class PinballManager : ISaveStateOwner
    {
        /// <summary>과도기 다리. 대입은 <c>GameContainer</c> 에서만 한다.</summary>
        public static PinballManager Instance { get; internal set; }

        /// <summary>세이브 키의 접두사. <c>tag:1</c> 꼴이 된다.</summary>
        private const string TagKeyPrefix = "tag:";

        /// <summary>특수 핀 태그 → 누적 적중 수. 값이 0 인 태그는 넣지 않는다.</summary>
        private readonly Dictionary<int, int> gauges = new Dictionary<int, int>();

        /// <summary>게이지가 바뀌었을 때. 화면이 진행도를 다시 그린다.</summary>
        public event Action OnGaugeChanged;

        private static PinballRewardDatabase Database => PinballDatabaseProvider.GetDatabase();

        /// <summary>1회 플레이에 드는 티켓 수.</summary>
        public int TicketCost => Database.TicketCost;

        /// <summary>한 세션에 쏠 수 있는 공의 최대 수.</summary>
        public int MaxShotsPerSession => Database.MaxShotsPerSession;

        /// <summary>배율 표. 화면이 버튼을 나열하는 데 쓴다.</summary>
        public IReadOnlyList<PinballMultiplierTier> MultiplierTiers => Database.multiplierTiers;

        /// <summary>공 하나를 <paramref name="multiplier"/> 배로 쏘는 데 드는 티켓.</summary>
        public int ShotCost(int multiplier)
        {
            return PinballRules.ScaleAmount(TicketCost, multiplier);
        }

        /// <summary>
        /// 그 배율이 열렸는가. <b>보유량이 기준이지 소모량이 아니다</b> —
        /// 600장을 들고 x5 를 열었어도 5장만 남으면 열린 채로 남는다. 쏠 수 있는지는
        /// <see cref="CanShoot"/> 가 따로 본다.
        /// </summary>
        public bool IsMultiplierUnlocked(int multiplier)
        {
            PinballMultiplierTier tier = Database.GetTier(multiplier);
            if (tier == null)
                return false;

            return PinballRules.IsMultiplierUnlocked(TicketCount, tier.requiredTickets);
        }

        /// <summary>그 배율을 열려면 티켓이 몇 장 있어야 하는가. 표에 없으면 -1.</summary>
        public int RequiredTicketsFor(int multiplier)
        {
            PinballMultiplierTier tier = Database.GetTier(multiplier);
            return tier != null ? tier.requiredTickets : -1;
        }

        /// <summary>티켓이 x1 한 발치라도 있는가.</summary>
        public bool CanPlay => CanShoot(1);

        /// <summary>
        /// 지금 그 배율로 한 발 쏠 수 있는가 — 배율이 열려 있고 티켓도 그만큼 있어야 한다.
        /// </summary>
        public bool CanShoot(int multiplier)
        {
            if (multiplier <= 0 || !IsMultiplierUnlocked(multiplier))
                return false;

            PointManager points = PointManager.Instance;
            return points != null && points.Get(PointType.PinballTicket) >= ShotCost(multiplier);
        }

        /// <summary>지금 보유한 티켓 수.</summary>
        public int TicketCount
        {
            get
            {
                PointManager points = PointManager.Instance;
                return points != null ? points.Get(PointType.PinballTicket) : 0;
            }
        }

        // ──────────────────────────────────────────────── 게이지 조회

        public int GetGauge(int tag)
        {
            return gauges.TryGetValue(tag, out int value) ? value : 0;
        }

        /// <summary>이 태그가 보상까지 몇 번 맞아야 하는가. 보상이 걸려 있지 않으면 0.</summary>
        public int GetRequiredHits(int tag)
        {
            PinballSpecialReward rule = Database.GetSpecial(tag);
            return rule != null ? rule.requiredHits : 0;
        }

        /// <summary>경품표에 걸린 특수 핀 규칙 전부. 화면이 게이지를 나열하는 데 쓴다.</summary>
        public IReadOnlyList<PinballSpecialReward> SpecialRewards => Database.specialRewards;

        /// <summary>그 태그의 규칙(보상·임계치). 경품표에 없으면 null.</summary>
        public PinballSpecialReward GetSpecialReward(int tag) => Database.GetSpecial(tag);

        /// <summary>경품표에 적힌 칸 수. 화면이 칸마다 경품을 적는 데 쓴다.</summary>
        public int SlotCount => Database.slotRewards != null ? Database.slotRewards.Count : 0;

        /// <summary>그 칸의 경품 목록. 없으면 null.</summary>
        public IReadOnlyList<PinballReward> GetSlotRewards(int slot)
        {
            PinballSlotReward slotReward = Database.GetSlot(slot);
            return slotReward != null ? slotReward.rewards : null;
        }

        // ──────────────────────────────────────────────── 한 발

        /// <summary>
        /// 공 한 개를 <b>발사 시점에 확정한다</b> — 칸을 뽑고, 티켓을 빼고, 칸 경품에 배율을
        /// 곱해 지급하고, 저장까지 여기서 끝낸다.
        ///
        /// <b>왜 발사 시점에 다 확정하나.</b> 이 판은 물리로 칸이 정해지지 않는다. 뽑은 칸으로
        /// 가는 궤적을 재생할 뿐이라(<c>Assets/Pinball/README.md</c>) 결과는 발사하는 순간
        /// 이미 정해져 있다. 그러면 지급도 그때 끝내는 것이 가장 안전하다 — 여러 발이 굴러가는
        /// 도중에 앱이 죽거나 화면을 나가도 <b>티켓과 경품이 갈라지지 않는다.</b>
        /// 재생은 그 뒤로 순수한 연출이 된다.
        ///
        /// <b>특수 핀은 여기 없다.</b> 적중 횟수는 재생해 봐야 나오므로
        /// <see cref="ResolveSpecialHit"/> 가 맞을 때마다 따로 반영한다.
        ///
        /// <paramref name="declaredProbability"/> 에는 <c>PinballBoard.declaredProbability</c> 를
        /// 그대로 넘긴다 — 매니저가 씬 오브젝트를 알지 않아야 헤드리스에서도 이 경로가 돈다.
        /// </summary>
        /// <param name="multiplier">이번 세션의 배율. 경품과 티켓 소모 양쪽에 곱해진다.</param>
        /// <param name="slot">뽑힌 칸. 화면이 재생에 그대로 넘긴다.</param>
        /// <returns>지급한 칸 경품. 실패하면 null 이고 재화는 하나도 건드리지 않는다.</returns>
        public IReadOnlyList<PointRewardEntry> TryShoot(
            IReadOnlyList<float> declaredProbability, int multiplier, out int slot)
        {
            slot = -1;

            if (!CanShoot(multiplier))
                return null;

            PointManager points = PointManager.Instance;
            if (points == null)
            {
                Debug.LogError("[핀볼] PointManager 가 없어 발사하지 못한다.");
                return null;
            }

            // 칸을 먼저 뽑는다. 못 뽑으면 재화를 건드리기 전에 돌아간다 —
            // ShopPurchaseManager.TryDrawGemBox 가 뽑기에서 쓰는 순서와 같다
            // ("재화를 먼저 빼고 후보가 없으면 그 재화가 조용히 사라진다").
            int drawn = PinballRules.DrawSlot(declaredProbability, UnityEngine.Random.value);
            if (drawn < 0)
            {
                Debug.LogError(
                    "[핀볼] 착지 칸을 뽑지 못했다. PinballBoard.declaredProbability 가 비었거나 " +
                    "전부 0 이다. Sim Lab 의 Edit Board 탭에서 확률표를 확인할 것.");
                return null;
            }

            if (!points.TrySpend(PointType.PinballTicket, ShotCost(multiplier), false))
            {
                // 위에서 CanShoot 으로 잔액을 봤으므로 정상 흐름에서는 여기 닿지 않는다.
                // 닿았다면 다른 경로가 같은 티켓을 쓰고 있다는 뜻이라 조용히 넘기지 않는다.
                Debug.LogError("[핀볼] 티켓 차감에 실패했다. 발사를 취소한다.");
                return null;
            }

            IReadOnlyList<PointRewardEntry> rewards = GetLandingRewards(drawn, multiplier);
            GrantWithoutSave(rewards);
            Save();     // 차감과 지급이 같은 파일 쓰기 한 번에 굳는다

            slot = drawn;
            return rewards;
        }

        /// <summary>
        /// 그 칸의 경품에 배율을 곱한 것. 발사 때는 지급할 목록으로, 착지 때는 연출에 띄울
        /// 목록으로 쓴다 — <b>착지 시점에는 조회일 뿐 지급하지 않는다.</b>
        /// </summary>
        public IReadOnlyList<PointRewardEntry> GetLandingRewards(int slot, int multiplier)
        {
            PinballSlotReward slotReward = Database.GetSlot(slot);
            if (slotReward == null)
            {
                Debug.LogError(
                    "[핀볼] " + slot + "번 칸의 경품이 경품표에 없다. 이 공은 빈손으로 끝난다. " +
                    "PinballRewardDatabase 의 칸 수가 판(PinballBoard.SlotCount)과 같은지 확인할 것.");
                return System.Array.Empty<PointRewardEntry>();
            }

            if (slotReward.rewards == null)
                return System.Array.Empty<PointRewardEntry>();

            var rewards = new List<PointRewardEntry>();
            for (int i = 0; i < slotReward.rewards.Count; i++)
            {
                PinballReward reward = slotReward.rewards[i];
                rewards.Add(new PointRewardEntry(
                    reward.pointType, PinballRules.ScaleAmount(reward.amount, multiplier)));
            }

            return rewards;
        }

        /// <summary>
        /// 특수 핀에 한 번 맞았다. 게이지를 올리고, 임계치에 닿으면 보상을 지급한다.
        ///
        /// <b>즉시 반영한다.</b> 게이지가 화면에서 실시간으로 차야 특수 핀을 맞힌 것이
        /// 보이고, 임계치 보상도 그 자리에서 터져야 인과가 보인다. 저장만
        /// <see cref="FlushSave"/> 로 미루므로 게이지와 보상이 갈라질 일은 없다.
        /// </summary>
        /// <param name="multiplier">이번 세션의 배율. 게이지가 이만큼씩 오른다.</param>
        /// <returns>임계치에 닿아 지급한 보상. 못 닿았으면 빈 목록.</returns>
        public IReadOnlyList<PointRewardEntry> ResolveSpecialHit(int tag, int multiplier)
        {
            PinballSpecialReward rule = Database.GetSpecial(tag);
            if (rule == null)
            {
                // 판에는 있는데 경품표에 없는 태그다. 게이지를 세 봐야 쓸 데가 없으므로
                // 올리지 않는다 — 나중에 보상을 붙이면 그때부터 0에서 시작한다.
                return System.Array.Empty<PointRewardEntry>();
            }

            int next = PinballRules.AdvanceGauge(
                GetGauge(tag), rule.requiredHits, Mathf.Max(1, multiplier), out int grantCount);

            SetGauge(tag, next);
            OnGaugeChanged?.Invoke();

            if (grantCount <= 0 || rule.rewards == null)
                return System.Array.Empty<PointRewardEntry>();

            // 배율이 크면 한 번 맞는 것으로 임계치를 여러 번 넘는다. 넘은 횟수만큼 준다 —
            // 1회로 깎으면 배율을 올릴수록 특수 핀이 손해가 된다.
            var rewards = new List<PointRewardEntry>();
            for (int i = 0; i < rule.rewards.Count; i++)
            {
                PinballReward reward = rule.rewards[i];
                rewards.Add(new PointRewardEntry(
                    reward.pointType, PinballRules.ScaleAmount(reward.amount, grantCount)));
            }

            GrantWithoutSave(rewards);
            return rewards;
        }

        /// <summary>
        /// 미뤄 둔 특수 핀 쪽 변화를 저장한다. 공이 전부 착지했을 때, 그리고 화면을 닫을 때 부른다.
        ///
        /// 칸 경품은 <see cref="TryShoot"/> 가 이미 저장했으므로 여기서 남는 것은 게이지와
        /// 임계치 보상뿐이다. 그것까지 적중할 때마다 저장하면 연사 중에 파일을 수십 번 쓴다.
        ///
        /// <b>여러 번 불러도 된다.</b> 저장은 현재 상태를 통째로 쓰는 것이라 멱등하다.
        /// </summary>
        public void FlushSave()
        {
            Save();
        }

        /// <summary>
        /// 저장 없이 지급한다. <c>PointRewardUtility.GrantRewards</c> 는 내부에서
        /// <c>SaveAll</c> 을 부르므로 여기서는 쓸 수 없다.
        /// </summary>
        private static void GrantWithoutSave(IReadOnlyList<PointRewardEntry> rewards)
        {
            PointManager points = PointManager.Instance;
            if (points == null || rewards == null)
                return;

            for (int i = 0; i < rewards.Count; i++)
                points.Add(rewards[i].PointType, rewards[i].Amount, false);
        }

        private void SetGauge(int tag, int value)
        {
            if (value <= 0)
                gauges.Remove(tag);
            else
                gauges[tag] = value;
        }

        private static void Save()
        {
            // ?. 가 필수다. 생성자 시점에는 SaveService 가 아직 null 이다.
            OJ.DI.GameContainer.SaveService?.SaveAll();
        }

        // ──────────────────────────────────────────────── 세이브

        public void WriteTo(OJ.Core.SaveState state)
        {
            if (state == null)
                return;

            state.Pinball.SpecialHitCounts.Clear();

            foreach (KeyValuePair<int, int> pair in gauges)
            {
                if (pair.Value <= 0)
                    continue;

                state.Pinball.SpecialHitCounts[TagKeyPrefix + pair.Key] = pair.Value;
            }
        }

        public void ReadFrom(OJ.Core.SaveState state)
        {
            if (state == null)
                return;

            gauges.Clear();

            foreach (KeyValuePair<string, int> pair in state.Pinball.SpecialHitCounts)
            {
                if (!TryParseTagKey(pair.Key, out int tag))
                {
                    Debug.LogWarning("[핀볼] 모르는 게이지 키라 버린다: " + pair.Key);
                    continue;
                }

                if (pair.Value > 0)
                    gauges[tag] = pair.Value;
            }

            // 여기서 OnGaugeChanged 를 쏘지 않는다. 로드는 아직 다른 매니저가 자기 몫을
            // 읽기 전이고, 구독자가 그 상태를 보면 절반만 로드된 게임을 그리게 된다.
        }

        private static bool TryParseTagKey(string key, out int tag)
        {
            tag = 0;
            if (string.IsNullOrEmpty(key) || !key.StartsWith(TagKeyPrefix, StringComparison.Ordinal))
                return false;

            return int.TryParse(key.Substring(TagKeyPrefix.Length), out tag);
        }
    }
}
