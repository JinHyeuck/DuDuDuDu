using UnityEditor;
using UnityEngine;

namespace OJ.Editor
{
    public sealed class SpriteImportSettingsPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!(assetImporter is TextureImporter textureImporter))
                return;

            if (textureImporter.textureType != TextureImporterType.Sprite)
                return;

            textureImporter.spriteImportMode = SpriteImportMode.Single;
            textureImporter.filterMode = FilterMode.Point;
        }
    }
}
