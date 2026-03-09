using System;
using System.Collections.Generic;

namespace Assets.Scripts.Common.Models
{
    [Serializable]
    public class PatternRef
    {
        public string @ref;
        public int spriteSeed;
        public List<SpriteOverride> overrides = new();
    }
}
