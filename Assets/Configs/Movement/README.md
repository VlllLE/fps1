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

## Feedback (VFX / camera / SFX)

`MovementFeedback` listens to controller events (jump, land, slide, wall bounce, dive, bhop, slide cancel) and:

- Spawns runtime particle systems (slide dust, jump/land bursts, wall sparks)
- Applies camera shake + roll tilt on the **MainCamera** transform (does not touch `FirstPersonCamera` pitch)
- Plays procedural placeholder clips under `Assets/Audio/Movement/`

Wire onto the scene player:

**Tools → Movement → Add Feedback To Scene Player**

Physics defaults on `DefaultMovementConfig` are unchanged aside from serializing existing crouch fields.

## Events added on `MovementController`

`StateChanged`, `Jumped`, `Landed`, `SlideStarted`, `SlideCancelled`, `WallBounced`, `Dived`, `BunnyHopped`, plus `SetConfig`, `Config`, `LastWallNormal`, `WallRunSide`.
