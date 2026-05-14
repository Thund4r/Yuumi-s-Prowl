# Yuumi's Prowl - Project Documentation

> **AI-Assisted Development Guide**
> This document reflects the current state of the codebase and helps Claude (and human developers) understand the project structure, conventions, and systems.

---

## Project Overview

**Yuumi's Prowl** is a mobile 2D puzzle game combining Zuma-style ball-chain mechanics with League of Legends' Yuumi character and her signature homing projectile ability (Q - Prowling Projectile).

### Core Gameplay Loop
1. Colored balls emerge from a hole and move along a curved spline path toward the end
2. Player taps to shoot Yuumi's homing projectile toward their cursor/finger
3. Projectile inserts into the ball chain on collision
4. 3+ consecutive matching colors are destroyed; cascade matches are detected at segment-merge boundaries
5. With a gap present, only the front segment moves (it pulls backward at `gapCloseSpeed`) until it absorbs everything behind it
6. **Win:** chain cleared entirely via matches, OR all balls retreat back into the hole via recoil
7. **Lose:** lead ball reaches the end of the path

### Technical Stack
- **Engine:** Unity 2022.3.62f3 LTS
- **Language:** C#
- **Platform:** Mobile (Android/iOS), tested in editor with mouse
- **Target Performance:** 60 FPS on mid-range devices
- **Physics:** 3D colliders used in XY plane (not 2D physics)
- **3rd-party:** Unity Splines, TextMeshPro (used by `ComboPopup`)

---

## Architecture Overview

Single persistent `Game` scene that loads map content as prefabs. Systems communicate via events and direct serialized references — no singletons.

```
┌──────────────────────────────────────────────────────────┐
│  LevelManager                                            │
│  Loads map prefabs into the active scene, binds each     │
│  map's PathController into BallChainManager, drives      │
│  win → next-map and lose → retry transitions.            │
└────────────────────┬─────────────────────────────────────┘
                     │
          ┌──────────▼──────────┐
          │     GameManager     │
          │  Win/lose state,    │
          │  retreat detection  │
          └──────────┬──────────┘
                     │ events
   ┌─────────────────┼──────────────────┬──────────────────┐
   │                 │                  │                  │
┌──▼────────┐  ┌─────▼──────┐  ┌────────▼────────┐  ┌──────▼──────┐
│ BallChain │  │   Match    │  │   Projectile    │  │  Power-Ups  │
│  System   │  │ Processor  │  │     System      │  │   System    │
└───────────┘  └─────┬──────┘  └─────────────────┘  └─────────────┘
                     │ OnMatchVisual
              ┌──────▼───────────┐
              │ MatchEffectPlayer │ ← particles + combo popups
              └───────────────────┘
```

### Namespaces

| Namespace | Contents |
|---|---|
| `YuumisProwl` | `BallColor`, `BallColorUtils`, `LevelData`, `BallPowerUpType` |
| `YuumisProwl.BallChain` | `Ball`, `BallNode`, `ChainSegment`, `BallChainManager`, `BallSpawner`, `PathController`, `MatchDetector`, `MatchProcessor`, `BallDestructionEffect`, `Obstacle` |
| `YuumisProwl.Projectile` | `Projectile`, `ProjectileSpawner` |
| `YuumisProwl.Managers` | `GameManager`, `LevelManager`, `Map` |
| `YuumisProwl.Player` | `YuumiController` |
| `YuumisProwl.PowerUps` | `PowerUpType`, `PowerUpInventory`, `PowerUpChargeTracker`, `PowerUpSpawner`, `PowerUpSettings`, `PowerUpUIController`, `PowerUpIconDatabase` |
| `YuumisProwl.Progression` | `RunManager`, `RunConfig`, `RunState`, `RunNode`, `RuntimeStats`, `UpgradeDefinition`, `UpgradeDraftUI` |
| `YuumisProwl.VFX` | `MatchEffectPlayer`, `ComboPopup` |
| `YuumisProwl.Utilities` | `ObjectPool<T>` |

