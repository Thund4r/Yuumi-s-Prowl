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
4. 3+ consecutive matching colors are destroyed; cascade matches are checked after each removal
5. Chain recoils backward on each match
6. **Win:** chain cleared entirely via matches, OR all balls retreat back into the hole via recoil
7. **Lose:** lead ball reaches the end of the path

### Technical Stack
- **Engine:** Unity 2022.3.62f3 LTS
- **Language:** C#
- **Platform:** Mobile (Android/iOS), tested in editor with mouse
- **Target Performance:** 60 FPS on mid-range devices
- **Physics:** 3D colliders used in XY plane (not 2D physics)

---

## Architecture Overview

Systems communicate through events and direct serialized references. No singletons — all references are assigned in the Inspector or found via `FindObjectOfType` as a fallback.

```
┌──────────────────────────────────────────────────────────┐
│  LevelManager                                            │
│  Applies LevelData settings in Awake, subscribes to     │
│  GameManager win/lose events, drives scene transitions   │
└────────────────────┬─────────────────────────────────────┘
                     │
          ┌──────────▼──────────┐
          │     GameManager     │
          │  Win/lose state,    │
          │  retreat detection  │
          └──────────┬──────────┘
                     │ events
        ┌────────────┼────────────┐
        │            │            │
┌───────▼──────┐ ┌───▼────────┐ ┌▼──────────────┐
│ BallChain    │ │  Match     │ │  Projectile   │
│ System       │ │  Processor │ │  System       │
└──────────────┘ └────────────┘ └───────────────┘
```

### Namespaces

| Namespace | Contents |
|---|---|
| `YuumisProwl` | `BallColor`, `BallColorUtils`, `LevelData`, `LevelTransitionData` |
| `YuumisProwl.BallChain` | `Ball`, `BallNode`, `BallChainManager`, `BallSpawner`, `PathController`, `MatchDetector`, `MatchProcessor`, `BallDestructionEffect`, `Obstacle` |
| `YuumisProwl.Projectile` | `Projectile`, `ProjectileSpawner` |
| `YuumisProwl.Managers` | `GameManager`, `LevelManager`, `TransitionController` |
| `YuumisProwl.Player` | `YuumiController` |
| `YuumisProwl.Utilities` | `ObjectPool<T>` |

---

## Implemented Systems

### Ball Chain System
- `BallNode` — plain data class holding `ball`, `pathProgress` (0–1 along spline), `chainIndex`
- `BallChainManager` — moves chain each frame; handles insertion/removal; maintains spacing via `PushBallsForward`/`PushBallsBackward`; controls visibility relative to `holeProgress`; delegates tail spawning via `NeedsTailBall()`; exposes `SetMoving(bool)`, `SetSpeed(float)`, `ClearChain()`, `HasVisibleBalls()`
- `PathController` — wraps Unity `SplineContainer`; provides `GetPointOnPath(float)`, `GetDirectionOnPath(float)`, `GetPathLength()`
- `BallSpawner` — plays intro animation (AnimationCurve-driven), then feeds tail balls up to `totalBallsToSpawn`; exposes `AllBallsSpawned`, `BallsRemaining`, `IsPlayingIntro`, `StartLevel()`; `GetRandomColor` avoids 3-in-a-row spawns
- `MatchDetector` — pure (non-MonoBehaviour) class; detects 3+ consecutive color matches and cascade matches
- `MatchProcessor` — MonoBehaviour in `BallChain/`; owns the match pipeline coroutine (check → destroy → close gap → recoil → cascade check); fires `OnBallsDestroyed(int count, BallColor)` and `OnChainCleared`; `ApplyChainRecoil(float distance)` is **public** and designed to be called externally (e.g. by power-ups)
- `BallColorUtils` — static utility in root namespace; single source of `ToUnityColor(BallColor)` used by both `Ball` and `Projectile`

### Projectile System
- `Projectile` — homing projectile that continuously tracks cursor/touch after launch; detects `Ball` tag (inserts into chain) and `Obstacle` component (discards self); uses instanced material per projectile
- `ProjectileSpawner` — pools projectiles; enforces one projectile in flight at a time (`projectileInFlight` flag); blocks shooting during intro (`BallSpawner.IsPlayingIntro`), during level transition (`LevelManager.IsTransitioning`), and while `MatchProcessor.IsProcessingMatches`; fires `OnShot` event; pre-loads next color while current projectile is in flight

