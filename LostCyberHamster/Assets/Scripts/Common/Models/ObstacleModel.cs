using System;

namespace Assets.Scripts.Common.Models
{
    [Serializable]
    public class ObstacleModel
    {
        public string spriteName;
        public int type;
        public float x;
        public float y;

        public override string ToString()
        {
            return $"spriteName: {spriteName}, type: {type}, x: {x}, y: {y}";
        }
    }
}
