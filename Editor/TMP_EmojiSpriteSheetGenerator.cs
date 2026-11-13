using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.TextCore;

namespace TMP_Emoji.Editor
{
    public static class TMP_EmojiSpriteSheetGenerator
    {
        public static void GenerateEmojiSpriteData(Texture2D emojiTexture, TextAsset emojiDataJsonFile,
            TMP_EmojiPlatformType tmpEmojiPlatformType, int emojiSize = 64)
        {
            if (emojiTexture == null || emojiDataJsonFile == null)
            {
                Debug.LogError("Missing input file");
                return;
            }

            if (!GetImageSize(emojiTexture, out var textureWidth, out var textureHeight))
            {
                Debug.LogError("Failed to get Emoji texture original size");
                return;
            }

            string emojiDataText = $"{{\"items\": {emojiDataJsonFile.text}}}";
            TMP_EmojiDataWrapper tmpEmojiDataListWrapper = JsonUtility.FromJson<TMP_EmojiDataWrapper>(emojiDataText);
            TMP_EmojiData[] emojiDataList = tmpEmojiDataListWrapper.items;

            if (emojiDataList == null)
            {
                Debug.LogError("Failed to parse Emoji JSON data file");
                return;
            }

            if (!IsEmojiSizeValid(emojiDataList, textureWidth, emojiSize)) return;

            if (!EnsureMultipleSpriteImport(emojiTexture))
            {
                Debug.LogError("Failed to set Emoji texture import to Sprite (Multiple)");
                return;
            }

            SpriteDataProviderFactories factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider dataProvider = factory.GetSpriteEditorDataProviderFromObject(emojiTexture);
            dataProvider.InitSpriteEditorDataProvider();

            List<SpriteRect> newEmojiSprites = new List<SpriteRect>();
            List<SpriteNameFileIdPair> spriteNameFileIdPairs = new List<SpriteNameFileIdPair>();
            Dictionary<string, uint> emojiSpriteUnicodeMapping = new Dictionary<string, uint>();

            int emojiGridSize = emojiSize + 2;

            foreach (var emojiData in emojiDataList)
            {
                if (!IsEmojiDataSupported(emojiData, tmpEmojiPlatformType)) continue;

                string newSpriteName = emojiData.image;
                GUID newSpriteGuid = GUID.Generate();

                newEmojiSprites.Add(new SpriteRect
                {
                    name = newSpriteName,
                    alignment = SpriteAlignment.BottomLeft,
                    rect = new Rect(
                        emojiGridSize * emojiData.sheet_x,
                        textureHeight - emojiGridSize - emojiGridSize * emojiData.sheet_y,
                        emojiGridSize,
                        emojiGridSize
                    ),
                    spriteID = newSpriteGuid
                });

                spriteNameFileIdPairs.Add(new SpriteNameFileIdPair(newSpriteName, newSpriteGuid));

                emojiSpriteUnicodeMapping[newSpriteName] = GetSpriteUnicode(emojiData.unified);
            }

#if UNITY_2021_2_OR_NEWER
            ISpriteNameFileIdDataProvider spriteNameFileIdDataProvider =
                dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            spriteNameFileIdDataProvider.SetNameFileIdPairs(spriteNameFileIdPairs);
#endif

            dataProvider.SetSpriteRects(newEmojiSprites.ToArray());
            dataProvider.Apply();

            // Re-import the asset to have the changes applied
            AssetImporter assetImporter = dataProvider.targetObject as AssetImporter;
            if (assetImporter == null)
            {
                Debug.LogError("Failed to re-import Emoji Texture with editor sprite data");
                return;
            }

            assetImporter.SaveAndReimport();
            GenerateTMPEmojiSpriteAsset(emojiSpriteUnicodeMapping, emojiTexture, emojiSize);
        }

#if TMP_UGUI_2_0_OR_NEWER
        /// <summary>
        /// Builds the TMP sprite asset for TextMeshPro bundled with com.unity.ugui 2.0+ (Unity 2023.2 / Unity 6+).
        /// TMP_SpriteAssetMenu.CreateSpriteAsset() is not public in this version, so the asset is built here.
        /// An existing asset at the same path is reused when the sprite sheet is generated again.
        /// </summary>
        private static void GenerateTMPEmojiSpriteAsset(Dictionary<string, uint> emojiSpriteUnicodeMapping,
            Texture2D emojiTexture, int emojiSize)
        {
            string emojiTexturePath = AssetDatabase.GetAssetPath(emojiTexture);
            string spriteAssetPath = Path.ChangeExtension(emojiTexturePath, "asset");
            int glyphSize = emojiSize / 2;

            TMP_SpriteAsset tmpEmojiSpriteAsset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(spriteAssetPath);
            bool isNewAsset = tmpEmojiSpriteAsset == null;
            if (isNewAsset)
            {
                tmpEmojiSpriteAsset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
                tmpEmojiSpriteAsset.name = Path.GetFileNameWithoutExtension(spriteAssetPath);
            }

            // Mark the asset as the current format so TMP does not run its legacy upgrade path
            // (which would clear the sprite tables) when the asset is loaded.
            // Done through SerializedObject because the version setter is internal.
            SerializedObject serializedSpriteAsset = new SerializedObject(tmpEmojiSpriteAsset);
            serializedSpriteAsset.FindProperty("m_Version").stringValue = "1.1.0";
            serializedSpriteAsset.ApplyModifiedPropertiesWithoutUndo();

            // In this TMP version the hash codes are non-serialized caches behind public setters,
            // so they cannot be set through SerializedObject.
            tmpEmojiSpriteAsset.hashCode = TMP_TextUtilities.GetSimpleHashCode(tmpEmojiSpriteAsset.name);

            tmpEmojiSpriteAsset.spriteSheet = emojiTexture;
            tmpEmojiSpriteAsset.spriteGlyphTable.Clear();
            tmpEmojiSpriteAsset.spriteCharacterTable.Clear();

            // Same ordering as TextMeshPro's own sprite asset creation: top-to-bottom, left-to-right.
            List<Sprite> sprites = new List<Sprite>();
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(emojiTexturePath))
            {
                if (asset is Sprite sprite) sprites.Add(sprite);
            }

