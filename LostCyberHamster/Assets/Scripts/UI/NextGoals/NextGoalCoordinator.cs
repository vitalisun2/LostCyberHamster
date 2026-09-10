using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Assets.Scripts.Tutorial;
using GameManagement;
using UnityEngine;
using Vues.GameCore;
using Vues.GameCore.ReturnActivities;

namespace LostCyberHamster.UI
{
    /// <summary>Выбирает одну рекомендацию после изменения состояния; история показов живёт только в сессии.</summary>
    internal sealed class NextGoalCoordinator : IDisposable
    {
        private static NextGoalCoordinator _instance;
        public static NextGoalCoordinator Instance => _instance ??= new NextGoalCoordinator();
        private readonly INextGoalRule[] _rules =
            { new RewardNextGoalRule(), new DevelopmentNextGoalRule(), new QuestNextGoalRule(), new StageNextGoalRule() };
        private readonly List<NextGoalCandidate> _candidates = new();
        private readonly Dictionary<string, double> _lastShown = new(StringComparer.Ordinal);
        private NextGoalConfiguration _configuration;
        private NextGoalCandidate _onboarding;
        private bool _dirty = true;
        private bool _dismissed;
        private bool _disposed;
        private DateTime _day;
        public int ConfigVersion => _configuration?.configVersion ?? 0;

        private NextGoalCoordinator()
        {
            PlayerProgressCommitter.CommitCompleted += OnCommit;
            GameDataManager.ProfileChanged += ResetProfile;
            GameDataManager.PlayerDataReplaced += Invalidate;
            ReturnActivityService.Changed += Invalidate;
            GameEventsManager.OnQuestStateChanged += OnQuest;
            GameEventsManager.OnDailyQuestSetChanged += Invalidate;
            GameEventsManager.OnStoryQuestSetChanged += Invalidate;
            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            try
            {
                var configuration = await NextGoalConfigurationLoader.LoadAsync();
                if (_disposed) return;
                _configuration = configuration;
                Invalidate();
            }
            catch (Exception exception)
            {
                if (!_disposed) Debug.LogWarning($"Next-goal cards disabled: {exception.Message}");
            }
        }

        public void EnterScreen()
        {
            ReturnActivityService.RefreshPeriods();
            Invalidate();
        }

        public NextGoalCandidate Select(NextGoalCardPlacement placement, string currentId)
        {
            if (_disposed || _dismissed || GameDataManager.PlayerData?.IsTutorialCompleted != true ||
                TutorialStorage.IsPlayerDataBackupActive) return null;
            Refresh();

            // Сохраняем обязательную последовательность первой сессии, затем используем четыре общих случая.
            var onboarding = _onboarding;
            if (onboarding != null) return onboarding;
            if (_configuration?.enabled != true) return null;
            return _candidates.Where(candidate => CanShow(candidate, placement) && IsDue(candidate, currentId))
                .OrderByDescending(candidate => _configuration.Find(candidate.Kind).priority)
                .ThenByDescending(candidate => candidate.Id == currentId)
                .ThenByDescending(candidate => candidate.Progress ?? 0)
                .ThenBy(candidate => candidate.Id, StringComparer.Ordinal).FirstOrDefault();
        }

        public NextGoalCandidate Resolve(NextGoalCandidate shown, NextGoalCardPlacement placement)
        {
            if (_disposed || _dismissed || shown?.IsCurrentProfile != true) return null;
            // Клик заново читает владельцев состояния и никогда не подменяет цель другой рекомендацией.
            Invalidate();
            Refresh();
            if (shown.IsOnboarding)
            {
                var current = _onboarding;
                return current?.Id == shown.Id ? current : null;
            }
            return _candidates.FirstOrDefault(candidate => candidate.Id == shown.Id && CanShow(candidate, placement));
        }

        public void MarkShown(NextGoalCandidate goal, NextGoalCardPlacement placement)
        {
            if (goal?.IsCurrentProfile != true) return;
            _lastShown[goal.Id] = Time.realtimeSinceStartupAsDouble;
            FirstSessionTelemetry.Record("next_goal_shown", $"{placement}|{goal.Id}", ConfigVersion);
        }

        public void Dismiss(NextGoalCandidate goal)
        {
            if (goal?.IsCurrentProfile != true) return;
            _dismissed = true;
            FirstSessionTelemetry.Record("next_goal_dismissed", goal.Id, ConfigVersion);
        }

        private bool CanShow(NextGoalCandidate candidate, NextGoalCardPlacement placement)
        {
            if (_configuration?.enabled != true) return false;
            var rule = _configuration.Find(candidate.Kind);
            // Home уже показывает готовые награды активностей в своём блоке.
            return rule.enabled && rule.screens.Contains(placement.ToString()) &&
                !(placement == NextGoalCardPlacement.Home && candidate.Action == NextGoalAction.ActivityReward);
        }

        private bool IsDue(NextGoalCandidate candidate, string currentId) => candidate.Id == currentId ||
            !_lastShown.TryGetValue(candidate.Id, out double last) ||
            Time.realtimeSinceStartupAsDouble - last >= (_configuration?.repeatAfterSeconds ?? 300);

        private void Refresh()
        {
            // Смена игрового дня также обновляет квестовые кандидаты; периоды рассчитывает владелец активности.
            var day = ReturnActivityService.UtcNow.Date;
            if (day != _day) { _day = day; _dirty = true; ReturnActivityService.RefreshPeriods(); }
            if (!_dirty || GameDataManager.PlayerData == null) return;
            _dirty = false;
            _candidates.Clear();
            _onboarding = BuildOnboarding();
            if (_configuration?.enabled != true) return;
            foreach (var rule in _rules)
                if (_configuration.Find(rule.Kind).enabled) rule.Collect(_candidates, _configuration);
        }

        private static NextGoalCandidate BuildOnboarding()
        {
            var goal = FirstSessionGoalPresenter.GetCurrent();
            if (!goal.HasValue) return null;
            var current = goal.Value;
            var kind = current.Id == "claim" ? NextGoalKind.Reward : current.Id == "morning" ? NextGoalKind.Quest :
                current.Id.StartsWith("shield", StringComparison.Ordinal) ? NextGoalKind.Development : NextGoalKind.Stage;
            return new NextGoalCandidate(kind, NextGoalAction.Onboarding, current.Id, current.Text, current.Detail,
                current.ActionText, current.Destination, current.Progress, startsShieldLesson: current.BeginsShieldLesson);
        }

        private void OnCommit(CheckpointReason _) => Invalidate();
        private void OnQuest(string _) => Invalidate();
        private void Invalidate() => _dirty = true;
        private void ResetProfile()
        {
            _dismissed = false;
            _lastShown.Clear();
            _candidates.Clear();
            Invalidate();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            PlayerProgressCommitter.CommitCompleted -= OnCommit;
            GameDataManager.ProfileChanged -= ResetProfile;
            GameDataManager.PlayerDataReplaced -= Invalidate;
            ReturnActivityService.Changed -= Invalidate;
            GameEventsManager.OnQuestStateChanged -= OnQuest;
            GameEventsManager.OnDailyQuestSetChanged -= Invalidate;
            GameEventsManager.OnStoryQuestSetChanged -= Invalidate;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            _instance?.Dispose();
            _instance = null;
        }
    }
}