### Player
- `YuumiController` — rotates Yuumi's GameObject to face cursor/touch using `Mathf.MoveTowardsAngle`; subscribes to `ProjectileSpawner.OnShot` and triggers an Animator `Throw` trigger; `rotationOffset` adjusts for sprite orientation

### Obstacle System
- `Obstacle` — empty marker component; place on any scene GameObject with any Collider; detected by `Projectile.OnTriggerEnter` via `GetComponent<Obstacle>()`; no tag required

### Level & Transition System
- `LevelData` (ScriptableObject, namespace `YuumisProwl`) — `totalBalls`, `colorCount`, `ballSpeed`, `nextSceneName`, `retrySceneName`
- `LevelManager` — applies `LevelData` in `Awake` (before `BallSpawner.Start`); subscribes to `GameManager.OnGameWon/OnGameLost`; on win: stores destination in `LevelTransitionData`, loads transition scene; on lose: reloads current scene or `retrySceneName`; exposes `IsTransitioning`
- `LevelTransitionData` — static (non-MonoBehaviour) class; passes `NextSceneName` across the scene load without DontDestroyOnLoad
- `TransitionController` — lives in the transition scene; immediately starts `LoadSceneAsync`; waits for Animator state by name (`animationStateName`), then activates once both animation (`normalizedTime >= 1`) AND load (`progress >= 0.9`) are complete

### Game State
- `GameManager` — no score tracking; fires `OnGameWon` / `OnGameLost`
  - **Win 1:** `MatchProcessor.OnChainCleared` fires (chain empty via matches — tail spawning stops when chain is empty, so AllBallsSpawned is irrelevant here)
  - **Win 2:** `BallCount > 0` AND `!HasVisibleBalls()` AND intro not playing (all balls retreated into hole)
  - **Lose:** `BallChainManager.OnBallReachedEnd`

---

## File Structure

```
Assets/
├── Animations/
│   ├── Yuumi_AnimationController.controller
│   ├── Transition_AnimationController.controller
│   └── Level 1 - 2 Transition.anim
├── Data/
│   └── Levels/
│       ├── Level_01.asset
│       └── Level_02.asset
├── Scenes/
│   ├── Level 1.unity
│   ├── Level 2.unity
│   └── Level 1 - 2 Transition.unity   ← transition scene between L1 and L2
├── Scripts/
│   ├── BallChain/
│   │   ├── Ball.cs
│   │   ├── BallChainManager.cs
│   │   ├── BallColor.cs               ← enum, namespace YuumisProwl
│   │   ├── BallDestructionEffect.cs   ← exists but not yet wired in
│   │   ├── BallNode.cs
│   │   ├── BallSpawner.cs
│   │   ├── MatchDetector.cs
│   │   ├── MatchProcessor.cs          ← in BallChain/, not Managers/
│   │   ├── Obstacle.cs
│   │   └── PathController.cs
│   ├── Managers/
│   │   ├── GameManager.cs
│   │   ├── LevelData.cs               ← namespace YuumisProwl (root)
│   │   ├── LevelManager.cs
│   │   └── TransitionController.cs
│   ├── Player/
│   │   └── YuumiController.cs
│   ├── Projectile/
│   │   ├── Projectile.cs
│   │   └── ProjectileSpawner.cs
│   └── Utilities/
│       ├── BallColorUtils.cs          ← namespace YuumisProwl
│       ├── LevelTransitionData.cs     ← static, namespace YuumisProwl
│       └── ObjectPool.cs
└── Prefabs/
    ├── Balls/
    │   └── Ball.prefab
    └── Projectiles/
        └── Projectile.prefab
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
All input handling uses `#if UNITY_EDITOR || UNITY_STANDALONE` / `#else` guards so mouse works in editor and touch works on device. Never use `Input.GetMouseButton` in a mobile build path.

### Object Pooling
`ObjectPool<T>` is used for all balls and projectiles. Never call `Destroy()` on a ball or projectile during gameplay — always return to pool. `Ball.OnReturnToPool()` and `Projectile.OnReturnToPool()` must be called before `Return()`.

### Color Mapping
`BallColorUtils.ToUnityColor(BallColor)` is the single source of truth. Do not add per-class switch statements mapping `BallColor` to `Color` — extend `BallColorUtils` instead.