            if (sprites.Count == 0)
            {
                Debug.LogError($"Emoji texture {emojiTexturePath} has no sprites. Check its import settings.");
                return;
            }

            sprites.Sort((a, b) =>
            {
                int compareY = b.rect.y.CompareTo(a.rect.y);
                return compareY != 0 ? compareY : a.rect.x.CompareTo(b.rect.x);
            });

            uint glyphIndex = 0;
            foreach (Sprite sprite in sprites)
            {
                if (!TryGetUnicode(emojiSpriteUnicodeMapping, sprite.name, out uint unicode)) continue;

                TMP_SpriteGlyph spriteGlyph = new TMP_SpriteGlyph
                {
                    index = glyphIndex,
                    metrics = CreateGlyphMetrics(glyphSize),
                    glyphRect = CreateGlyphRect((int)sprite.rect.x, (int)sprite.rect.y, glyphSize),
                    scale = 1.0f,
                    atlasIndex = 0,
                    sprite = sprite
                };
                tmpEmojiSpriteAsset.spriteGlyphTable.Add(spriteGlyph);

                TMP_SpriteCharacter spriteCharacter = new TMP_SpriteCharacter(unicode, spriteGlyph)
                {
                    name = sprite.name,
                    scale = 1.0f
                };
                tmpEmojiSpriteAsset.spriteCharacterTable.Add(spriteCharacter);

                glyphIndex++;
            }

            if (isNewAsset)
            {
                AssetDatabase.CreateAsset(tmpEmojiSpriteAsset, spriteAssetPath);
            }

            EnsureSpriteAssetMaterial(tmpEmojiSpriteAsset, emojiTexture);

            tmpEmojiSpriteAsset.UpdateLookupTables();
            EditorUtility.SetDirty(tmpEmojiSpriteAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(spriteAssetPath);
        }

