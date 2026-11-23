# FPS HUD (first pass)

Screen-space HUD for the Built-in RP / legacy Input Manager shooter. Everything lives under `Assets/UI/` and compiles without hard references to `Assets/Scripts/` (weapon / movement types are bound through reflection or optional `Component` fields).

## Drop into TestRange

1. Open the TestRange scene (built on your machine by the scene owner).
2. Drag `Assets/UI/Prefabs/FpsHudCanvas.prefab` into the scene hierarchy (or use **GameObject → UI → FPS HUD Canvas**).
3. Enter Play. On Awake the canvas builds crosshair / ammo / hit marker / damage-number roots if children are missing.
4. Optional: enable **Enable Damage Numbers** on `FpsHudController` for floating damage text.

No scene file changes are required from this PR — only drop the prefab into TestRange locally.

## What you get

| Element | Behaviour |
| --- | --- |
| Crosshair | Four-arm reticle; expands on fire and when the player moves quickly |
| Ammo | `magazine \| reserve` (bottom-right), color shifts when low / empty |
| Hit marker | Brief center X flash on successful damage (red on headshot) |
| Damage numbers | Optional; off by default |

## Automatic wiring (`WeaponHudBridge`)

The prefab includes `WeaponHudBridge`. With **Auto Find On Enable** left on, it looks for:

- `WeaponController` (or `WeaponSwitcher.CurrentWeapon`) → `OnAmmoChanged`, `OnWeaponFired`, `OnHitResolved`, `OnShotgunHitsResolved`
- `HitDetection.ImpactResolved` **only if** weapon hit events were not found
- `MovementController.CurrentSpeed` or `CharacterController.velocity` for crosshair expand

Optional Inspector fields (`Weapon Source`, `Hit Detection Source`, `Movement Source`) accept any `Component` on the player / weapon stack — assign them on the **player prefab** if auto-find is unreliable.

## Manual wiring still needed on the player side

Do these on the player prefab / TestRange setup (outside `Assets/UI/`):

1. Ensure a live `WeaponController` (or `WeaponSwitcher` with a current weapon) exists at runtime.
2. Ensure a scene `HitDetection` exists if you rely on the fallback impact event.
3. If auto-find fails, assign the soft `Component` refs on `WeaponHudBridge`, or call the public API yourself:

```csharp
fpsHud.SetAmmo(mag, reserve);
fpsHud.NotifyFired();
fpsHud.ShowHitMarker(damage, headshot);
// or
weaponHudBridge.HandleAmmoChanged(mag, reserve);
weaponHudBridge.HandleWeaponFired();
weaponHudBridge.HandleHitConfirmed(damage, headshot);
```

4. Optional UnityEvents on `WeaponHudBridge` (`OnWeaponFired`, `OnAmmoChanged`, `OnHitConfirmed`, `OnHitConfirmedDetailed`) can be hooked from other behaviours without code.

## Compatibility notes

- Built-in Render Pipeline, uGUI (`UnityEngine.UI`), legacy Input Manager.
- UI assembly definition: `UI` (references `UnityEngine.UI` only — not `Scripts`).
- Requires the **Unity UI (uGUI)** package. If `UnityEngine.UI` is missing, add `com.unity.ugui` via Package Manager.
- Rebuild / refresh the shipped prefab in-editor with **Assets → Create → UI → FPS HUD Canvas Prefab** if child wiring looks empty after import.

## Public hit-marker API

`FpsHudController.ShowHitMarker()` and `ShowHitMarker(float damage, bool headshot)` are the supported fallbacks when weapon events are not present.
