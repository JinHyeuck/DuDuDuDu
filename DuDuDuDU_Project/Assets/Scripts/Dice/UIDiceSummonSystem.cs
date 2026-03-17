using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

namespace OJ
{
    public class UIDiceSummonSystem : MonoBehaviour
    {
        public static UIDiceSummonSystem Instance;

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

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (summonButton != null)
                summonButton.onClick.RemoveListener(OnSummonButton);

            if (Instance == this)
                Instance = null;
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
            if (GameManager.Instance.inGameState == InGameState.Wave)
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
            summonsSinceLastCostIncrease++;
            if (summonsSinceLastCostIncrease >= Mathf.Max(1, summonsPerCostIncrease))
            {
                summonCost++;
                summonsSinceLastCostIncrease = 0;
            }
            UpdateSPUI();

            DiceType type = summonable[Random.Range(0, summonable.Count)];
            int star = 1;
            DiceTypeStarManager.Instance.OnDiceSpawn(type, star);
            board.SpawnDice(type, star, slotIndex);
            RunHistoryManager.Instance?.RecordSummon(type, star, GameManager.Instance != null ? GameManager.Instance.CurrentWaveIndex : 0, spentCost, currentSP);
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
