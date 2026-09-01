using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using VContainer;
using OJ.Analytics;
using OJ.DI;
using OJ.Dice;
using OJ.Equipment;
using OJ.Hunting;

namespace OJ.Point
{
    public class PointCheatController : MonoBehaviour
    {
        public static bool IsWallInvincible { get; private set; }
        public static DiceType DebugSummonDiceType { get; private set; } = DiceType.KingThunder;
        public static int DebugSummonStar { get; private set; } = 1;

        [SerializeField] private KeyCode toggleKey = KeyCode.BackQuote;
        [SerializeField] private bool startVisible = false;
        [Header("Window Size")]
        [SerializeField, Range(0.3f, 0.95f)] private float windowWidthRatio = 0.75f;
        [SerializeField, Range(0.3f, 0.95f)] private float windowHeightRatio = 0.78f;
        [Header("Mobile Trigger")]
        [SerializeField] private int tapsToToggle = 5;
        [SerializeField] private float multiTapWindow = 1.2f;
        [SerializeField] private float topLeftWidthRatio = 0.2f;
        [SerializeField] private float topLeftHeightRatio = 0.2f;

        private bool visible;
        private int selectedIndex;
        private string amountInput = "100";
        private string setValueInput = "0";
        private string monsterHpInput = "1000";
        private bool wallInvincible;
        private int debugDiceIndex;
        private int debugDiceStar = 1;
        private int selectedGemIndex;
        private string gemAmountInput = "1";
        private Vector2 scrollPos;
        private Rect windowRect = new Rect(20, 20, 360, 420);
        private int tapCount;
        private float firstTapTime;
        private Vector2 lastScreenSize;
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle buttonStyle;
        private GUIStyle textFieldStyle;
        private float lineGap = 10f;
        private float buttonHeight = 64f;
        private float textFieldHeight = 52f;
        private float titleHeight = 40f;
        private GameObject inputBlockerRoot;
        private bool lastVisibleState;

        /// <summary>
        /// BattleScene 매니저로 가는 창구. (8.3b)
        ///
        /// <b>로비·타이틀에서는 <c>IsActive</c> 가 false 인 것이 정상이다.</b> 이 오버레이는
        /// <c>DontDestroyOnLoad</c> 로 앱 내내 살아 있고 전투 밖에서도 열리므로, 전투 참조를
        /// 만지는 자리마다 <c>IsActive</c> 로 먼저 막는다 — 예전 <c>X.Instance == null</c>
        /// 검사가 하던 일과 같은 뜻이다.
        /// </summary>
        [Inject] private IBattleRefs battle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Object.FindFirstObjectByType<PointCheatController>() != null)
                return;

            var go = new GameObject(nameof(PointCheatController));
            Object.DontDestroyOnLoad(go);
            go.AddComponent<PointCheatController>();
#endif
        }

        private void Awake()
        {
            visible = startVisible;
            SyncDebugSummonSelection();
            EnsureInputBlocker();
            SetInputBlockerVisible(visible);
        }

        /// <summary>
        /// 창구를 받아 온다. (8.3b)
        ///
        /// <b>이 오브젝트는 컨테이너가 만들지 않는다.</b> 위 <see cref="Bootstrap"/> 가
        /// <c>new GameObject</c> + <c>AddComponent</c> 로 씬 밖에 세우는 개발용 오버레이라
        /// 어떤 스코프의 계층에도 속하지 않고, 그래서 <c>[Inject]</c> 필드를 아무도 채워
        /// 주지 않는다. 컨테이너 바깥 오브젝트에 쓰라고 있는 <c>IObjectResolver.Inject</c> 로
        /// 직접 채운다.
        ///
        /// <b>시점이 <c>Awake</c> 가 아니라 <c>Start</c> 인 이유.</b> 루트 컨테이너는
        /// <c>BeforeSceneLoad</c> 에 이미 서 있어 <c>Awake</c> 에서도 잡히긴 하지만,
        /// 창구를 채우는 배틀 스코프는 <c>sceneLoaded</c>(모든 <c>Awake</c> 뒤,
        /// 모든 <c>Start</c> 앞)에 만들어진다. 8.6 규칙대로 <c>Start</c> 에 두면
        /// 배선이 끝난 뒤라는 것이 한눈에 읽힌다.
        ///
        /// 창구 인스턴스는 루트가 들고 있는 하나뿐이고 씬이 바뀌어도 교체되지 않는다
        /// (배틀 스코프는 그 안을 채우고 비울 뿐이다). 그래서 여기서 한 번만 받으면 된다.
        /// </summary>
        private void Start()
        {
            GameContainer.Root.Container.Inject(this);
        }

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Input.GetKeyDown(toggleKey))
                visible = !visible;

            ProcessTopLeftMultiTap();

            if (lastVisibleState != visible)
                SetInputBlockerVisible(visible);
