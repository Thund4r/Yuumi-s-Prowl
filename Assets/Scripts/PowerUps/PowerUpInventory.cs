using System.Collections.Generic;
using UnityEngine;

namespace YuumisProwl.PowerUps
{
    /// <summary>
    /// Holds the player's earned power-ups in a fixed number of slots and tracks which
    /// one is currently equipped. The equipped power-up is consumed when a projectile
    /// is launched.
    ///
    /// Setup:
    ///   1. Add this component to any GameObject in the level scene (often the same
    ///      GameObject as PowerUpChargeTracker).
    ///   2. Assign the shared PowerUpSettings asset.
    ///   3. Assign in ProjectileSpawner so shots can pick up the equipped power-up.
    ///
    /// Input:
    ///   Editor/standalone testing uses number keys 1-3 to equip slots.
    ///   Future UI can call EquipSlot(int) directly.
    /// </summary>
    public class PowerUpInventory : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private PowerUpSettings settings;

        private readonly List<PowerUpType> slots = new List<PowerUpType>();
        private PowerUpType equippedPowerUp = PowerUpType.None;
        private int equippedSlotIndex = -1;

        public PowerUpType EquippedPowerUp => equippedPowerUp;
        public int EquippedSlotIndex => equippedSlotIndex;
        public int SlotCount => slots.Count;
        public int MaxSlots => settings != null ? settings.maxPowerUpSlots : 3;

        /// <summary>Fired when a new power-up is added to the inventory.</summary>
        public System.Action<PowerUpType> OnPowerUpEarned;
        /// <summary>Fired when the equipped power-up changes (None = unequipped).</summary>
        public System.Action<PowerUpType> OnPowerUpEquipped;
        /// <summary>Fired when the equipped power-up is consumed by a shot.</summary>
        public System.Action OnPowerUpConsumed;

        private void Update()
        {
            #if UNITY_EDITOR || UNITY_STANDALONE
            if (Input.GetKeyDown(KeyCode.Alpha1)) EquipSlot(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) EquipSlot(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) EquipSlot(2);
            if (Input.GetKeyDown(KeyCode.Alpha0)) UnequipPowerUp();

            // Debug: press P to manually grant a Pierce
            if (Input.GetKeyDown(KeyCode.P))
            {
                AddPowerUp(PowerUpType.Pierce);
                Debug.Log("PowerUpInventory: Debug Pierce granted.");
            }
            #endif
        }

        /// <summary>
        /// Adds a power-up to the next free slot. Returns false if all slots are full.
        /// </summary>
        public bool AddPowerUp(PowerUpType type)
        {
            if (type == PowerUpType.None) return false;
            if (slots.Count >= MaxSlots) return false;

            slots.Add(type);
            OnPowerUpEarned?.Invoke(type);
            Debug.Log($"PowerUpInventory: {type} added (slot {slots.Count - 1}).");
            return true;
        }

        /// <summary>
        /// Equips the power-up in the given slot. No-op if slot is empty.
        /// </summary>
        public void EquipSlot(int index)
        {
            if (index < 0 || index >= slots.Count) return;

            // Toggle: pressing the same slot again unequips
            if (equippedSlotIndex == index)
            {
                UnequipPowerUp();
                return;
            }

            equippedSlotIndex = index;
            equippedPowerUp = slots[index];
            OnPowerUpEquipped?.Invoke(equippedPowerUp);
            Debug.Log($"PowerUpInventory: Equipped {equippedPowerUp} from slot {index}.");
        }

        /// <summary>
        /// Clears the equipped power-up without consuming the slot.
        /// </summary>
        public void UnequipPowerUp()
        {
            if (equippedPowerUp == PowerUpType.None) return;

            equippedPowerUp = PowerUpType.None;
            equippedSlotIndex = -1;
            OnPowerUpEquipped?.Invoke(PowerUpType.None);
        }

        /// <summary>
        /// Consumes the equipped power-up (removes it from its slot) and returns its type.
        /// Called by ProjectileSpawner after a power-up-infused shot launches.
        /// </summary>
        public PowerUpType ConsumeEquipped()
        {
            if (equippedPowerUp == PowerUpType.None || equippedSlotIndex < 0)
                return PowerUpType.None;

            PowerUpType consumed = equippedPowerUp;
            if (equippedSlotIndex < slots.Count)
                slots.RemoveAt(equippedSlotIndex);

            equippedPowerUp = PowerUpType.None;
            equippedSlotIndex = -1;

            OnPowerUpConsumed?.Invoke();
            Debug.Log($"PowerUpInventory: Consumed {consumed}.");
            return consumed;
        }

        /// <summary>
        /// Returns the power-up at the given slot, or None if the slot is empty/invalid.
        /// </summary>
        public PowerUpType GetSlot(int index)
        {
            if (index < 0 || index >= slots.Count) return PowerUpType.None;
            return slots[index];
        }
    }
}
