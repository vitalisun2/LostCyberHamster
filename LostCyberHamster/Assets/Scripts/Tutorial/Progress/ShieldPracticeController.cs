using System;
using System.Collections.Generic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;
using GameManagement;
using LostCyberHamster.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Tutorial
{
    /// <summary>Объясняет реальный заряд и применение в текущем забеге; сохраняет только подтверждённое освоение.</summary>
    public sealed class ShieldPracticeController : IDisposable
    {
        private readonly Hamster _hamster;
        private readonly VisualElement _root;
        private readonly NotificationView _view;
        private readonly FirstSessionFocusOutline _focus;
        private readonly List<Rect> _excluded = new();
        private readonly string _profileId;
        private readonly long _generation;
        private readonly string _level;
        private readonly int _equippedAbilityAtStart;
        private readonly float _shieldDuration;
        private NotificationMessage _message;
        private int _phase = -1;
        private float _readSeconds;
        private float _lastTick;
        private bool _wasVisible;
        private bool _success;
        private bool _chargedRecorded;
        private bool _protectedRecorded;
        private bool _pendingUseCommit;
        private bool _commitFailureReported;
        private float _nextCommitAttempt;
        private int _lastCharge = -1;
        private bool _disposed;

        public bool IsPresenting { get; private set; }

        public ShieldPracticeController(Hamster hamster, VisualElement root)
        {
            _hamster = hamster ?? throw new ArgumentNullException(nameof(hamster));
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _view = new NotificationView(root);
            _focus = new FirstSessionFocusOutline(root);
            _profileId = GameDataManager.ProfileId;
            _generation = GameDataManager.Generation;
            _level = GameDataManager.PlayerData?.CurrentLevel;
            _equippedAbilityAtStart = GameDataManager.PlayerData?.ActiveSuperAttackId ?? 0;
            ShieldTutorialProgress.TryGetShieldDuration(out _shieldDuration);
            _lastTick = Time.unscaledTime;
            GameEventsManager.OnUltaUsed += OnUltaUsed;
            _hamster.ProtectedContactEvent.Subscribe(OnProtectedContact);
        }

        public void Tick(bool blocked)
        {
            if (_disposed) return;
            float now = Time.unscaledTime;
            float elapsed = Mathf.Clamp(now - _lastTick, 0, .25f);
            _lastTick = now;
            if (_pendingUseCommit && HasCurrentContext() && now >= _nextCommitAttempt) TryCommitUse();
            if (blocked || !HasCurrentContext() || !ShieldTutorialProgress.IsShieldEquipped ||
                !_hamster.HasSuperAttack || GameDataManager.PlayerData.HasUsedTutorialShield && !_success)
            {
                Hide();
                return;
            }

            // Этапы следуют фактической шкале; её готовность ещё не означает применение.
            int charge = _hamster.UltaChargeAmount.Value;
            int phase = _success ? 3 : charge >= 100 ? 2 : charge > 0 ? 1 : 0;
            if (phase != _phase)
            {
                _phase = phase;
                _readSeconds = 0;
                _message = CreateMessage(phase, charge);
                if (phase == 2 && !_chargedRecorded)
                {
                    _chargedRecorded = true;
                    FirstSessionTelemetry.Record("shield_charged", _level, charge);
                }
            }
            else if (phase == 1 && charge != _lastCharge) _message = CreateMessage(phase, charge);
            _lastCharge = charge;
            if (_wasVisible) _readSeconds += elapsed;

            // Общая плашка пропускает ввод; занятые HUD-области остаются видимыми.
            _excluded.Clear();
            Exclude(_root.Q<Energybar>());
            Exclude(_root.Q<Healthbar>());
            Exclude(_root.Q<Label>("run-score"));
            Exclude(_root.Q<Button>("btn_pause"));
            Exclude(_root.Q<Button>("btn_ultra"));
            Exclude(_root.Q<Button>("btn_jump"));
            bool visible = _readSeconds < 4f && _view.TryShow(_message, _excluded);
            if (!visible) _view.Hide();
            bool focused = phase == 2 && _focus.Show(_root.Q<Button>("btn_ultra"));
            if (phase != 2) _focus.Hide();
            _wasVisible = visible;
            IsPresenting = visible || focused;
        }

        private bool HasCurrentContext() => _hamster != null &&
            _equippedAbilityAtStart == ShieldTutorialProgress.ShieldId &&
            !TutorialStorage.IsPlayerDataBackupActive &&
            string.Equals(_profileId, GameDataManager.ProfileId, StringComparison.Ordinal) &&
            _generation == GameDataManager.Generation &&
            string.Equals(_level, GameDataManager.PlayerData?.CurrentLevel, StringComparison.Ordinal) &&
            !TutorialConstants.IsTutorialLevel(_level) &&
            LevelController.Instance?.LevelData?.Hamster == _hamster;

        private void OnUltaUsed()
        {
            if (_disposed || !HasCurrentContext() || GameDataManager.PlayerData.HasUsedTutorialShield ||
                !ShieldTutorialProgress.IsShieldEquipped || !_hamster.HasSuperAttack) return;

            // UltaUsed публикуется после успешного runtime.TryActivate и расхода заряда.
            _success = true;
            _pendingUseCommit = true;
            TryCommitUse();
            _phase = -1;
            _wasVisible = false;
        }

        private void TryCommitUse()
        {
            _nextCommitAttempt = Time.unscaledTime + 1f;
            try
            {
                ShieldTutorialProgress.MarkUsed();
                _pendingUseCommit = !GameDataManager.PlayerData.HasUsedTutorialShield;
                _commitFailureReported = false;
            }
            catch (Exception exception)
            {
                // Реальное применение уже произошло; повторяем сохранение в этой попытке, сохраняя gameplay.
                if (!_commitFailureReported) Debug.LogException(exception);
                _commitFailureReported = true;
            }
        }

        private void OnProtectedContact(Obstacle obstacle)
        {
            if (_disposed || _protectedRecorded || !_success || !HasCurrentContext() ||
                !ShieldTutorialProgress.IsShieldEquipped || !_hamster.IsProtected.Value) return;

            _protectedRecorded = true;
            FirstSessionTelemetry.Record("shield_protected", _level);
        }

        private NotificationMessage CreateMessage(int phase, int charge)
        {
            string title = phase switch
            {
                3 => FirstSessionGoalPresenter.Text("first_session_shield_used"),
                2 => FirstSessionGoalPresenter.Text("first_session_shield_use"),
                1 => FirstSessionGoalPresenter.Format("first_session_shield_charge_progress", charge),
                _ => FirstSessionGoalPresenter.Text("first_session_shield_charge")
            };
            string detail = phase == 3 && _shieldDuration > 0
                ? FirstSessionGoalPresenter.Format("first_session_shield_protection", _shieldDuration.ToString("0.#"))
                : null;
            return new NotificationMessage("shield-practice-" + phase, title, detail,
                priority: 100, allowedDuringGameplay: true);
        }

        private void Exclude(VisualElement element)
        {
            if (element?.panel != null && element.resolvedStyle.display != DisplayStyle.None &&
                element.worldBound.width > 0 && element.worldBound.height > 0) _excluded.Add(element.worldBound);
        }

        private void Hide()
        {
            _view.Hide();
            _focus.Hide();
            _wasVisible = IsPresenting = false;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            GameEventsManager.OnUltaUsed -= OnUltaUsed;
            if (_hamster != null) _hamster.ProtectedContactEvent.Unsubscribe(OnProtectedContact);
            _focus.Dispose();
            _view.Dispose();
            _wasVisible = IsPresenting = false;
        }
    }
}
