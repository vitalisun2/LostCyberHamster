using System;
using System.Collections.Generic;

namespace Assets.Scripts.Common.Models
{
    [Serializable]
    public class PatternTemplate
    {
        public string name;
        public string description;
        public int nextObstacleId;
        public List<ObstacleSlot> obstacles = new();
    }
}
