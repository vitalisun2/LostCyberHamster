using System;
using GameManagement;
using LostCyberHamster.UI;
using UnityEngine;
using UnityEngine.UIElements;
using Vues.GameCore;

namespace Assets.Scripts.Tutorial
{
    /// <summary>Проводит по реальным Skills/Hero и сохраняет свободный выход из урока между забегами.</summary>
    public sealed class ShieldOnboardingController : IDisposable
    {
        private readonly UIManager _ui;
        private readonly VisualElement _root;
        private readonly FirstSessionCoachView _view;
        private readonly FirstSessionFocusOutline _focus;
        private string _profileId;
        private string _lastPresentation;
        private ScreenEnum _lastScreen;
        private bool _deferred;
        private bool _disposed;
        private float _nextRefresh;

        public bool IsPresenting { get; private set; }

        public static bool IsShieldAvailableToLearn => GameDataManager.PlayerData != null &&
            !GameDataManager.PlayerData.HasUsedTutorialShield &&
            (ShieldTutorialProgress.IsShieldUnlocked ||
             CharacterDevelopmentService.CanUnlockSuperAttack(ShieldTutorialProgress.ShieldId));

        public ShieldOnboardingController(UIManager ui, VisualElement root)
        {
            _ui = ui ?? throw new ArgumentNullException(nameof(ui));
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _view = new FirstSessionCoachView(root);
            _focus = new FirstSessionFocusOutline(root);
            _profileId = GameDataManager.ProfileId;
        }

        public void Tick(bool blocked = false)
        {
            if (_disposed) return;
            var data = GameDataManager.PlayerData;
            if (blocked || data == null || _ui.HasModalOrTransition || _ui.HasPriorityPresentation ||
                TutorialStorage.IsPlayerDataBackupActive ||
                TutorialConstants.IsTutorialLevel(data.CurrentLevel))
            {
                Hide();
                _nextRefresh = 0;
                return;
            }

            // Новый профиль и явный возврат в Skills/Hero снимают только временное откладывание.
            bool screenChanged = _lastScreen != _ui.CurrentScreen;
            if (!string.Equals(_profileId, GameDataManager.ProfileId, StringComparison.Ordinal))
            {
                _profileId = GameDataManager.ProfileId;
                _deferred = false;
                _lastPresentation = null;
            }
            if (screenChanged && (_ui.CurrentScreen == ScreenEnum.CharacterDevelopmentScreen ||
                                  _ui.CurrentScreen == ScreenEnum.CharacterScreen)) _deferred = false;
            _lastScreen = _ui.CurrentScreen;
            if (!screenChanged && Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + .2f;
            Hide();

            bool lessonScreen = _ui.CurrentScreen == ScreenEnum.CharacterDevelopmentScreen ||
                                _ui.CurrentScreen == ScreenEnum.CharacterScreen ||
                                _ui.CurrentScreen == ScreenEnum.HomeScreen && FirstSessionNavigation.HasReturnRoute;
            if (ShieldTutorialProgress.IsPending && !_deferred && lessonScreen)
            {
                ShowLesson();
                return;
            }

        }

        private void ShowLesson()
        {
            if (!ShieldTutorialProgress.IsShieldUnlocked)
            {
                if (GameDataManager.PlayerData.DevelopmentPoints <= 0)
                {
                    ShowGuide("first_session_shield_wait", null, "first_session_continue", Continue);
                    return;
                }
                if (_ui.CurrentScreen != ScreenEnum.CharacterDevelopmentScreen)
                {
                    ShowGuide("first_session_shield_offer", "first_session_shield_unlock",
                        "first_session_skills", OpenSkills);
                    return;
                }
                ShowGuide("first_session_shield_offer", "first_session_shield_unlock", null, null,
                    _root.Q<VisualElement>("development-ability-card-1")?.Q<Button>(className: "development-card__unlock"));
                return;
            }

            if (ShieldTutorialProgress.IsShieldEquipped)
            {
                ShowGuide("first_session_shield_ready", null, "first_session_continue", Continue);
                return;
            }
            if (_ui.CurrentScreen == ScreenEnum.CharacterDevelopmentScreen)
            {
                ShowGuide("first_session_shield_equipment", null, null, null,
                    _root.Q<Button>("development__btn-equipment"));
                return;
            }
            if (_ui.CurrentScreen == ScreenEnum.CharacterScreen)
            {
                CharacterScreenController hero = _ui.GetController<CharacterScreenController>();
                bool abilities = hero != null && hero.IsAbilityTabShown;
                VisualElement target = !abilities ? _root.Q<Button>("hero-tab-abilities") :
                    hero.IsShieldSelectedForTutorial ? _root.Q<Button>("hero-ability-select") :
                    _root.Q<VisualElement>("hero-ability-slot-1");
                ShowGuide(abilities ? "first_session_shield_equip" : "first_session_shield_tab",
                    null, null, null, target);
                return;
            }
            ShowGuide("first_session_shield_equipment", null, "first_session_hero",
                () => UIManager.OnScreenShow?.Invoke(ScreenEnum.CharacterScreen));
        }

        private void ShowGuide(string titleKey, string detailKey, string actionKey, Action action,
            VisualElement target = null)
        {
            Rect? targetRect = target?.panel == null ? null : _root.WorldToLocal(target.worldBound);
            IsPresenting = _view.Show(Text(titleKey), detailKey == null ? null : Text(detailKey),
                actionKey == null ? null : Text(actionKey), action,
                actionKey == "first_session_continue" ? null : Text("first_session_later"), Later, target: targetRect);
            if (!IsPresenting) return;
            _focus.Show(target);
            TrackPresentation("lesson:" + titleKey, "shield_step_shown", titleKey);
        }

        private void OpenSkills() => UIManager.OnScreenShow?.Invoke(ScreenEnum.CharacterDevelopmentScreen);

        private void Later()
        {
            if (TryContinue()) FirstSessionTelemetry.Record("shield_later");
        }

        private void Continue() => TryContinue();

        private bool TryContinue()
        {
            if (_disposed || _ui.HasModalOrTransition ||
                !string.Equals(_profileId, GameDataManager.ProfileId, StringComparison.Ordinal)) return false;
            try
            {
                // Сначала фиксируем возвращение; при ошибке записи подсказка остаётся доступной.
                if (FirstSessionNavigation.HasReturnRoute) FirstSessionNavigation.Resume(_ui);
                else UIManager.OnScreenShow?.Invoke(ScreenEnum.HomeScreen);
                _deferred = true;
                Hide();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
        }

        private void TrackPresentation(string identity, string phase, string detail)
        {
            if (_lastPresentation == identity) return;
            _lastPresentation = identity;
            FirstSessionTelemetry.Record(phase, detail);
        }

        private static string Text(string key) => FirstSessionGoalPresenter.Text(key);

        private void Hide()
        {
            IsPresenting = false;
            _view.Hide();
            _focus.Hide();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            IsPresenting = false;
            _focus.Dispose();
            _view.Dispose();
        }
    }
}
