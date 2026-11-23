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
| Reload | R |
| Class loadout | `F1` Assault · `F2` SMG · `F3` Sniper · `F4` Hybrid |
| Loadout menu | `Esc` — class buttons + weapon slots |
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
| F1 | Assault | M4A1 | UMP |
| F2 | SMG | UMP | SCAR |
| F3 | Sniper | Bolt-action | UMP |
| F4 | Hybrid | DMR | M4A1 |

Re-sync scene player: **Tools → Sync Test Range Player (Character + Classes)**.