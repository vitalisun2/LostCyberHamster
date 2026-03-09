using System;
using System.Collections.Generic;

namespace Assets.Scripts.Common.Models
{
    [Serializable]
    public class LevelInfoRef
    {
        public string skyTexture;
        public string background2Texture;
        public string backgroundTexture;
        public string roadTexture;
        public string location;

        public List<PatternRef> patternSequence = new();
        public List<DecorationPattern> decorationPatterns = new();
    }
}
