using Assets.Scripts.Account;
using Assets.Scripts.System;
using Assets.Scripts.Tutorial;
using GameManagement;
using GameManagement.CloudSave;
using GameManagement.CloudSave.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Vues.GameCore;

namespace LostCyberHamster.UI
{
    public class SettingsScreenController : ScreenController
    {
        private const string BackgroundAddress =
            "SettingsScreenBackgroundSprite";
        private const string DropdownPopupScopeClass =
            "settings-dropdown-popup-scope";
        private const float DesignWidth = 1672f;
        private const float DesignHeight = 940f;
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
        private VisualElement _playerNameEdit =>
            _contentRoot.Q<VisualElement>("settings__player-name-edit");
        private TextField _textFieldPlayerName =>
            _contentRoot.Q<TextField>("settings__txt-player-name");
        private Button _buttonSavePlayerName =>
            _contentRoot.Q<Button>("settings__btn-save-player-name");
        private Button _buttonCancelPlayerName =>
            _contentRoot.Q<Button>("settings__btn-cancel-player-name");
        private Button _buttonStartTraining => _contentRoot.Q<Button>("settings__btn-start-training");
        private Label _labelPlayerNameError =>
            _contentRoot.Q<Label>("settings__lbl-player-name-error");
        private Button _buttonBack => _contentRoot.Q<Button>("btn_back");
        private VisualElement _musicCheckbox =>
            _contentRoot.Q<VisualElement>("settings__music-checkbox");
        private VisualElement _soundCheckbox =>
            _contentRoot.Q<VisualElement>("settings__sound-checkbox");

        private readonly AccountService _accountService;
        private readonly ExistingAccountRestoreCoordinator _existingAccountRestoreCoordinator;
        private readonly CloudSyncService _cloudSyncService;
        private Button _buttonCloudAction;
        private Button _buttonExistingAccount;
        private System.IDisposable _ownershipPrompt;
        private static ScreenEnum _returnScreen = ScreenEnum.HomeScreen;
        private static bool _openExistingAccount;
        private static bool _openProfileChoice;
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

        /// <summary>Открывает конкретный сценарий восстановления с предупреждением о замене прогресса.</summary>
        public static void OpenExistingAccountFrom(ScreenEnum sourceScreen)
        {
            _openProfileChoice = false;
            _openExistingAccount = true;
            OpenFrom(sourceScreen);
        }

        /// <summary>Открывает выбор профиля с действием, соответствующим текущему аккаунту и сохранению.</summary>
        public static void OpenProfileChoiceFrom(ScreenEnum sourceScreen)
        {
            _openExistingAccount = false;
            _openProfileChoice = true;
            OpenFrom(sourceScreen);
        }

        protected override string ScreenBackgroundAddress => BackgroundAddress;

        protected override ScreenLayout CreateLayout(VisualElement content)
        {
            return ScreenLayout.Fit(
                content.Q<VisualElement>("settings-viewport"),
                content.Q<VisualElement>("settings-scale-frame"),
                content.Q<VisualElement>("settings-design"),
                new Vector2(DesignWidth, DesignHeight),
                content.Q<VisualElement>("settings-name-scale-frame"),
                content.Q<VisualElement>("settings-name-design"), stretchWidth: true);
        }

        protected override void BindView()
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
            RenderCheckbox(_musicCheckbox, _toggleMusic.value);
            RenderCheckbox(_soundCheckbox, _toggleSound.value);

            _labelVersion.text = $"{Application.version}";
            _labelId.text = $"{SystemInfo.deviceUniqueIdentifier}";
            EnsureCloudActionButton();
            EnsureExistingAccountButton();

            SubscribeToAccountState();
            SubscribeToCloudSyncStatus();
            GameDataManager.ProfileChanged -= OnProfileChanged;
            GameDataManager.ProfileChanged += OnProfileChanged;
            UpdateAccountState(_accountService.State);
            ShowPlayerName(_accountService.PlayerName);
            SetPlayerNameEditMode(false);
            SetPlayerNameBusy(false);
            if (_openExistingAccount)
            {
                _openExistingAccount = false;
                _contentRoot.schedule.Execute(() => { if (_isActive) ShowExistingAccountConfirmation(); });
            }
            else if (_openProfileChoice)
            {
                _openProfileChoice = false;
                _contentRoot.schedule.Execute(() => { if (_isActive) ShowProfileChoice(); });
            }
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

