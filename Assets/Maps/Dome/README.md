# Dome map

Futuristic take on a small arena built around a large geodesic dome (original geometry, generated
materials/textures only): a hex-panel energy-glass dome with emissive trim, ground-level interior with a
raised core, a ring catwalk reached by four ramps, four dome doorways leading to outer corridors, two
elevated towers and a cargo gantry for long sightlines, vehicle/crate/container cover in the corner yards,
floating holo displays, glowing floor circuitry, teal/violet lighting, nebula skybox and exponential fog.

## Build the scene

The scene file is generated, not hand-authored:

- Unity menu: `Tools > Maps > Build Dome` (then `Tools > Maps > Open Dome` / `Enter Dome Play Mode`)
- CLI: `Unity -batchmode -projectPath . -executeMethod FpsMaps.Dome.EditorTools.DomeMapBuilder.BuildBatch -quit`

The builder writes `Assets/Scenes/Dome.unity`, generates textures, materials and meshes under
`Assets/Maps/Dome/Textures`, `Materials` and `Meshes`, spawns the `PlayerCharacter` prefab at `PlayerSpawn`
(via `TestRangePlayerSetup`) and adds the scene to Build Settings. Re-running it regenerates everything.

## Layout

- `Scripts/DomeLayout.cs`: dimensions shared by the builder and tests (origin at the dome centre, floor top at y = 0).
- `Scripts/DomeGeometry.cs`: procedural geodesic dome (icosphere dual hex/pentagon panels, glass, struts, collision shell, prism).
- `Scripts/DomeHoloFloat.cs`: bob/spin for holo displays (never adds colliders).
- `Shaders/`: `Dome/EnergyGlass` (fresnel + hex lattice + pulse), `Dome/HoloDisplay` (additive scanlines),
  `Dome/WorldTiled` (world-space triplanar tiling with emissive pulse).
- `Assets/Editor/DomeMapBuilder.cs`, `DomeMapAssets.cs`: scene and asset generation.
- `Tests/Editor`: geometry contract tests. `Tests/PlayMode`: loads the generated scene (ignored until it exists)
  and checks player, spawns, dome structure, lighting and wall layers.

Compass: azimuth 0 is +Z, 90 is +X. Spawns are tagged `Respawn`; `PlayerSpawn` is the south doorway.

## Movement notes

Every world collider is on the `Default` layer, which is included in every `MovementConfig.wallLayers`
(Everything), so wall run / wall bounce / slide / bhop see all geometry. Decor (glass, trim, holo
displays, floor circuitry) has no colliders so it can never block wall-run or ground raycasts. Ramps are
about 22 degrees, walls and drums are vertical (wall-run needs 70+ degrees), the dome shell is a double-sided
mesh collider that keeps players inside, and the perimeter walls keep the arena closed.