---

## Common Tasks

### Add a new ball color
1. Add the value to the `BallColor` enum in `BallColor.cs`
2. Add a case to `BallColorUtils.ToUnityColor()` in `BallColorUtils.cs`
3. Update `LevelData.colorCount` range if needed (`[Range(1, 6)]` currently caps at 6)

### Create a new level
1. Right-click in `Assets/Data/Levels/` → **Create → Yuumi → Level Data**
2. Set `levelName`, `totalBalls`, `colorCount`, `ballSpeed`
3. Set `nextSceneName` to the next level's scene name (or leave empty if final)
4. Set `retrySceneName` to reload a different scene on lose (or leave empty to reload self)
5. In your level scene's `LevelManager`, assign this asset to **Level Data**

### Create a new level scene
1. Duplicate an existing level scene
2. Rearrange the `SplineContainer` path and obstacle positions
3. Assign the correct `LevelData` asset to `LevelManager`
4. Add the scene to **File → Build Settings**

### Add an obstacle
1. Create a GameObject in the scene
2. Add any Collider (Box, Sphere, Mesh — any shape works)
3. Add the `Obstacle` component
4. Position it on or near the path — projectiles that hit it are discarded and respawned

### Add the transition scene
1. Create a new scene (name must match `LevelManager.transitionSceneName`, default `"Transition"`)
2. Add your animated Yuumi character and camera
3. Add an empty GameObject with `TransitionController`
4. Assign the Animator and set **Animation State Name** to match your Animator state exactly (case-sensitive)
5. Add the scene to Build Settings

---

## Power-Ups (In Progress — `power-ups` branch)

Power-ups are the next feature to implement. The codebase already has one deliberate hook for them:

- `MatchProcessor.ApplyChainRecoil(float distance)` is **public** and documented as externally callable — use this for any power-up that pushes the chain back.

### Design considerations
- Power-ups will likely be triggered via the projectile system (shoot a special projectile) or via a separate UI button
- `ProjectileSpawner` currently enforces one projectile in flight at a time; multi-projectile power-ups would need to bypass `projectileInFlight`
- `BallChainManager.SetSpeed(float)` can be called at runtime for a slow-down power-up
- `BallChainManager.GetBallChain()` returns the live list — power-ups that modify colors directly can call `Ball.Initialize(BallColor)` on individual nodes
- New power-up scripts should go in `Assets/Scripts/PowerUps/` with namespace `YuumisProwl.PowerUps`

---

## Not Yet Implemented

- UI system (in-game HUD, win/lose screens, menus)
- Sound effects and music
- Particle effects on ball destruction (`BallDestructionEffect` class exists, not wired into `MatchProcessor`)
- Power-ups (branch: `power-ups`)

---

## Troubleshooting

**Balls overlap after insertion**
Check `PushBallsForward` and `PushBallsBackward` in `BallChainManager`; verify `ballSpacing` matches the visual ball radius.

**Retreat win triggers immediately at start**
`holeProgress` may be > 0 while intro balls haven't reached it yet. Verify `holeProgress` is set correctly and that `BallSpawner.IsPlayingIntro` is true during the intro.

**Transition scene loads but animation doesn't complete**
`TransitionController.animationStateName` must exactly match the Animator state name. Check the Animator window — the state name is case-sensitive.

**Projectile doesn't detect obstacle**
The `Obstacle` component must be on the same GameObject as the Collider (or a parent with `GetComponent` reach). No tag is needed — detection is by component.

**Shooting blocked unexpectedly**
Check all three guards in `ProjectileSpawner.HandleInput`: `BallSpawner.IsPlayingIntro`, `LevelManager.IsTransitioning`, and `MatchProcessor.IsProcessingMatches`. Also check `projectileInFlight` — only one projectile can be in flight at a time.

**MatchProcessor not found in Managers/**
`MatchProcessor.cs` lives in `Assets/Scripts/BallChain/`, namespace `YuumisProwl.BallChain`. It is NOT in the Managers folder.

---

## Build Checklist

**Android:**
- Minimum API level 24 (Android 7.0)
- IL2CPP backend, ARM64 architecture
- Texture compression: ASTC
- All scenes added to Build Settings

**iOS:**
- Minimum iOS 12.0
- Build to Xcode, then archive from Xcode
