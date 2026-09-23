using System;

#nullable enable

namespace GameManagement.Progress
{
    [Serializable]
    public sealed class LevelProgressEntry
    {
        public const int MaxStars = 3;

        public LevelProgressEntry(LevelProgressKey key)
            : this(key, false, 0, null)
        {
        }

        public LevelProgressEntry(LevelProgressKey key, bool isUnlocked, int stars)
            : this(key, isUnlocked, stars, null)
        {
        }

        public LevelProgressEntry(LevelProgressKey key, bool isUnlocked, int stars, string? address)
        {
            Key = key;
            Address = address?.Trim();
            Stars = NormalizeStars(stars);
            IsUnlocked = isUnlocked || Stars > 0;
        }

        public LevelProgressKey Key { get; }
        public string? Address { get; }
        public bool IsUnlocked { get; }
        public int Stars { get; }
        public bool IsCompleted => Stars > 0;

        public LevelProgressEntry Unlock()
        {
            if (IsUnlocked)
            {
                return this;
            }

            return new LevelProgressEntry(Key, true, Stars, Address);
        }

        public LevelProgressEntry WithStars(int stars)
        {
            var normalized = NormalizeStars(stars);
            if (normalized == Stars)
            {
                return this;
            }

            return new LevelProgressEntry(Key, IsUnlocked || normalized > 0, normalized, Address);
        }

        public LevelProgressEntry ApplyStars(int stars)
        {
            var normalized = NormalizeStars(stars);
            if (normalized <= Stars)
            {
                return Unlock();
            }

            return new LevelProgressEntry(Key, true, normalized, Address);
        }

        private static int NormalizeStars(int stars)
        {
            if (stars < 0)
            {
                return 0;
            }

            if (stars > MaxStars)
            {
                return MaxStars;
            }

            return stars;
        }
    }
}
