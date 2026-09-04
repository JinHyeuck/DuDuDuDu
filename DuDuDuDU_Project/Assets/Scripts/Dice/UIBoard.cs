using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using VContainer;
using VContainer.Unity;
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

        private void Start()
        {
            CreateBoard();
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

        public void SpawnDice(DiceType type, int star, int slotIndex)
        {
            if (diceMap[slotIndex] != null) return;

            // 이 한 줄이 이 트랜치의 핵심이다. UIDice 는 창구를 스물 몇 곳 쓰는데
            // 전부 런타임에 찍혀 나오므로, 여기서 리졸버를 안 태우면 그 필드들이
            // 통째로 null 이 된다. 슬롯 트랜스폼이 곧 부모다.
            var dice = resolver.Instantiate(dicePrefab, slots[slotIndex].transform);
            dice.Init(type, star, slotIndex);
            diceMap[slotIndex] = dice;
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
