using System;
using Assets.Scripts.Tutorial;
using GameManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Vues.GameCore;

namespace LostCyberHamster.UI
{
    /// <summary>Возвращает из общего развития по сохранённому маршруту текущего профиля.</summary>
    internal sealed class DevelopmentReturnButton : IDisposable
    {
        private readonly Button _button;
        private readonly Action _callback;

        public DevelopmentReturnButton(Button button, Action resume)
        {
            _button = button;
            if (button == null) return;
            var profile = GameDataManager.ProfileId;
            var generation = GameDataManager.Generation;
            button.text = LocalizationManager.GetLocalizedString("first_session_continue");
            button.style.display = resume != null && FirstSessionNavigation.HasReturnRoute && !ShieldTutorialProgress.IsPending
                ? DisplayStyle.Flex : DisplayStyle.None;
            _callback = () =>
            {
                if (profile != GameDataManager.ProfileId || generation != GameDataManager.Generation ||
                    !FirstSessionNavigation.HasReturnRoute) return;
                button.SetEnabled(false);
                try { resume?.Invoke(); }
                catch (Exception exception) { Debug.LogException(exception); button.SetEnabled(true); }
            };
            button.clicked += _callback;
        }

        public void Dispose()
        {
            if (_button != null) _button.clicked -= _callback;
        }
    }
}
