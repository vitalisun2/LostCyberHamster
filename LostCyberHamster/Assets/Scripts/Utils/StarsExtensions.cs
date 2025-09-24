using System.Collections.Generic;
using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Utils
{
    public static class StarsExtensions
    {
        public static int GetStars(this Dictionary<LevelKey, int> dict, LevelKey key)
        {
            return dict != null && dict.TryGetValue(key, out var value) ? value : 0;
        }
    }
}
