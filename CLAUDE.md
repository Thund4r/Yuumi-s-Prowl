# Yuumi's Prowl - Project Documentation

> **AI-Assisted Development Guide**  
> This document helps Claude (and human developers) understand the project structure, conventions, and systems to provide better assistance throughout development.

---

## 📋 Project Overview

### What We're Building
**Yuumi's Prowl** is a mobile 2D puzzle game combining Zuma-style ball-chain mechanics with League of Legends' Yuumi character and her signature homing projectile ability (Q - Prowling Projectile).

### Core Gameplay Loop
1. Colored balls move along a curved path toward the end point
2. Player taps screen to shoot Yuumi's homing projectile
3. Projectile follows player's finger/touch position
4. On collision, projectile inserts into the ball chain
5. 3+ consecutive matching colors are destroyed
6. Clear all balls to win; balls reaching the end = lose

### Technical Stack
- **Engine**: Unity 2022.3.62f3 LTS
- **Language**: C#
- **Platform**: Mobile (Android/iOS)
- **Target Performance**: 60 FPS on mid-range devices
- **Development Timeline**: 5 weeks (hackathon pace)
- **Team**: 2 developers (1 solo during weeks 2-3)

---

## 🏗️ Architecture Overview

### System Design Philosophy
This project uses a **modular manager pattern** where independent systems communicate through clearly defined interfaces. Each system has a single responsibility and minimal coupling.

### Core Systems (✅ = Implemented)

```
┌─────────────────────────────────────────────────────┐
│              Game Manager ✅                         │
│  (Orchestrates game state, scoring, level flow)     │
└──────────────┬──────────────────────────────────────┘
               │
      ┌────────┴────────┐
      │                 │
┌─────▼─────┐     ┌────▼──────┐
│   Level   │     │    UI     │
│  Manager  │     │  Manager  │
│  (TODO)   │     │  (TODO)   │
└───────────┘     └───────────┘

┌─────────────────────────────────────────────────────┐
│           Ball Chain System ✅                       │
│  ┌──────────────┬──────────────┐                    │
│  │ Path         │ Chain        │                    │
│  │ Controller ✅│ Manager ✅   │                    │
│  └──────────────┴──────────────┘                    │
│  ┌──────────────┬──────────────┐                    │
│  │ Match        │ Ball         │                    │
│  │ Detector ✅  │ Spawner ✅   │                    │
│  └──────────────┴──────────────┘                    │
└─────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────┐
│           Projectile System ✅                       │
│  ┌──────────────┬──────────────┐                    │
│  │ Spawner ✅   │ Homing ✅    │                    │
│  │              │ Controller   │                    │
│  └──────────────┴──────────────┘                    │
└─────────────────────────────────────────────────────┘
```

### Key Design Patterns

**Object Pooling** (Critical for mobile performance)
- Balls and projectiles are pooled, never destroyed/instantiated during gameplay
- Reduces garbage collection and frame stutters

**State Machine** (Game flow)
- States: MainMenu, Playing, Paused, Won, Lost
- Clean transitions, prevents conflicting inputs

**Observer Pattern** (Event system)
- Ball destruction triggers score update
- Level complete triggers win screen
- Avoids tight coupling between systems

---

## ✅ Implemented Systems Status

### Complete & Functional:
1. ✅ **Ball Path System** - Balls move smoothly along curved paths
2. ✅ **Projectile System** - Homing projectiles follow cursor/touch
3. ✅ **Collision Detection** - Projectiles insert into ball chain on hit
4. ✅ **Match Detection** - Detects 3+ consecutive matching colors
5. ✅ **Ball Destruction** - Removes matched balls with gap closing animation
6. ✅ **Cascade Matching** - Checks for new matches after gap closes
7. ✅ **Scoring System** - Points with combo multipliers
8. ✅ **Win/Lose Conditions** - Chain cleared = Win, Ball reaches end = Lose
9. ✅ **Object Pooling** - All balls and projectiles pooled for performance
10. ✅ **Event System** - Decoupled communication between systems

