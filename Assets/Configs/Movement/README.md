# Character Movement Configs + Juice

## Archetype configs

| Asset | Feel | Notes |
|-------|------|--------|
| `../DefaultMovementConfig.asset` | Assault baseline | Used by TestRange today |
| `MovementConfig_Scout.asset` | Fast / floaty | Higher walk & air control, wall-run on |
| `MovementConfig_Heavy.asset` | Slow / grounded | Lower speeds, stickier slides, weaker bounce |
| `MovementConfig_Acrobat.asset` | Mobility focused | Strong slides, bhop, wall-run, coyote |
| `MovementConfig_Marksman.asset` | Deliberate | Slower sprint/slide, less air aggression |

Swap at runtime:

```csharp
movement.SetConfig(scoutConfig);
```

Or in Editor: **Tools → Movement → Apply \<Archetype\> Config To Scene Player**.

These tune `MovementConfig` only. Class loadout `moveSpeedMultiplier` still stacks via `MovementController.ClassSpeedMultiplier`.

## Choosing an archetype in game

Open the loadout menu on the **MOVEMENT** tab (`M`, or the tab in the `L` / `Esc` menu). It lists Default, Scout, Heavy,
Acrobat and Marksman with bars for their key stats (walk, sprint, jump height, air control and acceleration, slide boost
and length, bunny-hop cap and speed kept, dive speed, wall bounce) and the wall run, wall bounce and mantle traits.

- Hover an archetype to compare it with the selected one (white tick plus a percentage difference). Click it, or press
  `1` to `5`, to switch. The selected one is marked `SELECTED`.
- The choice calls `MovementController.SetConfig` on the live player, so no restart or scene reload is needed.
- It is saved through `ILoadoutStore` in `PlayerPrefs` key `FPS.Movement.v1` (`{"archetypeId":"Scout"}`) and re-applied in
  `MovementSelectionService.Start` on every map.
- **Default behaviour:** an explicit choice, independent of class. With nothing saved the player keeps the config the scene
  or prefab uses (`DefaultMovementConfig`, shown as Default) and nothing is written. **Reset movement to default** clears
  the saved key. An unknown saved id is ignored.
- **Class multiplier:** unchanged. The class sets `ClassSpeedMultiplier` (Assault 1.00, SMG 1.08, Sniper 0.94, Hybrid 1.00)
  and it multiplies the archetype's walk, sprint and bunny-hop speeds. Changing archetype never touches it and changing
  class never resets the archetype. The tab shows the resulting walk speed and the per-class list.

How it is wired:

| Piece | Where |
|-------|-------|
| `MovementArchetypeCatalog`, `MovementArchetypeEntry`, `MovementStatSheet` | `Assets/Scripts/Movement/MovementArchetypeCatalog.cs` |
| Catalog asset (loaded from Resources) | `Assets/Resources/MovementArchetypeCatalog.asset` |
| `MovementSelection` (saved JSON), `MovementSelectionService`, `MovementSelectionPage` | `Assets/Loadout/Scripts/` |
| `LoadoutBootstrap` adds the service to the player and the page to the menu at runtime | no scene or prefab edits needed |

After adding or renaming an archetype config in this folder, run **Tools → Movement → Rebuild Movement Catalog**
(an EditMode test fails if a config here is missing from the catalog).

## Feedback (VFX / camera / SFX)

`MovementFeedback` listens to controller events (jump, land, slide, wall bounce, dive, bhop, slide cancel) and:

- Spawns runtime particle systems (slide dust, jump/land bursts, wall sparks)
- Applies camera shake + roll tilt on the **MainCamera** transform (does not touch `FirstPersonCamera` pitch)
- Plays procedural placeholder clips under `Assets/Audio/Movement/`

`MovementFeedback` is on `PlayerCharacter.prefab` (and re-applied by
**Tools → Sync Test Range Player** / **Tools → Movement → Add Feedback To Scene Player**).

Clips also load from `Resources/Audio/Movement/` if inspector refs are missing.

**Drift note:** WASD wish direction uses the player root yaw only (not pitched
`CameraHolder` / camera forward). Idle damping snaps grounded micro-slides to zero.

**Crouch/slide:** eye height (`CameraHolder`) lowers with stance — hold `C`/`Ctrl`
to crouch; `Shift+W` then tap crouch to slide. `Esc` opens the loadout menu.

Physics defaults on `DefaultMovementConfig` are unchanged aside from serializing existing crouch fields.

## Events added on `MovementController`

`StateChanged`, `Jumped`, `Landed`, `SlideStarted`, `SlideCancelled`, `WallBounced`, `Dived`, `BunnyHopped`, plus `SetConfig`, `Config`, `LastWallNormal`, `WallRunSide`.
