# Weapon models

Primitive-composed gun prefabs for first-person use. Built without DCC tools
from Unity built-in Cube / Cylinder meshes so they work out of the box.

## Prefabs (`Prefabs/`)

| Prefab | Class | Approx. length (local Z) | Notes |
| --- | --- | --- | --- |
| `M4A1` | Assault rifle | ~0.54 m muzzle | Carry handle + handguard silhouette |
| `SCAR` | Assault rifle | ~0.48 m muzzle | Bulkier receiver, stock pad |
| `UMP` | SMG | ~0.30 m muzzle | Compact; angled mag |
| `BoltActionSniper` | Bolt sniper | ~0.66 m muzzle | Scope + bipod + bolt handle |
| `DMRSniper` | Semi DMR | ~0.58 m muzzle | Rail optic, shorter than bolt |
| `Karambit` | Melee knife | ~0.1 m blade reach | Imported Blender model (procedural fallback), secondary on every class |

## Karambit (melee)

The visible model is the imported `Assets/Weapons/Models/Karambit/Karambit.fbx` (about 19 cm,
hawkbill blade with belly and false edge, G10 scales, bolster/guard and finger ring; a `.glb`
copy sits next to it). It is generated headless by `Tools/Blender/build_karambit.py`:

```
blender -b --factory-startup --python Tools/Blender/build_karambit.py -- \
    --out Assets/Weapons/Models/Karambit [--renders <dir>]
```

`KarambitModel` instantiates the FBX under the prefab's `Model/` child (the pivot is the handle
centre, the blade points along +Z) and assigns `Mat_KarambitBlade`, `Mat_KarambitHandle` and
`Mat_KarambitAccent` by material-name match. If the FBX reference is missing or has no mesh,
the procedural `KarambitMeshBuilder` meshes are built instead (`UsesImportedModel` is false).

Held pose: the `Model` transform is `(-0.0436, -0.0474, -0.0205)` in holder space, which is the
right palm of `ProceduralFirstPersonArms` (wrist grip `(0, -0.09, -0.05)`), rotated so the
handle lies across the fist and the blade points away from the player toward screen centre.
`Tools/Blender/preview_karambit_held.py` renders a mock of that pose (`--pos`, `--quat`).

- Config: `Assets/Configs/Weapons/KarambitConfig.asset` (`weaponType = Melee`; `meleeRange`,
  `meleeRadius`, `meleeHitDelay`, `meleeSwingDuration`). Attack rate comes from `fireRate`.
- A press fires `WeaponController.OnMeleeSwing`, then after `meleeHitDelay` sends five short
  rays (centre + left/right/up/down fan) through `HitDetection` via `MeleeAttackResolver`;
  the first ray that applies damage wins, so one swing never hits a target twice.
- `MeleeSlashAnimator` plays an alternating forehand/backhand slash on the `Model` child.
- Melee weapons broadcast `-1` for magazine/reserve through `OnAmmoChanged`; the HUD ammo
  readout (`AmmoCounterView`) shows nothing for them. Reload and ADS are ignored.

## Scale & pivot

- Units are **meters**. Pivot is at the **receiver / grip** (local origin),
  matching the existing `TestPistol` viewmodel convention in `Player.prefab`.
- Intended as FPS viewmodels (same ballpark as the test pistol ~0.3 m).
  For world/third-person, parent under a socket and scale ~1.0, or duplicate
  and stretch to taste.
- Forward is **+Z**. Muzzle empties sit at the barrel tip.

## Hierarchy conventions (`WeaponController`)

Root has `WeaponController` + `WeaponSkinApplier`. Child empties:

- `MuzzlePoint` — muzzle flash / projectile spawn (wired to `muzzlePoint`)
- `FirePoint` — same tip; wired to `firePoint`
- `ShellEjectPoint` — right-side ejector; wired to `shellEjectPoint`

Mesh children use readable names (`GunBody`, `Barrel`, `Grip`, `Magazine`,
`Stock`, …). All mesh parts have `MeshRenderer`s so `WeaponSkinApplier`
can replace materials with triplanar skins.

Assign a `WeaponConfig` on the root `WeaponController` before play.
Default body materials live in `Materials/` (Built-in Standard).

## Not verified

Prefabs were authored as YAML outside the Unity editor. Open the project
locally to confirm mesh pivots, materials, and component wiring.
