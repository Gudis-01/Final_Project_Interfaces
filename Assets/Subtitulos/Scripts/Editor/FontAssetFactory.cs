using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Subtitulos.EditorTools
{
    /// <summary>
    /// Crea (una sola vez) los font assets de TextMeshPro a partir de archivos .ttf/.otf.
    /// Se crean "dinámicos": los caracteres se agregan al atlas cuando se usan por primera
    /// vez, así sirven acentos, ñ, ¿¡ y los íconos sin tener que preparar un set de caracteres.
    /// </summary>
    public static class FontAssetFactory
    {
        public static TMP_FontAsset GetOrCreate(string fontPath, int samplingPointSize = 90, int atlasSize = 1024,
                                                string assetPath = null)
        {
            assetPath ??= Path.ChangeExtension(fontPath, null) + " SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null) return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            if (font == null)
            {
                Debug.LogWarning($"[Subtítulos] No se encontró la fuente {fontPath}.");
                return null;
            }

            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, samplingPointSize, 9,
                GlyphRenderMode.SDFAA, atlasSize, atlasSize, AtlasPopulationMode.Dynamic, true);
            if (asset == null)
            {
                Debug.LogWarning($"[Subtítulos] No se pudo crear el font asset de {fontPath}.");
                return null;
            }

            asset.name = Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(asset, assetPath);

            // El material y la textura del atlas se guardan dentro del mismo asset.
            asset.material.name = asset.name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            foreach (Texture2D texture in asset.atlasTextures)
            {
                if (texture == null) continue;
                texture.name = asset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(texture, asset);
            }

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        /// <summary>Busca un font asset existente por nombre (p. ej. "Inter-Regular SDF").</summary>
        public static TMP_FontAsset Find(string name)
        {
            foreach (string guid in AssetDatabase.FindAssets(name + " t:TMP_FontAsset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null && asset.name == name) return asset;
            }
            return null;
        }
    }
}
