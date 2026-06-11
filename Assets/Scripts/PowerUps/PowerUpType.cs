namespace YuumisProwl.PowerUps
{
    /// <summary>
    /// A consumable potion the player holds in an inventory slot. "Armed" potions (Pierce, Bomb)
    /// load the next shot; "instant" potions (Hammer, Freeze) fire the moment they're used.
    /// Separate from BallPowerUpType, which identifies power-ups embedded in the ball chain.
    /// </summary>
    public enum PowerUpType
    {
        None,
        Pierce,   // armed: next shot pierces
        Bomb,     // armed: next shot explodes on contact
        Hammer,   // instant: spawns a hammer ball in the chain
        Freeze    // instant: stops the chain advancing for a few seconds
    }

    public static class PowerUpTypeExtensions
    {
        /// <summary>
        /// Armed potions modify the next projectile (equip, then consumed on launch). Instant
        /// potions fire immediately on use.
        /// </summary>
        public static bool IsArmed(this PowerUpType type)
            => type == PowerUpType.Pierce || type == PowerUpType.Bomb;
    }
}
