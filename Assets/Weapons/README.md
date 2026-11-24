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
| `Karambit` | Melee knife | ~0.1 m blade reach | Procedural curved blade + ring handle, secondary on every class |

## Karambit (melee)

`Karambit.prefab` has no imported model: `KarambitModel` builds the blade, handle, bolster
and pommel ring meshes in code (`KarambitMeshBuilder`, `Assets/Scripts/Weapons`) in `Awake`.
Materials: `Mat_KarambitBlade` (steel), `Mat_KarambitHandle` (G10), `Mat_KarambitAccent`
(anodised orange ring and bolster).

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
