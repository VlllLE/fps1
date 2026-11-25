# Weapon models

Primitive-composed gun prefabs for first-person use. Built without DCC tools
from Unity built-in Cube / Cylinder meshes so they work out of the box.

## Prefabs (`Prefabs/`)

| Prefab | Class | Approx. length (local Z) | Notes |
| --- | --- | --- | --- |
| `M4A1` | Assault rifle | ~0.54 m muzzle | Imported Blender model (procedural fallback); see "Assault rifle and SMG models" |
| `SCAR` | Assault rifle (SCAR-H) | ~0.60 m muzzle | Imported Blender model (procedural fallback) |
| `UMP` | SMG (UMP45) | ~0.41 m muzzle | Imported Blender model (procedural fallback) |
| `BoltActionSniper` | Bolt sniper | ~0.66 m muzzle | Scope + bipod + bolt handle |
| `DMRSniper` | Semi DMR | ~0.58 m muzzle | Rail optic, shorter than bolt |
| `Shotgun` | Pump shotgun | ~0.57 m muzzle | Ribbed pump forend, shell tube, side-saddle shell carrier |
| `LMG` | Light machine gun | ~0.74 m muzzle | Belt-fed: ammo box + visible belt links, carry handle, folded bipod, heavy barrel |
| `Karambit` | Melee knife | ~0.1 m blade reach | Imported Blender model (procedural fallback), secondary on every class |

## Shotgun and LMG

Both are generated (not hand-edited) by `Tools/WeaponGen/build_shotgun_lmg.py`, which writes
`Shotgun.prefab`, `LMG.prefab` and `Assets/Configs/Weapons/ShotgunConfig.asset` / `LMGConfig.asset`
with fixed GUIDs. Edit the script (part layout, stats) and re-run it from the repo root to change them.

- **Pump Shotgun** (`WeaponType.Shotgun`): 9 pellets x 12 damage, semi, 75 rpm, 6-shell tube, range 30 m with a
  steep damage falloff curve. It uses the existing `pelletCount` path in `WeaponController`
  (`FireShotgunPellets`, one `HitDetection.Resolve` ray per pellet, one shell spent per trigger pull).
  Per-shell reload is a small additive feature: `WeaponConfig.perShellReload` /
  `shellReloadTime` / `shellReloadFinishTime`. `reloadAmmoTime` is the lead-in before the first shell. While
  reloading, pulling the trigger cancels the reload and fires as long as a shell is chambered
  (`canCancelReload`). With an empty tube the reload cannot be interrupted. Existing weapons have
  `perShellReload = false` and behave exactly as before.
- **LMG** (`WeaponType.LMG`): 100-round belt, 200 reserve, auto 700 rpm, 24 damage, slow reload (5.4 s / 4.7 s
  tactical), slower ADS (0.34 s) and a slower ADS walk speed, higher vertical recoil than the M4A1.
  The `Magazine` child (ammo box + belt links) is what `WeaponController` drops and re-inserts during the
  reload animation, so the belt moves with the box.
- Both have a `WeaponSkinApplier` (default skins Damascus / Night Viper) and every skin in
  `Assets/Skins/Definitions` applies to every renderer.
- Registered in `Assets/Resources/WeaponLoadoutCatalog.asset` (primary and secondary capable). Re-run
  **Tools > Loadout > Rebuild Weapon Catalog** after changing them; it also picks them up automatically.
- `TestRangeWeaponLoadout` lists them after the DMR, so **Tools > Sync Test Range Weapon Loadout** puts
  them in the switcher on slots 6 and 7 (config stats are left as authored, not overwritten).
  In play they are chosen through the loadout menu (`L`).
- Note: `WeaponController` applies the weapon spread once to the shot direction and again per pellet, so the
  effective shotgun cone is roughly twice `baseSpread`; the config is tuned with that in mind.

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

## Assault rifle and SMG models (M4A1, SCAR-H, UMP45)

Detailed models (about 17k / 19k / 15k triangles) generated headless in Blender by
`Tools/Blender/build_m4a1.py`, `build_scar.py` and `build_ump.py` (shared kit: `assault_kit.py`):

```
blender -b --factory-startup --python Tools/Blender/build_m4a1.py -- \
    --out Assets/Weapons/Models/M4A1 [--renders <dir>] [--samples 48]
blender -b --factory-startup --python Tools/Blender/preview_assault_held.py -- --gun m4a1 --renders <dir>
```

Each FBX (a `.glb` copy sits next to it) has three objects: `Body`, `Magazine` (pivot at the top of the
magazine, which is what the reload animation drops and rotates) and `Bolt` (charging handle). Three
material slots (`<Gun>Metal`, `<Gun>Polymer`, `<Gun>Accent`) are remapped in the `.fbx.meta` to
`Mat_GunMetal`, `Mat_GunPolymer` and `Mat_AssaultAccent`. Skins are object-space triplanar, so the UVs
only matter for the default materials; `WeaponSkinApplier` replaces every slot.

`AssaultWeaponModel` (on the prefab root, execution order -200) instantiates the FBX under the `Model`
child, which is the first child so `WeaponController` finds the imported `Magazine` and `Bolt` first.
The old primitive parts live under `Procedural`; on success they are hidden and their `Magazine` / `Bolt`
are renamed `*_Procedural`. If the reference is missing, has no mesh, or throws, `Procedural` stays
visible and the muzzle / fire / eject points return to their old positions. The prefabs were rewired by
`Tools/Weapons/wire_assault_models.py` (re-runnable; skips prefabs that already have the component).
`Assets/Tests/Editor/AssaultWeaponModelTests.cs` covers import, wiring, fallback and skin coverage.

## Sniper scope and bipod mounting

`BoltActionSniper` and `DMRSniper` group the optic under a `Scope` pivot (child of the root,
x = 0, local +Z = bore direction). `ScopeBody`, `ScopeLens*` and the two `ScopeRing*` mounting
rings are children of that pivot, and the rings rest on the `Rail` (bolt sniper gained its own
rail; the DMR rail was lowered onto the receiver). The bolt sniper also has a `Forend` under the
barrel and a `Bipod` pivot (mount block + two splayed legs) hanging from it. Muzzle / fire / eject
points are unchanged. `WeaponController.adsPosition` / `hipPosition` are unset on every gun prefab
and ADS only lerps FOV, so nothing reads the scope transforms; if a sight-aligned ADS pose is added
later, aim it along `Scope` local +Z. `Assets/Tests/Editor/SniperMountingTests.cs` asserts the
parts touch. These two prefabs were edited by `Tools/Weapons/mount_sniper_parts.py` (the older
flat generator in `Assets/Skins/Tools~/write_batch2_assets.py` no longer matches them).

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

The Shotgun / LMG prefabs, configs and tests were additionally only compile-checked (C# against
UnityEngine 2021.3 reference assemblies) and their layout previewed as a 2D side silhouette. Nothing
about them has been imported or run in the Unity 6 editor: import success, in-engine look and feel,
reload animation on the `Magazine`/belt, shotgun spread and recoil tuning, skin shaders on the new
meshes, and all EditMode/PlayMode tests are unverified.

The M4A1 / SCAR / UMP Blender models were checked only in Blender (manifold meshes, FBX re-import,
renders, a mock of the held pose against the arm IK) and the C# was compile-checked against UnityEngine
2021.3 reference assemblies plus stubs. Unverified in the Unity 6 editor: FBX import and the
`importedModelPrefab` reference (the FBX root GameObject fileID `919132149155446097` is copied from the
Karambit prefab), material remap, skin rendering on the new meshes, the real held pose and reload
animation against the hand IK, and the new EditMode tests.
