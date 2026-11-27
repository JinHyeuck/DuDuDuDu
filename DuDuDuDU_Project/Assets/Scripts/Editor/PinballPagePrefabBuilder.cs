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
    /// <b>왜 코드로 굽나.</b> 이 화면은 손으로 만들 수 있는 것이지만, 배선이 많다 —
    /// <c>dialogView</c>, <c>PinballPlayback</c> 의 시드 테이블과 보드 뷰, 페이지의 버튼·텍스트·게이지·특수 핀 장식.
    /// 하나만 비어도 <b>런타임에 조용히 아무 일도 안 하거나 NRE 로 죽는다.</b>
    /// 도구가 구우면 그것들이 항상 같이 채워진다. <c>TowerLobbyEntryInstaller</c> 와 같은 판단.
    ///
    /// <b>모양은 아트 시안(<c>Art/0PSD/핀볼.psd</c>)을 따른다.</b> 좌표는 PSD 픽셀 그대로 적고 <see cref="Pos"/> 가
    /// 화면 중앙 기준으로 바꾼다. 기록은 <c>Docs/PinballUIArtPort.md</c>. 이 도구는 <b>이미 있으면 덮어쓰지 않는다</b> —
    /// 다시 굽기는 메뉴의 "다시 굽기 (덮어씀)".
    /// </summary>
    public static class PinballPagePrefabBuilder
    {
        private const string PrefabFolder = "Assets/Prefab/Refactory/LobbyScene";
        private const string PrefabPath = PrefabFolder + "/UIPinballPage.prefab";

        private const string BoardPrefabPath = "Assets/Pinball/PinballBoard_View.prefab";
        private const string SeedTablePath = "Assets/Pinball/PinballSeedTable.asset";

        private const string BonusBoardPrefabPath = "Assets/Pinball/BonusBoard_View.prefab";
        private const string BonusSeedTablePath = "Assets/Pinball/BonusSeedTable.asset";
        private const string BonusBoardAssetPath = "Assets/Pinball/BonusBoard.asset";

        /// <summary>
        /// 배율 버튼. <c>PinballRewardDatabase.PopulateDefaults</c> 의 기본 표와 같은 순서다 —
        /// 에셋에서 표를 바꿨다면 프리팹을 다시 구울 것.
        /// </summary>
        private static readonly int[] Multipliers = { 1, 2, 3, 5, 10, 20, 50, 100 };

        private static readonly Color GoldText = new Color(1f, 0.84f, 0.38f, 1f);

        /// <summary>보상 라운드 주사위 수. BonusDiceDatabase 의 기본값과 같다.</summary>
        private const int DiceCount = 5;

        // 보상 면 아랫부분 배치(시안 좌표). 아치 아래 바닥 위에 주사위 한 줄, 그 아래 버튼 두 개.
        private const float DiceTileVisible = 140f;
        private const float DiceTilePitch = 184f;
        private const float DiceTileY = 1508f;
        private const float ActionButtonY = 1640f;

        // 좌표는 전부 시안(PSD) 좌표 — 1080x1920, 좌상단 원점 px 다. Pos() 가 화면 중앙 기준 anchoredPosition 으로 바꾼다.
        private const string ArtRoot = "Assets/Resources/Art/";
        private const string PointDatabasePath = "Assets/ScriptableObject/PointMetadataDatabase.asset";

        /// <summary>
        /// 보드(로컬 600x1000)를 시안의 아치 안에 앉히는 <b>가로세로 다른</b> 배율.
        /// 물리(시드 테이블)는 그대로 두고 그림만 맞춘 것이라 핀·공 같은 둥근 것은 역배율로 되돌린다.
        /// 화면 X = 175 + 1.37·x, 화면 Y = 1324.2 − 0.92·y (x, y 는 보드 로컬 px).
        /// </summary>
        private const float BoardScaleX = 1.37f;
        private const float BoardScaleY = 0.92f;
        private static readonly Vector2 BoardOrigin = new Vector2(-365f, -364.2f);

        /// <summary>시안 칸 중심 X(다섯 칸)와 Y. 물리 칸 폭과 달라서 칸 글자 자리를 그림에 맞춰 덮어쓴다.</summary>
        // 아치 그림(243x282)은 4배로 PSD (53,301) 에 놓인다. 칸·구분선 자리는 이 그림의 원본 픽셀 기준이다.
        private const float ArchX = 53f;
        private const float ArchY = 301f;
        private const float ArchW = 972f;
        private const float ArchH = 1128f;
        private const float ArchPixel = 4f;

        // 스프링 그림 자리(시안 좌표). 쏜 공은 이 윗면에서 튀어나온 것처럼 나타난다 — 실제 발사 자리는 이보다 한참 아래(판 바닥)다.
        private const float SpringX = 920f;
        private const float SpringY = 924f;
        private const float SpringW = 56f;
        private const float SpringH = 188f;
        private const int SparkCount = 12;
        private static readonly Color SparkFlash = new Color(1f, 0.93f, 0.6f, 1f);
        private static readonly Color SparkGold = new Color(1f, 0.82f, 0.25f, 1f);

        private static readonly float[] ArtSlotX = { 245f, 390f, 540f, 688f, 838f };
        private const float ArtSlotY = 1310f;

        // 핀·공의 보이는 크기(화면 px). 물리 접촉 반경(가로 ≈35, 세로 ≈23.5)과 그림 사이의 타협이다.
        private const float PegVisible = 34f;
        private const float BallVisibleWidth = 33f;
        private const float RingVisible = 140f;
        private const float RingFillVisible = 120f;
        private const float RingIconScale = 4f;

        // 보상판 특수 핀(반지름 0.4 = 가로 55px)은 링 하나에 보상 아이콘이 들어간다.
        private const float BonusRingVisible = 104f;
        private const float BonusRingFillVisible = 90f;
        private const float ChestVisible = 95f;

        // 특수 핀 아래 글자 자리(핀 중심 기준 화면 px, 위가 +).
        private const float RingGaugeY = -92f;
        private const float RingRewardY = -132f;
        private const float ChestGaugeY = -166f;
        private const float ChestRewardY = -204f;

        // 하단 바의 배율 버튼 격자. 보이는 버튼 폭·높이와 열 중심 X, 행 중심 Y.
        private const float GridButtonWidth = 200f;
        private const float GridButtonHeight = 84f;
        private static readonly float[] GridColumnX = { 315f, 527f, 739f, 951f };
        private static readonly float[] GridRowY = { 1777f, 1869f };

        private static readonly Color ScreenBase = Hex(0x1D1C30);
        private static readonly Color StripLight = Hex(0x3E3F68);
        private static readonly Color RingColor = Hex(0xA2AAFF);
        private static readonly Color RingFillColor = Hex(0xF9FF5F);
        private static readonly Color GaugeTrack = Hex(0x45465F);
        private static readonly Color GaugeGreen = Hex(0x78FE04);
        [MenuItem("OJ/개발/핀볼/페이지 프리팹 굽기")]
        private static void BuildIfMissing() => Build(overwrite: false);

        /// <summary>
        /// 손본 것을 버리고 새로 굽는다. <b>같은 경로에 저장하므로 GUID 가 유지되고</b>,
        /// 그래서 다이얼로그 카탈로그를 다시 훑지 않아도 된다.
        ///
        /// 화면 구조(버튼 구성)를 바꿨을 때 쓴다. 위의 "굽기"는 이미 있으면 아무 일도
        /// 하지 않는데, 그게 <b>조용히 지나가서</b> 바뀐 줄 알고 플레이하는 사고가 한 번 났다.
        /// </summary>
        [MenuItem("OJ/개발/핀볼/페이지 프리팹 다시 굽기 (덮어씀)")]
        private static void Rebuild() => Build(overwrite: true);

        private static void Build(bool overwrite)
        {
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            if (exists && !overwrite)
            {
                Debug.LogWarning(
                    "[핀볼] 페이지 프리팹이 이미 있어 <b>아무것도 하지 않았다</b>: " + PrefabPath +
                    "\n  화면 구조가 바뀌어 다시 구워야 한다면 " +
                    "OJ/개발/핀볼/페이지 프리팹 다시 굽기 (덮어씀) 을 쓸 것.");
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

            // 보상 라운드 몫. <b>없으면 굽지 않는다</b> — 페이지가 이 배선을 전제로 돌고,
            // 반만 구워 두면 "센터핀을 다 채웠는데 아무 일도 안 일어나는" 상태가 된다.
            var bonusBoardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BonusBoardPrefabPath);
            var bonusSeedTable = AssetDatabase.LoadAssetAtPath<SeedTable>(BonusSeedTablePath);
            var bonusBoard = AssetDatabase.LoadAssetAtPath<PinballBoard>(BonusBoardAssetPath);

            if (bonusBoardPrefab == null || bonusSeedTable == null || bonusBoard == null)
            {
                Debug.LogError(
                    "[핀볼] 보상 라운드 에셋이 빠져 페이지를 굽지 않는다." + "\n" +
                    "  판 프리팹: " + (bonusBoardPrefab != null ? "OK" : BonusBoardPrefabPath) + "\n" +
                    "  시드 테이블: " + (bonusSeedTable != null ? "OK" : BonusSeedTablePath) + "\n" +
                    "  판 에셋: " + (bonusBoard != null ? "OK" : BonusBoardAssetPath) + "\n" +
                    "  Tools/Pinball/Sim Lab 에서 보상판을 만들고 Bake 탭에서 구울 것.");
                return;
            }

            TMP_FontAsset font = FindKoreanFont();
            if (font == null)
            {
                Debug.LogError("[핀볼] 한글 TMP 폰트를 못 찾았다. 프리팹을 굽지 않는다.");
                return;
            }

            System.IO.Directory.CreateDirectory(PrefabFolder);
            _artCache.Clear();

            GameObject root = null;
            try
            {
                root = Assemble(boardPrefab, seedTable, font, bonusBoardPrefab, bonusSeedTable, bonusBoard);
                if (root == null)
                    return;

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
                if (!ok)
                {
                    Debug.LogError("[핀볼] 페이지 프리팹 저장에 실패했다: " + PrefabPath);
                    return;
                }
            }
            catch (System.Exception e)
            {
                // 중간에 죽으면 반쯤 만든 오브젝트가 씬에 남지 않게 한다.
                Debug.LogError("[핀볼] 페이지 프리팹을 굽다 멈췄다 — 저장하지 않았다.\n" + e);
                return;
            }
            finally
            {
                if (root != null)
                    Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var baked = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            string next = exists
                // 덮어쓰기는 GUID 를 유지하므로 카탈로그가 이미 이 프리팹을 가리키고 있다.
                ? "  덮어썼다. GUID 가 그대로라 카탈로그는 다시 안 훑어도 된다."
                : "  다음: OJ/개발/다이얼로그 카탈로그/훑어서 갱신 을 돌려야 화면이 열린다.";

            Debug.Log("[핀볼] 페이지 프리팹을 구웠다: " + PrefabPath + "\n" +
                      "  배율 버튼 " + Multipliers.Length + "개 + 발사 버튼 1개\n" + next, baked);
        }

        private static GameObject Assemble(
            GameObject boardPrefab, SeedTable seedTable, TMP_FontAsset font,
            GameObject bonusBoardPrefab, SeedTable bonusSeedTable, PinballBoard bonusBoard)
        {
            GameObject root = NewRect("UIPinballPage", null);
            try
            {
                if (AssembleInto(root, boardPrefab, seedTable, font, bonusBoardPrefab, bonusSeedTable, bonusBoard))
                    return root;
            }
            catch
            {
                Object.DestroyImmediate(root);
                throw;
            }

            Object.DestroyImmediate(root);
            return null;
        }

        private static bool AssembleInto(
            GameObject root, GameObject boardPrefab, SeedTable seedTable, TMP_FontAsset font,
            GameObject bonusBoardPrefab, SeedTable bonusSeedTable, PinballBoard bonusBoard)
        {
            Stretch(root.GetComponent<RectTransform>());

            // dialogView 는 루트가 아니라 자식이어야 한다. DialogBase 가 이것을
            // SetActive 로 껐다 켜는데, 루트를 끄면 페이지 자신의 Awake 가 돌지 않는다.
            Image view = NewImage("View", root.transform, ScreenBase);
            Stretch(view.rectTransform);
            Transform v = view.transform;

            // 하늘, 그리고 위·아래 바. 바 위아래의 두 줄(밝은 띠 + 검은 줄)은 시안의 테두리다.
            PlaceBox(v, "Sky", LoadArt("Pinball/Pinball_Bg"), -2f, -7f, 1093f, 1942f, Vector2.zero);
            NewBand(v, "TopBar", ScreenBase, -400f, 112f);
            NewBand(v, "TopStrip", StripLight, 112f, 117f);
            NewBand(v, "TopLine", Color.black, 117f, 122f);
            NewBand(v, "BottomLine", Color.black, 1710f, 1715f);
            NewBand(v, "BottomStrip", StripLight, 1715f, 1720f);
            NewBand(v, "BottomBar", ScreenBase, 1720f, 2320f);

            // 뒤집히는 부분. 이 아래 두 면이 Y축 180도로 마주 보고 있고, 부모를 돌리면
            // 카드처럼 뒤집힌다. 보상 면이 로컬 180 이라 다 돌았을 때 정확히 정면으로 선다.
            GameObject pivot = NewRect("BoardPivot", v);
            Stretch(pivot.GetComponent<RectTransform>());

            GameObject pinballSide = NewRect("PinballSide", pivot.transform);
            Stretch(pinballSide.GetComponent<RectTransform>());
            Transform side = pinballSide.transform;

            GameObject bonusSide = NewRect("BonusSide", pivot.transform);
            Stretch(bonusSide.GetComponent<RectTransform>());
            bonusSide.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            bonusSide.SetActive(false);

            Image springImage = BuildStage(side);

            // 판. 프리팹 인스턴스로 넣어야 나중에 판을 다시 구웠을 때 따라온다.
            var board = PrefabUtility.InstantiatePrefab(boardPrefab, side) as GameObject;
            if (board == null)
            {
                Debug.LogError("[핀볼] 판 프리팹을 인스턴스로 만들지 못했다.");
                return false;
            }

            board.name = "Board";
            var boardView = board.GetComponent<PinballBoardView>();
            if (boardView == null)
            {
                Debug.LogError("[핀볼] 판 프리팹에 PinballBoardView 가 없다.");
                return false;
            }

            FitBoard(board);
            List<SkinWiring> skins = SkinBoard(board, boardView);

            // 재생기는 판과 같은 오브젝트에 둔다. 판을 지우면 재생기도 같이 사라져야
            // "판이 없는데 공만 굴러가는" 상태가 안 생긴다.
            var playback = board.AddComponent<PinballPlayback>();
            WirePlayback(playback, seedTable, boardView);

            PinballSpringLauncher springLauncher = BuildSpringLauncher(side, springImage, boardView);

            // 발사 버튼. 그림이 64² 안에 작게 들어 있어 투명 여백이 클릭을 먹지 않게 깎는다.
            Art launchArt = LoadArt("Pinball/PinBall_Btn_Normal");
            Image launchImage = PlaceBox(side, "LaunchButton", launchArt, 379f, 1408f, 318f, 246f, Vector2.zero);
            launchImage.raycastTarget = true;
            launchImage.raycastPadding = launchArt.Margins(6f, 6f);
            Button launch = launchImage.gameObject.AddComponent<Button>();
            launch.targetGraphic = launchImage;
            launch.transition = Selectable.Transition.SpriteSwap;
            launch.spriteState = new SpriteState { pressedSprite = LoadArt("Pinball/Pinball_Btn_Prs").sprite };
            launch.navigation = new Navigation { mode = Navigation.Mode.None };

            // 발사 버튼 아래 막대: 보유 티켓이 한 발 값을 채운 정도.
            SetBox(NewFlat(side, "TicketGaugeFrame", Color.black).rectTransform, 373f, 1633f, 356f, 49f, Vector2.zero);
            SetBox(NewFlat(side, "TicketGaugeTrack", GaugeTrack).rectTransform, 377f, 1637f, 348f, 41f, Vector2.zero);
            Image ticketFill = NewFlat(side, "TicketGaugeFill", GaugeGreen);
            SetBox(ticketFill.rectTransform, 377f, 1637f, 348f, 41f, Vector2.zero);
            MakeFilled(ticketFill, Image.FillMethod.Horizontal, 0.5f);
            TMP_Text ticketGaugeText = NewArtText(side, "TicketGaugeText", "1,000/50", 40f, Color.white, font,
                                                  551f, 1660f, 340f, 50f, Vector2.zero);

            // 윗줄 레벨 막대 + 하트 + 구슬.
            PlaceBox(side, "LevelGaugeBg", LoadArt("Pinball/PinBall_Gauge_Bg"), 46f, 154f, 987f, 60f, Vector2.zero);
            Image levelFill = PlaceBox(side, "LevelGaugeFill", LoadArt("Pinball/PinBall_Gauge_Full"),
                                       58f, 165f, 963f, 36f, Vector2.zero);
            MakeFilled(levelFill, Image.FillMethod.Horizontal, 0.58f);
            TMP_Text levelGaugeText = NewArtText(side, "LevelGaugeText", "1,000/50", 40f, Color.white, font,
                                                 228f, 183f, 300f, 50f, Vector2.zero);
            PlaceBox(side, "LevelHeart", LoadArt("Pinball/Pinball_Level"), 29f, 138f, 105f, 87f, Vector2.zero);
            TMP_Text levelText = NewArtText(side, "LevelText", "5", 53f, Color.white, font,
                                            79.5f, 178f, 90f, 70f, Vector2.zero);
            PlaceScaled(side, "LevelOrb", LoadArt("Gem/Gem_Pingball"), 3f, 3f, 1030.5f, 181f, Vector2.zero);

            TMP_Text session = NewArtText(side, "SessionText", "", 30f, Color.white, font,
                                          190f, 1520f, 300f, 44f, Vector2.zero);

            // 배율 버튼. 잠긴 것과 선택된 것은 페이지가 런타임에 구분해 준다.
            var options = new List<MultiplierWiring>();
            for (int i = 0; i < Multipliers.Length; i++)
                options.Add(BuildMultiplierButton(side, font, Multipliers[i], i));

            // 보너스 라운드 안내. 판 오른쪽에 떠 있다가 세션이 끝나면 가운데로 이동한다.
            // <b>핀볼 면에 둔다</b> — 보상 면으로 넘어가는 안내이므로 넘어가기 전에 보여야 한다.
            TMP_Text bannerText = NewArtText(side, "BonusBannerText", "보너스게임 가능", 30f, GoldText, font,
                                             800f, 262f, 420f, 80f, Vector2.zero);
            RectTransform banner = bannerText.rectTransform;
            bannerText.gameObject.SetActive(false);

            UIBonusDicePanel bonusPanel = BuildBonusSide(
                bonusSide.transform, font, bonusBoardPrefab, bonusSeedTable, bonusBoard);

            // 재화 칩과 뒤로. 뒤집히지 않는 바 위에 얹는다.
            List<ChipWiring> chips = BuildChips(v, font);
            Button close = BuildBackButton(v);

            var page = root.AddComponent<UIPinballPage>();
            page.dialogView = view.gameObject;
            WirePage(page, playback, boardView, launch, close, session, levelText, levelFill, levelGaugeText,
                     ticketFill, ticketGaugeText, options, chips, skins);
            WireFlip(page, pivot, pinballSide, bonusSide, bonusPanel, banner, bannerText);

            var pageSo = new SerializedObject(page);
            SetRef(pageSo, "springLauncher", springLauncher);
            pageSo.ApplyModifiedPropertiesWithoutUndo();

            return true;
        }

        /// <summary>
        /// 발사대 연출을 앉힌다. 스프링 그림은 그대로 두고 눌렸다 튕기게 하는 컴포넌트와, 공이 나타나는 순간의
        /// 번쩍임·불꽃(꺼진 채로 풀)을 만든다. 판에는 "스프링 윗면에 오를 때까지 공을 숨겨라" 를 건다 —
        /// 실제 발사 자리는 그림 아래 판 바닥이라, 숨기지 않으면 공이 스프링과 상관없이 아래에서 올라온다.
        /// </summary>
        private static PinballSpringLauncher BuildSpringLauncher(Transform side, Image spring, PinballBoardView boardView)
        {
            // 그림 칸에는 투명 여백이 있어 칸 가운데 피벗으로는 눌릴 때 보이는 아래 끝이 들뜬다.
            // 피벗을 보이는 아래 끝 가운데로 옮겨(보이는 자리는 그대로) 스케일만으로 눌리게 한다.
            RectTransform springRect = spring.rectTransform;
            Vector2 springCenter = springRect.anchoredPosition;
            Vector2 springSize = springRect.sizeDelta;
            Vector2 springFoot = Pos(SpringX + SpringW * 0.5f, SpringY + SpringH);
            springRect.pivot = new Vector2(0.5f + (springFoot.x - springCenter.x) / springSize.x,
                                           0.5f + (springFoot.y - springCenter.y) / springSize.y);
            springRect.anchoredPosition = springFoot;

            GameObject fx = NewRect("SpringFx", side);
            Stretch(fx.GetComponent<RectTransform>());

            // 번쩍임: 스프링 윗면에서 위로 뻗는 가는 막대. 피벗이 아래 가운데라 윗면 좌표에 그대로 앉는다.
            Image flash = NewFlat(fx.transform, "Flash", SparkFlash);
            RectTransform flashRect = flash.rectTransform;
            flashRect.anchorMin = new Vector2(0.5f, 0.5f);
            flashRect.anchorMax = new Vector2(0.5f, 0.5f);
            flashRect.pivot = new Vector2(0.5f, 0f);
            flashRect.sizeDelta = new Vector2(22f, 140f);
            flash.gameObject.SetActive(false);

            var sparks = new RectTransform[SparkCount];
            for (int i = 0; i < SparkCount; i++)
            {
                Image spark = NewFlat(fx.transform, "Spark" + i, i % 3 == 0 ? Color.white : SparkGold);
                float size = 9f + (i % 3) * 3f;
                SetRect(spark.rectTransform, new Vector2(size, size), Vector2.zero);
                spark.gameObject.SetActive(false);
                sparks[i] = spark.rectTransform;
            }

            var launcher = fx.AddComponent<PinballSpringLauncher>();
            var so = new SerializedObject(launcher);
            SetRef(so, "boardView", boardView);
            SetRef(so, "spring", springRect);
            SetRef(so, "flash", flashRect);
            so.FindProperty("springHeight").floatValue = SpringH;

            SerializedProperty list = FindList(so, "sparks");
            if (list != null)
            {
                list.arraySize = sparks.Length;
                for (int i = 0; i < sparks.Length; i++)
                    list.GetArrayElementAtIndex(i).objectReferenceValue = sparks[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            // 스프링 윗면의 보드 y(유닛). 화면 Y = (960 - BoardOrigin.y) - BoardScaleY * 보드 로컬 px.
            float springTopLocalPx = (960f - BoardOrigin.y - SpringY) / BoardScaleY;
            var viewSo = new SerializedObject(boardView);
            viewSo.FindProperty("concealLaunch").boolValue = true;
            viewSo.FindProperty("launchRevealY").floatValue = springTopLocalPx / boardView.pixelsPerUnit;
            viewSo.ApplyModifiedPropertiesWithoutUndo();

            return launcher;
        }

        // ──────────────────────────────────────────────── 판 입히기

        /// <summary>
        /// 면 하나의 무대: 바닥 · 아치 · 스프링. 두 면이 같은 그림을 쓴다.
        /// 가운데 분홍 칸은 옆 칸 그림으로 덮는다 — 다섯 칸이 모두 같은 확률(20%)이라 분홍만 튀면 "저기가 특별한가" 로 읽힌다.
        /// </summary>
        private static Image BuildStage(Transform side)
        {
            // 바닥은 아래 바 밑으로 내려가는 그림이라 바 윗선(1710)에서 자른다.
            // 바를 판 위로 올리면 보상 면의 버튼이 가려져서 마스크로 푼다.
            GameObject clipObject = NewRect("FloorClip", side);
            RectTransform clip = clipObject.GetComponent<RectTransform>();
            SetRect(clip, new Vector2(1300f, 1710f), new Vector2(0f, 105f));
            clipObject.AddComponent<RectMask2D>();
            PlaceBox(clip, "Floor", LoadArt("Pinball/Pinball_Bg_Bottom"), -89f, 961f, 1281f, 773f, clip.anchoredPosition);

            Art arch = LoadArt("Pinball/Pinball_Bg_Center");
            PlaceBox(side, "Arch", arch, ArchX, ArchY, ArchW, ArchH, Vector2.zero);

            // 아치 그림 한 장을 칸 하나 폭(원본 37px = 화면 148px)만큼 옮겨 겹치고, 분홍 칸 사각형만 남긴다.
            // 사방 1px 넓혀 검은 테두리까지 덮는다 — 안 그러면 확대 샘플링 때 가장자리에 분홍이 번진다(이웃 칸도 같은 테두리다).
            GameObject patch = NewRect("CenterCupPatch", side);
            RectTransform patchRect = patch.GetComponent<RectTransform>();
            SetBox(patchRect, ArchX + 104f * ArchPixel, ArchY + 230f * ArchPixel,
                   33f * ArchPixel, 44f * ArchPixel, Vector2.zero);
            patch.AddComponent<RectMask2D>();
            PlaceBox(patch.transform, "ArchCopy", arch, ArchX - 37f * ArchPixel, ArchY, ArchW, ArchH,
                     patchRect.anchoredPosition);

            return PlaceBox(side, "Spring", LoadArt("Pinball/Pinball_Spring"), SpringX, SpringY, SpringW, SpringH, Vector2.zero);
        }

        /// <summary>특수 핀 하나의 장식 배선. <see cref="UIPinballPage.SpecialSkin"/> 으로 옮겨 적는다.</summary>
        private sealed class SkinWiring
        {
            public int pegIndex;
            public int tag;
            public Image ringFill;
            public readonly List<BarWiring> bars = new List<BarWiring>();
            public float gaugeLabelY;
            public float rewardRowY;
        }

        private struct BarWiring
        {
            public Image image;
            public Sprite normal;
            public Sprite lit;
            public int percent;
        }

        /// <summary>
        /// 판(600x1000)을 시안의 아치 안에 앉힌다. 물리는 그대로고 그림만 가로세로 다른 배율로 늘린 것이다.
        /// 굽는 그림(배경·칸·벽·구분선)은 시안 그림으로 대신하므로 끈다.
        /// </summary>
        private static void FitBoard(GameObject board)
        {
            var rect = board.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            // pivot 을 먼저. 좌하단 기준이어야 BoardOrigin 이 "보드 로컬 (0,0) 의 화면 자리"가 된다.
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = BoardOrigin;
            rect.localScale = new Vector3(BoardScaleX, BoardScaleY, 1f);
            Mark(rect);

            foreach (string childName in new[] { "Background", "Slots", "Walls", "Dividers" })
            {
                Transform child = board.transform.Find(childName);
                if (child == null)
                {
                    Debug.LogWarning("[핀볼] 판 프리팹에 '" + childName + "' 가 없다 — 끄지 못했다.");
                    continue;
                }

                child.gameObject.SetActive(false);
                Mark(child.gameObject);
            }
        }

        /// <summary>
        /// 핀·공·칸에 시안 그림을 입힌다. 둥근 것은 판의 가로세로 배율을 역배율로 되돌려 찌그러지지 않게 한다 —
        /// 그래서 이 안의 크기는 전부 <b>화면 px</b> 이다.
        /// </summary>
        private static List<SkinWiring> SkinBoard(GameObject board, PinballBoardView boardView)
        {
            PinballBoard data = boardView.board;
            if (data == null || data.pegs == null || boardView.pegs == null)
                throw new System.InvalidOperationException("[핀볼] 판 프리팹의 board/pegs 가 비어 있다.");

            var counter = new Vector3(1f / BoardScaleX, 1f / BoardScaleY, 1f);

            // 칸: 시안의 칸 중심에 맞춘다. 칸 글자는 런타임에 이 자리 위에 올라간다.
            RectTransform[] slots = boardView.slots;
            for (int i = 0; slots != null && i < slots.Length && i < ArtSlotX.Length; i++)
            {
                if (slots[i] == null)
                    continue;

                slots[i].anchoredPosition = new Vector2(
                    (ArtSlotX[i] - (BoardOrigin.x + 540f)) / BoardScaleX,
                    (1324.2f - ArtSlotY) / BoardScaleY);
                Mark(slots[i]);
            }

            Art stone = LoadArt("Pinball/Pinball_Ston");
            Art ring = LoadArt("Pinball/PinBall_MiniRewardGauge");
            Art chest = LoadArt("Main/Icon_Reward_Chest_2");
            Art star = LoadArt("Pinball/PinBall_Star");
            Art moon = LoadArt("Pinball/pinball_Moon");

            var skins = new List<SkinWiring>();
            for (int i = 0; i < boardView.pegs.Length; i++)
            {
                RectTransform rt = boardView.pegs[i];
                var image = rt != null ? rt.GetComponent<Image>() : null;
                if (image == null)
                    continue;

                int tag = i < data.pegs.Length ? data.pegs[i].specialTag : 0;
                rt.localScale = counter;
                image.color = Color.white;
                image.raycastTarget = false;
                image.type = Image.Type.Simple;
                image.preserveAspect = false;

                if (tag == 1 || tag == 2)
                {
                    image.sprite = ring.sprite;
                    image.color = RingColor;
                    SizeByVisible(rt, ring, RingVisible);

                    Image fill = NewChildImage(rt, "Fill", ring.sprite, RingFillColor);
                    SizeByVisible(fill.rectTransform, ring, RingFillVisible);
                    MakeFilled(fill, Image.FillMethod.Radial360, 0f);
                    fill.fillOrigin = (int)Image.Origin360.Top;
                    fill.fillClockwise = true;

                    Art glyph = tag == 1 ? star : moon;
                    Image icon = NewChildImage(rt, "Icon", glyph.sprite, Color.white);
                    icon.rectTransform.sizeDelta = new Vector2(glyph.width * RingIconScale, glyph.height * RingIconScale);

                    skins.Add(new SkinWiring
                    {
                        pegIndex = i, tag = tag, ringFill = fill,
                        gaugeLabelY = RingGaugeY, rewardRowY = RingRewardY,
                    });
                }
                else if (tag == 3)
                {
                    image.sprite = chest.sprite;
                    SizeByVisible(rt, chest, ChestVisible);

                    var skin = new SkinWiring
                    {
                        pegIndex = i, tag = tag, gaugeLabelY = ChestGaugeY, rewardRowY = ChestRewardY,
                    };
                    // 아치 판 셋. 상자를 둘러싸는 ⌒(위 둘) + ⌣(아래) 우리다. 자리·크기는 PSD 실루엣이고(PinballPsdLayout 의 벽과
                    // 같은 표), 물리 벽이 이 그림을 따라간다. 그림 이름과 자리가 엇갈린다 — 안쪽 끝이 위로 솟는 ⌒ 이 되려면
                    // 왼쪽 자리에 Pinball_Right, 오른쪽 자리에 Pinball_Left 가 앉아야 한다.
                    // 판은 벽처럼 가만히 있어야 해서 상자 핀(맞으면 커졌다 줄어든다) 자식이 아니라 같은 높이의 따로 만든 묶음에 둔다.
                    RectTransform cage = NewRect("ChestCage", rt.parent).GetComponent<RectTransform>();
                    cage.anchorMin = rt.anchorMin;
                    cage.anchorMax = rt.anchorMax;
                    cage.pivot = new Vector2(0.5f, 0.5f);
                    cage.sizeDelta = Vector2.zero;
                    cage.anchoredPosition = rt.anchoredPosition;
                    cage.localScale = counter;
                    cage.SetSiblingIndex(rt.GetSiblingIndex() + 1);
                    Mark(cage);

                    skin.bars.Add(NewChestBar(cage, "BarLeft", "Pinball/Pinball_Right", 116f, -82f, 80.5f, 34));
                    skin.bars.Add(NewChestBar(cage, "BarRight", "Pinball/Pinball_Left", 116f, 90f, 79f, 67));
                    skin.bars.Add(NewChestBar(cage, "BarBottom", "Pinball/Pinball_Bottom", 214f, -1f, -105f, 100));
                    skins.Add(skin);
                }
                else
                {
                    image.sprite = stone.sprite;
                    SizeByVisible(rt, stone, PegVisible);
                }

                Mark(rt);
                Mark(image);
            }

            SkinBallAndLit(boardView, counter);
            return skins;
        }

        /// <summary>맞은 핀의 그림과 공 그림. 두 판이 같은 것을 쓴다.</summary>
        private static void SkinBallAndLit(PinballBoardView boardView, Vector3 counter)
        {
            var boardSo = new SerializedObject(boardView);
            SetRef(boardSo, "pegLitSprite", LoadArt("Pinball/Pinball_Ston_Prs").sprite);
            boardSo.ApplyModifiedPropertiesWithoutUndo();

            RectTransform ball = boardView.ball;
            var ballImage = ball != null ? ball.GetComponent<Image>() : null;
            if (ballImage != null)
            {
                Art gem = LoadArt("Gem/Gem_Pingball");
                ballImage.sprite = gem.sprite;
                ballImage.color = Color.white;
                ballImage.raycastTarget = false;
                SizeByVisible(ball, gem, BallVisibleWidth);
                ball.localScale = counter;
                Mark(ball);
                Mark(ballImage);
            }
        }

        /// <summary>
        /// 보상판 핀 입히기. 일반 핀은 돌, 특수 핀 여섯은 링 게이지(채움은 패널이 런타임에 돌린다).
        /// 링 안의 보상 아이콘도 라운드마다 풀에서 뽑히므로 패널이 얹는다 — 여기서는 링과 채움만 만든다.
        /// </summary>
        private static void SkinBonusBoard(PinballBoardView boardView)
        {
            PinballBoard data = boardView.board;
            if (data == null || data.pegs == null || boardView.pegs == null)
                throw new System.InvalidOperationException("[핀볼] 보상판 프리팹의 board/pegs 가 비어 있다.");

            var counter = new Vector3(1f / BoardScaleX, 1f / BoardScaleY, 1f);
            Art stone = LoadArt("Pinball/Pinball_Ston");
            Art ring = LoadArt("Pinball/PinBall_MiniRewardGauge");

            for (int i = 0; i < boardView.pegs.Length; i++)
            {
                RectTransform rt = boardView.pegs[i];
                var image = rt != null ? rt.GetComponent<Image>() : null;
                if (image == null)
                    continue;

                int tag = i < data.pegs.Length ? data.pegs[i].specialTag : 0;
                rt.localScale = counter;
                image.color = Color.white;
                image.raycastTarget = false;
                image.type = Image.Type.Simple;
                image.preserveAspect = false;

                if (tag != 0)
                {
                    image.sprite = ring.sprite;
                    image.color = RingColor;
                    SizeByVisible(rt, ring, BonusRingVisible);

                    Image fill = NewChildImage(rt, "Fill", ring.sprite, RingFillColor);
                    SizeByVisible(fill.rectTransform, ring, BonusRingFillVisible);
                    MakeFilled(fill, Image.FillMethod.Radial360, 0f);
                    fill.fillOrigin = (int)Image.Origin360.Top;
                    fill.fillClockwise = true;
                }
                else
                {
                    image.sprite = stone.sprite;
                    SizeByVisible(rt, stone, PegVisible);
                }

                Mark(rt);
                Mark(image);
            }

            SkinBallAndLit(boardView, counter);
        }

        private static BarWiring NewChestBar(
            RectTransform holder, string name, string pathPrefix, float visibleWidth, float x, float yUp, int percent)
        {
            Art normal = LoadArt(pathPrefix + "_Normal");
            Art lit = LoadArt(pathPrefix + "_Prs");
            Image image = PlaceRelative(holder, name, normal, visibleWidth, x, yUp);
            return new BarWiring { image = image, normal = normal.sprite, lit = lit.sprite, percent = percent };
        }

        /// <summary>그림의 <b>보이는 폭</b>이 visibleWidth 가 되도록 크기를 준다. 가로세로 같은 배율.</summary>
        private static void SizeByVisible(RectTransform rect, Art art, float visibleWidth)
        {
            float s = visibleWidth / art.VisW;
            rect.sizeDelta = new Vector2(art.width * s, art.height * s);
        }

        /// <summary>프리팹 인스턴스에 손댄 것이 오버라이드로 남게 한다.</summary>
        private static void Mark(Object target)
        {
            if (target != null && PrefabUtility.IsPartOfPrefabInstance(target))
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }

        /// <summary>
        /// <c>UIPinballPage</c> 의 전환 배선. 여기가 비면 센터핀을 다 채워도 판이 안 돈다.
        /// </summary>
        private static void WireFlip(
            UIPinballPage page, GameObject pivot, GameObject pinballSide,
            GameObject bonusSide, UIBonusDicePanel bonusPanel,
            RectTransform banner, TMP_Text bannerText)
        {
            var so = new SerializedObject(page);
            SetRef(so, "boardPivot", pivot.GetComponent<RectTransform>());
            SetRef(so, "pinballSideRoot", pinballSide);
            SetRef(so, "bonusSideRoot", bonusSide);
            SetRef(so, "bonusPanel", bonusPanel);
            SetRef(so, "bonusBanner", banner);
            SetRef(so, "bonusBannerText", bannerText);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 보상 라운드 면을 조립한다. 핀볼 면과 같은 무대(바닥·아치·스프링) 위에 보상판을 같은 배율로 앉히고,
        /// 윗줄에는 족보 막대, 아치 아래에는 주사위·버튼을 둔다. 광고·전액 버튼은 아래 바 오른쪽 빈 자리다.
        ///
        /// 핀 위 보상 아이콘·수량·진행도는 라운드마다 값이 달라 <see cref="UIBonusDicePanel"/> 이 런타임에 만든다.
        /// </summary>
        private static UIBonusDicePanel BuildBonusSide(
            Transform parent, TMP_FontAsset font,
            GameObject bonusBoardPrefab, SeedTable bonusSeedTable, PinballBoard bonusBoard)
        {
            BuildStage(parent);

            var board = PrefabUtility.InstantiatePrefab(bonusBoardPrefab, parent) as GameObject;
            if (board == null)
            {
                Debug.LogError("[핀볼] 보상판 프리팹을 인스턴스로 만들지 못했다.");
                return null;
            }

            board.name = "BonusBoard";
            var boardView = board.GetComponent<PinballBoardView>();
            if (boardView == null)
            {
                Debug.LogError("[핀볼] 보상판 프리팹에 PinballBoardView 가 없다.");
                return null;
            }

            // 핀볼 면과 같은 매핑(보드 → 화면). 보상판도 같은 6x10 이라 아치에 그대로 들어간다.
            FitBoard(board);
            SkinBonusBoard(boardView);

            var playback = board.AddComponent<PinballPlayback>();
            WirePlayback(playback, bonusSeedTable, boardView);

            // 패널은 판과 형제로 둔다. 판을 다시 구워 교체해도 UI 배선이 안 끊긴다.
            GameObject panelRoot = NewRect("BonusPanel", parent);
            Stretch(panelRoot.GetComponent<RectTransform>());
            var panel = panelRoot.AddComponent<UIBonusDicePanel>();
            Transform p = panelRoot.transform;

            // 윗줄: 핀볼 면의 레벨 막대 자리에 족보 막대.
            PlaceBox(p, "HandPlate", LoadArt("Pinball/PinBall_Gauge_Bg"), 46f, 154f, 987f, 60f, Vector2.zero);
            PlaceScaled(p, "HandOrb", LoadArt("Gem/Gem_Pingball"), 3f, 3f, 1030.5f, 181f, Vector2.zero);
            TMP_Text hand = NewArtText(p, "HandText", "", 40f, GoldText, font, 540f, 183f, 900f, 50f, Vector2.zero);

            TMP_Text cycle = NewArtText(p, "CycleText", "", 30f, Color.white, font, 300f, 262f, 520f, 44f, Vector2.zero);
            TMP_Text reroll = NewArtText(p, "RerollText", "", 30f, Color.white, font, 800f, 262f, 400f, 44f, Vector2.zero);

            // 핀 진행도는 여기서 만들지 않는다 — 패널이 판 위의 핀 좌표를 읽어
            // 런타임에 핀마다 직접 붙인다(UIBonusDicePanel.BuildPinLabels).

            // 주사위 5칸. 눌러서 잠그고, 잠긴 것은 리롤에서 빠진다.
            var diceSlots = new List<DiceWiring>();
            for (int i = 0; i < DiceCount; i++)
                diceSlots.Add(BuildDiceSlot(p, font, i, 540f + (i - (DiceCount - 1) * 0.5f) * DiceTilePitch, DiceTileY));

            Button rerollButton = NewKitButton(
                p, "RerollButton", "다시 굴리기", "InfinityMode/Btn_PurpleGray",
                300f, ActionButtonY, 420f, 92f, 40f, font, out _, out _);

            Button shootButton = NewKitButton(
                p, "ShootButton", "발사", "Ingame/Big_Btn_Yellow",
                780f, ActionButtonY, 420f, 92f, 44f, font, out _, out _);

            // 이 둘은 조건이 맞을 때만 패널이 켠다. 꺼 둔 채로 굽는다 —
            // 광고가 없는데 버튼이 보이면 "광고가 안 나온다"는 고장으로 읽힌다.
            // 자리는 뒤로 버튼 오른쪽, 핀볼 면에서 배율 버튼이 있던 바다.
            Button adButton = NewKitButton(
                p, "AdButton", "광고 보고 한 번 더", "Ingame/Big_Btn_Green",
                430f, 1822f, 400f, 92f, 32f, font, out _, out _);
            adButton.gameObject.SetActive(false);

            Button claimAllButton = NewKitButton(
                p, "ClaimAllButton", "바로 전액 받기", "Ingame/Big_Btn_Yellow",
                840f, 1822f, 400f, 92f, 32f, font, out _, out _);
            claimAllButton.gameObject.SetActive(false);

            WireBonusPanel(panel, playback, boardView, bonusSeedTable, bonusBoard,
                           diceSlots, rerollButton, shootButton, adButton, claimAllButton,
                           hand, cycle, reroll);

            return panel;
        }

        /// <summary>주사위 한 칸에 필요한 참조 묶음.</summary>
        private struct DiceWiring
        {
            public Button button;
            public TMP_Text label;
            public GameObject lockMark;
        }

        private static DiceWiring BuildDiceSlot(
            Transform parent, TMP_FontAsset font, int index, float cx, float cy)
        {
            Button button = NewKitButton(
                parent, "Dice" + index, "1", "InfinityMode/Btn_PurpleGray",
                cx, cy, DiceTileVisible, DiceTileVisible, 76f, font, out TMP_Text label, out Vector2 size);

            // 잠긴 칸은 노란 판을 덮고 자물쇠를 붙인다. 색만으로 구분하지 않게 한다. 꺼 둔 채로 굽고 패널이 켠다.
            Image mark = NewSliced(button.transform, "Locked", LoadSprite("Ingame/Big_Btn_Yellow"), 3f,
                                   size, Vector2.zero);
            mark.raycastTarget = false;
            mark.transform.SetAsFirstSibling();

            Image lockIcon = NewChildImage(mark.rectTransform, "Icon", LoadSprite("Upgrade/Icon_lock"), Color.white);
            lockIcon.preserveAspect = true;
            SetRect(lockIcon.rectTransform, new Vector2(64f, 64f), new Vector2(size.x * 0.5f - 22f, size.y * 0.5f - 20f));
            mark.gameObject.SetActive(false);

            return new DiceWiring { button = button, label = label, lockMark = mark.gameObject };
        }

        /// <summary>
        /// 킷 버튼 판(9슬라이스 · 배율 3). 보이는 사각형의 중심·크기를 시안 좌표로 준다.
        /// 눌러서 비활성이 되는 버튼은 회색 틴트로 죽는다.
        /// </summary>
        private static Button NewKitButton(
            Transform parent, string name, string label, string spritePath,
            float visibleCenterX, float visibleCenterY, float visibleWidth, float visibleHeight,
            float fontSize, TMP_FontAsset font, out TMP_Text caption, out Vector2 size)
        {
            ButtonRect(visibleCenterX, visibleCenterY, visibleWidth, visibleHeight, 3f,
                       out size, out Vector2 position);
            Image image = NewSliced(parent, name, LoadSprite(spritePath), 3f, size, position);

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = DisabledGray(button.colors);

            caption = NewText("Text", image.transform, label, fontSize, Color.white, font);
            NoWrap(caption);
            SetRect(caption.rectTransform, new Vector2(size.x - 20f, size.y - 24f), new Vector2(0f, 2f));
            return button;
        }

        private static void WireBonusPanel(
            UIBonusDicePanel panel, PinballPlayback playback, PinballBoardView boardView,
            SeedTable seedTable, PinballBoard board, List<DiceWiring> dice,
            Button reroll, Button shoot, Button ad, Button claimAll,
            TMP_Text hand, TMP_Text cycle, TMP_Text rerollText)
        {
            var so = new SerializedObject(panel);
            SetRef(so, "playback", playback);
            SetRef(so, "boardView", boardView);
            SetRef(so, "seedTable", seedTable);
            SetRef(so, "board", board);
            SetRef(so, "rerollButton", reroll);
            SetRef(so, "shootButton", shoot);
            SetRef(so, "adButton", ad);
            SetRef(so, "claimAllButton", claimAll);
            SetRef(so, "handText", hand);
            SetRef(so, "cycleText", cycle);
            SetRef(so, "rerollText", rerollText);

            SerializedProperty list = so.FindProperty("diceSlots");
            if (list == null)
            {
                Debug.LogError(
                    "[핀볼] UIBonusDicePanel.diceSlots 를 못 찾았다. 이름이 바뀌었는지 확인할 것.");
            }
            else
            {
                list.arraySize = dice.Count;
                for (int i = 0; i < dice.Count; i++)
                {
                    SerializedProperty entry = list.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("button").objectReferenceValue = dice[i].button;
                    entry.FindPropertyRelative("label").objectReferenceValue = dice[i].label;
                    entry.FindPropertyRelative("lockMark").objectReferenceValue = dice[i].lockMark;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }
        // ──────────────────────────────────────────────── 시안 그림 배치

        /// <summary>
        /// 스프라이트 하나와 그 <b>보이는 사각형</b>(알파 8 초과). 킷 그림은 투명 여백이 있어 시안에서 잰 보이는 크기에서
        /// Image 크기를 거꾸로 구해야 한다. y 는 위가 0 이다.
        /// </summary>
        private sealed class Art
        {
            public Sprite sprite;
            public int width, height;
            public int x0, x1, yTop0, yTop1;

            public float VisW => x1 - x0;
            public float VisH => yTop1 - yTop0;
            public float VisCx => (x0 + x1) * 0.5f;
            public float VisCy => (yTop0 + yTop1) * 0.5f;

            /// <summary>투명 여백(좌, 하, 우, 상)을 화면 px 로. <c>Graphic.raycastPadding</c> 이 이 순서다.</summary>
            public Vector4 Margins(float sx, float sy)
            {
                return new Vector4(x0 * sx, (height - yTop1) * sy, (width - x1) * sx, yTop0 * sy);
            }
        }

        private static readonly Dictionary<string, Art> _artCache = new Dictionary<string, Art>();

        /// <summary>
        /// 스프라이트를 읽는다. <b>없으면 멈춘다.</b> 굽기가 null 스프라이트로 끝나면
        /// 흰 사각형만 가득한 프리팹이 조용히 저장되고, 그것은 실행해 보기 전에는 모른다.
        /// </summary>
        private static Sprite LoadSprite(string pathUnderArt)
        {
            string path = ArtRoot + pathUnderArt + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                throw new System.InvalidOperationException(
                    "[핀볼 굽기] 스프라이트가 없다(파일이 없거나 Sprite 로 임포트되지 않았다): " + path);
            }

            return sprite;
        }

        private static Art LoadArt(string pathUnderArt)
        {
            if (_artCache.TryGetValue(pathUnderArt, out Art cached))
                return cached;

            Art art = Measure(LoadSprite(pathUnderArt));
            _artCache[pathUnderArt] = art;
            return art;
        }

        /// <summary>스프라이트가 놓인 파일에서 보이는 사각형을 잰다. 아틀라스 조각이 아니라 한 장짜리라고 가정한다.</summary>
        private static Art Measure(Sprite sprite)
        {
            string assetPath = AssetDatabase.GetAssetPath(sprite);
            string path = System.IO.Path.GetFullPath(assetPath);

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                texture.LoadImage(System.IO.File.ReadAllBytes(path));
                int w = texture.width;
                int h = texture.height;
                if ((int)sprite.rect.width != w || (int)sprite.rect.height != h)
                {
                    throw new System.InvalidOperationException(
                        "[핀볼 굽기] 스프라이트 크기가 파일과 다르다(임포트 최대 크기에 눌렸는지 확인): " + assetPath);
                }

                Color32[] pixels = texture.GetPixels32();
                int minX = w, maxX = -1, minRow = h, maxRow = -1;
                for (int row = 0; row < h; row++)
                {
                    for (int col = 0; col < w; col++)
                    {
                        if (pixels[row * w + col].a <= 8)
                            continue;

                        if (col < minX) minX = col;
                        if (col > maxX) maxX = col;
                        if (row < minRow) minRow = row;
                        if (row > maxRow) maxRow = row;
                    }
                }

                if (maxX < 0)
                    throw new System.InvalidOperationException("[핀볼 굽기] 보이는 픽셀이 없다: " + assetPath);

                // 텍스처 행은 아래가 0 이다. 위가 0 인 좌표로 바꾼다.
                return new Art
                {
                    sprite = sprite, width = w, height = h,
                    x0 = minX, x1 = maxX + 1,
                    yTop0 = h - 1 - maxRow, yTop1 = h - minRow,
                };
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        /// <summary>
        /// 그림의 보이는 사각형이 (visCx, visCy) 를 중심으로 sx·sy 배가 되도록 놓는다.
        /// 중심은 시안 좌표(좌상단 원점)다. parentOffset 은 부모 중심의 화면 중앙 기준 위치.
        /// </summary>
        private static Image PlaceScaled(
            Transform parent, string name, Art art, float sx, float sy,
            float visCx, float visCy, Vector2 parentOffset)
        {
            Image image = NewImage(name, parent, Color.white);
            image.sprite = art.sprite;
            image.raycastTarget = false;

            float w = art.width * sx;
            float h = art.height * sy;
            float centerX = visCx + (w * 0.5f - art.VisCx * sx);
            float centerY = visCy + (h * 0.5f - art.VisCy * sy);
            SetRect(image.rectTransform, new Vector2(w, h), Pos(centerX, centerY) - parentOffset);
            return image;
        }

        /// <summary>보이는 사각형이 정확히 (bx, by, bw, bh) 를 덮게 놓는다. 가로세로 배율이 달라도 맞춘다.</summary>
        private static Image PlaceBox(
            Transform parent, string name, Art art, float bx, float by, float bw, float bh, Vector2 parentOffset)
        {
            return PlaceScaled(parent, name, art, bw / art.VisW, bh / art.VisH,
                               bx + bw * 0.5f, by + bh * 0.5f, parentOffset);
        }

        /// <summary>
        /// 둥근 핀의 자식으로 붙인다. 부모는 역배율이라 여기 좌표는 화면 px, 위가 +, 부모 중심이 원점이다.
        /// 보이는 폭이 visibleWidth 가 되게 가로세로 같은 배율로 키운다.
        /// </summary>
        private static Image PlaceRelative(
            RectTransform parent, string name, Art art, float visibleWidth, float x, float yUp)
        {
            Image image = NewImage(name, parent, Color.white);
            image.sprite = art.sprite;
            image.raycastTarget = false;

            float s = visibleWidth / art.VisW;
            float w = art.width * s;
            float h = art.height * s;
            SetRect(image.rectTransform, new Vector2(w, h),
                    new Vector2(x + (w * 0.5f - art.VisCx * s), yUp - (h * 0.5f - art.VisCy * s)));
            return image;
        }

        private static Image NewChildImage(RectTransform parent, string name, Sprite sprite, Color color)
        {
            Image image = NewImage(name, parent, color);
            image.sprite = sprite;
            image.raycastTarget = false;
            image.rectTransform.anchoredPosition = Vector2.zero;
            return image;
        }

        /// <summary>9슬라이스 판. 테두리는 스프라이트 .meta 의 것을 쓰고 여기서는 배율만 준다.</summary>
        private static Image NewSliced(
            Transform parent, string name, Sprite sprite, float pixelScale, Vector2 size, Vector2 position)
        {
            Image image = NewImage(name, parent, Color.white);
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.pixelsPerUnitMultiplier = 1f / pixelScale;
            SetRect(image.rectTransform, size, position);
            return image;
        }

        /// <summary>단색 사각형. null 스프라이트 Image 는 Filled 가 안 먹어서 흰 점 한 장을 늘려 쓴다.</summary>
        private static Image NewFlat(Transform parent, string name, Color color)
        {
            Image image = NewImage(name, parent, color);
            image.sprite = LoadSprite("Pinball/FromPsd/Pixel_White");
            image.raycastTarget = false;
            return image;
        }

        /// <summary>화면 폭을 가로로 꽉 채우는 띠. 시안 Y(위가 0) 의 [yTop0, yTop1).</summary>
        private static void NewBand(Transform parent, string name, Color color, float yTop0, float yTop1)
        {
            RectTransform rect = NewFlat(parent, name, color).rectTransform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(0f, yTop1 - yTop0);
            rect.anchoredPosition = new Vector2(0f, 960f - (yTop0 + yTop1) * 0.5f);
        }

        private static void MakeFilled(Image image, Image.FillMethod method, float amount)
        {
            image.type = Image.Type.Filled;
            image.fillMethod = method;
            image.fillOrigin = 0;
            image.fillAmount = amount;
        }

        private static void SetBox(RectTransform rect, float x, float y, float w, float h, Vector2 parentOffset)
        {
            SetRect(rect, new Vector2(w, h), Pos(x + w * 0.5f, y + h * 0.5f) - parentOffset);
        }

        /// <summary>시안에서 잰 글자 중심(좌상단 원점)에 놓는다. 한 줄이고 넘쳐도 줄바꿈하지 않는다.</summary>
        private static TMP_Text NewArtText(
            Transform parent, string name, string text, float size, Color color, TMP_FontAsset font,
            float cx, float cy, float width, float height, Vector2 parentOffset)
        {
            TMP_Text label = NewText(name, parent, text, size, color, font);
            NoWrap(label);
            SetRect(label.rectTransform, new Vector2(width, height), Pos(cx, cy) - parentOffset);
            return label;
        }

        private static void NoWrap(TMP_Text label)
        {
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
        }

        /// <summary>
        /// 킷 버튼(Btn_Gray · Big_Btn_Yellow · Btn_PurpleGray)의 보이는 사각형 → Image 크기·중심.
        /// 여백이 위(6px)가 아래(2px)보다 커서 중심이 (6-2)/2 x 배율 만큼 위로 간다.
        /// </summary>
        private static void ButtonRect(
            float visibleCenterX, float visibleCenterY, float visibleWidth, float visibleHeight, float pixelScale,
            out Vector2 size, out Vector2 position)
        {
            size = new Vector2(visibleWidth + 8f * pixelScale, visibleHeight + 8f * pixelScale);
            position = Pos(visibleCenterX, visibleCenterY - 2f * pixelScale);
        }

        /// <summary>시안 좌표(좌상단 원점, 1080x1920) → 화면 중앙 기준 anchoredPosition.</summary>
        private static Vector2 Pos(float designX, float designY)
        {
            return new Vector2(designX - 540f, 960f - designY);
        }

        private static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }

        // ──────────────────────────────────────────────── 위·아래 바

        /// <summary>상단 재화 칩 하나에 필요한 참조 묶음.</summary>
        private sealed class ChipWiring
        {
            public PointType type;
            public TMP_Text text;
            public Image icon;
        }

        /// <summary>
        /// 상단 재화 칩 셋. 시안에는 칩 틀과 숫자뿐이라 어떤 재화인지는 <b>추정</b>이다 —
        /// 스태미나·골드·무료 보석. 아이콘은 재화 표의 것을 얹었다.
        /// </summary>
        private static List<ChipWiring> BuildChips(Transform parent, TMP_FontAsset font)
        {
            Art frame = LoadArt("Pinball/FromPsd/Pinball_Chip");
            var database = AssetDatabase.LoadAssetAtPath<OJ.Point.PointMetadataDatabase>(PointDatabasePath);

            PointType[] types = { PointType.Stamina, PointType.Gold, PointType.FreeGem };
            float[] frameX = { 47f, 288f, 546f };
            float[] textShift = { 21.5f, 28.5f, 21.5f };

            var chips = new List<ChipWiring>();
            for (int i = 0; i < types.Length; i++)
            {
                PlaceBox(parent, "Chip_" + types[i], frame, frameX[i], 27f, 203f, 56f, Vector2.zero);

                Sprite iconSprite = database != null ? database.Get(types[i])?.icon : null;
                Image icon = null;
                if (iconSprite != null)
                {
                    // 시안의 고기 아이콘이 원본의 2배라서 같은 2배로 맞춘다. 보이는 중심이 칩 왼쪽 끝 위.
                    icon = PlaceScaled(parent, "ChipIcon_" + types[i], Measure(iconSprite), 2f, 2f,
                                       frameX[i] + 16f, 54f, Vector2.zero);
                }
                else
                {
                    Debug.LogWarning("[핀볼] 재화 표에 " + types[i] + " 아이콘이 없어 칩 아이콘을 만들지 않았다.");
                }

                TMP_Text text = NewArtText(parent, "ChipText_" + types[i], "0", 25f, Color.white, font,
                                           frameX[i] + 101.5f + textShift[i], 56f, 140f, 36f, Vector2.zero);

                chips.Add(new ChipWiring { type = types[i], text = text, icon = icon });
            }

            return chips;
        }

        /// <summary>
        /// 왼쪽 아래 뒤로 버튼. 시안은 아래 바 그림 안에 버튼이 구워져 있어서 거기서 버튼만 오려 냈다
        /// (<c>FromPsd/Pinball_Back</c>). 눌림은 색 틴트로 준다.
        /// </summary>
        private static Button BuildBackButton(Transform parent)
        {
            Image image = PlaceBox(parent, "BackButton", LoadArt("Pinball/FromPsd/Pinball_Back"),
                                   12f, 1735f, 164f, 169f, Vector2.zero);
            image.raycastTarget = true;

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }

        private static ColorBlock DisabledGray(ColorBlock colors)
        {
            colors.disabledColor = new Color(0.6f, 0.6f, 0.65f, 1f);
            return colors;
        }

        /// <summary>배율 버튼 하나에 필요한 참조 묶음.</summary>
        private struct MultiplierWiring
        {
            public int multiplier;
            public Button button;
            public TMP_Text label;
            public TMP_Text requirement;
            public GameObject selectedMark;
        }

        /// <summary>
        /// 아래 바의 배율 버튼. 시안에 배율 UI 가 없어 <b>추정</b>한 배치다 —
        /// 뒤로 버튼 오른쪽에 4열 2행. 잠김·선택은 페이지가 런타임에 입힌다.
        /// </summary>
        private static MultiplierWiring BuildMultiplierButton(
            Transform parent, TMP_FontAsset font, int multiplier, int index)
        {
            float cx = GridColumnX[index % GridColumnX.Length];
            float cy = GridRowY[index / GridColumnX.Length];

            ButtonRect(cx, cy, GridButtonWidth, GridButtonHeight, 3f, out Vector2 size, out Vector2 position);
            Image image = NewSliced(parent, "Multiplier_x" + multiplier,
                                    LoadSprite("InfinityMode/Btn_PurpleGray"), 3f, size, position);

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = DisabledGray(button.colors);

            TMP_Text label = NewText("Label", image.transform, "x" + multiplier, 34f, Color.white, font);
            NoWrap(label);
            SetRect(label.rectTransform, new Vector2(size.x - 20f, 44f), new Vector2(0f, 2f));

            TMP_Text requirement = NewText("Requirement", image.transform, "", 21f, GoldText, font);
            NoWrap(requirement);
            SetRect(requirement.rectTransform, new Vector2(size.x - 20f, 32f), new Vector2(0f, -28f));

            // 선택 표시는 노란 버튼 판 하나를 위에 덮는다. 꺼 둔 채로 굽고 페이지가 켠다.
            Image mark = NewSliced(image.transform, "Selected", LoadSprite("Ingame/Big_Btn_Yellow"), 3f,
                                   size, Vector2.zero);
            mark.raycastTarget = false;
            mark.transform.SetAsFirstSibling();
            mark.gameObject.SetActive(false);

            return new MultiplierWiring
            {
                multiplier = multiplier,
                button = button,
                label = label,
                requirement = requirement,
                selectedMark = mark.gameObject,
            };
        }

        // ──────────────────────────────────────────────── 배선

        private static void WirePage(
            UIPinballPage page, PinballPlayback playback, PinballBoardView boardView,
            Button launch, Button close, TMP_Text session,
            TMP_Text levelText, Image levelFill, TMP_Text levelGaugeText,
            Image ticketFill, TMP_Text ticketGaugeText,
            List<MultiplierWiring> options, List<ChipWiring> chips, List<SkinWiring> skins)
        {
            var so = new SerializedObject(page);
            SetRef(so, "playback", playback);
            SetRef(so, "boardView", boardView);
            SetRef(so, "launchButton", launch);
            SetRef(so, "closeButton", close);
            SetRef(so, "sessionText", session);
            SetRef(so, "levelText", levelText);
            SetRef(so, "levelGaugeFill", levelFill);
            SetRef(so, "levelGaugeText", levelGaugeText);
            SetRef(so, "ticketGaugeFill", ticketFill);
            SetRef(so, "ticketGaugeText", ticketGaugeText);

            SerializedProperty optionList = FindList(so, "multiplierOptions");
            if (optionList != null)
            {
                optionList.arraySize = options.Count;
                for (int i = 0; i < options.Count; i++)
                {
                    SerializedProperty entry = optionList.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("multiplier").intValue = options[i].multiplier;
                    entry.FindPropertyRelative("button").objectReferenceValue = options[i].button;
                    entry.FindPropertyRelative("label").objectReferenceValue = options[i].label;
                    entry.FindPropertyRelative("requirementLabel").objectReferenceValue = options[i].requirement;
                    entry.FindPropertyRelative("selectedMark").objectReferenceValue = options[i].selectedMark;
                }
            }

            SerializedProperty chipList = FindList(so, "currencyChips");
            if (chipList != null)
            {
                chipList.arraySize = chips.Count;
                for (int i = 0; i < chips.Count; i++)
                {
                    SerializedProperty entry = chipList.GetArrayElementAtIndex(i);
                    // PointType 값이 이어지지 않는 enum 이라 인덱스가 아니라 값 그대로 넣는다.
                    entry.FindPropertyRelative("pointType").intValue = (int)chips[i].type;
                    entry.FindPropertyRelative("text").objectReferenceValue = chips[i].text;
                    entry.FindPropertyRelative("icon").objectReferenceValue = chips[i].icon;
                }
            }

            SerializedProperty skinList = FindList(so, "specialSkins");
            if (skinList != null)
            {
                skinList.arraySize = skins.Count;
                for (int i = 0; i < skins.Count; i++)
                {
                    SkinWiring skin = skins[i];
                    SerializedProperty entry = skinList.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("pegIndex").intValue = skin.pegIndex;
                    entry.FindPropertyRelative("tag").intValue = skin.tag;
                    entry.FindPropertyRelative("ringFill").objectReferenceValue = skin.ringFill;
                    entry.FindPropertyRelative("gaugeLabelY").floatValue = skin.gaugeLabelY;
                    entry.FindPropertyRelative("rewardRowY").floatValue = skin.rewardRowY;

                    SerializedProperty barList = entry.FindPropertyRelative("bars");
                    barList.arraySize = skin.bars.Count;
                    for (int b = 0; b < skin.bars.Count; b++)
                    {
                        SerializedProperty bar = barList.GetArrayElementAtIndex(b);
                        bar.FindPropertyRelative("image").objectReferenceValue = skin.bars[b].image;
                        bar.FindPropertyRelative("normalSprite").objectReferenceValue = skin.bars[b].normal;
                        bar.FindPropertyRelative("litSprite").objectReferenceValue = skin.bars[b].lit;
                        bar.FindPropertyRelative("percent").intValue = skin.bars[b].percent;
                    }
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SerializedProperty FindList(SerializedObject so, string field)
        {
            SerializedProperty list = so.FindProperty(field);
            if (list == null)
            {
                Debug.LogError("[핀볼] UIPinballPage." + field + " 를 못 찾았다. 이름이 바뀌었는지 확인할 것.");
            }

            return list;
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

            // 켜진 계층에 AddComponent 하면 Awake 가 참조 배선보다 먼저 돌아 스스로 enabled=false 가 되고,
            // 그 상태로 프리팹에 저장돼 공이 첫 자리에 멈춘 채 안 굴러간다. 배선을 마친 뒤 되켠다.
            so.FindProperty("m_Enabled").boolValue = true;
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
