namespace YuumisProwl
{
    /// <summary>
    /// In-chain disruptor enemies. Durability lives on the boss bar — these are one-clear
    /// puzzle modifiers carried directly on a ball (mirrors how BallPowerUpType.Hammer works).
    /// Behaviour lives in EnemyManager / BossManager; the enum is the only per-ball state.
    /// </summary>
    public enum EnemyType
    {
        None = 0,
        /// <summary>Colourless wall. Not colour-matchable; cracked by an adjacent match or any AoE.</summary>
        Stone = 1,
        /// <summary>Coloured ball that shields the boss (reduced ball-clear damage) while alive.</summary>
        Warden = 2,
    }
}
