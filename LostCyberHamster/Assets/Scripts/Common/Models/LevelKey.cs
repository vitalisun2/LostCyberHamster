using System;
using System.Globalization;

namespace Assets.Scripts.Common.Models
{
    [Serializable]
    public readonly struct LevelKey : IEquatable<LevelKey>
    {
        public readonly string LocationId;
        public readonly PartOfDay Part;
        public readonly int Index;

        public LevelKey(string locationId, PartOfDay part, int index)
        {
            if (string.IsNullOrWhiteSpace(locationId))
            {
                throw new ArgumentException("Location identifier cannot be null or whitespace.", nameof(locationId));
            }

            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be non-negative.");
            }

            LocationId = NormalizeLocationId(locationId);
            Part = part;
            Index = index;
        }

        public static LevelKey Parse(string compact)
        {
            if (!TryParse(compact, out var levelKey))
            {
                throw new FormatException($"Invalid level key format: '{compact}'.");
            }

            return levelKey;
        }

        public static bool TryParse(string compact, out LevelKey levelKey)
        {
            levelKey = default;

            if (string.IsNullOrWhiteSpace(compact))
            {
                return false;
            }

            var segments = compact.Split('/');
            if (segments.Length != 3)
            {
                return false;
            }

            var location = segments[0];
            var partSegment = segments[1];
            var indexSegment = segments[2];

            if (string.IsNullOrWhiteSpace(location))
            {
                return false;
            }

            if (!Enum.TryParse(partSegment, true, out PartOfDay part))
            {
                return false;
            }

            if (!int.TryParse(indexSegment, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) || index < 0)
            {
                return false;
            }

            levelKey = new LevelKey(location, part, index);
            return true;
        }

        public string ToCompactString()
        {
            var location = LocationId;
            var part = Part.ToString().ToLowerInvariant();
            var index = Index.ToString(CultureInfo.InvariantCulture);
            return string.Join('/', location, part, index);
        }

        public string ToPath()
        {
            var level = $"level_{Index:D2}";
            return string.Join('/', LocationId, Part.ToString().ToLowerInvariant(), level);
        }

        public string ToDisplayName()
        {
            var textInfo = CultureInfo.InvariantCulture.TextInfo;
            var locationDisplay = textInfo.ToTitleCase(LocationId.Replace('_', ' '));
            return $"{locationDisplay} – {Part} – {Index}";
        }

        public bool Equals(LevelKey other)
        {
            return string.Equals(LocationId, other.LocationId, StringComparison.OrdinalIgnoreCase) &&
                   Part == other.Part &&
                   Index == other.Index;
        }

        public override bool Equals(object obj)
        {
            return obj is LevelKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(LocationId?.ToLowerInvariant(), Part, Index);
        }

        public static bool operator ==(LevelKey left, LevelKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(LevelKey left, LevelKey right)
        {
            return !(left == right);
        }

        private static string NormalizeLocationId(string locationId)
        {
            return locationId.Trim().ToLowerInvariant();
        }
    }
}