        /// <summary>
        /// Creates the TextMeshPro/Sprite material as a sub-asset of the sprite asset if it has none,
        /// and points the material at the sprite sheet.
        /// </summary>
        private static void EnsureSpriteAssetMaterial(TMP_SpriteAsset spriteAsset, Texture2D spriteSheet)
        {
            Material material = spriteAsset.material;
            if (material == null)
            {
                Shader shader = Shader.Find("TextMeshPro/Sprite");
                if (shader == null)
                {
                    Debug.LogError("Failed to find shader TextMeshPro/Sprite. Are the TMP Essential Resources imported?");
                    return;
                }

                material = new Material(shader)
                {
                    name = spriteAsset.name + " Material",
                    hideFlags = HideFlags.HideInHierarchy
                };
                AssetDatabase.AddObjectToAsset(material, spriteAsset);

                spriteAsset.material = material;
                spriteAsset.materialHashCode = TMP_TextUtilities.GetSimpleHashCode(material.name);
            }

            material.SetTexture("_MainTex", spriteSheet);
            EditorUtility.SetDirty(material);
        }
#else
        /// <summary>
        /// Builds the TMP sprite asset for the standalone com.unity.textmeshpro 3.0.x package (Unity 2022.3).
        /// TMP_SpriteAssetMenu.CreateSpriteAsset() is public in this version and sets up the asset,
        /// its material and its hash codes. Only the unicode values and glyph sizes are changed after it.
        /// </summary>
        private static void GenerateTMPEmojiSpriteAsset(Dictionary<string, uint> emojiSpriteUnicodeMapping,
            Texture2D emojiTexture, int emojiSize)
        {
            string emojiTexturePath = AssetDatabase.GetAssetPath(emojiTexture);
            int glyphSize = emojiSize / 2;

            Selection.activeObject = emojiTexture;
            TMP_SpriteAssetMenu.CreateSpriteAsset();

            TMP_SpriteAsset tmpEmojiSpriteAsset =
                AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(Path.ChangeExtension(emojiTexturePath, "asset"));
            if (tmpEmojiSpriteAsset == null)
            {
                Debug.LogError("Failed to create TMP sprite asset");
                return;
            }

            foreach (TMP_SpriteCharacter spriteCharacter in tmpEmojiSpriteAsset.spriteCharacterTable)
            {
                if (!TryGetUnicode(emojiSpriteUnicodeMapping, spriteCharacter.name, out uint unicode)) continue;

                spriteCharacter.unicode = unicode;
                spriteCharacter.glyph.glyphRect = CreateGlyphRect(spriteCharacter.glyph.glyphRect.x,
                    spriteCharacter.glyph.glyphRect.y, glyphSize);
                spriteCharacter.glyph.metrics = CreateGlyphMetrics(glyphSize);
            }

            tmpEmojiSpriteAsset.UpdateLookupTables();
            EditorUtility.SetDirty(tmpEmojiSpriteAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(tmpEmojiSpriteAsset));
        }
#endif

        /// <summary>
        /// Unicode value TextMeshPro gives to a sprite that has no unicode. TMP leaves these sprites out of the
        /// unicode lookup, but they are still available by name with &lt;sprite name="..."&gt;.
        /// </summary>
        private const uint NoUnicode = 0xFFFE;

        /// <summary>
        /// Variation selector-16 (emoji presentation). It only selects the emoji style of the code point before it.
        /// </summary>
        private const string EmojiVariationSelector = "FE0F";

        /// <summary>
        /// Converts an emoji "unified" value (for example "00A9-FE0F" or "1F468-200D-1F469") to the one unicode value
        /// a TMP sprite character can hold. The variation selector is removed first. When one code point remains,
        /// it is the unicode value. Sequences (keycaps, ZWJ sequences, flags) cannot be stored as one unicode value,
        /// so they get <see cref="NoUnicode"/>.
        /// </summary>
        private static uint GetSpriteUnicode(string unified)
        {
            string codePoint = null;
            foreach (string part in unified.Split('-'))
            {
                if (string.Equals(part, EmojiVariationSelector, System.StringComparison.OrdinalIgnoreCase)) continue;
                if (codePoint != null) return NoUnicode;

                codePoint = part;
            }

            if (codePoint == null) return NoUnicode;

            return uint.TryParse(codePoint, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint unicode)
                ? unicode
                : NoUnicode;
        }

