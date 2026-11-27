# TestRange — default controls (legacy Input Manager)

## On-screen HUD (Play Mode)

- **Crosshair** (center) — spreads when you move or fire
- **Bottom-right** — current **weapon name** + ammo `mag | reserve` (turns yellow/red when low/empty)
- **Hit marker** — brief X when shots deal damage (red tint on headshots)

| Action | Keys |
|--------|------|
| Move | W A S D / Arrow keys |
| Look | Mouse (cursor locked on play; Esc toggles lock) |
| Jump | Space |
| Sprint | Left Shift |
| Crouch / hold slide | C or Left Ctrl |
| Dolphin dive | V (while moving fast, airborne context) |
| Fire | Left mouse |
| Aim (ADS) | Right mouse |
| Reload | R (Pump Shotgun loads one shell at a time; pull the trigger to interrupt) |
| Class loadout | `F1` Assault · `F2` SMG · `F3` Sniper · `F4` Hybrid |
| Loadout menu | `Esc` (pause menu) or `L` (straight to the Loadout tab) — class, primary, secondary (incl. Karambit), skin per weapon, live preview |
| Movement menu | `M` — movement archetype (Default / Scout / Heavy / Acrobat / Marksman), stats and class speed multiplier |
| Weapon slot | `1` Primary · `2` Secondary (per class) |
| Cycle weapons | Mouse scroll (when slots filled) |
| Last weapon | Q |
| Cycle camo/skin | `[` / `]` (active view model) |

### Movement tech (CoD / Apex-style)

| Tech | Keys | How it works |
|------|------|----------------|
| **Slide** | **Shift + W**, then tap **C** / **Ctrl** while at sprint speed (≥ ~4 m/s) | Crouch on a fast sprint to enter slide. |
| **Slide cancel** | While sliding, press **Space** | Jump to cancel slide with a horizontal boost. |
| **Bunny hop** | Hold **Space** through landings or tap **Space** within ~0.15s of landing | Chains hops and keeps speed on flat ground. |
| **Dolphin dive** | **Shift + W** until sprinting, then **V** | Forward dive while sprinting (or out of a sprint jump). Cooldown ~1.2s. |
| **Air strafe** | **A** / **D** in the air while moving | Hold strafe; speed gains come from `MovementConfig` air control. |
| **Wall bounce** | Jump toward **Ramp_A / B** or **Wall_*** while moving **≥ ~6 m/s** airborne | **Space** near a wall to bounce off; walls need a collider on Default layer. |

Stance: hold **C** / **Ctrl** on the ground to crouch (slower walk, lower capsule). **Space** jumps when grounded; release slide by letting go of crouch or timing out.

If jumps or slides never register but WASD works, run **Tools → Sync Test Range Player** after pulling updates (ground check + camera refs).

### Class loadouts (default on Play: **Assault**)

| Key | Class | Primary | Secondary |
|-----|--------|---------|-----------|
| F1 | Assault | M4A1 | Karambit |
| F2 | SMG | UMP | Karambit |
| F3 | Sniper | Bolt-action | Karambit |
| F4 | Hybrid | DMR | Karambit |

Re-sync scene player: **Tools → Sync Test Range Player (Character + Classes)**.

### Weapon selection menu (`Esc` / `L`)

- **LOADOUT** tab: pick a **class**, then override the **primary** and **secondary** (the Karambit is secondary-only) and choose a **skin per weapon**. The preview on the right turns the focused weapon; hover a skin to preview it before picking.
- Choices apply to the player immediately and are saved in `PlayerPrefs` (key `FPS.Loadout.v1`), so they survive scene / map changes and restarts. `F1`–`F4` class picks are saved too. **Reset loadout to defaults** clears the save.
- **MOVEMENT** tab (`M`): pick a movement archetype (Default, Scout, Heavy, Acrobat, Marksman) with stat bars, hover-to-compare, click or `1`–`5` to switch. Applies live and is saved in `PlayerPrefs` (key `FPS.Movement.v1`). The class speed multiplier still stacks on top. See `Assets/Configs/Movement/README.md`.
- **MAP** tab: the map selector (unchanged).
- `Esc` closes the menu, `L` toggles it on the Loadout tab, `M` toggles it on the Movement tab. Closing always re-locks the cursor and restores the time scale; firing / weapon switching are suppressed while it is open.
- The catalog also offers the **Pump Shotgun** (9 pellets, short range, per-shell reload) and the **LMG** (100-round belt, slow reload, heavy recoil) as primary or secondary.
- The weapon/skin/class lists come from `Assets/Resources/WeaponLoadoutCatalog.asset`; after adding a weapon prefab, skin or class run **Tools → Loadout → Rebuild Weapon Catalog**.

### Enemy bots (TestRange, Shipment, Dome)

Armed AI soldiers spawn automatically in every map: **3 bots at Normal difficulty** shortly after the scene loads. They patrol, spot you through a view cone with a line-of-sight check, chase or strafe at a preferred range, shoot with real weapon configs (`HitDetection`, fire rate, bursts, reload), duck behind cover when hurt, die and respawn. You get a health bar, red damage-direction wedges, a vignette and a "YOU DIED" screen, then respawn at your start after about 3 s. Health regenerates after ~5 s without damage. The static training targets are unchanged.

| Key | Action |
|-----|--------|
| `F8` | Spawn another bot out of your sight |
| `F9` | Freeze / unfreeze every bot (they stop moving, aiming and shooting) |
| `F10` | Cycle difficulty Easy → Normal → Hard → Veteran (applies to every bot immediately, saved in `PlayerPrefs` key `FPS.Bots.Difficulty.v1`) |
| `F11` | Toggle the bot debug overlay (state, health and distance above each bot) |
| `\` or `F12` | Remove all bots; press again to bring the default bots back. `F12` always works; `\` is also detected by the typed character, so it works on UK (ISO) and other layouts where the key is not reported as Backslash |

| Difficulty | Health | Reaction | Aim error | Damage vs player |
|------------|--------|----------|-----------|------------------|
| Easy | 70 | ~0.95 s | ~7° | 25 % |
| Normal | 100 | ~0.5 s | ~3.5° | 40 % |
| Hard | 130 | ~0.3 s | ~2° | 60 % |
| Veteran | 160 | ~0.15 s | ~1° | 80 % |

Robustness: bots that want to move but make no progress escalate through repath, a new waypoint/reposition point, direct steering and finally a respawn; Chase/Attack switches have a minimum state time and sight requirement so a bot at a corner does not flicker; an attacking bot that cannot shoot for 3 s repositions; bots push each other apart instead of stacking. The player's yaw/pitch is reset to the spawn facing on respawn and the damage vignette is a soft radial gradient. Tunables are on `BotSettings` (*Anti-flicker*, *Unstuck and separation*).

Difficulty also scales movement speed, view distance, burst length and cover use. Set the environment variable `FPS_BOTS=0` to disable the auto-spawn and bot keys.

**Navigation:** there is no baked NavMesh. The `BotManager` builds one at runtime from the scene colliders (legacy `UnityEngine.AI` `NavMeshBuilder`, player/targets/bots excluded) so it works on all three maps without editing scenes or map builders. If NavMesh generation fails, bots fall back to raycast/capsule-cast steering.

**Customising:** drop `BotWaypoint` components on empty objects to define patrol routes (otherwise random reachable points are used), or create a `BotSettings` asset (*Create → Bots → Bot Settings*) and assign it with `BotManager.UseSettings`.
