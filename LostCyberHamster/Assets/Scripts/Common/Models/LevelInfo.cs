using System;
using System.Collections.Generic;
using System.Text;

namespace Assets.Scripts.Common.Models
{
    [Serializable]
    public class LevelInfo
    {
        // Sky, Background2, Road are auto-generated from location + daypart
        // Only backgroundTexture kept for per-level customization flexibility
        public string backgroundTexture;

        public List<DecorationPattern> decorationPatterns;

        public List<Pattern> patterns = new();

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.AppendLine($"backgroundTexture: {backgroundTexture}");
            sb.AppendLine($"decorationPatterns: {string.Join(", ", decorationPatterns)}");
            sb.AppendLine($"patterns: {string.Join(", ", patterns)}");

            return sb.ToString();
        }
    }
}
