# PlayerCharacter (primitive FPS humanoid)

Drop-in full-body player under `Assets/Characters/Player/PlayerCharacter.prefab`.

Built from Unity primitives only (no Asset Store). Intended for third-person /
shadows / looking-down body visibility, with separate first-person arms under the camera.

**Status:** Prefab YAML and `.meta` GUIDs were authored without a Unity Editor run.
Open the project once and, if anything looks off, regenerate via
**Tools → Build Player Character Prefab**.

## Hierarchy

```
PlayerCharacter                    CharacterController + movement/input/weapon bridge
├── CameraHolder (y=1.6)           FirstPersonCamera
│   └── MainCamera                 culls PlayerLocalHide
│       ├── WeaponHolder / TestPistol
│       └── FirstPersonArms        FP view arms (pitch with camera)
└── BodyRoot                       third-person / shadow body
    └── Hips
        ├── Torso, Head*, Neck*
        ├── Left/Right shoulders + arms
        └── Left/Right hips + legs + boots
```

`*` Head and Neck use layer `PlayerLocalHide` so the local camera does not draw them
(avoids head clipping). Legs/arms stay visible when looking down.
`LocalPlayerBodyVisibility` enforces this at runtime.

Animator uses empty `Animations/PlayerAnimator.controller` (placeholder only).

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
3. Open `PlayerCharacter` prefab, copy `BodyRoot` (and optionally `FirstPersonArms`),
   paste as children of the scene `Player` / `MainCamera` respectively.
4. Add `LocalPlayerBodyVisibility` to the root; assign `MainCamera` and Head/Neck
   into `hideFromLocalCamera`.
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
- No idle/run animation clips — controller is empty until animation work lands.
