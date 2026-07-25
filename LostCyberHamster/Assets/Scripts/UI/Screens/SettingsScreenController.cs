using System.Threading.Tasks;
using Assets.Scripts.Account;
using Assets.Scripts.System;
using GameManagement;
using GameManagement.CloudSave;
using GameManagement.CloudSave.Models;
using UnityEngine;
using UnityEngine.UIElements;
using Vues.GameCore;

namespace LostCyberHamster.UI
{
    public class SettingsScreenController : ScreenController
    {
        private const int MinPlayerNameLength = 3;
        private const int MaxPlayerNameLength = 16;

        protected override ScreenEnum _screenAssetName => ScreenEnum.SettingsScreen;

        private DropdownField _dropdownLanguages => _contentRoot.Q<DropdownField>("settings__dd-languages");
        private Toggle _toggleMusic => _contentRoot.Q<Toggle>("settings__cbx-music");
        private Toggle _toggleSound => _contentRoot.Q<Toggle>("settings__cbx-sound");
        private Toggle _toggleVibration => _contentRoot.Q<Toggle>("settings__cbx-vibrate");
        private Label _labelVersion => _contentRoot.Q<Label>("settings__lbl-version");
        private Label _labelId => _contentRoot.Q<Label>("settings__lbl-id");
        private Label _labelAccountState => _contentRoot.Q<Label>("settings__lbl-account-state");
        private VisualElement _cloudSyncStatusRow => _contentRoot.Q<VisualElement>("settings__cloud-sync-status");
        private Label _labelCloudSyncStatus => _contentRoot.Q<Label>("settings__lbl-cloud-sync-status");
        private Button _buttonLinkAccount => _contentRoot.Q<Button>("settings__btn-link-account");
        private VisualElement _playerNameView => _contentRoot.Q<VisualElement>("settings__player-name-view");
        private Label _labelPlayerName => _contentRoot.Q<Label>("settings__lbl-player-name");
        private Button _buttonChangePlayerName => _contentRoot.Q<Button>("settings__btn-change-player-name");
        private VisualElement _playerNameEdit => _contentRoot.Q<VisualElement>("settings__player-name-edit");
        private TextField _textFieldPlayerName => _contentRoot.Q<TextField>("settings__txt-player-name");
        private Button _buttonSavePlayerName => _contentRoot.Q<Button>("settings__btn-save-player-name");
        private Button _buttonCancelPlayerName => _contentRoot.Q<Button>("settings__btn-cancel-player-name");
        private Label _labelPlayerNameError => _contentRoot.Q<Label>("settings__lbl-player-name-error");
        private Button _buttonHome => _contentRoot.Q<Button>("btn_home");
        private Button _buttonBack => _contentRoot.Q<Button>("btn_back");
        private Button _buttonSettings => _contentRoot.Q<Button>("btn_settings");

        private readonly AccountService _accountService;
        private readonly ExistingAccountRestoreCoordinator _existingAccountRestoreCoordinator;
        private readonly CloudSyncService _cloudSyncService;
        private static ScreenEnum _returnScreen = ScreenEnum.HomeScreen;
        private SettingsData _settingsData = new();
        private bool _hasAccountLinkConflict;
        private bool _isPlayerNameSaving;
        private bool _isActive;
        private bool _isLanguageSaving;
        private string _playerNameErrorLocalizationKey;
        private int _accountUiVersion;

        public SettingsScreenController(
            UIDocument uiDocument,
            AccountService accountService,
            ExistingAccountRestoreCoordinator existingAccountRestoreCoordinator,
            CloudSyncService cloudSyncService)
            : base(uiDocument)
        {
            _accountService = accountService;
            _existingAccountRestoreCoordinator = existingAccountRestoreCoordinator;
            _cloudSyncService = cloudSyncService;
        }

        /// <summary>Открывает настройки и запоминает один экран для возврата.</summary>
        public static void OpenFrom(ScreenEnum sourceScreen)
        {
            _returnScreen = sourceScreen == ScreenEnum.SettingsScreen
                ? ScreenEnum.HomeScreen
                : sourceScreen;
            UIManager.OnScreenShow?.Invoke(ScreenEnum.SettingsScreen);
        }

