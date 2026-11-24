# Player classes / loadouts

Class loadouts pick primary + secondary weapons from `Assets/Weapons/Prefabs`
and apply them through `WeaponSwitcher` via `ClassLoadoutApplier`.

## Included classes

| Asset | Class | Primary | Secondary | Move speed × |
| --- | --- | --- | --- | --- |
| `Definitions/Class_Assault` | Assault | M4A1 | Karambit | 1.00 |
| `Definitions/Class_SMG` | SMG | UMP | Karambit | 1.08 |
| `Definitions/Class_Sniper` | Sniper | BoltActionSniper | Karambit | 0.94 |
| `Definitions/Class_Hybrid` | Hybrid | DMRSniper | Karambit | 1.00 |

Every class carries the **Karambit** knife (`Assets/Weapons/Prefabs/Karambit.prefab`,
`Assets/Configs/Weapons/KarambitConfig.asset`) in the secondary slot, so the secondary key
(`2`) draws the knife. Left click slashes; the HUD shows no ammo for it.
The separate melee slot on `ClassDefinition` is still reserved and unused.

## Add a new class

1. **Assets → Create → Classes → Class Definition** (or duplicate an asset under `Definitions/`).
2. Set **Class Name** / **Description**.
3. Assign **Primary** / **Secondary** weapon prefabs from `Assets/Weapons/Prefabs/`
   (and optional matching `WeaponConfig` under `Assets/Configs/Weapons/`).
   If the prefab field is empty, the config’s `weaponPrefab` is used as a fallback.
4. Optionally set **Move Speed Multiplier** (1 = unchanged movement feel).
5. Save under `Assets/Classes/Definitions/`.

You can also run **Tools → Classes → Create Default Class Definitions** to refresh the four built-ins.

## Pick a class in TestRange

Avoid hand-editing `TestRange.unity`. Use either:

### A. Component on the player (recommended)

1. Open TestRange.
2. **Tools → Classes → Add ClassLoadoutApplier To Player (Scene)**  
   (or add `ClassLoadoutApplier` on the Player and wire `WeaponSwitcher`).
3. Assign a `ClassDefinition` on the applier, **or** use  
   **Tools → Classes → Apply To Scene Player → Assault / SMG / Sniper / Hybrid**.
4. Enter Play Mode — the applier clears the weapon holder and spawns that class’s guns.

Same menu exists for the Player prefab: **Add ClassLoadoutApplier To Player Prefab**.

### B. Runtime / code

```csharp
var applier = player.GetComponent<ClassLoadoutApplier>();
applier.ApplyLoadout(myClassDefinition);   // also works mid-session
```

## Scripts

| Script | Role |
| --- | --- |
| `Assets/Scripts/Classes/ClassDefinition.cs` | ScriptableObject loadout data |
| `Assets/Scripts/Classes/ClassLoadoutApplier.cs` | Spawns weapons into `WeaponSwitcher` on Awake / on demand |
| `Assets/Editor/ClassLoadoutMenu.cs` | Editor menus above |

`ClassLoadoutApplier` runs before `WeaponSwitcher.Start` (`DefaultExecutionOrder(-50)`),
so the starting primary is equipped on first frame. It optionally sets
`MovementController.ClassSpeedMultiplier` from the class asset.
