using UnityEngine;

namespace OJ
{
    public class DebugCornerDiceSummoner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private UIDiceSummonSystem summonSystem;

        [Header("Debug Summon")]
        [SerializeField] private DiceType targetDiceType = DiceType.KingThunder;
        [SerializeField] private int targetStar = 1;
        [SerializeField] private bool allowDuringWave = true;

        [Header("Top Right Trigger")]
        [SerializeField] private int requiredTapCount = 5;
        [SerializeField] private float tapResetSeconds = 1.5f;
        [SerializeField] private float minNormalizedX = 0.8f;
        [SerializeField] private float minNormalizedY = 0.8f;

        private int currentTapCount;
        private float lastTapTime = -999f;

        private void Reset()
        {
            summonSystem = GetComponent<UIDiceSummonSystem>();
        }

        private void Awake()
        {
            if (summonSystem == null)
                summonSystem = GetComponent<UIDiceSummonSystem>();
        }

        private void Update()
        {
            if (TryGetTapPosition(out Vector2 tapPosition) == false)
                return;

            if (IsInTopRightTriggerArea(tapPosition) == false)
                return;

            RegisterTap();
        }

        private bool TryGetTapPosition(out Vector2 tapPosition)
        {
            tapPosition = default;

            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    tapPosition = touch.position;
                    return true;
                }
            }

            if (Input.GetMouseButtonDown(0))
            {
                tapPosition = Input.mousePosition;
                return true;
            }

            return false;
        }

        private bool IsInTopRightTriggerArea(Vector2 tapPosition)
        {
            if (Screen.width <= 0 || Screen.height <= 0)
                return false;

            float normalizedX = tapPosition.x / Screen.width;
            float normalizedY = tapPosition.y / Screen.height;
            return normalizedX >= minNormalizedX && normalizedY >= minNormalizedY;
        }

        private void RegisterTap()
        {
            if (Time.unscaledTime - lastTapTime > tapResetSeconds)
                currentTapCount = 0;

            lastTapTime = Time.unscaledTime;
            currentTapCount++;

            if (currentTapCount < Mathf.Max(1, requiredTapCount))
                return;

            currentTapCount = 0;
            TrySummonDebugDice();
        }

        private void TrySummonDebugDice()
        {
            if (summonSystem == null || summonSystem.board == null)
            {
                Debug.LogWarning("[DebugCornerDiceSummoner] UIDiceSummonSystem or board is missing.");
                return;
            }

            if (allowDuringWave == false && GameManager.Instance != null && GameManager.Instance.inGameState == InGameState.Wave)
            {
                Debug.Log("[DebugCornerDiceSummoner] Wave 중에는 테스트 소환이 비활성화되어 있습니다.");
                return;
            }

            int emptySlotIndex = GetRandomEmptySlot();
            if (emptySlotIndex < 0)
            {
                Debug.Log("[DebugCornerDiceSummoner] 빈 슬롯이 없습니다.");
                return;
            }

            DiceType summonType = GetDebugSummonType();
            int summonStar = Mathf.Max(1, GetDebugSummonStar());
            DiceTypeStarManager.Instance?.OnDiceSpawn(summonType, summonStar);
            summonSystem.board.SpawnDice(summonType, summonStar, emptySlotIndex);
            RunHistoryManager.Instance?.RecordSummon(
                summonType,
                summonStar,
                GameManager.Instance != null ? GameManager.Instance.CurrentWaveIndex : 0,
                0,
                summonSystem.currentSP);

            Debug.Log($"[DebugCornerDiceSummoner] Summoned {summonType} x{summonStar} to slot {emptySlotIndex}.");
        }

        private DiceType GetDebugSummonType()
        {
            DiceType cheatType = PointCheatController.DebugSummonDiceType;
            if (System.Enum.IsDefined(typeof(DiceType), cheatType) && cheatType != DiceType.Max)
                return cheatType;

            return targetDiceType;
        }

        private int GetDebugSummonStar()
        {
            return Mathf.Max(1, PointCheatController.DebugSummonStar > 0 ? PointCheatController.DebugSummonStar : targetStar);
        }

        private int GetRandomEmptySlot()
        {
            int total = summonSystem.board.rows * summonSystem.board.cols;
            int emptyCount = 0;

            for (int i = 0; i < total; i++)
            {
                if (summonSystem.board.GetDice(i) == null)
                    emptyCount++;
            }

            if (emptyCount == 0)
                return -1;

            int pickIndex = Random.Range(0, emptyCount);
            for (int i = 0; i < total; i++)
            {
                if (summonSystem.board.GetDice(i) != null)
                    continue;

                if (pickIndex == 0)
                    return i;

                pickIndex--;
            }

            return -1;
        }
    }
}