### Not Yet Implemented:
- ❌ UI System (score display, health, level select)
- ❌ Power-ups
- ❌ Sound effects
- ❌ Particle effects (basic system ready, not integrated)
- ❌ Level progression
- ❌ Menu system

See `GAME_LOGIC_SUMMARY.md` for detailed implementation documentation.

## 💻 Development Standards

### Code Style Conventions

```csharp
// ✅ GOOD: Clear, descriptive names
public class BallChainManager : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float ballSpeed = 2f;
    [SerializeField] private float ballSpacing = 0.5f;
    
    private List<Ball> activeBalls = new List<Ball>();
    private PathController pathController;
    
    public void InsertBall(Ball newBall, int index)
    {
        // Implementation
    }
}

// ❌ BAD: Unclear names, poor organization
public class BallManager : MonoBehaviour
{
    public float s = 2f;
    public float sp = 0.5f;
    List<Ball> balls = new List<Ball>();
    
    public void Insert(Ball b, int i) { }
}
```

### Naming Conventions

| Type | Convention | Example |
|------|-----------|---------|
| Classes | PascalCase | `BallChainManager` |
| Methods | PascalCase | `InsertBall()` |
| Private Fields | camelCase with _ prefix | `_ballSpeed` or `ballSpeed` |
| Public Fields | PascalCase | `BallSpeed` |
| Serialized Fields | camelCase | `ballSpeed` |
| Constants | UPPER_SNAKE_CASE | `MAX_BALLS_IN_CHAIN` |
| GameObjects | PascalCase with underscores | `Ball_Red`, `PathPoint_0` |
| Prefabs | PascalCase | `BallPrefab`, `ProjectilePrefab` |

### File Organization

```
Assets/
├── Scenes/
│   ├── MainMenu.unity
│   ├── GameScene.unity
│   └── TestScene.unity
├── Scripts/
│   ├── Managers/
│   │   ├── GameManager.cs
│   │   ├── LevelManager.cs
│   │   ├── UIManager.cs
│   │   └── AudioManager.cs
│   ├── BallChain/
│   │   ├── Ball.cs
│   │   ├── BallChainManager.cs
│   │   ├── PathController.cs
│   │   ├── MatchDetector.cs
│   │   └── BallSpawner.cs
│   ├── Projectile/
│   │   ├── Projectile.cs
│   │   ├── ProjectileSpawner.cs
│   │   └── HomingController.cs
│   ├── Player/
│   │   ├── CannonController.cs
│   │   └── InputManager.cs
│   └── Utilities/
│       ├── ObjectPool.cs
│       └── GameEvents.cs
├── Prefabs/
│   ├── Balls/
│   ├── Projectiles/
│   └── VFX/
├── Sprites/
├── Audio/
└── Data/
    ├── Levels/
    └── Config/
```

### Git Workflow

**Branching Strategy:**
```
main (always deployable)
├── dev (active development)
    ├── feature/ball-chain-system
    ├── feature/match-detection
    └── feature/ui-implementation
```

**Commit Guidelines:**
- Commit after each working feature
- Use descriptive commit messages
- Test before committing

```bash
# ✅ GOOD
git commit -m "Add ball insertion logic with spacing animation"
git commit -m "Fix projectile collision detection for edge cases"
git commit -m "Implement match detection with cascade support"

# ❌ BAD
git commit -m "updates"
git commit -m "fixed stuff"
git commit -m "wip"
```

**When to Commit:**
- After completing a feature (even if small)
- Before trying something risky/experimental
- At end of each work session
- Before switching to work on different system

---

## 🎮 Key Systems Documentation

### 1. Ball Chain System

**Core Concept:** Balls move along a path as a chain, maintaining fixed spacing between each ball.

**Data Structure:**
```csharp
public class BallChainManager : MonoBehaviour
{
    [System.Serializable]
    public class BallNode
    {
        public Ball ball;
        public float pathProgress; // 0-1 along path
        public int chainIndex;
    }
    
    private List<BallNode> ballChain = new List<BallNode>();
    private const float BALL_SPACING = 0.5f; // Distance between ball centers
}
```

