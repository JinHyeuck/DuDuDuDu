using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using OJ.Lobby;
using L = OJ.EditorTools.LobbyPsdLayout;
using Kit = OJ.EditorTools.UiBakeKit;

namespace OJ.EditorTools
{
    /// <summary>
    /// 로비 옆 메뉴(<see cref="UILobbyMenuDialog"/>) 프리팹을 굽는다.
    ///
    /// <b>두 번 눌러도 안전하다.</b> "굽기"는 이미 있으면 아무것도 하지 않고 말해 준다. 구조를 바꿨다면
    /// "다시 굽기" 를 쓴다 — 같은 경로에 덮어쓰므로 GUID 가 유지되어 카탈로그를 다시 훑을 필요가 없다.
    /// 처음 구웠을 때만 <c>OJ/개발/다이얼로그 카탈로그/훑어서 갱신</c> 을 돌린다.
    /// </summary>
    public static class LobbyMenuPrefabBaker
    {
        private const string PrefabPath = "Assets/Prefab/UI/UILobbyMenuDialog.prefab";

        private const string RowArt = "Main/Menu_Btn";
        private const string SmallBoxArt = "Upgrade/Ui_Popup_SmallBox";
        private const string DotArt = "Main/Icon_NewPoint";
        private const string CloseArt = "Upgrade/Icon_Cancle";

        private struct Row
        {
            public string name, label, iconArt;
            public float iconScale;
            public bool dot;
        }

        private static readonly Row[] Rows =
        {
            new Row { name = "ItemRow",    label = "아이템", iconArt = "Main/Icon_Item",    iconScale = 3f, dot = false },
            new Row { name = "MailRow",    label = "우편",   iconArt = "Mail/Icon_Mail",    iconScale = 4f, dot = true  },
            new Row { name = "QuestRow",   label = "퀘스트", iconArt = "Main/Icon_Mission", iconScale = 3f, dot = true  },
            new Row { name = "SettingRow", label = "설정",   iconArt = "Main/Icon_Setting", iconScale = 3f, dot = false },
        };

        [MenuItem("OJ/개발/로비/옆 메뉴 프리팹 굽기")]
        private static void BakeIfMissing() => Bake(overwrite: false);

        [MenuItem("OJ/개발/로비/옆 메뉴 프리팹 다시 굽기 (덮어씀)")]
        private static void Rebake() => Bake(overwrite: true);

        private static void Bake(bool overwrite)
        {
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            if (exists && !overwrite)
            {
                Debug.LogWarning("[옆 메뉴] 프리팹이 이미 있어 <b>아무것도 하지 않았다</b>. 구조를 바꿨다면 " +
                                 "OJ/개발/로비/옆 메뉴 프리팹 다시 굽기 (덮어씀) 을 쓸 것.");
                return;
            }

            TMP_FontAsset font = Kit.LoadFont();
            if (font == null)
                return;

            Kit.ClearCache();
            try
            {
                // 새로 들여온 그림은 9슬라이스 테두리가 0 이라 먼저 맞춘다 (x=왼 y=아래 z=오른 w=위, 투명 여백 포함).
                Kit.EnsureSliceBorder("Main/Menu_Btn", new Vector4(19f, 21f, 20f, 17f));
                Kit.EnsureSliceBorder("Main/Btn_MainMode", new Vector4(13f, 13f, 13f, 13f));

                if (!Kit.SavePrefab(Assemble(font), PrefabPath))
                    return;
            }
            catch (System.Exception e)
            {
                Debug.LogError("[옆 메뉴] 프리팹을 굽다 멈췄다 — 저장하지 않았다.\n" + e);
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[옆 메뉴] 프리팹을 구웠다: " + PrefabPath + "\n" +
                      (exists
                          ? "  덮어썼다. GUID 가 그대로라 카탈로그는 다시 안 훑어도 된다."
                          : "  다음: OJ/개발/다이얼로그 카탈로그/훑어서 갱신 을 돌려야 창이 열린다."));
        }

