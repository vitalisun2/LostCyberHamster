using System.Threading.Tasks;
using Assets.Scripts.Account;
using Assets.Scripts.System;
using GameManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Vues.GameCore;

namespace LostCyberHamster.UI
{
    public class SettingsModalController : ModalController
    {
        protected override ScreenEnum _modalAssetName => ScreenEnum.SettingsModal;

        private DropdownField _dropdownLanguages => _modalContent.Q<DropdownField>("settings__dd-languages");
        private Toggle _toggleMusic => _modalContent.Q<Toggle>("settings__cbx-music");
        private Toggle _toggleSound => _modalContent.Q<Toggle>("settings__cbx-sound");
        private Toggle _toggleVibration => _modalContent.Q<Toggle>("settings__cbx-vibrate");
        private Label _labelVersion => _modalContent.Q<Label>("settings__lbl-version");
        private Label _labelId => _modalContent.Q<Label>("settings__lbl-id");
        private Label _labelAccountState => _modalContent.Q<Label>("settings__lbl-account-state");
        private Button _buttonLinkAccount => _modalContent.Q<Button>("settings__btn-link-account");
        private Button _buttonSave => _modalContent.Q<Button>("settings__btn-save");
        private Button _buttonCancel => _modalContent.Q<Button>("settings__btn-cancel");

        private readonly AccountService _accountService;
        private SettingsData _settingsData = new();

        public SettingsModalController(UIDocument uiDocument, AccountService accountService): base(uiDocument)
        {
            _accountService = accountService;
        }

        protected override async Task OnShowAsync()
        {
            _settingsData = new SettingsData();

            _settingsData.MusicVolume = AudioManager.MusicVolume;
            _settingsData.SfxVolume = AudioManager.SfxVolume;
            _settingsData.Language = (int)LocalizationManager.CurrentLanguage;
            _settingsData.EnableVibration = VibrationManager.EnableVibration;


            _dropdownLanguages.choices = LocalizationManager.GetAvaliableLanguages();
            _dropdownLanguages.value = LocalizationManager.Language;

            _toggleMusic.value = AudioManager.MusicVolume > 0;
            _toggleSound.value = AudioManager.SfxVolume > 0;

            _labelVersion.text = $"{Application.version}";
            _labelId.text = $"{SystemInfo.deviceUniqueIdentifier}";

            _buttonLinkAccount.SetEnabled(false);
            SubscribeToAccountState();
            UpdateAccountState(_accountService.State);
        }

        private void SubscribeToAccountState()
        {
            _accountService.StateChanged -= OnAccountStateChanged;
            _accountService.StateChanged += OnAccountStateChanged;
        }

        private void UnsubscribeFromAccountState()
        {
            _accountService.StateChanged -= OnAccountStateChanged;
        }

        private void OnAccountStateChanged(AccountState state)
        {
            UpdateAccountState(state);
        }

        /// <summary>
        /// Показывает актуальное пользовательское описание состояния аккаунта одной строкой.
        /// </summary>
        private void UpdateAccountState(AccountState state)
        {
            var stateLocalizationKey = state switch
            {
                AccountState.NotStarted => "account_state_not_started",
                AccountState.Resolving => "account_state_resolving",
                AccountState.Guest => "account_state_guest",
                AccountState.Error => "account_state_error",
                _ => "account_state_error"
            };

            _labelAccountState.text = LocalizationManager.GetLocalizedString(stateLocalizationKey);
        }

        private async void OnChangeLanguageAsync(ChangeEvent<string> evt)
        {
            _settingsData.Language = (int)LocalizationManager.GetLanguage(evt.newValue);
        }

        private async void OnClickButtonSave(ClickEvent evt)
        {
            AudioManager.SetMusicVolume(_settingsData.MusicVolume);
            AudioManager.SetSfxVolume(_settingsData.SfxVolume);
            await LocalizationManager.SetLanguageAsync((SystemLanguage)_settingsData.Language);
            VibrationManager.EnableVibration = _settingsData.EnableVibration;

            GameDataManager.Settings = _settingsData;
            GameDataManager.SaveSettings();
            UnsubscribeFromAccountState();
            Hide();
            UIManager.OnRepaintScreen();
        }

        protected override void OnSubscribeToEvents()
        {
            _buttonSave?.RegisterCallback<ClickEvent>(OnClickButtonSave);
            _buttonCancel?.RegisterCallback<ClickEvent>(OnClickButtonCancel);
            _dropdownLanguages?.RegisterValueChangedCallback(OnChangeLanguageAsync);
            _toggleMusic?.RegisterValueChangedCallback(OnChangeMusicAsync);
            _toggleSound?.RegisterValueChangedCallback(OnChangeSoundAsync);
            _toggleVibration?.RegisterValueChangedCallback(OnChangeVibrationAsync);
            _buttonCloseModal.UnregisterCallback<ClickEvent>(OnClickButtonClose);
            _buttonCloseModal.RegisterCallback<ClickEvent>(OnClickButtonClose);
        }

        private void OnChangeSoundAsync(ChangeEvent<bool> evt)
        {
            _settingsData.SfxVolume = evt.newValue ? 1 : 0;
        }


        private void OnChangeMusicAsync(ChangeEvent<bool> evt)
        {
            _settingsData.MusicVolume = evt.newValue ? 1 : 0;
        }

        private void OnChangeVibrationAsync(ChangeEvent<bool> evt)
        {
            _settingsData.EnableVibration = evt.newValue;
        }

        private void OnClickButtonCancel(ClickEvent evt)
        {
            UnsubscribeFromAccountState();
            Hide();
        }

        private void OnClickButtonClose(ClickEvent evt)
        {
            UnsubscribeFromAccountState();
        }

        protected override void OnUnsubscribeFromEvents()
        {
            _buttonSave?.UnregisterCallback<ClickEvent>(OnClickButtonSave);
            _buttonCancel?.UnregisterCallback<ClickEvent>(OnClickButtonCancel);
            _dropdownLanguages?.UnregisterValueChangedCallback(OnChangeLanguageAsync);
            _toggleMusic?.UnregisterValueChangedCallback(OnChangeMusicAsync);
            _toggleSound?.UnregisterValueChangedCallback(OnChangeSoundAsync);
            _toggleVibration?.UnregisterValueChangedCallback(OnChangeVibrationAsync);
            _buttonCloseModal.UnregisterCallback<ClickEvent>(OnClickButtonClose);
            UnsubscribeFromAccountState();
        }

    }
}