**Movement Logic:**
```csharp
// Each ball's position is determined by its pathProgress value
void UpdateBallPositions()
{
    foreach (var node in ballChain)
    {
        node.ball.transform.position = 
            pathController.GetPointOnPath(node.pathProgress);
    }
}

// Move chain forward
void MoveChain(float deltaTime)
{
    float moveDistance = ballSpeed * deltaTime;
    
    for (int i = 0; i < ballChain.Count; i++)
    {
        ballChain[i].pathProgress += moveDistance / pathLength;
    }
}
```

**Insertion Algorithm:**
```csharp
public void InsertBall(Ball newBall, float insertProgress)
{
    // Find insertion point
    int insertIndex = FindInsertionIndex(insertProgress);
    
    // Create new node
    var newNode = new BallNode {
        ball = newBall,
        pathProgress = insertProgress,
        chainIndex = insertIndex
    };
    
    // Insert into chain
    ballChain.Insert(insertIndex, newNode);
    
    // Push subsequent balls backward to maintain spacing
    PushBallsBackward(insertIndex + 1);
    
    // Update chain indices
    UpdateChainIndices();
}

void PushBallsBackward(int startIndex)
{
    for (int i = startIndex; i < ballChain.Count; i++)
    {
        // Calculate required spacing
        float requiredProgress = 
            ballChain[i - 1].pathProgress + (BALL_SPACING / pathLength);
        
        // Only push if too close
        if (ballChain[i].pathProgress < requiredProgress)
        {
            ballChain[i].pathProgress = requiredProgress;
        }
        else
        {
            break; // Rest of chain has proper spacing
        }
    }
}
```

### 2. Match Detection System

**Algorithm:**
```csharp
public class MatchDetector
{
    public List<Ball> DetectMatches(List<BallNode> chain, int centerIndex)
    {
        var matchedBalls = new List<Ball>();
        Ball centerBall = chain[centerIndex].ball;
        
        // Expand left
        int leftIndex = centerIndex;
        while (leftIndex > 0 && 
               chain[leftIndex - 1].ball.BallColor == centerBall.BallColor)
        {
            leftIndex--;
        }
        
        // Expand right
        int rightIndex = centerIndex;
        while (rightIndex < chain.Count - 1 && 
               chain[rightIndex + 1].ball.BallColor == centerBall.BallColor)
        {
            rightIndex++;
        }
        
        // Check if match is 3 or more
        int matchCount = rightIndex - leftIndex + 1;
        if (matchCount >= 3)
        {
            for (int i = leftIndex; i <= rightIndex; i++)
            {
                matchedBalls.Add(chain[i].ball);
            }
        }
        
        return matchedBalls;
    }
    
    // After removing balls, check for cascade matches
    public List<Ball> CheckCascade(List<BallNode> chain, int gapStartIndex)
    {
        // If gap was created, check if balls on either side match
        if (gapStartIndex > 0 && gapStartIndex < chain.Count)
        {
            return DetectMatches(chain, gapStartIndex);
        }
        return new List<Ball>();
    }
}
```

### 3. Projectile System

**Homing Behavior:**
```csharp
public class Projectile : MonoBehaviour
{
    [SerializeField] private float homingSpeed = 10f;
    [SerializeField] private float rotationSpeed = 5f;
    
    private Vector3 targetPosition;
    
    void Update()
    {
        UpdateTarget();
        MoveTowardsTarget();
        RotateTowardsTarget();
    }
    
    void UpdateTarget()
    {
        #if UNITY_EDITOR
        targetPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        #else
        if (Input.touchCount > 0)
        {
            targetPosition = Camera.main.ScreenToWorldPoint(
                Input.GetTouch(0).position
            );
        }
        #endif
        targetPosition.z = 0;
    }
    
    void MoveTowardsTarget()
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        transform.position += direction * homingSpeed * Time.deltaTime;
    }
    
    void RotateTowardsTarget()
    {
        Vector3 direction = targetPosition - transform.position;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        Quaternion targetRotation = Quaternion.Euler(0, 0, angle);
        transform.rotation = Quaternion.Slerp(
            transform.rotation, 
            targetRotation, 
            rotationSpeed * Time.deltaTime
        );
    }
}
```