        private static GameObject Assemble(TMP_FontAsset font)
        {
            GameObject root = Kit.NewRect("UILobbyMenuDialog", null);
            Kit.Stretch(root.GetComponent<RectTransform>());

            var dialog = root.AddComponent<UILobbyMenuDialog>();

            // dialogView 는 루트가 아니라 자식이어야 한다. 루트를 끄면 컴포넌트까지 같이 잠들어 다시 열 수 없다.
            GameObject view = Kit.NewRect("View", root.transform);
            Kit.Stretch(view.GetComponent<RectTransform>());
            dialog.dialogView = view;
            Transform p = view.transform;

            // 시안에서 판 바깥은 로비가 그대로 보인다. 어둡게 덮지 않고 누르면 닫히기만 한다.
            Kit.AddBackdrop(dialog, p, new Color(0f, 0f, 0f, 0f));

            // 판 위의 빈 곳을 눌러도 바깥으로 안 새게 판은 레이캐스트를 받는다(딤 버튼이 닫아 버리면 안 된다).
            Kit.FlatRect(p, "Slab", L.Menu.SlabX, L.Menu.SlabY, L.Menu.SlabW, L.Menu.SlabH,
                         Kit.Hex(L.Menu.SlabColor)).raycastTarget = true;
            Kit.FlatRect(p, "EdgeLight", L.Menu.EdgeX, L.Menu.SlabY, L.Menu.EdgeLightW, L.Menu.SlabH,
                         Kit.Hex(L.Menu.EdgeLightColor)).raycastTarget = true;
            Kit.FlatRect(p, "EdgeDark", L.Menu.EdgeX + L.Menu.EdgeLightW, L.Menu.SlabY, L.Menu.EdgeDarkW, L.Menu.SlabH,
                         Color.black).raycastTarget = true;
            Kit.FlatRect(p, "Panel", L.Menu.PanelX, L.Menu.PanelY, L.Menu.PanelW, L.Menu.PanelH,
                         Kit.Hex(L.Menu.PanelColor));

            var buttons = new Button[Rows.Length];
            var dots = new GameObject[Rows.Length];
            for (int i = 0; i < Rows.Length; i++)
                buttons[i] = BuildRow(p, font, Rows[i], L.Menu.RowY[i], out dots[i]);

            Image close = Kit.PlaceDesign(p, "CloseButton", Kit.LoadArt(CloseArt),
                                          L.Menu.CloseX, L.Menu.CloseY, L.Menu.CloseSize, L.Menu.CloseSize, Vector2.zero);
            dialog.AddExitButton(Kit.SpriteButton(close));

            var so = new SerializedObject(dialog);
            Kit.SetRef(so, "itemButton", buttons[0]);
            Kit.SetRef(so, "mailButton", buttons[1]);
            Kit.SetRef(so, "questButton", buttons[2]);
            Kit.SetRef(so, "settingButton", buttons[3]);
            Kit.SetRef(so, "mailDot", dots[1]);
            Kit.SetRef(so, "questDot", dots[2]);
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        /// <summary>
        /// 줄 하나. 뿌리는 Menu_Btn 이고 아이콘 칸·아이콘·글자·빨간 점이 자식이다 —
        /// 눌렀을 때 줄이 눌리면 안의 것이 같이 눌려야 해서다. 자식 좌표는 줄 중심 기준이다.
        /// </summary>
        private static Button BuildRow(Transform parent, TMP_FontAsset font, Row row, float rowTop, out GameObject dot)
        {
            Image rowImage = Kit.SlicedVisible(parent, row.name, RowArt,
                                               L.Menu.RowX, rowTop, L.Menu.RowW, L.Menu.RowH, Color.white, Vector2.zero);
            Button button = Kit.SpriteButton(rowImage);
            Vector2 center = rowImage.rectTransform.anchoredPosition;
            Transform r = rowImage.transform;

            float boxY = rowTop + L.Menu.IconBoxDown;
            Kit.SmallBoxRect(L.Menu.IconBoxX, boxY, L.Menu.IconBoxW, L.Menu.IconBoxH, out Vector2 size, out Vector2 pos);
            Kit.Sliced(r, "IconBox", SmallBoxArt, size, pos - center, Kit.Hex(L.Menu.IconBoxColor));

            Kit.Art icon = Kit.LoadArt(row.iconArt);
            Vector2 boxCenter = Kit.Pos(L.Menu.IconBoxX + L.Menu.IconBoxW * 0.5f, boxY + L.Menu.IconBoxH * 0.5f) - center;
            Kit.PlaceVisible(r, "Icon", icon, boxCenter.x, boxCenter.y, icon.VisW * row.iconScale, icon.VisH * row.iconScale);

            TMP_Text label = Kit.NewText("Label", r, row.label, L.Menu.LabelSize, Color.white, font);
            Kit.SetRect(label.rectTransform, new Vector2(240f, 64f),
                        Kit.Pos(L.Menu.LabelCx, rowTop + L.Menu.LabelDown) - center);

            dot = null;
            if (row.dot)
            {
                Vector2 dotCenter = Kit.Pos(L.Menu.DotX + L.Menu.DotSize * 0.5f,
                                            rowTop - L.Menu.DotUp + L.Menu.DotSize * 0.5f) - center;
                Image dotImage = Kit.PlaceVisible(r, "Dot", Kit.LoadArt(DotArt),
                                                  dotCenter.x, dotCenter.y, L.Menu.DotSize, L.Menu.DotSize);
                dot = dotImage.gameObject;
            }

            return button;
        }
    }
}
