using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using OJ.Dice;
using OJ.Hunting;
using OJ.StageStar;
using OJ.Tower;

namespace OJ.EditorTools
{
    /// <summary>
    /// 다이스 언락의 UI 도구 둘 — 상세창에 해금 표시 달기, 성장 목록 칸에 자물쇠·언락 안내 달기.
    ///
    /// <b>둘 다 손으로 만든 프리팹이라 <c>Create()</c> 가 없다.</b> 그래서 통째로 굽지 않고
    /// 필요한 오브젝트만 얹고 참조만 잇는다. 같은 경로에 저장하므로 GUID 가 유지되어
    /// <c>DialogCatalog</c> 와 <c>UIDiceGrowthPage.itemPrefab</c> 의 참조가 안 끊긴다.
    /// YAML 직접 편집이 금지라(AGENTS 절대규칙 3) 이 경로가 유일한 방법이다.
    /// </summary>
    public static class DiceUnlockUIPrefabBaker
    {
        private const string DetailPanelPath =
            "Assets/Prefab/Refactory/LobbyScene/UIDiceGrowthDetailPanel.prefab";

        private const string GrowthItemPath =
            "Assets/Prefab/Lobby/UIDiceGrowthItem.prefab";

        private const string RewardElementPath =
            "Assets/Prefab/Hunting/UIRewardElement.prefab";

        private const string TowerResultDialogPath =
            "Assets/Prefab/Refactory/Tower/UITowerResultDialog.prefab";

        private const string StageResultDialogPath =
            "Assets/Prefab/Refactory/BattleScene/UIStageResultDialog.prefab";

        private const string StageRewardDialogPath =
            "Assets/Prefab/Refactory/LobbyScene/UIStageRewardDialog.prefab";

        private const string StarRewardElementPath =
            "Assets/Prefab/Lobby/UIStageStarRewardElement.prefab";

        private const string LockedRootName = "LockedRoot";

