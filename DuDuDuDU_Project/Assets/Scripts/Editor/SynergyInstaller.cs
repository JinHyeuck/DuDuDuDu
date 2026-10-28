using System.Linq;
using System.Text;
using OJ.Pinball;
using OJ.Point;
using OJ.Synergy;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OJ.EditorTools
{
    /// <summary>
    /// 시너지를 붙일 때 한 번 하는 일을 한 메뉴로 묶는다 — 메뉴가 여럿이면 하나를 빠뜨린다.
    ///
    ///  1. 시너지 창 프리팹(뼈대만; 내용은 <see cref="UISynergyDialog"/> 가 코드로 만든다)
    ///  2. 로비 입구 버튼 프리팹 + LobbyScene 에 심기(왼쪽 아이콘 줄, 특별한 7일 아래)
    ///  3. <see cref="PointType.SynergyTicket"/> 메타데이터
    ///  4. 핀볼 칸 경품에 시너지 티켓 보충
    ///  5. 다이얼로그 카탈로그 갱신
    ///
    /// <b>두 번 눌러도 안전하다.</b> 이미 있는 것은 건너뛰고(프리팹 뼈대만 다시 굽는다), 손으로 바꾼 경품·위치는 건드리지 않는다.
    /// 끝에 무엇이 되고 안 됐는지 한 줄씩 찍는다 — 일부만 성공한 상태를 에디터가 초록불로 덮지 않게.
    /// </summary>
    public static class SynergyInstaller
    {
        private const string PrefabFolder = "Assets/Prefab/Synergy";
        private const string DialogPath = PrefabFolder + "/UISynergyDialog.prefab";
        private const string EntryPath = PrefabFolder + "/UISynergyEntryButton.prefab";
        private const string LobbyScenePath = "Assets/Scenes/LobbyScene.unity";
        private const string MetadataPath = "Assets/ScriptableObject/PointMetadataDatabase.asset";
        private const string PinballPath = "Assets/ScriptableObject/PinballRewardDatabase.asset";

        /// <summary>왼쪽 아이콘 줄의 칸 간격(멤버십 -440 → 특별한 7일 -640).</summary>
        private const float LeftColumnStep = 200f;

        [MenuItem("OJ/개발/시너지/설치")]
        public static void Install()
        {
            var report = new StringBuilder("[시너지] 설치").AppendLine();
            System.IO.Directory.CreateDirectory(PrefabFolder);

            TMP_FontAsset font = FindKoreanFont();
            report.AppendLine(BuildDialogPrefab());
            report.AppendLine(BuildEntryPrefab(font));
            report.AppendLine(InstallIntoLobby());
            report.AppendLine(RegisterTicketMetadata());
            report.AppendLine(AddPinballTickets());

            AssetDatabase.SaveAssets();
            bool catalog = EditorApplication.ExecuteMenuItem("OJ/개발/다이얼로그 카탈로그/훑어서 갱신");
            report.AppendLine(catalog ? "  카탈로그: 갱신 메뉴 실행" : "  카탈로그: 실패 — 메뉴를 직접 누를 것");

            AssetDatabase.Refresh();
            Debug.Log(report.ToString());
        }

        private static string BuildDialogPrefab()
        {
            var root = new GameObject("UISynergyDialog", typeof(RectTransform));
            try
            {
                root.layer = 5;
                Stretch((RectTransform)root.transform);

                var view = new GameObject("View", typeof(RectTransform));
                view.layer = 5;
                view.transform.SetParent(root.transform, false);
                Stretch((RectTransform)view.transform);

                var dialog = root.AddComponent<UISynergyDialog>();
                dialog.dialogView = view;
                dialog.UseBackBtn = true;

                PrefabUtility.SaveAsPrefabAsset(root, DialogPath, out bool ok);
                return ok ? "  창 프리팹: " + DialogPath : "  창 프리팹: 저장 실패 " + DialogPath;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static string BuildEntryPrefab(TMP_FontAsset font)
        {
            var temp = new GameObject("__SynergyEntryBakeRoot");
            try
            {
                UISynergyEntryButton entry = UISynergyEntryButton.Create(temp.transform, font);
                GameObject go = entry.gameObject;
                go.transform.SetParent(null, false);

                PrefabUtility.SaveAsPrefabAsset(go, EntryPath, out bool ok);
                Object.DestroyImmediate(go);
                return ok ? "  입구 프리팹: " + EntryPath : "  입구 프리팹: 저장 실패 " + EntryPath;
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
        }

        private static string InstallIntoLobby()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EntryPath);
            if (prefab == null)
                return "  로비: 입구 프리팹이 없어 심지 못했다";

            Scene scene = SceneManager.GetSceneByPath(LobbyScenePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded)
                scene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Additive);

            string result;
            if (FindInScene<UISynergyEntryButton>(scene) != null)
            {
                result = "  로비: 입구가 이미 있다. 그대로 둔다(자리를 바꾸려면 씬에서 옮길 것)";
            }
            else
            {
                // 왼쪽 아이콘 줄(멤버십 → 특별한 7일)의 다음 칸. 처음에 탑 입구 위에 붙였더니 스테이지 입장 버튼을 가렸다.
                MonoBehaviour above = FindInScene<OJ.Shop.UISpecialSevenDaysLobbyButton>(scene);
                Transform parent = above != null ? above.transform.parent : FindCanvas(scene)?.transform;
                if (parent == null)
                {
                    result = "  로비: Canvas 를 못 찾아 심지 못했다";
                }
                else if (PrefabUtility.InstantiatePrefab(prefab, parent) is GameObject instance)
                {
                    instance.name = "UISynergyEntryButton";
                    var rect = (RectTransform)instance.transform;
                    rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = above != null
                        ? ((RectTransform)above.transform).anchoredPosition + new Vector2(0f, -LeftColumnStep)
                        : new Vector2(92f, -840f);
                    if (above != null)
                        rect.SetSiblingIndex(above.transform.GetSiblingIndex() + 1);

                    EditorSceneManager.MarkSceneDirty(scene);
                    result = "  로비: 입구를 심었다" + (above != null ? "(특별한 7일 아래)" : "(기본 자리)");
                }
                else
                {
                    result = "  로비: 프리팹 인스턴스 생성 실패";
                }
            }

            if (scene.isDirty)
                EditorSceneManager.SaveScene(scene);
            if (!wasLoaded)
                EditorSceneManager.CloseScene(scene, true);

            return result;
        }

        /// <summary>
        /// 티켓 메타데이터. <b>아이콘은 핀볼 티켓 그림을 임시로 빌린다</b> — 비워 두면 화면에 빈칸이 뜨고
        /// Resources 경로 보고서가 실패한다. 전용 그림이 나오면 PointMetadataDatabase.asset 에서 바꿀 것.
        /// </summary>
        private static string RegisterTicketMetadata()
        {
            var metadata = AssetDatabase.LoadAssetAtPath<PointMetadataDatabase>(MetadataPath);
            if (metadata == null)
                return "  메타데이터: PointMetadataDatabase 를 못 찾았다 " + MetadataPath;

            var so = new SerializedObject(metadata);
            SerializedProperty list = so.FindProperty("metadataList");
            if (list == null || !list.isArray)
                return "  메타데이터: metadataList 필드를 못 찾았다";

            Sprite borrowed = null;
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                int type = entry.FindPropertyRelative("pointType").intValue;
                if (type == (int)PointType.SynergyTicket)
                    return "  메타데이터: 이미 있다";
                if (type == (int)PointType.PinballTicket)
                    borrowed = entry.FindPropertyRelative("icon").objectReferenceValue as Sprite;
            }

            list.arraySize++;
            SerializedProperty added = list.GetArrayElementAtIndex(list.arraySize - 1);
            added.FindPropertyRelative("pointType").intValue = (int)PointType.SynergyTicket;
            added.FindPropertyRelative("displayName").stringValue = "시너지 티켓";
            added.FindPropertyRelative("description").stringValue = "시너지 옵션을 다시 굴릴 때 쓰는 티켓입니다.";
            added.FindPropertyRelative("icon").objectReferenceValue = borrowed;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(metadata);

            return "  메타데이터: 등록했다(아이콘은 핀볼 티켓 임시)";
        }

        /// <summary>핀볼 칸 경품에 시너지 티켓이 없으면 코드 기본값과 같은 양을 더한다. 이미 있는 칸은 건드리지 않는다.</summary>
        private static string AddPinballTickets()
        {
            var database = AssetDatabase.LoadAssetAtPath<PinballRewardDatabase>(PinballPath);
            if (database == null)
                return "  핀볼: 경품표 에셋이 없다 " + PinballPath;

            int added = 0;
            int[] amounts = PinballRewardDatabase.SynergyTicketsPerSlot;
            for (int i = 0; i < database.slotRewards.Count && i < amounts.Length; i++)
            {
                PinballSlotReward slot = database.slotRewards[i];
                if (slot == null || slot.rewards.Any(r => r.pointType == PointType.SynergyTicket))
                    continue;

                slot.rewards.Add(new PinballReward(PointType.SynergyTicket, amounts[i]));
                slot.label = (slot.label ?? string.Empty) + " + 시너지 티켓 " + amounts[i];
                added++;
            }

            if (added > 0)
                EditorUtility.SetDirty(database);

            return "  핀볼: 칸 " + added + "개에 시너지 티켓을 더했다(이미 있던 칸은 그대로)";
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Canvas FindCanvas(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                {
                    if (canvas.gameObject.name == "Canvas")
                        return canvas;
                }
            }

            return null;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null)
                    return found;
            }

            return null;
        }

        /// <summary>BM HANNA 를 먼저 찾는다(이 게임의 폰트). 없을 때만 한글이 있는 아무 폰트.</summary>
        private static TMP_FontAsset FindKoreanFont()
        {
            var hanna = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/BMHANNAProOTF/BMHANNAProOTF SDF.asset");
            if (hanna != null)
                return hanna;

            const int sample = '가';
            return AssetDatabase.FindAssets("t:TMP_FontAsset")
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p, System.StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>)
                .FirstOrDefault(f => f != null && f.HasCharacter(sample));
        }
    }
}
