using System;
using System.Collections.Generic;

namespace Assets.Scripts.Common.Models
{
    [Serializable]
    public class LocationTheme
    {
        public List<SpriteTypeMapping> obstacle_sprite_to_type_mappings = new();
    }

    [Serializable]
    public class SpriteTypeMapping
    {
        public int type;
        public List<string> sprites = new();
        public string @default;
    }
}
