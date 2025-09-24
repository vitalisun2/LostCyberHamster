using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Common
{
    public static class LevelPathBuilder
    {
        public static string Build(LevelKey key)
        {
            return $"{key.ToPath()}.json";
        }

        public static string PreviewPath(LevelKey key)
        {
            return $"{key.ToPath()}/preview.png";
        }

        public static string IntroImage(LevelKey k, int n) =>
            $"{k.ToPath()}/intro_{n}.png";
    }
}
