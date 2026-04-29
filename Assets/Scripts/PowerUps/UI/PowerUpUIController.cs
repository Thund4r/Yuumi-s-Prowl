using UnityEngine;
using UnityEngine.UI;

namespace YuumisProwl.PowerUps.UI
{
    /// <summary>
    /// Drives the power-up HUD: charge meter plus a row of slot buttons that show
    /// earned power-ups and let the player tap to equip (tap again to unequip).
    ///
    /// Setup:
    ///   1. Build the UI in the scene — a Canvas with:
    ///      - A Slider (charge meter)
    ///      - N slot rows, each containing a Button with a child Image (icon)
    ///        and an optional child GameObject (equipped highlight border).
    ///   2. Add this component to any GameObject in the scene (often the Canvas itself).
    ///   3. Assign the PowerUpInventory and PowerUpChargeTracker references.
    ///   4. Fill in the slots array with one entry per visible slot (usually matches
    ///      PowerUpSettings.maxPowerUpSlots — but any mismatch is tolerated).
    ///   5. Assign a PowerUpIconDatabase for icon/color lookups.
    /// </summary>
    public class PowerUpUIController : MonoBehaviour
    {
        [System.Serializable]
        public class SlotUI
        {
            public Button button;
            [Tooltip("Image displayed inside the button. Disabled when the slot is empty.")]
            public Image icon;
            [Tooltip("Optional GameObject toggled on when this slot is the equipped one.")]
            public GameObject equippedHighlight;
        }

        [Header("References")]
        [SerializeField] private PowerUpInventory inventory;
        [SerializeField] private PowerUpChargeTracker chargeTracker;
        [SerializeField] private PowerUpIconDatabase iconDatabase;

        [Header("Charge Meter")]
        [SerializeField] private Slider chargeSlider;

        [Header("Slots")]
        [SerializeField] private SlotUI[] slots;

        private void Start()
        {
            if (inventory == null)    Debug.LogError("PowerUpUIController: PowerUpInventory not assigned!");
            if (chargeTracker == null) Debug.LogError("PowerUpUIController: PowerUpChargeTracker not assigned!");

            WireSlotButtons();

            if (inventory != null)
            {
                inventory.OnPowerUpEarned   += HandleInventoryChanged;
                inventory.OnPowerUpConsumed += HandleInventoryConsumed;
                inventory.OnPowerUpEquipped += HandleEquippedChanged;
            }

            if (chargeTracker != null)
                chargeTracker.OnChargeChanged += HandleChargeChanged;

            // Initial refresh
            RefreshSlots();
            RefreshEquippedHighlight();
            if (chargeTracker != null)
                HandleChargeChanged(chargeTracker.CurrentCharge, chargeTracker.ChargeThreshold);
        }

        private void OnDestroy()
        {
            if (inventory != null)
            {
                inventory.OnPowerUpEarned   -= HandleInventoryChanged;
                inventory.OnPowerUpConsumed -= HandleInventoryConsumed;
                inventory.OnPowerUpEquipped -= HandleEquippedChanged;
            }

            if (chargeTracker != null)
                chargeTracker.OnChargeChanged -= HandleChargeChanged;
        }

        private void WireSlotButtons()
        {
            if (slots == null) return;

            for (int i = 0; i < slots.Length; i++)
            {
                int index = i; // capture for closure
                if (slots[i] == null) continue;

                if (slots[i].button != null)
                {
                    slots[i].button.onClick.RemoveAllListeners();
                    slots[i].button.onClick.AddListener(() => OnSlotClicked(index));
                }

                // Prevent child images from stealing clicks from the parent Button
                if (slots[i].icon != null)
                    slots[i].icon.raycastTarget = false;
            }
        }

        /// <summary>
        /// Tap-to-equip, tap-again-to-unequip. Ignores empty slots.
        /// </summary>
        private void OnSlotClicked(int index)
        {
            if (inventory == null) return;
            if (index < 0 || index >= inventory.SlotCount) return;

            inventory.EquipSlot(index);
        }

        private void HandleChargeChanged(int current, int threshold)
        {
            if (chargeSlider == null || threshold <= 0) return;
            chargeSlider.value = Mathf.Clamp01((float)current / threshold);
        }

        private void HandleInventoryChanged(PowerUpType type) => RefreshSlots();
        private void HandleInventoryConsumed() => RefreshSlots();

        private void HandleEquippedChanged(PowerUpType type)
        {
            RefreshEquippedHighlight();
        }

        private void RefreshSlots()
        {
            if (slots == null || inventory == null) return;

            for (int i = 0; i < slots.Length; i++)
            {
                SlotUI slot = slots[i];
                if (slot == null) continue;

                PowerUpType type = inventory.GetSlot(i);
                bool filled = type != PowerUpType.None;

                if (slot.icon != null)
                {
                    slot.icon.enabled = filled;
                    if (filled && iconDatabase != null)
                    {
                        slot.icon.sprite = iconDatabase.GetIcon(type);
                        slot.icon.color  = iconDatabase.GetTint(type);
                    }
                }

                if (slot.button != null)
                    slot.button.interactable = filled;
            }
        }

        private void RefreshEquippedHighlight()
        {
            if (slots == null || inventory == null) return;

            int equippedIndex = inventory.EquippedSlotIndex;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].equippedHighlight != null)
                    slots[i].equippedHighlight.SetActive(i == equippedIndex);
            }
        }
    }
}
