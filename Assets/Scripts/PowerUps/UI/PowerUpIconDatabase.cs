using UnityEngine;

namespace YuumisProwl.PowerUps.UI
{
    /// <summary>
    /// Maps PowerUpType values to display sprites and tint colors used by the UI.
    /// Create via: Right-click in Project → Yuumi → Power-Up Icon Database.
    /// Keeps icon data centralized so adding a new power-up is just adding an entry.
    /// </summary>
    [CreateAssetMenu(fileName = "PowerUpIconDatabase", menuName = "Yuumi/Power-Up Icon Database")]
    public class PowerUpIconDatabase : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            public PowerUpType type;
            public Sprite icon;
            public Color tint = Color.white;
        }

        [SerializeField] private Entry[] entries;

        public Sprite GetIcon(PowerUpType type)
        {
            if (entries == null) return null;
            foreach (var e in entries)
                if (e.type == type) return e.icon;
            return null;
        }

        public Color GetTint(PowerUpType type)
        {
            if (entries == null) return Color.white;
            foreach (var e in entries)
                if (e.type == type) return e.tint;
            return Color.white;
        }
    }
}
