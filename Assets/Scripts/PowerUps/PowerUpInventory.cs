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

        private PowerUpType[] slots;
        private PowerUpType equippedPowerUp = PowerUpType.None;
        private int equippedSlotIndex = -1;

        public PowerUpType EquippedPowerUp => equippedPowerUp;
        public int EquippedSlotIndex => equippedSlotIndex;
        public int SlotCount => slots.Length;
        public int MaxSlots => settings != null ? settings.maxPowerUpSlots : 3;

        private void Awake()
        {
            slots = new PowerUpType[MaxSlots];
        }

        /// <summary>Fired when a new power-up is added to the inventory.</summary>
        public System.Action<PowerUpType> OnPowerUpEarned;
        /// <summary>Fired when the equipped power-up changes (None = unequipped).</summary>
        public System.Action<PowerUpType> OnPowerUpEquipped;
        /// <summary>Fired when the equipped power-up is consumed by a shot.</summary>
        public System.Action OnPowerUpConsumed;
        /// <summary>Fired when an instant potion (Hammer/Freeze) is used. PotionEffects handles it.</summary>
        public System.Action<PowerUpType> OnInstantPotionUsed;

        /// <summary>
        /// Uses the potion in the given slot. Armed potions (Pierce/Bomb) equip to modify the next
        /// shot; instant potions (Hammer/Freeze) fire immediately and clear the slot. The single
        /// entry point for both keyboard input and the slot-button UI.
        /// </summary>
        public void UseSlot(int index)
        {
            if (index < 0 || index >= slots.Length) return;
            PowerUpType type = slots[index];
            if (type == PowerUpType.None) return;

            if (type.IsArmed())
            {
                EquipSlot(index); // toggle equip; consumed by ProjectileSpawner on launch
                return;
            }

            // Instant: clear the slot and fire the effect now.
            if (equippedSlotIndex == index) UnequipPowerUp();
            slots[index] = PowerUpType.None;
            OnInstantPotionUsed?.Invoke(type);
            OnPowerUpConsumed?.Invoke(); // refresh the slot UI
            Debug.Log($"PowerUpInventory: Used instant potion {type} (slot {index}).");
        }

        private void Update()
        {
            // Power-up slot selection — a real gameplay control on any platform with a keyboard
            // (editor, desktop, and the WebGL build). Mobile uses the on-screen slot buttons.
            #if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
            if (Input.GetKeyDown(KeyCode.Alpha1)) UseSlot(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) UseSlot(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) UseSlot(2);
            if (Input.GetKeyDown(KeyCode.Alpha4)) UseSlot(3);
            if (Input.GetKeyDown(KeyCode.Alpha0)) UnequipPowerUp();
            #endif

            // Debug cheat: press P to grant a free Freeze potion. Kept out of web and mobile builds.
            #if UNITY_EDITOR || UNITY_STANDALONE
            if (Input.GetKeyDown(KeyCode.P))
            {
                AddPowerUp(PowerUpType.Freeze);
                Debug.Log("PowerUpInventory: Debug Freeze potion granted.");
            }
            #endif
        }

        /// <summary>
        /// Adds a power-up to the next free slot. Returns false if all slots are full.
        /// </summary>
        public bool AddPowerUp(PowerUpType type)
        {
            if (type == PowerUpType.None) return false;

            // Find the first empty slot from the left
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == PowerUpType.None)
                {
                    slots[i] = type;
                    OnPowerUpEarned?.Invoke(type);
                    Debug.Log($"PowerUpInventory: {type} added (slot {i}).");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Equips the power-up in the given slot. No-op if slot is empty.
        /// </summary>
        public void EquipSlot(int index)
        {
            if (index < 0 || index >= slots.Length) return;
            if (slots[index] == PowerUpType.None) return;
            if (!slots[index].IsArmed()) return; // instant potions fire via UseSlot, never equip

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
            if (equippedSlotIndex < slots.Length)
                slots[equippedSlotIndex] = PowerUpType.None;

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
            if (index < 0 || index >= slots.Length) return PowerUpType.None;
            return slots[index];
        }
    }
}
