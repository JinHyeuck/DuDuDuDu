using System.Collections.Generic;
using UnityEngine;
using Pinball;

namespace OJ.Pinball
{
    /// <summary>
    /// 보상판의 <c>SeedTable</c> 에서 궤적을 뽑아 <see cref="BonusDiceManager"/> 에 넘긴다.
    /// <see cref="IBonusShotSource"/> 의 유일한 실제 구현이다.
    ///
    /// <b>마스크를 먼저 고르는 이유는 확률을 맞추기 위해서다.</b> <c>SpecialHitSolver</c> 가
    /// 판에 적힌 목표 적중 확률에 맞는 패턴을 뽑아 주고, <c>SeedBag</c> 이 그 패턴을 가진
    /// 시드만 돌려준다 — <c>PinballPlayback.PlayForSlot</c> 이 쓰는 순서와 같다.
    /// <b>적중 횟수는 그 마스크로 세지 않는다</b>(<see cref="CountHits"/> 참고).
    ///
    /// <b>슬롯은 보상과 무관하다.</b> 보상판의 아래 칸은 떨어지는 공을 받는 받이일 뿐이고,
    /// 여기서 슬롯을 고르는 것은 <i>어느 궤적 풀에서 뽑을지</i>를 정하는 일에 지나지 않는다.
    /// </summary>
    public sealed class BonusShotSource : IBonusShotSource
    {
        private readonly SeedTable table;
        private readonly PinballBoard board;
        private readonly SeedBag bag;
        private readonly SpecialHitSolver solver;

        /// <summary>
        /// 뽑은 시드가 어느 핀을 <b>몇 번</b> 맞는지 세려고 둔다.
        ///
        /// <c>SeedTable</c> 이 들고 있는 것은 "어느 핀을 맞았나"(<c>hitMask</c>)와
        /// "전부 몇 번"(<c>specialHits</c>)뿐이라 <b>핀별 횟수가 없다.</b> 그런데 화면은
        /// <c>PinballPlayback.OnSpecialHit</c> 으로 맞을 때마다 한 번씩 세므로, 마스크로
        /// 어림하면 <b>보이는 횟수와 셈한 횟수가 갈라진다</b> — 게이지가 중간에 멈추거나
        /// 끝에서 훌쩍 뛴다. 그래서 여기서 재생과 <i>같은 궤적</i>을 미리 돌려 정확히 센다.
        /// </summary>
        private readonly PinballSimulator sim;
        private readonly List<HitEvent> hitBuffer = new List<HitEvent>(64);
        private readonly List<int> tags;
        private readonly System.Random rand = new System.Random();

        /// <summary>비트 인덱스 → 태그. <c>PinballBoard.SpecialTags()</c> 의 순서 그대로다.</summary>
        public IReadOnlyList<int> Tags => tags;

        public BonusShotSource(SeedTable table, PinballBoard board)
        {
            this.table = table;
            this.board = board;

            if (table == null || board == null)
            {
                tags = new List<int>();
                return;
            }

            bag = new SeedBag(table);
            tags = board.SpecialTags();
            sim = new PinballSimulator(board.CreateSnapshot());

            // Natural 정책이면 적중 확률을 제어하지 않는다 — 판이 만드는 대로 둔다.
            if (board.specialHitPolicy != SpecialHitPolicy.Natural)
                solver = new SpecialHitSolver(table, board);
        }

        /// <summary>
        /// 쓸 수 있는 상태인가. 배선이 빠졌거나 테이블이 판과 어긋나면 false —
        /// 그 상태로 쏘면 재현 불일치로 공이 안 나가므로 화면이 미리 막아야 한다.
        /// </summary>
        public bool IsUsable => table != null && board != null && bag != null && !table.IsStale;

        public bool TryDraw(int ballCount, List<BonusShot> shots, Dictionary<int, int> hitsByTag)
        {
            if (!IsUsable || shots == null || hitsByTag == null || ballCount <= 0)
                return false;

            for (int i = 0; i < ballCount; i++)
            {
                int slot = PickSlot();
                if (slot < 0)
                    continue;

                int wanted = solver != null && solver.BitCount > 0 ? solver.PickMask(slot, rand) : -1;

                // 그 패턴의 궤적이 없으면 패턴 지정을 포기한다. 확률이 조금 흔들릴 뿐,
                // 적중은 아래에서 실제 궤적으로 세므로 셈이 틀어지지는 않는다.
                if (!bag.TryTake(slot, wanted, out uint seed) && !bag.TryTake(slot, -1, out seed))
                    continue;   // 이 슬롯 풀이 비었다. 다음 발로 넘어간다

                shots.Add(new BonusShot(slot, seed));
                CountHits(seed, hitsByTag);
            }

            return shots.Count > 0;
        }

        /// <summary>
        /// 어느 궤적 풀에서 뽑을지. 판이 적어 둔 <c>declaredProbability</c> 를 그대로 쓰되,
        /// 그것이 비었거나 뽑은 칸에 시드가 없으면 <b>시드가 있는 칸 중에서 고른다</b> —
        /// 여기서 -1 로 돌아서면 그 공이 통째로 사라진다.
        /// </summary>
        private int PickSlot()
        {
            int drawn = PinballRules.DrawSlot(board.declaredProbability, (float)rand.NextDouble());
            if (drawn >= 0 && drawn < table.SlotCount && table.pools[drawn].Count > 0)
                return drawn;

            int usable = 0;
            for (int i = 0; i < table.SlotCount; i++)
            {
                if (table.pools[i].Count > 0)
                    usable++;
            }

            if (usable == 0)
                return -1;

            int pick = rand.Next(usable);
            for (int i = 0; i < table.SlotCount; i++)
            {
                if (table.pools[i].Count == 0)
                    continue;

                if (pick == 0)
                    return i;

                pick--;
            }

            return -1;
        }

        /// <summary>
        /// 그 시드의 궤적을 돌려 태그별 적중 수를 더한다.
        ///
        /// <b>판정 조건을 <c>PinballPlayback</c> 과 똑같이 맞춰 둔다</b> —
        /// <c>pegIndex</c> 가 유효하고 <c>specialTag</c> 가 0 이 아닌 충돌만 센다.
        /// 한 글자라도 어긋나면 화면에 보이는 횟수와 지급의 근거가 갈라진다.
        /// </summary>
        private void CountHits(uint seed, Dictionary<int, int> hitsByTag)
        {
            hitBuffer.Clear();
            sim.Run(seed, null, hitBuffer);

            for (int i = 0; i < hitBuffer.Count; i++)
            {
                int pegIndex = hitBuffer[i].pegIndex;
                if (pegIndex < 0 || pegIndex >= board.pegs.Length)
                    continue;

                int tag = board.pegs[pegIndex].specialTag;
                if (tag == 0)
                    continue;

                hitsByTag.TryGetValue(tag, out int n);
                hitsByTag[tag] = n + 1;
            }
        }
    }
}
