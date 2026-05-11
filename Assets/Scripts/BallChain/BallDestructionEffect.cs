using UnityEngine;

namespace YuumisProwl.BallChain
{
    /// <summary>
    /// Plays one or more ParticleSystems (plus an optional sound) when balls are destroyed.
    /// Tints every system's start color to the match color. If no systems are explicitly
    /// assigned, every ParticleSystem on this GameObject and its children is used.
    /// </summary>
    public class BallDestructionEffect : MonoBehaviour
    {
        [Header("Effect Settings")]
        [Tooltip("Particle systems to play. If empty, all ParticleSystems in this prefab's hierarchy are auto-collected.")]
        [SerializeField] private ParticleSystem[] particleEffects;
        [Tooltip("If true, each system's main.startColor is tinted to the match color.")]
        [SerializeField] private bool tintParticles = true;
        [SerializeField] private float effectDuration = 1f;
        [SerializeField] private AudioClip destructionSound;

        private AudioSource audioSource;

        private void Awake()
        {
            if (particleEffects == null || particleEffects.Length == 0)
                particleEffects = GetComponentsInChildren<ParticleSystem>(includeInactive: true);

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null && destructionSound != null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
        }

        public void PlayEffect(Vector3 position, Color color)
        {
            transform.position = position;

            if (particleEffects != null)
            {
                for (int i = 0; i < particleEffects.Length; i++)
                {
                    var ps = particleEffects[i];
                    if (ps == null) continue;

                    if (tintParticles)
                    {
                        var main = ps.main;
                        main.startColor = color;
                    }
                    ps.Play();
                }
            }

            if (audioSource != null && destructionSound != null)
                audioSource.PlayOneShot(destructionSound);

            CancelInvoke(nameof(DisableEffect));
            Invoke(nameof(DisableEffect), effectDuration);
        }

        private void DisableEffect()
        {
            gameObject.SetActive(false);
        }

        public void StopEffect()
        {
            if (particleEffects != null)
            {
                for (int i = 0; i < particleEffects.Length; i++)
                {
                    if (particleEffects[i] != null) particleEffects[i].Stop();
                }
            }
            gameObject.SetActive(false);
        }
    }
}
