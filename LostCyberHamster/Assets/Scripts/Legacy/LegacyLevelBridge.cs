using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Legacy
{
    public static class LegacyLevelBridge
    {
        public static LevelKey ToKey(string legacy) => LevelKey.Parse(legacy);
        public static string ToName(LevelKey k) => k.ToCompactString();
    }
}
