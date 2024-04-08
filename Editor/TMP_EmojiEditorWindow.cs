using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TMP_Emoji.Editor
{
    public class TMP_EmojiEditorWindow : EditorWindow
    {
        [MenuItem("Emoji/TextMeshPro Emoji Generator")]
        private static void ShowWindow()
        {
            TMP_EmojiEditorWindow emojiEditorWindow = GetWindow<TMP_EmojiEditorWindow>();
            emojiEditorWindow.titleContent = new GUIContent("Emoji Generator");
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;

            ObjectField emojiSpriteSheetField = new ObjectField("Sprite Sheet")
            {
                objectType = typeof(Texture2D)
            };
            root.Add(emojiSpriteSheetField);

            ObjectField emojiJsonFileField = new ObjectField("Json File")
            {
                objectType = typeof(TextAsset)
            };
            root.Add(emojiJsonFileField);

            IntegerField emojiSizeField = new IntegerField("Emoji Size");
            root.Add(emojiSizeField);

            EnumField emojiPlatformField = new EnumField("Target Platform", TMP_EmojiPlatformType.Twitter);
            root.Add(emojiPlatformField);

            Button generateButton = new Button
            {
                text = "Generate TextMeshPro Emoji Sprite Asset"
            };
            generateButton.clicked += () => TMP_EmojiSpriteSheetGenerator.GenerateEmojiSpriteData((Texture2D)emojiSpriteSheetField.value,
                (TextAsset)emojiJsonFileField.value, (TMP_EmojiPlatformType)emojiPlatformField.value, emojiSizeField.value);
            root.Add(generateButton);
        }
    }
}