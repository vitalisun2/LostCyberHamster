using System;
using System.Collections.Generic;
using System.Text;

namespace Assets.Scripts.Common.Models
{
    [Serializable]
    public class LevelInfo
    {
        public string backgroundTexture;
        public string roadTexture;

        public List<DecorationPattern> decorationPatterns;

        public List<Pattern> patterns = new();

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.AppendLine($"backgroundTexture: {backgroundTexture}");
            sb.AppendLine($"roadTexture: {roadTexture}");
            sb.AppendLine($"decorationPatterns: {string.Join(", ", decorationPatterns)}");
            sb.AppendLine($"patterns: {string.Join(", ", patterns)}");

            return sb.ToString();
        }
    }
}