        private void OnProfileChanged()
        {
            if (_isActive) UpdateAccountState(_accountService.State);
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
            else if (_accountService.IsGuestRecoveryUnavailable)
                stateLocalizationKey = "account_guest_credentials_missing";
            else if (state == AccountState.Error && !string.IsNullOrEmpty(_accountService.LastRecoveryErrorKey))
                stateLocalizationKey = _accountService.LastRecoveryErrorKey;

            _labelAccountState.text = LocalizationManager.GetLocalizedString(stateLocalizationKey);
            _buttonLinkAccount.text = LocalizationManager.GetLocalizedString(
                _accountService.CanReauthenticateLinkedOwner ? "account_reauthenticate" :
                state == AccountState.Error || state == AccountState.NotStarted
                    ? "cloud_sync_action_retry"
                    : _hasAccountLinkConflict || state == AccountState.SigningIn
                    ? "btn_sign_in"
                    : "btn_link_account").ToUpperInvariant();
            _buttonLinkAccount.style.display = state == AccountState.Linked
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            _buttonLinkAccount.SetEnabled(!_accountService.IsGuestRecoveryUnavailable &&
                (state == AccountState.Guest || state == AccountState.Error || state == AccountState.NotStarted));
            if (_buttonExistingAccount != null)
            {
                _buttonExistingAccount.text = LocalizationManager.GetLocalizedString("btn_sign_in").ToUpperInvariant();
                _buttonExistingAccount.style.display = state == AccountState.Guest && !_hasAccountLinkConflict
                    ? DisplayStyle.Flex : DisplayStyle.None;
            }
            _buttonChangePlayerName.SetEnabled(
                state == AccountState.Guest || state == AccountState.Linked);
            if (state == AccountState.Guest || state == AccountState.Linked)
                ShowPlayerName(_accountService.PlayerName);

            UpdateCloudSyncStatus(_cloudSyncService.Status);
        }

        /// <summary>Показывает актуальное состояние облачного сохранения.</summary>
        private void UpdateCloudSyncStatus(CloudSyncStatusEnum status)
        {
            // Локальное сохранение остаётся видимым при недоступной авторизации.
            _cloudSyncStatusRow.style.display = DisplayStyle.Flex;

            // Выводим понятное состояние синхронизации.
            var localizationKey = status switch
            {
                CloudSyncStatusEnum.Saved => "cloud_sync_status_saved",
                CloudSyncStatusEnum.Synchronizing => "cloud_sync_status_synchronizing",
                CloudSyncStatusEnum.Pending => "cloud_sync_status_pending",
                CloudSyncStatusEnum.Conflict => "cloud_sync_status_conflict",
                CloudSyncStatusEnum.LocalOnly => "cloud_sync_status_local",
                CloudSyncStatusEnum.Unavailable => "cloud_sync_status_unavailable",
                _ => "cloud_sync_status_pending"
            };

            _labelCloudSyncStatus.text = LocalizationManager.GetLocalizedString(localizationKey);
            if (System.DateTime.TryParse(GameDataManager.LastCloudSyncUtc, out var confirmedAt))
                _labelCloudSyncStatus.text += "\n" + LocalizationManager.GetLocalizedString("cloud_sync_last_confirmed") +
                    " " + confirmedAt.ToLocalTime().ToString("g");
            if (_buttonCloudAction != null)
            {
                _buttonCloudAction.text = LocalizationManager.GetLocalizedString(
                    _cloudSyncService.HasUnresolvedConflict || ProfileOwnershipService.Instance?.CanAdoptGuestProgress == true
                        ? "cloud_sync_action_choose" : "cloud_sync_action_retry").ToUpperInvariant();
                _buttonCloudAction.SetEnabled(status != CloudSyncStatusEnum.Synchronizing);
            }
        }

        /// <summary>Даёт гостю прямой вход в существующий аккаунт через предварительное предупреждение.</summary>
        private void EnsureExistingAccountButton()
        {
            _buttonExistingAccount = _buttonLinkAccount.parent.Q<Button>("settings__btn-existing-account");
            if (_buttonExistingAccount != null) return;
            _buttonExistingAccount = new Button { name = "settings__btn-existing-account" };
            _buttonExistingAccount.AddToClassList("lcs_btn");
            _buttonExistingAccount.AddToClassList("settings-art-button");
            _buttonExistingAccount.AddToClassList("settings-art-button--secondary");
            _buttonExistingAccount.AddToClassList("settings-account__button");
            _buttonLinkAccount.parent.Add(_buttonExistingAccount);
        }

