using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using OJ.Equipment;

namespace OJ.EditorTools
{
    /// <summary>
    /// 장비 UI 를 2026-10-22 시안(<c>Docs/EquipmentUIArtPort.md</c>)으로 굽는다.
    ///
    /// 1. <b>강화 팝업</b>(<c>UIEquipmentConfirmDialog.prefab</c>) — 루트는 그대로 두고 자식만 지운 뒤
    ///    <see cref="UIEquipmentConfirmDialog.Build"/> 로 다시 짓는다. 루트를 새로 만들면 컴포넌트
    ///    fileID 가 바뀌어 장비 페이지(이 프리팹을 중첩)와 카탈로그의 참조가 조용히 None 이 된다.
    /// 2. <b>장비 페이지</b>(<c>UIEquipmentPage.prefab</c>) — 아래쪽 탭 바(장착 효과/보석)와 보석 목록,
    ///    꺼져 있던 푸터를 지우고 장착 효과 영역을 탭 바 자리까지 늘린다. 푸터의 레벨업 버튼은
    ///    "일괄 레벨업" 으로 캐릭터 아래에, 전투력 띠는 캐릭터 위로 옮긴다(시안 Item_Slot_NewLayout).
    ///
    /// 다시 돌려도 안전하다 — 1 은 매번 같은 결과를 새로 짓고, 2 는 이미 옮겨진 것을 건너뛴다.
    /// 끝에 사후 검사를 찍는다.
    /// </summary>
    public static class EquipmentUIPrefabBaker
    {
        private const string DialogPath = "Assets/Prefab/Refactory/LobbyScene/UIEquipmentConfirmDialog.prefab";
        private const string PagePath = "Assets/Prefab/Refactory/LobbyScene/UIEquipmentPage.prefab";
        private const string FontPath = "Assets/BMHANNAProOTF/BMHANNAProOTF SDF.asset";

        [MenuItem("OJ/개발/장비 강화 팝업 굽기")]
        public static void Bake()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
            {
                Debug.LogError("[장비 굽기] BM HANNA 폰트가 없다: " + FontPath);
                return;
            }

            var log = new StringBuilder("[장비 굽기]\n");
            BakeDialog(font, log);
            BakePage(log);

            AssetDatabase.SaveAssets();
            Verify(log);
            Debug.Log(log.ToString());
        }

        private static void BakeDialog(TMP_FontAsset font, StringBuilder log)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(DialogPath);
            try
            {
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

                var dialog = root.GetComponent<UIEquipmentConfirmDialog>();
                var so = new SerializedObject(dialog);
                so.FindProperty("_exitBtn").ClearArray();
                so.ApplyModifiedPropertiesWithoutUndo();

                dialog.Build(font);
                PrefabUtility.SaveAsPrefabAsset(root, DialogPath);
                log.AppendLine("  팝업: 다시 지었다");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void BakePage(StringBuilder log)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PagePath);
            try
            {
                Transform group = root.transform.Find("View/ElementGroup");
                Transform character = group.Find("CharacterArea");

                // 일괄 레벨업 — 푸터의 버튼을 옮겨 쓴다(글자 폰트·재질을 그대로 이어받으려고).
                // Big_Btn_Yellow x3, 보이는 (398,822)~(714,942). CharacterArea 가운데는 PSD y 550.
                Transform levelUpAll = character.Find("BtnLevelUpAll");
                if (levelUpAll == null)
                {
                    Transform footerButton = group.Find("Footer/BtnLevelUp");
                    if (footerButton != null)
                    {
                        footerButton.SetParent(character, false);
                        footerButton.name = "BtnLevelUpAll";
                        levelUpAll = footerButton;
                        log.AppendLine("  페이지: 푸터 레벨업 버튼 → BtnLevelUpAll");
                    }
                }

                if (levelUpAll != null)
                {
                    var rect = (RectTransform)levelUpAll;
                    SetCenter(rect, new Vector2(340f, 144f), new Vector2(556f - 540f, 550f - 876f));
                    var image = levelUpAll.GetComponent<Image>();
                    image.type = Image.Type.Sliced;
                    image.pixelsPerUnitMultiplier = 1f / 3f;

                    TMP_Text label = levelUpAll.GetComponentInChildren<TMP_Text>(true);
                    label.text = "일괄 레벨업";
                    label.fontSize = 45f;
                    var labelRect = label.rectTransform;
                    labelRect.anchorMin = Vector2.zero;
                    labelRect.anchorMax = Vector2.one;
                    labelRect.offsetMin = new Vector2(0f, 6f);   // 아래 턱(6x3=18 중 일부)을 피한다
                    labelRect.offsetMax = new Vector2(0f, -18f); // 위 여백 6px x3
                    levelUpAll.SetAsLastSibling();
                }

                // 전투력 띠 — 캐릭터 위, 보이는 (378,155)~(692,231). 검정 80%(시안 주석).
                Transform power = FindChildWithChild(character, "TotalAttack");
                if (power != null)
                {
                    SetCenter((RectTransform)power, new Vector2(314f + 52f, 76f + 52f), new Vector2(535f + 2f - 540f, 550f - (193f + 2f)));
                    power.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.8f);
                }

