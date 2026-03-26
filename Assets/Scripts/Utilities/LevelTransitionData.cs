namespace YuumisProwl
{
    /// <summary>
    /// Static holder that passes the target scene name from a level scene
    /// to the transition scene. Survives scene loads without DontDestroyOnLoad
    /// because it is not a MonoBehaviour.
    /// </summary>
    public static class LevelTransitionData
    {
        public static string NextSceneName;
    }
}
