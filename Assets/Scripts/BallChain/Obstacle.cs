using UnityEngine;

namespace YuumisProwl.BallChain
{
    /// <summary>
    /// Marks a GameObject as an obstacle that destroys any projectile on contact.
    /// The ProjectileSpawner automatically queues the next projectile after destruction.
    ///
    /// Setup:
    ///   1. Create a new GameObject in the scene.
    ///   2. Add this component.
    ///   3. Add any Collider shape you want (Box, Sphere, Mesh, Capsule, etc.).
    ///      The collider must NOT be a trigger — the projectile's trigger detects solid colliders.
    ///   4. Position and scale the obstacle in the scene.
    ///
    /// No scripting is required — the Projectile detects this component on contact.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Obstacle : MonoBehaviour
    {
        private void Awake()
        {
            // Ensure the collider is solid, not a trigger.
            // A trigger collider would be invisible to the projectile's own trigger.
            Collider col = GetComponent<Collider>();
            if (col.isTrigger)
            {
                col.isTrigger = false;
                Debug.LogWarning($"Obstacle ({name}): Collider was a trigger — changed to solid so the projectile can detect it.");
            }
        }
    }
}