                // 탭 바·보석 목록·푸터 — 탭이 없어졌다. 장착 효과 영역을 탭 바 위쪽(PSD 1050)까지 늘린다.
                DestroyIfExists(group, "Title", log);
                DestroyIfExists(group, "Footer", log);
                Transform inventory = group.Find("InventoryArea");
                DestroyIfExists(inventory, "InventoryScroll", log);
                var inventoryRect = (RectTransform)inventory;
                // 긴 화면용으로 이미 세로로 늘려 뒀으면(TallScreenLayoutBaker) 그대로 둔다 — 1920 에서 870 자리와 같다.
                if (Mathf.Approximately(inventoryRect.anchorMin.y, inventoryRect.anchorMax.y))
                {
                    inventoryRect.anchoredPosition = new Vector2(inventoryRect.anchoredPosition.x, 870f);
                    inventoryRect.sizeDelta = new Vector2(inventoryRect.sizeDelta.x, 870f);
                }

                // 강화 팝업은 맨 위에 그려져야 한다 — 예전엔 절반 창이라 아래 영역 뒤에 있어도 됐다.
                Transform dialog = group.Find("UIEquipmentConfirmDialog");
                dialog.SetAsLastSibling();

                var page = root.GetComponent<UIEquipmentPage>();
                var so = new SerializedObject(page);
                so.FindProperty("levelUpAllButton").objectReferenceValue =
                    levelUpAll != null ? levelUpAll.GetComponent<Button>() : null;
                so.FindProperty("confirmDialog").objectReferenceValue = dialog.GetComponent<UIEquipmentConfirmDialog>();
                so.ApplyModifiedPropertiesWithoutUndo();

                TallScreenLayoutBaker.ApplyEquipmentPage(root.transform, log);

                PrefabUtility.SaveAsPrefabAsset(root, PagePath);
                log.AppendLine("  페이지: 저장했다");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void Verify(StringBuilder log)
        {
            GameObject page = AssetDatabase.LoadAssetAtPath<GameObject>(PagePath);
            var so = new SerializedObject(page.GetComponent<UIEquipmentPage>());
            Check(log, "페이지.confirmDialog", so.FindProperty("confirmDialog").objectReferenceValue != null);
            Check(log, "페이지.levelUpAllButton", so.FindProperty("levelUpAllButton").objectReferenceValue != null);
            Check(log, "페이지.equippedEffectRoot", so.FindProperty("equippedEffectRoot").objectReferenceValue != null);
            Check(log, "탭 바 없음", page.transform.Find("View/ElementGroup/Title") == null);

            GameObject dialog = AssetDatabase.LoadAssetAtPath<GameObject>(DialogPath);
            var dso = new SerializedObject(dialog.GetComponent<UIEquipmentConfirmDialog>());
            Check(log, "팝업.dialogView", dso.FindProperty("dialogView").objectReferenceValue != null);
            Check(log, "팝업.gemRows = " + Define.MaxEquipmentSlot, dso.FindProperty("gemRows").arraySize == Define.MaxEquipmentSlot);
            Check(log, "팝업.gemPopup", dso.FindProperty("gemPopup").objectReferenceValue != null);
            Check(log, "팝업.font", dso.FindProperty("font").objectReferenceValue != null);
        }

        private static void Check(StringBuilder log, string what, bool ok)
        {
            log.AppendLine((ok ? "  OK   " : "  FAIL ") + what);
        }

        private static Transform FindChildWithChild(Transform parent, string grandChildName)
        {
            foreach (Transform child in parent)
            {
                if (child.Find(grandChildName) != null)
                    return child;
            }

            return null;
        }

        private static void DestroyIfExists(Transform parent, string name, StringBuilder log)
        {
            Transform target = parent.Find(name);
            if (target == null)
                return;

            Object.DestroyImmediate(target.gameObject);
            log.AppendLine("  페이지: 지웠다 " + parent.name + "/" + name);
        }

        private static void SetCenter(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }
    }
}
