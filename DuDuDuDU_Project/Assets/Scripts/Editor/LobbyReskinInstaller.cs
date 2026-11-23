using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using OJ.Lobby;
using OJ.Point;
using OJ.SeasonPass;
using Kit = OJ.EditorTools.UiBakeKit;
using L = OJ.EditorTools.LobbyPsdLayout.Lobby;
using Object = UnityEngine.Object;

namespace OJ.EditorTools
{
    /// <summary>
    /// 로비 화면을 시안(Main_AddNew_LAyout)대로 다시 입힌다. 수치는 <see cref="LobbyPsdLayout.Lobby"/> 에만 있다.
    ///
    /// <b>순서.</b> (A) 로비 버튼 프리팹 7개의 안쪽을 고쳐 저장하고, (B) 씬의 인스턴스를 프리팹 상태로 되돌린 뒤
    /// 시안 자리에 다시 놓고, 씬에만 있는 HUD·재화 칸·보상 띠·아래 버튼 줄을 고친다.
    ///
    /// <b>좌표.</b> 전부 시안 좌표(1080x1920, 좌상단 원점)로 적고 <see cref="DesignSpace"/> 가 월드 위치로 바꾼다.
    /// 앵커는 건드리지 않는다 — 위쪽 HUD 는 위에, 아래 줄은 아래에 붙은 채로 둬야 긴 화면에서도 시안처럼 보인다.
    /// 재실행해도 같은 결과가 나온다(만든 자식은 이름으로 찾고, 이미 있으면 다시 만들지 않는다).
    ///
    /// 메뉴로만 돈다 — 씬을 덮는 도구를 <c>[InitializeOnLoad]</c> 로 돌리지 않는다(MIGRATION_BASELINE 1.1).
    /// </summary>
    internal static class LobbyReskinInstaller
    {
        private const string LobbyScenePath = "Assets/Scenes/LobbyScene.unity";
        private const string PrefabDir = "Assets/Prefab/Lobby/";
        private const string PlainMaterialPath = "Assets/BMHANNAProOTF/BMHANNAProOTF SDF Material_NoneOutLine.mat";
        private const string MembershipPrefab = PrefabDir + "UIMembershipLobbyButton.prefab";
        private const string SevenDaysPrefab = PrefabDir + "UISpecialSevenDaysLobbyButton.prefab";
        private const string SeasonPassPrefab = PrefabDir + "UISeasonPassLobbyButton.prefab";
        private const string QuestPrefab = PrefabDir + "UIQuestLobbyButton.prefab";
        private const string TowerPrefab = PrefabDir + "UITowerEntryButton.prefab";
        private const string PinballPrefab = PrefabDir + "UIPinballEntryButton.prefab";
        private const string SynergyPrefab = "Assets/Prefab/Synergy/UISynergyEntryButton.prefab";

        private const string LobbyRoot = "Canvas/LobbyLayoutController/";
        private const string TileArt = "Main/Btn_MainMode";
        private const string DotArt = "Main/Icon_NewPoint";

        // 시안 글자 중심에 BM HANNA 글자 중심을 맞추는 세로 보정(시안 좌표, 아래가 +). 구운 뒤 크롭 비교로 정한다.
        private const float TextDy = 0f;

        private static TMP_FontAsset font;

        [MenuItem("OJ/개발/로비/시안 적용")]
        private static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[로비] 플레이 중에는 적용하지 않는다. 멈추고 다시 누를 것.");
                return;
            }

            Kit.ClearCache();
            font = Kit.LoadFont();
            if (font == null)
                return;

            try
            {
                BakePrefabs();
            }
            catch (Exception e)
            {
                Debug.LogError("[로비] 프리팹을 굽다 멈췄다: " + e);
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Scene scene = SceneManager.GetSceneByPath(LobbyScenePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded)
                scene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Additive);

