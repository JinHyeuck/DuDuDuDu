using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using VContainer;
using VContainer.Unity;
using OJ.Core;
using OJ.DI;
using OJ.Relic;

namespace OJ.Dice
{
    public class UIBoard : MonoBehaviour
    {
        // 8.3b: 배틀 스코프가 채운다. BattleScene 안에서는 null 이 아니다.
        // 단 스코프는 씬의 모든 Awake 뒤에 빌드되므로 Awake 에서 쓰면 안 된다.
        [Inject] private IBattleRefs battle;

        // 8.3b: 이 보드는 슬롯과 주사위를 런타임에 찍는다. 스코프의 씬 순회는 이미
        // 끝난 뒤라 그냥 Instantiate 하면 새로 태어난 UIDice 의 [Inject] 가 빈 채로 남는다.
        // 리졸버를 통해 찍어야 그 자리에서 주입이 붙는다. 창구와 마찬가지로 Awake 에서는
        // 아직 null 이므로 CreateBoard 는 Start 에 머물러야 한다.
        [Inject] private IObjectResolver resolver;

        [Header("Board Settings")]
        public GridLayoutGroup grid;
        public GameObject slotPrefab;
        public UIDice dicePrefab;
        public int rows = 6;
        public int cols = 4;

        private UIDice selectedDice;
        private List<GameObject> slots = new();
        public UIDice[] diceMap;

        public int ShotIndex = 0;

        // ── 자리 해금 테스트 모드 ───────────────────────────────────
        // 꺼져 있으면 아래 값은 전부 쓰이지 않고 IsSlotUnlocked 는 언제나 true 다.
        private bool slotLockActive;
        private int freeSlotCount;
        private int purchasedSlotCount;
        private bool[] slotUnlocked;
        private GameObject[] slotLockOverlays;
        private TMP_Text[] slotLockLabels;

        /// <summary>자리 해금 모드가 이 판에 걸려 있는가.</summary>
        public bool IsSlotLockActive => slotLockActive;

        /// <summary>다음 자리를 열 때 드는 SP.</summary>
        public int NextSlotUnlockCost => SlotUnlockRules.Cost(purchasedSlotCount);

        private void Start()
        {
            CreateBoard();
        }

        private void OnDestroy()
        {
            if (battle != null && battle.BattlePoints != null)
                battle.BattlePoints.OnBattlePointChanged -= OnBattlePointChanged;
        }

        private void CreateBoard()
        {
            int total = rows * cols;
            diceMap = new UIDice[total];

            for (int i = 0; i < total; i++)
            {
                // 부모를 반드시 넘긴다. 부모 없는 오버로드는 스코프 아래 찍었다가
                // SetParent(null) 로 떼어내는 분기를 타서, 슬롯이 grid 밑이 아니라
                // 씬 루트에 떨어지고 GridLayoutGroup 배치를 통째로 놓친다.
                var slot = resolver.Instantiate(slotPrefab, grid.transform);
                slots.Add(slot);
            }

            SetUpSlotLocks(total);

            // 시작 주사위(유물 '선제 배치')는 여기서 놓지 않는다.
            //
            // 놓으려면 두 가지가 이미 끝나 있어야 한다 — 보드가 만들어졌을 것,
            // 그리고 <c>RelicManager.BeginStageRun()</c> 이 지난 판의 적용 표시를
            // 지웠을 것. 뒤쪽은 <c>GameManager.Start</c> 에서 일어나는데
            // <b>Start 끼리의 순서는 정해져 있지 않다.</b>
            //
            // 여기서 부르면 UIBoard 가 먼저 도는 판에서 <b>지난 판의 표시가 아직 살아
            // 있어 조용히 건너뛴다</b> — 유물을 끼고도 시작 주사위가 안 나온다.
            // 그래서 두 Start 가 모두 끝난 다음 프레임에 도는
            // <c>GameManager.CoApplyStageStartRelics</c> 한 곳으로 모았다.
        }

        /// <summary>
        /// 칸에 다이스를 놓는다.
        ///
        /// <paramref name="playSummonFx"/> 는 <b>"방금 생겼다" 와 "원래 있던 것을 다시
        /// 놓는다" 를 가른다.</b> 되돌리기(<c>WaveRewindManager.RestoreBoard</c>)와 탑 편성
        /// 배치는 보드를 통째로 다시 깔기 때문에, 켜 두면 스물네 칸이 한꺼번에 튀어올라
        /// 소환으로 보인다. 소환·선물·쌍둥이·선제배치는 전부 기본값으로 둔다.
        /// </summary>
        public void SpawnDice(DiceType type, int star, int slotIndex, bool playSummonFx = true)
        {
            if (diceMap[slotIndex] != null) return;

            if (!IsSlotUnlocked(slotIndex))
            {
                Debug.LogWarning("[보드] 잠긴 칸에는 다이스를 놓을 수 없다: " + slotIndex);
                return;
            }

            // 이 한 줄이 이 트랜치의 핵심이다. UIDice 는 창구를 스물 몇 곳 쓰는데
            // 전부 런타임에 찍혀 나오므로, 여기서 리졸버를 안 태우면 그 필드들이
            // 통째로 null 이 된다. 슬롯 트랜스폼이 곧 부모다.
            var dice = resolver.Instantiate(dicePrefab, slots[slotIndex].transform);
            dice.Init(type, star, slotIndex);
            diceMap[slotIndex] = dice;

            if (playSummonFx)
                DiceFx.PlaySummon(dice.transform);
        }

