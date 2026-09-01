using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OJ.EditorTools
{
    public static class SpriteNormalMapApplier
    {
        private const string NormalSuffix = "_normal";
        private const string SecondaryTextureName = "_NormalMap";

        private static readonly string[] TextureExtensions =
        {
            ".png",
            ".jpg",
            ".jpeg",
            ".tga",
            ".psd",
            ".tif",
            ".tiff",
            ".bmp"
        };

        [MenuItem("Tools/OJ/Sprite Normal Maps/Apply To Selection")]
        [MenuItem("Assets/OJ/Apply Sprite Normal Maps", false, 2000)]
        public static void ApplyToSelection()
        {
            string[] rootPaths = Selection.assetGUIDs
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .ToArray();

            if (rootPaths.Length == 0)
                rootPaths = new[] { "Assets" };

            Apply(rootPaths);
        }

        [MenuItem("Tools/OJ/Sprite Normal Maps/Apply To All Assets")]
        public static void ApplyToAllAssets()
        {
            Apply(new[] { "Assets" });
        }

        private static void Apply(IReadOnlyList<string> rootPaths)
        {
            List<string> sourceTexturePaths = FindSourceTexturePaths(rootPaths);
            int appliedCount = 0;
            int skippedCount = 0;

            try
            {
                AssetDatabase.StartAssetEditing();

                for (int i = 0; i < sourceTexturePaths.Count; i++)
                {
                    string sourcePath = sourceTexturePaths[i];
                    EditorUtility.DisplayProgressBar(
                        "Apply Sprite Normal Maps",
                        sourcePath,
                        sourceTexturePaths.Count > 0 ? (float)i / sourceTexturePaths.Count : 1.0f);

                    string normalPath = FindNormalMapPath(sourcePath);
                    if (string.IsNullOrEmpty(normalPath))
                    {
                        skippedCount++;
                        continue;
                    }

                    if (ApplyNormalMap(sourcePath, normalPath))
                        appliedCount++;
                    else
                        skippedCount++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
            }

            Debug.Log($"Sprite normal map apply complete. Applied: {appliedCount}, Skipped: {skippedCount}");
        }

        private static List<string> FindSourceTexturePaths(IReadOnlyList<string> rootPaths)
        {
            HashSet<string> paths = new HashSet<string>();

            foreach (string rootPath in rootPaths)
            {
                if (AssetDatabase.IsValidFolder(rootPath))
                {
                    string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { rootPath });
                    foreach (string guid in guids)
                        AddIfSourceTexture(paths, AssetDatabase.GUIDToAssetPath(guid));

                    continue;
                }

                AddIfSourceTexture(paths, rootPath);
            }

            return paths.OrderBy(path => path).ToList();
        }

        private static void AddIfSourceTexture(HashSet<string> paths, string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return;

            if (!IsTexturePath(assetPath))
                return;

            if (IsNormalMapName(assetPath))
                return;

            if (!(AssetImporter.GetAtPath(assetPath) is TextureImporter))
                return;

            paths.Add(assetPath);
        }

        private static bool ApplyNormalMap(string sourcePath, string normalPath)
        {
            TextureImporter sourceImporter = AssetImporter.GetAtPath(sourcePath) as TextureImporter;
            TextureImporter normalImporter = AssetImporter.GetAtPath(normalPath) as TextureImporter;
            Texture2D normalTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);

            if (sourceImporter == null || normalImporter == null || normalTexture == null)
                return false;

            ConfigureNormalTexture(normalImporter);

            List<SecondarySpriteTexture> secondaryTextures = sourceImporter.secondarySpriteTextures?.ToList()
                ?? new List<SecondarySpriteTexture>();

            SecondarySpriteTexture normalSecondaryTexture = new SecondarySpriteTexture
            {
                name = SecondaryTextureName,
                texture = normalTexture
            };

            secondaryTextures.RemoveAll(texture =>
                texture.name == SecondaryTextureName || texture.name == "NormalMap");
            secondaryTextures.Add(normalSecondaryTexture);

            sourceImporter.secondarySpriteTextures = secondaryTextures.ToArray();
            sourceImporter.SaveAndReimport();
            return true;
        }

        private static void ConfigureNormalTexture(TextureImporter normalImporter)
        {
            bool changed = false;

            if (normalImporter.sRGBTexture)
            {
                normalImporter.sRGBTexture = false;
                changed = true;
            }

            if (normalImporter.alphaIsTransparency)
            {
                normalImporter.alphaIsTransparency = false;
                changed = true;
            }

            if (normalImporter.mipmapEnabled)
            {
                normalImporter.mipmapEnabled = false;
                changed = true;
            }

            if (changed)
                normalImporter.SaveAndReimport();
        }

        private static string FindNormalMapPath(string sourcePath)
        {
            string directory = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
            string sourceName = Path.GetFileNameWithoutExtension(sourcePath);

            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(sourceName))
                return null;

            foreach (string extension in TextureExtensions)
            {
                string candidatePath = $"{directory}/{sourceName}{NormalSuffix}{extension}";
                if (AssetImporter.GetAtPath(candidatePath) is TextureImporter)
                    return candidatePath;
            }

            return null;
        }

        private static bool IsTexturePath(string assetPath)
        {
            string extension = Path.GetExtension(assetPath);
            return TextureExtensions.Any(textureExtension =>
                string.Equals(textureExtension, extension, System.StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsNormalMapName(string assetPath)
        {
            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            return fileName.EndsWith(NormalSuffix, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