            try
            {
                ApplyScene(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[로비] 시안 적용 끝. 씬 저장함.");
            }
            catch (Exception e)
            {
                Debug.LogError("[로비] 씬을 고치다 멈췄다(저장하지 않았다): " + e);
            }
            finally
            {
                if (!wasLoaded && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        // ──────────────────────────────────────────────── 좌표 도우미

        /// <summary>한 오브젝트가 시안에서 차지하는 사각형: 중심 (Cx, Cy) 와 크기 (W, H).</summary>
        private readonly struct Spot
        {
            public readonly float Cx, Cy, W, H;

            public Spot(float cx, float cy, float w, float h)
            {
                Cx = cx; Cy = cy; W = w; H = h;
            }
        }

        /// <summary>시안 좌표 (x, y) → 월드 위치. 원점 오브젝트의 사각형 중심이 시안 좌표 (cx, cy) 다.</summary>
        private readonly struct DesignSpace
        {
            private readonly Transform origin;
            private readonly float cx, cy;

            public DesignSpace(Transform origin, float cx, float cy)
            {
                if (Mathf.Abs(origin.lossyScale.x - 1f) > 0.001f || Mathf.Abs(origin.lossyScale.y - 1f) > 0.001f)
                    throw new InvalidOperationException("[로비] '" + origin.name + "' 의 배율이 1 이 아니다: " + origin.lossyScale);

                this.origin = origin;
                this.cx = cx;
                this.cy = cy;
            }

            public Vector3 World(float x, float y)
            {
                return origin.TransformPoint(new Vector3(x - cx, cy - y, 0f));
            }
        }

        private enum HAlign { Left, Center, Right }

        private static void Fit(RectTransform rt, Vector2 size, Vector3 world)
        {
            rt.pivot = new Vector2(0.5f, 0.5f);
            if (rt.anchorMin != rt.anchorMax)
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
            }

            rt.sizeDelta = size;
            rt.position = new Vector3(world.x, world.y, rt.position.z);
        }

        private static void Move(DesignSpace sp, Transform t, float x, float y)
        {
            var rt = (RectTransform)t;
            rt.pivot = new Vector2(0.5f, 0.5f);
            Vector3 world = sp.World(x, y);
            rt.position = new Vector3(world.x, world.y, rt.position.z);
        }

        private static Spot SlicedSpot(string artPath, float x, float y, float w, float h, float s)
        {
            return SlicedSpot(Kit.LoadArt(artPath), x, y, w, h, s);
        }

        /// <summary>투명 여백도 테두리라 s 배로 늘어난다. 보이는 사각형 (x, y, w, h) 를 주면 Image 사각형을 준다.</summary>
        private static Spot SlicedSpot(Kit.Art art, float x, float y, float w, float h, float s)
        {
            float left = art.x0, right = art.width - art.x1;
            float top = art.yTop0, bottom = art.height - art.yTop1;
            return new Spot(
                x + w * 0.5f + (right - left) * s * 0.5f,
                y + h * 0.5f + (bottom - top) * s * 0.5f,
                w + (left + right) * s,
                h + (top + bottom) * s);
        }

        /// <summary>9슬라이스 판을 보이는 사각형 (x, y, w, h) 에 맞춘다. artPath 가 null 이면 지금 스프라이트를 쓴다.</summary>
        private static void FitSliced(
            DesignSpace sp, Image image, string artPath, float x, float y, float w, float h, float s, bool? hit)
        {
            Kit.Art art = artPath != null ? Kit.LoadArt(artPath) : Kit.Measure(image.sprite);
            if (artPath != null)
                image.sprite = art.sprite;

            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.pixelsPerUnitMultiplier = 1f / s;

            Spot spot = SlicedSpot(art, x, y, w, h, s);
            Fit(image.rectTransform, new Vector2(spot.W, spot.H), sp.World(spot.Cx, spot.Cy));

            if (hit.HasValue)
            {
                image.raycastTarget = hit.Value;
                image.raycastPadding = hit.Value
                    ? new Vector4(art.x0 * s, (art.height - art.yTop1) * s, (art.width - art.x1) * s, art.yTop0 * s)
                    : Vector4.zero;
            }
        }

        /// <summary>그림의 <b>보이는 부분</b> 중심이 시안 (cx, cy) 에 오게 s 배로 놓는다. hit 이면 보이는 부분만 눌린다.</summary>
        private static void ApplyArt(DesignSpace sp, Image image, Kit.Art art, float s, float cx, float cy, bool hit)
        {
            image.sprite = art.sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = Color.white;

            float imageCx = cx - (art.VisCx - art.width * 0.5f) * s;
            float imageCy = cy - (art.VisCy - art.height * 0.5f) * s;
            Fit(image.rectTransform, new Vector2(art.width * s, art.height * s), sp.World(imageCx, imageCy));

            image.raycastTarget = hit;
            image.raycastPadding = hit
                ? new Vector4(art.x0 * s, (art.height - art.yTop1) * s, (art.width - art.x1) * s, art.yTop0 * s)
                : Vector4.zero;
        }

        /// <summary>스크립트가 스프라이트를 갈아 끼우는 칸. 지금 들어 있는 그림(없으면 고기)으로 자리를 잡는다.</summary>
        private static void ApplyExisting(DesignSpace sp, Image image, float s, float cx, float cy, bool hit)
        {
            Kit.Art art = image.sprite != null ? Kit.Measure(image.sprite) : Kit.LoadArt("Gem/Gem_GamePlay");
            ApplyArt(sp, image, art, s, cx, cy, hit);
        }

        private static void PlaceText(
            DesignSpace sp, TMP_Text text, string value, float size, Color? color, HAlign align, float x, float cy)
        {
            const float width = 360f;

            if (value != null)
                text.text = value;

            text.fontSize = size;
            text.enableAutoSizing = false;
            if (color.HasValue)
                text.color = color.Value;

            text.alignment = align == HAlign.Left ? TextAlignmentOptions.MidlineLeft
                : align == HAlign.Right ? TextAlignmentOptions.MidlineRight
                : TextAlignmentOptions.Midline;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;

            float rectCx = align == HAlign.Left ? x + width * 0.5f : align == HAlign.Right ? x - width * 0.5f : x;
            Fit(text.rectTransform, new Vector2(width, size * 1.4f), sp.World(rectCx, cy + TextDy));
        }

        private static void Dot(DesignSpace sp, Transform parent, string name, float cx, float cy)
        {
            Transform t = parent.Find(name);
            if (t == null)
                return;

            Image image = t.GetComponent<Image>();
            if (image == null)
                image = t.gameObject.AddComponent<Image>();

            ApplyArt(sp, image, Kit.LoadArt(DotArt), 1f, cx, cy, false);
        }

        // ──────────────────────────────────────────────── 찾기·만들기

        private static Transform Req(Transform parent, string name)
        {
            Transform t = parent.Find(name);
            if (t == null)
                throw new InvalidOperationException("[로비] '" + parent.name + "' 아래에 '" + name + "' 이 없다.");

            return t;
        }

        private static void Hide(Transform parent, string name)
        {
            Transform t = parent.Find(name);
            if (t != null)
                t.gameObject.SetActive(false);
        }

        private static Image EnsureImage(Transform parent, string name)
        {
            Transform t = parent.Find(name);
            if (t == null)
                return Kit.NewImage(name, parent, Color.white);

            Image image = t.GetComponent<Image>();
            return image != null ? image : t.gameObject.AddComponent<Image>();
        }

        private static TMP_Text EnsureText(Transform parent, string name, string value, float size, Color color)
        {
            Transform t = parent.Find(name);
            if (t != null && t.TryGetComponent(out TMP_Text existing))
                return existing;

            return Kit.NewText(name, parent, value, size, color, font);
        }

        private static Transform At(Scene scene, string path)
        {
            int slash = path.IndexOf('/');
            string first = slash < 0 ? path : path.Substring(0, slash);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != first)
                    continue;

                if (slash < 0)
                    return root.transform;

                Transform t = root.transform.Find(path.Substring(slash + 1));
                if (t != null)
                    return t;
            }

            throw new InvalidOperationException("[로비] 씬에서 못 찾았다: " + path);
        }

