using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Pinball;
using OJ.Pinball;

namespace OJ.EditorTools
{
    /// <summary>
    /// <see cref="UIPinballPage"/> 프리팹을 굽는다.
    ///
    /// <b>왜 코드로 굽나.</b> 이 화면은 손으로 만들 수 있는 것이지만, 배선이 여섯 군데다 —
    /// <c>dialogView</c>, <c>PinballPlayback</c> 의 시드 테이블과 보드 뷰, 페이지의 버튼·텍스트.
    /// 하나만 비어도 <b>런타임에 조용히 아무 일도 안 하거나 NRE 로 죽는다.</b>
    /// 도구가 구우면 그 여섯이 항상 같이 채워진다. <c>TowerLobbyEntryInstaller</c> 와 같은 판단.
    ///
    /// <b>모양은 임시다.</b> 자리와 크기만 맞춰 둔 것이고, 다듬는 것은 프리팹에서 하면 된다 —
    /// 이 도구는 <b>이미 있으면 덮어쓰지 않는다.</b>
    /// </summary>
    public static class PinballPagePrefabBuilder
    {
        private const string PrefabFolder = "Assets/Prefab/Refactory/LobbyScene";
        private const string PrefabPath = PrefabFolder + "/UIPinballPage.prefab";

        private const string BoardPrefabPath = "Assets/Pinball/PinballBoard_View.prefab";
        private const string SeedTablePath = "Assets/Pinball/SeedTable.asset";

        /// <summary>발사 버튼의 발수. 자리가 셋이라 셋까지다 — 늘리려면 columns 도 같이 늘릴 것.</summary>
        private static readonly int[] BallCounts = { 1, 10, 30 };

        // 자리 값은 1080x1920 기준이다. 중심이 (0,0) 이고 위가 +다.
        private static readonly Color Backdrop = new Color(0.07f, 0.08f, 0.15f, 1f);
        private static readonly Color Accent = new Color(0.36f, 0.42f, 0.92f, 1f);
        private static readonly Color GoldText = new Color(1f, 0.84f, 0.38f, 1f);

        [MenuItem("OJ/개발/핀볼/페이지 프리팹 굽기")]
        private static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                Debug.Log("[핀볼] 페이지 프리팹이 이미 있다. 그대로 둔다 — " +
                          "다시 굽고 싶으면 먼저 지울 것: " + PrefabPath);
                return;
            }

