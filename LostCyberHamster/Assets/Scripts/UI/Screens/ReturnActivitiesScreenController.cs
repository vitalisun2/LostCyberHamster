using System;
using System.Linq;
using GameManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Vues.GameCore.ReturnActivities;

namespace LostCyberHamster.UI
{
    /// <summary>Показывает выбранную активность и выдаёт её награду с отдельным подтверждением квитанции.</summary>
    public sealed class ReturnActivitiesScreenController : ScreenController
    {
        public static string InitialKind { get; set; } = "cycle";
        private readonly Action _play;
        private string _kind;
        private string _rendered;
        private bool _busy;
        private StyleColor _backgroundTint;
        private ActivityRewardSnapshot _actionReward;
        private ActivityRewardSnapshot _receipt;
        private IVisualElementScheduledItem _timer;
        protected override ScreenEnum _screenAssetName => ScreenEnum.ReturnActivitiesScreen;
        protected override string ScreenBackgroundAddress => "HomeScreenSprite";

        public ReturnActivitiesScreenController(UIDocument document, Action<ActivityRewardSnapshot> showReward, Action play)
            : base(document) => _play = play;

        protected override ScreenLayout CreateLayout(VisualElement content) => ScreenLayout.Fit(
            content.Q("return-viewport"), content.Q("return-scale-frame"), content.Q("return-screen"), new Vector2(1844, 853));
        private Button Button(string name) => _contentRoot.Q<Button>(name);
        private Label Label(string name) => _contentRoot.Q<Label>(name);
        private static string Text(string key, params object[] values) =>
            string.Format(LocalizationManager.GetLocalizedString(key), values);

        protected override void BindView()
        {
            // Каждая точка входа показывает только свою активность.
            _kind = InitialKind == "week" ? "week" : "cycle";
            _contentRoot.Q("return-screen").EnableInClassList("return-screen--week", _kind == "week");
            _rendered = null; _busy = false; _receipt = null;
            Label("return-title").text = ActivityUiText.Get(_kind).ToUpperInvariant();
            Label("return-cycle-rule").text = Text("retention_cycle_rule");
            Label("return-wins-title").text = Text("retention_wins");
            Label("return-days-title").text = Text("retention_days");
            Button("return-recover").text = ActivityUiText.Get("recover_action");
            _contentRoot.Q("return-cycle-panel").style.display = _kind == "cycle" ? DisplayStyle.Flex : DisplayStyle.None;
            _contentRoot.Q("return-week-panel").style.display = _kind == "week" ? DisplayStyle.Flex : DisplayStyle.None;
            Refresh();
            ReturnActivityTelemetry.RecordView("details_opened", _kind);
        }

        protected override void OnSubscribeToEvents()
        {
            _backgroundTint = _background.style.unityBackgroundImageTintColor;
            _background.style.unityBackgroundImageTintColor = new Color(0.57f, 0.62f, 0.64f);
            Button("return-back").clicked += Back;
            Button("return-action").clicked += Act;
            Button("return-recover").clicked += Recover;
            ReturnActivityService.Changed += Refresh;
            GameDataManager.ProfileChanged += ProfileChanged;
            _timer = _contentRoot.schedule.Execute(Tick).Every(1000);
        }

        protected override void OnUnsubscribeFromEvents()
        {
            _background.style.unityBackgroundImageTintColor = _backgroundTint;
            Button("return-back").clicked -= Back;
            Button("return-action").clicked -= Act;
            Button("return-recover").clicked -= Recover;
            ReturnActivityService.Changed -= Refresh;
            GameDataManager.ProfileChanged -= ProfileChanged;
            _timer?.Pause(); _timer = null;
        }

        private void ProfileChanged() { _receipt = null; _actionReward = null; _rendered = null; Refresh(); }
        private void Back()
        {
            if (_busy || !AcceptReceipt()) return;
            UIManager.OnScreenShow?.Invoke(ScreenEnum.HomeScreen);
        }