**Collision Detection:**
```csharp
void OnTriggerEnter2D(Collider2D collision)
{
    if (collision.CompareTag("Ball"))
    {
        Ball hitBall = collision.GetComponent<Ball>();
        
        // Find where on path we hit
        float insertProgress = hitBall.PathProgress;
        
        // Notify chain manager to insert this projectile
        BallChainManager.Instance.InsertBall(
            ConvertToChainBall(), 
            insertProgress
        );
        
        // Return projectile to pool
        ProjectilePool.ReturnToPool(this);
    }
}
```

### 4. Level System

**Level Data Structure:**
```csharp
[CreateAssetMenu(fileName = "Level", menuName = "Yuumi/Level Data")]
public class LevelData : ScriptableObject
{
    [Header("Path Configuration")]
    public Vector3[] pathPoints;
    
    [Header("Ball Sequence")]
    public BallColor[] initialBalls;
    public int ballsToSpawn = 50;
    
    [Header("Difficulty")]
    public float ballSpeed = 2f;
    public int colorCount = 4; // Number of different colors
    
    [Header("Win Condition")]
    public int targetScore = 1000;
}
```

**Loading a Level:**
```csharp
public class LevelManager : MonoBehaviour
{
    public void LoadLevel(LevelData data)
    {
        // Setup path
        pathController.InitializePath(data.pathPoints);
        
        // Configure ball chain
        ballChainManager.SetSpeed(data.ballSpeed);
        ballSpawner.SetColorCount(data.colorCount);
        
        // Spawn initial balls
        SpawnInitialBalls(data.initialBalls);
        
        // Set win condition
        GameManager.Instance.SetTargetScore(data.targetScore);
    }
}
```

---

## 📱 Mobile-Specific Considerations

### Touch Input Best Practices

**DO:**
```csharp
// Handle both touch and mouse for testing
void HandleInput()
{
    Vector3 inputPosition = Vector3.zero;
    bool hasInput = false;
    
    #if UNITY_EDITOR
    if (Input.GetMouseButton(0))
    {
        inputPosition = Input.mousePosition;
        hasInput = true;
    }
    #else
    if (Input.touchCount > 0)
    {
        inputPosition = Input.GetTouch(0).position;
        hasInput = true;
    }
    #endif
    
    if (hasInput)
    {
        ProcessInput(inputPosition);
    }
}
```

**DON'T:**
```csharp
// Don't use Input.GetMouseButton on mobile builds
void Update()
{
    if (Input.GetMouseButton(0)) // Won't work on mobile!
    {
        // ...
    }
}
```

### Performance Optimization

**Target: 60 FPS on mid-range devices (Samsung Galaxy A series, iPhone 11)**

**Critical Optimizations:**

1. **Object Pooling** (MANDATORY)
```csharp
public class ObjectPool<T> where T : MonoBehaviour
{
    private Queue<T> pool = new Queue<T>();
    private T prefab;
    private Transform parent;
    
    public T Get()
    {
        if (pool.Count > 0)
        {
            T obj = pool.Dequeue();
            obj.gameObject.SetActive(true);
            return obj;
        }
        return GameObject.Instantiate(prefab, parent);
    }
    
    public void Return(T obj)
    {
        obj.gameObject.SetActive(false);
        pool.Enqueue(obj);
    }
}
```

2. **Reduce Update Calls**
```csharp
// ❌ BAD: Every ball updates independently
public class Ball : MonoBehaviour
{
    void Update()
    {
        // Move ball
    }
}

// ✅ GOOD: Manager updates all balls
public class BallChainManager : MonoBehaviour
{
    void Update()
    {
        foreach (var ball in activeBalls)
        {
            UpdateBall(ball);
        }
    }
}
```