        public void ClearDice(int slotIndex)
        {
            if (diceMap[slotIndex] != null)
            {
                Destroy(diceMap[slotIndex].gameObject);
                diceMap[slotIndex] = null;
            }
        }

        public void OnDiceClicked(UIDice dice)
        {
            if (selectedDice == null)
            {
                selectedDice = dice;
                Highlight(dice, true);
            }
            else
            {
                if (selectedDice == dice)
                {
                    Highlight(dice, false);
                    selectedDice = null;
                    return;
                }

                // 8.3b: MergeSystem 도 같은 BattleScene 매니저다. 클릭은 Start 이후에만
                // 들어오므로 창구는 이미 채워져 있다 — ?. 로 감싸면 병합 실패가 조용해진다.
                battle.Merge.TryMerge(selectedDice, dice);
                Highlight(selectedDice, false);
                selectedDice = null;
            }
        }

        /// <summary>
        /// 카탈로그에서 꺼내 띄운다. (10.4)
        ///
        /// 예전에는 씬 인스턴스를 <c>[SerializeField]</c> 로 직접 가리켰고, 그것이 비면
        /// <c>Resources.FindObjectsOfTypeAll</c> 로 훑어 씬 소속만 골라 때웠다.
        /// 두 경로 다 <b>실패가 조용하다</b> — 참조가 <c>None</c> 이어도, 탐색이 빈손이어도
        /// 아무 로그 없이 상세창만 안 열린다. 게다가 그 탐색은 씬에 인스턴스가 상주해야
        /// 성립하는 방식이라, 팝업을 필요할 때 만드는 구조와 양립하지 않는다.
        ///
        /// <c>Show</c> 가 아니라 <c>Get</c> 인 것은 <c>Open</c> 이 주사위를 받아 안에서
        /// <c>Enter</c> 까지 부르기 때문이다 — <c>Show</c> 로 열면 대상 없이 한 번 뜬다.
        /// </summary>
        public void OpenBattleDiceDetail(UIDice dice)
        {
            if (dice == null)
                return;

            GameContainer.UI?.Get<UIBattleDiceDetailPanel>()?.Open(dice);
        }

        private void Highlight(UIDice dice, bool on)
        {
        }

        public UIDice GetDice(int slotIndex) => diceMap[slotIndex];

        /// <summary>칸의 사각형. 범위 밖이거나 아직 칸을 안 만들었으면 null. 튜토리얼이 그 칸만 열어 둔다.</summary>
        public RectTransform GetSlotRect(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= slots.Count || slots[slotIndex] == null)
                return null;

            return slots[slotIndex].transform as RectTransform;
        }

