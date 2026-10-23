using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using OJ.Hunting;

namespace OJ.EditorTools
{
    /// <summary>
    /// 로비·전투 씬의 UI 를 세로로 긴 화면(22:9)에서 안 깨지게 앵커를 옮긴다.
    ///
    /// <b>무엇이 깨졌나.</b> 캔버스는 가로(1080) 기준으로 스케일되므로 화면이 길수록 세로 단위가 늘어난다
    /// (22:9 면 1080x2640). 그런데 씬의 요소 대부분이 <b>가운데(0.5)</b> 에 앵커돼 있어, 늘어난 높이의 절반만큼
    /// 위·아래로 갈라져 나간다 — 아래 메뉴는 바닥에서 떠오르고 위 배너는 가운데로 내려오며, 전투의
    /// 위·아래 그라데이션 띠는 화면 한가운데에 거뭇하게 걸린다. 위쪽 요소는 위에, 아래쪽 요소는 아래에 붙인다.
    ///
    /// <b>1080x1920 에서는 아무것도 달라지지 않는다.</b> 요소의 피벗이 1920 기준 캔버스에서 놓인 자리를
    /// 그대로 두고 앵커만 바꾸므로(<see cref="ReAnchorY"/>), 앵커가 어디든 같은 점에 놓인다.
    ///
    /// <b>두 번 눌러도 안전하다.</b> 이미 옮긴 요소는 같은 값으로 다시 계산돼 변하지 않는다.
    /// 씬은 YAML 을 만지지 않고 API 로만 고쳐 저장한다(AGENTS 절대 규칙 3).
    /// </summary>
    public static class TallScreenLayoutBaker
    {
        private const string BattleScenePath = "Assets/Scenes/BattleScene.unity";
        private const string LobbyScenePath = "Assets/Scenes/LobbyScene.unity";
        private const string EquipmentPagePath = "Assets/Prefab/Refactory/LobbyScene/UIEquipmentPage.prefab";
        private const float ReferenceHeight = 1920f;

        private const float Top = 1f;
        private const float Middle = 0.5f;
        private const float Bottom = 0f;

        private readonly struct Frame
        {
            public readonly float Bottom;
            public readonly float Height;

            public Frame(float bottom, float height)
            {
                Bottom = bottom;
                Height = height;
            }
        }

