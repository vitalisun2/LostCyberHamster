using System;
using System.Linq;
using GameManagement;
using UnityEngine.UIElements;
using Vues.GameCore.ReturnActivities;

namespace LostCyberHamster.UI
{
    /// <summary>Привязывает две строки Home к снимку и освобождает подписки вместе с деревом.</summary>
    internal sealed class HomeActivityPresenter : IDisposable
    {
        private readonly VisualElement _root;
        private readonly Action<string> _open;
        private readonly Button _cycle;
        private readonly Button _week;
        private readonly Label _status;
        private readonly Label _heading;
        private readonly IVisualElementScheduledItem _timer;

        public HomeActivityPresenter(VisualElement root, Action<string> open)
        {
            _root = root; _open = open;
            _cycle = root.Q<Button>("home-activity-cycle");
            _week = root.Q<Button>("home-activity-week");
            _status = root.Q<Label>("home-activity-status");
            _heading = root.Q<Label>("home-activity-heading");
            _cycle.clicked += OpenCycle; _week.clicked += OpenWeek;
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
            var state = ReturnActivityService.GetSnapshot();
            _status.text = ActivityUiText.Status(compact: true);
            _heading.text = ActivityUiText.Get("activities");
            _cycle.SetEnabled(state != null); _week.SetEnabled(state != null);
            if (state == null) { _cycle.text = _week.text = ActivityUiText.Get("loading"); return; }

            // Порядок меняется только при смене достижимого действия, target сохраняет свой kind.
            var utc = ReturnActivityService.UtcNow;
            _cycle.text = HomeActivitySelector.Cycle(state, utc);
            _week.text = HomeActivitySelector.Week(state, utc);
            bool weekFirst = HomeActivitySelector.WeekFirst(state, utc);
            var first = weekFirst ? _week : _cycle;
            var last = weekFirst ? _cycle : _week;
            if (_root.IndexOf(first) > _root.IndexOf(last)) first.PlaceBehind(last);
            _cycle.EnableInClassList("return-home__row--ready", state.Rewards.Any(reward => reward.Kind == "cycle" && !reward.Claimed));
            _week.EnableInClassList("return-home__row--ready", state.Rewards.Any(reward => reward.Kind == "week" && !reward.Claimed));
            int count = state.Rewards.Count(reward => !reward.Claimed);
            if (count > 0) _heading.text = ActivityUiText.Get("ready_count", count);
        }

        public void Dispose()
        {
            _timer.Pause();
            _cycle.clicked -= OpenCycle; _week.clicked -= OpenWeek;
            ReturnActivityService.Changed -= Refresh;
            GameDataManager.ProfileChanged -= Refresh;
        }
    }
}
