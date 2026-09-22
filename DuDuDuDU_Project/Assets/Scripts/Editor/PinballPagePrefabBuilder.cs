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
        private const string SeedTablePath = "Assets/Pinball/PinballSeedTable.asset";

        private const string BonusBoardPrefabPath = "Assets/Pinball/BonusBoard_View.prefab";
        private const string BonusSeedTablePath = "Assets/Pinball/BonusSeedTable.asset";
        private const string BonusBoardAssetPath = "Assets/Pinball/BonusBoard.asset";

        /// <summary>
        /// 배율 버튼. <c>PinballRewardDatabase.PopulateDefaults</c> 의 기본 표와 같은 순서다 —
        /// 에셋에서 표를 바꿨다면 프리팹을 다시 구울 것.
        /// </summary>
        private static readonly int[] Multipliers = { 1, 2, 3, 5, 10, 20, 50, 100 };

        /// <summary>배율 버튼 격자. 4열 2행.</summary>
        private static readonly float[] MultiplierColumns = { -390f, -130f, 130f, 390f };
        private static readonly float[] MultiplierRows = { -520f, -645f };

        // 자리 값은 1080x1920 기준이다. 중심이 (0,0) 이고 위가 +다.
        // 탑 UI 와 같은 팔레트. UITowerUIFactory 의 값은 internal 이라 에디터 어셈블리에서
        // 볼 수 없어 여기 옮겨 적었다.
        private static readonly Color Backdrop = new Color(0.07f, 0.08f, 0.15f, 1f);
        private static readonly Color Accent = new Color(0.36f, 0.42f, 0.92f, 1f);
        private static readonly Color AccentSoft = new Color(0.24f, 0.28f, 0.55f, 1f);
        private static readonly Color GoldText = new Color(1f, 0.84f, 0.38f, 1f);

        /// <summary>「바로 전액 받기」는 돈을 낸 사람의 버튼이라 색을 달리한다.</summary>
        private static readonly Color GoldButton = new Color(0.62f, 0.48f, 0.15f, 1f);

        /// <summary>보상 라운드 주사위 수. BonusDiceDatabase 의 기본값과 같다.</summary>
        private const int DiceCount = 5;

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

            GameObject root = Assemble(
                boardPrefab, seedTable, font, bonusBoardPrefab, bonusSeedTable, bonusBoard);
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
            Stretch(root.GetComponent<RectTransform>());

            // dialogView 는 루트가 아니라 자식이어야 한다. DialogBase 가 이것을
            // SetActive 로 껐다 켜는데, 루트를 끄면 페이지 자신의 Awake 가 돌지 않는다.
            Image view = NewImage("View", root.transform, Backdrop);
            Stretch(view.rectTransform);

            TMP_Text titleText = NewText("Title", view.transform, "핀볼", 48f, Color.white, font);
            SetRect(titleText.rectTransform, new Vector2(600f, 70f), new Vector2(0f, 820f));

            // 뒤집히는 부분. 이 아래 두 면이 Y축 180도로 마주 보고 있고, 부모를 돌리면
            // 카드처럼 뒤집힌다. 보상 면이 로컬 180 이라 다 돌았을 때 정확히 정면으로 선다.
            GameObject pivot = NewRect("BoardPivot", view.transform);
            Stretch(pivot.GetComponent<RectTransform>());

            GameObject pinballSide = NewRect("PinballSide", pivot.transform);
            Stretch(pinballSide.GetComponent<RectTransform>());

            GameObject bonusSide = NewRect("BonusSide", pivot.transform);
            Stretch(bonusSide.GetComponent<RectTransform>());
            bonusSide.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            bonusSide.SetActive(false);

            // 판. 프리팹 인스턴스로 넣어야 나중에 판을 다시 구웠을 때 따라온다.
            var board = PrefabUtility.InstantiatePrefab(boardPrefab, pinballSide.transform) as GameObject;
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
            // 판이 1000px 이라 중심을 180 에 두면 아래 끝이 -320 이다. 그 밑이 조작 영역.
            boardRect.anchoredPosition = new Vector2(0f, 180f);

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

            TMP_Text gauge = NewText("GaugeText", pinballSide.transform, "", 30f, GoldText, font);
            SetRect(gauge.rectTransform, new Vector2(900f, 60f), new Vector2(0f, 740f));

            TMP_Text ticket = NewText("TicketText", pinballSide.transform, "0", 36f, Color.white, font);
            SetRect(ticket.rectTransform, new Vector2(400f, 50f), new Vector2(-230f, -380f));

            TMP_Text cost = NewText("CostText", pinballSide.transform, "1", 36f, GoldText, font);
            SetRect(cost.rectTransform, new Vector2(400f, 50f), new Vector2(230f, -380f));

            TMP_Text session = NewText("SessionText", pinballSide.transform, "", 28f, Color.white, font);
            SetRect(session.rectTransform, new Vector2(400f, 44f), new Vector2(0f, -432f));

            // 배율 버튼. 잠긴 것과 선택된 것은 페이지가 런타임에 구분해 준다.
            var options = new List<MultiplierWiring>();
            for (int i = 0; i < Multipliers.Length; i++)
            {
                options.Add(BuildMultiplierButton(pinballSide.transform, font, Multipliers[i], i));
            }

            Button launch = NewButton("LaunchButton", pinballSide.transform, "발사", Accent, font, out _);
            SetRect(launch.GetComponent<RectTransform>(), new Vector2(420f, 130f), new Vector2(0f, -790f));

            // DialogBase 의 exitBtn 에 넣지 않는다 — 그건 무조건 닫아 버려서
            // "세션 중에는 못 나간다"를 페이지가 판정할 수 없다.
            Button close = NewButton("CloseButton", view.transform, "X", Accent, font, out _);
            SetRect(close.GetComponent<RectTransform>(), new Vector2(90f, 90f), new Vector2(460f, 840f));

            // 보너스 라운드 안내. 판 오른쪽에 떠 있다가 세션이 끝나면 가운데로 이동한다.
            // <b>핀볼 면에 둔다</b> — 보상 면으로 넘어가는 안내이므로 넘어가기 전에 보여야 한다.
            TMP_Text bannerText = NewText(
                "BonusBannerText", pinballSide.transform, "보너스게임 가능", 30f, GoldText, font);
            RectTransform banner = bannerText.rectTransform;
            SetRect(banner, new Vector2(360f, 96f), new Vector2(330f, 430f));
            bannerText.gameObject.SetActive(false);

            UIBonusDicePanel bonusPanel = BuildBonusSide(
                bonusSide.transform, font, bonusBoardPrefab, bonusSeedTable, bonusBoard);

            var page = root.AddComponent<UIPinballPage>();
            page.dialogView = view.gameObject;
            WirePage(page, playback, boardView, launch, close, ticket, cost, session, gauge, options);
            WireFlip(page, pivot, pinballSide, bonusSide, bonusPanel, banner, bannerText);

            return root;
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
        /// 보상 라운드 면을 조립한다. 판 하나와 주사위 조작 UI 가 들어간다.
        ///
        /// <b>모양은 임시다.</b> 자리와 크기만 맞춰 둔 것이고, 다듬는 것은 프리팹에서 한다 —
        /// 핀볼 면과 같은 규약이다.
        /// </summary>
        private static UIBonusDicePanel BuildBonusSide(
            Transform parent, TMP_FontAsset font,
            GameObject bonusBoardPrefab, SeedTable bonusSeedTable, PinballBoard bonusBoard)
        {
            var board = PrefabUtility.InstantiatePrefab(bonusBoardPrefab, parent) as GameObject;
            if (board == null)
            {
                Debug.LogError("[핀볼] 보상판 프리팹을 인스턴스로 만들지 못했다.");
                return null;
            }

            board.name = "BonusBoard";
            var boardRect = board.GetComponent<RectTransform>();
            boardRect.anchorMin = new Vector2(0.5f, 0.5f);
            boardRect.anchorMax = new Vector2(0.5f, 0.5f);
            boardRect.pivot = new Vector2(0.5f, 0.5f);
            boardRect.anchoredPosition = new Vector2(0f, 300f);

            var boardView = board.GetComponent<PinballBoardView>();
            if (boardView == null)
            {
                Debug.LogError("[핀볼] 보상판 프리팹에 PinballBoardView 가 없다.");
                return null;
            }

            var playback = board.AddComponent<PinballPlayback>();
            WirePlayback(playback, bonusSeedTable, boardView);

            // 패널은 판과 형제로 둔다. 판을 다시 구워 교체해도 UI 배선이 안 끊긴다.
            GameObject panelRoot = NewRect("BonusPanel", parent);
            Stretch(panelRoot.GetComponent<RectTransform>());
            var panel = panelRoot.AddComponent<UIBonusDicePanel>();

            TMP_Text hand = NewText("HandText", panelRoot.transform, "", 34f, GoldText, font);
            SetRect(hand.rectTransform, new Vector2(900f, 54f), new Vector2(0f, -330f));

            TMP_Text cycle = NewText("CycleText", panelRoot.transform, "", 28f, Color.white, font);
            SetRect(cycle.rectTransform, new Vector2(420f, 44f), new Vector2(-240f, -395f));

            TMP_Text reroll = NewText("RerollText", panelRoot.transform, "", 28f, Color.white, font);
            SetRect(reroll.rectTransform, new Vector2(420f, 44f), new Vector2(240f, -395f));

            // 핀 진행도는 여기서 만들지 않는다 — 패널이 판 위의 핀 좌표를 읽어
            // 런타임에 핀마다 직접 붙인다(UIBonusDicePanel.BuildPinLabels).

            // 주사위 5칸. 눌러서 잠그고, 잠긴 것은 리롤에서 빠진다.
            var diceSlots = new List<DiceWiring>();
            for (int i = 0; i < DiceCount; i++)
            {
                float x = (i - (DiceCount - 1) * 0.5f) * 200f;
                diceSlots.Add(BuildDiceSlot(panelRoot.transform, font, i, new Vector2(x, -500f)));
            }

            Button rerollButton = NewButton(
                "RerollButton", panelRoot.transform, "다시 굴리기", AccentSoft, font, out _);
            SetRect(rerollButton.GetComponent<RectTransform>(),
                    new Vector2(400f, 120f), new Vector2(-230f, -660f));

            Button shootButton = NewButton(
                "ShootButton", panelRoot.transform, "발사", Accent, font, out _);
            SetRect(shootButton.GetComponent<RectTransform>(),
                    new Vector2(400f, 120f), new Vector2(230f, -660f));

            // 이 둘은 조건이 맞을 때만 패널이 켠다. 꺼 둔 채로 굽는다 —
            // 광고가 없는데 버튼이 보이면 "광고가 안 나온다"는 고장으로 읽힌다.
            Button adButton = NewButton(
                "AdButton", panelRoot.transform, "광고 보고 한 번 더", AccentSoft, font, out _);
            SetRect(adButton.GetComponent<RectTransform>(),
                    new Vector2(560f, 110f), new Vector2(0f, -790f));
            adButton.gameObject.SetActive(false);

            Button claimAllButton = NewButton(
                "ClaimAllButton", panelRoot.transform, "바로 전액 받기", GoldButton, font, out _);
            SetRect(claimAllButton.GetComponent<RectTransform>(),
                    new Vector2(560f, 110f), new Vector2(0f, -910f));
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
            Transform parent, TMP_FontAsset font, int index, Vector2 position)
        {
            Button button = NewButton(
                "Dice" + index, parent, "1", AccentSoft, font, out TMP_Text label);
            SetRect(button.GetComponent<RectTransform>(), new Vector2(170f, 170f), position);
            label.fontSize = 72f;

            // 잠금 표시는 테두리 한 줄이면 충분하다. 꺼 둔 채로 굽고 패널이 켠다.
            Image mark = NewImage("Locked", button.transform, GoldText);
            SetRect(mark.rectTransform, new Vector2(184f, 184f), Vector2.zero);
            mark.raycastTarget = false;
            mark.transform.SetAsFirstSibling();
            mark.gameObject.SetActive(false);

            return new DiceWiring { button = button, label = label, lockMark = mark.gameObject };
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

        /// <summary>배율 버튼 하나에 필요한 참조 묶음.</summary>
        private struct MultiplierWiring
        {
            public int multiplier;
            public Button button;
            public TMP_Text label;
            public TMP_Text requirement;
            public GameObject selectedMark;
        }

        private static MultiplierWiring BuildMultiplierButton(
            Transform parent, TMP_FontAsset font, int multiplier, int index)
        {
            float x = MultiplierColumns[index % MultiplierColumns.Length];
            float y = MultiplierRows[index / MultiplierColumns.Length];

            Button button = NewButton("Multiplier_x" + multiplier, parent,
                "x" + multiplier, AccentSoft, font, out TMP_Text label);
            SetRect(button.GetComponent<RectTransform>(), new Vector2(240f, 110f), new Vector2(x, y));
            SetRect(label.rectTransform, new Vector2(220f, 50f), new Vector2(0f, 18f));

            TMP_Text requirement = NewText("Requirement", button.transform, "", 22f, GoldText, font);
            SetRect(requirement.rectTransform, new Vector2(220f, 36f), new Vector2(0f, -28f));

            // 선택 표시는 테두리 한 줄이면 충분하다. 꺼 둔 채로 굽고 페이지가 켠다.
            Image mark = NewImage("Selected", button.transform, Accent);
            SetRect(mark.rectTransform, new Vector2(252f, 122f), Vector2.zero);
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
            Button launch, Button close, TMP_Text ticket, TMP_Text cost,
            TMP_Text session, TMP_Text gauge, List<MultiplierWiring> options)
        {
            var so = new SerializedObject(page);
            SetRef(so, "playback", playback);
            SetRef(so, "boardView", boardView);
            SetRef(so, "launchButton", launch);
            SetRef(so, "closeButton", close);
            SetRef(so, "ticketText", ticket);
            SetRef(so, "costText", cost);
            SetRef(so, "sessionText", session);
            SetRef(so, "gaugeText", gauge);

            SerializedProperty list = so.FindProperty("multiplierOptions");
            if (list == null)
            {
                Debug.LogError(
                    "[핀볼] UIPinballPage.multiplierOptions 를 못 찾았다. 이름이 바뀌었는지 확인할 것.");
            }
            else
            {
                list.arraySize = options.Count;
                for (int i = 0; i < options.Count; i++)
                {
                    SerializedProperty entry = list.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("multiplier").intValue = options[i].multiplier;
                    entry.FindPropertyRelative("button").objectReferenceValue = options[i].button;
                    entry.FindPropertyRelative("label").objectReferenceValue = options[i].label;
                    entry.FindPropertyRelative("requirementLabel").objectReferenceValue = options[i].requirement;
                    entry.FindPropertyRelative("selectedMark").objectReferenceValue = options[i].selectedMark;
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
