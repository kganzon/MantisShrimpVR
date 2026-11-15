# Mantis Shrimp VR - Clean All-in-One Starter (Desktop-first)

This is a **fresh Unity project content package**, not a complete Unity project or verified Quest build. It replaces the sequence of v1-v5 patches. Do not import older MantisReef scripts alongside it.

## Start clean (important)
1. In Unity Hub create a **NEW** Unity 6 Universal 3D (URP) project called `MantisShrimpVR_Clean`.
2. In **Edit > Project Settings > Player > Other Settings**, set **Active Input Handling** to **Both** (or Input Manager (Old) for desktop-only testing). Restart Unity if prompted.
3. Extract this ZIP **outside** your Unity project. Copy its single `Assets/MantisShrimpVR` folder into your NEW project's `Assets` directory. You should have `Assets/MantisShrimpVR/Runtime/MantisGame.cs` and `Assets/MantisShrimpVR/Editor/BuildMantisReef.cs`.
4. Optional but recommended: copy the FBX files from your already downloaded asset packs into `Assets/ThirdParty/Underwater`, `Assets/ThirdParty/Aquarium`, and `Assets/ThirdParty/Quaternius`. The builder searches FBX names across the whole Assets tree, so exact subfolder names are flexible. Import FBX **only**, not the old Unity scripts. This package does not redistribute third-party assets.
5. Wait for Unity to compile with zero red Console errors. Choose **Mantis Shrimp > Build COMPLETE Clean Reef (New Scene)**.
6. Open `Assets/MantisShrimpVR/Scenes/MantisReef_Clean.unity` and press Play.

## Controls
- WASD: move; mouse: look; Escape: release cursor; click in Game view to recapture cursor.
- Space: hop; press again while airborne for up to 3 boosts. Descend slowly. Tweak `MantisDesktopPlayer` in Inspector.
- Left mouse button: strike nearby prey; V: cycle visual filters; 1/2/3: select filters.

## Scene features
- Irregular low-poly rock cave, separate side-wall and ceiling colliders, clear center opening, no forced movement.
- Contextual text prompts inside cave, on exit, and near reflection.
- Naturalistic fog/lighting with colorful low-poly corals; optional existing FBX assets with primitive fallback.
- First-person stylized mantis shrimp claws; staged full-body shrimp reveal near pearl panel (not a physically accurate mirror).
- Buoyant stackable hops, desktop prey scoring, simple fish movement, simple net pursuit.
- Sensory modes are artistic color simulations, not faithful UV or polarization rendering.

## Known limitations
- Not tested inside Unity in this environment; check the Console and Inspector after generation.
- This is desktop-first, not a configured Quest XR project. VR hand tracking, real haptics, comfort settings, punch animations, real reflection shader, polished UI, fish animation controllers and net capture gameplay remain future work.
- Imported FBX assets may have material, scale or orientation differences. The builder normalizes approximate size and disables decorative imported colliders. Use the Inspector to tune placement.
- UI uses built-in Unity UI and legacy Input API to minimize dependencies. Use **Both** input handling when adding XR later.
- Scene can be rebuilt; it overwrites `MantisReef_Clean.unity`, so save custom edits to another scene first.
- All third-party assets remain in your own project; follow their original licenses.

## Troubleshooting
- **No menu**: open Console, resolve red C# errors; ensure `BuildMantisReef.cs` is inside `Editor`.
- **Duplicate class errors**: you imported an older package into this new project. Remove old scripts; only `Assets/MantisShrimpVR` contains the new C# scripts.
- **No imported coral/fish**: check the FBX files are somewhere under `Assets` and have finished importing. Builder uses geometric fallbacks when missing.
- **Stuck in cave**: select `CAVE - clear central passage` and inspect side-wall colliders. The center path is intended to be clear; the decorative rocks have no colliders.
- **Mouse stuck**: press Escape; click Game view to recapture.
- **Missing fonts**: if your Unity version does not provide `LegacyRuntime.ttf`, change the font name in `BuildMantisReef.cs` to a built-in font available in your editor.
