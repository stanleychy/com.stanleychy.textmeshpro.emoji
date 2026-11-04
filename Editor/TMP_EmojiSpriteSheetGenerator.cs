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

            SpriteDataProviderFactories factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider dataProvider = factory.GetSpriteEditorDataProviderFromObject(emojiTexture);
            dataProvider.InitSpriteEditorDataProvider();

            string emojiDataText = $"{{\"items\": {emojiDataJsonFile.text}}}";
            TMP_EmojiDataWrapper tmpEmojiDataListWrapper = JsonUtility.FromJson<TMP_EmojiDataWrapper>(emojiDataText);
            TMP_EmojiData[] emojiDataList = tmpEmojiDataListWrapper.items;

            if (emojiDataList == null)
            {
                Debug.LogError("Failed to parse Emoji JSON data file");
                return;
            }

            List<SpriteRect> newEmojiSprites = new List<SpriteRect>();
            List<SpriteNameFileIdPair> spriteNameFileIdPairs = new List<SpriteNameFileIdPair>();
            Dictionary<string, string> emojiSpriteUnicodeMapping = new Dictionary<string, string>();

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

                emojiSpriteUnicodeMapping[newSpriteName] = string.Concat(emojiData.unified.Split('-'));
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

        private static void GenerateTMPEmojiSpriteAsset(Dictionary<string, string> emojiSpriteUnicodeMapping,
            Texture2D emojiTexture, int emojiSize)
        {
            string emojiTexturePath = AssetDatabase.GetAssetPath(emojiTexture);
            string spriteAssetPath = Path.ChangeExtension(emojiTexturePath, "asset");
            int glyphSize = emojiSize / 2;

            // Build the TMP_SpriteAsset ourselves instead of calling TMP_SpriteAssetMenu.CreateSpriteAsset(),
            // which is no longer public in the TextMeshPro version bundled with com.unity.ugui (Unity 6+).
            TMP_SpriteAsset tmpEmojiSpriteAsset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(spriteAssetPath);
            bool isNewAsset = tmpEmojiSpriteAsset == null;
            if (isNewAsset)
            {
                tmpEmojiSpriteAsset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
                tmpEmojiSpriteAsset.name = Path.GetFileNameWithoutExtension(spriteAssetPath);
            }

            // Mark the asset as the current format so TMP does not run its legacy upgrade path
            // (which would clear the sprite tables) when the asset is loaded, and set its name hash.
            // Done through SerializedObject because these members are not publicly settable in every TMP version.
            SerializedObject serializedSpriteAsset = new SerializedObject(tmpEmojiSpriteAsset);
            SetStringProperty(serializedSpriteAsset, "1.1.0", "m_Version");
            SetIntProperty(serializedSpriteAsset, TMP_TextUtilities.GetSimpleHashCode(tmpEmojiSpriteAsset.name),
                "m_HashCode", "hashCode");
            serializedSpriteAsset.ApplyModifiedPropertiesWithoutUndo();

            tmpEmojiSpriteAsset.spriteSheet = emojiTexture;
            tmpEmojiSpriteAsset.spriteGlyphTable.Clear();
            tmpEmojiSpriteAsset.spriteCharacterTable.Clear();

            // Same ordering as TextMeshPro's own sprite asset creation: top-to-bottom, left-to-right.
            List<Sprite> sprites = new List<Sprite>();
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(emojiTexturePath))
            {
                if (asset is Sprite sprite) sprites.Add(sprite);
            }

            sprites.Sort((a, b) =>
            {
                int compareY = b.rect.y.CompareTo(a.rect.y);
                return compareY != 0 ? compareY : a.rect.x.CompareTo(b.rect.x);
            });

            uint glyphIndex = 0;
            foreach (Sprite sprite in sprites)
            {
                if (!emojiSpriteUnicodeMapping.TryGetValue(sprite.name, out string codePoint))
                {
                    Debug.LogWarning($"Sprite {sprite.name} Skipped: no matching emoji data");
                    continue;
                }

                uint.TryParse(codePoint, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var unicode);

                TMP_SpriteGlyph spriteGlyph = new TMP_SpriteGlyph
                {
                    index = glyphIndex,
                    metrics = new GlyphMetrics(glyphSize, glyphSize, 0, glyphSize, glyphSize),
                    glyphRect = new GlyphRect((int)sprite.rect.x + 1, (int)sprite.rect.y + 1, glyphSize, glyphSize),
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

        private static SerializedProperty FindFirstProperty(SerializedObject serializedObject,
            params string[] propertyNames)
        {
            foreach (string propertyName in propertyNames)
            {
                SerializedProperty property = serializedObject.FindProperty(propertyName);
                if (property != null) return property;
            }

            Debug.LogWarning($"TMP_SpriteAsset has no serialized property named {string.Join(" / ", propertyNames)}");
            return null;
        }

        private static void SetStringProperty(SerializedObject serializedObject, string value,
            params string[] propertyNames)
        {
            SerializedProperty property = FindFirstProperty(serializedObject, propertyNames);
            if (property != null) property.stringValue = value;
        }

        private static void SetIntProperty(SerializedObject serializedObject, int value, params string[] propertyNames)
        {
            SerializedProperty property = FindFirstProperty(serializedObject, propertyNames);
            if (property != null) property.intValue = value;
        }

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

                SerializedObject serializedSpriteAsset = new SerializedObject(spriteAsset);
                SerializedProperty materialProperty =
                    FindFirstProperty(serializedSpriteAsset, "m_Material", "material");
                if (materialProperty != null) materialProperty.objectReferenceValue = material;
                SetIntProperty(serializedSpriteAsset, TMP_TextUtilities.GetSimpleHashCode(material.name),
                    "m_MaterialHashCode", "materialHashCode");
                serializedSpriteAsset.ApplyModifiedPropertiesWithoutUndo();
            }

            material.SetTexture("_MainTex", spriteSheet);
            EditorUtility.SetDirty(material);
        }

        #region Utility Functions

        private static bool IsEmojiDataSupported(TMP_EmojiData tmpEmojiData, TMP_EmojiPlatformType tmpEmojiPlatformType)
        {
            if (string.IsNullOrEmpty(tmpEmojiData.image))
            {
                Debug.LogWarning($"Emoji {tmpEmojiData.name} Skipped: image file name is null or empty");
                return false;
            }

            if (tmpEmojiData.unified.Length > 9)
            {
                Debug.LogWarning(
                    $"Emoji {tmpEmojiData.name} Skipped: unicode {tmpEmojiData.unified} is too long for TextMeshPro");
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