using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Analytics;
using OJ.DI;
using OJ.Hunting;
using OJ.Relic;
using OJ.UI;
using OJ.Utils;
using VContainer;
using VContainer.Unity;   // resolver.Instantiate 확장 메서드

namespace OJ.Dice
{
    public class UIDiceCraftPanelDialog : DialogBase
    {
        // 8.3b: 이 창은 씬에 상주하지 않는다. 카탈로그의 프리팹을 UIService 가 런타임에
        // 찍으므로 BattleScope 의 씬 순회로는 닿지 않고, 찍는 쪽의 리졸버가 채워 준다.
        //
        // OnLoad 가 하던 일은 Start 로 내렸다. DialogBase.Awake 가 Load -> OnLoad 를
        // 부르는데, 창구를 Awake 에서 읽지 않는 것이 이 트랜치의 규약이기 때문이다.
        //
        // 다만 "Awake 때는 아직 null" 이 이 경로의 이유는 아니다. resolver.Instantiate 는
        // 인스턴스를 꺼 둔 채 만들어 주입을 끝낸 뒤에 켜므로(VContainer 의
        // ObjectResolverUnityExtensions.Instantiate) 여기서는 Awake 가 주입 '뒤'에 돈다.
        // 그래도 Start 로 내려 둔다 — 생성 경로가 씬 순회로 바뀌면 그때는 Awake 가 먼저
        // 도니, 어느 경로로 태어나든 같게 두는 편이 안전하다.
        [Inject] private IBattleRefs battle;

        // 목록 항목(UIDiceCraftProgressItem)도 자기 몫의 창구를 [Inject] 로 받는다.
        // 평범한 Instantiate 로 찍으면 그 필드가 비어서 재고 표시가 0 으로 굳는다.
        [Inject] private IObjectResolver resolver;

        [Header("List")]
        [SerializeField] private Transform listRoot;
        [SerializeField] private UIDiceCraftProgressItem listItemPrefab;