3. **Sprite Atlasing**
- Combine all ball sprites into one atlas
- Reduces draw calls dramatically
- Unity Sprite Packer handles this automatically

4. **Particle System Limits**
```csharp
// Limit max particles on mobile
#if UNITY_ANDROID || UNITY_IOS
    particleSystem.maxParticles = 50;
#else
    particleSystem.maxParticles = 200;
#endif
```

### Build Process

**Android Build Checklist:**
- [ ] Set build settings to Android
- [ ] Set minimum API level to 24 (Android 7.0)
- [ ] Use IL2CPP backend (faster than Mono)
- [ ] Enable ARM64 architecture
- [ ] Set texture compression to ASTC
- [ ] Build as APK for testing, AAB for release

**iOS Build Checklist:**
- [ ] Set build settings to iOS
- [ ] Minimum iOS version: 12.0
- [ ] Add camera usage description (if using AR later)
- [ ] Set target device to iPhone + iPad
- [ ] Build to Xcode project, then build from Xcode

---

## 🛠️ Common Tasks & Solutions

### How to Add a New Ball Color

1. **Create sprite variant:**
```
Assets/Sprites/Balls/Ball_NewColor.png
```

2. **Add to BallColor enum:**
```csharp
public enum BallColor
{
    Red,
    Blue,
    Green,
    Yellow,
    Purple, // New color
}
```

3. **Add to Ball prefab:**
- Update BallColorManager script
- Add sprite to color dictionary

### How to Create a New Level

1. **Create Level Data asset:**
- Right-click in Data/Levels
- Create → Yuumi → Level Data
- Name it "Level_X"

2. **Configure in Inspector:**
- Add path points (manually or copy from existing)
- Set ball sequence
- Adjust difficulty parameters

3. **Add to LevelManager:**
```csharp
public LevelData[] levels;
// Drag new level into array in Inspector
```

### How to Add Particle Effects

1. **Create particle system:**
```csharp
public class BallDestructionEffect : MonoBehaviour
{
    public ParticleSystem destructionParticles;
    
    public void PlayAt(Vector3 position, Color ballColor)
    {
        transform.position = position;
        
        var main = destructionParticles.main;
        main.startColor = ballColor;
        
        destructionParticles.Play();
    }
}
```

2. **Pool the effects:**
```csharp
private ObjectPool<BallDestructionEffect> effectPool;

void OnBallDestroyed(Ball ball)
{
    var effect = effectPool.Get();
    effect.PlayAt(ball.transform.position, ball.Color);
    
    // Return to pool after effect completes
    StartCoroutine(ReturnEffectAfterDelay(effect, 2f));
}
```

### How to Implement Power-Ups

```csharp
public enum PowerUpType
{
    ColorBomb,  // Destroys all balls of one color
    SlowTime,   // Slows ball chain movement
    LaserShot,  // Projectile destroys everything in line
}

public class PowerUp : MonoBehaviour
{
    public PowerUpType type;
    public float duration = 5f;
    
    public void Activate()
    {
        switch (type)
        {
            case PowerUpType.ColorBomb:
                ActivateColorBomb();
                break;
            case PowerUpType.SlowTime:
                ActivateSlowTime();
                break;
            case PowerUpType.LaserShot:
                ActivateLaserShot();
                break;
        }
    }
}
```

---

## 🤖 AI Assistant Collaboration Guidelines

### How to Ask for Help Effectively

**❌ POOR:** "My code doesn't work, help"

**✅ GOOD:** 
```
"I'm implementing ball insertion in BallChainManager. When a projectile 
hits the chain, I'm inserting the ball but subsequent balls aren't 
maintaining spacing. Here's my InsertBall() method:

[paste code]

Expected: Balls should push backward to maintain 0.5f spacing
Actual: Balls overlap after insertion

I've checked that BALL_SPACING is set correctly. What am I missing?"
```

### Context to Provide When Debugging

