using System;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using Vues.GameCore;

namespace LostCyberHamster.UI
{
    /// <summary>Показывает варианты сохранения и позволяет отложить разрешение конфликта.</summary>
    public sealed class CloudSaveConflictModalController : ModalController
    {
        private const string DateFormat = "g";
        private const string RetryErrorKey = "cloud_save_conflict_retry";

        private CloudSaveConflictModalDto _data;
        private GameResultModalPresentation _presentation;
        private bool _isBusy;
        private string _errorLocalizationKey;

        protected override ScreenEnum _modalAssetName => ScreenEnum.CloudSaveConflictModal;

        public event Action CloudSelected;
        public event Action ThisDeviceSelected;
        public event Action LaterSelected;

        public CloudSaveConflictModalController(UIDocument uiDocument) : base(uiDocument)
        {
        }

        /// <summary>Обновляет значения обеих карточек.</summary>
        public void SetData(CloudSaveConflictModalDto data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            Render();
        }

        /// <summary>Блокирует или разблокирует оба действия выбора.</summary>
        public void SetBusy(bool isBusy)
        {
            _isBusy = isBusy;
            RenderActions();
        }

        /// <summary>Показывает локализованную ошибку; пустой ключ возвращает пояснение выбора.</summary>
        public void SetError(string localizationKey)
        {
            _errorLocalizationKey = string.IsNullOrWhiteSpace(localizationKey)
                ? null
                : localizationKey;
            RenderStatus();
        }

        protected override Task OnShowAsync()
        {
            _buttonCloseModal.style.display = DisplayStyle.None;
            Render();
            return Task.CompletedTask;
        }

        protected override void OnSubscribeToEvents()
        {
            // Восстанавливаем художественный host после повторного включения UI.
            _presentation ??= GameResultModalPresentation.Apply(
                _root,
                _modalContent.Q<VisualElement>("cloud-save-conflict-viewport"),
                _modalContent.Q<VisualElement>("cloud-save-conflict-frame"),
                _modalContent.Q<VisualElement>("cloud-save-conflict-design"),
                new Vector2(1086f, 533f),
                ModalScaleMode.Contain,
                useSafeArea: true);
            Render();

            // Подключаем действия без повторных callbacks.
            GetCloudButton()?.UnregisterCallback<ClickEvent>(OnCloudSelected);
            GetCloudButton()?.RegisterCallback<ClickEvent>(OnCloudSelected);
            GetThisDeviceButton()?.UnregisterCallback<ClickEvent>(OnThisDeviceSelected);
            GetThisDeviceButton()?.RegisterCallback<ClickEvent>(OnThisDeviceSelected);
            GetLaterButton()?.UnregisterCallback<ClickEvent>(OnLaterSelected);
            GetLaterButton()?.RegisterCallback<ClickEvent>(OnLaterSelected);
        }

        protected override void OnUnsubscribeFromEvents()
        {
            // Освобождаем действия текущего дерева.
            GetCloudButton()?.UnregisterCallback<ClickEvent>(OnCloudSelected);
            GetThisDeviceButton()?.UnregisterCallback<ClickEvent>(OnThisDeviceSelected);
            GetLaterButton()?.UnregisterCallback<ClickEvent>(OnLaterSelected);

            // Возвращаем общий host следующему окну.
            _presentation?.Restore();
            _presentation = null;
        }

        private void OnCloudSelected(ClickEvent _)
        {
            if (!_isBusy && _data?.CanChooseCloud == true)
                CloudSelected?.Invoke();
        }

        private void OnThisDeviceSelected(ClickEvent _)
        {
            if (!_isBusy && _data?.ThisDevice != null)
                ThisDeviceSelected?.Invoke();
        }

        private void OnLaterSelected(ClickEvent _)
        {
            LaterSelected?.Invoke();
        }

        private void Render()
        {
            if (_modalContent == null)
                return;

            // Отсутствующий облачный snapshot отображаем без вымышленных значений.
            RenderCard("cloud", _data?.Cloud);
            RenderCard("device", _data?.ThisDevice);

            // Обновление данных сохраняет busy и сообщение ошибки.
            RenderActions();
            RenderStatus();
        }

        private void RenderCard(string prefix, CloudSaveConflictCardDto data)
        {
            // Неполученный snapshot остаётся пустой карточкой.
            if (data == null)
            {
                SetText($"cloud-conflict__{prefix}-player-level", "—");
                SetText($"cloud-conflict__{prefix}-levels", "—");
                SetText($"cloud-conflict__{prefix}-money", "—");
                SetText($"cloud-conflict__{prefix}-crystals", "—");
                SetText($"cloud-conflict__{prefix}-saved", "—");
                return;
            }

            // Значения и время остаются данными текущего сохранения.
            SetText($"cloud-conflict__{prefix}-player-level", data.PlayerLevel.ToString(CultureInfo.CurrentCulture));
            SetText($"cloud-conflict__{prefix}-levels", data.CompletedLevels.ToString(CultureInfo.CurrentCulture));
            SetText($"cloud-conflict__{prefix}-money", data.Money.ToString(CultureInfo.CurrentCulture));
            SetText($"cloud-conflict__{prefix}-crystals", data.Crystals.ToString(CultureInfo.CurrentCulture));
            SetText($"cloud-conflict__{prefix}-saved", data.SavedAt.ToLocalTime().ToString(DateFormat, CultureInfo.CurrentCulture));
        }

        private void RenderActions()
        {
            GetCloudButton()?.SetEnabled(!_isBusy && _data?.CanChooseCloud == true);
            GetThisDeviceButton()?.SetEnabled(!_isBusy && _data?.ThisDevice != null);
            GetLaterButton()?.SetEnabled(true);
        }

        private void RenderStatus()
        {
            var body = _modalContent?.Q<Label>("cloud-conflict__body");
            var error = _modalContent?.Q<Label>("cloud-conflict__error");
            bool hasError = !string.IsNullOrEmpty(_errorLocalizationKey);

            // Ошибка занимает место пояснения; нижнее предупреждение остаётся видимым.
            if (body != null)
                body.style.display = hasError ? DisplayStyle.None : DisplayStyle.Flex;
            if (error == null)
                return;

            error.style.display = hasError ? DisplayStyle.Flex : DisplayStyle.None;
            if (!hasError)
                return;

            // Неизвестный код заменяем общим локализованным сообщением.
            string localized = LocalizationManager.GetLocalizedString(_errorLocalizationKey);
            error.text = string.IsNullOrWhiteSpace(localized) ||
                string.Equals(localized, _errorLocalizationKey, StringComparison.Ordinal)
                ? LocalizationManager.GetLocalizedString(RetryErrorKey)
                : localized;
        }

        private void SetText(string elementName, string value)
        {
            var label = _modalContent.Q<Label>(elementName);
            if (label != null)
                label.text = value;
        }

        private Button GetCloudButton()
        {
            return _modalContent?.Q<Button>("cloud-conflict__choose-cloud");
        }

        private Button GetThisDeviceButton()
        {
            return _modalContent?.Q<Button>("cloud-conflict__choose-device");
        }

        private Button GetLaterButton()
        {
            return _modalContent?.Q<Button>("cloud-conflict__later");
        }
    }
}
