using UnityEngine;
using System.Collections.Generic;
using VContainer;
using OJ.Analytics;
using OJ.DI;
using OJ.Hunting;
using OJ.Point;
using OJ.Relic;

namespace OJ.Dice
{
    public class MergeSystem : MonoBehaviour
    {
        public const int MaxStar = 4;

        // 8.3b: 배틀 스코프가 채운다. BattleScene 안에서는 null 이 아니다.
        // 단 스코프는 씬의 모든 Awake 뒤에 빌드되므로 Awake 에서는 아직 null 이다.
        [Inject] private IBattleRefs battle;

        public bool TryMerge(UIDice from, UIDice to)
        {
            if (battle.Game.inGameState == InGameState.Wave)
                return false;

            if (from == null || to == null)
                return false;

            if (!DiceMetaDataProvider.CanMerge(from.Type) || !DiceMetaDataProvider.CanMerge(to.Type))
                return false;

            // No merge beyond max star. Composite/multi-attribute dice is removed.
            if (to.Star >= MaxStar || from.Star >= MaxStar)
                return false;

            if (from.Type != to.Type || from.Star != to.Star)
                return false;

            DiceType fromType = from.Type;
            int fromStar = from.Star;
            DiceType toType = to.Type;
            int toStar = to.Star;
            DiceType mergedType = GetRandomMergedType(fromType);
            battle.DiceStars.OnDiceRemove(fromType, fromStar);
            battle.DiceStars.OnDiceRemove(toType, toStar);

            int newStar = Mathf.Min(MaxStar, to.Star + 1);

            to.Init(mergedType, newStar, to.SlotIndex);

            battle.DiceStars.OnDiceSpawn(mergedType, newStar);
            // RunHistoryManager 는 루트에 사는 별개 서비스라 ?. 를 그대로 둔다.
            // 반면 웨이브 번호의 GameManager null 검사는 지웠다 — 이 메서드는 맨 위에서
            // 이미 battle.Game.inGameState 를 그냥 읽고 있고, 씬 매니저는 여기서 null 일 수 없다.
            RunHistoryManager.Instance?.RecordMerge(
                fromType,
                fromStar,
                toType,
                toStar,
                mergedType,
                newStar,
                battle.Game.CurrentWaveIndex);

            Destroy(from.gameObject);
            RelicManager.Instance?.ApplyMergeInsurance();

            return true;
        }

        private DiceType GetRandomMergedType(DiceType fallbackType)
        {
            UIDiceSummonSystem summonSystem = battle.Summon;
            // summonSystem == null 검사는 지웠다. 씬 매니저라 BattleScene 안에서 null 이면
            // 그건 사고이고 조용히 fallbackType 으로 새면 안 된다.
            // deckTypes 쪽 검사는 참조가 아니라 덱 데이터에 대한 검사라 남긴다.
            if (summonSystem.deckTypes == null || summonSystem.deckTypes.Count == 0)
                return fallbackType;

            List<DiceType> candidates = new List<DiceType>(summonSystem.deckTypes.Count);
            for (int i = 0; i < summonSystem.deckTypes.Count; i++)
            {
                DiceType type = summonSystem.deckTypes[i];
                if (!DiceMetaDataProvider.IsSummonable(type))
                    continue;

                candidates.Add(type);
            }

            if (candidates.Count == 0)
                return fallbackType;

            return candidates[Random.Range(0, candidates.Count)];
        }

        // ── 진화 · 교환 ──────────────────────────────────────────────────────────────
        //
        // 조합식을 대신하는 두 동작이다. 규칙(배선·비용·후보)은 DiceEvolution 이 들고,
        // 여기는 <b>보드를 실제로 바꾸는 일</b>만 한다 — 재화를 깎고, 재고를 갱신하고,
        // 슬롯의 다이스를 다른 다이스로 갈아 끼운다.
        //
        // <b>왜 MergeSystem 인가.</b> 진화도 교환도 "보드의 다이스를 다른 다이스로
        // 바꾼다" 는 점에서 머지와 같은 종류의 일이고, 그러려면 IBattleRefs 창구가
        // 필요하다. 새 MonoBehaviour 를 세우면 씬에 오브젝트를 놓고 BattleScope 의
        // 등록 목록까지 늘려야 하는데, 얻는 것이 파일 분리뿐이다.