Always include:
1. **What you're trying to do** (the goal)
2. **What's happening** (current behavior)
3. **What should happen** (expected behavior)
4. **Relevant code** (the specific method/class)
5. **What you've already tried**

### Preferred Code Generation Patterns

When asking Claude to generate code:

**Specify:**
- Unity version (2022.3.62f3 LTS)
- Target platform (mobile)
- Performance constraints (60 FPS, object pooling required)
- Integration points (which existing systems it needs to work with)

**Example:**
```
"Generate a BallSpawner class that:
- Uses object pooling (we have ObjectPool<T> utility)
- Spawns balls at pathProgress = 0
- Distributes colors based on LevelData.colorCount
- Works with existing BallChainManager
- Optimized for mobile (no per-frame allocations)"
```

---

## 🐛 Troubleshooting Guide

### Common Errors & Solutions

**Error: "NullReferenceException: Object reference not set to an instance of an object"**

Common causes:
1. Forgot to assign reference in Inspector
2. Calling method before Start()/Awake() initializes it
3. Object was destroyed but reference still exists

```csharp
// Add null checks
if (ballChainManager != null)
{
    ballChainManager.InsertBall(ball, progress);
}
else
{
    Debug.LogError("BallChainManager is null! Check Inspector assignment.");
}
```

**Error: "IndexOutOfRangeException"**

Check:
- List/Array bounds before accessing
- Chain indices after insertion/removal

```csharp
// Safe access
if (index >= 0 && index < ballChain.Count)
{
    var ball = ballChain[index];
}
```

**Issue: Projectiles don't follow finger on mobile**

Check:
1. Using Touch input, not Mouse input
2. Converting screen to world space correctly
3. Camera is Orthographic for 2D

```csharp
// Correct conversion
Vector3 touchPos = Input.GetTouch(0).position;
Vector3 worldPos = Camera.main.ScreenToWorldPoint(touchPos);
worldPos.z = 0; // Important for 2D!
```

### Performance Issues

**Problem: Frame rate drops below 30 FPS**

Debug steps:
1. Open Unity Profiler (Window → Analysis → Profiler)
2. Check which system is causing spikes
3. Common culprits:

```csharp
// ❌ Creating garbage every frame
void Update()
{
    var balls = GetAllBalls(); // Returns new List every frame!
}

// ✅ Cache and reuse
private List<Ball> cachedBalls = new List<Ball>();

void Update()
{
    GetAllBallsNonAlloc(cachedBalls);
}
```

**Problem: Game stutters when destroying balls**

Solution: Use object pooling instead of Destroy()

```csharp
// ❌ Causes GC spike
Destroy(ball.gameObject);

// ✅ Return to pool
ballPool.Return(ball);
ball.gameObject.SetActive(false);
```

### Build Errors

**Android: "Unable to find unity activity"**
- Check Player Settings → Android → Minimum API Level (should be 24+)
- Verify Android SDK path in Preferences

**iOS: "Signing requires a development team"**
- Open in Xcode
- Select project → Signing & Capabilities
- Choose your development team

**Build size too large (>150MB)**
- Remove unused assets
- Compress textures (Format: ASTC)
- Disable unused architectures
- Strip engine code (Player Settings → Strip Engine Code)

---

## 📅 Week-by-Week Development Priorities

### Week 1: Core Mechanics (CRITICAL PATH)
**Must Have:**
- ✅ Ball moves along path
- ✅ Projectile shoots and homes
- ✅ Collision detection works
- ✅ Basic insertion (even if spacing is rough)

**Nice to Have:**
- Smooth insertion animation
- Multiple balls on path

### Week 2: Match System (CRITICAL PATH)
**Must Have:**
- ✅ Detect 3+ matching balls
- ✅ Remove matched balls
- ✅ Close gaps in chain
- ✅ Basic scoring

**Nice to Have:**
- Cascade matching
- Combo multipliers

### Week 3: Game Feel
**Must Have:**
- ✅ Particle effects on match
- ✅ Sound effects
- ✅ Basic UI (score, level)

