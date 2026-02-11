using UnityEngine;

namespace YuumisProwl.BallChain
{
    /// <summary>
    /// Handles visual effects when balls are destroyed.
    /// Can be enhanced with particle systems, sound, etc.
    /// </summary>
    public class BallDestructionEffect : MonoBehaviour
    {
        [Header("Effect Settings")]
        [SerializeField] private ParticleSystem particleEffect;
        [SerializeField] private float effectDuration = 1f;
        [SerializeField] private AudioClip destructionSound;

        private AudioSource audioSource;

        private void Awake()
        {
            if (particleEffect == null)
            {
                particleEffect = GetComponent<ParticleSystem>();
            }

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null && destructionSound != null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
        }

        /// <summary>
        /// Plays destruction effect at a specific position with a color.
        /// </summary>
        public void PlayEffect(Vector3 position, Color color)
        {
            transform.position = position;

            // Set particle color
            if (particleEffect != null)
            {
                var main = particleEffect.main;
                main.startColor = color;
                particleEffect.Play();
            }

            // Play sound
            if (audioSource != null && destructionSound != null)
            {
                audioSource.PlayOneShot(destructionSound);
            }

            // Auto-disable after duration
            Invoke(nameof(DisableEffect), effectDuration);
        }

        private void DisableEffect()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Stops the effect immediately.
        /// </summary>
        public void StopEffect()
        {
            if (particleEffect != null)
            {
                particleEffect.Stop();
            }
            gameObject.SetActive(false);
        }
    }
}