        /// <summary>
        /// 보상 칸 안쪽을 <b>늘어나게</b> 만든다.
        ///
        /// <b>왜 필요한가.</b> 칸은 230x230 인데 아이콘(192)·숫자가 <i>고정 크기로 가운데</i>
        /// 박혀 있다. 그래서 부모가 칸을 줄여도 안쪽은 그대로라 <b>칸 밖으로 삐져나온다</b> —
        /// 스테이지 마일스톤을 그리드로 바꿔 셀을 좁히면 바로 드러나는 문제다.
        /// 늘어나게 바꿔 두면 칸이 몇으로 줄든 안쪽이 따라간다.
        ///
        /// 아래쪽에 숫자 자리를 남기고, 환급 아이콘은 그 위 가운데에 절반쯤 크기로 얹는다.
        /// </summary>
        [MenuItem("OJ/개발/다이스 언락/보상 칸 안쪽 늘어나게 만들기")]
        private static void InstallRewardElementStretch()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(RewardElementPath);
            try
            {
                var element = contents.GetComponent<UIRewardElement>();
                if (element == null)
                {
                    Debug.LogError("[굽기] " + RewardElementPath + " 에 UIRewardElement 가 없다.");
                    return;
                }

                // 이름을 박지 않고 컴포넌트로 찾는다 — 이 프리팹의 자식 이름은 손댈 수 있다.
                Image icon = FindIconExcept(contents.transform, "OverlayIcon");
                TMP_Text amount = contents.GetComponentInChildren<TMP_Text>(true);
                Transform overlay = contents.transform.Find("OverlayIcon");

                // <b>preserveAspect 가 핵심이다.</b> 늘어나는 사각형에 스프라이트를 채우면
                // 셀이 가로로 길 때 그림이 그대로 <b>납작해진다</b> — 실제로 그렇게 찌그러졌다.
                // 켜 두면 사각형 안에서 비율을 지키며 들어가고, 남는 쪽은 여백이 된다.
                if (icon != null)
                {
                    icon.preserveAspect = true;
                    StretchWithPadding((RectTransform)icon.transform, 12f, 12f, 6f, 34f);
                }

                if (overlay != null)
                {
                    overlay.GetComponent<Image>().preserveAspect = true;
                    StretchWithPadding((RectTransform)overlay, 60f, 60f, 30f, 58f);
                }

                if (amount != null)
                {
                    var rect = amount.rectTransform;
                    rect.anchorMin = new Vector2(0f, 0f);
                    rect.anchorMax = new Vector2(1f, 0f);
                    rect.pivot = new Vector2(0.5f, 0f);
                    rect.sizeDelta = new Vector2(0f, 34f);
                    rect.anchoredPosition = new Vector2(0f, 2f);
                }

                PrefabUtility.SaveAsPrefabAsset(contents, RewardElementPath, out bool ok);
                Debug.Log(ok
                    ? "[굽기] 보상 칸 안쪽을 늘어나게 바꿨다: " + RewardElementPath
                    : "[굽기] 프리팹 저장에 실패했다: " + RewardElementPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>
        /// 스테이지 마일스톤의 보상 칸을 <b>4열 그리드</b>로 바꾼다.
        ///
        /// 지금은 가로 한 줄(<c>HorizontalLayoutGroup</c>)이라 칸이 4개만 돼도 폭을 넘어
        /// <b>화면 밖으로 나간다.</b> 그리드는 열 수를 넘기면 스스로 다음 줄로 내리므로
        /// 4개부터 2줄이 되고, 4x2 = <b>8개까지</b> 자리가 보장된다.
        /// </summary>
        [MenuItem("OJ/개발/다이스 언락/보상 칸 그리드로 바꾸기 (스테이지 보상·결과)")]
        private static void InstallRewardGrids()
        {
            // 마일스톤 화면은 위에 "보상" 제목이 있어 그만큼 덜 쓴다.
            ApplyRewardGrid(StageRewardDialogPath, captionReserve: 72f);
            ApplyRewardGrid(StageResultDialogPath, captionReserve: 0f);
        }

        /// <summary>
        /// 보상 칸을 <b>4열 그리드</b>로 바꾸고 자리를 넓힌다.
        ///
        /// <b>왜 그리드인가.</b> 지금은 가로 한 줄이라 칸이 4개만 돼도 폭을 넘어
        /// <b>화면 밖으로 나간다.</b> 그리드는 열 수를 넘기면 스스로 다음 줄로 내리므로
        /// 4개부터 2줄이 되고 4x2 = <b>8개까지</b> 자리가 보장된다.
        ///
        /// <b>왜 BG 까지 키우나.</b> 셀이 납작하면 <c>preserveAspect</c> 가 세로에 맞춰
        /// 아이콘을 줄인다 — 넘치지는 않지만 <b>작아진다.</b> 부모 보상 박스에 남는
        /// 세로를 내줘야 아이콘이 커진다.
        /// </summary>
        private static void ApplyRewardGrid(string prefabPath, float captionReserve)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                Debug.LogError("[굽기] 프리팹을 못 찾았다: " + prefabPath);
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Transform root = FindDeep(contents.transform, "RewardRoot");
                if (root == null)
                {
                    Debug.LogError("[굽기] RewardRoot 를 못 찾았다: " + prefabPath);
                    return;
                }

                // 부모 보상 박스를 키운다. 아래쪽에 다른 것이 없는 자리라 세로만 늘린다.
                var box = (RectTransform)root.parent;
                if (box != null && box.sizeDelta.y < BoxHeight)
                    box.sizeDelta = new Vector2(box.sizeDelta.x, BoxHeight);

                var line = root.GetComponent<HorizontalOrVerticalLayoutGroup>();
                if (line != null)
                    Object.DestroyImmediate(line, true);

                float height = BoxHeight - 40f - captionReserve;
                var rect = (RectTransform)root;
                rect.sizeDelta = new Vector2(GridWidth, height);
                rect.anchoredPosition = new Vector2(0f, -(captionReserve * 0.5f) - 10f);

                var grid = root.GetComponent<GridLayoutGroup>();
                if (grid == null)
                    grid = root.gameObject.AddComponent<GridLayoutGroup>();

                float cellWidth = (GridWidth - Spacing * (Columns - 1)) / Columns;
                float cellHeight = (height - Spacing) * 0.5f;

                grid.cellSize = new Vector2(cellWidth, cellHeight);
                grid.spacing = new Vector2(Spacing, Spacing);
                grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
                grid.startAxis = GridLayoutGroup.Axis.Horizontal;
                grid.childAlignment = TextAnchor.MiddleCenter;
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = Columns;

                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath, out bool ok);
                Debug.Log(ok
                    ? string.Format("[굽기] 보상 칸을 {0}열 그리드로 바꿨다 (셀 {1:0}x{2:0}): {3}",
                        Columns, cellWidth, cellHeight, prefabPath)
                    : "[굽기] 프리팹 저장에 실패했다: " + prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // 보상 칸 배치 상수. 두 화면이 같은 값을 써야 같은 크기로 보인다.
        private const float BoxHeight = 480f;
        private const float GridWidth = 900f;
        private const float Spacing = 10f;
        private const int Columns = 4;

        /// <summary>
        /// 탑 결과창의 보상을 <b>글자에서 아이콘 칸으로</b> 바꾼다.
        ///
        /// 탑만 <c>rewardText</c> 한 줄로 적고 있어서, 같은 보상이 스테이지 화면과
        /// <b>다르게 보였다.</b> 다른 화면과 같은 <c>UIRewardElement</c> 를 쓰면
        /// 딤 처리된 다이스 + 환급 아이콘 규칙도 그대로 따라온다.
        ///
        /// 칸 수가 적어(골드 · 다이아 · 신화 스크롤 · 다이스) 한 줄이면 충분하다.
        /// </summary>
        [MenuItem("OJ/개발/다이스 언락/탑 결과창 보상을 아이콘으로 바꾸기")]
        private static void InstallTowerResultRewards()
        {
            var rewardAsset = AssetDatabase.LoadAssetAtPath<GameObject>(RewardElementPath);
            if (rewardAsset == null)
            {
                Debug.LogError("[굽기] 보상 칸 프리팹을 못 찾았다: " + RewardElementPath);
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(TowerResultDialogPath) == null)
            {
                Debug.LogError("[굽기] 탑 결과창 프리팹을 못 찾았다: " + TowerResultDialogPath +
                               System.Environment.NewLine +
                               "  OJ/개발/무한의 탑 쪽 굽기 메뉴를 먼저 돌렸는지 볼 것.");
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(TowerResultDialogPath);
            try
            {
                var dialog = contents.GetComponent<UITowerResultDialog>();
                if (dialog == null)
                {
                    Debug.LogError("[굽기] " + TowerResultDialogPath + " 에 UITowerResultDialog 가 없다.");
                    return;
                }

                Transform box = FindDeep(contents.transform, "RewardBox");
                if (box == null)
                {
                    Debug.LogError("[굽기] RewardBox 를 못 찾았다. 계층이 바뀌었는지 확인할 것.");
                    return;
                }

                var so = new SerializedObject(dialog);
                bool changed = false;

                Transform root = box.Find("RewardRoot");
                if (root == null)
                {
                    GameObject created = NewUIObject("RewardRoot", box);
                    // RewardBox 는 920x240 이고 위쪽은 캡션·해금 게이지가 쓴다.
                    SetRect((RectTransform)created.transform, new Vector2(880f, 120f), new Vector2(0f, 44f));

                    var line = created.AddComponent<HorizontalLayoutGroup>();
                    line.childAlignment = TextAnchor.MiddleCenter;
                    line.spacing = 12f;
                    line.childControlWidth = true;
                    line.childControlHeight = true;
                    line.childForceExpandWidth = false;
                    line.childForceExpandHeight = false;

                    root = created.transform;
                    changed = true;
                }

                Transform template = root.Find("Template");
                if (template == null)
                {
                    var created = (GameObject)PrefabUtility.InstantiatePrefab(rewardAsset, root);
                    created.name = "Template";

                    // 레이아웃이 크기를 정한다. 한 줄에 넷이 들어가도록 넉넉히.
                    var element = created.AddComponent<LayoutElement>();
                    element.preferredWidth = 118f;
                    element.preferredHeight = 118f;

                    created.SetActive(false);
                    template = created.transform;
                    changed = true;
                }

                changed |= Wire(so, "rewardRoot", root.GetComponent<RectTransform>());
                changed |= Wire(so, "rewardElementTemplate", template.GetComponent<UIRewardElement>());

                if (!changed)
                {
                    Debug.Log("[굽기] 이미 달려 있다. 아무것도 바꾸지 않았다: " + TowerResultDialogPath);
                    return;
                }

                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(contents, TowerResultDialogPath, out bool ok);
                Debug.Log(ok
                    ? "[굽기] 탑 결과창 보상을 아이콘 칸으로 바꿨다: " + TowerResultDialogPath
                    : "[굽기] 프리팹 저장에 실패했다: " + TowerResultDialogPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>
        /// 별 보상 행에 <b>다이스 슬롯</b>을 하나 더 단다.
        ///
        /// 기존 슬롯을 그대로 복제해 오른쪽에 놓는다 — 크기·스케일·앵커가 같아야
        /// 두 칸이 같은 크기로 보인다. <b>좌표는 기존 슬롯 기준</b>으로 잡는다:
        /// 이 행의 자식들은 앵커가 제각각이라 절대 좌표로 맞추면 어긋난다.
        /// </summary>
        [MenuItem("OJ/개발/다이스 언락/별 보상 행에 다이스 슬롯 달기")]
        private static void InstallStarRewardDiceSlot()
        {
            var rewardAsset = AssetDatabase.LoadAssetAtPath<GameObject>(RewardElementPath);
            if (rewardAsset == null)
            {
                Debug.LogError("[굽기] 보상 칸 프리팹을 못 찾았다: " + RewardElementPath);
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(StarRewardElementPath);
            try
            {
                var row = contents.GetComponent<UIStageStarRewardElement>();
                if (row == null)
                {
                    Debug.LogError("[굽기] " + StarRewardElementPath + " 에 UIStageStarRewardElement 가 없다.");
                    return;
                }

                var so = new SerializedObject(row);
                SerializedProperty existingProp = so.FindProperty("rewardElement");
                var existing = existingProp != null ? existingProp.objectReferenceValue as UIRewardElement : null;
                if (existing == null)
                {
                    Debug.LogError("[굽기] 기존 보상 슬롯을 못 찾았다. rewardElement 가 비어 있다.");
                    return;
                }

                Transform slot = contents.transform.Find("DiceRewardElement");
                bool changed = false;

                if (slot == null)
                {
                    var created = (GameObject)PrefabUtility.InstantiatePrefab(rewardAsset, existing.transform.parent);
                    created.name = "DiceRewardElement";

                    var from = (RectTransform)existing.transform;
                    var to = (RectTransform)created.transform;
                    to.anchorMin = from.anchorMin;
                    to.anchorMax = from.anchorMax;
                    to.pivot = from.pivot;
                    to.sizeDelta = from.sizeDelta;
                    to.localScale = from.localScale;

                    // 기존 슬롯의 실제 폭(스케일 포함)만큼 오른쪽으로 밀고 조금 띄운다.
                    float step = from.sizeDelta.x * from.localScale.x + 16f;
                    to.anchoredPosition = from.anchoredPosition + new Vector2(step, 0f);

                    created.SetActive(false);
                    slot = created.transform;
                    changed = true;
                }

                changed |= Wire(so, "diceRewardElement", slot.GetComponent<UIRewardElement>());

                if (!changed)
                {
                    Debug.Log("[굽기] 이미 달려 있다. 아무것도 바꾸지 않았다: " + StarRewardElementPath);
                    return;
                }

                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(contents, StarRewardElementPath, out bool ok);
                Debug.Log(ok
                    ? "[굽기] 별 보상 행에 다이스 슬롯을 달았다: " + StarRewardElementPath +
                      System.Environment.NewLine +
                      "  위치는 기존 슬롯 오른쪽으로 잡았다 — 겹치면 인스펙터에서 옮길 것."
                    : "[굽기] 프리팹 저장에 실패했다: " + StarRewardElementPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>환급용 오버레이를 뺀 본 아이콘을 찾는다.</summary>
        private static Image FindIconExcept(Transform root, string excludeName)
        {
            Image[] images = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i].transform == root || images[i].name == excludeName)
                    continue;

                return images[i];
            }

            return null;
        }

        private static void StretchWithPadding(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>
        /// 공용 보상 칸에 <b>환급 아이콘</b>을 단다.
        ///
        /// <c>Assets/Prefab/Hunting/UIRewardElement.prefab</c> 은 결과창·스테이지 보상·
        /// 별 보상·스테이지 결과 <b>네 화면이 함께 쓰는</b> 칸이다. 여기 하나만 달면
        /// 네 곳이 한 번에 따라오고, 어느 화면에서만 다이스가 이상하게 보이는 일이 없다.
        ///
        /// 아이콘은 다이스 그림 <b>위에</b> 겹친다 — "이 다이스 대신 이 재화를 받았다" 를
        /// 그림 하나로 말하는 것이 목적이라, 옆에 나란히 놓으면 뜻이 안 산다.
        /// </summary>
        [MenuItem("OJ/개발/다이스 언락/보상 칸에 환급 아이콘 달기")]
        private static void InstallRewardElementOverlay()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(RewardElementPath) == null)
            {
                Debug.LogError("[굽기] 보상 칸 프리팹을 못 찾았다: " + RewardElementPath);
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(RewardElementPath);
            try
            {
                var element = contents.GetComponent<UIRewardElement>();
                if (element == null)
                {
                    Debug.LogError("[굽기] " + RewardElementPath + " 에 UIRewardElement 가 없다.");
                    return;
                }

                var so = new SerializedObject(element);
                bool changed = false;

                Image overlay = EnsureOverlayIcon(contents.transform, ref changed);
                changed |= Wire(so, "overlayIcon", overlay);

                if (!changed)
                {
                    Debug.Log("[굽기] 이미 달려 있다. 아무것도 바꾸지 않았다: " + RewardElementPath);
                    return;
                }

                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(contents, RewardElementPath, out bool ok);
                if (!ok)
                {
                    Debug.LogError("[굽기] 프리팹 저장에 실패했다: " + RewardElementPath);
                    return;
                }

                Debug.Log("[굽기] 보상 칸에 환급 아이콘을 달았다: " + RewardElementPath +
                          System.Environment.NewLine +
                          "  이 칸을 쓰는 네 화면이 함께 따라온다.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>
        /// 아이콘 위에 겹치는 환급 아이콘. 칸이 230x230, 본 아이콘이 192x192 라
        /// 그 절반쯤 되는 크기로 가운데에 얹는다. 평소에는 꺼져 있다.
        /// </summary>
        private static Image EnsureOverlayIcon(Transform root, ref bool changed)
        {
            Transform existing = root.Find("OverlayIcon");
            if (existing != null)
                return existing.GetComponent<Image>();

            GameObject go = NewUIObject("OverlayIcon", root);
            var icon = go.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            SetRect((RectTransform)go.transform, new Vector2(104f, 104f), new Vector2(0f, 6f));
            go.SetActive(false);

            changed = true;
            return icon;
        }

        /// <summary>
        /// 다이스 상세창에 <b>해금 표시</b>를 단다.
        ///
        /// 이 창은 보유·미보유를 둘 다 맡는다. 비용 칸의 제목과 버튼 글자는 <b>이미 있는</b>
        /// 오브젝트를 코드가 갈아 끼우므로 참조만 이어 주면 되고, 해금 조건과 중복 안내
        /// 두 줄만 새로 만든다 — 골드 줄이 미보유일 때 꺼지면서 그 자리가 빈다.
        ///
        /// <b>멱등하다.</b> 없는 것만 만들고, 바뀐 것이 없으면 저장도 하지 않는다.
        /// </summary>
        [MenuItem("OJ/개발/다이스 언락/상세창에 해금 표시 달기")]
        private static void InstallGrowthDetailUnlock()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(DetailPanelPath) == null)
            {
                Debug.LogError("[굽기] 상세창 프리팹을 못 찾았다: " + DetailPanelPath);
                return;
            }

            TMP_FontAsset font = FindKoreanFont();
            if (font == null)
            {
                Debug.LogError("[굽기] 한글 TMP 폰트를 못 찾았다. 프리팹을 건드리지 않는다.");
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(DetailPanelPath);
            try
            {
                var panel = contents.GetComponent<UIDiceGrowthDetailPanel>();
                if (panel == null)
                {
                    Debug.LogError("[굽기] " + DetailPanelPath + " 에 UIDiceGrowthDetailPanel 이 없다.");
                    return;
                }

                // <b>이름으로 훑어 찾는다.</b> 경로를 박아 두면 누가 계층을 한 칸만 옮겨도
                // 조용히 null 이 되고, 그건 "안내가 안 뜬다" 로만 드러난다.
                Transform price = FindDeep(contents.transform, "Price");
                if (price == null)
                {
                    Debug.LogError("[굽기] Price 칸을 못 찾았다. 계층이 바뀌었는지 확인할 것.");
                    return;
                }

                // Pricetitle 은 MileStone 밑에도 하나 있다. 반드시 Price 안에서 찾을 것.
                Transform priceTitle = price.Find("Pricetitle");
                Transform goldRow = price.Find("Gold");
                Transform scrollRow = price.Find("Scroll");

                var so = new SerializedObject(panel);
                bool changed = false;

                GameObject unlockRoot = EnsureUnlockRoot(price, ref changed);
                changed |= MigrateLegacyDetailParts(price, unlockRoot.transform);

                TMP_Text mission = EnsureLabel(unlockRoot.transform, "UnlockMissionText", font, 32f,
                    TextAlignmentOptions.Left, new Color(0.86f, 0.89f, 0.95f, 1f),
                    new Vector2(560f, 56f), new Vector2(-110f, 56f), ref changed);

                Button missionButton = EnsureActionButton(unlockRoot.transform, "UnlockMissionButton", "진행",
                    new Color(0.24f, 0.58f, 0.38f, 1f), font, new Vector2(180f, 68f), new Vector2(330f, 56f), ref changed);

                // 두 줄 사이의 얇은 선. <b>이 선이 "and 가 아니라 or" 를 말한다.</b>
                EnsureDivider(unlockRoot.transform, ref changed);

                TMP_Text priceLabel = EnsureLabel(unlockRoot.transform, "UnlockPriceLabel", font, 32f,
                    TextAlignmentOptions.Left, new Color(0.86f, 0.89f, 0.95f, 1f),
                    new Vector2(220f, 56f), new Vector2(-330f, -20f), ref changed);
                if (priceLabel != null && string.IsNullOrEmpty(priceLabel.text))
                    priceLabel.text = "즉시 구매 :";

                Image priceIcon = EnsureIcon(unlockRoot.transform, "UnlockPriceIcon",
                    new Vector2(48f, 48f), new Vector2(-180f, -20f), ref changed);

                TMP_Text priceText = EnsureLabel(unlockRoot.transform, "UnlockPriceText", font, 32f,
                    TextAlignmentOptions.Left, new Color(0.95f, 0.85f, 0.55f, 1f),
                    new Vector2(220f, 56f), new Vector2(-30f, -20f), ref changed);

                Button buyButton = EnsureActionButton(unlockRoot.transform, "UnlockBuyButton", "구매",
                    new Color(0.20f, 0.52f, 0.86f, 1f), font, new Vector2(180f, 68f), new Vector2(330f, -20f), ref changed);

                TMP_Text notice = EnsureLabel(unlockRoot.transform, "UnlockNotice", font, 24f,
                    TextAlignmentOptions.Center, new Color(0.70f, 0.74f, 0.82f, 1f),
                    new Vector2(840f, 44f), new Vector2(0f, -84f), ref changed);

                changed |= Wire(so, "priceTitleText", priceTitle != null ? priceTitle.GetComponent<TMP_Text>() : null);
                changed |= Wire(so, "goldRow", goldRow != null ? goldRow.gameObject : null);
                changed |= Wire(so, "scrollRow", scrollRow != null ? scrollRow.gameObject : null);
                changed |= Wire(so, "unlockRoot", unlockRoot);
                changed |= Wire(so, "unlockMissionText", mission);
                changed |= Wire(so, "unlockMissionButton", missionButton);
                changed |= Wire(so, "unlockPriceIcon", priceIcon);
                changed |= Wire(so, "unlockPriceText", priceText);
                changed |= Wire(so, "unlockBuyButton", buyButton);
                changed |= Wire(so, "unlockNoticeText", notice);

                if (!changed)
                {
                    Debug.Log("[굽기] 이미 다 달려 있다. 아무것도 바꾸지 않았다: " + DetailPanelPath);
                    return;
                }

                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(contents, DetailPanelPath, out bool ok);
                if (!ok)
                {
                    Debug.LogError("[굽기] 프리팹 저장에 실패했다: " + DetailPanelPath);
                    return;
                }

                Debug.Log("[굽기] 상세창에 해금 표시를 달았다: " + DetailPanelPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>
        /// 미보유일 때만 켜지는 것들의 부모. <b>하나로 묶는 이유</b>는 껐다 켜는 자리가
        /// 하나여야 "어떤 줄은 남고 어떤 줄은 사라지는" 어긋남이 안 생기기 때문이다.
        /// </summary>
        private static GameObject EnsureUnlockRoot(Transform price, ref bool changed)
        {
            Transform existing = price.Find("UnlockRoot");
            if (existing != null)
                return existing.gameObject;

            GameObject go = NewUIObject("UnlockRoot", price);
            Stretch((RectTransform)go.transform);

            changed = true;
            return go;
        }

        /// <summary>
        /// 이 도구의 <b>옛 판</b>이 <c>Price</c> 바로 밑에 만든 두 줄을 <c>UnlockRoot</c> 로 옮긴다.
        ///
        /// 옛 판은 "조건 한 줄 + 안내 한 줄" 이었고, 지금은 "미션 줄 + 선 + 구매 줄 + 안내" 다.
        /// 그대로 두면 옛 두 줄이 <b>UnlockRoot 밖에 남아</b> 보유해도 안 꺼지고 갱신도 안 되는
        /// 유령이 된다 — 화면에는 낡은 문구가 계속 떠 있고 원인은 안 보인다.
        ///
        /// <b>지우지 않고 옮겨 쓴다.</b> 이름만 바꾸면 그 자리에 그대로 쓸 수 있고,
        /// GameObject 이름 변경은 파일이 아니라서 GUID 에 영향이 없다.
        /// </summary>
        private static bool MigrateLegacyDetailParts(Transform price, Transform unlockRoot)
        {
            bool moved = false;

            Transform condition = price.Find("UnlockCondition");
            if (condition != null)
            {
                condition.name = "UnlockMissionText";
                condition.SetParent(unlockRoot, false);
                SetRect((RectTransform)condition, new Vector2(560f, 56f), new Vector2(-110f, 56f));
                moved = true;
            }

            Transform notice = price.Find("UnlockNotice");
            if (notice != null)
            {
                notice.SetParent(unlockRoot, false);
                SetRect((RectTransform)notice, new Vector2(840f, 44f), new Vector2(0f, -84f));
                moved = true;
            }

            return moved;
        }

        /// <summary>두 갈래 사이의 얇은 선. <b>이 선이 and 가 아니라 or 임을 말한다.</b></summary>
        private static void EnsureDivider(Transform parent, ref bool changed)
        {
            if (parent.Find("UnlockDivider") != null)
                return;

            GameObject go = NewUIObject("UnlockDivider", parent);
            var line = go.AddComponent<Image>();
            line.color = new Color(1f, 1f, 1f, 0.16f);
            line.raycastTarget = false;

            SetRect((RectTransform)go.transform, new Vector2(820f, 2f), new Vector2(0f, 18f));
            changed = true;
        }

        private static Image EnsureIcon(Transform parent, string name, Vector2 size, Vector2 pos, ref bool changed)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return existing.GetComponent<Image>();

            GameObject go = NewUIObject(name, parent);
            var icon = go.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            SetRect((RectTransform)go.transform, size, pos);
            changed = true;
            return icon;
        }

        private static TMP_Text EnsureLabel(
            Transform parent, string name, TMP_FontAsset font, float size,
            TextAlignmentOptions align, Color color, Vector2 rectSize, Vector2 pos, ref bool changed)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return existing.GetComponent<TMP_Text>();

            TMP_Text label = NewLabel(name, parent, font, size, align, color);
            SetRect(label.rectTransform, rectSize, pos);
            label.textWrappingMode = TextWrappingModes.Normal;

            changed = true;
            return label;
        }

        /// <summary>글자를 얹은 버튼 하나. 배경 Image 가 곧 targetGraphic 이다.</summary>
        private static Button EnsureActionButton(
            Transform parent, string name, string label, Color color, TMP_FontAsset font,
            Vector2 size, Vector2 pos, ref bool changed)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return existing.GetComponent<Button>();

            GameObject go = NewUIObject(name, parent);
            var background = go.AddComponent<Image>();
            background.color = color;

            var button = go.AddComponent<Button>();
            button.targetGraphic = background;
            SetRect((RectTransform)go.transform, size, pos);

            TMP_Text text = NewLabel("Label", go.transform, font, 30f, TextAlignmentOptions.Center, Color.white);
            text.text = label;
            SetRect(text.rectTransform, size, Vector2.zero);

            changed = true;
            return button;
        }

        /// <summary>이름으로 계층 전체를 훑는다. 첫 번째 것을 준다.</summary>
        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindDeep(root.GetChild(i), name);
                if (hit != null)
                    return hit;
            }

            return null;
        }

        /// <summary>
        /// 성장 목록 칸에 <b>딤 + 자물쇠 + 언락 안내</b>를 단다.
        ///
        /// <b>멱등하다.</b> 이미 있는 것은 건드리지 않고 <b>없는 것만</b> 만든다 —
        /// 두 번 눌러도 덮개가 둘이 되지 않고, 손으로 옮겨 둔 위치·색도 그대로 남는다.
        /// 굽기 도구가 사람의 조정을 조용히 되돌리면 값이 왜 바뀌었는지 아무도 못 찾는다.
        ///
        /// <b>항목별로 판정하는 것이 중요하다.</b> "LockedRoot 가 있으면 끝" 으로 두면
        /// 나중에 안내 줄을 더했을 때 <b>이미 자물쇠를 단 프리팹에는 영영 안 들어간다</b> —
        /// 실제로 이 도구가 한 번 돈 뒤에 안내 줄이 추가됐다.
        ///
        /// <b>이 프리팹은 손으로 만든 것이라 <c>Create()</c> 가 없다.</b> 그래서 통째로 다시
        /// 굽지 않고 필요한 오브젝트만 얹는다. YAML 직접 편집은 금지라(AGENTS 절대규칙 3)
        /// 이 경로가 유일한 방법이다.
        /// </summary>
        [MenuItem("OJ/개발/다이스 언락/성장 목록 칸에 자물쇠 달기")]
        private static void InstallGrowthItemLock()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GrowthItemPath);
            if (prefab == null)
            {
                Debug.LogError("[굽기] 성장 목록 칸 프리팹을 못 찾았다: " + GrowthItemPath);
                return;
            }

            TMP_FontAsset font = FindKoreanFont();
            if (font == null)
            {
                // 안내 줄이 한글이라 폰트 없이 구우면 네모로 저장된다.
                Debug.LogError("[굽기] 한글 TMP 폰트를 못 찾았다. 프리팹을 건드리지 않는다.");
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(GrowthItemPath);
            try
            {
                var item = contents.GetComponent<UIDiceGrowthItem>();
                if (item == null)
                {
                    Debug.LogError("[굽기] " + GrowthItemPath + " 에 UIDiceGrowthItem 이 없다.");
                    return;
                }

                var so = new SerializedObject(item);
                bool changed = false;

                Transform lockedRoot = EnsureLockedRoot(contents.transform, ref changed);
                changed |= EnsureLockIcon(lockedRoot);
                changed |= RaiseLegacyLockIcon(lockedRoot);

                // 자물쇠 <b>아래</b>에 필요 재화와 조건이 붙는다. 칸이 220x300 남짓이라
                // 자물쇠를 위로 올려 자리를 낸다.
                //
                // <b>순서가 중요하다.</b> 줄을 먼저 만들고 나서 옛것을 그 안으로 들여야 한다 —
                // 반대로 하면 옮길 곳이 없어 아무것도 안 옮겨지고, 이어지는 Ensure 가 줄 안에
                // 새로 만들어 <b>옛것과 둘이 된다.</b> 실제로 그렇게 아이콘이 두 개가 됐다.
                Transform costRow = EnsureCostRow(lockedRoot, ref changed);
                changed |= ConsolidateCostParts(lockedRoot, costRow);

                Image costIcon = EnsureCostIcon(costRow, ref changed);
                TMP_Text costText = EnsureCostText(costRow, font, ref changed);
                TMP_Text conditionText = EnsureConditionText(lockedRoot, font, ref changed);

                changed |= Wire(so, "lockedRoot", lockedRoot.gameObject);
                changed |= Wire(so, "unlockCostIcon", costIcon);
                changed |= Wire(so, "unlockCostText", costText);
                changed |= Wire(so, "unlockConditionText", conditionText);

                if (!changed)
                {
                    Debug.Log("[굽기] 이미 다 달려 있다. 아무것도 바꾸지 않았다: " + GrowthItemPath);
                    return;
                }

                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(contents, GrowthItemPath, out bool ok);
                if (!ok)
                {
                    Debug.LogError("[굽기] 프리팹 저장에 실패했다: " + GrowthItemPath);
                    return;
                }

                Debug.Log("[굽기] 자물쇠와 언락 안내를 달았다: " + GrowthItemPath +
                          System.Environment.NewLine +
                          "  Tools/ui/diff_prefab.py 로 한 번 더 굽는 것이 멱등인지 확인할 수 있다.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>
        /// 직렬화 필드를 잇는다. <b>이미 같은 것을 가리키면 바꾸지 않는다</b> —
        /// 그래야 "바뀐 것이 있나" 판정이 정확해지고, 아무것도 안 바뀐 저장이 일어나지 않는다.
        /// </summary>
        private static bool Wire(SerializedObject so, string fieldName, Object value)
        {
            SerializedProperty property = so.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError("[굽기] " + fieldName + " 필드를 못 찾았다. 스크립트가 컴파일됐는지 확인할 것.");
                return false;
            }

            if (property.objectReferenceValue == value)
                return false;

            property.objectReferenceValue = value;
            return true;
        }

        private static Transform EnsureLockedRoot(Transform parent, ref bool changed)
        {
            Transform existing = parent.Find(LockedRootName);
            if (existing != null)
                return existing;

            GameObject go = NewUIObject(LockedRootName, parent);
            Stretch((RectTransform)go.transform);

            Image dim = go.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.62f);
            dim.raycastTarget = false;

            changed = true;
            return go.transform;
        }

        /// <summary>
        /// 자물쇠 아이콘. 못 찾으면 딤만 남긴다 — <b>없는 그림을 지어내지 않는다.</b>
        /// 위쪽으로 올려 아래에 안내 두 줄이 들어갈 자리를 만든다.
        /// </summary>
        private static bool EnsureLockIcon(Transform lockedRoot)
        {
            if (lockedRoot.Find("Lock") != null)
                return false;

            Sprite lockSprite = FindLockSprite();
            if (lockSprite == null)
                return false;

            GameObject go = NewUIObject("Lock", lockedRoot);
            var icon = go.AddComponent<Image>();
            icon.sprite = lockSprite;
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            SetRect((RectTransform)go.transform, new Vector2(56f, 56f), new Vector2(0f, 62f));
            return true;
        }

        /// <summary>
        /// 이 도구의 <b>옛 판</b>이 달아 둔 자물쇠를 위로 올린다.
        ///
        /// 옛 판은 안내 줄이 없어서 자물쇠를 칸 정중앙(0,0)에 놓았는데, 지금은 그 자리에
        /// 재화 줄이 온다 — 그대로 두면 <b>숫자 위에 자물쇠가 겹친다.</b>
        ///
        /// <b>정확히 옛 기본값일 때만 옮긴다.</b> 사람이 한 번이라도 움직였으면 그 값이
        /// 곧 결정이고, 도구가 그것을 되돌리면 왜 바뀌었는지 아무도 못 찾는다.
        /// </summary>
        private static bool RaiseLegacyLockIcon(Transform lockedRoot)
        {
            Transform lockIcon = lockedRoot.Find("Lock");
            if (lockIcon == null)
                return false;

            var rect = (RectTransform)lockIcon;
            if (rect.anchoredPosition != Vector2.zero)
                return false;

            rect.anchoredPosition = new Vector2(0f, 62f);
            return true;
        }

        /// <summary>
        /// 아이콘 + 숫자를 한 덩어리로 묶어 <b>가운데</b>에 놓는다.
        ///
        /// <b>왜 레이아웃 그룹인가.</b> 둘을 각자 좌표로 놓으면 숫자의 자릿수("45/120" 과
        /// "5/120")에 따라 덩어리 폭이 바뀌어 중심이 매번 어긋난다. 실제로 그래서
        /// 가운데 정렬이 안 맞아 보였다. <c>HorizontalLayoutGroup</c> 이 자식 폭을 재서
        /// 가운데로 모으고, <c>ContentSizeFitter</c> 가 덩어리 폭을 내용에 맞춘다 —
        /// 그러면 자릿수가 몇이든 중심이 0 이다.
        /// </summary>
        /// <summary>레이아웃이 크기를 정할 수 있게 <c>LayoutElement</c> 를 보장한다. 폭 0 은 "재서 쓰라".</summary>
        private static void EnsureLayoutElement(GameObject go, float width, float height)
        {
            var element = go.GetComponent<LayoutElement>();
            if (element == null)
                element = go.AddComponent<LayoutElement>();

            element.preferredWidth = width > 0f ? width : -1f;
            element.preferredHeight = height;
        }

        private static Transform EnsureCostRow(Transform lockedRoot, ref bool changed)
        {
            Transform existing = lockedRoot.Find("UnlockCostRow");
            if (existing != null)
                return existing;

            GameObject go = NewUIObject("UnlockCostRow", lockedRoot);
            SetRect((RectTransform)go.transform, new Vector2(200f, 40f), new Vector2(0f, -8f));

            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            changed = true;
            return go.transform;
        }

        /// <summary>
        /// 재화 줄의 아이콘·글자를 <b>하나로 모은다.</b>
        ///
        /// 옛 판은 둘을 <c>LockedRoot</c> 바로 밑에 각자 좌표로 놓았고, 그 뒤 이 도구가
        /// 줄 안에 또 만들어 <b>둘씩</b> 생긴 프리팹이 실제로 나왔다. 그래서 두 가지를 한다 —
        /// 줄 밖에 있는 것은 안으로 들이고, 줄 안에 이미 있으면 밖의 것은 지운다.
        ///
        /// <b>지우는 쪽을 밖으로 고른 이유</b>는 줄 안의 것이 레이아웃에 잡혀 있어서다.
        /// 밖의 것은 어차피 아무도 참조하지 않는 유령이 된다.
        /// </summary>
        private static bool ConsolidateCostParts(Transform lockedRoot, Transform costRow)
        {
            bool changed = false;
            changed |= Consolidate(lockedRoot, costRow, "UnlockCostIcon");
            changed |= Consolidate(lockedRoot, costRow, "UnlockCostText");
            return changed;
        }

        private static bool Consolidate(Transform outside, Transform row, string name)
        {
            Transform stray = outside.Find(name);
            if (stray == null)
                return false;

            if (row.Find(name) != null)
            {
                // 줄 안에 이미 있다. 밖의 것은 갱신도 안 되고 꺼지지도 않는 유령이라 지운다.
                Object.DestroyImmediate(stray.gameObject);
                return true;
            }

            stray.SetParent(row, false);
            return true;
        }

        private static Image EnsureCostIcon(Transform costRow, ref bool changed)
        {
            Transform existing = costRow.Find("UnlockCostIcon");
            if (existing != null)
            {
                // 이사돼 온 것은 LayoutElement 가 없다. 없으면 레이아웃이 크기를 못 정한다.
                EnsureLayoutElement(existing.gameObject, 34f, 34f);
                return existing.GetComponent<Image>();
            }

            GameObject go = NewUIObject("UnlockCostIcon", costRow);
            var icon = go.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            // 레이아웃 그룹이 크기를 정하므로 좌표가 아니라 LayoutElement 로 말한다.
            var element = go.AddComponent<LayoutElement>();
            element.preferredWidth = 34f;
            element.preferredHeight = 34f;

            changed = true;
            return icon;
        }

        private static TMP_Text EnsureCostText(Transform costRow, TMP_FontAsset font, ref bool changed)
        {
            Transform existing = costRow.Find("UnlockCostText");
            if (existing != null)
            {
                // 폭은 0 으로 둔다 — TMP 의 preferredWidth 를 레이아웃이 읽어 가야
                // 자릿수가 늘어도 안 잘리고 가운데가 안 어긋난다.
                EnsureLayoutElement(existing.gameObject, 0f, 36f);
                return existing.GetComponent<TMP_Text>();
            }

            TMP_Text label = NewLabel("UnlockCostText", costRow, font, 26f,
                TextAlignmentOptions.Left, new Color(0.95f, 0.85f, 0.55f, 1f));

            // 높이만 고정한다. 폭은 TMP 의 preferredWidth 를 레이아웃이 읽어 간다 —
            // 고정하면 자릿수가 늘 때 잘리고, 그러면 가운데 정렬도 다시 어긋난다.
            var element = label.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 36f;

            changed = true;
            return label;
        }

        private static TMP_Text EnsureConditionText(Transform lockedRoot, TMP_FontAsset font, ref bool changed)
        {
            Transform existing = lockedRoot.Find("UnlockConditionText");
            if (existing != null)
                return existing.GetComponent<TMP_Text>();

            TMP_Text label = NewLabel("UnlockConditionText", lockedRoot, font, 22f,
                TextAlignmentOptions.Center, new Color(0.80f, 0.84f, 0.92f, 1f));
            SetRect(label.rectTransform, new Vector2(200f, 60f), new Vector2(0f, -62f));
            label.textWrappingMode = TextWrappingModes.Normal;

            changed = true;
            return label;
        }

        private static GameObject NewUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            go.transform.SetParent(parent, false);
            return go;
        }

        private static TMP_Text NewLabel(
            string name, Transform parent, TMP_FontAsset font, float size,
            TextAlignmentOptions align, Color color)
        {
            var label = NewUIObject(name, parent).AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = size;
            label.alignment = align;
            label.color = color;
            label.raycastTarget = false;
            label.text = string.Empty;
            return label;
        }

        private static void SetRect(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Sprite FindLockSprite()
        {
            return AssetDatabase.FindAssets("Icon_Unlock t:Sprite")
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p, System.StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
                .FirstOrDefault(s => s != null);
        }

        private static TMP_FontAsset FindKoreanFont()
        {
            const int Sample = '가';

            return AssetDatabase.FindAssets("t:TMP_FontAsset")
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p, System.StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>)
                .FirstOrDefault(f => f != null && f.HasCharacter(Sample));
        }
    }
}
