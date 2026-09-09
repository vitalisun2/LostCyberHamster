using System;
using System.Globalization;

namespace Vues.GameCore.ReturnActivities
{
    /// <summary>Определяет UTC-периоды активности; источник времени остаётся клиентским.</summary>
    public static class ActivityDayPolicy
    {
        public const string Version = "local_utc_v1";
        public static string Day(DateTime utc) => utc.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        public static DateTime WeekStart(DateTime utc)
        {
            var date = utc.ToUniversalTime().Date;
            return date.AddDays(-((int)date.DayOfWeek + 6) % 7);
        }
        public static string Week(DateTime utc) => Day(WeekStart(utc));
        public static bool IsDate(string value) => DateTime.TryParseExact(value, "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out _);
        public static DateTime Parse(string value) => DateTime.ParseExact(value, "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }
}