        protected override async Task OnLoadAsync()
        {
            _isActive = true;
            _hasAccountLinkConflict = false;
            _playerNameErrorLocalizationKey = null;
            _accountUiVersion++;
            _settingsData = GameDataManager.Settings ?? new SettingsData();

            _settingsData.MusicVolume = AudioManager.MusicVolume;
            _settingsData.SfxVolume = AudioManager.SfxVolume;
            _settingsData.Language = (int)LocalizationManager.CurrentLanguage;
            _settingsData.EnableVibration = VibrationManager.EnableVibration;


            _dropdownLanguages.choices = LocalizationManager.GetAvaliableLanguages();
            _dropdownLanguages.value = LocalizationManager.Language;

            _toggleMusic.value = AudioManager.MusicVolume > 0;
            _toggleSound.value = AudioManager.SfxVolume > 0;
            _toggleVibration.value = VibrationManager.EnableVibration;

            _labelVersion.text = $"{Application.version}";
            _labelId.text = $"{SystemInfo.deviceUniqueIdentifier}";
            _buttonHome.style.display = DisplayStyle.None;
            _buttonBack.text = LocalizationManager.GetLocalizedString("btn_back");
            _buttonBack.style.display = DisplayStyle.Flex;
            _buttonSettings.style.display = DisplayStyle.None;

            SubscribeToAccountState();
            SubscribeToCloudSyncStatus();
            UpdateAccountState(_accountService.State);
            ShowPlayerName(_accountService.PlayerName);
            SetPlayerNameEditMode(false);
            SetPlayerNameBusy(false);
            await ChangeBackgroundAsync("BackgroundScreenSprite");
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

        private void SubscribeToCloudSyncStatus()
        {
            _cloudSyncService.StatusChanged -= OnCloudSyncStatusChanged;
            _cloudSyncService.StatusChanged += OnCloudSyncStatusChanged;
        }

        private void UnsubscribeFromCloudSyncStatus()
        {
            _cloudSyncService.StatusChanged -= OnCloudSyncStatusChanged;
        }

        private void OnCloudSyncStatusChanged(CloudSyncStatusEnum status)
        {
            UpdateCloudSyncStatus(status);
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
            _buttonChangePlayerName.SetEnabled(
                state == AccountState.Guest || state == AccountState.Linked);
            if (state == AccountState.Guest || state == AccountState.Linked)
                ShowPlayerName(_accountService.PlayerName);

            UpdateCloudSyncStatus(_cloudSyncService.Status);
        }

        /// <summary>Показывает актуальное состояние облачного сохранения.</summary>
        private void UpdateCloudSyncStatus(CloudSyncStatusEnum status)
        {
            // Показываем статус только связанному аккаунту.
            var isLinked = _accountService.State == AccountState.Linked;
            _cloudSyncStatusRow.style.display = isLinked
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            if (!isLinked)
                return;

            // Выводим понятное состояние синхронизации.
            var localizationKey = status switch
            {
                CloudSyncStatusEnum.Saved => "cloud_sync_status_saved",
                CloudSyncStatusEnum.Synchronizing => "cloud_sync_status_synchronizing",
                CloudSyncStatusEnum.Pending => "cloud_sync_status_pending",
                CloudSyncStatusEnum.Conflict => "cloud_sync_status_conflict",
                _ => "cloud_sync_status_pending"
            };

            _labelCloudSyncStatus.text = LocalizationManager.GetLocalizedString(localizationKey);
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

                if (result == AccountLinkResult.Conflict && _isActive)
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

        private void OnClickChangePlayerName(ClickEvent evt)
        {
            _textFieldPlayerName.value = GetPlayerNameBase(_accountService.PlayerName);
            _playerNameErrorLocalizationKey = null;
            _labelPlayerNameError.style.display = DisplayStyle.None;
            SetPlayerNameEditMode(true);
        }

        /// <summary>
        /// Проверяет новое имя и сохраняет его через сервис аккаунта.
        /// </summary>
        private async void OnClickSavePlayerName(ClickEvent evt)
        {
            if (_isPlayerNameSaving)
                return;

            // Отклоняем имя, не соответствующее правилам поля.
            var playerName = _textFieldPlayerName.value?.Trim() ?? string.Empty;
            if (!IsPlayerNameValid(playerName))
            {
                ShowPlayerNameError("player_name_validation_error");
                return;
            }

            // Блокируем повторный запрос и сохраняем имя на сервере.
            var accountUiVersion = _accountUiVersion;
            SetPlayerNameBusy(true);
            try
            {
                var updatedPlayerName = await _accountService.UpdatePlayerNameAsync(playerName);
                if (accountUiVersion != _accountUiVersion)
                    return;

                ShowPlayerName(updatedPlayerName);
                SetPlayerNameEditMode(false);
            }
            catch
            {
                if (accountUiVersion == _accountUiVersion)
                    ShowPlayerNameError("player_name_save_error");
            }
            finally
            {
                if (accountUiVersion == _accountUiVersion)
                    SetPlayerNameBusy(false);
            }
        }

        private void OnClickCancelPlayerName(ClickEvent evt)
        {
            _textFieldPlayerName.value = GetPlayerNameBase(_accountService.PlayerName);
            _playerNameErrorLocalizationKey = null;
            _labelPlayerNameError.style.display = DisplayStyle.None;
            SetPlayerNameEditMode(false);
        }

        private void ShowPlayerName(string playerName)
        {
            _labelPlayerName.text = playerName ?? string.Empty;
        }

        private void ShowPlayerNameError(string localizationKey)
        {
            _playerNameErrorLocalizationKey = localizationKey;
            _labelPlayerNameError.text = LocalizationManager.GetLocalizedString(localizationKey);
            _labelPlayerNameError.style.display = DisplayStyle.Flex;
        }

        private void SetPlayerNameEditMode(bool isEditing)
        {
            _playerNameView.style.display = isEditing
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            _playerNameEdit.style.display = isEditing
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        private void SetPlayerNameBusy(bool isBusy)
        {
            // Блокируем элементы отдельной операции имени.
            _isPlayerNameSaving = isBusy;
            _textFieldPlayerName.SetEnabled(!isBusy);
            _buttonSavePlayerName.SetEnabled(!isBusy);
            _buttonCancelPlayerName.SetEnabled(!isBusy);

            // Показываем состояние серверного сохранения на его кнопке.
            _buttonSavePlayerName.text = LocalizationManager.GetLocalizedString(
                isBusy ? "player_name_saving" : "btn_save_player_name");
        }

        private static bool IsPlayerNameValid(string playerName)
        {
            // Проверяем границы и служебный разделитель Unity Player Names.
            if (playerName.Length < MinPlayerNameLength ||
                playerName.Length > MaxPlayerNameLength ||
                playerName.Contains("#"))
            {
                return false;
            }

            // Запрещаем все виды пробельных символов.
            foreach (var symbol in playerName)
            {
                if (char.IsWhiteSpace(symbol))
                    return false;
            }

            return true;
        }

        private static string GetPlayerNameBase(string playerName)
        {
            // Пустое полное имя даёт пустое поле редактирования.
            if (string.IsNullOrWhiteSpace(playerName))
                return string.Empty;

            // Убираем назначенный Unity суффикс.
            var suffixIndex = playerName.LastIndexOf('#');
            return suffixIndex > 0
                ? playerName.Substring(0, suffixIndex)
                : playerName;
        }

        private async void OnChangeLanguageAsync(ChangeEvent<string> evt)
        {
            if (_isLanguageSaving)
                return;

            _isLanguageSaving = true;
            _dropdownLanguages.SetEnabled(false);
            try
            {
                var language = LocalizationManager.GetLanguage(evt.newValue);
                await LocalizationManager.SetLanguageAsync(language);
                _settingsData.Language = (int)language;
                SaveSettings();
                if (_isActive)
                    RefreshLocalizedText();
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[Settings] Failed to change language: {exception.Message}");
            }
            finally
            {
                _isLanguageSaving = false;
                if (_isActive)
                    _dropdownLanguages.SetEnabled(true);
            }
        }

        protected override void OnSubscribeToEvents()
        {
            _buttonBack?.RegisterCallback<ClickEvent>(OnClickButtonBack);
            _buttonLinkAccount?.RegisterCallback<ClickEvent>(OnClickButtonLinkAccount);
            _buttonChangePlayerName?.RegisterCallback<ClickEvent>(OnClickChangePlayerName);
            _buttonSavePlayerName?.RegisterCallback<ClickEvent>(OnClickSavePlayerName);
            _buttonCancelPlayerName?.RegisterCallback<ClickEvent>(OnClickCancelPlayerName);
            _dropdownLanguages?.RegisterValueChangedCallback(OnChangeLanguageAsync);
            _toggleMusic?.RegisterValueChangedCallback(OnChangeMusicAsync);
            _toggleSound?.RegisterValueChangedCallback(OnChangeSoundAsync);
            _toggleVibration?.RegisterValueChangedCallback(OnChangeVibrationAsync);
        }

        private void OnChangeSoundAsync(ChangeEvent<bool> evt)
        {
            _settingsData.SfxVolume = evt.newValue ? 1 : 0;
            AudioManager.SetSfxVolume(_settingsData.SfxVolume);
            SaveSettings();
        }


        private void OnChangeMusicAsync(ChangeEvent<bool> evt)
        {
            _settingsData.MusicVolume = evt.newValue ? 1 : 0;
            AudioManager.SetMusicVolume(_settingsData.MusicVolume);
            SaveSettings();
        }

        private void OnChangeVibrationAsync(ChangeEvent<bool> evt)
        {
            _settingsData.EnableVibration = evt.newValue;
            VibrationManager.EnableVibration = evt.newValue;
            SaveSettings();
        }

        private void RefreshLocalizedText()
        {
            // Обновляем локализованные элементы без перезагрузки экрана и потери введённого имени.
            _contentRoot.Query<LocalizedLabel>().ForEach(label =>
            {
                if (!string.IsNullOrEmpty(label.key))
                    label.text = LocalizationManager.GetLocalizedString(label.key);
            });
            _contentRoot.Query<LocalizedButton>().ForEach(button =>
            {
                if (!string.IsNullOrEmpty(button.key))
                    button.text = LocalizationManager.GetLocalizedString(button.key);
            });

            // Обновляем динамические подписи, которые не управляются ключом UXML напрямую.
            _buttonBack.text = LocalizationManager.GetLocalizedString("btn_back");
            UpdateAccountState(_accountService.State);
            if (!string.IsNullOrEmpty(_playerNameErrorLocalizationKey))
            {
                _labelPlayerNameError.text =
                    LocalizationManager.GetLocalizedString(_playerNameErrorLocalizationKey);
            }
            SetPlayerNameBusy(_isPlayerNameSaving);
        }

        private void OnClickButtonBack(ClickEvent evt)
        {
            var returnScreen = _returnScreen;
            _returnScreen = ScreenEnum.HomeScreen;
            EndSession();
            UIManager.OnScreenShow?.Invoke(returnScreen);
        }

        private void SaveSettings()
        {
            GameDataManager.Settings = _settingsData;
            GameDataManager.SaveSettings();
        }

        private void EndSession()
        {
            if (!_isActive)
                return;

            _isActive = false;
            ResetAccountConflictUi();
            UnsubscribeFromAccountState();
            UnsubscribeFromCloudSyncStatus();
        }

        private void ResetAccountConflictUi()
        {
            _hasAccountLinkConflict = false;
            _accountUiVersion++;
        }

        protected override void OnUnsubscribeFromEvents()
        {
            _buttonBack?.UnregisterCallback<ClickEvent>(OnClickButtonBack);
            _buttonLinkAccount?.UnregisterCallback<ClickEvent>(OnClickButtonLinkAccount);
            _buttonChangePlayerName?.UnregisterCallback<ClickEvent>(OnClickChangePlayerName);
            _buttonSavePlayerName?.UnregisterCallback<ClickEvent>(OnClickSavePlayerName);
            _buttonCancelPlayerName?.UnregisterCallback<ClickEvent>(OnClickCancelPlayerName);
            _dropdownLanguages?.UnregisterValueChangedCallback(OnChangeLanguageAsync);
            _toggleMusic?.UnregisterValueChangedCallback(OnChangeMusicAsync);
            _toggleSound?.UnregisterValueChangedCallback(OnChangeSoundAsync);
            _toggleVibration?.UnregisterValueChangedCallback(OnChangeVibrationAsync);
            EndSession();
        }

    }
}
