#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using OJ.Dice;
using OJ.Equipment;
using OJ.Point;
using OJ.Relic;
using OJ.Stage;
using OJ.StageReward;
using OJ.Utils;

namespace OJ.EditorTools
{
    /// <summary>
    /// TitleScene 에만 있는 StaticResource 를 Resources 프리팹으로 뽑는다.
    ///
    /// 왜 필요한가: StaticResource 는 TitleScene 에 단 하나 있고 데이터베이스 6개를
    /// 전부 물고 있다. Lobby/Battle 을 직접 재생하면 이게 없어서 MonoSingleton 이
    /// 빈 인스턴스를 만들고, Provider 들이 Resources.Load -> 코드 기본값 순으로 조용히
    /// 내려간다. 그런데 Resources.Load 폴백이 있는 DB 는 3개뿐이고 그중 2개는 에셋이
    /// Resources 규약 밖(Assets/ScriptableObject/)이라 <b>항상</b> 실패한다.
    /// 프리팹 하나로 6개를 한꺼번에 되살린다. (MIGRATION_BASELINE 2.1)
    ///
    /// 자동 실행하지 않는다. 씬을 건드리는 에디터 훅은 1.1 에서 전부 걷어냈다.
    /// </summary>
    public static class StaticResourcePrefabExtractor
    {
        private const string TitleScenePath = "Assets/Scenes/TitleScene.unity";
        private const string ResourcesFolder = "Assets/Resources";

        [MenuItem("Tools/OJ/Static Resource/Extract Prefab From TitleScene")]
        public static void Extract()
        {
            string prefabPath;
            if (!TryResolvePrefabPath(out prefabPath))
                return;

            Scene titleScene = SceneManager.GetSceneByPath(TitleScenePath);
            bool wasAlreadyLoaded = titleScene.IsValid() && titleScene.isLoaded;
            if (!wasAlreadyLoaded)
                titleScene = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Additive);

            try
            {
                StaticResource source = FindInScene(titleScene);
                if (source == null)
                {
                    Debug.LogError($"{TitleScenePath} 에서 StaticResource 를 찾지 못했다. 추출을 중단한다.");
                    return;
                }

                if (PrefabUtility.IsPartOfPrefabInstance(source.gameObject))
                {
                    Debug.LogWarning("StaticResource 가 이미 프리팹 인스턴스다. 다시 뽑는 대신 " +
                                     "기존 프리팹에 Apply 하는 것이 맞는지 먼저 확인할 것.");
                }

                int before = CountAssignedDatabases(source);

                GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(
                    source.gameObject, prefabPath, InteractionMode.UserAction);

                if (prefab == null)
                {
                    Debug.LogError($"프리팹 저장에 실패했다: {prefabPath}");
                    return;
                }

                if (titleScene.isDirty)
                    EditorSceneManager.SaveScene(titleScene);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Verify(prefabPath, before);
            }
            finally
            {
                if (!wasAlreadyLoaded && titleScene.IsValid() && titleScene.isLoaded)
                    EditorSceneManager.CloseScene(titleScene, true);
            }
        }

        /// <summary>경로 정본은 StaticResource 에 붙은 [SingletonPrefab] 이다. 여기서 지어내지 않는다.</summary>
        private static bool TryResolvePrefabPath(out string prefabPath)
        {
            prefabPath = null;

            var attribute = (SingletonPrefabAttribute)Attribute.GetCustomAttribute(
                typeof(StaticResource), typeof(SingletonPrefabAttribute));

            if (attribute == null)
            {
                Debug.LogError("StaticResource 에 [SingletonPrefab] 이 없다. 런타임이 프리팹을 " +
                               "찾지 않으므로 지금 뽑아 봐야 쓰이지 않는다.");
                return false;
            }

            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            {
                Debug.LogError($"{ResourcesFolder} 폴더가 없다.");
                return false;
            }

            prefabPath = $"{ResourcesFolder}/{attribute.ResourcePath}.prefab";
            return true;
        }

        private static StaticResource FindInScene(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                StaticResource found = roots[i].GetComponentInChildren<StaticResource>(true);
                if (found != null)
                    return found;
            }

            return null;
        }

        /// <summary>
        /// 뽑아 낸 프리팹의 참조가 실제로 살아 있는지 본다. 여기서 세지 않으면
        /// "프리팹은 생겼는데 필드가 전부 None" 을 런타임까지 못 알아챈다.
        /// </summary>
        private static void Verify(string prefabPath, int expectedDatabaseCount)
        {
            GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (saved == null)
            {
                Debug.LogError($"저장 직후 프리팹을 다시 읽지 못했다: {prefabPath}");
                return;
            }

            StaticResource resource = saved.GetComponent<StaticResource>();
            if (resource == null)
            {
                Debug.LogError($"{prefabPath} 에 StaticResource 컴포넌트가 없다.");
                return;
            }

            List<string> missing = MissingDatabases(resource);
            int after = CountAssignedDatabases(resource);

            Debug.Log($"StaticResource 프리팹 추출 완료: {prefabPath}\n" +
                      $"  데이터베이스 {after}/6 연결 (씬 원본은 {expectedDatabaseCount}/6)\n" +
                      $"  ElementResources={Count(resource.ElementResources)} " +
                      $"RarityResources={Count(resource.RarityResources)} " +
                      $"EquipmentResources={Count(resource.EquipmentResources)} " +
                      $"StageThemeResources={Count(resource.StageThemeResources)}");

            if (missing.Count > 0)
            {
                Debug.LogError("프리팹에서 비어 있는 데이터베이스: " + string.Join(", ", missing) +
                               " — 이대로면 직접 재생 시 그 데이터만 조용히 null 이 된다.");
            }

            if (after != expectedDatabaseCount)
            {
                Debug.LogError($"씬 원본은 {expectedDatabaseCount}개인데 프리팹은 {after}개다. " +
                               "추출 과정에서 참조가 끊겼다.");
            }

            Selection.activeObject = saved;
        }

        private static List<string> MissingDatabases(StaticResource resource)
        {
            var missing = new List<string>();
            if (resource.PointMetadataDatabase == null) missing.Add(nameof(resource.PointMetadataDatabase));
            if (resource.DiceMetaDataDatabase == null) missing.Add(nameof(resource.DiceMetaDataDatabase));
            if (resource.GemDefinitionDatabase == null) missing.Add(nameof(resource.GemDefinitionDatabase));
            if (resource.RelicDatabase == null) missing.Add(nameof(resource.RelicDatabase));
            if (resource.StageDatabase == null) missing.Add(nameof(resource.StageDatabase));
            if (resource.StageRewardDatabase == null) missing.Add(nameof(resource.StageRewardDatabase));
            return missing;
        }

        private static int CountAssignedDatabases(StaticResource resource)
        {
            return 6 - MissingDatabases(resource).Count;
        }

        private static int Count<T>(List<T> list)
        {
            return list != null ? list.Count : 0;
        }
    }
}
#endif
