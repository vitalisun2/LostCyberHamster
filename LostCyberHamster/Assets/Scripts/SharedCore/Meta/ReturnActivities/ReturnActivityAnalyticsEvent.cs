using Unity.Services.Analytics;
using Application = UnityEngine.Application;

namespace Vues.GameCore.ReturnActivities
{
    /// <summary>Версионная схема аналитики активностей без идентификаторов авторизации.</summary>
    public sealed class ReturnActivityAnalyticsEvent : Event
    {
        public ReturnActivityAnalyticsEvent(ReturnActivityEvent item) : base("return_activity")
        {
            SetParameter("ra_schema", 1);
            SetParameter("ra_event_id", item.Id);
            SetParameter("ra_action", item.Action);
            SetParameter("ra_kind", item.Kind ?? string.Empty);
            SetParameter("ra_period", item.Period ?? string.Empty);
            SetParameter("ra_correlation", item.Correlation ?? string.Empty);
            SetParameter("ra_policy", ActivityDayPolicy.Version);
            SetParameter("ra_trust", "local");
            SetParameter("ra_step", item.Step);
            SetParameter("ra_wins", item.Wins);
            SetParameter("ra_days", item.Days);
            SetParameter("ra_coins", item.Coins);
            SetParameter("ra_gems", item.Gems);
            SetParameter("ra_app_version", Application.version);
            SetParameter("ra_config", ReturnActivityConfig.Current?.Version ?? 0);
        }
    }
}