        /// <summary>
        /// 상위 단계로 올린다. 성공하면 재화를 깎고 같은 슬롯의 다이스를 바꿔 끼운다.
        ///
        /// <b>웨이브 중에는 막는다.</b> 머지와 같은 규약이다 — 발사 중에 슬롯의 타입이
        /// 바뀌면 <c>PlayerController</c> 가 들고 있는 쿨타임 상태와 화면이 어긋난다.
        /// </summary>
        public bool TryEvolve(UIDice dice)
        {
            if (dice == null || battle.Game.inGameState == InGameState.Wave)
                return false;

            if (!DiceEvolution.CanEvolve(dice.Type, dice.Star))
                return false;

            if (!DiceEvolution.TryGetEvolveTarget(dice.Type, out DiceType target))
                return false;

            int cost = DiceEvolution.GetEvolveCost(dice.Type);
            if (PointManager.Instance == null || !PointManager.Instance.TrySpend(PointType.BattleEnhanceStone, cost))
                return false;

            // 상위 단계는 성급 개념이 없다(showStarUI = false). 옛 조합도 결과를 항상
            // 1성으로 놓았고, 그 값이 곧 데미지의 pip 이자 쿨타임 배수의 지수라
            // 함부로 올리면 밸런스가 통째로 움직인다. 파워는 성급이 아니라
            // baseAttack 으로 올렸다 — DiceMetaDataDatabase 에셋 참조.
            const int evolvedStar = 1;
            ReplaceInPlace(dice, target, evolvedStar);

            if (DiceEvolution.GetTier(target) == DiceTier.King)
                RelicManager.Instance?.OnMythicCrafted(target);

            RunHistoryManager.Instance?.RecordCraft(target, battle.Game.CurrentWaveIndex);
            return true;
        }

        /// <summary>
        /// 같은 단계의 다른 다이스로 바꾼다. 성급은 그대로 간다 —
        /// 4성 노말을 교환하면 4성 파이어가 되지 1성으로 떨어지지 않는다.
        /// </summary>
        public bool TryExchange(UIDice dice)
        {
            if (dice == null || battle.Game.inGameState == InGameState.Wave)
                return false;

            if (!DiceEvolution.CanExchange(dice.Type))
                return false;

            if (!DiceEvolution.TryGetExchangeCandidates(dice.Type, exchangeBuffer))
                return false;

            int cost = DiceEvolution.GetExchangeCost(dice.Type);
            if (PointManager.Instance == null || !PointManager.Instance.TrySpend(PointType.BattleEnhanceStone, cost))
                return false;

            DiceType target = exchangeBuffer[Random.Range(0, exchangeBuffer.Count)];
            ReplaceInPlace(dice, target, dice.Star);
            return true;
        }

        // 교환 후보를 담는 재사용 버퍼. 매번 List 를 새로 만들지 않으려는 것이고,
        // 이 시스템은 씬에 하나뿐이라 재진입이 없다.
        private readonly List<DiceType> exchangeBuffer = new List<DiceType>(5);

        /// <summary>
        /// 슬롯은 그대로 두고 타입·성급만 바꿔 끼운다.
        ///
        /// <b>재고 갱신 순서가 중요하다.</b> <c>OnDiceRemove</c> → <c>OnDiceSpawn</c> 이어야
        /// <c>DiceTypeStarManager</c> 의 (타입,성급) 카운트가 맞고, 그 안에서
        /// <c>PlayerController.RefreshDice</c> 와 보드 UI 갱신이 같이 돈다.
        /// 새 오브젝트를 만들지 않으므로 슬롯 인덱스와 드래그 상태가 보존된다.
        /// </summary>
        private void ReplaceInPlace(UIDice dice, DiceType newType, int newStar)
        {
            battle.DiceStars.OnDiceRemove(dice.Type, dice.Star);
            dice.Init(newType, newStar, dice.SlotIndex);
            battle.DiceStars.OnDiceSpawn(newType, newStar);
        }
    }
}