        /// <summary>Добавляет действие к существующей строке статуса без перестройки экрана.</summary>
        private void EnsureCloudActionButton()
        {
            _buttonCloudAction = _cloudSyncStatusRow.Q<Button>("settings__btn-cloud-sync-action");
            if (_buttonCloudAction != null) return;
            _buttonCloudAction = new Button { name = "settings__btn-cloud-sync-action" };
            _buttonCloudAction.AddToClassList("lcs_btn");
            _buttonCloudAction.AddToClassList("settings-art-button");
            _buttonCloudAction.AddToClassList("settings-art-button--primary");
            _buttonCloudAction.AddToClassList("settings-row-action");
            _cloudSyncStatusRow.Add(_buttonCloudAction);
        }

        private void OnClickCloudAction(ClickEvent _)
        {
            ShowProfileChoice();
        }

        /// <summary>Открывает доступный выбор прогресса либо запрашивает его сетевое согласование.</summary>
        private void ShowProfileChoice()
        {
            if (_cloudSyncService.HasUnresolvedConflict) _cloudSyncService.ShowConflict();
            else if (ProfileOwnershipService.Instance?.CanAdoptGuestProgress == true)
            {
                _ownershipPrompt?.Dispose();
                _ownershipPrompt = ProfileOwnershipPrompt.Show(_contentRoot, ShowExistingAccountConfirmation,
                    () => UpdateAccountState(_accountService.State));
            }
            else if (_accountService.State == AccountState.Guest &&
                !string.IsNullOrEmpty(GameDataManager.OwnerPlayerId) &&
                _accountService.TryGetAuthenticatedPlayerId(out var playerId) &&
                GameDataManager.OwnerPlayerId != playerId)
                ShowExistingAccountConfirmation();
            else if (_accountService.RequiresReauthentication || _accountService.IsGuestRecoveryUnavailable)
                UpdateAccountState(_accountService.State);
            else
            {
                _accountService.Start();
                _cloudSyncService.RequestRetry();
            }
        }

        private void OnClickExistingAccount(ClickEvent _) => ShowExistingAccountConfirmation();

        /// <summary>Запрашивает явный выбор до запуска входа с заменой локального прогресса.</summary>
        private void ShowExistingAccountConfirmation()
        {
            if (!_isActive || _accountService.State != AccountState.Guest) return;
            _ownershipPrompt?.Dispose();
            _ownershipPrompt = ProfileOwnershipPrompt.ShowExistingAccountConfirmation(_contentRoot,
                () => _ = RestoreExistingAccountAsync());
        }

        /// <summary>Восстанавливает выбранный аккаунт и сообщает только фактический результат восстановления гостя.</summary>
        private async System.Threading.Tasks.Task RestoreExistingAccountAsync()
        {
            var version = _accountUiVersion;
            try
            {
                var result = await _existingAccountRestoreCoordinator.RestoreAsync();
                if (!_isActive || version != _accountUiVersion) return;
                if (result == ExistingAccountRestoreResult.Restored)
                {
                    _hasAccountLinkConflict = false;
                    UpdateAccountState(_accountService.State);
                    return;
                }
            }
            catch (System.Exception)
            {
                // AccountService сохраняет исходный профиль; UI остаётся доступным для следующего действия.
            }
            if (_isActive && version == _accountUiVersion)
                _labelAccountState.text = LocalizationManager.GetLocalizedString(_accountService.State == AccountState.Guest
                    ? "account_sign_in_failed_retry" : "account_sign_in_unavailable");
        }

