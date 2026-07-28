using System;
using System.Threading.Tasks;
using Assets.Scripts.System.Resources;
using UnityEngine;
using UnityEngine.UIElements;
using Vues.GameCore;

namespace LostCyberHamster.UI
{
    /// <summary>
    /// Показывает новый уровень и открытый суперудар перед выполнением выбранного действия.
    /// </summary>
    public sealed class LevelUpModalController : ModalController
    {
        private readonly Action _closeAction;

        private int _previousLevel;
        private int _currentLevel;
        private SuperAttackData _superAttack;
        private Action _okAction;
        private AddressableLease<Sprite> _iconLease;

        private Label Title =>
            _modalContent.Q<Label>("level-up-title");

        private Label Transition =>
            _modalContent.Q<Label>("level-up-transition");

        private VisualElement AttackIcon =>
            _modalContent.Q<VisualElement>("level-up-attack-icon");

        private Label AttackName =>
            _modalContent.Q<Label>("level-up-attack-name");

        private Label AttackStatus =>
            _modalContent.Q<Label>("level-up-attack-status");

        private Button OkButton =>
            _modalContent.Q<Button>("btn_level_up_ok");

        protected override ScreenEnum _modalAssetName =>
            ScreenEnum.LevelUpModal;

        public LevelUpModalController(
            UIDocument uiDocument,
            Action closeAction)
            : base(uiDocument)
        {
            _closeAction = closeAction ??
                throw new ArgumentNullException(nameof(closeAction));
        }

        public void SetLevelUpData(
            int previousLevel,
            int currentLevel,
            SuperAttackData superAttack)
        {
            _previousLevel = previousLevel;
            _currentLevel = currentLevel;
            _superAttack = superAttack ??
                throw new ArgumentNullException(nameof(superAttack));
        }

        public void SetOkAction(Action action)
        {
            _okAction = action;
        }

        protected override async Task OnShowAsync()
        {
            ReleaseIcon();
            _buttonCloseModal.style.display = DisplayStyle.None;

            Title.text = Localize("level_up_title");
            Transition.text = FormatLocalized(
                "level_up_transition",
                _previousLevel.ToString(),
                _currentLevel.ToString());
            OkButton.text = Localize("level_up_ok");

            AttackName.text = Localize(
                _superAttack.NameLocalizationKey);
            AttackStatus.text = Localize(
                "level_up_attack_available");

            if (string.IsNullOrWhiteSpace(_superAttack.IconAddress))
            {
                return;
            }

            AddressableLease<Sprite> lease = null;
            try
            {
                lease = await AddressableLoader.LoadAssetAsync<Sprite>(
                    _superAttack.IconAddress);
                if (lease.Value == null)
                {
                    lease.Dispose();
                    return;
                }

                _iconLease = lease;
                AttackIcon.style.backgroundImage =
                    new StyleBackground(lease.Value);
            }
            catch (Exception exception)
            {
                lease?.Dispose();
                Debug.LogError(
                    $"Could not load level-up icon " +
                    $"'{_superAttack.IconAddress}': {exception.Message}");
            }
        }

        protected override void OnSubscribeToEvents()
        {
            OkButton?.RegisterCallback<ClickEvent>(OnOkClicked);
        }

        protected override void OnUnsubscribeFromEvents()
        {
            OkButton?.UnregisterCallback<ClickEvent>(OnOkClicked);
            ReleaseIcon();
        }

        private void OnOkClicked(ClickEvent clickEvent)
        {
            Action queuedAction = _okAction;
            _okAction = null;

            _closeAction.Invoke();
            queuedAction?.Invoke();
        }

        private void ReleaseIcon()
        {
            _iconLease?.Dispose();
            _iconLease = null;

            VisualElement icon = AttackIcon;
            if (icon != null)
            {
                icon.style.backgroundImage = null;
            }
        }

        private static string Localize(string key)
        {
            string localized =
                LocalizationManager.GetLocalizedString(key);
            return string.IsNullOrWhiteSpace(localized)
                ? key ?? string.Empty
                : localized;
        }

        private static string FormatLocalized(
            string key,
            params string[] values)
        {
            string template = Localize(key);
            for (int index = 0; index < values.Length; index++)
            {
                template = template.Replace(
                    $"{{{index}}}",
                    values[index] ?? string.Empty);
            }

            return template;
        }
    }
}
