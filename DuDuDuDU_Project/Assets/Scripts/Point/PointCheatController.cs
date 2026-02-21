using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    public class PointCheatController : MonoBehaviour
    {
        [SerializeField] private KeyCode toggleKey = KeyCode.BackQuote;
        [SerializeField] private bool startVisible = false;
        [Header("Window Size")]
        [SerializeField, Range(0.3f, 0.95f)] private float windowWidthRatio = 0.75f;
        [SerializeField, Range(0.3f, 0.95f)] private float windowHeightRatio = 0.67f;
        [Header("Mobile Trigger")]
        [SerializeField] private int tapsToToggle = 5;
        [SerializeField] private float multiTapWindow = 1.2f;
        [SerializeField] private float topLeftWidthRatio = 0.2f;
        [SerializeField] private float topLeftHeightRatio = 0.2f;

        private bool visible;
        private int selectedIndex;
        private string amountInput = "100";
        private string setValueInput = "0";
        private Rect windowRect = new Rect(20, 20, 360, 280);
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
            EnsureInputBlocker();
            SetInputBlockerVisible(visible);
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
            if (GUILayout.Button("Close", buttonStyle, GUILayout.Height(buttonHeight)))
                visible = false;

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
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
            float height = Mathf.Max(280f, Screen.height * Mathf.Clamp01(windowHeightRatio));
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
    }
}
