using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Common
{
    public static class LevelPathBuilder
    {
        public static string Build(LevelKey key)
        {
            return key.ToPath();
        }

        public static string PreviewPath(LevelKey key)
        {
            return key.ToPath();
        }
    }
}