        /// <summary>프리팹 인스턴스에서 바꾼 값이 덮어쓰기로 남게 한다. 안 하면 저장해도 인스턴스 쪽 변경이 사라진다.</summary>
        private static void TouchTree(GameObject root)
        {
            foreach (Component c in root.GetComponentsInChildren<Component>(true))
                Touch(c);

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                Touch(t.gameObject);
        }

        private static void Touch(Object o)
        {
            if (o == null)
                return;

            EditorUtility.SetDirty(o);
            if (PrefabUtility.IsPartOfPrefabInstance(o))
                PrefabUtility.RecordPrefabInstancePropertyModifications(o);
        }

        // ──────────────────────────────────────────────── A. 프리팹

        private static readonly Spot MembershipSpot = new Spot(93f, 415f, 160f, 190f);
        private static readonly Spot SevenDaysSpot = new Spot(85f, 600f, 160f, 190f);

        private static Spot PassSpot => SlicedSpot("Pass/Pass_premium_Btn", L.PassX, L.PassY, L.PassW, L.PassH, 3f);
        private static Spot QuestSpot => SlicedSpot(TileArt, L.TileX, L.QuestTileY, L.TileW, L.TileH, 3f);
        private static Spot SynergySpot => SlicedSpot(TileArt, L.TileX, L.SynergyTileY, L.TileW, L.TileH, 3f);
        private static Spot TowerSpot => SlicedSpot(TileArt, L.TowerTileX, L.TowerTileY, L.ButtonW, 145f, 3f);
        private static Spot PinballSpot => SlicedSpot(TileArt, L.PinballTileX, L.RowY, L.ButtonW, L.RowH, 3f);

