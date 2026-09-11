using System;
using System.Globalization;

namespace Vues.GameCore.ReturnActivities
{
    /// <summary>Определяет периоды активности по времени устройства; legacy UTC живёт только для безопасного cutover старых сохранений.</summary>
    public static class ActivityDayPolicy
    {
        public const string LegacyVersion = "local_utc_v1";
        public const string Version = "device_local_v2";

        public static DateTime Normalize(DateTime moment, string version = null)
        {
            version ??= Version;
            return string.Equals(version, LegacyVersion, StringComparison.Ordinal)
                ? moment.ToUniversalTime()
                : moment.ToLocalTime();
        }

        public static string Day(DateTime moment, string version = null) =>
            Normalize(moment, version).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        public static DateTime WeekStart(DateTime moment, string version = null)
        {
            var date = Normalize(moment, version).Date;
            return date.AddDays(-((int)date.DayOfWeek + 6) % 7);
        }

        public static string Week(DateTime moment, string version = null) =>
            WeekStart(moment, version).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        public static string WeekOfDay(string value)
        {
            var date = Parse(value).Date;
            return date.AddDays(-((int)date.DayOfWeek + 6) % 7).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        public static bool IsDate(string value) => DateTime.TryParseExact(value, "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

        public static DateTime Parse(string value) => DateTime.ParseExact(value, "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None);
    }
}
