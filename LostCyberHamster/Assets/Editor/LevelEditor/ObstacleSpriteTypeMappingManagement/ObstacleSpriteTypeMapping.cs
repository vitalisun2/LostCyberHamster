using System.Collections.Generic;
using Assets.Scripts.Common.Models;

namespace Assets.Editor.LevelEditor.ObstacleSpriteTypeMappingManagement
{
    [System.Serializable]
    public class ObstacleSpriteTypeMapping
    {
        public ObstacleTypeEnum type;
        public List<string> sprites;
    }
}
