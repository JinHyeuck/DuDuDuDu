using UnityEngine;
using System.Collections.Generic;
using VContainer;
using OJ.Analytics;
using OJ.DI;
using OJ.Hunting;
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
    }
}
