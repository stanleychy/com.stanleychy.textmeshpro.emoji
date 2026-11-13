using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TMP_Emoji.Editor
{
    /// <summary>
    /// Finds the generation inputs for an emoji sprite sheet from https://github.com/iamcal/emoji-data,
    /// so that the user only has to select the sprite sheet texture.
    /// </summary>
    public static class TMP_EmojiSourceResolver
    {
        /// <summary>
        /// File names of the emoji data file in emoji-data. Both files have the same content.
        /// </summary>
        private static readonly string[] EmojiDataFileNames = { "emoji.json", "emoji_pretty.json" };

        /// <summary>
        /// Finds the emoji data file for a sprite sheet.
        /// A file in the same folder as the sprite sheet is used first. This lets a project keep sprite sheets
        /// from different emoji-data versions, each next to its own data file.
        /// If the folder has no data file, the only data file in the project is used.
        /// </summary>
        public static bool TryFindEmojiDataFile(Texture2D emojiTexture, out TextAsset emojiDataFile)
        {
            emojiDataFile = null;
            string textureFolder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(emojiTexture));

            string projectDataFilePath = null;
            int projectDataFileCount = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:TextAsset emoji"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsEmojiDataFileName(Path.GetFileName(path))) continue;

                if (Path.GetDirectoryName(path) == textureFolder)
                {
                    emojiDataFile = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                    return emojiDataFile != null;
                }

                projectDataFilePath = path;
                projectDataFileCount++;
            }

            if (projectDataFileCount == 1)
            {
                emojiDataFile = AssetDatabase.LoadAssetAtPath<TextAsset>(projectDataFilePath);
                return emojiDataFile != null;
            }

            Debug.LogError(projectDataFileCount == 0
                ? "No emoji.json found. Download emoji.json from https://github.com/iamcal/emoji-data " +
                  "and put it in the same folder as the sprite sheet."
                : $"Found {projectDataFileCount} emoji.json files, but none in {textureFolder}. " +
                  "Put the emoji.json that matches the sprite sheet in the same folder as the sprite sheet.");
            return false;
        }

        /// <summary>
        /// Finds the platform from the sprite sheet file name, for example "sheet_twitter_64.png".
        /// </summary>
        public static bool TryGetPlatformType(Texture2D emojiTexture, out TMP_EmojiPlatformType platformType)
        {
            string fileName = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(emojiTexture));
            foreach (TMP_EmojiPlatformType type in Enum.GetValues(typeof(TMP_EmojiPlatformType)))
            {
                if (fileName.IndexOf(type.ToString(), StringComparison.OrdinalIgnoreCase) < 0) continue;

                platformType = type;
                return true;
            }

            platformType = default;
            Debug.LogError($"Cannot find the platform in the sprite sheet name \"{fileName}\". " +
                           $"The name must contain one of: {string.Join(", ", Enum.GetNames(typeof(TMP_EmojiPlatformType)))}. " +
                           "Keep the original file name from emoji-data, for example sheet_twitter_64.png.");
            return false;
        }

        /// <summary>
        /// Calculates the emoji size from the sprite sheet width. Each grid cell is the emoji size plus
        /// 1 pixel of padding on each side, and the number of columns comes from the largest sheet_x.
        /// </summary>
        public static bool TryGetEmojiSize(TMP_EmojiData[] emojiDataList, int textureWidth, out int emojiSize)
        {
            emojiSize = 0;

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

            if (textureWidth % columnCount != 0 || textureWidth / columnCount <= 2)
            {
                Debug.LogError($"The sprite sheet ({textureWidth} px wide) does not match the emoji data " +
                               $"({columnCount} columns). Use a sprite sheet and emoji.json from the same " +
                               "emoji-data version.");
                return false;
            }

            emojiSize = textureWidth / columnCount - 2;
            return true;
        }

        private static bool IsEmojiDataFileName(string fileName)
        {
            foreach (string emojiDataFileName in EmojiDataFileNames)
            {
                if (string.Equals(fileName, emojiDataFileName, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }
    }
}
