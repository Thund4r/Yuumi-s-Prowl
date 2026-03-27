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
6. **Win:** chain cleared entirely, OR all balls retreat back into the hole via recoil
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
| `YuumisProwl.BallChain` | `Ball`, `BallNode`, `BallChainManager`, `BallSpawner`, `PathController`, `MatchDetector`, `BallDestructionEffect`, `Obstacle` |
| `YuumisProwl.Projectile` | `Projectile`, `ProjectileSpawner` |
| `YuumisProwl.Managers` | `GameManager`, `LevelManager`, `MatchProcessor`, `TransitionController` |
| `YuumisProwl.Player` | `YuumiController` |
| `YuumisProwl.Utilities` | `ObjectPool<T>` |

---

## Implemented Systems

### Ball Chain System
- `BallNode` — standalone class holding `ball`, `pathProgress` (0–1 along spline), `chainIndex`
- `BallChainManager` — moves chain, handles insertion/removal, gap closing, ball visibility relative to `holeProgress`, tail spawning delegation via `NeedsTailBall()`
- `PathController` — wraps Unity `SplineContainer`; provides `GetPointOnPath(float)` and `GetDirectionOnPath(float)`
- `BallSpawner` — plays intro animation, then feeds tail balls up to `totalBallsToSpawn`; exposes `AllBallsSpawned`, `BallsRemaining`, `IsPlayingIntro`, `StartLevel()`
- `MatchDetector` — pure class; detects 3+ consecutive color matches and cascade matches
- `MatchProcessor` — MonoBehaviour; owns the match coroutine pipeline (check → remove → close gap → recoil → cascade); fires `OnBallsDestroyed` and `OnChainCleared`
- `BallColorUtils` — static utility in root namespace; single source of `ToUnityColor(BallColor)` used by both `Ball` and `Projectile`

### Projectile System
- `Projectile` — homing projectile that tracks cursor/touch; detects `Ball` tag (inserts) and `Obstacle` component (discards self)
- `ProjectileSpawner` — pools projectiles, handles input, blocks shooting during intro (`BallSpawner.IsPlayingIntro`) and level transition (`LevelManager.IsTransitioning`); fires `OnShot` event

### Player
- `YuumiController` — rotates Yuumi's GameObject to face cursor/touch using `Mathf.MoveTowardsAngle`; subscribes to `ProjectileSpawner.OnShot` and triggers an Animator `Throw` trigger; `rotationOffset` adjusts for sprite orientation

### Obstacle System
- `Obstacle` — place on any scene GameObject with any Collider shape; detected by `Projectile.OnTriggerEnter` via `GetComponent<Obstacle>()`; no tag required

### Level & Transition System
- `LevelData` (ScriptableObject) — `totalBalls`, `colorCount`, `ballSpeed`, `nextSceneName`, `retrySceneName`
- `LevelManager` — applies `LevelData` in `Awake` (before `BallSpawner.Start`); subscribes to `GameManager.OnGameWon/OnGameLost`; on win: stores destination in `LevelTransitionData`, loads transition scene; on lose: reloads current or `retrySceneName`; exposes `IsTransitioning`
- `LevelTransitionData` — static (non-MonoBehaviour) class; passes `NextSceneName` from a level scene to the transition scene across the scene load
- `TransitionController` — lives in the transition scene; immediately starts `LoadSceneAsync` on the target level; activates the new scene once both the async load is ready (≥0.9) AND the Animator state `normalizedTime >= 1`

### Game State
- `GameManager` — no score tracking; win conditions checked via events and `Update`; fires `OnGameWon` / `OnGameLost`
  - **Win 1:** `MatchProcessor.OnChainCleared` fires AND `BallSpawner.AllBallsSpawned`
  - **Win 2:** `BallChainManager.BallCount > 0` AND `!HasVisibleBalls()` AND intro not playing (retreat into hole)
  - **Lose:** `BallChainManager.OnBallReachedEnd`

---

## File Structure

```
Assets/
├── Animations/
│   └── Yuumi_AnimationController.controller
├── Data/
│   └── Levels/              ← LevelData .asset files go here
├── Scenes/
│   ├── New Scene.unity      ← current dev scene
│   └── Transition.unity     ← transition animation scene (to create)
├── Scripts/
│   ├── BallChain/
│   │   ├── Ball.cs
│   │   ├── BallChainManager.cs
│   │   ├── BallColor.cs             ← enum, namespace YuumisProwl
│   │   ├── BallDestructionEffect.cs
│   │   ├── BallNode.cs
│   │   ├── BallSpawner.cs
│   │   ├── MatchDetector.cs
│   │   ├── Obstacle.cs
│   │   └── PathController.cs
│   ├── Managers/
│   │   ├── GameManager.cs
│   │   ├── LevelData.cs
│   │   ├── LevelManager.cs
│   │   ├── MatchProcessor.cs
│   │   └── TransitionController.cs
│   ├── Player/
│   │   └── YuumiController.cs
│   ├── Projectile/
│   │   ├── Projectile.cs
│   │   └── ProjectileSpawner.cs
│   └── Utilities/
│       ├── BallColorUtils.cs        ← namespace YuumisProwl
│       ├── LevelTransitionData.cs   ← static, namespace YuumisProwl
│       └── ObjectPool.cs
└── Prefabs/
    ├── Balls/
    └── Projectiles/
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
1. Create a new scene named `Transition` (must match `LevelManager.transitionSceneName`)
2. Add your animated Yuumi character and camera
3. Add an empty GameObject with `TransitionController`
4. Assign the Animator and set **Animation State Name** to match your Animator state
5. Add the scene to Build Settings

---

## Not Yet Implemented

- UI system (in-game HUD, win/lose screens, menus)
- Sound effects and music
- Particle effects on ball destruction (`BallDestructionEffect` class exists, not wired in)
- Power-ups

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
Check both `BallSpawner.IsPlayingIntro` and `LevelManager.IsTransitioning` — `ProjectileSpawner.HandleInput` returns early if either is true.

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
