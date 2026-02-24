using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace OJ
{
    public class UIMythicDiceCraftPanel : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            UIMythicDiceCraftPanel existing = Object.FindFirstObjectByType<UIMythicDiceCraftPanel>();
            if (existing != null)
            {
                existing.enabled = true;
                existing.showPanel = true;
                return;
            }

            var go = new GameObject(nameof(UIMythicDiceCraftPanel));
            go.AddComponent<UIMythicDiceCraftPanel>();
            Object.DontDestroyOnLoad(go);
        }

        [SerializeField] private bool showPanel = true;
        [SerializeField] private KeyCode toggleKey = KeyCode.F7;
        [SerializeField] private Vector2 panelPos = new Vector2(16f, 120f);
        [SerializeField] private float panelWidth = 420f;
        [SerializeField] private float panelHeight = 420f;

        private Vector2 scroll;
        private readonly Dictionary<(DiceType type, int star), int> materialCounts = new Dictionary<(DiceType type, int star), int>();
        private readonly List<UIDice> consumeBuffer = new List<UIDice>();
        private readonly StringBuilder lineBuilder = new StringBuilder(128);

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
                showPanel = !showPanel;
        }

        private void OnGUI()
        {
            if (!showPanel)
                return;

            InGameState state = GameManager.Instance != null ? GameManager.Instance.inGameState : InGameState.None;

            if (GameManager.Instance == null || state != InGameState.Setting || UIBoard.Instance == null || UIBoard.Instance.diceMap == null)
            {
                DrawStatus(state);
                return;
            }

            BuildMaterialCounts();

            GUILayout.BeginArea(new Rect(panelPos.x, panelPos.y, panelWidth, panelHeight), GUI.skin.window);
            GUILayout.Label("Mythic Dice Craft");
            scroll = GUILayout.BeginScrollView(scroll);

            List<DiceType> mythics = DiceMetaDataProvider.GetMythicTypes();
            for (int i = 0; i < mythics.Count; i++)
            {
                DrawMythicItem(mythics[i]);
                GUILayout.Space(6f);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawStatus(InGameState state)
        {
            GUILayout.BeginArea(new Rect(panelPos.x, panelPos.y, panelWidth, 86f), GUI.skin.window);
            GUILayout.Label("Mythic Dice Craft");
            GUILayout.Label($"State: {state} (Need: Setting)");
            GUILayout.Label($"Board Ready: {(UIBoard.Instance != null && UIBoard.Instance.diceMap != null ? "Yes" : "No")} | Toggle: {toggleKey}");
            GUILayout.EndArea();
        }

        private void DrawMythicItem(DiceType mythicType)
        {
            var meta = DiceMetaDataProvider.GetMeta(mythicType);
            string title = meta != null && !string.IsNullOrEmpty(meta.displayName) ? meta.displayName : mythicType.ToString();
            GUILayout.Label(title);

            var recipe = DiceMetaDataProvider.GetRecipeMaterials(mythicType);
            bool canCraft = recipe != null && recipe.Count > 0;

            if (recipe != null)
            {
                for (int i = 0; i < recipe.Count; i++)
                {
                    var req = recipe[i];
                    int have = GetMaterialCount(req.diceType, req.star);
                    bool ok = have >= req.count;
                    if (!ok)
                        canCraft = false;

                    lineBuilder.Clear();
                    lineBuilder.Append(req.star);
                    lineBuilder.Append("★ ");
                    lineBuilder.Append(req.diceType);
                    lineBuilder.Append(" x");
                    lineBuilder.Append(req.count);
                    lineBuilder.Append(" (");
                    lineBuilder.Append(have);
                    lineBuilder.Append("/");
                    lineBuilder.Append(req.count);
                    lineBuilder.Append(")");
                    GUILayout.Label((ok ? "[OK] " : "[NO] ") + lineBuilder);
                }
            }

            bool pressed = GUILayout.Button(canCraft ? "Craft" : "Need Materials");
            if (pressed && canCraft)
                TryCraft(mythicType);
        }

        private void BuildMaterialCounts()
        {
            materialCounts.Clear();
            UIDice[] map = UIBoard.Instance.diceMap;
            for (int i = 0; i < map.Length; i++)
            {
                UIDice dice = map[i];
                if (dice == null)
                    continue;

                var key = (dice.Type, dice.Star);
                materialCounts.TryGetValue(key, out int count);
                materialCounts[key] = count + 1;
            }
        }

        private int GetMaterialCount(DiceType type, int star)
        {
            materialCounts.TryGetValue((type, star), out int count);
            return count;
        }

        private bool TryCraft(DiceType mythicType)
        {
            if (DiceMetaDataProvider.IsSummonable(mythicType))
                return false;

            var recipe = DiceMetaDataProvider.GetRecipeMaterials(mythicType);
            if (recipe == null || recipe.Count == 0)
                return false;

            consumeBuffer.Clear();
            UIDice[] map = UIBoard.Instance.diceMap;
            bool[] used = new bool[map.Length];

            for (int i = 0; i < recipe.Count; i++)
            {
                var req = recipe[i];
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

                DiceTypeStarManager.Instance.OnDiceRemove(dice.Type, dice.Star);
                Destroy(dice.gameObject);
            }

            int slotIndex = GetFirstEmptySlot();
            if (slotIndex < 0)
                return false;

            int mythicStar = 1;
            DiceTypeStarManager.Instance.OnDiceSpawn(mythicType, mythicStar);
            UIBoard.Instance.SpawnDice(mythicType, mythicStar, slotIndex);
            return true;
        }

        private int GetFirstEmptySlot()
        {
            UIDice[] map = UIBoard.Instance.diceMap;
            for (int i = 0; i < map.Length; i++)
            {
                if (map[i] == null)
                    return i;
            }

            return -1;
        }
    }
}
