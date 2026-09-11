using System;
using System.Linq;
using GameManagement;
using UnityEngine.UIElements;
using Vues.GameCore.ReturnActivities;

namespace LostCyberHamster.UI
{
    /// <summary>Показывает две самостоятельные активности Home в постоянном порядке.</summary>
    internal sealed class HomeActivityPresenter : IDisposable
    {
        private readonly VisualElement _root;
        private readonly Action<string> _open;
        private readonly Button _cycle;
        private readonly Button _week;
        private readonly IVisualElementScheduledItem _timer;

        public HomeActivityPresenter(VisualElement root, Action<string> open)
        {
            // Каждая художественная полоса — одно действие, внутренние слои пропускают нажатия.
            _root = root; _open = open;
            _cycle = root.Q<Button>("home-activity-cycle");
            _week = root.Q<Button>("home-activity-week");
            root.Q<Label>("home-cycle-title").text = ActivityUiText.Get("cycle");
            root.Q<Label>("home-week-title").text = ActivityUiText.Get("week");
            _cycle.clicked += OpenCycle; _week.clicked += OpenWeek;

            // Подписки принадлежат текущему экранному дереву.
            ReturnActivityService.Changed += Refresh;
            GameDataManager.ProfileChanged += Refresh;
            _timer = root.schedule.Execute(Tick).Every(1000);
            Refresh();
            ReturnActivityTelemetry.RecordView("home_exposed", "both");
        }

        private void OpenCycle() => _open("cycle");
        private void OpenWeek() => _open("week");
        private void Tick()
        {
            ReturnActivityService.RefreshPeriods();
            ReturnActivityTelemetry.Flush();
            Refresh();
        }

        private void Refresh()
        {
            // Постоянное место и краткие отдельные счётчики сохраняют читаемость Home.
            var state = ReturnActivityService.GetSnapshot();
            string dayPolicyVersion = ReturnActivityService.DayPolicyVersion;
            _cycle.SetEnabled(state != null); _week.SetEnabled(state != null);
            var cycle = _root.Q<Label>("home-cycle-progress");
            var week = _root.Q<Label>("home-week-progress");
            if (state == null) cycle.text = week.text = ActivityUiText.Get("loading");
            else
            {
                int step = state.Step == 7 && state.LastCreditedDay != ActivityDayPolicy.Day(ReturnActivityService.UtcNow, dayPolicyVersion) ? 0 : state.Step;
                cycle.text = Text("retention_cycle_short", step);
                week.text = Text("retention_week_short", Math.Min(state.Week.TargetWins, state.Week.AttemptIds.Count),
                    state.Week.TargetWins, Math.Min(state.Week.TargetDays, state.Week.Days.Count), state.Week.TargetDays);
            }

            // Готовность награды не подменяет уже заработанный прогресс.
            foreach (string kind in new[] { "cycle", "week" })
            {
                var ready = _root.Q<Label>("home-" + kind + "-ready");
                ready.text = Text("retention_ready");
                ready.style.display = state?.Rewards.Any(r => r.Kind == kind && (!r.Claimed || !r.Presented)) == true
                    ? DisplayStyle.Flex : DisplayStyle.None;
            }
            var status = _root.Q<Label>("home-activity-status");
            bool needsAttention = ReturnActivityRecovery.IsRequired || !ReturnActivityService.CanMutate || ReturnActivityService.IsClockBlocked;
            status.text = needsAttention ? ActivityUiText.Status(compact: true) : string.Empty;
            status.style.display = needsAttention ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static string Text(string key, params object[] values) =>
            string.Format(LocalizationManager.GetLocalizedString(key), values);

        public void Dispose()
        {
            _timer.Pause();
            _cycle.clicked -= OpenCycle; _week.clicked -= OpenWeek;
            ReturnActivityService.Changed -= Refresh;
            GameDataManager.ProfileChanged -= Refresh;
        }
    }
}
