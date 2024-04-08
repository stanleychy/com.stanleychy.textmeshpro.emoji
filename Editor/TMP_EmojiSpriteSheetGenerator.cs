using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using TMPro;
using TMPro.EditorUtilities;
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
            int glyphSize = emojiSize / 2;

            Selection.activeObject = emojiTexture;
            TMP_SpriteAssetMenu.CreateSpriteAsset();

            TMP_SpriteAsset tmpEmojiSpriteAsset =
                AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(Path.ChangeExtension(emojiTexturePath, "asset"));

            foreach (var spriteCharacter in tmpEmojiSpriteAsset.spriteCharacterTable)
            {
                string codePoint = emojiSpriteUnicodeMapping[spriteCharacter.name];
                uint.TryParse(codePoint, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var unicode);
                spriteCharacter.unicode = unicode;
                spriteCharacter.glyph.glyphRect = new GlyphRect(spriteCharacter.glyph.glyphRect.x + 1,
                    spriteCharacter.glyph.glyphRect.y + 1, glyphSize, glyphSize);
                spriteCharacter.glyph.metrics = new GlyphMetrics(glyphSize, glyphSize, 0, glyphSize, glyphSize);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(tmpEmojiSpriteAsset));
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