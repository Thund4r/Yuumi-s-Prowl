using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YuumisProwl.Progression;

namespace YuumisProwl.Enemy
{
    /// <summary>
    /// Radial poison dial shown on the boss. Full green circle at full poison duration; as the
    /// duration drains the empty wedge sweeps round like a clock, then the dial hides once poison
    /// clears. Drives a Filled / Radial-360 Image's fillAmount from PoisonSynergy.
    ///
    /// Setup: put this on the boss prefab, assign a child Image set to Image Type = Filled,
    /// Fill Method = Radial 360 (configure Fill Origin / Clockwise for the sweep direction you want).
    /// PoisonSynergy is found in the scene at runtime (it can't be referenced from a prefab).
    /// </summary>
    public class PoisonDialUI : MonoBehaviour
    {
        [Tooltip("The radial dial image — Image Type = Filled, Fill Method = Radial 360.")]
        [SerializeField] private Image dial;
        [Tooltip("Optional label showing the current poison stack count.")]
        [SerializeField] private TMP_Text stackLabel;
        [Tooltip("Optional root toggled with poison presence. Defaults to the dial's GameObject.")]
        [SerializeField] private GameObject visibleRoot;

        private PoisonSynergy poison;
        private int shownStacks = -1;

        private void Awake()
        {
            if (visibleRoot == null && dial != null) visibleRoot = dial.gameObject;
        }

        private void OnEnable()
        {
            // Scene-level system — can't be wired from the boss prefab, so resolve it at spawn.
            poison = FindObjectOfType<PoisonSynergy>();
            SetVisible(false);
        }

        private void Update()
        {
            if (poison == null) return;

            bool active = poison.HasPoison;
            SetVisible(active);
            if (!active) return;

            if (dial != null)
                dial.fillAmount = poison.PoisonDurationFraction;

            if (stackLabel != null && poison.PoisonStacks != shownStacks)
            {
                shownStacks = poison.PoisonStacks;
                stackLabel.text = shownStacks.ToString();
            }
        }

        private void SetVisible(bool visible)
        {
            if (visibleRoot != null && visibleRoot.activeSelf != visible)
                visibleRoot.SetActive(visible);
        }
    }
}
