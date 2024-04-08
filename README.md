# TextMeshPro Emoji

This package is intended to support emoji with TextMesh Pro sprite feature. By providing an editor tool to easily
generate TextMesh Pro Sprite Asset from an emoji sprite sheet developers will be able to use unicode in text and let
TextMesh Pro handle the sprite mapping and display by unicode.

# Installation

### Via manifest.json

Add this line to the dependecies:

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

1. Download one of the emoji sprite sheet image and `emoji.json` from `https://github.com/iamcal/emoji-data`
2. Copy the downloaded sprite sheet image and json file into your Unity project
3. Open the tool from the menu bar, <b>Emoji > TextMeshPro Emoji Generator</b>
4. Select the downloaded emoji sprite sheet image and json file in the editor window, select target platform (Twitter by default) of
   the downloaded emoji sprite sheet image
5. Click <b>Generate TextMeshPro Emoji Sprite Asset</b> button and the sprite asset supported by TextMeshPro will be
   generated next to the emoji sprite sheet image
6. Open <b>Project Settings > TextMesh Pro > Settings > Default Sprite Asset</b>, select the newly generated sprite
   asset

# Limitations

- Emoji with longer unicode (exceed UTF-32) are not supported, limited by TextMeshPro's unicode size