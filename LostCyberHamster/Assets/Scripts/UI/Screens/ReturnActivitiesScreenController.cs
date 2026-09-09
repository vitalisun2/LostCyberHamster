using System;
using System.Linq;
using GameManagement;
using UnityEngine.UIElements;
using Vues.GameCore.ReturnActivities;

namespace LostCyberHamster.UI
{
    /// <summary>Показывает две самостоятельные активности и передаёт выбранную квитанцию modal host.</summary>
    public sealed class ReturnActivitiesScreenController : ScreenController
    {
        public static string InitialKind { get; set; } = "cycle";
        private readonly Action<ActivityRewardSnapshot> _showReward;
        private readonly Action _play;
        private IVisualElementScheduledItem _timer;
        private string _rendered;
        protected override ScreenEnum _screenAssetName => ScreenEnum.ReturnActivitiesScreen;
        protected override string ScreenBackgroundAddress => "HomeScreenSprite";

        public ReturnActivitiesScreenController(UIDocument document, Action<ActivityRewardSnapshot> showReward, Action play)
            : base(document) { _showReward = showReward; _play = play; }

        protected override ScreenLayout CreateLayout(VisualElement content) => new(content.Q("return-screen"));
        private Button Button(string name) => _contentRoot.Q<Button>(name);
        private Label Label(string name) => _contentRoot.Q<Label>(name);

        protected override void BindView()
        {
            _rendered = null;
            Label("return-title").text = ActivityUiText.Get("activities");
            Label("return-cycle-title").text = ActivityUiText.Get("cycle");
            Label("return-cycle-rule").text = ActivityUiText.Get("cycle_rule");
            Label("return-week-title").text = ActivityUiText.Get("week");
            Button("return-back").text = ActivityUiText.Get("back");
            Button("return-play").text = ActivityUiText.Get("play");
            Button("return-recover").text = ActivityUiText.Get("recover_action");
            _contentRoot.Q("return-cycle-panel").EnableInClassList("return-panel--selected", InitialKind == "cycle");
            _contentRoot.Q("return-week-panel").EnableInClassList("return-panel--selected", InitialKind == "week");
            Refresh();
            ReturnActivityTelemetry.RecordView("details_opened", InitialKind);
        }

        protected override void OnSubscribeToEvents()
        {
            Button("return-back").clicked += Back;
            Button("return-play").clicked += Play;
            Button("return-cycle-claim").clicked += OpenCycle;
            Button("return-week-claim").clicked += OpenWeek;
            Button("return-recover").clicked += Recover;
            ReturnActivityService.Changed += Refresh;
            GameDataManager.ProfileChanged += Refresh;
            _timer = _contentRoot.schedule.Execute(Tick).Every(1000);
        }

        protected override void OnUnsubscribeFromEvents()
        {
            Button("return-back").clicked -= Back;
            Button("return-play").clicked -= Play;
            Button("return-cycle-claim").clicked -= OpenCycle;
            Button("return-week-claim").clicked -= OpenWeek;
            Button("return-recover").clicked -= Recover;
            ReturnActivityService.Changed -= Refresh;
            GameDataManager.ProfileChanged -= Refresh;
            _timer?.Pause(); _timer = null;
        }

        private void Back() => UIManager.OnScreenShow?.Invoke(ScreenEnum.HomeScreen);
        private void Play() => _play();
        private void OpenCycle() => Open("cycle");
        private void OpenWeek() => Open("week");
        private void Open(string kind)
        {
            var reward = ReturnActivityRewardService.GetRewards().FirstOrDefault(item => item.Kind == kind);
            if (reward != null) _showReward(reward);
        }
        private void Recover()
        {
            if (!ReturnActivityRecovery.RestoreActivityHistory()) Label("return-status").text = ActivityUiText.Get("save_failed");
            else { _rendered = null; Refresh(); }
        }
        private void Tick()
        {
            ReturnActivityService.RefreshPeriods();
            ReturnActivityTelemetry.Flush();
            Refresh();
        }

