# Ball Path Test Scene Setup Guide

This guide helps you set up and test the Ball Path System in Unity.

## Quick Setup Steps

### 1. Open Unity and Import Scripts

1. Open the project in Unity 2022.3.62f3
2. Wait for Unity to compile all scripts
3. Fix any script GUID references if needed

### 2. Create the Ball Prefab (3D)

1. In Unity, create a new **3D Object → Sphere**
2. Rename it to "Ball"
3. Scale it to (0.5, 0.5, 0.5)
4. Add the `Ball` component (Scripts/BallChain/Ball.cs)
5. The `MeshRenderer` and `SphereCollider` should be added automatically
6. Configure `SphereCollider`:
   - Set as **Trigger** (✓)
   - Radius: 0.5
7. Set Tag to "Ball" (create if doesn't exist)
8. Save as prefab in `Assets/Prefabs/Balls/Ball.prefab`

### 3. Set Up the Test Scene

#### Option A: Use Existing Scene
Open `Assets/Scenes/BallPathTest.unity` and configure the references manually (see below).

#### Option B: Create New Scene from Scratch

1. **Create Path Controller:**
   - Create Empty GameObject → Rename to "PathController"
   - Add `PathController` component
   - Create 5 child empty GameObjects named "PathPoint_0" through "PathPoint_4"
   - Position them in a curved path:
     - PathPoint_0: (-6, 2, 0)
     - PathPoint_1: (-3, -2, 0)
     - PathPoint_2: (0, 1, 0)
     - PathPoint_3: (3, -2, 0)
     - PathPoint_4: (6, 2, 0)
   - Assign all PathPoints to the `Path Points` array in PathController

2. **Create Game Manager:**
   - Create Empty GameObject → Rename to "GameManager"
   - Add `BallChainManager` component:
     - Assign PathController reference
     - Assign Ball prefab
     - Ball Speed: 1.5
     - Ball Spacing: 0.5
     - Initial Pool Size: 50
   - Add `BallSpawner` component:
     - Assign BallChainManager reference (from same GameObject)
     - Spawn Interval: 0.5
     - Max Balls: 30
     - Spawn On Start: ✓
     - Color Count: 4
     - Auto Spawn: ✓

3. **Configure Camera:**
   - Select Main Camera
   - Set Projection to **Perspective**
   - Field of View: 60
   - Background: Skybox
   - Position: (0, 5, -10)
   - Rotation: (10, 0, 0) - slight downward angle

4. **Add Lighting:**
   - Create **Directional Light** (Light → Directional Light)
   - Position: (0, 3, 0)
   - Rotation: (50, -30, 0)
   - Intensity: 1
   - Enable Shadows

### 4. Create Projectile Prefab (3D)

1. In Unity, create a new **3D Object → Sphere**
2. Rename it to "Projectile"
3. Scale it to (0.3, 0.3, 0.3) - smaller than balls
4. Add the `Projectile` component (Scripts/Projectile/Projectile.cs)
5. Add `TrailRenderer` component:
   - Time: 0.5
   - Width: 0.2 → 0 (curve)
   - Color: White → Transparent
6. Configure `SphereCollider`:
   - Set as **Trigger** (✓)
7. Set Tag to "Projectile" (create if doesn't exist)
8. Save as prefab in `Assets/Prefabs/Projectiles/Projectile.prefab`

### 5. Test the Scene

1. Press **Play** in Unity
2. You should see:
   - Yellow spheres marking the path points (Scene view gizmos)
   - Cyan line showing the calculated path (Scene view)
   - 3D spherical balls spawning automatically every 0.5 seconds
   - Balls moving along the path in a smooth chain
   - Different colored balls with 3D shading (4 colors by default)
   - Shadows cast by the directional light
   - Balls properly lit with the standard material

3. **Controls:**
   - **Click/Tap** anywhere to shoot a projectile
   - Projectile homes toward your mouse/finger position
   - When projectile hits a ball, it inserts into the chain
   - **Space** manually spawns a ball to the chain
   - Auto-ball-spawn is enabled by default

### 5. Troubleshooting

**No balls appearing:**
- Check Ball prefab is assigned in BallChainManager
- Check Console for errors
- Verify scripts compiled successfully

**Balls not moving:**
- Check Path Points are assigned to PathController
- Verify BallChainManager is enabled
- Check Ball Speed > 0

**Path not visible:**
- Make sure Scene view is active
- Check PathController → Draw Gizmos is enabled
- Verify path points have different positions

**Script compilation errors:**
- Unity may need to generate .meta files
- Try reimporting the Scripts folder
- Check all namespaces are correct

### 6. Customization

**Change Path Shape:**
- Select PathController's child PathPoints
- Move them in Scene view to create different curves
- Add more path points for complex paths

**Adjust Ball Speed:**
- Select GameManager → BallChainManager
- Modify Ball Speed (try 0.5 - 3.0)

**Change Colors:**
- Select GameManager → BallSpawner
- Adjust Color Count (1-6)

**Spawn Rate:**
- Select GameManager → BallSpawner
- Modify Spawn Interval (seconds between spawns)

## Next Steps

Once the basic path system is working:
1. Test ball insertion at different positions
2. Implement Match Detection system
3. Add Projectile system
4. Implement collision and matching logic

## Debug Information

**Scene Hierarchy should look like:**
```
- Main Camera (Perspective, FOV: 60)
- Directional Light (50, -30, 0)
- PathController
  - PathPoint_0
  - PathPoint_1
  - PathPoint_2
  - PathPoint_3
  - PathPoint_4
- GameManager
  - BallChainManager (component)
  - BallSpawner (component)
  - ProjectileSpawner (component)
```

**Key Differences from 2D:**
- Uses 3D Sphere mesh instead of 2D sprite
- Uses MeshRenderer + Material instead of SpriteRenderer
- Uses SphereCollider instead of CircleCollider2D
- Uses Perspective camera instead of Orthographic
- Requires lighting (Directional Light added)
- Balls have 3D depth and proper shadows

**Expected Console Output:**
```
Spawned ball #1 - Color: Red
Spawned ball #2 - Color: Blue
Spawned projectile - Color: Green
Projectile hit ball! Inserting Green at progress 0.45
Inserted ball at progress 0.45 - Color: Green
...
```

## Features Demonstrated

### Ball Path System
- ✅ Smooth curved path movement
- ✅ Perfect ball spacing maintained
- ✅ Object pooling for performance
- ✅ Multiple ball colors

### Projectile System
- ✅ Homing behavior (follows mouse/touch)
- ✅ Smooth rotation toward target
- ✅ Trail renderer effect
- ✅ Collision detection with balls
- ✅ Ball insertion on hit
- ✅ Object pooling for projectiles

## Next Steps

Once both systems are working:
1. Implement **Match Detection** (3+ matching colors)
2. Add **Ball Destruction** animations
3. Implement **Scoring System**
4. Add **Sound Effects**
5. Create actual **Game Levels**
