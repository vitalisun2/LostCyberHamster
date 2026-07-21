using System.Threading.Tasks;
using Assets.Scripts.Account;
using Assets.Scripts.System;
using GameManagement;
using GameManagement.CloudSave;
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
        private readonly ExistingAccountRestoreCoordinator _existingAccountRestoreCoordinator;
        private SettingsData _settingsData = new();
        private bool _hasAccountLinkConflict;
        private int _accountUiVersion;

        public SettingsModalController(
            UIDocument uiDocument,
            AccountService accountService,
            ExistingAccountRestoreCoordinator existingAccountRestoreCoordinator)
            : base(uiDocument)
        {
            _accountService = accountService;
            _existingAccountRestoreCoordinator = existingAccountRestoreCoordinator;
        }

        protected override async Task OnShowAsync()
        {
            _hasAccountLinkConflict = false;
            _accountUiVersion++;
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
                AccountState.Linking => "account_state_linking",
                AccountState.SigningIn => "account_state_signing_in",
                AccountState.Linked => "account_state_linked",
                AccountState.Error => "account_state_error",
                _ => "account_state_error"
            };

            if (state == AccountState.Guest && _hasAccountLinkConflict)
                stateLocalizationKey = "account_link_conflict";

            _labelAccountState.text = LocalizationManager.GetLocalizedString(stateLocalizationKey);
            _buttonLinkAccount.text = LocalizationManager.GetLocalizedString(
                _hasAccountLinkConflict || state == AccountState.SigningIn
                    ? "btn_sign_in"
                    : "btn_link_account");
            _buttonLinkAccount.style.display = state == AccountState.Linked
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            _buttonLinkAccount.SetEnabled(state == AccountState.Guest);
        }

        private async void OnClickButtonLinkAccount(ClickEvent evt)
        {
            if (_accountService.State != AccountState.Guest)
                return;

            var accountUiVersion = _accountUiVersion;

            try
            {
                if (_hasAccountLinkConflict)
                {
                    var restoreResult = await _existingAccountRestoreCoordinator.RestoreAsync();
                    if (accountUiVersion != _accountUiVersion)
                        return;

                    if (restoreResult == ExistingAccountRestoreResult.Restored)
                    {
                        _hasAccountLinkConflict = false;
                        UpdateAccountState(_accountService.State);
                    }
                    else if (_accountService.State == AccountState.Guest)
                    {
                        _labelAccountState.text = LocalizationManager.GetLocalizedString(
                            "account_sign_in_failed_retry");
                    }

                    return;
                }

                var result = await _accountService.LinkCurrentGuestAsync();
                if (accountUiVersion != _accountUiVersion)
                    return;

                var modal = _modal;
                if (result == AccountLinkResult.Conflict &&
                    modal != null &&
                    modal.resolvedStyle.display == DisplayStyle.Flex)
                {
                    _hasAccountLinkConflict = true;
                    UpdateAccountState(_accountService.State);
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[Account] Link UI action failed: {exception.Message}");
            }
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
            ResetAccountConflictUi();
            UnsubscribeFromAccountState();
            Hide();
            UIManager.OnRepaintScreen();
        }

        protected override void OnSubscribeToEvents()
        {
            _buttonSave?.RegisterCallback<ClickEvent>(OnClickButtonSave);
            _buttonCancel?.RegisterCallback<ClickEvent>(OnClickButtonCancel);
            _buttonLinkAccount?.RegisterCallback<ClickEvent>(OnClickButtonLinkAccount);
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
            ResetAccountConflictUi();
            UnsubscribeFromAccountState();
            Hide();
        }

        private void OnClickButtonClose(ClickEvent evt)
        {
            ResetAccountConflictUi();
            UnsubscribeFromAccountState();
        }

        private void ResetAccountConflictUi()
        {
            _hasAccountLinkConflict = false;
            _accountUiVersion++;
        }

        protected override void OnUnsubscribeFromEvents()
        {
            _buttonSave?.UnregisterCallback<ClickEvent>(OnClickButtonSave);
            _buttonCancel?.UnregisterCallback<ClickEvent>(OnClickButtonCancel);
            _buttonLinkAccount?.UnregisterCallback<ClickEvent>(OnClickButtonLinkAccount);
            _dropdownLanguages?.UnregisterValueChangedCallback(OnChangeLanguageAsync);
            _toggleMusic?.UnregisterValueChangedCallback(OnChangeMusicAsync);
            _toggleSound?.UnregisterValueChangedCallback(OnChangeSoundAsync);
            _toggleVibration?.UnregisterValueChangedCallback(OnChangeVibrationAsync);
            _buttonCloseModal.UnregisterCallback<ClickEvent>(OnClickButtonClose);
            ResetAccountConflictUi();
            UnsubscribeFromAccountState();
        }

    }
}