        private void Act()
        {
            if (_busy) return;
            if (_receipt != null)
            {
                if (AcceptReceipt()) { _rendered = null; Refresh(); }
                return;
            }
            var expected = _actionReward;
            if (expected == null) { _play(); return; }
            _busy = true;
            Button("return-action").SetEnabled(false);

            // Нажатие получает только показанную награду; квитанция остаётся видимой до ACK.
            var result = ReturnActivityRewardService.Claim(expected);
            _busy = false;
            if (result == ActivityClaimResult.Claimed || result == ActivityClaimResult.AlreadyClaimed)
            {
                _receipt = expected;
                _rendered = null;
                Refresh();
            }
            else
            {
                Refresh();
                Label("return-status").text = ActivityUiText.Get(result == ActivityClaimResult.SaveFailed ? "save_failed" : "unavailable");
            }
        }

        private bool AcceptReceipt()
        {
            if (_receipt == null) return true;
            if (!_receipt.IsCurrent) { _receipt = null; return true; }
            _busy = true;
            bool accepted = ReturnActivityRewardService.Acknowledge(_receipt);
            _busy = false;
            if (accepted) _receipt = null;
            else Label("return-status").text = ActivityUiText.Get("save_failed");
            return accepted;
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
            if (_busy) return;
            var state = ReturnActivityService.GetSnapshot();
            var now = ReturnActivityService.UtcNow;
            var policyNow = ReturnActivityService.PolicyNow;
            string dayPolicyVersion = ReturnActivityService.DayPolicyVersion;
            _contentRoot.Q("return-screen").EnableInClassList("return-screen--recovery", ReturnActivityRecovery.IsRequired);
            bool warning = !ReturnActivityService.CanMutate || ReturnActivityRecovery.IsRequired || ReturnActivityService.IsClockBlocked;
            Label("return-status").text = warning ? ActivityUiText.Status() : string.Empty;
            Button("return-recover").style.display = ReturnActivityRecovery.IsRequired ? DisplayStyle.Flex : DisplayStyle.None;
            if (state == null) { Button("return-action").SetEnabled(false); return; }
            string dayId = ActivityDayPolicy.Day(now, dayPolicyVersion);
            string signature = $"{GameDataManager.ProfileId}/{GameDataManager.Generation}/{state.Revision}/{dayId}/{_receipt?.Id}";
            if (_rendered == signature) { RefreshAction(); return; }
            _rendered = signature;

            // Показываем заработанные дни текущего цикла, включая уже подтверждённые награды.
            int cycle = state.Step == 7 && state.LastCreditedDay != dayId ? state.Cycle + 1 : state.Cycle;
            int earned = cycle == state.Cycle ? state.Step : 0;
            var amounts = cycle != state.Cycle || state.CycleRewards.Count != 7 ? ReturnActivityConfig.Current?.Days : state.CycleRewards.ToArray();
            var days = _contentRoot.Q("return-days-first");
            days.Clear();
            if (amounts != null)
                for (int i = 0; i < 7; i++)
                {
                    var reward = state.Rewards.FirstOrDefault(r => r.Kind == "cycle" && r.Cycle == cycle && r.Step == i + 1);
                    bool ready = i < earned && reward?.Claimed == false;
                    var day = new VisualElement { pickingMode = PickingMode.Ignore };
                    day.AddToClassList("return-day");
                    var title = new Label(Text("retention_day", i + 1)); title.AddToClassList("return-day__title"); day.Add(title);
                    var stamp = new VisualElement(); stamp.AddToClassList("return-day__stamp");
                    stamp.EnableInClassList("return-day__stamp--earned", i < earned && !ready);
                    stamp.EnableInClassList("return-day__stamp--ready", ready);
                    var coin = new VisualElement(); coin.AddToClassList("return-day__coin"); stamp.Add(coin);
                    if (amounts[i].Gems > 0) { var gem = new VisualElement(); gem.AddToClassList("return-day__gem"); stamp.Add(gem); }
                    if (i < earned)
                    {
                        var mark = new Label(ready ? Text("retention_ready") : "✓");
                        mark.AddToClassList(ready ? "return-day__ready" : "return-day__check"); stamp.Add(mark);
                    }
                    day.Add(stamp);
                    var amount = new Label(ActivityUiText.Amount(amounts[i].Coins, amounts[i].Gems)); amount.AddToClassList("return-day__amount"); day.Add(amount);
                    days.Add(day);
                }
            Label("return-cycle-progress").text = Text("retention_cycle_progress", earned);
            Label("return-calendar").text = _kind == "cycle"
                ? Text("retention_daily_reset", ActivityUiText.NextReset(now, dayPolicyVersion))
                : ActivityUiText.Get("week_rule", state.Week.TargetWins, state.Week.TargetDays);

            // Победы и разные дни имеют самостоятельные счётчики и шкалы.
            var week = state.Week;
            int wins = Math.Min(week.TargetWins, week.AttemptIds.Count);
            int activeDays = Math.Min(week.TargetDays, week.Days.Count);
            RenderProgress("wins", wins, week.TargetWins);
            RenderProgress("days", activeDays, week.TargetDays);
            var weekEnd = ActivityDayPolicy.WeekStart(now, dayPolicyVersion).AddDays(7);
            Label("return-week-deadline").text = Text("retention_deadline", Math.Max(0, (int)Math.Ceiling((weekEnd - policyNow).TotalDays)));
            int remainingDays = Math.Max(0, week.TargetDays - activeDays);
            int possibleDays = (weekEnd - policyNow.Date).Days - (week.Days.Contains(dayId) ? 1 : 0);
            Label("return-week-hint").text = week.Completed ? Text("retention_week_done") :
                remainingDays > possibleDays ? ActivityUiText.Get("week_too_late") :
                wins == week.TargetWins && remainingDays > 0 ? Text("retention_more_days", remainingDays) : Text("retention_more_wins", Math.Max(0, week.TargetWins - wins));

            // FIFO сохраняется между циклами и неделями; новые периоды не заменяют состав старого приза.
            var pending = ReturnActivityRewardService.GetRewards().Where(r => r.Kind == _kind).ToArray();
            _actionReward = pending.FirstOrDefault();
            if (_actionReward?.Claimed == true && _receipt == null) _receipt = _actionReward;
            var shown = _receipt ?? _actionReward;
            Label("return-reward-title").text = _kind == "week" ? Text("retention_week_reward") : Text("retention_day_reward", shown?.Step ?? Math.Min(7, earned + 1));
            int next = Math.Min(earned, 6);
            Label("return-reward-amount").text = shown != null ? ActivityUiText.Amount(shown.Coins, shown.Gems) :
                _kind == "week" ? ActivityUiText.Amount(week.Coins, 0) : amounts == null ? string.Empty : ActivityUiText.Amount(amounts[next].Coins, amounts[next].Gems);
            Label("return-pending").text = pending.Length > 1 ? Text("retention_more_rewards", pending.Length - 1) : string.Empty;
            RefreshAction();
        }

        private void RenderProgress(string id, int current, int target)
        {
            Label("return-" + id + "-count").text = $"{current} / {target}";
            var slots = _contentRoot.Q("return-" + id + "-slots");
            slots.Clear();
            for (int i = 0; i < target; i++)
            {
                var slot = new Label(i < current ? "✓" : string.Empty) { pickingMode = PickingMode.Ignore };
                slot.AddToClassList("return-week-slot");
                slot.EnableInClassList("return-week-slot--complete", i < current);
                slots.Add(slot);
            }
        }

        private void RefreshAction()
        {
            Button("return-action").text = _receipt != null ? ActivityUiText.Get("done") :
                _actionReward != null ? ActivityUiText.Get("claim") : ActivityUiText.Get("play");
            Button("return-action").SetEnabled(!_busy && (_actionReward == null || ReturnActivityService.CanMutate));
            if (_receipt != null) Label("return-status").text = ActivityUiText.Get("received");
        }
    }
}