        public int GetSlotIndexFromObject(GameObject hitObj)
        {
            if (hitObj == null)
                return -1;

            Transform hitTransform = hitObj.transform;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] == null)
                    continue;

                Transform slotTransform = slots[i].transform;
                if (hitTransform == slotTransform || hitTransform.IsChildOf(slotTransform))
                    return i;
            }

            return -1;
        }

        public bool TryMoveDiceToSlot(UIDice dice, int toSlotIndex)
        {
            if (dice == null || diceMap == null)
                return false;

            if (toSlotIndex < 0 || toSlotIndex >= diceMap.Length)
                return false;

            int fromSlotIndex = dice.SlotIndex;
            if (fromSlotIndex < 0 || fromSlotIndex >= diceMap.Length)
                return false;

            if (diceMap[fromSlotIndex] != dice)
                return false;

            if (!IsSlotUnlocked(toSlotIndex))
                return false;

            if (fromSlotIndex == toSlotIndex)
            {
                dice.transform.SetParent(slots[toSlotIndex].transform);
                dice.transform.localPosition = Vector3.zero;
                dice.transform.localScale = Vector3.one;
                return true;
            }

            if (diceMap[toSlotIndex] != null)
                return false;

            diceMap[fromSlotIndex] = null;
            diceMap[toSlotIndex] = dice;
            dice.SetSlotIndex(toSlotIndex);

            dice.transform.SetParent(slots[toSlotIndex].transform);
            dice.transform.localPosition = Vector3.zero;
            dice.transform.localScale = Vector3.one;
            return true;
        }

        // ── 자리 해금 ──────────────────────────────────────────────

        /// <summary>
        /// 칸이 열려 있는가. <b>모드가 꺼져 있으면 언제나 true</b> 라서, 소환·유물·치트가
        /// 모드를 따로 알 필요 없이 이 함수만 거치면 된다.
        /// </summary>
        public bool IsSlotUnlocked(int slotIndex)
        {
            if (!slotLockActive)
                return true;

            if (slotIndex < 0 || slotIndex >= slotUnlocked.Length)
                return false;

            return slotIndex < freeSlotCount || slotUnlocked[slotIndex];
        }

        /// <summary>
        /// 잠긴 칸을 SP 로 연다. 비용은 어느 칸이냐가 아니라 <b>몇 번째 구매냐</b>로 정해진다
        /// (50, 55, 60 ...). 관리 단계(연속 웨이브 중에는 웨이브에서도)에서만 된다.
        /// </summary>
        public bool TryUnlockSlot(int slotIndex)
        {
            if (!slotLockActive || IsSlotUnlocked(slotIndex))
                return false;

            if (battle.Game == null || !battle.Game.CanManageBoard)
                return false;

            int cost = NextSlotUnlockCost;
            if (battle.BattlePoints == null
                || !battle.BattlePoints.TrySpend(BattlePointType.SummonPoint, cost))
            {
                Debug.Log("SP 부족! 자리 해금에 " + cost + " 필요");
                return false;
            }

            slotUnlocked[slotIndex] = true;
            purchasedSlotCount++;

            if (slotLockOverlays[slotIndex] != null)
                Destroy(slotLockOverlays[slotIndex]);
            slotLockOverlays[slotIndex] = null;
            slotLockLabels[slotIndex] = null;

            RefreshSlotLockLabels();
            return true;
        }

        private void SetUpSlotLocks(int total)
        {
            // 자리구매는 정식 규칙이다. 탑(편성이 곧 배치다)만 뺀다.
            // 튜토리얼도 켜 둔다 — 자리 구매를 가르친다. 대본 소환은 무료인 첫 줄에만 놓는다(TutorialStage).
            slotLockActive = !battle.Tower.IsActive;

            purchasedSlotCount = 0;
            if (!slotLockActive)
                return;

            // 첫 줄은 열려 있다. 한 줄의 칸 수는 그리드 설정(고정 열 수)이 정본이다.
            freeSlotCount = grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount
                ? grid.constraintCount
                : cols;

            slotUnlocked = new bool[total];
            slotLockOverlays = new GameObject[total];
            slotLockLabels = new TMP_Text[total];

            TMP_FontAsset font = battle.Summon != null && battle.Summon.spText != null
                ? battle.Summon.spText.font
                : null;

            for (int i = freeSlotCount; i < total; i++)
                CreateLockOverlay(i, font);

            RefreshSlotLockLabels();

            battle.BattlePoints.OnBattlePointChanged -= OnBattlePointChanged;
            battle.BattlePoints.OnBattlePointChanged += OnBattlePointChanged;
        }

        private void CreateLockOverlay(int slotIndex, TMP_FontAsset font)
        {
            var overlay = new GameObject("SlotLock", typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)overlay.transform;
            rect.SetParent(slots[slotIndex].transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = overlay.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.72f);

            var labelObject = new GameObject("Cost", typeof(RectTransform), typeof(TextMeshProUGUI));
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var label = labelObject.GetComponent<TextMeshProUGUI>();
            if (font != null)
                label.font = font;
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 30f;
            label.raycastTarget = false;

            int captured = slotIndex;
            overlay.GetComponent<Button>().onClick.AddListener(() => TryUnlockSlot(captured));

            slotLockOverlays[slotIndex] = overlay;
            slotLockLabels[slotIndex] = label;
        }

        private void OnBattlePointChanged(BattlePointType type, int value)
        {
            if (type == BattlePointType.SummonPoint)
                RefreshSlotLockLabels();
        }

        /// <summary>잠긴 칸마다 다음 해금 비용을 적는다. SP 가 모자라면 붉게 보인다.</summary>
        private void RefreshSlotLockLabels()
        {
            if (!slotLockActive)
                return;

            int cost = NextSlotUnlockCost;
            int sp = battle.BattlePoints != null ? battle.BattlePoints.Get(BattlePointType.SummonPoint) : 0;
            Color color = sp >= cost ? Color.white : new Color(1f, 0.35f, 0.35f);

            for (int i = 0; i < slotLockLabels.Length; i++)
            {
                if (slotLockLabels[i] == null)
                    continue;

                slotLockLabels[i].text = cost + "\nSP";
                slotLockLabels[i].color = color;
            }
        }

        public bool TrySwapDice(UIDice a, UIDice b)
        {
            if (a == null || b == null || a == b || diceMap == null)
                return false;

            int aIndex = a.SlotIndex;
            int bIndex = b.SlotIndex;

            if (aIndex < 0 || aIndex >= diceMap.Length || bIndex < 0 || bIndex >= diceMap.Length)
                return false;

            if (diceMap[aIndex] != a || diceMap[bIndex] != b)
                return false;

            diceMap[aIndex] = b;
            diceMap[bIndex] = a;

            a.SetSlotIndex(bIndex);
            b.SetSlotIndex(aIndex);

            a.transform.SetParent(slots[bIndex].transform);
            a.transform.localPosition = Vector3.zero;
            a.transform.localScale = Vector3.one;

            b.transform.SetParent(slots[aIndex].transform);
            b.transform.localPosition = Vector3.zero;
            b.transform.localScale = Vector3.one;

            return true;
        }
    }
}
