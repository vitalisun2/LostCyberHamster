using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Assets.Scripts.System.Resources;
using Assets.Scripts.Tutorial;
using GameManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace LostCyberHamster.UI
{
    /// <summary>Связывает карточку с координатором и держит графику только на время жизни экрана.</summary>
    internal sealed class NextGoalCardPresenter : IDisposable
    {
        private readonly NextGoalCardView _view;
        private readonly NextGoalCardPlacement _placement;
        private readonly Action<NextGoalCandidate> _open;
        private readonly NextGoalCoordinator _coordinator;
        private readonly Dictionary<NextGoalKind, AddressableLease<Sprite>> _icons = new();
        private readonly CancellationTokenSource _loading = new();
        private NextGoalCandidate _goal;
        private string _shownId;
        private string _shownProfile;
        private long _shownGeneration;
        private double _nextRefresh;
        private bool _disposed;
        private int _iconVersion;
        private int _renderedIconVersion = -1;
        public bool IsVisible => _view.IsVisible;
        public bool HasGoal => _goal != null;

        public NextGoalCardPresenter(VisualElement host, NextGoalCardPlacement placement, Action<NextGoalCandidate> open)
        {
            _view = new NextGoalCardView(host);
            _placement = placement;
            _open = open;
            _coordinator = NextGoalCoordinator.Instance;
            _coordinator.EnterScreen();
            _ = LoadIconsAsync();
        }

        private async Task LoadIconsAsync()
        {
            foreach (NextGoalKind kind in Enum.GetValues(typeof(NextGoalKind)))
            {
                if (_disposed) return;
                var suffix = kind == NextGoalKind.Development ? "development" : kind.ToString().ToLowerInvariant();
                try
                {
                    var lease = await AddressableLoader.LoadAssetAsync<Sprite>("goal_" + suffix, _loading.Token);
                    if (_disposed) { lease.Dispose(); return; }
                    _icons.Add(kind, lease);
                    _iconVersion++;
                }
                catch (OperationCanceledException) { return; }
                catch (Exception exception) { Debug.LogWarning($"Next-goal icon unavailable: {suffix} ({exception.GetType().Name})."); }
            }
        }

        public void Tick(bool blocked = false)
        {
            if (_disposed) return;
            if (blocked || !Application.isFocused || GameDataManager.PlayerData == null ||
                TutorialStorage.IsPlayerDataBackupActive)
            {
                _view.Hide();
                _nextRefresh = 0;
                return;
            }

            // Геометрия обновляется отдельно от правил; кандидаты пересчитываются только после invalidation.
            double now = Time.realtimeSinceStartupAsDouble;
            if (now < _nextRefresh) { MarkVisible(); return; }
            _nextRefresh = now + .2;
            var goal = _coordinator.Select(_placement, _goal?.IsCurrentProfile == true ? _goal.Id : null);
            if (goal == null) { _goal = null; _view.Hide(); return; }
            bool changed = goal != _goal || _iconVersion != _renderedIconVersion || !_view.IsVisible;
            _goal = goal;
            if (changed)
            {
                var icon = goal.Icon;
                if (icon == null && _icons.TryGetValue(goal.Kind, out var lease)) icon = lease.Value;
                if (icon == null) { _view.Hide(); return; }
                _view.Show(goal.Text, goal.Detail, goal.ActionText, new StyleBackground(icon), Open, Dismiss,
                    _placement, NextGoalText.Get("next_goal_heading"), goal.Progress);
                _renderedIconVersion = _iconVersion;
            }
            MarkVisible();
        }

        private void MarkVisible()
        {
            if (!_view.RefreshLayout() || _goal?.IsCurrentProfile != true) return;
            if (_shownId == _goal.Id && _shownProfile == _goal.ProfileId && _shownGeneration == _goal.Generation) return;
            _shownId = _goal.Id;
            _shownProfile = _goal.ProfileId;
            _shownGeneration = _goal.Generation;
            _coordinator.MarkShown(_goal, _placement);
        }

        private void Open()
        {
            var current = _coordinator.Resolve(_goal, _placement);
            if (current == null) { _goal = null; _view.Hide(); _nextRefresh = 0; return; }
            FirstSessionTelemetry.Record("next_goal_clicked", $"{_placement}|{current.Id}", _coordinator.ConfigVersion);
            _open(current);
        }

        private void Dismiss()
        {
            _coordinator.Dismiss(_goal);
            _view.Hide();
            _goal = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _loading.Cancel();
            _loading.Dispose();
            _view.Dispose();
            foreach (var lease in _icons.Values) lease.Dispose();
            _icons.Clear();
        }
    }
}
