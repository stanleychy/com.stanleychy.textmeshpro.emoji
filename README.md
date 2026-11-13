# TextMeshPro Emoji

This package is intended to support emoji with TextMesh Pro sprite feature. By providing an editor tool to easily
generate TextMesh Pro Sprite Asset from an emoji sprite sheet developers will be able to use unicode in text and let
TextMesh Pro handle the sprite mapping and display by unicode.

# Requirements

- Unity 2022.3 or newer
- TextMesh Pro
    - Unity 6 / 2023.2+: TextMesh Pro is part of the built-in `com.unity.ugui` package, nothing extra to install
    - Unity 2022.3: install `com.unity.textmeshpro` (3.0.x) from the Package Manager if your project doesn't already
      have it

In both cases, import <b>Window > TextMeshPro > Import TMP Essential Resources</b> so the `TextMeshPro/Sprite` shader
is available.

# Installation

### Via manifest.json

Add this line to the dependencies:

```json lines
{
  "dependencies": {
    "com.stanleychy.textmeshpro.emoji": "https://github.com/stanleychy/com.stanleychy.textmeshpro.emoji.git"
  }
}
```

### Via Unity Package Manager

1. Open Unity Package Manager from Unity Editor, <b>Window > Package Manager</b>
2. Click the <b>+</b> button, select <b>Add package from git URL...</b>
3. Input `https://github.com/stanleychy/com.stanleychy.textmeshpro.emoji.git` and click <b>Add</b>

# Usage

1. Download one emoji sprite sheet image and `emoji.json` from `https://github.com/iamcal/emoji-data`.
2. Put the sprite sheet image and `emoji.json` in the same folder in your Unity project.
   Do not change the name of the sprite sheet image (for example, `sheet_twitter_64.png`).
3. In the Project window, right-click the sprite sheet image.
4. Select <b>Create > TextMeshPro > Emoji Sprite Asset</b>.
   The tool makes the TextMesh Pro sprite asset in the same folder as the sprite sheet image.
   You can select more than one sprite sheet image. The tool makes one sprite asset for each image.
5. Open <b>Project Settings > TextMesh Pro > Settings > Default Sprite Asset</b> and select the new sprite asset.

The tool finds these values automatically:

- Emoji data file: the `emoji.json` (or `emoji_pretty.json`) in the same folder as the sprite sheet image.
  If that folder has no data file, the tool uses the only `emoji.json` in the project.
- Platform: from the name of the sprite sheet image (`apple`, `google`, `twitter` or `facebook`).
- Emoji size: from the width of the sprite sheet image and the number of columns in `emoji.json`.
- Import settings: the tool sets the sprite sheet image to Texture Type <b>Sprite</b> and Sprite Mode <b>Multiple</b>.

To make the sprite asset again, do steps 3 and 4 again. The tool replaces the sprites and characters in the
sprite asset. In Unity 6 / 2023.2+, the tool keeps the existing asset, so references to the asset stay correct.

If the emoji look blurred (usually with small sprite sheets, for example 16 or 20 pixels), select the sprite sheet
image and set <b>Compression</b> to <b>None</b> in the Inspector. This uses more texture memory.

# Limitations

- A TextMesh Pro sprite character can have only one unicode value.
  The tool removes the emoji variation selector (`FE0F`) from each emoji.
  Emoji sequences (keycaps, flags and ZWJ sequences such as family emoji) have more than one code point.
  These emoji have no unicode value in the sprite asset. To show them, use the sprite name,
  for example `<sprite name="0023-fe0f-20e3">`.
