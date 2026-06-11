using UnityEngine;
using YuumisProwl.BallChain;
using YuumisProwl.Progression;

namespace YuumisProwl.PowerUps
{
    /// <summary>
    /// Executes instant potion effects (Hammer, Freeze) when PowerUpInventory fires
    /// OnInstantPotionUsed. Armed potions (Pierce/Bomb) are applied by ProjectileSpawner on launch,
    /// so they never reach here.
    /// </summary>
    public class PotionEffects : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PowerUpInventory inventory;
        [SerializeField] private BallChainManager ballChainManager;
        [SerializeField] private PowerUpSettings settings;
        [Tooltip("Per-run stats. When assigned, overrides the matching settings values (e.g. hammer recoil).")]
        [SerializeField] private RuntimeStats runtimeStats;

        private void OnEnable()
        {
            if (inventory != null) inventory.OnInstantPotionUsed += HandleInstantPotion;
        }

        private void OnDisable()
        {
            if (inventory != null) inventory.OnInstantPotionUsed -= HandleInstantPotion;
        }

        private void HandleInstantPotion(PowerUpType type)
        {
            if (ballChainManager == null) return;

            switch (type)
            {
                case PowerUpType.Hammer:
                    if (ballChainManager.BallCount > 0)
                    {
                        float recoil = runtimeStats != null ? runtimeStats.HammerRecoilDistance
                                     : settings != null ? settings.hammerRecoilDistance : 3f;
                        ballChainManager.SpawnHammerBall(0, recoil); // near the front of the chain
                    }
                    break;

                case PowerUpType.Freeze:
                    float duration = settings != null ? settings.freezeDuration : 4f;
                    ballChainManager.FreezeChain(duration);
                    break;
            }
        }
    }
}
