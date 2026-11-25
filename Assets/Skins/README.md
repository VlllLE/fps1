# Weapon Skins

Mesh-agnostic weapon skins for the Built-in Render Pipeline (Unity 6).
Materials project tileable textures in **object-space triplanar**, so any
gun mesh works without custom UVs.

## Layout

| Folder | Contents |
| --- | --- |
| `Scripts/` | `WeaponSkinDefinition` (ScriptableObject) and `WeaponSkinApplier` |
| `Shaders/` | Built-in surface shaders (`Skins/*`) |
| `Textures/` | Generated tileable PNGs (+ data maps for animated skins) |
| `Materials/` | Ready-to-use materials |
| `Definitions/` | Skin assets (static + `Animated/`) |
| `Tools~/` | Python texture generator (ignored by Unity) |

## Equip a skin at runtime

1. Add `WeaponSkinApplier` to the weapon root (same object as `WeaponController`, or a parent).
2. Assign a default `WeaponSkinDefinition`, or call from code:

```csharp
var applier = weapon.GetComponent<WeaponSkinApplier>();
applier.ApplySkin(skinDefinition);   // apply
applier.ClearSkin();                 // restore original materials
```

The applier replaces materials on Mesh/SkinnedMesh renderers under the
target root and skips particle/trail/line renderers.

The default skin is applied in `Awake` and re-applied in `OnEnable`, so weapons that
are holstered/equipped by `WeaponSwitcher` or spawned by `ClassLoadoutApplier` always
show their skin. If a skin shader is unsupported on the current GPU/pipeline the applier
falls back to a Standard-shader material with the skin texture and tint
(`ResolveRenderableMaterial`).

Skin shaders tile in **world-scale** object space (`SKIN_TILES_PER_METER`), so the same
`_Scale` looks the same on a pistol and a sniper regardless of mesh import scale, and
metallic is capped at 0.5 to avoid near-black results in Built-in RP without reflection probes.

`TestRangeSkinCycler` no longer overrides per-weapon skins on start: each weapon keeps its
prefab default until `[` or `]` is pressed. It cycles through `Resources/WeaponSkinCatalog`
(all skins; no per-scene list). After adding a skin run **Tools > Skins > Rebuild Weapon Skin
Catalog** (an EditMode test fails if the catalog is stale).

Every weapon prefab carries its own `WeaponSkinApplier` (M4A1, SCAR, UMP, sniper, DMR, Karambit and
the `TestPistol` inside `Player.prefab`). `WeaponLoadoutService.ApplySkinToWeapon` reuses it and only
adds one if a prefab has none, and skips the call when that skin is already current.

`Scripts/Skins.asmdef` makes the skin code testable from `Assets/Tests`.

## Add a static skin

1. Create a seamless tileable albedo PNG (512² or 1024², wrap mode Repeat).
   Prefer object-space-friendly patterns: camo, noise, geometric lattices.
   You can extend `Tools~/generate_skin_textures.py` and re-run it.
2. Drop the PNG in `Textures/` and let Unity import it (sRGB, mipmaps on).
3. Duplicate `Materials/Mat_Skin_WoodlandCamo.mat` (or create a material
   with shader `Skins/TriplanarStatic`), assign your texture to `_MainTex`,
   tune `_Scale`, `_Metallic`, `_Glossiness`.
4. **Assets → Create → Skins → Weapon Skin**. Point `material` at the new
   material. Set name / rarity. Leave `applyParameterOverrides` off unless
   you want runtime tint/scale overrides.
5. Save under `Definitions/`.

## Add an animated skin

1. Pick a shader under `Skins/`:
   - `ScrollingEnergy` — circuit traces scroll; uses `SkinCircuitMask` + `SkinNoise`
   - `PulsingEmissive` — cell glow pulse; uses `SkinNoise`
   - `Holographic` — fresnel + iridescent shift; uses `SkinNoise`
   - `FlowingLava` — dual-layer flowing heat; uses `SkinNoise`
   - `NeonRain` — falling neon streaks; uses `SkinNoise`
   - `ChromaticShift` — metallic colour bands; uses `SkinNoise`
   - `StormArc` — electric arcs on traces; uses `SkinCircuitMask` + `SkinNoise`
   - `EmberAsh` — rising embers over ash; uses `SkinNoise`
2. Create a material with that shader, assign the data textures, set
   `_Speed`, `_Intensity`, colours.
3. Create a `WeaponSkinDefinition` under `Definitions/Animated/`.
   Enable `applyParameterOverrides` if loadouts should drive speed/colour/intensity
   through the ScriptableObject fields (`speed`, `skinColor`, `accentColor`, `intensity`, `scale`).

**Palette shaders** (`StormArc`, `ChromaticShift`, `Holographic`, `EmberAsh`, `FlowingLava`) use several
colour slots (`_EnergyColor`, `_AccentColor`, `_MidColor`, `_EmissionColor`). `WritePropertyBlock` writes the
single `accentColor` into all of them, which flattens the palette to one colour, so these skins keep
`applyParameterOverrides` off and are tuned on their materials instead. Their `Definitions` fields
just mirror the material values.

Shared property names (`_SkinColor`, `_EnergyColor` / `_EmissionColor` /
`_AccentColor`, `_Speed`, `_Intensity`, `_Scale`) are written by
`WeaponSkinDefinition.WritePropertyBlock`.



## Batch 2 skins

**Static:** Desert Tiger, Urban Hex, Carbon Fiber, Copper Patina, Kryptek Fracture, Night Viper.

**Animated:** Neon Rain, Chromatic Shift, Storm Arc, Ember Ash
(`Skins/NeonRain`, `Skins/ChromaticShift`, `Skins/StormArc`, `Skins/EmberAsh`).

Previews for batch-2 static textures live in the project store under `media/skins-batch2/`.

## Notes

- Pipeline: **Built-in** (`m_CustomRenderPipeline` is unset). These shaders
  will not work as-is under URP/HDRP.
- Shaders / materials were authored without the Unity editor and are
  **uncompiled and unverified** in-engine until opened locally.
- Preview images for the static textures live in the project store under
  `media/skins/`.
