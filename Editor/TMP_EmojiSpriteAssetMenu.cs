using UnityEditor;
using UnityEngine;

namespace TMP_Emoji.Editor
{
    /// <summary>
    /// Adds "Assets > Create > TextMeshPro > Emoji Sprite Asset" to the Project window context menu.
    /// Right-click an emoji sprite sheet from emoji-data to generate its TMP sprite asset next to it.
    /// </summary>
    public static class TMP_EmojiSpriteAssetMenu
    {
        private const string MenuPath = "Assets/Create/TextMeshPro/Emoji Sprite Asset";

        [MenuItem(MenuPath)]
        private static void CreateEmojiSpriteAsset()
        {
            foreach (Texture2D emojiTexture in Selection.GetFiltered<Texture2D>(SelectionMode.Assets))
            {
                TMP_EmojiSpriteSheetGenerator.GenerateEmojiSpriteData(emojiTexture);
            }
        }

        /// <summary>
        /// Enables the menu item only when at least one texture is selected.
        /// </summary>
        [MenuItem(MenuPath, true)]
        private static bool ValidateCreateEmojiSpriteAsset()
        {
            return Selection.GetFiltered<Texture2D>(SelectionMode.Assets).Length > 0;
        }
    }
}
