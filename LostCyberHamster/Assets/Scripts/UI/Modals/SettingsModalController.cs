using System.Threading.Tasks;
using Assets.Scripts.System;
using GameManagement;
using LostCyberHamster.Account;
using UnityEngine;
using UnityEngine.UIElements;
using Vues.GameCore;

namespace LostCyberHamster.UI
{
    /// <summary>
    /// Управляет пользовательскими настройками и account-link flow из общего модального окна.
    /// </summary>
    public class SettingsModalController : ModalController
    {
        protected override ScreenEnum _modalAssetName => ScreenEnum.SettingsModal;

        private DropdownField _dropdownLanguages => _modalContent.Q<DropdownField>("settings__dd-languages");
        private Toggle _toggleMusic => _modalContent.Q<Toggle>("settings__cbx-music");
        private Toggle _toggleSound => _modalContent.Q<Toggle>("settings__cbx-sound");
        private Toggle _toggleVibration => _modalContent.Q<Toggle>("settings__cbx-vibrate");
        private Label _labelVersion => _modalContent.Q<Label>("settings__lbl-version");
        private Label _labelId => _modalContent.Q<Label>("settings__lbl-id");
        private Label _labelAccountStatus => _modalContent.Q<Label>("settings__lbl-account-status");
        private Button _buttonSave => _modalContent.Q<Button>("settings__btn-save");
        private Button _buttonCancel => _modalContent.Q<Button>("settings__btn-cancel");
        private Button _buttonSaveProgress => _modalContent.Q<Button>("settings__btn-save-progress");

        private SettingsData _settingsData = new();
        private bool _isLinkingAccount;

        public SettingsModalController(UIDocument uiDocument): base(uiDocument)
        {
        }

        internal SettingsModalController(VisualElement root): base(root)
        {
        }

        protected override async Task OnShowAsync()
        {
            _settingsData = new SettingsData();
            _isLinkingAccount = false;

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

            await AccountServiceProvider.Current.IsLinkedAsync();
            UpdateAccountStatus(AccountServiceProvider.Current.Snapshot);
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
            Hide();
            UIManager.OnRepaintScreen();
        }

        protected override void OnSubscribeToEvents()
        {
            _buttonSave?.RegisterCallback<ClickEvent>(OnClickButtonSave);
            _buttonCancel?.RegisterCallback<ClickEvent>(OnClickButtonCancel);
            _buttonSaveProgress?.RegisterCallback<ClickEvent>(OnClickSaveProgress);
            _dropdownLanguages?.RegisterValueChangedCallback(OnChangeLanguageAsync);
            _toggleMusic?.RegisterValueChangedCallback(OnChangeMusicAsync);
            _toggleSound?.RegisterValueChangedCallback(OnChangeSoundAsync);
            _toggleVibration?.RegisterValueChangedCallback(OnChangeVibrationAsync);

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
            Hide();
        }

        private async void OnClickSaveProgress(ClickEvent evt)
        {
            if (_isLinkingAccount)
            {
                return;
            }

            _isLinkingAccount = true;
            _buttonSaveProgress?.SetEnabled(false);
            SetAccountStatusText(Text("account_save_in_progress"));

            try
            {
                var result = await AccountServiceProvider.Current.LinkUnityAccountAsync();
                if (result.IsSuccess)
                {
                    await AccountServiceProvider.Current.RefreshLinkStateAsync();
                    UpdateAccountStatus(AccountServiceProvider.Current.Snapshot);
                    return;
                }

                SetAccountStatusText(result.Status == AccountLinkStatus.AlreadyLinked
                    ? Text("account_already_linked")
                    : Text("account_save_error"));
            }
            finally
            {
                _isLinkingAccount = false;
                if (!AccountServiceProvider.Current.Snapshot.IsLinked)
                {
                    _buttonSaveProgress?.SetEnabled(true);
                }
            }
        }

        protected override void OnUnsubscribeFromEvents()
        {
            _buttonSave?.UnregisterCallback<ClickEvent>(OnClickButtonSave);
            _buttonCancel?.UnregisterCallback<ClickEvent>(OnClickButtonCancel);
            _buttonSaveProgress?.UnregisterCallback<ClickEvent>(OnClickSaveProgress);
            _dropdownLanguages?.UnregisterValueChangedCallback(OnChangeLanguageAsync);
            _toggleMusic?.UnregisterValueChangedCallback(OnChangeMusicAsync);
            _toggleSound?.UnregisterValueChangedCallback(OnChangeSoundAsync);
            _toggleVibration?.UnregisterValueChangedCallback(OnChangeVibrationAsync);
        }

        private void UpdateAccountStatus(AccountSnapshot snapshot)
        {
            if (snapshot.IsLinked)
            {
                SetAccountStatusText(Text("account_saved"));
                if (_buttonSaveProgress != null)
                {
                    _buttonSaveProgress.text = Text("account_saved_button");
                    _buttonSaveProgress.SetEnabled(false);
                }

                return;
            }

            var statusText = snapshot.State == AccountState.Offline || snapshot.State == AccountState.Error
                ? Text("account_save_error")
                : Text("account_guest");

            SetAccountStatusText(statusText);
            if (_buttonSaveProgress != null)
            {
                _buttonSaveProgress.text = Text("account_save_button");
                _buttonSaveProgress.SetEnabled(true);
            }
        }

        private void SetAccountStatusText(string text)
        {
            if (_labelAccountStatus != null)
            {
                _labelAccountStatus.text = text;
            }
        }

        private static string Text(string key)
        {
            return LocalizationManager.GetLocalizedString(key) ?? key;
        }
    }
}