        [MenuItem("OJ/개발/긴 화면/씬 배치 굽기 (전투·로비)")]
        private static void Bake()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogError("[긴 화면] 저장 안 된 씬이 열려 있다. 먼저 저장하거나 되돌리고 다시 누른다: " + SceneManager.GetSceneAt(i).name);
                    return;
                }
            }

            string activePath = SceneManager.GetActiveScene().path;
            var log = new StringBuilder("[긴 화면] 앵커를 위/아래로 옮긴다 (1920 기준 자리는 그대로)\n");

            BakeBattle(log);
            BakeLobby(log);

            if (!string.IsNullOrEmpty(activePath) && SceneManager.GetActiveScene().path != activePath)
                EditorSceneManager.OpenScene(activePath, OpenSceneMode.Single);

            Debug.Log(log.ToString());
        }

        [MenuItem("OJ/개발/긴 화면/장비 페이지 굽기")]
        private static void BakeEquipmentPage()
        {
            var log = new StringBuilder("[긴 화면] 장비 페이지\n");
            GameObject root = PrefabUtility.LoadPrefabContents(EquipmentPagePath);
            try
            {
                ApplyEquipmentPage(root.transform, log);
                PrefabUtility.SaveAsPrefabAsset(root, EquipmentPagePath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            Debug.Log(log.ToString());
        }

        /// <summary>
        /// 장비 페이지는 위 1050 이 캐릭터 무대, 아래 870 이 장착 효과 목록이다. 무대(배경 그림·발판)는 위에 붙이고
        /// 목록 영역은 무대 아래에서 바닥까지 늘린다 — 화면이 길어지면 늘어난 높이는 목록이 가져간다.
        /// 목록 아래 깔린 시안 그림(Layout)이 무대와 목록 사이로 비쳐 보이는 것도 이렇게 막는다.
        /// </summary>
        internal static void ApplyEquipmentPage(Transform page, StringBuilder log)
        {
            foreach (string path in new[] { "View/BG/Image", "View/BG/Image (1)" })
            {
                var rect = page.Find(path) as RectTransform;
                if (rect == null)
                    log.AppendLine("  !! " + path + " 없음");
                else
                    ReAnchorY(rect, Top, PivotY(rect), log, path);
            }

            var inventory = page.Find("View/ElementGroup/InventoryArea") as RectTransform;
            if (inventory == null)
            {
                log.AppendLine("  !! View/ElementGroup/InventoryArea 없음");
                return;
            }

            string before = Describe(inventory);
            Frame frame = FrameOf(inventory);
            float topGap = ReferenceHeight - (frame.Bottom + frame.Height);
            inventory.anchorMin = new Vector2(0f, 0f);
            inventory.anchorMax = new Vector2(1f, 1f);
            inventory.offsetMin = new Vector2(0f, frame.Bottom);
            inventory.offsetMax = new Vector2(0f, -topGap);
            log.AppendLine("  InventoryArea (무대 아래에서 바닥까지)\n    전: " + before + "\n    후: " + Describe(inventory));
        }

        private static void BakeBattle(StringBuilder log)
        {
            Scene scene = EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Single);
            log.AppendLine("\n" + scene.name);

            Transform canvas = FindCanvas(scene, "UI/Canvas");
            if (canvas == null)
            {
                log.AppendLine("  !! UI/Canvas 를 못 찾았다 — 전투 씬을 건너뛴다");
                return;
            }

            // 위 그라데이션·몬스터 처치 바는 위에, 나머지(아래 그라데이션·보드·우물·도우미·강화)는 아래에 붙인다.
            var moves = new (string name, float anchorY)[]
            {
                ("Image (1)", Top),
                ("MonsterKill", Top),
                ("Image (3)", Bottom),
                ("Well", Bottom),
                ("Helper", Bottom),
                ("UIBoard", Bottom),
                ("Upgrade", Bottom),
            };

            foreach ((string name, float anchorY) in moves)
                ReAnchorNamed(canvas, name, anchorY, log);

            AddViewFit(scene, log);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void BakeLobby(StringBuilder log)
        {
            Scene scene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
            log.AppendLine("\n" + scene.name);

            Transform canvas = FindCanvas(scene, "Canvas");
            Transform layout = canvas != null ? canvas.Find("LobbyLayoutController") : null;
            if (layout == null)
            {
                log.AppendLine("  !! Canvas/LobbyLayoutController 를 못 찾았다 — 로비 씬을 건너뛴다");
                return;
            }

            // 아래 메뉴는 바닥에, 위 메뉴(Menu 를 품은 띠)와 전투력 표시는 위에 붙인다.
            ReAnchorNamed(layout, "Bottom", Bottom, log);
            ReAnchorNamed(layout, "Middle", Top, log);
            ReAnchorNamed(layout, "Content/UICombatPowerDisplay", Top, log);

            // Stage 는 높이 419 짜리 가운데 상자라 그 안의 요소가 전부 가운데에 딸려 다닌다. 화면 높이 전체로 늘려
            // 상자 자체는 의미를 잃게 하고, 안의 요소를 위(배너) 또는 아래(보상 줄·소탕·입장)에 직접 붙인다.
            Transform stage = layout.Find("Content/Stage");
            if (stage == null)
            {
                log.AppendLine("  !! Content/Stage 를 못 찾았다");
            }
            else
            {
                var children = new (string name, float anchorY)[]
                {
                    ("TitleBG", Top),
                    ("StageRewardBtn", Bottom),
                    ("Sweep", Bottom),
                    ("GoBattleScene", Bottom),
                    ("UIMeatFestivalLobbyButton", Middle),
                };

                // 부모를 바꾸기 전에 자식의 피벗 높이를 모두 재 둔다 — 바꾼 뒤에는 옛 자리를 계산할 수 없다.
                var pivots = new Dictionary<string, float>();
                var befores = new Dictionary<string, string>();
                foreach ((string name, float _) in children)
                {
                    var child = stage.Find(name) as RectTransform;
                    if (child == null)
                        continue;

                    pivots[name] = PivotY(child);
                    befores[name] = Describe(child);
                }

                StretchStage((RectTransform)stage, log);

                foreach ((string name, float anchorY) in children)
                {
                    var child = stage.Find(name) as RectTransform;
                    if (child == null || !pivots.TryGetValue(name, out float pivotY))
                    {
                        log.AppendLine("  !! Stage/" + name + " 없음");
                        continue;
                    }

                    ReAnchorY(child, anchorY, pivotY, log, "Content/Stage/" + name, befores[name]);
                }
            }

            AddLobbyViewFit(scene, log);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // 로비 월드(캠프파이어·캐릭터)가 화면 가운데에 놓이므로 중심은 두고 시야만 가로 기준으로 맞춘다.
        private static void AddLobbyViewFit(Scene scene, StringBuilder log)
        {
            Camera world = null;
            foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (camera.gameObject.scene == scene && camera.name == "Main Camera")
                    world = camera;
            }

            if (world == null)
            {
                log.AppendLine("  !! Main Camera 를 못 찾아 BattleViewFit 을 붙이지 못했다");
                return;
            }

            var fit = world.GetComponent<BattleViewFit>();
            if (fit == null)
                fit = Undo.AddComponent<BattleViewFit>(world.gameObject);

            var so = new SerializedObject(fit);
            SerializedProperty cameras = so.FindProperty("cameras");
            cameras.arraySize = 1;
            cameras.GetArrayElementAtIndex(0).objectReferenceValue = world;
            so.FindProperty("background").objectReferenceValue = null;
            so.FindProperty("keepCentered").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            log.AppendLine("  Main Camera 에 BattleViewFit (cameras: Main Camera / keepCentered)");
        }

        private static void StretchStage(RectTransform stage, StringBuilder log)
        {
            string before = Describe(stage);
            stage.anchorMin = new Vector2(0.5f, 0f);
            stage.anchorMax = new Vector2(0.5f, 1f);
            stage.sizeDelta = new Vector2(1080f, 0f);
            stage.anchoredPosition = Vector2.zero;
            log.AppendLine("  Content/Stage  (세로 전체로 늘림)\n    전: " + before + "\n    후: " + Describe(stage));
        }

        private static void AddViewFit(Scene scene, StringBuilder log)
        {
            Camera world = null;
            Camera ui = null;
            foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (camera.gameObject.scene != scene)
                    continue;
                if (camera.name == "Main Camera")
                    world = camera;
                else if (camera.name == "UICamera")
                    ui = camera;
            }

            GameObject background = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "StageBackground")
                    background = root;
            }

            if (world == null || ui == null || background == null || background.GetComponent<SpriteRenderer>() == null)
            {
                log.AppendLine("  !! Main Camera / UICamera / StageBackground 중 하나를 못 찾아 BattleViewFit 을 붙이지 못했다");
                return;
            }

            var fit = world.GetComponent<BattleViewFit>();
            if (fit == null)
                fit = Undo.AddComponent<BattleViewFit>(world.gameObject);

            var so = new SerializedObject(fit);
            SerializedProperty cameras = so.FindProperty("cameras");
            cameras.arraySize = 2;
            cameras.GetArrayElementAtIndex(0).objectReferenceValue = world;
            cameras.GetArrayElementAtIndex(1).objectReferenceValue = ui;
            so.FindProperty("background").objectReferenceValue = background.GetComponent<SpriteRenderer>();
            so.ApplyModifiedPropertiesWithoutUndo();

            log.AppendLine("  Main Camera 에 BattleViewFit (cameras: Main Camera, UICamera / background: StageBackground)");
        }

        private static Transform FindCanvas(Scene scene, string path)
        {
            string[] parts = path.Split('/');
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != parts[0])
                    continue;

                Transform current = root.transform;
                for (int i = 1; i < parts.Length && current != null; i++)
                    current = current.Find(parts[i]);

                return current;
            }

            return null;
        }

        private static void ReAnchorNamed(Transform parent, string path, float anchorY, StringBuilder log)
        {
            var rect = parent.Find(path) as RectTransform;
            if (rect == null)
            {
                log.AppendLine("  !! " + path + " 없음");
                return;
            }

            ReAnchorY(rect, anchorY, PivotY(rect), log, path);
        }

        /// <summary>
        /// 세로 앵커를 <paramref name="anchorY"/>(0 아래, 0.5 가운데, 1 위)로 옮기되, 피벗이 1920 기준 캔버스에서
        /// 놓인 높이(<paramref name="pivotY"/>, 아래에서 잰 값)는 그대로 둔다. 세로가 한 점에 앵커된 요소만 다룬다.
        /// </summary>
        private static void ReAnchorY(RectTransform rect, float anchorY, float pivotY, StringBuilder log, string label, string before = null)
        {
            if (!Mathf.Approximately(rect.anchorMin.y, rect.anchorMax.y))
            {
                log.AppendLine("  !! " + label + " 은 세로로 늘어나는 앵커라 건너뛴다: " + Describe(rect));
                return;
            }

            before = before ?? Describe(rect);
            Frame parent = FrameOf(rect.parent as RectTransform);

            Vector2 min = rect.anchorMin;
            Vector2 max = rect.anchorMax;
            Vector2 size = rect.sizeDelta;
            min.y = max.y = anchorY;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, pivotY - (parent.Bottom + anchorY * parent.Height));

            log.AppendLine("  " + label + "\n    전: " + before + "\n    후: " + Describe(rect));
        }

        // 1920 기준 캔버스에서 요소 피벗의 높이(아래에서 잰 값).
        private static float PivotY(RectTransform rect)
        {
            Frame frame = FrameOf(rect);
            return frame.Bottom + rect.pivot.y * frame.Height;
        }

        // 캔버스를 (0, 1920) 으로 보고, 부모 사슬을 따라 내려와 요소의 아래 끝과 높이를 계산한다.
        private static Frame FrameOf(RectTransform rect)
        {
            if (rect == null || rect.GetComponent<Canvas>() != null && rect.GetComponent<Canvas>().isRootCanvas)
                return new Frame(0f, ReferenceHeight);

            Frame parent = FrameOf(rect.parent as RectTransform);
            float anchorBottom = parent.Bottom + rect.anchorMin.y * parent.Height;
            float anchorSpan = (rect.anchorMax.y - rect.anchorMin.y) * parent.Height;
            float height = anchorSpan + rect.sizeDelta.y;
            float pivotY = anchorBottom + anchorSpan * rect.pivot.y + rect.anchoredPosition.y;
            return new Frame(pivotY - rect.pivot.y * height, height);
        }

        private static string Describe(RectTransform rect)
        {
            return "a=(" + rect.anchorMin.x.ToString("0.##") + "," + rect.anchorMin.y.ToString("0.##") + ")-(" +
                   rect.anchorMax.x.ToString("0.##") + "," + rect.anchorMax.y.ToString("0.##") + ") pos=" +
                   rect.anchoredPosition + " size=" + rect.sizeDelta + " 피벗높이(1920)=" + PivotY(rect).ToString("0.#");
        }
    }
}