        private void Refresh()
        {
            var state = ReturnActivityService.GetSnapshot();
            var now = ReturnActivityService.UtcNow;
            Label("return-status").text = ActivityUiText.Status();
            Label("return-calendar").text = ActivityUiText.Get("calendar", ActivityUiText.NextReset(now));
            Button("return-recover").style.display = ReturnActivityRecovery.IsRequired ? DisplayStyle.Flex : DisplayStyle.None;
            if (state == null) return;
            string signature = $"{GameDataManager.ProfileId}/{GameDataManager.Generation}/{state.Revision}/{ActivityDayPolicy.Day(now)}";
            if (_rendered == signature) return;
            _rendered = signature;

            // Слоты показывают заработанное отдельно от факта Claim.
            var config = ReturnActivityConfig.Current;
            int cycle = state.Step == 7 && state.LastCreditedDay != ActivityDayPolicy.Day(now) ? state.Cycle + 1 : state.Cycle;
            int earned = cycle == state.Cycle ? state.Step : 0;
            var amounts = cycle != state.Cycle || state.CycleRewards.Count != 7
                ? config?.Days : state.CycleRewards.ToArray();
            var firstRow = _contentRoot.Q("return-days-first");
            var secondRow = _contentRoot.Q("return-days-second");
            firstRow.Clear(); secondRow.Clear();
            if (amounts != null)
                for (int i = 0; i < 7; i++)
                {
                    var slot = new Label(ActivityUiText.Get("slot", i + 1, ActivityUiText.Amount(amounts[i].Coins, amounts[i].Gems)));
                    slot.AddToClassList("return-day");
                    slot.EnableInClassList("return-day--earned", i < earned);
                    slot.EnableInClassList("return-day--final", i == 6);
                    if (i < earned) slot.text += "\n✓";
                    (i < 4 ? firstRow : secondRow).Add(slot);
                }
            Label("return-cycle-progress").text = ActivityUiText.Get("cycle_progress", cycle, earned);
            Label("return-week-progress").text = HomeActivitySelector.Week(state, now);
            Label("return-week-rule").text = ActivityUiText.Get("week_rule", state.Week.TargetWins, state.Week.TargetDays);
            Label("return-week-reward").text = ActivityUiText.Amount(state.Week.Coins, 0);
            Label("return-week-deadline").text = ActivityUiText.Get("week_deadline", state.Week.Id, ActivityUiText.WeekDeadline(now));
            Label("return-next-week").text = config == null ? ActivityUiText.Get("paused") :
                ActivityUiText.Get("next_week", config.WeeklyWins, config.WeeklyDays, config.WeeklyCoins);
            int possibleDays = (ActivityDayPolicy.WeekStart(now).AddDays(7) - now.Date).Days -
                (state.Week.Days.Contains(ActivityDayPolicy.Day(now)) ? 1 : 0);
            Label("return-week-unreachable").text = !state.Week.Completed && state.Week.TargetDays - state.Week.Days.Count > possibleDays
                ? ActivityUiText.Get("week_too_late") : string.Empty;
            RenderClaim("cycle"); RenderClaim("week");
        }

        private void RenderClaim(string kind)
        {
            var rewards = ReturnActivityRewardService.GetRewards().Where(item => item.Kind == kind).ToArray();
            var button = Button("return-" + kind + "-claim");
            var label = Label("return-" + kind + "-pending");
            button.SetEnabled(rewards.Length > 0 && ReturnActivityService.CanMutate);
            button.text = rewards.FirstOrDefault()?.Claimed == true ? ActivityUiText.Get("view_receipt") : ActivityUiText.Get("claim");
            label.text = rewards.Length == 0 ? ActivityUiText.Get("none_ready") :
                ActivityUiText.Get("pending", rewards.Length, rewards[0].OriginDay,
                    ActivityUiText.Amount(rewards[0].Coins, rewards[0].Gems));
        }
    }
}
