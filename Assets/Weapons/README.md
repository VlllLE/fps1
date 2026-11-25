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

## Detailed Blender models: Pump Shotgun, LMG, sidearm Pistol

The primitive-built shotgun, LMG and pistol (`TestPistol` in `Player.prefab` / `PlayerCharacter.prefab`) are
replaced at runtime by hard-surface models generated headless with Blender 4.2 (`bpy`/`bmesh`, no downloaded
assets). Each is 15 to 40 thousand triangles, UV unwrapped (smart project) and uses three material slots
(`<Gun>_Metal`, `<Gun>_Polymer`, `<Gun>_Accent`, remapped in the `.fbx.meta` to `Mat_GunMetal`, `Mat_GunPolymer`
and the new `Mat_GunAccent`). `WeaponSkinApplier` still replaces every slot with the triplanar skin material
(object space, so the UVs do not matter for skins).

| Gun | Generator | FBX / GLB | Mesh objects (animatable transforms) | Tris |
| --- | --- | --- | --- | --- |
| Shotgun | `Tools/Blender/build_shotgun.py` | `Models/Shotgun/Shotgun.fbx` | `ShotgunBody`, `Pump` | ~21.9k |
| LMG | `Tools/Blender/build_lmg.py` | `Models/LMG/LMG.fbx` | `LMGBody`, `Magazine` (ammo box + belt), `Bolt`, `Bipod` | ~20.5k |
| Pistol | `Tools/Blender/build_pistol.py` | `Models/Pistol/Pistol.fbx` | `PistolBody`, `Slide`, `Magazine` | ~19.4k |

```
blender -b --factory-startup --python Tools/Blender/build_lmg.py -- \
    --out Assets/Weapons/Models/LMG [--renders <dir>] [--samples 48] [--res 100]
python3 Tools/Blender/wire_weapon_models.py     # .meta files, Mat_GunAccent, prefab wiring (idempotent)
```

`Tools/Blender/weapon_lib.py` holds the shared geometry (lofts, extrusions, exact booleans, stipple, UVs),
FBX/GLB export, studio renders and the held-pose mock. Coordinates are authored in Unity weapon space, so
the FBX has identity rotation and scale 1 and the muzzle / eject empties of the prefabs line up unchanged
(muzzle `(0, 0.045, 0.57)` shotgun, `(0, 0.04, 0.74)` LMG, `(0, 0, 0.30)` pistol).

- Shotgun: receiver with ejection port and action-bar slots, ventilated rib, bead sight, twin-bar
  ribbed pump forend (own `Pump` object), magazine tube with cap, side saddle of six shells, stippled
  stock with butt pad. `WeaponPartAnimator` cycles the pump after every shot and at the end of a reload
  (`WeaponController` still plays its per-shell reload and bolt-rack click).
- LMG: receiver with ejection port, feed slot and cooling slots, feed cover with Picatinny rail and flip-up
  sight, finned heavy barrel with gas system and slotted flash hider, vented handguard, carry handle, folded
  bipod, skeletal stock, stippled raked grip, ammo box with a visible cartridge belt running to the feed
  slot. Box and belt are one `Magazine` object, so the existing reload animation drops them together;
  `Bolt` is the charging handle `WeaponController` racks at the end of the reload.
- Pistol: slide with front/rear serrations, top cocking grooves, ejection port with a chambered round,
  lightening slots and dovetailed three-dot sights; polymer frame with accessory rail, trigger guard, slide
  stop, takedown lever, magazine catch, stippled grip with finger grooves and a flared magwell; ported
  compensator on the fixed barrel. The `Magazine` object gives the pistol a reload drop it did not have.
  `WeaponPartAnimator` kicks the `Slide` back on every shot and holds it open on an empty magazine.

Runtime: `WeaponModelLoader` (execution order -300, so before `WeaponController` and `WeaponSkinApplier`)
instantiates the FBX under a `DetailedModel` child, applies the materials by slot name, and then hides the
primitive children listed in `fallbackChildren` (renamed `<name>_Fallback` so name lookups such as
`Magazine` / `Bolt` hit the model). The loader is try/catch guarded: a missing or empty FBX, a missing
required part or any exception leaves the primitives visible and logs a warning. The primitives stay in the
prefabs on purpose. `Tools/WeaponGen/build_shotgun_lmg.py` regenerates `Shotgun.prefab` and `LMG.prefab`
without the loader, so run `wire_weapon_models.py` again after using it. `PlayerCharacterBuilder` /
`TestRangeBuilder` likewise rebuild `TestPistol` from primitives; re-run the wiring script after them.

Tests: `Assets/Tests/Editor/WeaponModelTests.cs` (parts, triangle budget, UVs, material slots, muzzle
end, loader wiring and fallback behaviour, animators). Renders and the held first-person mocks were rendered
with Blender Cycles (`--renders`), not with Unity.

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

### Detailed Blender models (Shotgun, LMG, Pistol)

Generated and rendered headlessly in Blender 4.2.3 only; the C# was compile-checked against hand-written
UnityEngine/UnityEditor stubs. Unverified in-engine: FBX import and the `.fbx.meta` material remap, the
`WeaponModelLoader` instantiate/fallback path, `WeaponPartAnimator` feel (pump, slide, bolt), the reload
animation on the new `Magazine`/belt, the held pose in the real hand IK (the Blender held mock is an
approximation), skins on the new UVs, the new EditMode tests (not run), and triangle counts after Unity
import. Blender renders are not Unity renders. `PlayerCharacterBuilder`, `TestRangeBuilder` and
`Tools/WeaponGen/build_shotgun_lmg.py` regenerate the primitive versions and drop the loader wiring;
re-run `Tools/Blender/wire_weapon_models.py` afterwards.