        private async void OnClickButtonLinkAccount(ClickEvent evt)
        {
            if (_accountService.CanReauthenticateLinkedOwner)
            {
                await _accountService.ReauthenticateLinkedOwnerAsync();
                if (_isActive) UpdateAccountState(_accountService.State);
                return;
            }
            if (_accountService.State == AccountState.Error || _accountService.State == AccountState.NotStarted)
            {
                _accountService.Start();
                return;
            }
            if (_accountService.State != AccountState.Guest)
                return;

            var accountUiVersion = _accountUiVersion;

            try
            {
                if (_hasAccountLinkConflict)
                {
                    ShowExistingAccountConfirmation();
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
            _playerNameEdit.schedule.Execute(FocusPlayerNameField);
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
            _playerNameView.style.display = DisplayStyle.Flex;
            _playerNameEdit.style.display = isEditing
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        private void FocusPlayerNameField()
        {
            if (_isActive &&
                _playerNameEdit.resolvedStyle.display == DisplayStyle.Flex)
            {
                _textFieldPlayerName.Focus();
            }
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
                isBusy ? "player_name_saving" : "btn_save_player_name").ToUpperInvariant();
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
            SetDropdownPopupStyleScope(true);
            _buttonBack?.RegisterCallback<ClickEvent>(OnClickButtonBack);
            _buttonLinkAccount?.RegisterCallback<ClickEvent>(OnClickButtonLinkAccount);
            _buttonCloudAction?.RegisterCallback<ClickEvent>(OnClickCloudAction);
            _buttonExistingAccount?.RegisterCallback<ClickEvent>(OnClickExistingAccount);
            _buttonChangePlayerName?.RegisterCallback<ClickEvent>(OnClickChangePlayerName);
            _buttonSavePlayerName?.RegisterCallback<ClickEvent>(OnClickSavePlayerName);
            _buttonCancelPlayerName?.RegisterCallback<ClickEvent>(OnClickCancelPlayerName);
            _buttonStartTraining?.RegisterCallback<ClickEvent>(OnClickStartTraining);
            _dropdownLanguages?.RegisterValueChangedCallback(OnChangeLanguageAsync);
            _toggleMusic?.RegisterValueChangedCallback(OnChangeMusicAsync);
            _toggleSound?.RegisterValueChangedCallback(OnChangeSoundAsync);
            _toggleVibration?.RegisterValueChangedCallback(OnChangeVibrationAsync);
        }

        private void OnChangeSoundAsync(ChangeEvent<bool> evt)
        {
            _settingsData.SfxVolume = evt.newValue ? 1 : 0;
            AudioManager.SetSfxVolume(_settingsData.SfxVolume);
            RenderCheckbox(_soundCheckbox, evt.newValue);
            SaveSettings();
        }


        private void OnChangeMusicAsync(ChangeEvent<bool> evt)
        {
            _settingsData.MusicVolume = evt.newValue ? 1 : 0;
            AudioManager.SetMusicVolume(_settingsData.MusicVolume);
            RenderCheckbox(_musicCheckbox, evt.newValue);
            SaveSettings();
        }

        private static void RenderCheckbox(
            VisualElement checkbox,
            bool isChecked)
        {
            checkbox?.EnableInClassList(
                "settings-checkbox--checked",
                isChecked);
        }

        private void SetDropdownPopupStyleScope(bool isEnabled)
        {
            _contentRoot.panel?.visualTree.EnableInClassList(
                DropdownPopupScopeClass,
                isEnabled);
        }

        private void OnChangeVibrationAsync(ChangeEvent<bool> evt)
        {
            _settingsData.EnableVibration = evt.newValue;
            VibrationManager.EnableVibration = evt.newValue;
            SaveSettings();
        }

        private void OnClickStartTraining(ClickEvent evt)
        {
            EndSession();
            TutorialLaunchService.StartReplayFromMenu();
            SceneManager.LoadScene("Game");
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
                    button.text = LocalizationManager.GetLocalizedString(button.key).ToUpperInvariant();
            });

            // Обновляем динамические подписи, которые не управляются ключом UXML напрямую.
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
            _ownershipPrompt?.Dispose();
            _ownershipPrompt = null;
            ResetAccountConflictUi();
            UnsubscribeFromAccountState();
            UnsubscribeFromCloudSyncStatus();
            GameDataManager.ProfileChanged -= OnProfileChanged;
        }

        private void ResetAccountConflictUi()
        {
            _hasAccountLinkConflict = false;
            _accountUiVersion++;
        }

        protected override void OnUnsubscribeFromEvents()
        {
            SetDropdownPopupStyleScope(false);
            _buttonBack?.UnregisterCallback<ClickEvent>(OnClickButtonBack);
            _buttonLinkAccount?.UnregisterCallback<ClickEvent>(OnClickButtonLinkAccount);
            _buttonCloudAction?.UnregisterCallback<ClickEvent>(OnClickCloudAction);
            _buttonExistingAccount?.UnregisterCallback<ClickEvent>(OnClickExistingAccount);
            _buttonChangePlayerName?.UnregisterCallback<ClickEvent>(OnClickChangePlayerName);
            _buttonSavePlayerName?.UnregisterCallback<ClickEvent>(OnClickSavePlayerName);
            _buttonCancelPlayerName?.UnregisterCallback<ClickEvent>(OnClickCancelPlayerName);
            _buttonStartTraining?.UnregisterCallback<ClickEvent>(OnClickStartTraining);
            _dropdownLanguages?.UnregisterValueChangedCallback(OnChangeLanguageAsync);
            _toggleMusic?.UnregisterValueChangedCallback(OnChangeMusicAsync);
            _toggleSound?.UnregisterValueChangedCallback(OnChangeSoundAsync);
            _toggleVibration?.UnregisterValueChangedCallback(OnChangeVibrationAsync);
            EndSession();
        }

    }
}
