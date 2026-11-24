# Shipment map

Tiny neon container-yard arena inspired by the classic "Shipment" layout (original geometry, generated
materials/textures only): tight lanes between stacked cargo containers, an open central lane, jumpable
container tops (cargo steps `0.9 -> 1.8 -> 2.6 m`), and a dock/crane edge on the east side opening onto
a night-city skyline and water.

## Build the scene

The scene file is generated, not hand-authored:

- Unity menu: `Tools > Maps > Build Shipment` (then `Tools > Maps > Open Shipment` / `Enter Shipment Play Mode`)
- CLI: `Unity -batchmode -projectPath . -executeMethod ShipmentMapBuilder.BuildBatch -quit`

The builder writes `Assets/Scenes/Shipment.unity`, generates textures/materials under
`Assets/Maps/Shipment/Textures` and `Materials`, spawns the `PlayerCharacter` prefab at `PlayerSpawn`
(via `TestRangePlayerSetup`), and adds the scene to Build Settings.

## Layout

- `Scripts/ShipmentLayout.cs`: pure-data layout (blocks, lights, spawns, jump routes) shared by the builder and tests.
- `Scripts/ShipmentSpawnPoint.cs`, `ShipmentHoloPulse.cs`, `ShipmentMapInfo.cs`: runtime helpers.
- `Assets/Editor/ShipmentMapBuilder.cs` and `ShipmentMapVisuals.cs`: scene and asset generation.
- `Tests/EditMode`: layout contract tests (no overlaps, traversable lanes, clear spawns, reachable jump routes).
- `Tests/PlayMode`: loads the generated scene; checks player, spawns and wall layers.

## Movement notes

All world colliders are `BoxCollider`s on the `Default` layer, which is included in every
`MovementConfig.wallLayers` (Everything), so wall run / wall bounce / slide / bhop see them. Walls are
perfectly vertical; stacked container sides give wall-bounce surfaces. An invisible bounds cap, east
blocker and ceiling keep players inside the arena.
