# Testing Guide - Yuumi's Prowl

Quick guide to test all implemented game systems.

## 🎮 Basic Gameplay Test

### Setup:
1. Open Unity 2022.3.62f3
2. Open scene: `Assets/Scenes/BallPathTest.unity` or `Assets/Scenes/New Scene.unity`
3. Press **Play**

### What Should Happen:
- Balls spawn automatically at the start of the path
- Balls move forward along the curved path
- Ball spacing is maintained perfectly

## 🎯 Projectile & Collision Test

### Test Steps:
1. **Click or tap** anywhere on screen
2. A projectile should spawn
3. **Move your mouse/finger** - projectile should follow it
4. Position cursor over a ball in the chain
5. Projectile should hit the ball

### Expected Results:
- Projectile spawns at configured spawn point
- Projectile smoothly homes toward cursor
- Trail effect follows projectile
- On collision, projectile disappears
- New ball inserts into chain at hit position

### Console Output:
```
Spawned projectile - Color: Red
Projectile hit ball! Inserting Red at progress 0.45
Inserted ball at progress 0.45 - Color: Red
```

## 🔥 Match Detection Test

### Simple Match (3 Balls):
1. Watch the auto-spawning balls for color patterns
2. When you see 2 adjacent balls of the same color, shoot a matching projectile between them
3. **Expected:** All 3 balls should disappear

### Console Output:
```
Inserted ball at progress 0.45 - Color: Blue
Match detected! 3 Blue balls from index 2 to 4
💥 Destroying Blue ball at position (1.2, 0.5, 0)
💥 Destroying Blue ball at position (1.7, 0.5, 0)
💥 Destroying Blue ball at position (2.2, 0.5, 0)
Destroyed 3 Blue balls!
Score: +30 (Combo x1) | Total: 30/1000
```

## 💥 Cascade Test

### Setup a Cascade:
1. Look for this pattern: `[Red][Red][Blue][Blue][Blue][Red][Red]`
2. Shoot a Blue projectile at any Blue ball
3. **Expected Sequence:**
   - First match: 4 Blues destroyed
   - Gap closes (animated)
   - Reds come together
   - Cascade: 4 Reds destroyed
   - Score increases twice with combo multiplier

### Console Output:
```
Match detected! 4 Blue balls from index 2 to 5
Destroyed 4 Blue balls!
Score: +40 (Combo x1) | Total: 40/1000
Cascade match found! 4 more balls!
Match detected! 4 Red balls from index 0 to 3
Destroyed 4 Red balls!
Score: +60 (Combo x2) | Total: 100/1000
```

## 🏆 Win Condition Test

### Method 1: Clear All Balls
1. Destroy all balls in the chain
2. **Expected:** Win message in console

### Method 2: Reach Target Score
1. Keep destroying balls until score >= 1000
2. **Expected:** Win message in console

### Console Output:
```
All balls cleared! Level Complete!
=== VICTORY ===
Final Score: 1250
Max Combo: 5
```

## 💀 Lose Condition Test

### Let Ball Reach End:
1. Don't shoot any projectiles
2. Wait for balls to reach the end of the path
3. **Expected:** Game over message

### Console Output:
```
Ball reached the end! Game Over!
=== GAME OVER ===
Final Score: 450/1000
```

## 🔧 Debug Features

### Console Logs to Watch:
- `Spawned ball #X - Color: [color]` - Ball spawning
- `Spawned projectile - Color: [color]` - Projectile spawning
- `Projectile hit ball!` - Collision detected
- `Inserted ball at progress X` - Ball inserted
- `Match detected! X [color] balls` - Match found
- `Cascade match found!` - Cascade triggered
- `Score: +X (Combo xY)` - Score update

### Scene View Gizmos:
- **Yellow spheres** - Path control points
- **Cyan line** - Calculated path
- **Green sphere + line** - Projectile spawn point

## 🐛 Common Issues & Solutions

### Projectile flies toward camera:
**Problem:** Camera is at Z = 0, spawn point calculation breaks
**Solution:** Move camera to Z = -10 or set spawn point Z-distance manually

### No matches detected:
**Check:**
- Are there actually 3+ consecutive balls of same color?
- Is `destructionDelay` too short? (Try increasing to 0.2s)
- Check console for match detection logs

### Balls overlap after insertion:
**Check:**
- `ballSpacing` value (should be ~0.5)
- Path length calculation
- `PushBallsBackward` is being called

### Cascades not working:
**Check:**
- Gap is closing properly (watch animation)
- `DetectCascadeMatch` is being called
- Colors on both sides of gap actually match

## 📊 Performance Test

### Frame Rate:
1. Press Play
2. Open **Stats window** (Game view → Stats)
3. Spawn 30+ balls
4. Shoot multiple projectiles
5. **Target:** Maintain 60 FPS

### Expected Performance:
- ✅ 60 FPS with 50 balls on screen
- ✅ No frame drops during match destruction
- ✅ Smooth gap closing animation
- ✅ No GC spikes (check Profiler)

## 🎯 Scoring System Test

### Verify Score Calculation:
```
Match 1: 3 balls × 10 points × 1.0 = 30 points
Match 2: 4 balls × 10 points × 1.5 = 60 points
Match 3: 5 balls × 10 points × 2.0 = 100 points
Total: 190 points in 3 matches
```

### Combo Test:
1. Create a cascade scenario
2. Watch score increase with each cascade
3. Verify combo multiplier increases: 1.0x → 1.5x → 2.0x → 2.5x

## ✅ Complete Test Checklist

- [ ] Balls spawn and move along path
- [ ] Projectile spawns on click/tap
- [ ] Projectile follows cursor/finger
- [ ] Projectile has trail effect
- [ ] Collision detection works
- [ ] Ball inserts at correct position
- [ ] Match of 3 balls detected and destroyed
- [ ] Match of 4+ balls detected and destroyed
- [ ] Gap closes with smooth animation
- [ ] Cascade matches detected
- [ ] Multiple cascades work
- [ ] Score increases correctly
- [ ] Combo multiplier works
- [ ] Win condition (clear chain) works
- [ ] Win condition (reach score) works
- [ ] Lose condition (ball reaches end) works
- [ ] 60 FPS maintained during gameplay
- [ ] No memory leaks or GC spikes

## 🚀 Advanced Testing

### Stress Test:
1. Set `maxBalls` to 100 in BallSpawner
2. Set `spawnInterval` to 0.1
3. Spam projectiles
4. **Expected:** System should handle it without crashes

### Edge Cases:
1. **Insert at chain start** - Projectile hits first ball
2. **Insert at chain end** - Projectile hits last ball
3. **Destroy entire chain at once** - Line up all same color
4. **Cascades at start/end** - Verify gap closing works

---

All systems should pass these tests! Report any issues. 🎮✨