**Nice to Have:**
- Screen shake
- Advanced animations
- Background music

### Week 4: Levels & Content
**Must Have:**
- ✅ 3-5 playable levels
- ✅ Level progression
- ✅ Win/lose screens
- ✅ Main menu

**Nice to Have:**
- Different path shapes
- Obstacles
- More levels

### Week 5: Polish & Demo
**Must Have:**
- ✅ Bug-free demo build
- ✅ 60 FPS on test devices
- ✅ Tutorial/instructions
- ✅ Presentation-ready

**Nice to Have:**
- Power-ups
- Achievements
- Advanced visual polish

---

## ✅ Testing Guidelines

### After Each Feature

**Ball Chain System:**
- [ ] Balls maintain exact spacing
- [ ] No balls overlap
- [ ] Chain moves smoothly at set speed
- [ ] Path endpoints work correctly

**Projectile System:**
- [ ] Projectile spawns at correct position
- [ ] Follows finger accurately
- [ ] Collision detection is reliable
- [ ] No memory leaks (pooling works)

**Match Detection:**
- [ ] Detects exactly 3+ matches (not 2)
- [ ] Handles edge cases (start/end of chain)
- [ ] Cascades work correctly
- [ ] No false positives

### Mobile Testing Checklist

**Pre-Build:**
- [ ] No compiler errors
- [ ] All scenes added to build settings
- [ ] Touch input code active (not mouse-only)

**On-Device:**
- [ ] Touch input responsive
- [ ] Frame rate steady (use FPS counter)
- [ ] No overheating after 5 min play
- [ ] Screen orientation locked
- [ ] UI elements visible on phone screen
- [ ] Audio plays correctly

**Performance Benchmarks:**
- [ ] 60 FPS with 50 balls on screen
- [ ] No frame drops when destroying 10+ balls
- [ ] Smooth particle effects
- [ ] Build size under 100MB

### Device Testing Priority

**Must test on:**
1. Your primary development phone
2. Oldest device you have access to
3. One iPhone (if targeting iOS)

**Test scenarios:**
- Play for 5 continuous minutes
- Rapid-fire projectiles (stress test)
- Multiple quick matches (particle stress)
- Background app and return
- Low battery mode

---

## 🎯 Success Metrics

### Minimum Viable Demo (End of Week 5)
- [ ] 3 complete, bug-free levels
- [ ] Core gameplay loop feels good
- [ ] Runs at 60 FPS on test device
- [ ] Has win/lose conditions
- [ ] Basic audio feedback
- [ ] 5-minute demo script prepared

### Stretch Goals (If Time Permits)
- [ ] 5+ levels with variety
- [ ] Power-ups implemented
- [ ] Leaderboard/high scores
- [ ] Polished menu system
- [ ] Advanced particle effects
- [ ] Yuumi voice lines/animations

---

## 📚 Quick Reference

### Essential Unity Shortcuts
- `Ctrl + S` - Save
- `Ctrl + P` - Play mode
- `F` - Frame selected object
- `Ctrl + D` - Duplicate
- `Ctrl + Shift + F` - Move to scene view

### Git Commands
```bash
git status                    # Check what changed
git add .                     # Stage all changes
git commit -m "message"       # Commit
git push                      # Push to remote
git pull                      # Pull latest
git stash                     # Temporarily store changes
git stash pop                 # Restore stashed changes
```

### Performance Profiling
```csharp
// Measure execution time
float startTime = Time.realtimeSinceStartup;
YourFunction();
float duration = Time.realtimeSinceStartup - startTime;
Debug.Log($"Function took {duration * 1000}ms");
```

---

## 🚀 Getting Started

1. Read Project Overview
2. Review Architecture Overview
3. Check current Week priorities
4. Review relevant System Documentation
5. Write code
6. Test frequently
7. Commit when working

**When stuck:** Check Troubleshooting Guide → Ask teammate → Ask Claude with proper context

**Remember:** Progress over perfection. A working game with basic graphics beats a beautiful game that crashes.

Good luck! 🎮✨