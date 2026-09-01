using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using VContainer;
using OJ.Analytics;
using OJ.DI;
using OJ.Hunting;
using OJ.Relic;

namespace OJ.Dice
{
    public class UIDiceSummonSystem : MonoBehaviour
    {
        // 8.3b: 배틀 스코프가 채운다. BattleScene 안에서는 null 이 아니다.
        // 단 Awake 시점에는 아직 비어 있다 — 스코프는 씬의 모든 Awake 뒤에 빌드된다.
        [Inject] private IBattleRefs battle;

        [Header("References")]
        public UIBoard board;
        public Button summonButton;
        public TMP_Text spText;

        [Header("SP Settings")]
        public int currentSP = 100;
        public int summonCost = 10;

        [SerializeField] private int summonsPerCostIncrease = 2;
        private int summonsSinceLastCostIncrease = 0;

        [Header("Dice Settings")]
        public List<DiceType> deckTypes = new()
        {
            DiceType.Normal,
            DiceType.Fire,
            DiceType.Ice,
            DiceType.Poison,
            DiceType.Thunder,
            DiceType.Tornado,
            DiceType.Stun,
            DiceType.ArmorBreak,
            DiceType.Wind,
            DiceType.Time
        };

        private void OnDestroy()
        {
            if (summonButton != null)
                summonButton.onClick.RemoveListener(OnSummonButton);

        }

        private void Start()
        {
            summonButton.onClick.AddListener(OnSummonButton);
            UpdateSPUI();
        }

        public void SetStageStartSp(int startSp, int startSummonCost = 10)
        {
            currentSP = Mathf.Max(0, startSp);
            summonCost = Mathf.Max(1, startSummonCost);
            summonsSinceLastCostIncrease = 0;
            UpdateSPUI();
        }

        private void UpdateSPUI()
        {
            spText.text = $"{currentSP} / {summonCost}";
            spText.color = currentSP >= summonCost ? Color.white : Color.red;
        }

        public void AddSP(int addsp)
        {
            if (addsp <= 0)
                return;

            currentSP += addsp;
            UpdateSPUI();
        }

        private void OnSummonButton()
        {
            if (battle.Game.inGameState == InGameState.Wave)
                return;

            if (currentSP < summonCost)
            {
                Debug.Log("SP 부족!");
                return;
            }

            int slotIndex = GetRandomEmptySlot();
            if (slotIndex == -1)
            {
                Debug.Log("보드가 꽉 찼음!");
                return;
            }

            List<DiceType> summonable = new List<DiceType>();
            for (int i = 0; i < deckTypes.Count; i++)
            {
                if (DiceMetaDataProvider.IsSummonable(deckTypes[i]))
                    summonable.Add(deckTypes[i]);
            }

            if (summonable.Count == 0)
                return;

            currentSP -= summonCost;
            int spentCost = summonCost;
            bool skipCostIncrease = RelicManager.Instance != null && RelicManager.Instance.ShouldSkipSummonCostIncrease();
            if (!skipCostIncrease)
            {
                summonsSinceLastCostIncrease++;
                if (summonsSinceLastCostIncrease >= Mathf.Max(1, summonsPerCostIncrease))
                {
                    summonCost++;
                    summonsSinceLastCostIncrease = 0;
                }
            }
            UpdateSPUI();

            DiceType type = summonable[Random.Range(0, summonable.Count)];
            int star = RelicManager.Instance != null ? RelicManager.Instance.RollSummonStar() : 1;
            battle.DiceStars.OnDiceSpawn(type, star);
            board.SpawnDice(type, star, slotIndex);
            RelicManager.Instance?.TrySpawnTwinDice(type);
            // GameManager 의 null 검사를 지운다. 소환 버튼은 BattleScene 에서만 눌리고
            // 그 안에서 battle.Game 이 null 이면 그것은 사고다 — 0 웨이브로 조용히
            // 기록해서 덮으면 안 된다. RelicManager·RunHistoryManager 는 루트 서비스라
            // 로비에서도 살아 있어야 하므로 ?. 를 그대로 둔다.
            RunHistoryManager.Instance?.RecordSummon(type, star, battle.Game.CurrentWaveIndex, spentCost, currentSP);
        }

        private int GetRandomEmptySlot()
        {
            List<int> emptySlots = new();
            int total = board.rows * board.cols;

            for (int i = 0; i < total; i++)
            {
                if (board.GetDice(i) == null)
                    emptySlots.Add(i);
            }

            if (emptySlots.Count == 0) return -1;
            return emptySlots[Random.Range(0, emptySlots.Count)];
        }
    }
}
