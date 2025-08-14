using System;
using System.Collections.Generic;

namespace Assets.Scripts.Common.Models
{
    [Serializable]
    public class Pattern
    {
        public string name;
        public string desсription;
        public List<ObstacleModel> obstacles;
    }
}