#endif
        }

        private void OnDestroy()
        {
            if (inputBlockerRoot != null)
                Destroy(inputBlockerRoot);
        }

        private void OnGUI()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            EnsureWindowRectForCurrentResolution();

            if (!visible)
                return;

            windowRect = GUI.Window(991237, windowRect, DrawWindow, "Point Cheat");
#endif
        }

        private void DrawWindow(int id)
        {
            EnsureGuiStyles();

            scrollPos = GUILayout.BeginScrollView(scrollPos);
            GUILayout.BeginVertical();

            GUILayout.Label("Point Type", labelStyle, GUILayout.Height(titleHeight));
            string[] names = GetPointTypeNames();
            selectedIndex = Mathf.Clamp(selectedIndex, 0, names.Length - 1);
            selectedIndex = GUILayout.SelectionGrid(selectedIndex, names, 3, buttonStyle, GUILayout.Height(buttonHeight * 2.2f));

            GUILayout.Space(lineGap);
            GUILayout.Label("Amount (+/-)", labelStyle, GUILayout.Height(titleHeight));
            amountInput = GUILayout.TextField(amountInput, 16, textFieldStyle, GUILayout.Height(textFieldHeight));

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Add", buttonStyle, GUILayout.Height(buttonHeight)))
                AddAmount();
            if (GUILayout.Button("Subtract", buttonStyle, GUILayout.Height(buttonHeight)))
                SubtractAmount();
            GUILayout.EndHorizontal();

            GUILayout.Space(lineGap);
            GUILayout.Label("Set Value", labelStyle, GUILayout.Height(titleHeight));
            setValueInput = GUILayout.TextField(setValueInput, 16, textFieldStyle, GUILayout.Height(textFieldHeight));
            if (GUILayout.Button("Set", buttonStyle, GUILayout.Height(buttonHeight)))
                SetValue();

            GUILayout.Space(lineGap);
            DrawDebugSummonSection();

            GUILayout.Space(lineGap);
            DrawMonsterHpSection();

            GUILayout.Space(lineGap);
            DrawGemCheatSection();

            GUILayout.Space(lineGap);
            bool nextWallInvincible = GUILayout.Toggle(wallInvincible, "Wall Invincible", buttonStyle, GUILayout.Height(buttonHeight));
            if (nextWallInvincible != wallInvincible)
            {
                wallInvincible = nextWallInvincible;
                IsWallInvincible = wallInvincible;
            }

            GUILayout.Space(lineGap);
            if (GUILayout.Button("Close", buttonStyle, GUILayout.Height(buttonHeight)))
                visible = false;

            GUILayout.EndVertical();
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        private void DrawDebugSummonSection()
        {
            GUILayout.Label("Debug Summon Dice", labelStyle, GUILayout.Height(titleHeight));
            string[] diceNames = GetSelectableDiceTypeNames();
            debugDiceIndex = Mathf.Clamp(debugDiceIndex, 0, diceNames.Length - 1);
            debugDiceIndex = GUILayout.SelectionGrid(debugDiceIndex, diceNames, 3, buttonStyle, GUILayout.Height(buttonHeight * 4.4f));
            DebugSummonDiceType = GetSelectableDiceTypes()[debugDiceIndex];

            GUILayout.Space(lineGap * 0.5f);
            GUILayout.Label("Debug Summon Star", labelStyle, GUILayout.Height(titleHeight));
            debugDiceStar = Mathf.Clamp(debugDiceStar, 1, 7);
            string[] starNames = { "1", "2", "3", "4", "5", "6", "7" };
            debugDiceStar = GUILayout.SelectionGrid(debugDiceStar - 1, starNames, 7, buttonStyle, GUILayout.Height(buttonHeight)) + 1;
            DebugSummonStar = debugDiceStar;

            GUILayout.Space(lineGap * 0.5f);
            if (GUILayout.Button("Summon Selected Dice", buttonStyle, GUILayout.Height(buttonHeight)))
                SummonSelectedDice();
        }

        private void DrawMonsterHpSection()
        {
            GUILayout.Label("Monster HP", labelStyle, GUILayout.Height(titleHeight));
            monsterHpInput = GUILayout.TextField(monsterHpInput, 16, textFieldStyle, GUILayout.Height(textFieldHeight));

            if (GUILayout.Button("Set Active Monsters HP", buttonStyle, GUILayout.Height(buttonHeight)))
                SetActiveMonstersHp();
        }

        private void DrawGemCheatSection()
        {
            if (EquipmentManager.Instance == null)
                return;

            GUILayout.Label("Gem Cheat", labelStyle, GUILayout.Height(titleHeight));
            var gems = EquipmentManager.Instance.GetGemDefinitions();
            if (gems == null || gems.Count == 0)
            {
                GUILayout.Label("No gems found in database.", labelStyle);
                return;
            }

            string[] gemNames = new string[gems.Count];
            for (int i = 0; i < gems.Count; i++)
                gemNames[i] = $"{gems[i].displayName} ({gems[i].rarity})";

            selectedGemIndex = Mathf.Clamp(selectedGemIndex, 0, gemNames.Length - 1);
            selectedGemIndex = GUILayout.SelectionGrid(selectedGemIndex, gemNames, 2, buttonStyle, GUILayout.Height(buttonHeight * (gems.Count / 2f + 1)));

            GUILayout.Space(lineGap * 0.5f);
            GUILayout.Label("Gem Amount", labelStyle, GUILayout.Height(titleHeight));
            gemAmountInput = GUILayout.TextField(gemAmountInput, 16, textFieldStyle, GUILayout.Height(textFieldHeight));

            if (GUILayout.Button("Add Selected Gem", buttonStyle, GUILayout.Height(buttonHeight)))
                AddSelectedGem();
        }

        private void AddSelectedGem()
        {
            if (EquipmentManager.Instance == null)
                return;

            var gems = EquipmentManager.Instance.GetGemDefinitions();
            if (selectedGemIndex < 0 || selectedGemIndex >= gems.Count)
                return;

            if (!int.TryParse(gemAmountInput, out int amount) || amount <= 0)
                return;

            EquipmentManager.Instance.AddGem(gems[selectedGemIndex].gemId, amount);
            Debug.Log($"[PointCheat] Added {amount} of {gems[selectedGemIndex].displayName}.");
        }

        private void ProcessTopLeftMultiTap()
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (IsInTopLeftZone(Input.mousePosition))
                    RegisterTap();
            }

            if (Input.touchCount <= 0)
                return;

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase != TouchPhase.Began)
                    continue;

                if (!IsInTopLeftZone(touch.position))
                    continue;

                RegisterTap();
            }
        }

        private bool IsInTopLeftZone(Vector2 screenPos)
        {
            float width = Screen.width * Mathf.Clamp01(topLeftWidthRatio);
            float height = Screen.height * Mathf.Clamp01(topLeftHeightRatio);

            bool inX = screenPos.x >= 0f && screenPos.x <= width;
            bool inY = screenPos.y >= (Screen.height - height) && screenPos.y <= Screen.height;
            return inX && inY;
        }

        private void RegisterTap()
        {
            if (tapCount == 0 || Time.unscaledTime - firstTapTime > multiTapWindow)
            {
                tapCount = 1;
                firstTapTime = Time.unscaledTime;
                return;
            }

            tapCount++;
            if (tapCount >= tapsToToggle)
            {
                visible = !visible;
                tapCount = 0;
                firstTapTime = 0f;
            }
        }

        private void EnsureWindowRectForCurrentResolution()
        {
            Vector2 currentScreen = new Vector2(Screen.width, Screen.height);
            if (lastScreenSize == currentScreen)
                return;

            lastScreenSize = currentScreen;

            float width = Mathf.Max(360f, Screen.width * Mathf.Clamp01(windowWidthRatio));
            float height = Mathf.Max(420f, Screen.height * Mathf.Clamp01(windowHeightRatio));
            float x = Mathf.Max(10f, (Screen.width - width) * 0.5f);
            float y = Mathf.Max(10f, (Screen.height - height) * 0.5f);

            windowRect = new Rect(x, y, width, height);
        }

        private void EnsureGuiStyles()
        {
            float scale = Mathf.Max(1f, Mathf.Min(Screen.width / 1080f, Screen.height / 1920f));
            int fontSize = Mathf.RoundToInt(24f * scale);

            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.window);
                titleStyle.fontStyle = FontStyle.Bold;
            }

            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label);
                labelStyle.alignment = TextAnchor.MiddleLeft;
                labelStyle.fontStyle = FontStyle.Bold;
            }

            if (buttonStyle == null)
            {
                buttonStyle = new GUIStyle(GUI.skin.button);
                buttonStyle.alignment = TextAnchor.MiddleCenter;
                buttonStyle.fontStyle = FontStyle.Bold;
            }

            if (textFieldStyle == null)
            {
                textFieldStyle = new GUIStyle(GUI.skin.textField);
                textFieldStyle.alignment = TextAnchor.MiddleLeft;
                textFieldStyle.padding = new RectOffset(14, 14, 10, 10);
            }

            labelStyle.fontSize = fontSize;
            buttonStyle.fontSize = fontSize;
            textFieldStyle.fontSize = fontSize;

            lineGap = 12f * scale;
            buttonHeight = Mathf.Max(64f, 64f * scale);
            textFieldHeight = Mathf.Max(52f, 52f * scale);
            titleHeight = Mathf.Max(36f, 36f * scale);
        }

        private void EnsureInputBlocker()
        {
            if (inputBlockerRoot != null)
                return;

            inputBlockerRoot = new GameObject("PointCheatInputBlocker");
            DontDestroyOnLoad(inputBlockerRoot);

            Canvas canvas = inputBlockerRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue - 1;

            inputBlockerRoot.AddComponent<GraphicRaycaster>();

            var image = inputBlockerRoot.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;

            RectTransform rect = inputBlockerRoot.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void SetInputBlockerVisible(bool isVisible)
        {
            EnsureInputBlocker();
            if (inputBlockerRoot != null)
                inputBlockerRoot.SetActive(isVisible);

            lastVisibleState = isVisible;
        }

        private void AddAmount()
        {
            if (PointManager.Instance == null)
                return;

            if (!int.TryParse(amountInput, out int amount) || amount <= 0)
                return;

            PointManager.Instance.Add(GetSelectedPointType(), amount);
        }

        private void SubtractAmount()
        {
            if (PointManager.Instance == null)
                return;

            if (!int.TryParse(amountInput, out int amount) || amount <= 0)
                return;

            PointType type = GetSelectedPointType();
            int current = PointManager.Instance.Get(type);
            PointManager.Instance.Set(type, Mathf.Max(0, current - amount));
        }

        private void SetValue()
        {
            if (PointManager.Instance == null)
                return;

            if (!int.TryParse(setValueInput, out int value))
                return;

            PointManager.Instance.Set(GetSelectedPointType(), Mathf.Max(0, value));
        }

        private PointType GetSelectedPointType()
        {
            PointType[] values = GetSelectablePointTypes();
            if (values.Length == 0)
                return PointType.Gold;

            selectedIndex = Mathf.Clamp(selectedIndex, 0, values.Length - 1);
            return values[selectedIndex];
        }

        private static PointType[] GetSelectablePointTypes()
        {
            var all = (PointType[])System.Enum.GetValues(typeof(PointType));
            int count = 0;

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == PointType.Max)
                    continue;
                count++;
            }

            PointType[] result = new PointType[count];
            int idx = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == PointType.Max)
                    continue;
                result[idx++] = all[i];
            }

            return result;
        }

        private static string[] GetPointTypeNames()
        {
            PointType[] types = GetSelectablePointTypes();
            string[] names = new string[types.Length];
            for (int i = 0; i < types.Length; i++)
                names[i] = types[i].ToString();
            return names;
        }

        private void SyncDebugSummonSelection()
        {
            DiceType[] diceTypes = GetSelectableDiceTypes();
            debugDiceIndex = 0;
            for (int i = 0; i < diceTypes.Length; i++)
            {
                if (diceTypes[i] != DebugSummonDiceType)
                    continue;

                debugDiceIndex = i;
                break;
            }

            debugDiceStar = Mathf.Clamp(DebugSummonStar, 1, 7);
        }

        private static DiceType[] GetSelectableDiceTypes()
        {
            var all = (DiceType[])System.Enum.GetValues(typeof(DiceType));
            int count = 0;

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == DiceType.Max)
                    continue;
                count++;
            }

            DiceType[] result = new DiceType[count];
            int idx = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == DiceType.Max)
                    continue;
                result[idx++] = all[i];
            }

            return result;
        }

        private static string[] GetSelectableDiceTypeNames()
        {
            DiceType[] diceTypes = GetSelectableDiceTypes();
            string[] names = new string[diceTypes.Length];
            for (int i = 0; i < diceTypes.Length; i++)
                names[i] = diceTypes[i].ToString();
            return names;
        }

        private void SummonSelectedDice()
        {
            // 보드도 주사위 매니저도 전투 씬에만 있다. 예전에는 둘을 따로 null 검사했지만
            // 창구는 <b>한 번에 전부 채워지거나 전부 비거나</b> 둘 중 하나이므로
            // (BattleContext.Bind 참조) IsActive 한 줄이 그 검사와 정확히 같은 뜻이다.
            if (!battle.IsActive)
                return;

            int slotIndex = GetRandomEmptySlot();
            if (slotIndex < 0)
            {
                Debug.Log("[PointCheat] 빈 슬롯이 없습니다.");
                return;
            }

            DiceType summonType = DebugSummonDiceType;
            int summonStar = Mathf.Clamp(DebugSummonStar, 1, 7);

            battle.DiceStars.OnDiceSpawn(summonType, summonStar);
            battle.Board.SpawnDice(summonType, summonStar, slotIndex);

            // RunHistoryManager 는 전투 매니저가 아니라 루트 서비스라 창구 밖이다 — 그대로 둔다.
            // 웨이브·SP 는 위 IsActive 가 통과한 이상 반드시 살아 있으므로 삼항이 필요 없다.
            RunHistoryManager.Instance?.RecordSummon(
                summonType,
                summonStar,
                battle.Game.CurrentWaveIndex,
                0,
                battle.Summon.currentSP);
        }

        private int GetRandomEmptySlot()
        {
            // 부르는 쪽이 이미 IsActive 를 확인했지만, 이 메서드만 따로 읽어도 뜻이 서게
            // 남겨 둔다. diceMap 검사는 창구와 무관하다 — 보드가 아직 격자를 만들기 전인
            // 순간을 거르는 것이고, 그건 전투 중에도 성립할 수 있는 상태다.
            if (!battle.IsActive || battle.Board.diceMap == null)
                return -1;

            int total = battle.Board.rows * battle.Board.cols;
            int emptyCount = 0;

            for (int i = 0; i < total; i++)
            {
                if (battle.Board.GetDice(i) == null)
                    emptyCount++;
            }

            if (emptyCount == 0)
                return -1;

            int pickIndex = Random.Range(0, emptyCount);
            for (int i = 0; i < total; i++)
            {
                if (battle.Board.GetDice(i) != null)
                    continue;

                if (pickIndex == 0)
                    return i;

                pickIndex--;
            }

            return -1;
        }

        private void SetActiveMonstersHp()
        {
            // 몬스터 매니저는 전투 씬에만 있다. 로비에서 눌러도 조용히 아무 일도 없는 것이
            // 예전 MonsterManager.Instance == null 검사와 같은 동작이다.
            if (!battle.IsActive)
                return;

            if (!int.TryParse(monsterHpInput, out int hp))
                return;

            hp = Mathf.Max(1, hp);

            for (int i = 0; i < battle.Monsters.activeMonsters.Count; i++)
            {
                Monster monster = battle.Monsters.activeMonsters[i];
                if (monster == null || monster.gameObject.activeInHierarchy == false)
                    continue;

                monster.SetHp(hp);
            }

            Debug.Log($"[PointCheat] Set active monsters HP to {hp}.");
        }
    }
}
