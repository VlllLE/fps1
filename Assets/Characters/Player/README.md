# PlayerCharacter (procedural humanoid FPS body)

Drop-in full-body player under `Assets/Characters/Player/PlayerCharacter.prefab`.

The visible character is **original procedural geometry** (no Asset Store, no copied
assets). It is generated at runtime by `Assets/Scripts/Characters/`:

| Script | Role |
|--------|------|
| `HumanoidBody` | Builds a 19-bone body under `BodyRoot`, sized to the `CharacterController` (feet at the capsule bottom, 2 m = standard height). Optional hit volumes for targets. |
| `HumanoidParts` / `HumanoidMeshBuilder` / `HumanoidMaterials` | Stylised limb, torso, head, boot and visor meshes plus a 5-slot suit palette (Standard shader). |
| `HumanoidAnimator` / `HumanoidIK` | Code-driven idle breathing, walk/strafe gait, crouch, slide, airborne, dive and weapon-ready arm poses using two-bone IK. No Animator clips needed. |
| `ProceduralFirstPersonArms` | First-person arms on the camera. Hands IK to grip points on `WeaponHolder`, so weapon attachment points are untouched. |
| `HumanoidBodyBootstrap` | At scene load, upgrades legacy primitive players and targets (old scenes/prefabs) to the procedural body. Idempotent. |

Bodies are rebuilt every time the scene loads and are not saved into scenes or prefabs
(`BodyRoot` uses `HideFlags.DontSaveInEditor`), so there is no baked mesh to go stale.
Select a `HumanoidBody` and use the context menu **Build** to preview it in the editor.

Movement, input and drift code only supplies velocity and state to the animator; it is not
modified.

**Status:** written without a Unity Editor run. Open the project once and, if anything
looks off, regenerate via **Tools → Build Player Character Prefab**.

## Hierarchy

```
PlayerCharacter                    CharacterController + movement/input/weapon bridge + HumanoidBody
├── CameraHolder (y=1.6)           FirstPersonCamera
│   └── MainCamera                 culls PlayerLocalHide, ProceduralFirstPersonArms
│       ├── WeaponHolder / TestPistol
│       └── FirstPersonArms        generated FP arms (hands follow the weapon holder)
└── BodyRoot                       generated third-person / shadow body
    └── Hips
        ├── Spine > Chest > Neck > Head (+ ShoulderL/R > UpperArm > Forearm > Hand)
        └── ThighL/R > ShinL/R > FootL/R
```

Neck, Head and the shoulder chain (which carries the body arms) are hidden from the local
camera by `LocalPlayerBodyVisibility` (`PlayerLocalHide` layer); `HumanoidBody` registers
them through `SetHideTargets`. Legs stay visible when looking down.

Targets built by `HumanoidBody.AddTo(target, HumanoidBodySettings.Target(color, scale))`
keep direct-child `Head`, `Body`, `Arm_L/R`, `Leg_L/R` hit volumes (`HitZone`) that match
the mesh proportions.

The `Animations/PlayerAnimator.controller` is still an empty placeholder; animation is procedural.

## CharacterController (do not change when swapping)

Match the existing TestRange player so `MovementController` crouch/slide heights stay valid:

| Property | Value |
|----------|-------|
| Height   | 2     |
| Radius   | 0.4   |
| Center   | (0, 1, 0) |
| Slope Limit | 45 |
| Step Offset | 0.3 |
| Skin Width | 0.08 |

Eye height stays at **CameraHolder local Y = 1.6**.

## Swap TestRange capsule → this prefab

### Option A — Replace the whole player (recommended)

1. Open `Assets/Scenes/TestRange.unity`.
2. Note the existing `Player` transform position / rotation (default ~ `(0, 0.55, -5)`).
3. Delete the scene `Player` instance (the capsule body child `PlayerBody` goes with it).
4. Drag `Assets/Characters/Player/PlayerCharacter.prefab` into the scene.
5. Rename the instance to **`Player`** (PlayMode smoke tests and builders look up that name).
6. Confirm on the root:
   - `CharacterController` values match the table above (unchanged).
   - `MovementController.config` → `Assets/Configs/DefaultMovementConfig.asset`
   - `MovementController.cameraTransform` → child `MainCamera`
   - `InputHandler` present; `FirstPersonCamera.playerBody` → root transform
   - `PlayerWeaponBridge.weapon` → child `TestPistol`
7. Enter Play Mode: move, look down (legs visible), look around (no head in view).

Do **not** retune movement physics or class systems as part of this swap.

### Option B — Visual-only transplant (keep existing Player root)

1. Select scene `Player`.
2. Delete child `PlayerBody` (the capsule mesh).
3. Add `HumanoidBody` to the scene `Player` root and `ProceduralFirstPersonArms` to
   `MainCamera` (both build themselves on Awake).
4. Add `LocalPlayerBodyVisibility` to the root and assign `MainCamera`; `HumanoidBody`
   registers the hidden bones itself.
5. Leave `CharacterController`, `MovementController`, and configs untouched.

### Layer setup

Project Settings → Tags and Layers should include user layer **`PlayerLocalHide`**
(index 8). This PR adds it to `ProjectSettings/TagManager.asset`. The builder menu
also ensures the layer exists when regenerating the prefab.

For a remote / third-person pawn later, call
`LocalPlayerBodyVisibility.ConfigureAsRemotePlayer()` so the head is visible.

## Regenerate in Editor

`Tools → Build Player Character Prefab` rebuilds materials (if missing), the hide
layer, and overwrites `PlayerCharacter.prefab`. Safe to re-run; does not edit
`MovementConfig` assets.

## What this does not change

- `Assets/Prefabs/Player.prefab` and TestRange scene are left as-is until you swap.
- Movement physics defaults and class/loadout systems are untouched.
- No animation clips — the controller is still empty; `HumanoidAnimator` poses bones from code.