        private static void BakePrefabs()
        {
            EditPrefab(MembershipPrefab, MembershipSpot, BakeMembership);
            EditPrefab(SevenDaysPrefab, SevenDaysSpot, BakeSevenDays);
            EditPrefab(SeasonPassPrefab, PassSpot, BakeSeasonPass);
            EditPrefab(QuestPrefab, QuestSpot, (sp, root) => BakeSmallTile(sp, root, L.QuestTileY, true));
            EditPrefab(SynergyPrefab, SynergySpot, (sp, root) => BakeSmallTile(sp, root, L.SynergyTileY, false));
            EditPrefab(TowerPrefab, TowerSpot, BakeTower);
            EditPrefab(PinballPrefab, PinballSpot, BakePinball);
        }

        /// <summary>프리팹을 열어 루트를 시안 사각형 크기로 맞추고 고친 뒤 같은 경로에 저장한다(GUID 유지).</summary>
        private static void EditPrefab(string path, Spot spot, Action<DesignSpace, Transform> edit)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var rt = (RectTransform)root.transform;
                rt.localScale = Vector3.one;
                rt.pivot = new Vector2(0.5f, 0.5f);
                if (rt.anchorMin != rt.anchorMax)
                {
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                }

                rt.sizeDelta = new Vector2(spot.W, spot.H);

                edit(new DesignSpace(root.transform, spot.Cx, spot.Cy), root.transform);

                PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok);
                if (!ok)
                    throw new InvalidOperationException("[로비] 프리팹 저장에 실패했다: " + path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void BakeMembership(DesignSpace sp, Transform root)
        {
            Transform view = Req(root, "View");
            Image slot = Req(view, "Slot").GetComponent<Image>();
            ApplyArt(sp, slot, Kit.LoadArt("Main/Icon_Membership"), L.LeftIconScale,
                L.MembershipIconCx, L.MembershipIconCy, true);
            Hide(slot.transform, "Meat");
            Hide(slot.transform, "Crown");

            Button button = root.GetComponent<Button>();
            if (button != null && button.targetGraphic == null)
                button.targetGraphic = slot;

            PlaceText(sp, Req(view, "Label").GetComponent<TMP_Text>(), "멤버십", L.LeftLabelSize, null,
                HAlign.Center, L.MembershipLabelCx, L.MembershipLabelCy);

            Move(sp, Req(view, "Badge"), 125f, 352f);
        }

        private static void BakeSevenDays(DesignSpace sp, Transform root)
        {
            // View 는 스크립트가 켜고 끈다. 끄지 않고 안의 그림만 바꾼다.
            Transform view = Req(root, "View");
            Image slot = Req(view, "Slot").GetComponent<Image>();
            ApplyArt(sp, slot, Kit.LoadArt("Main/Icon_Special7Day"), L.LeftIconScale,
                L.SevenIconCx, L.SevenIconCy, true);
            Hide(slot.transform, "Gem");
            Hide(slot.transform, "Days");

            PlaceText(sp, Req(view, "Label").GetComponent<TMP_Text>(), "특별 7일", L.LeftLabelSize, null,
                HAlign.Center, L.SevenLabelCx, L.SevenLabelCy);

            Dot(sp, view, "RedDot", 145f, 535f);
        }

        private static void BakeSeasonPass(DesignSpace sp, Transform root)
        {
            Image plate = root.GetComponent<Image>();
            FitSliced(sp, plate, "Pass/Pass_premium_Btn", L.PassX, L.PassY, L.PassW, L.PassH, 3f, true);
            plate.color = Color.white;

            PlaceText(sp, Req(root, "Title").GetComponent<TMP_Text>(), null, L.PassTitleSize, null,
                HAlign.Center, L.PassTitleCx, L.PassTitleCy);

            Transform remainT = root.Find("Remain") ?? Req(root, "Summary");
            remainT.name = "Remain";
            TMP_Text remain = remainT.GetComponent<TMP_Text>();
            PlaceText(sp, remain, "25일 2시간 남음", L.PassRemainSize, Kit.Hex(L.PassRemainColor),
                HAlign.Left, L.PassRemainX, L.PassRemainCy);
            Material plain = AssetDatabase.LoadAssetAtPath<Material>(PlainMaterialPath);
            if (plain != null)
                remain.fontSharedMaterial = plain;

            TMP_Text level = EnsureText(root, "Level", "Lv.1", L.PassLevelSize, Kit.Hex(L.PassLevelColor));
            PlaceText(sp, level, "Lv.1", L.PassLevelSize, Kit.Hex(L.PassLevelColor),
                HAlign.Center, L.PassLevelCx, L.PassLevelCy);

            Image clock = EnsureImage(root, "Clock");
            ApplyArt(sp, clock, Kit.LoadArt("Pass/Pass_Clork"), 3f, L.PassClockCx, L.PassClockCy, false);

            Dot(sp, root, "RedDot", L.PassDotCx, L.PassDotCy);

            var script = root.GetComponent<UISeasonPassLobbyButton>();
            if (script == null)
                throw new InvalidOperationException("[로비] 시즌 패스 프리팹에 UISeasonPassLobbyButton 이 없다.");

            var so = new SerializedObject(script);
            Kit.SetRef(so, "levelText", level);
            Kit.SetRef(so, "remainText", remain);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>퀘스트·시너지: 시안에 없는 입구라 메뉴 버튼 크기의 작은 칸으로 왼쪽 열에 쌓는다.</summary>
        private static void BakeSmallTile(DesignSpace sp, Transform root, float tileY, bool quest)
        {
            Image plate = root.GetComponent<Image>();
            FitSliced(sp, plate, TileArt, L.TileX, tileY, L.TileW, L.TileH, 3f, true);
            plate.color = Color.white;

            float cx = L.TileX + L.TileW * 0.5f;
            if (quest)
            {
                Image icon = EnsureImage(root, "Icon");
                icon.transform.SetAsFirstSibling();
                ApplyArt(sp, icon, Kit.LoadArt("Main/Icon_Mission"), 2f, cx, tileY + 40f, false);

                PlaceText(sp, Req(root, "Label").GetComponent<TMP_Text>(), "퀘스트", L.LeftLabelSize, null,
                    HAlign.Center, cx, tileY + 98f);
                Dot(sp, root, "RedDot", L.TileX + L.TileW - 8f, tileY + 8f);
            }
            else
            {
                PlaceText(sp, Req(root, "Title").GetComponent<TMP_Text>(), "시너지", L.LeftLabelSize, null,
                    HAlign.Center, cx, tileY + 42f);
                PlaceText(sp, Req(root, "Progress").GetComponent<TMP_Text>(), null, 24f, null,
                    HAlign.Center, cx, tileY + 84f);
            }
        }

        private static void BakeTower(DesignSpace sp, Transform root)
        {
            Image plate = root.GetComponent<Image>();
            FitSliced(sp, plate, TileArt, L.TowerTileX, L.TowerTileY, L.ButtonW, 145f, 3f, true);
            plate.color = Color.white;

            Image icon = EnsureImage(root, "Icon");
            icon.transform.SetAsFirstSibling();
            ApplyArt(sp, icon, Kit.LoadArt("Main/Icon_InfinityMode"), L.TileIconScale,
                L.TowerIconCx, L.TowerIconCy, false);

            PlaceText(sp, Req(root, "Title").GetComponent<TMP_Text>(), null, L.TileTitleSize, null,
                HAlign.Center, L.TowerTitleCx, L.TileTitleCy);
            Hide(root, "Progress");
            Move(sp, Req(root, "NewBadge"), L.TowerBadgeCx, L.TowerBadgeCy);
        }

        private static void BakePinball(DesignSpace sp, Transform root)
        {
            Image plate = root.GetComponent<Image>();
            FitSliced(sp, plate, TileArt, L.PinballTileX, L.RowY, L.ButtonW, L.RowH, 3f, true);
            plate.color = Color.white;

            Image icon = EnsureImage(root, "Icon");
            icon.transform.SetAsFirstSibling();
            ApplyArt(sp, icon, Kit.LoadArt("Main/Icon_Pinball"), L.TileIconScale,
                L.PinballIconCx, L.PinballIconCy, false);

            PlaceText(sp, Req(root, "Title").GetComponent<TMP_Text>(), "뽑기", L.TileTitleSize, null,
                HAlign.Center, L.PinballTitleCx, L.TileTitleCy);
            Hide(root, "Ticket");
        }

        // ──────────────────────────────────────────────── B. 씬

        private static void ApplyScene(Scene scene)
        {
            DesignSpace sp = CanvasSpace(scene);

            Rebuild(scene, sp, MembershipPrefab, MembershipSpot, null);
            Rebuild(scene, sp, SevenDaysPrefab, SevenDaysSpot, null);
            Rebuild(scene, sp, SeasonPassPrefab, PassSpot, null);
            Rebuild(scene, sp, QuestPrefab, QuestSpot, new Vector2(0f, 1f));
            Rebuild(scene, sp, SynergyPrefab, SynergySpot, null);
            Rebuild(scene, sp, TowerPrefab, TowerSpot, null);
            Rebuild(scene, sp, PinballPrefab, PinballSpot, null);

            ApplyHud(scene, sp);
            ApplyMenuButton(scene, sp);
            ApplyRewardStrip(scene, sp);
            ApplySweep(scene, sp);
            ApplyGoButton(scene, sp);
        }

        private static DesignSpace CanvasSpace(Scene scene)
        {
            Transform canvas = At(scene, "Canvas/LobbyLayoutController").parent;
            var rt = (RectTransform)canvas;
            Vector2 size = rt.rect.size;
            if (Mathf.Abs(size.x - 1080f) > 0.5f || Mathf.Abs(size.y - 1920f) > 0.5f)
            {
                throw new InvalidOperationException(
                    "[로비] 캔버스가 1080x1920 이 아니다(" + size + "). Game 뷰를 1080x1920 으로 맞추고 다시 누를 것.");
            }

            return new DesignSpace(canvas, 540f, 960f);
        }

        /// <summary>
        /// 씬의 프리팹 인스턴스를 프리팹 상태로 되돌리고 시안 자리에 다시 놓는다.
        /// 되돌리지 않으면 예전 인스턴스 덮어쓰기(크기·그림)가 방금 구운 프리팹을 가린다.
        /// 이름·켜짐·앵커는 로비 배치의 일부라 보존한다.
        /// </summary>
        private static void Rebuild(Scene scene, DesignSpace sp, string prefabPath, Spot spot, Vector2? anchors)
        {
            var found = new System.Collections.Generic.List<GameObject>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    GameObject go = t.gameObject;
                    if (PrefabUtility.IsOutermostPrefabInstanceRoot(go) &&
                        PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go) == prefabPath)
                    {
                        found.Add(go);
                    }
                }
            }

            if (found.Count == 0)
                throw new InvalidOperationException("[로비] 씬에 이 프리팹 인스턴스가 없다: " + prefabPath);

            foreach (GameObject go in found)
            {
                var rt = (RectTransform)go.transform;
                string name = go.name;
                bool active = go.activeSelf;
                Vector2 anchorMin = rt.anchorMin, anchorMax = rt.anchorMax;

                PrefabUtility.RevertPrefabInstance(go, InteractionMode.AutomatedAction);

                go.name = name;
                go.SetActive(active);
                if (anchors.HasValue)
                {
                    rt.anchorMin = anchors.Value;
                    rt.anchorMax = anchors.Value;
                }
                else
                {
                    rt.anchorMin = anchorMin;
                    rt.anchorMax = anchorMax;
                }

                Fit(rt, new Vector2(spot.W, spot.H), sp.World(spot.Cx, spot.Cy));
                TouchTree(go);
            }
        }

        private static void ApplyHud(Scene scene, DesignSpace sp)
        {
            Transform top = At(scene, LobbyRoot + "Top");
            Transform profile = Req(top, "profile");

            FitSliced(sp, profile.GetComponent<Image>(), null,
                L.ProfileX, L.ProfileY, L.ProfileSize, L.ProfileSize, 3f, null);

            profile.GetComponent<Image>().color = Kit.Hex(L.ProfileTint);

            Image back = EnsureImage(profile, "BatBack");
            back.color = Color.black;
            back.raycastTarget = false;
            back.transform.SetAsFirstSibling();
            Fit(back.rectTransform, new Vector2(L.BatBackSize, L.BatBackSize), sp.World(L.BatBackCx, L.BatBackCy));

            // 박쥐는 틀 안에서 잘려 보인다 — 마스크 안에 넣는다.
            Transform mask = profile.Find("BatMask");
            if (mask == null)
            {
                GameObject m = Kit.NewRect("BatMask", profile);
                m.AddComponent<RectMask2D>();
                mask = m.transform;
            }

            mask.SetAsFirstSibling();
            back.transform.SetAsFirstSibling();
            Fit((RectTransform)mask, new Vector2(L.BatW, L.BatH), sp.World(L.BatCx, L.BatCy));

            Image bat = null, fight = null;
            foreach (Image image in profile.GetComponentsInChildren<Image>(true))
            {
                if (image.transform == profile || image.sprite == null)
                    continue;

                if (image.sprite.name.StartsWith("Bat"))
                    bat = image;
                else if (image.sprite.name == "Icon_Fight")
                    fight = image;
            }

            if (bat == null || fight == null)
                throw new InvalidOperationException("[로비] profile 안에서 박쥐/전투력 아이콘을 못 찾았다.");

            bat.transform.SetParent(mask, false);
            ApplyExisting(sp, bat, 4f, L.BatCx, L.BatCy, false);
            ApplyExisting(sp, fight, 1f, L.PowerIconCx, L.PowerIconCy, false);

            PlaceText(sp, Req(profile, "Text (TMP)").GetComponent<TMP_Text>(), null, L.NickSize, null,
                HAlign.Left, L.NickX, L.NickCy);
            PlaceText(sp, Req(profile, "Text (TMP) (1)").GetComponent<TMP_Text>(), null, L.PowerSize, null,
                HAlign.Left, L.ApkX, L.ApkCy);
            PlaceText(sp, Req(profile, "Text (TMP) (2)").GetComponent<TMP_Text>(), null, L.PowerSize, null,
                HAlign.Left, L.PowerX, L.PowerCy);

            var chips = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in top)
            {
                if (child.name.StartsWith("UIPointItem"))
                    chips.Add(child);
            }

            if (chips.Count != L.ChipX.Length)
                throw new InvalidOperationException("[로비] 재화 칸이 " + chips.Count + "개다(3개여야 한다).");

            chips.Sort((a, b) => ((RectTransform)a).anchoredPosition.x.CompareTo(((RectTransform)b).anchoredPosition.x));

            for (int i = 0; i < chips.Count; i++)
            {
                float x = L.ChipX[i];
                Transform chip = chips[i];
                FitSliced(sp, chip.GetComponent<Image>(), null, x, L.ChipY, L.ChipW, L.ChipH, 2f, null);

                ApplyExisting(sp, Req(chip, "Icon").GetComponent<Image>(), L.ChipIconScale,
                    x + L.ChipIconDx, L.ChipIconCy, false);
                PlaceText(sp, Req(chip, "Amount").GetComponent<TMP_Text>(), null, L.ChipTextSize, null,
                    HAlign.Right, x + L.ChipW - L.ChipTextRightPad, L.ChipTextCy);
            }

            TouchTree(top.gameObject);
        }

        private static void ApplyMenuButton(Scene scene, DesignSpace sp)
        {
            Transform menu = At(scene, LobbyRoot + "Middle/Menu");
            FitSliced(sp, menu.GetComponent<Image>(), null, L.MenuX, L.MenuY, L.MenuW, L.MenuH, 3f, true);
            ApplyExisting(sp, Req(menu, "Image").GetComponent<Image>(), 3f, L.MenuIconCx, L.MenuIconCy, false);

            Image dot = EnsureImage(menu, "RedDot");
            ApplyArt(sp, dot, Kit.LoadArt(DotArt), 1f, L.MenuDotCx, L.MenuDotCy, false);
            dot.gameObject.SetActive(false);

            var button = menu.GetComponent<Button>();
            if (button.onClick.GetPersistentEventCount() > 0)
                Debug.LogWarning("[로비] Menu 버튼에 예전 persistent onClick 이 남아 있다. 옆 메뉴와 겹쳐 열릴 수 있다.");

            var script = menu.GetComponent<UILobbyMenuButton>();
            if (script == null)
                script = menu.gameObject.AddComponent<UILobbyMenuButton>();

            var so = new SerializedObject(script);
            if (so.FindProperty("button").objectReferenceValue == null)
                Kit.SetRef(so, "button", button);
            if (so.FindProperty("redDot").objectReferenceValue == null)
                Kit.SetRef(so, "redDot", dot.gameObject);
            so.ApplyModifiedPropertiesWithoutUndo();

            TouchTree(menu.gameObject);
        }

        private static void ApplyRewardStrip(Scene scene, DesignSpace sp)
        {
            Transform strip = At(scene, LobbyRoot + "Content/Stage/StageRewardBtn");
            FitSliced(sp, strip.GetComponent<Image>(), null, L.StripX, L.StripY, L.StripW, L.StripH, 3f, null);

            Transform wave = Req(strip, "WaveRewardBtn");
            ApplyExisting(sp, wave.GetComponent<Image>(), 3f, L.WaveIconCx, L.WaveIconCy, true);
            PlaceText(sp, Req(wave, "Text (TMP)").GetComponent<TMP_Text>(), null, L.StripLabelSize, null,
                HAlign.Center, L.WaveLabelCx, L.WaveLabelCy);

            Transform idle = Req(strip, "IdleRewardBtn");
            ApplyExisting(sp, idle.GetComponent<Image>(), 3f, L.IdleIconCx, L.IdleIconCy, true);
            PlaceText(sp, Req(idle, "Text (TMP)").GetComponent<TMP_Text>(), null, L.StripLabelSize, null,
                HAlign.Center, L.IdleLabelCx, L.IdleLabelCy);

            Transform star = Req(strip, "StageStarEntryButton");
            ApplyExisting(sp, star.GetComponent<Image>(), 3f, L.StarIconCx, L.StarIconCy, true);
            PlaceText(sp, Req(star, "Text (TMP) (1)").GetComponent<TMP_Text>(), null, L.StripLabelSize, null,
                HAlign.Center, L.StarLabelCx, L.StarLabelCy);
            Hide(star, "TotalStarText");

            TouchTree(strip.gameObject);
        }

        private static void ApplySweep(Scene scene, DesignSpace sp)
        {
            Transform sweep = At(scene, LobbyRoot + "Content/Stage/Sweep");
            FitSliced(sp, sweep.GetComponent<Image>(), null, L.SweepX, L.RowY, L.ButtonW, L.RowH, 3f, true);

            PlaceText(sp, Req(sweep, "Text (TMP)").GetComponent<TMP_Text>(), null, L.SweepLabelSize, null,
                HAlign.Center, L.SweepLabelCx, L.SweepLabelCy);
            PlaceText(sp, Req(sweep, "Text (TMP) (1)").GetComponent<TMP_Text>(), null, L.SweepStateSize, null,
                HAlign.Center, L.SweepStateCx, L.SweepStateCy);
            Hide(sweep, "Image");

            TouchTree(sweep.gameObject);
        }

        private static void ApplyGoButton(Scene scene, DesignSpace sp)
        {
            Transform go = At(scene, LobbyRoot + "Content/Stage/GoBattleScene");
            FitSliced(sp, go.GetComponent<Image>(), null, L.GoX, L.RowY, L.ButtonW, L.RowH, 3f, true);

            PlaceText(sp, Req(go, "Text (TMP)").GetComponent<TMP_Text>(), "스테이지 입장", L.GoLabelSize, null,
                HAlign.Center, L.GoLabelCx, L.GoLabelCy);

            Transform pillT = go.Find("CostPill");
            Image pill = pillT != null ? pillT.GetComponent<Image>() : Kit.NewImage("CostPill", go, Color.white);
            pill.transform.SetAsFirstSibling();
            pill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            pill.type = Image.Type.Sliced;
            pill.fillCenter = true;
            pill.pixelsPerUnitMultiplier = 1.6f;
            pill.color = new Color(0f, 0f, 0f, L.CostPillAlpha);
            pill.raycastTarget = false;
            Fit(pill.rectTransform, new Vector2(L.CostPillW, L.CostPillH),
                sp.World(L.CostPillX + L.CostPillW * 0.5f, L.CostPillY + L.CostPillH * 0.5f));

            ApplyExisting(sp, Req(go, "Image").GetComponent<Image>(), L.CostIconScale,
                L.CostIconCx, L.CostIconCy, false);
            PlaceText(sp, Req(go, "Text (TMP) (1)").GetComponent<TMP_Text>(), null, L.CostTextSize, null,
                HAlign.Center, L.CostTextCx, L.CostTextCy);

            TouchTree(go.gameObject);
        }
    }
}
