# Unloyal Front

A personal Roblox launcher (bootstrapper) in the style of Bloxstrap / Fishstrap.

- **Mods**: import a `.zip` of textures / sounds / cursors / sky and it is auto-applied on every launch. Toggle or remove packs and the originals come back.
- **Custom font**: pick a `.ttf` / `.otf` and Roblox's text uses it.
- **FastFlags**: advanced JSON editor for client flags (Roblox only honors an allowlist).
- Downloads and updates its own copy of Roblox, and handles the website's Play button.

## Get the .exe (no coding tools needed)

GitHub builds it for you for free:

1. Make a free account at github.com and click **New repository** (name it anything, private is fine).
2. On the new repo page click **uploading an existing file**.
3. Open the folder you extracted this project into, press **Ctrl+A**, and drag everything into the GitHub page
   (you should see `UnloyalFront`, `.github`, `README.md`, `.gitignore`). Click **Commit changes**.
4. Open the **Actions** tab. A run called *Build UnloyalFront* starts by itself (about 3 minutes).
   Wait for the green check, click the run, and download **UnloyalFront** under *Artifacts*.
5. Unzip the download and double-click `UnloyalFront.exe`.

Windows may say "unknown publisher" because the app is not code-signed.
Click **More info → Run anyway**. It is your own build.

If the build shows a red X, open the run, copy the red error text, and send it back to me to fix.

## Using it

1. First run asks to **set up** (copies itself to `%LocalAppData%\UnloyalFront` and registers the Play button). Say Yes.
2. **Mods → Import .zip**, pick your pack (your `unsetmains.zip` works as-is).
3. **Fonts → Choose font file...**
4. Press **Launch Roblox**, or just click Play on roblox.com.

Changes apply on the next launch. The first launch downloads Roblox (a few minutes).

## Mod pack format

A pack is a folder that mirrors Roblox's folders:

```
MyPack/
  content/textures/ArrowCursor.png
  content/sounds/action_jump.mp3
  PlatformContent/pc/textures/sky/sky512_up.tex
```

Only `content`, `PlatformContent`, `ExtraContent` and `shaders` are applied. `.exe`, `.dll`, scripts and other
executable file types are always ignored, so a pack can only change looks and sounds.

## Files and layout

```
UnloyalFront/
  App.xaml(.cs)            startup: settings window, or launch when opened from a roblox:// link
  Bootstrapper.cs          version check, download, install, launch
  Installer.cs             copy-to-AppData + link handler registration
  Paths.cs                 every folder the app uses
  Integrations/            ModManager, FontManager, FastFlagManager
  Models/                  settings + list items
  UI/                      MainWindow, LaunchWindow
  Utility/Log.cs           log file
```

Data lives in `%LocalAppData%\UnloyalFront` (Mods, Versions, logs, settings.json).
To remove everything: Settings → **Remove link handler**, then delete that folder.

## Notes

- Keep to cosmetic changes. Don't try to modify Roblox's program files; that can get an account banned.
- Never share your Roblox cookie with any tool, including this one (it never asks for it).
- If Roblox changes its download layout this app may need a small update.
