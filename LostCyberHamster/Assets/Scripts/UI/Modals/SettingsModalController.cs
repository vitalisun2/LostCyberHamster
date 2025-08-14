using System.Threading.Tasks;
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
        private Button _buttonSave => _modalContent.Q<Button>("settings__btn-save");
        private Button _buttonCancel => _modalContent.Q<Button>("settings__btn-cancel");

        private SettingsData _settingsData = new();

        public SettingsModalController(UIDocument uiDocument): base(uiDocument)
        {
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


        protected override void OnUnsubscribeFromEvents()
        {
            _buttonSave?.UnregisterCallback<ClickEvent>(OnClickButtonSave);
            _buttonCancel?.UnregisterCallback<ClickEvent>(OnClickButtonCancel);
            _dropdownLanguages?.UnregisterValueChangedCallback(OnChangeLanguageAsync);
            _toggleMusic?.UnregisterValueChangedCallback(OnChangeMusicAsync);
            _toggleSound?.UnregisterValueChangedCallback(OnChangeSoundAsync);
        }

    }
}