        [Header("Detail Header")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private Image iconImage;

        [Header("Detail Materials")]
        [SerializeField] private Transform materialRoot;
        [SerializeField] private UIDiceCraftMaterialStatusItem materialItemPrefab;

        [Header("Detail Action")]
        [SerializeField] private Button summonButton;
        [SerializeField] private TMP_Text summonButtonText;

        private readonly List<UIDiceCraftProgressItem> items = new List<UIDiceCraftProgressItem>();
        private readonly List<UIDiceCraftMaterialStatusItem> materialItems = new List<UIDiceCraftMaterialStatusItem>();
        private readonly Dictionary<DiceType, UIDiceCraftProgressItem> itemMap = new Dictionary<DiceType, UIDiceCraftProgressItem>();
        private readonly List<UIDice> consumeBuffer = new List<UIDice>();
        private readonly List<DiceType> mythicTypes = new List<DiceType>();

        private DiceType selectedMythicType = DiceType.KingNormal;
        private bool hasSelection;

        /// <summary>
        /// 전투 매니저를 읽어도 되는가. 원본의 <c>DiceTypeStarManager.Instance != null</c>·
        /// <c>UIBoard.Instance != null</c> 가드를 대신한다.
        ///
        /// 이 창은 UIService 카탈로그에서 열리므로 <b>전투 밖(로비·타이틀)</b>에서도 살아 있을
        /// 수 있고, 그때 창구가 비어 있는 것은 사고가 아니라 정상이다.
        ///
        /// <c>battle</c> 자체는 검사하지 않는다. 이 창을 만드는 곳은 <c>UIService.Get</c>
        /// 하나뿐이고 그쪽은 루트 리졸버로 찍으므로 창구는 항상 채워진다. 여기서
        /// <c>battle</c> 이 null 이라면 그것은 정상 상태가 아니라 배선 사고이고, 가려 두면
        /// 재고가 조용히 0 으로 굳은 화면이 된다 — 크게 터지는 편이 낫다.
        /// 형제 파일(<c>UIDiceCraftProgressItem</c>·<c>UIDiceGrowthDetailPanel</c>·
        /// <c>UIDiceCraftProgressDialog</c>)도 같은 규약이다.
        ///
        /// IsActive 가 true 면 14개는 한꺼번에 채워졌으니 창구 <b>뒤</b>는 반드시 있다.
        /// </summary>
        private bool HasBattle => battle.IsActive;

        protected override void OnLoad()
        {
            // 목록 굽기(BuildIfNeeded)와 재고 구독은 Start 로 내렸다. 여기는 DialogBase.Awake
            // 안이라 resolver 도 battle 도 아직 null 이다.
            if (summonButton != null)
                summonButton.onClick.AddListener(OnClickSummon);
        }

        /// <summary>
        /// 주입이 끝난 뒤 처음 도는 자리. 원본이 <c>OnLoad</c> 에서 하던 두 가지를 여기서 한다.
        /// 둘 다 여러 번 불려도 같은 결과라, 첫 <c>Open</c> 이 <c>Start</c> 보다 먼저 와서
        /// <c>OnEnter</c> 가 같은 일을 해도 겹치지 않는다.
        /// </summary>
        private void Start()
        {
            BuildIfNeeded();
            SubscribeInventoryChanged();
        }

        protected override void OnUnload()
        {
            if (summonButton != null)
                summonButton.onClick.RemoveListener(OnClickSummon);

            if (HasBattle)
                battle.DiceStars.OnDiceInventoryChanged -= HandleDiceInventoryChanged;
        }

        protected override void OnEnter()
        {
            BuildIfNeeded();

            SubscribeInventoryChanged();

            RefreshAll();
        }

        /// <summary>
        /// 재고 변경 구독. 두 번 걸리지 않게 먼저 뗀다 — 원본 <c>OnEnter</c> 가 하던 그대로다.
        /// </summary>
        private void SubscribeInventoryChanged()
        {
            if (!HasBattle)
                return;

            battle.DiceStars.OnDiceInventoryChanged -= HandleDiceInventoryChanged;
            battle.DiceStars.OnDiceInventoryChanged += HandleDiceInventoryChanged;
        }

        public void Open(DiceType mythicType)
        {
            if (!hasSelection || mythicTypes.Contains(mythicType))
            {
                selectedMythicType = mythicType;
                hasSelection = true;
            }

            Enter();
        }

        public void RefreshAll()
        {
            for (int i = 0; i < items.Count; i++)
                items[i].Refresh();

            SortItemsByPercentDesc();

            if (!hasSelection || !mythicTypes.Contains(selectedMythicType))
                SelectBestItem();

            RefreshSelectionState();
            RefreshDetail();
        }

        private void BuildIfNeeded()
        {
            if (listItemPrefab == null || listRoot == null || items.Count > 0)
                return;

            mythicTypes.Clear();
            mythicTypes.AddRange(DiceMetaDataProvider.GetMythicTypes());

            for (int i = 0; i < mythicTypes.Count; i++)
            {
                DiceType type = mythicTypes[i];

                // 부모(listRoot)를 반드시 넘긴다. 부모 없는 오버로드는 스코프 아래 만들었다가
                // SetParent(null) 하는 분기를 타서 오브젝트가 엉뚱한 씬에 남는다.
                UIDiceCraftProgressItem item = resolver.Instantiate(listItemPrefab, listRoot);
                item.Bind(type, GetRecipeProgressPercent, OnClickListItem, false);
                items.Add(item);
                itemMap[type] = item;
            }
        }

        private void OnClickListItem(DiceType mythicType)
        {
            selectedMythicType = mythicType;
            hasSelection = true;
            RefreshSelectionState();
            RefreshDetail();
        }

        private void RefreshSelectionState()
        {
            foreach (var pair in itemMap)
                pair.Value.SetSelected(pair.Key == selectedMythicType);
        }

        private void SelectBestItem()
        {
            if (mythicTypes.Count == 0)
                return;

            DiceType best = mythicTypes[0];
            int bestPercent = GetRecipeProgressPercent(best);

            for (int i = 1; i < mythicTypes.Count; i++)
            {
                DiceType type = mythicTypes[i];
                int percent = GetRecipeProgressPercent(type);
                if (percent > bestPercent)
                {
                    best = type;
                    bestPercent = percent;
                }
            }

            selectedMythicType = best;
            hasSelection = true;
        }

        private void RefreshDetail()
        {
            if (!hasSelection)
                return;

            DiceMetaDataDatabase.DiceMeta meta = DiceMetaDataProvider.GetMeta(selectedMythicType);
            IReadOnlyList<DiceMetaDataDatabase.DiceRecipeMaterial> recipe = DiceMetaDataProvider.GetRecipeMaterials(selectedMythicType);

            if (nameText != null)
                nameText.SetText(meta != null && !string.IsNullOrEmpty(meta.displayName) ? meta.displayName : selectedMythicType.ToString());

            if (iconImage != null)
                iconImage.sprite = DiceMetaDataProvider.GetIcon(selectedMythicType);

            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(selectedMythicType) : 1;
            if (levelText != null)
                levelText.SetText("Lv.{0}", level);

            bool canCraft = HasBattle && battle.DiceStars.CanCraft(recipe);
            if (summonButton != null)
                summonButton.interactable = canCraft;
            if (summonButtonText != null)
                summonButtonText.SetText(canCraft ? "소환" : "재료 부족");

            RefreshMaterialSlots(recipe);
        }

        private void RefreshMaterialSlots(IReadOnlyList<DiceMetaDataDatabase.DiceRecipeMaterial> recipe)
        {
            int visibleCount = 0;
            if (recipe != null)
            {
                for (int i = 0; i < recipe.Count; i++)
                {
                    DiceMetaDataDatabase.DiceRecipeMaterial req = recipe[i];
                    int have = HasBattle
                        ? battle.DiceStars.GetTypeStarCount(req.diceType, req.star)
                        : 0;

                    for (int unit = 0; unit < req.count; unit++)
                    {
                        bool owned = have > unit;
                        UIDiceCraftMaterialStatusItem item = GetOrCreateMaterialItem(visibleCount);
                        if (item == null)
                            continue;
                        item.gameObject.SetActive(true);
                        item.Bind(req.diceType, req.star, owned);
                        visibleCount++;
                    }
                }
            }

            HideMaterialSlotsFrom(visibleCount);
        }

        private void HideMaterialSlotsFrom(int start)
        {
            for (int i = start; i < materialItems.Count; i++)
            {
                if (materialItems[i] != null)
                    materialItems[i].gameObject.SetActive(false);
            }
        }

        private UIDiceCraftMaterialStatusItem GetOrCreateMaterialItem(int index)
        {
            if (index < materialItems.Count && materialItems[index] != null)
                return materialItems[index];

            if (materialItemPrefab == null || materialRoot == null)
                return null;

            // 재료 칸은 지금 창구를 읽지 않지만 생성 경로를 갈라 두지 않는다.
            // 부모(materialRoot)를 넘기는 이유는 위 목록 항목과 같다.
            UIDiceCraftMaterialStatusItem created = resolver.Instantiate(materialItemPrefab, materialRoot);
            materialItems.Add(created);
            return created;
        }

        private void SortItemsByPercentDesc()
        {
            items.Sort((a, b) =>
            {
                int pa = GetRecipeProgressPercent(a.MythicType);
                int pb = GetRecipeProgressPercent(b.MythicType);
                return pb.CompareTo(pa);
            });

            for (int i = 0; i < items.Count; i++)
                items[i].transform.SetSiblingIndex(i);
        }

        private int GetRecipeProgressPercent(DiceType mythicType)
        {
            IReadOnlyList<DiceMetaDataDatabase.DiceRecipeMaterial> recipe = DiceMetaDataProvider.GetRecipeMaterials(mythicType);
            if (!HasBattle || recipe == null || recipe.Count == 0)
                return 0;

            return battle.DiceStars.GetRecipeProgressPercent(recipe);
        }

        private void OnClickSummon()
        {
            if (!hasSelection)
                return;

            if (!TryCraft(selectedMythicType))
                return;

            Exit();
        }

        private bool TryCraft(DiceType mythicType)
        {
            if (DiceMetaDataProvider.IsSummonable(mythicType))
                return false;

            // 원본은 UIBoard.Instance 와 DiceTypeStarManager.Instance 를 따로 검사했다.
            // 창구는 전투가 살아 있으면 14개를 한꺼번에 채우므로 그 둘의 null 검사가
            // IsActive 하나로 합쳐진다. 판(diceMap)이 아직 없는지는 별개라 그대로 남긴다.
            if (!HasBattle || battle.Board.diceMap == null)
                return false;

            IReadOnlyList<DiceMetaDataDatabase.DiceRecipeMaterial> recipe = DiceMetaDataProvider.GetRecipeMaterials(mythicType);
            if (recipe == null || recipe.Count == 0)
                return false;

            if (!battle.DiceStars.CanCraft(recipe))
                return false;

            consumeBuffer.Clear();
            UIDice[] map = battle.Board.diceMap;
            bool[] used = new bool[map.Length];

            for (int i = 0; i < recipe.Count; i++)
            {
                DiceMetaDataDatabase.DiceRecipeMaterial req = recipe[i];
                int found = 0;
                for (int idx = 0; idx < map.Length; idx++)
                {
                    if (used[idx])
                        continue;

                    UIDice dice = map[idx];
                    if (dice == null)
                        continue;

                    if (dice.Type != req.diceType || dice.Star != req.star)
                        continue;

                    used[idx] = true;
                    consumeBuffer.Add(dice);
                    found++;
                    if (found >= req.count)
                        break;
                }

                if (found < req.count)
                {
                    consumeBuffer.Clear();
                    return false;
                }
            }

            for (int i = 0; i < consumeBuffer.Count; i++)
            {
                UIDice dice = consumeBuffer[i];
                if (dice == null)
                    continue;

                if (dice.SlotIndex >= 0 && dice.SlotIndex < map.Length && map[dice.SlotIndex] == dice)
                    map[dice.SlotIndex] = null;

                battle.DiceStars.OnDiceRemove(dice.Type, dice.Star);
                Destroy(dice.gameObject);
            }

            int slotIndex = GetFirstEmptySlot();
            if (slotIndex < 0)
                return false;

            const int mythicStar = 1;
            battle.DiceStars.OnDiceSpawn(mythicType, mythicStar);
            battle.Board.SpawnDice(mythicType, mythicStar, slotIndex);
            RelicManager.Instance?.OnMythicCrafted(mythicType);

            // 원본의 GameManager.Instance != null 삼항은 위 IsActive 가드에 흡수됐다 —
            // 여기까지 왔다면 전투가 살아 있고, 그러면 Game 도 반드시 있다.
            RunHistoryManager.Instance?.RecordCraft(mythicType, battle.Game.CurrentWaveIndex);
            return true;
        }

        /// <summary>TryCraft 의 IsActive 가드를 통과한 뒤에만 불린다.</summary>
        private int GetFirstEmptySlot()
        {
            UIDice[] map = battle.Board.diceMap;
            for (int i = 0; i < map.Length; i++)
            {
                if (map[i] == null)
                    return i;
            }

            return -1;
        }

        private void HandleDiceInventoryChanged()
        {
            if (!isEnter)
                return;

            RefreshAll();
        }
    }
}
