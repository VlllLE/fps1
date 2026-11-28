# fps1: CoD-style movement shooter

A first-person shooter prototype built in **Unity 6 (6000.6.10f1)** around tight, skill-based movement
(slide cancelling, bunny hopping, dolphin dives, air strafing, wall bounces) and a small set of arenas,
weapons and cosmetics to try it all out. Guns, maps and skins are generated from code or scripts in this repo. The body you see is one of three character looks (Soldier, Operator/SWAT, Ghillie), chosen by weapon class: Sniper and DMR use Ghillie, SMG and shotgun use Operator, rifles and the LMG use Soldier (bots with assault rifles are mixed). Those looks are downloaded models. Guns are Blender-built, with a procedural fallback, and a generated soldier body is used only when a look is missing.

![First-person view with the map selector label](docs/screenshots/ui-map-selector-v2.jpg)

## Contents

- [Features](#features)
- [Screenshots](#screenshots)
- [Controls](#controls)
- [Getting started](#getting-started)
- [Project layout](#project-layout)
- [Tests](#tests)
- [Blender models and tooling](#blender-models-and-tooling)
- [More documentation](#more-documentation)

## Features

### Movement

Tuned through `MovementConfig` assets (`Assets/Configs`), implemented in `Assets/Scripts/Movement`.

- **Slide and slide cancel**: crouch while sprinting to slide, press Space mid-slide to cancel into a jump with a horizontal boost.
- **Bunny hop**: hold Space through landings (or tap it just before touching down) to chain hops and keep speed.
- **Dolphin dive**: dive forward out of a sprint; short cooldown.
- **Air strafing**: hold A or D in the air to build speed.
- **Wall bounce**: jump into a wall at speed and press Space to bounce off it.
- **Movement archetypes**: Default, Scout, Heavy, Acrobat and Marksman configs, plus per-class speed multipliers. Pick one in game from the **MOVEMENT** tab of the loadout menu (`M`).
- **Movement feedback**: slide dust, landing and wall sparks, camera shake and roll, procedural placeholder sounds.

### Weapons and attachments

Gun prefabs live in `Assets/Weapons/Prefabs`, stats in `Assets/Configs/Weapons`. Every visible gun model is a
detailed model generated headless in Blender (`Assets/Weapons/Models`), with the original primitive-built parts kept
in each prefab as an automatic fallback (see [Blender models and tooling](#blender-models-and-tooling)).

| Weapon | Type | Notes |
| --- | --- | --- |
| M4A1 | Assault rifle | Carbine with rail handguard and charging handle |
| SCAR-H | Assault rifle | Heavier battle rifle |
| UMP45 | SMG | Compact, angled magazine |
| Bolt-Action Sniper | Sniper | Scope on a rail, bipod mounted on a fore-end |
| DMR | Semi-auto marksman rifle | Rail-mounted optic, quad-rail handguard |
| Pump Shotgun | Shotgun | Fires 9 pellets per shot, per-shell reload that can be cancelled by firing |
| LMG | Light machine gun | Belt-fed, 100-round box, folded bipod, slow reload |
| Karambit | Melee knife | Secondary on every class |

The eight weapons above are what the loadout menu offers. A pistol (Blender model, animated slide) is also on the player
prefab as the test sidearm, but it is not in the loadout catalog.

Attachments are modelled as parts of the weapon models: scopes with mounting rings and rails on both snipers, a bipod
on the bolt-action sniper and the LMG. The karambit swings through `MeleeAttackResolver` (five short rays per slash)
and plays an alternating forehand and backhand slash. Details are in [`Assets/Weapons/README.md`](Assets/Weapons/README.md).

**Bullets and firing animation.** Every hitscan shot draws a pooled tracer streak from the muzzle to the hit point (or max range), one per shotgun pellet, plus a muzzle flash with a short light, ejected shell casings that fall and fade, and impact effects: sparks, dust and a bullet-hole decal on the world, a small non-gory hit puff on targets. The effects are visual only, so hit detection and the damage-only hitmarker are unchanged. They work from the first-person gun and from the held gun of bots and the player's own body (`Assets/Scripts/Weapons/Fx`, `ShotEffects`). Each gun kicks procedurally when fired (rifle, SMG, LMG, shotgun pump, bolt cycle on the bolt-action sniper, DMR, pistol), and the third-person soldier recoils and works the bolt or pump with the same fire event; the karambit swings the body's torso and arm.

### Weapon skins

20 skins that work on any gun mesh through object-space triplanar projection (Built-in Render Pipeline shaders):

- **Static (12):** Woodland Camo, Arctic Digital, Honeycomb, Prism, Halftone Pop, Damascus, Desert Tiger, Urban Hex,
  Carbon Fiber, Copper Patina, Kryptek Fracture, Night Viper.
- **Animated (8):** Scrolling Energy, Pulsing Emissive, Holographic, Flowing Lava, Neon Rain, Chromatic Shift, Storm Arc, Ember Ash.

`WeaponSkinApplier` sits on every weapon prefab. In Play Mode, `[` and `]` run the **skin cycler**
(`TestRangeSkinCycler`) through the whole skin catalog for the active weapon. See [`Assets/Skins/README.md`](Assets/Skins/README.md).

### Loadout menu and classes

- Press `Esc` (pause menu) or `L` (straight to the Loadout tab) to pick a class, a primary, a secondary and a skin per weapon, with a live rotating preview. Choices apply immediately and persist in `PlayerPrefs`.
- Classes switch on the fly with `F1` to `F4`:

| Key | Class | Primary | Secondary | Speed |
| --- | --- | --- | --- | --- |
| `F1` | Assault | M4A1 | Karambit | x1.00 |
| `F2` | SMG | UMP | Karambit | x1.08 |
| `F3` | Sniper | Bolt-Action Sniper | Karambit | x0.94 |
| `F4` | Hybrid | DMR | Karambit | x1.00 |

The menu can override any slot (including the shotgun and LMG), so the class is just a starting point.

### Movement menu

- Press `M` (or open the **MOVEMENT** tab in the `L` / `Esc` menu) to choose a movement archetype: **Default**, **Scout**, **Heavy**, **Acrobat** or **Marksman**.
- Each archetype shows its key stats as bars: walk and sprint speed, jump height, air control and acceleration, slide boost and length, bunny-hop cap and speed kept, dive speed and wall bounce, plus the wall run, wall bounce and mantle traits.
- Hover an archetype to compare it against the selected one (the white tick marks the selected value and the percentage is the difference). Click it, or press `1` to `5` while the tab is open, to switch. The chosen one is marked `SELECTED`.
- The choice applies to the live player straight away, with no restart, and is saved in `PlayerPrefs` (key `FPS.Movement.v1`), so it comes back next session and on every map.
- It is an explicit choice, independent of class: the default is **Default** (whatever config the scene already uses) and nothing is saved until you pick one. **Reset movement to default** clears it.
- The class speed multiplier (Assault x1.00, SMG x1.08, Sniper x0.94, Hybrid x1.00) is not replaced by the archetype. It still comes from the class and multiplies the archetype's speeds, and the tab shows the resulting walk speed and the per-class list.

### Maps

| Map | Scene | Hotkey | Description |
| --- | --- | --- | --- |
| **TestRange** | `Assets/Scenes/TestRange.unity` | `F5` | Open grey range with walls, ramps and bots, built for practising movement and shooting |
| **Shipment** | `Assets/Scenes/Shipment.unity` | `F6` | Tiny futuristic reskin of the classic container yard: neon-lit stacks, open centre lane, jumpable container tops |
| **Dome** | `Assets/Scenes/Dome.unity` | `F7` | Arena around a hex-panel energy-glass geodesic dome with a ring catwalk, towers and holo displays |
| **Triad** | `Assets/Scenes/Triad.unity` | `Home` | Dusk city grey-box: 3 lanes x 3 height tiers x 3 enterable buildings (Warehouse, Office, Depot), with NavMesh bot routes through every lane and building |
| **Bazaar** | `Assets/Scenes/Bazaar.unity` | `End` | Daylight desert-town tactical map in the Valorant/CS style: two bomb sites (A, B) with painted markers and plant-zone triggers, three routes (A Long and A Short, Mid, B Tunnels and Window), attacker and defender yards with buy zones, one long sightline, rotate paths and heaven/hell rooms. Plays the [tactical mode](#tactical-mode) automatically |

The **map selector** switches maps at runtime with `F5`/`F6`/`F7`/`Home`/`End`, cycles with `PgUp`/`PgDn`, and shows the current map in the top-right corner.
There is also a **MAP** tab in the `Esc` menu. The maps are generated by editor tools (see [Getting started](#getting-started)).

### Tactical mode

Bazaar (and any map that has plant zones plus attacker and defender spawns) runs a round-based bomb mode, with no buying or economy. Your usual create-a-class loadout is what you carry. Shipment stays a 1v1 practice map and never starts it.

- **Squads**: 4 attackers (you and three bots) against 4 defenders (bots). Friendly fire is off by default (`TacticalRules.friendlyFire`).
- **Round loop**: 15 s freeze phase (class select, nobody can move or shoot), a 100 s live round, a 5 s round-end pause, first squad to 4 rounds wins. Sides swap after 3 rounds. Nobody respawns inside a round, everyone is revived and re-placed at the start of the next one, and your weapons are refilled when the round goes live.
- **Bomb**: one attacker (random, possibly you) carries it. Inside plant zone A or B, hold `E` for 3.2 s to plant it (the carrier has to stand still and the hold must not be interrupted). The fuse then burns for 40 s and the round clock stops. A defender holds `E` next to the bomb for 7 s to defuse it. A dead carrier drops the bomb and any living attacker walking over it picks it up.
- **Win conditions**: eliminate the defenders, eliminate the attackers before a plant, the bomb explodes (attackers), the bomb is defused (defenders), or time runs out with no plant (defenders). If the defenders are wiped after the plant the attackers win. If the attackers are wiped after the plant the round plays on until the bomb goes off or is defused.
- **Bots**: attacking bots pick a target site, the carrier trails its entries to the site and plants while the others ring around it, then guard the bomb. Defending bots hold posts at the sites (using the map's waypoints), rotate when a mate makes contact and retake after a plant. See [Bot tactics](#bot-tactics).
- **HUD**: a top bar with the score, alive pips per side (you on the left), side names and the round timer (freeze time, round clock, or the fuse once the bomb is planted); a bomb chip ("YOU HAVE THE BOMB", "BOMB DROPPED", "BOMB PLANTED A 0:32", defused, exploded) with a fuse bar; a plant / defuse progress bar plus a "HOLD E TO PLANT / DEFUSE" prompt; ROUND n / GO / ROUND WON / ROUND LOST / SIDES SWAPPED / VICTORY / DEFEAT banners; a kill feed. Hold `Tab` for the scoreboard (kills, deaths, alive, bomb carrier); it also stays up when the match ends. The layout scales with the screen (`TacticalHudText.ResolveScale`) so it stays readable on a portrait phone-sized window.
- **Class select**: during freeze time the loadout menu opens as a panel on screen (class, primary, secondary, skins). Pick a class, then click READY or press `Enter` to start early; the round goes live when the timer ends and your weapons are refilled. `Esc` still opens the normal menu.
- **Spectating**: after you die the view stays on your body for about two seconds, then a third-person camera follows a living teammate (or an enemy once your squad is gone). Left click, `Space` or the right arrow picks the next player, right click or the left arrow the previous one, and the camera moves on by itself when the watched player dies.
- **Code**: `Assets/Tactical` (`TacticalMatch` scene driver, `TacticalMatchModel` and `TacticalBombModel` pure rules, `TacticalBotDirector`, `TacticalBombProp`, `TacticalHud`, `TacticalHudText`, `TacticalStats`, `TacticalSpectator`), shared team types in `Assets/Scripts/Tactical` (`CombatTeam`, `ITacticalZone`). A map opts in by providing `ITacticalZone` plant zones, `Respawn` tagged spawns (`Spawn_A##` attackers, `Spawn_D##` defenders) and `BotWaypoint` markers. Set the environment variable `FPS_TACTICAL=off` to disable the automatic start.

### Bot tactics

Bots on every map (and in the tactical mode) fight and move with a few human habits. All of it is tuned per difficulty (`F10` cycles Easy, Normal, Hard, Veteran) through `BotSettings` presets, and every number is a public field.

- **Cover and peeking**: after a bot has been exposed to its target for a few seconds (`peekExposeMin/Max`) it steps behind the nearest cover, waits there (`peekHideMin/Max`, counted from arrival) and peeks back out. Exposure is paid back at half speed while out of sight. Easy bots never peek (`usePeek` is off) and keep the old health and reload cover behaviour.
- **Stay out of the open**: a bot that holds a position (a post, a guard spot) walks to a nearby NavMesh edge, so it stands against a wall instead of in the middle of the floor (`preferWalls`).
- **Aim and reaction**: getting hit makes the aim flinch (`flinchError`, fading over `flinchTime`). Each new engagement rolls a reaction time between the base value and `reactionTime * (1 + reactionJitter)`, so bots no longer all react on the same frame. Easy bots flinch hard and react slowly, Veteran bots barely flinch.
- **Teamwork**: `teamwork` (0 Easy, 0.4 Normal, 0.7 Hard, 1 Veteran) scales the squad behaviour below. At 0 the squads behave as in the first tactical version.
- **Attackers**: roles are Carrier, Entry (on the site ring), Support (outer ring), Flanker, PostPlant and, for a dropped bomb, Picker. Flankers take an alternate route: the director picks a map waypoint that lies at least 8 m off the direct line and 8 m from both ends, with at most a 1.9x detour (`TacticalRoutePlanner`), walks there, then approaches the site from that side. The carrier stages about 11 m short of the site until the entries arrive (or 14 s pass). After the plant, bots hold spots with a clear line of sight to the bomb and rush it when someone starts a defuse.
- **Defenders**: Anchors hold posts at their site. When a mate reports contact at a site the nearest anchor of the other site rotates there (once per contact, at most twice a round, never leaving a site empty). After a plant one bot defuses and the others take cover spots, some via side routes. The defuser waits short of the bomb while attackers are on it and there is slack in the fuse, and nobody tries a defuse that cannot finish (`DefuseHopeless`).
- **Code**: `Assets/Bots/Scripts` (`BotPeekTimer`, `BotAimModel`, `BotBrain`, `BotController`, `BotNavigation.TryFindWallPoint`), `Assets/Tactical/Scripts` (`TacticalBotDirector`, `TacticalBotRole`, `TacticalRoutePlanner`). Tests: `BotTacticsTests`, `BotTacticsPlayModeTests`, `TacticalRoutePlannerTests`, `TacticalTeamPlayTests`.

### Characters

- **Character looks**: Soldier, Operator/SWAT and Ghillie, skinned at runtime onto the 19-bone humanoid rig. Sniper and DMR use Ghillie, SMG and shotgun use Operator, rifles and the LMG use Soldier. Bots with assault rifles are a mix of all three. The generated body in `Assets/Characters/Models/Soldier.fbx` is the fallback when a look is missing or fails validation. Credits: [`Assets/Characters/Looks/CREDITS.md`](Assets/Characters/Looks/CREDITS.md). The static training dummies on TestRange and Dome wear a mix of the three looks (faced towards the player spawn, with a subtle hue pull for the old target colour), keeping their hit zones, damage and respawn.
- **Code-driven animation**: idle, walk and strafe, crouch, slide, airborne and dive poses with two-bone IK, no animation clips needed.
- **Gun holding**: bots and the player's own body hold their actual weapon with both hands (shoulder-stocked when aiming, lowered when patrolling, aim pitch over the spine, recoil and reload hand motion).
- **Death animations**: procedural, direction-aware deaths (fall back, forward collapse, headshot snap-back, side fall, kneel), a dropped gun, and a third-person death camera for the player.
- **First-person arms** (`SoldierArmsFP.fbx`) that follow the weapon holder and grip each gun.
- **Procedural fallback**: if the soldier model is missing or fails validation, the older primitive-built procedural humanoid and arms are used instead.

## Screenshots

### Maps

TestRange, Shipment, Dome, Triad and Bazaar from several sides.

| | |
| --- | --- |
| ![TestRange overview](docs/screenshots/map-testrange-overview-v4.jpg) | ![TestRange from the spawn](docs/screenshots/map-testrange-spawn-v4.jpg) |
| ![Shipment overview](docs/screenshots/map-shipment-overview.jpg) | ![Shipment from the west](docs/screenshots/map-shipment-west.jpg) |
| ![Shipment at the west spawn](docs/screenshots/map-shipment-spawn.jpg) | ![Dome overview](docs/screenshots/map-dome-overview.jpg) |
| ![Dome from the north](docs/screenshots/map-dome-north-v4.jpg) | ![Dome at the south spawn](docs/screenshots/map-dome-spawn-v4.jpg) |
| ![Triad overview](docs/screenshots/map-triad-overview.jpg) | ![Triad middle lane](docs/screenshots/map-triad-lane.jpg) |
| ![Triad Warehouse interior](docs/screenshots/map-triad-interior.jpg) | ![Bots in Triad](docs/screenshots/map-triad-bots.jpg) |
| ![Bazaar overview](docs/screenshots/map-bazaar-overview.jpg) | ![Bazaar top view](docs/screenshots/map-bazaar-top.jpg) |
| ![Bazaar A Long](docs/screenshots/map-bazaar-a-long.jpg) | ![Bazaar Mid](docs/screenshots/map-bazaar-mid.jpg) |
| ![Bazaar A site](docs/screenshots/map-bazaar-site-a.jpg) | ![Bazaar B site](docs/screenshots/map-bazaar-site-b.jpg) |
| ![Bazaar B Tunnels](docs/screenshots/map-bazaar-tunnels.jpg) | |

### Menus

The loadout menu (`L` or `Esc`), with the map label shown top right. The map selector label is the first image on this page.

![Loadout menu with the weapon preview](docs/screenshots/ui-loadout-menu-v2.jpg)

The MOVEMENT tab (`M`) with Default selected, hovering Scout to compare its stats against the selected archetype.

![Movement menu comparing Scout with the selected Default archetype](docs/screenshots/ui-movement-menu.jpg)

### Characters

The Soldier look on TestRange: a target bot, the player from a three-quarter view, and a side view in the ready stance.

| | | |
| --- | --- | --- |
| ![Target bot - Soldier look, SCAR (red team tint), as spawned by the bot system.](docs/screenshots/soldier-target-v3.jpg) | ![Player body, three-quarter view - Soldier look, M4A1 with Woodland skin.](docs/screenshots/soldier-player-v3.jpg) | ![Side view - Soldier look holding the M4A1 (Woodland), ready stance.](docs/screenshots/soldier-side-v3.jpg) |

### Character looks

Three looks, chosen by weapon class: Sniper and DMR use Ghillie, SMG and shotgun use Operator, rifles and the LMG use Soldier. Bots with assault rifles are a mix of all three.

| | | |
| --- | --- | --- |
| ![Operator (SWAT) - UMP, Kryptek Fracture](docs/screenshots/look-swat-v3.jpg) | ![Ghillie - Bolt-Action Sniper, Damascus](docs/screenshots/look-ghillie-v3.jpg) | ![The three looks: Soldier, Operator, Ghillie](docs/screenshots/looks-lineup-v3.jpg) |

### Weapons in engine

Blender-built guns in Play Mode on the Shipment map, wearing weapon skins, against its dark neon purple and blue backdrop. The M4A1, shotgun and DMR are first person (arms only). The other shots are the third-person body, with the skin on the held gun and the look for that weapon: Soldier for the SCAR and LMG, Operator (SWAT) for the UMP and shotgun, Ghillie for the bolt sniper and DMR.

| | |
| --- | --- |
| ![M4A1 with the Woodland Camo skin, first person](docs/screenshots/model-m4a1.jpg) | ![SCAR - Desert Tiger (Soldier look)](docs/screenshots/model-scar-v3.jpg) |
| ![UMP - Kryptek Fracture (Operator / SWAT look)](docs/screenshots/model-ump-v3.jpg) | ![Pump shotgun with the Copper Patina skin, first person](docs/screenshots/model-shotgun.jpg) |
| ![LMG - Arctic Digital (Soldier look)](docs/screenshots/model-lmg-v3.jpg) | ![Bolt-Action Sniper - Damascus (Ghillie look)](docs/screenshots/model-sniper-v3.jpg) |
| ![DMR with the Night Viper skin, first person](docs/screenshots/model-dmr.jpg) | ![Shotgun - Copper Patina (Operator / SWAT look)](docs/screenshots/model-shotgun-tp-v3.jpg) |

The DMR in third person, on the Ghillie look:

![DMR - Night Viper (Ghillie look)](docs/screenshots/model-dmr-tp-v3.jpg)

The Damascus-skinned karambit in first person:

![Karambit in hand](docs/screenshots/fp-karambit.jpg)

### Blender renders

Studio renders written by the generators (M4A1, shotgun, LMG, DMR, bolt sniper, pistol), plus the karambit:

![Blender renders of the weapon models](docs/screenshots/blender-weapon-renders.jpg)

![Karambit Blender render](docs/screenshots/fp-karambit-model.jpg)

### Skins

Static skin textures (one tile each):

![Static skin textures](docs/screenshots/skins-static-textures.jpg)

Animated skins on the Blender models (DMR, bolt sniper, shotgun, karambit). These are still frames of shaders that animate in game:

![Animated skins](docs/screenshots/skins-animated.png)

## Controls

Legacy Input Manager. Source of truth: [`Assets/Scenes/TestRange_Controls.md`](Assets/Scenes/TestRange_Controls.md) and the input code in `Assets/Scripts`.

### Movement and combat

| Action | Keys |
| --- | --- |
| Move | `W` `A` `S` `D` or arrow keys |
| Look | Mouse (cursor is locked in play; `Esc` opens the menu and unlocks it) |
| Jump | `Space` |
| Sprint | `Left Shift` |
| Crouch / slide | `C` or `Left Ctrl` |
| Dolphin dive | `V` while sprinting |
| Fire | Left mouse |
| Aim down sights | Right mouse |
| Reload | `R` |
| Primary / secondary | `1` / `2` |
| Cycle weapons | Mouse wheel |
| Last weapon | `Q` |
| Plant / defuse the bomb (tactical mode) | Hold `E` inside a plant zone (carrier) or next to the planted bomb (defender) |
| Scoreboard (tactical mode) | Hold `Tab` |
| Ready up during class select (tactical mode) | `Enter` or the READY button |
| Switch spectated player after dying (tactical mode) | Left / right click, `Space`, left / right arrow |

### Menus and cosmetics

| Action | Keys |
| --- | --- |
| Loadout menu | `L` (Loadout tab) or `Esc` (pause menu) |
| Movement archetype | `M` (Movement tab), then click or press `1` to `5` (Default, Scout, Heavy, Acrobat, Marksman) |
| Class | `F1` Assault, `F2` SMG, `F3` Sniper, `F4` Hybrid |
| Map | `F5` TestRange, `F6` Shipment, `F7` Dome, `Home` Triad, `End` Bazaar, `PgUp` / `PgDn` to cycle |
| Cycle skin | `[` previous, `]` next (active weapon) |
| Bots | `F8` spawn, `F9` freeze, `F10` difficulty, `F11` debug overlay, `\` or `F12` remove / restore (see `TestRange_Controls.md`) |

### Movement techniques

| Technique | How |
| --- | --- |
| Slide | Sprint (`Shift` + `W`) to roughly 4 m/s or more, then tap `C` / `Ctrl` |
| Slide cancel | Press `Space` while sliding for a horizontal boost jump |
| Bunny hop | Hold `Space` through landings, or tap it within about 0.15 s of landing |
| Dolphin dive | Sprint, then `V` (cooldown about 1.2 s) |
| Air strafe | Hold `A` or `D` in the air while moving; gains come from the air control values in `MovementConfig` |
| Wall bounce | Jump toward a wall at about 6 m/s or faster, then `Space` near it |

Walls need a collider on the `Default` layer to be bounceable. If jumps or slides never register but `WASD` works,
run **Tools > Sync Test Range Player (Character + Classes)**.

## Getting started

1. Install **Unity 6000.6.10f1** (pinned in `ProjectSettings/ProjectVersion.txt`). The project uses the Built-in Render Pipeline and the legacy Input Manager.
2. Open the repository root as a project in Unity Hub and let it import.
3. Open a scene from `Assets/Scenes` (`TestRange`, `Shipment`, `Dome`, `Triad` or `Bazaar`) and press Play. Maps can also be switched in play with `F5`/`F6`/`F7`/`Home`/`End`.

The `Tools` menu holds the editor utilities:

| Menu item | What it does |
| --- | --- |
| `Tools > Build Test Range`, `Open Test Range Scene`, `Enter Test Range Play Mode` | Rebuild, open or play the TestRange scene |
| `Tools > Maps > Build Shipment` / `Open Shipment` / `Enter Shipment Play Mode` | Regenerate, open or play Shipment |
| `Tools > Maps > Build Dome` / `Open Dome` / `Enter Dome Play Mode` | Regenerate, open or play Dome |
| `Tools > Maps > Build Triad` / `Open Triad` / `Enter Triad Play Mode` | Regenerate, open or play Triad |
| `Tools > Maps > Build Bazaar` / `Open Bazaar` / `Enter Bazaar Play Mode` | Regenerate, open or play Bazaar |
| `Tools > Sync Test Range Player (Character + Classes)` | Re-sync the scene player (character, ground check, camera refs, classes) |
| `Tools > Sync Test Range Weapon Loadout` | Put the weapon prefabs into the TestRange weapon switcher |
| `Tools > Loadout > Rebuild Weapon Catalog` | Regenerate `Assets/Resources/WeaponLoadoutCatalog.asset` after adding weapons, skins or classes |
| `Tools > Skins > Rebuild Weapon Skin Catalog` | Regenerate the skin catalog used by the skin cycler (an EditMode test fails if it is stale) |
| `Tools > Classes > ...` | Create the default class definitions, add the class applier, apply a class to the scene player |
| `Tools > Movement > ...` | Ensure the movement configs, add feedback, apply an archetype config to the scene player, and `Rebuild Movement Catalog` (regenerates `Assets/Resources/MovementArchetypeCatalog.asset` behind the MOVEMENT tab; an EditMode test fails if it is stale) |
| `Tools > Build Player Character Prefab` | Regenerate the `PlayerCharacter` prefab |
| `Tools > Run Play Mode Smoke Check` | Quick play-mode sanity run |

Map scenes can also be built headless, for example
`Unity -batchmode -projectPath . -executeMethod ShipmentMapBuilder.BuildBatch -quit`
(Dome: `FpsMaps.Dome.EditorTools.DomeMapBuilder.BuildBatch`, Triad: `FpsMaps.Triad.EditorTools.TriadMapBuilder.BuildBatch`, Bazaar: `FpsMaps.Bazaar.EditorTools.BazaarMapBuilder.BuildBatch`).

## Project layout

```
Assets/
  Scenes/          TestRange, Shipment, Dome, Triad, Bazaar scenes + TestRange_Controls.md
  Scripts/         Movement, Player, Weapons, Characters (humanoid rig, soldier binder, fallback), Classes, Maps, Targets
  Loadout/         Loadout menu, catalog, selection store, preview rig
  Tactical/        Round-based bomb mode: match driver, rules models, bot director, bomb prop and tests
  Weapons/         Gun prefabs, materials, Blender-built models (Models/<Gun>/*.fbx), weapon README
  Skins/           Skin shaders, textures, materials, definitions, applier; Tools~ has the Python texture generators
  Classes/         Class definition assets (Assault, SMG, Sniper, Hybrid)
  Configs/         MovementConfig archetypes and per-weapon WeaponConfig assets
  Characters/      PlayerCharacter prefab and notes; Looks/ holds Soldier, Operator and Ghillie; Models/ holds the fallback body and first-person arms
  Maps/            Shipment, Dome, Triad and Bazaar scripts, shaders, materials, textures and tests
  Resources/       Weapon loadout and movement archetype catalogs, runtime-loaded audio
  Editor/          Map builders, scene sync tools and the Tools menu items
  Tests/           EditMode (Editor) and PlayMode test assemblies
Tools/
  Blender/         Headless Blender 4.2.3 generators for every gun, the karambit and the soldier, plus wire_weapon_models.py
  WeaponGen/       Python generator for the shotgun and LMG primitive prefabs
  Weapons/         Python scripts that mount sniper scopes/bipods and wire imported models into prefabs
docs/
  screenshots/     Images used by this README
```

## Tests

Unity Test Framework (`com.unity.test-framework` 1.4.5). Open **Window > General > Test Runner**
and pick EditMode or PlayMode, or run them headless:

```bash
# EditMode
Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults EditModeResults.xml

# PlayMode
Unity -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults PlayModeResults.xml
```

- **EditMode** (`Assets/Tests/Editor`, plus `Assets/Maps/*/Tests`): movement logic, hit detection, class and loadout selection, movement archetype catalog and selection, skin catalog and applier, humanoid body, melee, shotgun and LMG configs, sniper mounting, map catalog, and map layout and geometry contracts.
- **PlayMode** (`Assets/Tests/PlayMode`, plus `Assets/Maps/*/Tests`): TestRange smoke tests, movement techniques such as bunny hop, humanoid bodies in scene, map selector, loadout and movement menus, melee, shotgun and LMG, and the Shipment, Dome, Triad and Bazaar scenes plus the tactical round loop on Bazaar in `Assets/Tactical/Tests` (the generated scene has to exist first, so build the map before running its PlayMode tests).

## Blender models and tooling

The guns, the karambit and the fallback body are authored as code and exported into the Unity project, so they can be rebuilt without
opening a DCC tool. The three in-game looks are downloaded models in `Assets/Characters/Looks`. Each generator is a headless **Blender 4.2.3** script that writes an `.fbx`
(plus a `.glb` copy) with the muzzle on +Z, metre units and identity rotation, so the prefabs' anchor points line up.

| Where | What |
| --- | --- |
| `Tools/Blender/` | Generators: `build_m4a1.py`, `build_scar.py`, `build_ump.py` (shared `assault_kit.py`), `build_shotgun.py`, `build_lmg.py`, `build_pistol.py` (shared `weapon_lib.py`), `build_bolt_sniper.py`, `build_dmr.py` (shared `gunlib.py`, `gunparts.py`), `build_karambit.py`, and `build_soldier.py` (with `soldier_lib/`). `preview_*_held.py` and `render_gun.py` render studio and held-pose mock images. |
| `Assets/Weapons/Models/<Gun>/` | Exported gun models (`.fbx`, `.glb`, and `.anchors.json` for the snipers). |
| `Assets/Characters/Models/` | `Soldier.fbx` (fallback third-person body), `SoldierArmsFP.fbx` (first-person arms), baked AO maps and soldier materials. |

Run a generator from the repository root, for example:

```bash
blender -b --factory-startup --python Tools/Blender/build_m4a1.py -- \
    --out Assets/Weapons/Models/M4A1 [--renders <dir>]
blender -b --factory-startup --python Tools/Blender/build_soldier.py -- \
    --out Assets/Characters/Models [--renders <dir>] [--no-render]
```

Each script documents its own options at the top of the file, so new generators only need a docstring.
Keep the `.meta` files next to the FBXs when overwriting them: they hold the importer settings, GUIDs and material remaps.

### FBX with a procedural fallback

The original primitive-built parts are never deleted. Small loader components on each prefab (`AssaultWeaponModel`,
`ImportedGunModel`, `WeaponModelLoader`, `KarambitModel`) instantiate the FBX at runtime, validate it, and only then hide
the primitive parts. If the FBX is missing, empty or fails validation, the primitives stay visible and the game keeps
working. The soldier works the same way: `SoldierModelBinder` skins the FBX onto the runtime bones, and
`HumanoidBody.UsesImportedModel` / `ImportFallbackReason` report which body is active.

### Wiring scripts

Plain Python, no Unity or Blender needed, and safe to re-run:

- `python3 Tools/Blender/wire_weapon_models.py` wires the shotgun, LMG and pistol models into their prefabs and writes the `.fbx.meta` files and `Mat_GunAccent`.
  **Re-run it after `Tools/WeaponGen/build_shotgun_lmg.py`**, because that primitive builder regenerates `Shotgun.prefab` and `LMG.prefab` from scratch and drops the wiring.
- `Tools/Weapons/wire_assault_models.py` (M4A1, SCAR, UMP) and `Tools/Weapons/wire_imported_gun_models.py` (bolt sniper, DMR) do the same for those prefabs.
- `python3 Tools/Blender/write_gun_meta.py <model folder> <Gun>` writes the `.meta` files (importer settings, material remaps, pinned GUIDs) for a generated sniper model folder.
- `Tools/Weapons/mount_sniper_parts.py` rebuilds the scope and bipod hierarchy of the primitive sniper prefabs.

Per-model details (triangle counts, object names, material slots, held poses) are in
[`Assets/Weapons/README.md`](Assets/Weapons/README.md) and [`Assets/Characters/Player/README.md`](Assets/Characters/Player/README.md).

## More documentation

- [`Assets/Scenes/TestRange_Controls.md`](Assets/Scenes/TestRange_Controls.md): keys, movement tech, loadout menu
- [`Assets/Weapons/README.md`](Assets/Weapons/README.md): weapon prefabs and Blender models, shotgun and LMG, karambit, sniper mounting
- [`Assets/Skins/README.md`](Assets/Skins/README.md): skin system and how to add skins
- [`Assets/Classes/README.md`](Assets/Classes/README.md): classes and the loadout menu service
- [`Assets/Configs/Movement/README.md`](Assets/Configs/Movement/README.md): movement archetypes and feedback
- [`Assets/Characters/Player/README.md`](Assets/Characters/Player/README.md): character looks, fallback body, player prefab
- [`Assets/Maps/Shipment/README.md`](Assets/Maps/Shipment/README.md), [`Assets/Maps/Dome/README.md`](Assets/Maps/Dome/README.md), [`Assets/Maps/Triad/README.md`](Assets/Maps/Triad/README.md), [`Assets/Maps/Bazaar/README.md`](Assets/Maps/Bazaar/README.md): map layouts and build steps
