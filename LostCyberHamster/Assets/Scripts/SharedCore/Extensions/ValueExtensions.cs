using System.Collections.Generic;

namespace Extensions
{
    public static class ValueExtensions
    {
        public static bool IsDefault<T>(this T value, T defaultValue = default)
        {
            return EqualityComparer<T>.Default.Equals(value, defaultValue);
        }
    }
}
