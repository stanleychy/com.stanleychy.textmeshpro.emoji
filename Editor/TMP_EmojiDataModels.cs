using System;

namespace TMP_Emoji.Editor
{
    [Serializable]
    public class TMP_EmojiData
    {
        public string name;
        public string unified;
        public string image;
        public int sheet_x;
        public int sheet_y;
        public bool has_img_apple;
        public bool has_img_google;
        public bool has_img_twitter;
        public bool has_img_facebook;
    }

    [Serializable]
    public class TMP_EmojiDataWrapper
    {
        public TMP_EmojiData[] items;
    }
    
    [Serializable]
    public enum TMP_EmojiPlatformType
    {
        Apple,
        Google,
        Twitter,
        Facebook
    }
}