        /// <summary>
        /// Finds the unicode value for a sprite name.
        /// Logs a warning and returns false when the sprite has no emoji data.
        /// </summary>
        private static bool TryGetUnicode(Dictionary<string, uint> emojiSpriteUnicodeMapping, string spriteName,
            out uint unicode)
        {
            if (emojiSpriteUnicodeMapping.TryGetValue(spriteName, out unicode)) return true;

            Debug.LogWarning($"Sprite {spriteName} Skipped: no matching emoji data");
            return false;
        }

        /// <summary>
        /// Glyph rect inside one emoji grid cell. Each cell has a 1 pixel padding around the emoji.
        /// </summary>
        private static GlyphRect CreateGlyphRect(int cellX, int cellY, int glyphSize)
        {
            return new GlyphRect(cellX + 1, cellY + 1, glyphSize, glyphSize);
        }

        private static GlyphMetrics CreateGlyphMetrics(int glyphSize)
        {
            return new GlyphMetrics(glyphSize, glyphSize, 0, glyphSize, glyphSize);
        }

        #region Utility Functions

        /// <summary>
        /// Sets the texture import to Sprite with Sprite Mode Multiple, and re-imports it when a change is needed.
        /// Unity only keeps the sprite rects of a texture in this mode. In other modes no sprites are created,
        /// and the TMP sprite asset gets no glyphs or characters.
        /// </summary>
        private static bool EnsureMultipleSpriteImport(Texture2D texture)
        {
            string texturePath = AssetDatabase.GetAssetPath(texture);
            TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null) return false;

            if (importer.textureType == TextureImporterType.Sprite &&
                importer.spriteImportMode == SpriteImportMode.Multiple) return true;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.SaveAndReimport();
            return true;
        }

        /// <summary>
        /// Checks that the emoji size matches the sprite sheet. Each grid cell is the emoji size plus
        /// 1 pixel of padding on each side, and the number of columns comes from the largest sheet_x.
        /// Logs the expected emoji size when it does not match.
        /// </summary>
        private static bool IsEmojiSizeValid(TMP_EmojiData[] emojiDataList, int textureWidth, int emojiSize)
        {
            int columnCount = 0;
            foreach (TMP_EmojiData emojiData in emojiDataList)
            {
                if (emojiData.sheet_x + 1 > columnCount) columnCount = emojiData.sheet_x + 1;
            }

            if (columnCount == 0)
            {
                Debug.LogError("Emoji JSON data file has no sprite sheet positions");
                return false;
            }

            if (emojiSize > 0 && textureWidth == columnCount * (emojiSize + 2)) return true;

            Debug.LogError($"Emoji Size {emojiSize} does not match the sprite sheet: {textureWidth} px wide with " +
                           $"{columnCount} columns. Expected Emoji Size: {textureWidth / columnCount - 2}");
            return false;
        }

        private static bool IsEmojiDataSupported(TMP_EmojiData tmpEmojiData, TMP_EmojiPlatformType tmpEmojiPlatformType)
        {
            if (string.IsNullOrEmpty(tmpEmojiData.image))
            {
                Debug.LogWarning($"Emoji {tmpEmojiData.name} Skipped: image file name is null or empty");
                return false;
            }

            if ((tmpEmojiPlatformType == TMP_EmojiPlatformType.Apple && !tmpEmojiData.has_img_apple) ||
                (tmpEmojiPlatformType == TMP_EmojiPlatformType.Google && !tmpEmojiData.has_img_google) ||
                (tmpEmojiPlatformType == TMP_EmojiPlatformType.Twitter && !tmpEmojiData.has_img_twitter) ||
                (tmpEmojiPlatformType == TMP_EmojiPlatformType.Facebook && !tmpEmojiData.has_img_facebook))
            {
                Debug.LogWarning(
                    $"Emoji {tmpEmojiData.name} Skipped: emoji not supported on platform {tmpEmojiPlatformType}");
                return false;
            }

            return true;
        }

        private static bool GetImageSize(Texture2D asset, out int width, out int height)
        {
            if (asset != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(asset);
                TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

                if (importer != null)
                {
                    object[] args = new object[2] { 0, 0 };
                    MethodInfo mi = typeof(TextureImporter).GetMethod("GetWidthAndHeight",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (mi != null) mi.Invoke(importer, args);

                    width = (int)args[0];
                    height = (int)args[1];

                    return true;
                }
            }

            height = width = 0;
            return false;
        }

        #endregion
    }
}