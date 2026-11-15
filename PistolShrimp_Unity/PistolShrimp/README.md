# Pistol Shrimp VR Player

Low-poly pistol shrimp you wear in first person. One claw is the big snapper that fires a water jet, the other is a small pincer. Built for Quest with XR Interaction Toolkit, but the scripts only need the Input System package.

## Install (about two minutes)

1. Copy the `PistolShrimp` folder into your project's `Assets` folder.
2. In Unity: **Tools > Pistol Shrimp > Build Prefabs**. This fixes import settings, creates the materials and builds the prefabs into `PistolShrimp/Prefabs`.
3. Drag `PistolShrimpPlayer_RightHanded` (or `_LeftHanded`) into a scene that has an XR Origin. Press Play.

Nothing needs wiring. The script finds the headset through `Camera.main` and reads the controllers straight from the Input System.

No headset on? Press Play anyway. The claws stay in their rest pose, **Space** works the snapper and **Left Shift** works the pincer.

## Controls

| Input | What happens |
|---|---|
| Snapper hand trigger, squeeze | Claw cocks open. The controller buzzes harder as tension builds, then clicks when it locks. |
| Snapper hand trigger, release | Claw snaps shut and fires. |
| Pincer hand trigger or grip | Pincer closes. Picks up a rigidbody if one is at the tip. |

Prefer a normal gun feel? Set **Snap Mode** to `SnapOnPull`: the claw rests open and fires when you pull.

## Who owns what

| Piece | Owns |
|---|---|
| Shrimp prefab | Claw rig, snap, the `Muzzle` point that defines where a shot starts and where it points |
| `WaterBullet` prefab | Its own flight, bubble trail and the cavitation pop. Spawned in world space, never parented to the claw |
| Your world | The water (fog, particles, caustics), colliders, and what reacts to a hit |

The only thing the world has to know about the shrimp is one interface:

```csharp
using PistolShrimp;
using UnityEngine;

public class Prey : MonoBehaviour, ISnapHittable
{
    public void OnSnapHit(SnapHit hit)
    {
        // hit.direct   : the jet touched this object
        // hit.strength : 1 for a direct hit, less if only the shockwave reached it
        // hit.point, hit.normal, hit.direction, hit.source
    }
}
```

`SnapTarget.cs` is a working example. Rigidbodies near the pop get pushed without any script. Use `WaterBullet.hitMask` to choose which layers the jet can hit.

## Things you will probably tune

All on the `PistolShrimpPlayer` component.

| Field | Why |
|---|---|
| Snap / Pinch > Position Offset, Rotation Offset | How the claw sits on the hand. Default pulls the claw back so the hand is inside the palm. Adjust in the headset until aiming feels right. |
| Claw Pose | `Aim` points the claw where the controller points. `Grip` follows the palm. |
| Eye Offset | Moves the body relative to the headset. Lower Y to see more of the rostrum and antennae. |
| Body Turn Smooth Time | How fast the body catches up with a head turn. 0 locks it to the head. |
| Colorway | 0 candy stripe, 1 tiger orange, 2 glass with red band. `SetColorway(int)` and `NextColorway()` switch at runtime. |
| On `WaterBullet`: Launch Speed, Drag | Range is roughly (speed - 3) / drag. Default is about 7 m. |

## Scale

The model is authored at player scale: about 1.3 m long, so the claws are hand-sized. To feel like a 4 cm shrimp, build the world big rather than shrinking the player. If you do scale the XR Origin instead, leave **Match Rig Scale** on and the shrimp and its bullets scale with it.

The feet are 0.5 m below the eyes. A standing player's head is about 1.6 m up, so the shrimp hovers. For walking on the seabed, lower the XR Origin by about 1.1 m.

## What is inside

- `Models/PistolShrimp_RightSnapper.fbx`, `_LeftSnapper.fbx`: 1,122 triangles, 50 bones, rigid skinning, one looping `Idle` clip (swimmerets, legs, antennae).
- `Models/ShrimpVFXMeshes.fbx`: faceted bubble, water jet and shock ring.
- `Textures/ShrimpPalette_*.png`: 32 x 256 palette per colorway. Repaint a row to recolor a body part.
- `Audio/snap.wav`, `cavitation_pop.wav`: synthesized placeholders.
- `Source~/`: the Blender Python that generates the model, if you want to change the shape. Unity ignores this folder.

Bones you may want: `Snap_Claw`, `Snap_Dactyl`, `Pinch_Claw`, `Pinch_Dactyl`, `Antenna_L_1..3`, `Antenna_R_1..3`, `Abd1..5`, `Tail`, `Pleo_*`, `Leg_*`.

## Checked and not checked

Checked: the FBX files re-import cleanly with all bones, skin weights, UVs and the idle clip. The arm IK was tested numerically on 2,000 hand positions. All C# passes a syntax check.

Not checked: the scripts have not been compiled or run inside Unity, because there was no Unity available where this was built. If the Console shows an error after import, it will most likely be a small API difference in your Unity version.