---

## Implemented Systems

### Ball Chain System

The chain is internally one or more **`ChainSegment`s** (contiguous runs of balls). Gaps between segments (e.g. from a Bomb or a mid-segment match) persist visibly; each segment moves, recoils, and matches independently.

- `BallNode` — data class holding `ball`, `pathProgress` (0–1 along spline), `chainIndex`, `segmentId`. Also carries two visual-only offsets used for smooth insertion animation: `smoothShift` (path-progress) and `worldOffset` (world-space). These decay to zero over `insertionDuration`.
- `ChainSegment` — `id`, `List<BallNode> balls`. Lead = index 0 (highest progress), Tail = last index.
- `BallChainManager` — owns segments. Movement is **lead-driven**:
  - **Single segment**: the lead moves forward at `ballSpeed`; everything follows at fixed spacing.
  - **Multiple segments**: only the front segment moves, and it moves *backward* at `gapCloseSpeed`. Every other segment is stationary. Forward motion only resumes once the chain merges back into one segment.
  - `MergeTouchingSegments` runs each frame and fires `OnSegmentsMerged(mergedSegmentId, boundaryLocalIndex)` — `MatchProcessor` uses this event to run cascade match detection at the merge point.
  - Public runtime API: `SetPathController(PathController)`, `SetMoving(bool)`, `SetSpeed(float)`, `ClearChain()`, `HasVisibleBalls()`, `InsertBallAtProgress(BallColor, float, Vector3?)`, `SpawnHammerBall(int, float)`, `GetBallChain()`, `GetSegments()`.
  - Pool is initialized in `Awake` (so it's ready before `LevelManager.Start` runs `LoadMap` → `SpawnBall`).
- `PathController` — wraps Unity `SplineContainer`. Lives **on each map prefab** alongside its spline (not in the scene). `BallChainManager.SetPathController` rebinds it on each map load.
- `BallSpawner` — plays the per-level intro animation (AnimationCurve-driven), then feeds tail balls up to `totalBallsToSpawn`. **Does not auto-start** — `LevelManager.LoadMap` calls `StartLevel()` after the map's PathController has been bound.
- `MatchDetector` — pure (non-MonoBehaviour) class; detects 3+ consecutive color matches and scans whole segments for `DetectAllMatches`.
- `MatchProcessor` — owns the segmented match pipeline:
  1. Insertion-driven: after a ball is inserted, wait `destructionDelay`, then `DetectMatchAtIndex` at the insertion site.
  2. Removal splits the containing segment if the match was mid-segment.
  3. With a gap present, the chain's lead-driven backward motion pulls the front segment toward the next segment. At each merge, `OnSegmentsMergedHandler` runs cascade detection on the seam.
  4. Once the front segment is back-most (all gaps closed), apply a `SmoothStep` recoil to the merged segment.
  - Concurrent sequences are supported: matches that happen mid-gap-closing share the sequence keyed by `segmentId` (see `sequencesById`).
  - Events: `OnBallsDestroyed(int, BallColor)` (for charge tracking), `OnChainCleared`, `OnMatchSequenceComplete(cascadeCount, lastGapGlobalIndex)`, **`OnMatchVisual(List<Vector3> positions, BallColor, int cascadeIndex)`** (fired *before* `RemoveBalls`, used by `MatchEffectPlayer`).
  - Public power-up entry points: `ApplyChainRecoil(float, ChainSegment)`, `TriggerRecoil(float, ChainSegment)`, `TriggerRecoil(float, int globalChainIndex = -1)`, `ProcessPierceAftermath(int)`.
- `BallColorUtils` — single source of `ToUnityColor(BallColor)` used everywhere that needs the on-screen color.

### Projectile System
- `Projectile` — homing projectile that continuously tracks cursor/touch after launch. On `Ball` hit: calls `BallChainManager.InsertBallAtProgress(color, progress, transform.position)` so the new ball's `worldOffset` makes it slide in from the projectile's actual world position. On `Obstacle` hit: discarded.
- `ProjectileSpawner` — pools projectiles; enforces one in flight at a time (`projectileInFlight`). Blocks shooting during `BallSpawner.IsPlayingIntro` and `LevelManager.IsTransitioning`. **Does not block during `MatchProcessor.IsProcessingMatches`** — the player can shoot while the chain is closing a gap because the match processor supports concurrent sequences per segment. Pre-loads the next color while a projectile is in flight. Fires `OnShot`.
- Power-up effects on the projectile are applied via `Projectile.SetPowerUp(PowerUpType, …)` immediately before launch; `Pierce` does an instant `Physics.SphereCastAll`, `Bomb` does `Physics.OverlapSphere` on contact.

### Player
- `YuumiController` — rotates Yuumi's GameObject toward cursor/touch via `Mathf.MoveTowardsAngle`. Subscribes to `ProjectileSpawner.OnShot` to trigger an Animator `Throw` trigger. `rotationOffset` compensates for sprite orientation.

### Obstacle System
- `Obstacle` — empty marker component placed on any GameObject with a Collider; detected by `Projectile.OnTriggerEnter` via `GetComponent<Obstacle>()`. No tag required.

### Level & Map System

The game runs in a single persistent `Game.unity` scene; *map content* lives in prefabs under `Assets/Prefabs/Maps/`. There is no transition scene system anymore.

- `LevelData` (ScriptableObject, namespace `YuumisProwl`) — `totalBalls`, `colorCount`, `ballSpeed`. Assigned to a `Map` prefab's root, not directly to the scene.
- `Map` — component placed on the root of a map prefab. Exposes `PathController` and `LevelData` references to whatever's inside that prefab.
- `LevelManager` — holds a `Map[] mapPrefabs` array. On `Start` instantiates the first map; on win advances to the next (or wraps if `loopMaps`); on lose re-instantiates the same map after `pauseBetweenMaps`. Each `LoadMap` does: destroy current map instance → `ClearChain` → instantiate prefab → `SetPathController` → apply `LevelData` → `InitializeGame` → `SetMoving(true)` → `BallSpawner.StartLevel`. Exposes `IsTransitioning` (true throughout teardown/instantiate/intro).
- `MainMenu` (scene `Main Menu.unity`) — currently auto-loads the `Game` scene on `Start`. The auto-load is placeholder behavior intended to be wired to a Play button.

### Power-Ups
- `PowerUpType` (enum, namespace `YuumisProwl.PowerUps`) — `None`, `Pierce`, `Bomb`. Player-earned, equipped to modify the next projectile.
- `BallPowerUpType` (enum, namespace `YuumisProwl`) — `None`, `Hammer`. Embedded directly in a ball in the chain.
- `PowerUpSettings` (ScriptableObject) — shared tuning asset; charge thresholds, slot count, hammer recoil distance, pierce distance/speed, bomb radius, natural-spawn rates.
- `PowerUpInventory` — fixed-size slot array (size from `PowerUpSettings.maxPowerUpSlots`). `AddPowerUp` finds the first empty slot. `EquipSlot(int)` toggles (pressing the same slot again unequips). `ConsumeEquipped()` is called by `ProjectileSpawner` immediately before `Launch`. Number keys 1–3 equip slots in editor builds; key `P` debug-grants a Pierce.
- `PowerUpChargeTracker` — listens to `MatchProcessor.OnBallsDestroyed` and `OnMatchSequenceComplete`, accumulates charge per ball + per cascade, and calls `AwardPowerUp` (random pick from `{Pierce, Bomb}`) when the threshold is hit.
- `PowerUpSpawner` — natural in-chain spawning of `Hammer` balls (a roll every `naturalSpawnInterval`, also rewarded when a cascade meets `rewardCascadeThreshold`).
- `PowerUpUIController` / `PowerUpIconDatabase` — Inventory UI: per-slot icon Image; raycast target is disabled on the icon child so clicks reach the slot button. The equipped highlight is refreshed both on equip and on consume.

### VFX
- `MatchProcessor.OnMatchVisual` — fires synchronously *before* `RemoveBalls` at every match site (initial insertion match, mid-sequence match, merge cascade) so positions are still valid. Gated by `enableDestructionEffects` on `MatchProcessor`.
- `MatchEffectPlayer` (scene-level, in `Game.unity`) — subscribes to `OnMatchVisual`. Pools both effect prefabs via `ObjectPool<T>`. Plays one pooled `BallDestructionEffect` at the **centroid** of the destroyed balls (one effect per match, not per ball), and one pooled `ComboPopup` at the same centroid showing combo text when `cascadeIndex >= comboLabelMinCascade`.
- `BallDestructionEffect` — accepts an **array of ParticleSystems** (or auto-collects every `ParticleSystem` in the prefab's hierarchy if the array is empty). `tintParticles` toggles whether each system's `main.startColor` gets the match color. `effectDuration` controls when the GameObject auto-deactivates; `MatchEffectPlayer.ReturnEffectAfter` waits ~1.2s before returning to the pool, so `effectDuration` must be ≤ that.
- `ComboPopup` — world-space `TMP_Text` that animates upward + scales + fades over `duration`, then self-deactivates.

### Game State
- `GameManager` — fires `OnGameWon` / `OnGameLost`. `InitializeGame()` is called by `LevelManager.LoadMap` (no longer by `GameManager.Start`).
  - **Win 1:** `MatchProcessor.OnChainCleared` (chain emptied via matches).
  - **Win 2:** `BallCount > 0` AND `!HasVisibleBalls()` AND intro not playing (all balls retreated into the hole).
  - **Lose:** `BallChainManager.OnBallReachedEnd`.

### Progression & In-Run Upgrades
- `RunManager` — orchestrates a run: generates `RunNode[]`, loads maps via `LevelManager`, listens to `GameManager.OnGameWon` to trigger draft UI, applies upgrades, and advances to the next node. On loss or final node win, ends the run, clears balls, and grants essence reward to `PlayerProfile`. Resets `RuntimeStats` at run start and applies meta upgrades from profile. Calculates essence rewards based on floors cleared, depth multiplier, difficulty multiplier, and player's meta upgrade bonuses.
- `RunConfig` (ScriptableObject) — run authoring: `mapPool[]`, `mapCount`, `allowDuplicates`, and difficulty curves `ballSpeedCurve` / `totalBallsCurve` (sampled as multipliers based on floor progress `t`).
- `RunState` — in-memory run state: `RunNode[]`, `currentNodeIndex`, `gold`, `appliedUpgrades[]`. Discarded at run end; meta persistence lives in `PlayerProfile`.
- `RunNode` / `RunNodeType` — defines one step in a run: `Gameplay` (load a map) or `Shop` (stubbed for step 6).
- `RuntimeStats` (MonoBehaviour) — mutable per-run wrapper for tunables: `YuumiRotationSpeed`, `ChargePerBallDestroyed`, `CascadeBonusCharge`, `ChargeThreshold`, `PierceMaxDistance`, `PierceSpeedMultiplier`, `PierceWidthMultiplier`, `BombRadius`, `HammerRecoilDistance`. `ResetToDefaults()` copies baselines from `PowerUpSettings`. Consumed by `YuumiController`, `PowerUpChargeTracker`, `ProjectileSpawner`, etc. with null-safe fallbacks.
- `UpgradeDefinition` (ScriptableObject) — defines an in-run upgrade: `UpgradeStat` (enum: `YuumiRotationSpeed`, `ChargePerBall`, `PierceWidth`, `BombRadius`), `ModifierValue` (applied as multiplicative for speed/width, additive for charge/radius), `UpgradeName`, `Description`, `Icon`. `Apply(RuntimeStats)` mutates the stat. Create via **Yuumi → Upgrade Definition** context menu.
- `UpgradeDraftUI` — displays 3 random upgrade choices after a level win (except final node). Shows icon, name, description per option. Fades in/out and fires a callback on selection so `RunManager` can apply and advance.
- Draft flow: `GameManager.OnGameWon` → `RunManager.HandleNodeWon()` → if not last node, call `UpgradeDraftUI.Show(options, callback)` → `RunManager.HandleUpgradeSelected(upgrade)` → apply to `RuntimeStats` → `AdvanceToNextNode()` → load next map.

### Meta Progression
- `MetaProgressionSettings` (ScriptableObject) — tunable configuration for post-run rewards: `baseEssencePerFloor`, `essenceDepthCurve` (multiplier based on run depth), `essenceDifficultyScaling` (bool; if true, multiplies by ballSpeed×totalBalls), and meta upgrade caps (`chargePerBallMetaCap`, `essenceGainCapMultiplier`). Create via **Yuumi → Meta Progression Settings**.
- `PlayerProfile` — serializable persistent player data: `essenceTotal`, `essenceSpent`, `metaUpgrades[]` (array of `MetaUpgradeState`). Currently tracks: Charge Per Ball (additive, capped) and Essence Gain (multiplicative, capped). Discarded/loaded by `PlayerProfileManager`.
- `PlayerProfileManager` (MonoBehaviour, DontDestroyOnLoad) — singleton managing save/load. Loads profile from JSON in `persistentDataPath` on Awake. `GrantEssence(int)` credits the player and saves. `GetOrCreateMetaUpgrade(upgradeId)` initializes or returns a meta upgrade state. Flow: `RunManager.EndRun()` → `GrantEssenceReward()` (calculates based on floors, difficulty, meta bonuses) → `PlayerProfileManager.GrantEssence()` → saves to disk.
- Meta upgrade application: `RunManager.StartNewRun()` → `ApplyMetaUpgradesToRunStats()` reads `PlayerProfileManager.Profile.metaUpgrades[]` and mutates `RuntimeStats` before the run starts. Ball Speed Reduction is applied to `ballSpeedMult` in `LoadCurrentNode`. Essence Gain multiplier is applied during essence reward calculation. Draft Reroll count is accessible via `GetDraftRerollCount()`.
- `MetaShopUI` — displays the meta shop screen (player essence balance + 4 upgrades). Dynamically instantiates `MetaShopUpgradeCard` for each upgrade in `MetaProgressionSettings`. Fades in/out on show/hide. Wired to a button on the main menu.
- `MetaShopUpgradeCard` — individual upgrade card showing: icon, name, description, progress bar (current rank / max ranks), cost of next rank, buy button (disabled if maxed or insufficient essence). Calls `PlayerProfileManager.PurchaseUpgrade()` on purchase.

---

## File Structure

```
Assets/
├── Animations/
│   └── Yuumi_AnimationController.controller
├── Data/
│   └── Levels/
│       ├── Level_01.asset
│       └── Level_02.asset
├── Scenes/
│   ├── Main Menu.unity
│   └── Game.unity                     ← single persistent gameplay scene
├── Scripts/
│   ├── BallChain/
│   │   ├── Ball.cs
│   │   ├── BallChainManager.cs
│   │   ├── BallColor.cs               ← enum, namespace YuumisProwl
│   │   ├── BallDestructionEffect.cs   ← wired in via MatchEffectPlayer
│   │   ├── BallNode.cs                ← carries smoothShift + worldOffset
│   │   ├── BallPowerUpType.cs         ← enum, namespace YuumisProwl
│   │   ├── BallSpawner.cs
│   │   ├── ChainSegment.cs
│   │   ├── MatchDetector.cs
│   │   ├── MatchProcessor.cs          ← in BallChain/, not Managers/
│   │   ├── Obstacle.cs
│   │   └── PathController.cs
│   ├── MainMenu/
│   │   └── MainMenu.cs
│   ├── Managers/
│   │   ├── GameManager.cs
│   │   ├── LevelData.cs               ← namespace YuumisProwl (root)
│   │   ├── LevelManager.cs            ← map loader
│   │   └── Map.cs                     ← component placed on each map prefab
│   ├── Player/
│   │   └── YuumiController.cs
│   ├── Progression/
│   │   ├── RunManager.cs               ← orchestrates the run meta-loop
│   │   ├── RunConfig.cs                ← run structure + difficulty curves
│   │   ├── RunState.cs                 ← in-run mutable state
│   │   ├── RunNode.cs                  ← defines a run step (Gameplay or Shop)
│   │   ├── RuntimeStats.cs             ← per-run mutable stats wrapper
│   │   ├── UpgradeDefinition.cs        ← defines an in-run upgrade
│   │   ├── UpgradeDraftUI.cs           ← UI for selecting upgrades after wins
│   │   ├── MetaProgressionSettings.cs  ← tuning for essence rewards + meta upgrades
│   │   ├── MetaUpgradeConfig.cs        ← (nested in MetaProgressionSettings) defines cost, ranks, progression
│   │   ├── PlayerProfile.cs            ← serializable persistent player data
│   │   ├── PlayerProfileManager.cs     ← save/load + essence grant + upgrade purchase
│   │   ├── MetaShopUI.cs               ← main meta shop screen controller
│   │   └── MetaShopUpgradeCard.cs      ← single upgrade card with progress bar + buy button
│   ├── PowerUps/
│   │   ├── PowerUpChargeTracker.cs
│   │   ├── PowerUpInventory.cs
│   │   ├── PowerUpSettings.cs
│   │   ├── PowerUpSpawner.cs
│   │   ├── PowerUpType.cs
│   │   └── UI/
│   │       ├── PowerUpIconDatabase.cs
│   │       └── PowerUpUIController.cs
│   ├── Projectile/
│   │   ├── Projectile.cs
│   │   └── ProjectileSpawner.cs
│   ├── Utilities/
│   │   ├── BallColorUtils.cs          ← namespace YuumisProwl
│   │   └── ObjectPool.cs
│   └── VFX/
│       ├── ComboPopup.cs
│       └── MatchEffectPlayer.cs
└── Prefabs/
    ├── Balls/
    │   └── Ball.prefab
    ├── Maps/                          ← one prefab per map (PathController + obstacles + decor)
    ├── Projectiles/
    │   └── Projectile.prefab
    └── VFX/                           ← BallDestructionEffect.prefab, ComboPopup.prefab
```

---

## Key Conventions

### Naming

| Type | Convention | Example |
|---|---|---|
| Classes | PascalCase | `BallChainManager` |
| Methods | PascalCase | `InsertBall()` |
| Private / serialized fields | camelCase | `ballSpeed` |
| Public properties | PascalCase | `BallSpeed` |
| Constants | UPPER_SNAKE_CASE | `MIN_MATCH_COUNT` |

### Input Pattern
All input handling uses `#if UNITY_EDITOR || UNITY_STANDALONE` / `#else` guards so mouse works in editor and touch works on device. Always guard against `EventSystem.current.IsPointerOverGameObject()` before treating input as a gameplay click — `ProjectileSpawner` does this so UI button clicks don't fire shots.

### Object Pooling
`ObjectPool<T>` is used for balls, projectiles, destruction effects, and combo popups. Never call `Destroy()` on a pooled object during gameplay — always `Return()`. Where a "did we finish" coroutine is needed (e.g. effects with a duration), the caller schedules a `WaitForSeconds` and then returns the object to its pool.

### Color Mapping
`BallColorUtils.ToUnityColor(BallColor)` is the single source of truth. Do not add per-class switch statements mapping `BallColor` to `Color` — extend `BallColorUtils` instead.

### Where settings should live
- **Constant gameplay-feel values** (destruction delay, insertion duration, ball spacing, gap-close speed) — currently fields on the relevant MonoBehaviour. A future `GameSettings` ScriptableObject could unify these.
- **Per-level difficulty knobs** — `LevelData` asset, applied by `LevelManager` on map load.
- **Category-specific tunables** (power-ups) — dedicated SO like `PowerUpSettings`.

---

## Common Tasks

### Add a new ball color
1. Add the value to the `BallColor` enum in `BallColor.cs`.
2. Add a case to `BallColorUtils.ToUnityColor()` in `BallColorUtils.cs`.
3. Update `LevelData.colorCount` range if needed (`[Range(1, 6)]` currently caps at 6).

### Create a new map (gameplay layout)
1. In `Game.unity` (or any scratch scene), create an empty GameObject `Map_XX` at the scene root.
2. Add the `Map` component to it.
3. Add a child GameObject with `SplineContainer` + `PathController` (PathController has `RequireComponent<SplineContainer>`). Sculpt the spline.
4. Add child GameObjects for obstacles (each with a Collider + the `Obstacle` component) and any decorations.
5. On `Map`, drag the child `PathController` into **Path Controller** and a `LevelData` asset into **Level Data**.
6. Drag the `Map_XX` GameObject into `Assets/Prefabs/Maps/` to create the prefab; then delete it from the scene.
7. On `LevelManager` in `Game.unity`, add the new prefab to **Map Prefabs**.

### Create a new `LevelData` asset
1. Right-click in `Assets/Data/Levels/` → **Create → Yuumi → Level Data**.
2. Set `levelName`, `totalBalls`, `colorCount`, `ballSpeed`.
3. Reference it from a Map prefab's `Map` component (not from any scene directly).

### Add an obstacle
1. Add a GameObject inside a map prefab.
2. Add any Collider (Box, Sphere, Mesh — shape is free).
3. Add the `Obstacle` component.
4. Position it on or near the path — projectiles that hit it are discarded and respawned.

### Add a match VFX layer
1. Edit the `BallDestructionEffect.prefab` in `Assets/Prefabs/VFX/`.
2. Add new child `ParticleSystem`s — they're auto-collected on Awake (or assign them explicitly via the **Particle Effects** array).
3. If you don't want a particular system tinted by the match color, uncheck **Tint Particles** on that prefab (it's all-or-nothing currently — split into multiple prefabs if you need mixed behavior).
4. Make sure `effectDuration` (on the prefab) is ≥ the longest-lived particle's lifetime but ≤ `MatchEffectPlayer.ReturnEffectAfter`'s wait (currently 1.2s).

### Create an in-run upgrade
1. Right-click in `Assets/Data/` → **Yuumi → Upgrade Definition**. Name it e.g. `Upgrade_RotationSpeed`.
2. Set **Upgrade Name**, **Description**, optional **Icon**, and **Modifier Value**.
   - For *multiplicative* upgrades (Speed, Width), use values like `1.2` (20% increase) or `1.5` (50% increase).
   - For *additive* upgrades (Charge, Bomb Radius), use raw deltas like `2` or `0.5`.
3. Set **Stat** (the enum selecting which stat to modify: `YuumiRotationSpeed`, `ChargePerBall`, `PierceWidth`, or `BombRadius`).
4. Add the asset to **RunManager**'s **Upgrade Pool** array in the inspector (the pool to draft from each level).

---

## Not Yet Implemented

- **Step 6b:** In-run shop screen and shop node placement in runs (meta shop UI is complete; in-run shop TBD).
- **Step 7:** Color synergy infrastructure (color weights, match hooks).
- **Step 8:** Fire-and-forget homing: projectile auto-targets nearby color matches after launch.
- Per-map intro animations (Animator hook designed but not built — would live on each Map prefab and be played by `LevelManager.LoadMapRoutine` before `BallSpawner.StartLevel`).
- Sound effects and music (`BallDestructionEffect` has audio support but no clips wired up).
- Settings/Options menu.
- MainMenu Play button (currently auto-loads `Game.unity` in `Start`).
- A unified `GameSettings` ScriptableObject for cross-level constants (destruction delay, insertion duration, ball spacing).

---

## Troubleshooting

**Balls don't spawn / can't fire after pressing Play**
The most common cause is that `LevelManager.mapPrefabs` is empty or a Map's `PathController` reference is unassigned. Check the Console — `LevelManager: No map prefabs assigned!` or a `NullReferenceException` in `BallChainManager.SpawnBall` (pool not yet initialized) tells you which.

**Balls overlap after insertion**
Check `PushSegmentBallsForward` / `PushSegmentBallsBackward` in `BallChainManager`; verify `ballSpacing` matches the visual ball radius.

**Retreat win triggers immediately at start**
`holeProgress` may be > 0 while intro balls haven't reached it yet. Verify `holeProgress` is set correctly and that `BallSpawner.IsPlayingIntro` is true during the intro.

**Inserted ball clips into the chain**
The smooth insertion uses `BallNode.worldOffset` so the new ball slides in from the projectile's actual world position. If clipping returns, check that `Projectile` is passing `transform.position` into `BallChainManager.InsertBallAtProgress(color, progress, projectileWorldPos)`. The decay rate is `ballSpacing / insertionDuration` per second.

**Cascade matches fire too early (before the chain closes)**
Cascade detection happens at segment merge boundaries, not on a timer. If they fire prematurely, the bug is likely in `BallChainManager.MergeTouchingSegments` — verify it only merges when `(ahead.Tail.pathProgress - behind.Lead.pathProgress) <= mergeTolerance`.

**Projectile doesn't detect obstacle**
The `Obstacle` component must be on the same GameObject as the Collider (or a parent with `GetComponent` reach). No tag is needed — detection is by component.

**Shooting blocked unexpectedly**
Check the two guards in `ProjectileSpawner.HandleInput`: `BallSpawner.IsPlayingIntro` and `LevelManager.IsTransitioning`. Also check `projectileInFlight` — only one projectile can be in flight at a time. Note: `MatchProcessor.IsProcessingMatches` is **no longer** a block — concurrent sequences let the player shoot during gap closing.

**Combo popup looks far away from the match position**
`MatchEffectPlayer` positions the popup at the centroid of the destroyed balls. If it visually appears offset, check the `ComboPopup` prefab's `TMP_Text` Alignment (should be Center / Middle) and that its child has zero local offset with a `(0.5, 0.5)` pivot.

**Match VFX doesn't play at all**
On `MatchProcessor`, verify **Enable Destruction Effects** is on. On the scene's `MatchEffectPlayer`, verify both prefabs are assigned and **Match Processor** is wired. The event fires *before* `RemoveBalls`, so a null reference inside the handler can break the match flow — keep handler logic resilient.

**MatchProcessor not found in Managers/**
`MatchProcessor.cs` lives in `Assets/Scripts/BallChain/`, namespace `YuumisProwl.BallChain`. It is NOT in the Managers folder.

---

## Build Checklist

**Android:**
- Minimum API level 24 (Android 7.0)
- IL2CPP backend, ARM64 architecture
- Texture compression: ASTC
- Only `Main Menu` and `Game` scenes need to be in Build Settings — there are no transition scenes.

**iOS:**
- Minimum iOS 12.0
- Build to Xcode, then archive from Xcode
