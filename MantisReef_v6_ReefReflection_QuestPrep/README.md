# Mantis Shrimp VR — Reef v6 upgrade and Quest 3S preparation

This is an **add-on to the clean all-in-one Unity project**, not a replacement for it. The ZIP contains ONE new editor script in `Assets/MantisShrimpVR/Editor/`. It intentionally does not duplicate existing runtime classes.

## Install / build

1. Back up your working Unity project.
2. Extract this ZIP OUTSIDE the project. Copy the ZIP's `Assets/MantisShrimpVR/Editor/UpgradeReefV6.cs` into the same location inside your existing Unity project. Do not create folders called `Assets 4` or `Assets 5`.
3. Ensure the original clean all-in-one package is installed, and the underwater asset FBX files are already imported.
4. Open `Assets/MantisShrimpVR/Scenes/MantisReef_Clean.unity` (not an empty scene). Exit Play mode.
5. Use **Mantis Shrimp > Upgrade to v6 - Reef, Flat Reflection, VR Preparation**.
6. Open `Assets/MantisShrimpVR/Scenes/MantisReef_V6.unity` and press Play.

## Changes

- Adds up to 145 more coral, plants and shell assets, 32 more reef rocks, and 44 additional fish (actual counts depend on imported FBX file names). The cave emergence corridor remains clear.
- Removes the pop-out 3D reflection and puts a **flat, stylized 2D shrimp portrait** onto the shell. This is a placeholder educational image, **not a real mirror**. It is visible even when the player is far away; you can change its size/position in the scene. The source texture is `Assets/MantisShrimpVR/Generated/V6_FlatShrimpPortrait.png`.
- Replaces the fishing net visual with a vertical net mouth and a **handle above the net**. Its existing chase script remains attached to the net parent.
- Keeps the working desktop WASD, mouse, buoyant hops, narration, and color toggles unchanged.
- Saves a NEW scene without overwriting the clean version.

## Known limitations

- The net uses simple procedural rope geometry, not cloth simulation. Its mouth is oriented along local +Z; you may need to rotate the net parent in the Inspector for a particular approach direction.
- The 2D reflection is an illustrative portrait, not a live dynamic reflection; a Quest-friendly live mirror would need a stereo-aware design and profiling.
- Fish use existing simple circular motion; schools do not yet avoid rocks or react to the player.
- The Quest VR rig is **not automatically configured** by this package. Desktop controller and XR Origin should not be active together in the same playable scene.
- No Unity editor or Quest device was available for compilation or headset testing; check Console and verify in your Unity version.

## Quest 3S development: staged migration

### Stage A — Get a headset-independent XR test scene

1. In Unity Hub, install **Android Build Support**, **Android SDK & NDK Tools**, and **OpenJDK** for your Unity 6 LTS editor.
2. In Package Manager, install **XR Plug-in Management**, **OpenXR Plugin**, **XR Interaction Toolkit**, and **Input System**. If Unity offers XR Interaction Toolkit starter assets, import them for the appropriate action maps.
3. In **File > Build Profiles**, create/switch to Android.
4. In **Project Settings > XR Plug-in Management**, enable **OpenXR for Android** and configure Meta Quest support, controller interaction profiles, and project validation warnings.
5. Duplicate `MantisReef_V6.unity` as `MantisReef_Quest.unity`.
6. In the Quest scene, **disable the desktop player root** and create an **XR Origin (VR)** with tracked camera and controllers. Position its origin inside the cave, preserving the player's natural head height and floor calibration. The existing low camera viewpoint is for desktop only.
7. Use an XR locomotion provider or a custom gesture-swim provider; do not attach the desktop keyboard controller to the XR Origin. Keep locomotion player-controlled; never force forward movement.
8. Parent a separate first-person claw rig to the XR camera, and hide it from the reflection representation as appropriate. Bind the strike action to controller motion using tracked velocity with a cooldown; haptics should be driven through XR controller output, not keyboard input.
9. For the sensory modes, test fog and object material cues in stereo on device. The current color changes are approximations, not literal UV or polarization vision.
10. When you have the Quest, enable developer mode, connect by USB-C, authorize debugging, and Build & Run. Profile headset frame timing and keep 72 FPS as an initial target.

### Stage B — Before museum visitors

- Add a stationary/comfort locomotion alternative to physical swimming, snap-turn option, seated/standing calibration, recenter, and a reset-to-start flow.
- Move HUD narration from screen-space overlay to a comfortable **world-space or XR-compatible canvas**, avoiding persistent text locked too close to the face.
- Replace flat shrimp portrait with a more polished UV-mapped image or a performant reflection illusion.
- Test jumping and repeated vertical boosts for comfort in VR; cap velocity and height and offer reduced-motion settings.

## Troubleshooting

If the menu is absent, open Window > General > Console and resolve the **first** compiler error. Do not copy the whole previous ZIP into the project again. If you get a duplicate-class error, search the Project panel for duplicate `MantisDesktopPlayer`, `MantisFishSwim`, or `UpgradeReefV6` scripts.

If fish/decor are missing, verify that the imported FBX filenames match the names in the original packs (e.g. `Fish1.fbx`, `coral_branch.fbx`) and that the files are somewhere inside Unity's `Assets` folder.