            GameObject boardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BoardPrefabPath);
            if (boardPrefab == null)
            {
                Debug.LogError("[핀볼] 판 프리팹을 못 찾았다: " + BoardPrefabPath);
                return;
            }

            var seedTable = AssetDatabase.LoadAssetAtPath<SeedTable>(SeedTablePath);
            if (seedTable == null)
            {
                Debug.LogError("[핀볼] 시드 테이블을 못 찾았다: " + SeedTablePath);
                return;
            }

            TMP_FontAsset font = FindKoreanFont();
            if (font == null)
            {
                Debug.LogError("[핀볼] 한글 TMP 폰트를 못 찾았다. 프리팹을 굽지 않는다.");
                return;
            }

            System.IO.Directory.CreateDirectory(PrefabFolder);

            GameObject root = Assemble(boardPrefab, seedTable, font);
            if (root == null)
                return;

            try
            {
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
                if (!ok)
                {
                    Debug.LogError("[핀볼] 페이지 프리팹 저장에 실패했다: " + PrefabPath);
                    return;
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[핀볼] 페이지 프리팹을 구웠다: " + PrefabPath + "\n" +
                      "  다음: OJ/개발/다이얼로그 카탈로그/훑어서 갱신 을 돌려야 화면이 열린다.",
                      AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        }

        private static GameObject Assemble(GameObject boardPrefab, SeedTable seedTable, TMP_FontAsset font)
        {
            GameObject root = NewRect("UIPinballPage", null);
            Stretch(root.GetComponent<RectTransform>());

            // dialogView 는 루트가 아니라 자식이어야 한다. DialogBase 가 이것을
            // SetActive 로 껐다 켜는데, 루트를 끄면 페이지 자신의 Awake 가 돌지 않는다.
            Image view = NewImage("View", root.transform, Backdrop);
            Stretch(view.rectTransform);

            TMP_Text titleText = NewText("Title", view.transform, "핀볼", 48f, Color.white, font);
            SetRect(titleText.rectTransform, new Vector2(600f, 70f), new Vector2(0f, 820f));

            // 판. 프리팹 인스턴스로 넣어야 나중에 판을 다시 구웠을 때 따라온다.
            var board = PrefabUtility.InstantiatePrefab(boardPrefab, view.transform) as GameObject;
            if (board == null)
            {
                Debug.LogError("[핀볼] 판 프리팹을 인스턴스로 만들지 못했다.");
                Object.DestroyImmediate(root);
                return null;
            }

            board.name = "Board";
            var boardRect = board.GetComponent<RectTransform>();
            boardRect.anchorMin = new Vector2(0.5f, 0.5f);
            boardRect.anchorMax = new Vector2(0.5f, 0.5f);
            boardRect.pivot = new Vector2(0.5f, 0.5f);
            boardRect.anchoredPosition = new Vector2(0f, 120f);

            var boardView = board.GetComponent<PinballBoardView>();
            if (boardView == null)
            {
                Debug.LogError("[핀볼] 판 프리팹에 PinballBoardView 가 없다.");
                Object.DestroyImmediate(root);
                return null;
            }

            // 재생기는 판과 같은 오브젝트에 둔다. 판을 지우면 재생기도 같이 사라져야
            // "판이 없는데 공만 굴러가는" 상태가 안 생긴다.
            var playback = board.AddComponent<PinballPlayback>();
            WirePlayback(playback, seedTable, boardView);

            TMP_Text gauge = NewText("GaugeText", view.transform, "", 30f, GoldText, font);
            SetRect(gauge.rectTransform, new Vector2(900f, 60f), new Vector2(0f, 740f));

            TMP_Text ticket = NewText("TicketText", view.transform, "0", 36f, Color.white, font);
            SetRect(ticket.rectTransform, new Vector2(300f, 50f), new Vector2(0f, -540f));

            // 1발·10발·30발. 티켓이 모자란 버튼은 페이지가 잠근다.
            var options = new List<(int count, Button button, TMP_Text cost)>();
            float[] columns = { -340f, 0f, 340f };
            for (int i = 0; i < BallCounts.Length; i++)
            {
                int count = BallCounts[i];
                Button button = NewButton("LaunchButton_" + count, view.transform,
                    count + "발", Accent, font, out TMP_Text caption);
                SetRect(button.GetComponent<RectTransform>(),
                    new Vector2(300f, 130f), new Vector2(columns[i], -680f));

                SetRect(caption.rectTransform, new Vector2(280f, 50f), new Vector2(0f, 24f));

                TMP_Text cost = NewText("Cost", button.transform, count.ToString(), 26f, GoldText, font);
                SetRect(cost.rectTransform, new Vector2(280f, 40f), new Vector2(0f, -30f));

                options.Add((count, button, cost));
            }

            // DialogBase 의 exitBtn 에 넣지 않는다 — 그건 무조건 닫아 버려서
            // "공이 굴러가는 중에는 못 나간다"를 페이지가 판정할 수 없다.
            Button close = NewButton("CloseButton", view.transform, "X", Accent, font, out _);
            SetRect(close.GetComponent<RectTransform>(), new Vector2(90f, 90f), new Vector2(460f, 840f));

            var page = root.AddComponent<UIPinballPage>();
            page.dialogView = view.gameObject;
            WirePage(page, playback, boardView, close, ticket, gauge, options);

            return root;
        }

        /// <summary>
        /// <c>PinballPlayback</c> 의 <c>[SerializeField] private</c> 을 채운다.
        /// 필드가 private 이라 <see cref="SerializedObject"/> 말고는 길이 없다.
        /// </summary>
        private static void WirePlayback(PinballPlayback playback, SeedTable seedTable, PinballBoardView boardView)
        {
            var so = new SerializedObject(playback);
            SetRef(so, "seedTable", seedTable);
            SetRef(so, "boardView", boardView);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WirePage(
            UIPinballPage page, PinballPlayback playback, PinballBoardView boardView,
            Button close, TMP_Text ticket, TMP_Text gauge,
            List<(int count, Button button, TMP_Text cost)> options)
        {
            var so = new SerializedObject(page);
            SetRef(so, "playback", playback);
            SetRef(so, "boardView", boardView);
            SetRef(so, "closeButton", close);
            SetRef(so, "ticketText", ticket);
            SetRef(so, "gaugeText", gauge);

            SerializedProperty list = so.FindProperty("launchOptions");
            if (list == null)
            {
                Debug.LogError("[핀볼] UIPinballPage.launchOptions 를 못 찾았다. 이름이 바뀌었는지 확인할 것.");
            }
            else
            {
                list.arraySize = options.Count;
                for (int i = 0; i < options.Count; i++)
                {
                    SerializedProperty entry = list.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("ballCount").intValue = options[i].count;
                    entry.FindPropertyRelative("button").objectReferenceValue = options[i].button;
                    entry.FindPropertyRelative("costLabel").objectReferenceValue = options[i].cost;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetRef(SerializedObject so, string field, Object value)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null)
            {
                // 필드 이름이 바뀌면 배선이 조용히 빠진다. 그 사고를 지금 말한다.
                Debug.LogError("[핀볼] 배선할 필드를 못 찾았다: " + so.targetObject.GetType().Name +
                               "." + field + " — 이름이 바뀌었는지 확인할 것.");
                return;
            }

            property.objectReferenceValue = value;
        }

        // ──────────────────────────────────────────────── 조립 헬퍼

        private static GameObject NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null)
                go.transform.SetParent(parent, false);

            return go;
        }

        private static Image NewImage(string name, Transform parent, Color color)
        {
            GameObject go = NewRect(name, parent);
            var image = go.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text NewText(
            string name, Transform parent, string text, float size, Color color, TMP_FontAsset font)
        {
            GameObject go = NewRect(name, parent);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.font = font;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }

        private static Button NewButton(
            string name, Transform parent, string label, Color color, TMP_FontAsset font,
            out TMP_Text caption)
        {
            Image background = NewImage(name, parent, color);
            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;

            caption = NewText("Text", background.transform, label, 36f, Color.white, font);
            Stretch(caption.rectTransform);

            return button;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetRect(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static TMP_FontAsset FindKoreanFont()
        {
            const int Sample = '가';

            string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
            System.Array.Sort(guids);

            for (int i = 0; i < guids.Length; i++)
            {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    AssetDatabase.GUIDToAssetPath(guids[i]));

                if (font != null && font.HasCharacter(Sample))
                    return font;
            }

            return null;
        }
    }
}
