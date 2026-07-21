using System;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine.UIElements;

namespace LostCyberHamster.UI
{
    /// <summary>Показывает обязательный выбор между облачным и локальным сохранением.</summary>
    public sealed class CloudSaveConflictModalController : ModalController
    {
        private const string DateFormat = "g";

        private CloudSaveConflictModalData _data;

        protected override ScreenEnum _modalAssetName => ScreenEnum.CloudSaveConflictModal;

        public event Action CloudSelected;
        public event Action ThisDeviceSelected;

        public CloudSaveConflictModalController(UIDocument uiDocument) : base(uiDocument)
        {
        }

        /// <summary>Обновляет значения обеих карточек.</summary>
        public void SetData(CloudSaveConflictModalData data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            Render();
        }

        /// <summary>Блокирует или разблокирует оба действия выбора.</summary>
        public void SetBusy(bool isBusy)
        {
            var cloudButton = GetCloudButton();
            var thisDeviceButton = GetThisDeviceButton();
            if (cloudButton != null)
                cloudButton.SetEnabled(!isBusy);
            if (thisDeviceButton != null)
                thisDeviceButton.SetEnabled(!isBusy);
        }

        protected override Task OnShowAsync()
        {
            _buttonCloseModal.style.display = DisplayStyle.None;
            SetBusy(isBusy: false);
            Render();
            return Task.CompletedTask;
        }

        protected override void OnSubscribeToEvents()
        {
            GetCloudButton()?.UnregisterCallback<ClickEvent>(OnCloudSelected);
            GetCloudButton()?.RegisterCallback<ClickEvent>(OnCloudSelected);
            GetThisDeviceButton()?.UnregisterCallback<ClickEvent>(OnThisDeviceSelected);
            GetThisDeviceButton()?.RegisterCallback<ClickEvent>(OnThisDeviceSelected);
        }

        protected override void OnUnsubscribeFromEvents()
        {
            GetCloudButton()?.UnregisterCallback<ClickEvent>(OnCloudSelected);
            GetThisDeviceButton()?.UnregisterCallback<ClickEvent>(OnThisDeviceSelected);
        }

        private void OnCloudSelected(ClickEvent _)
        {
            CloudSelected?.Invoke();
        }

        private void OnThisDeviceSelected(ClickEvent _)
        {
            ThisDeviceSelected?.Invoke();
        }

        private void Render()
        {
            if (_data == null || _modalContent == null)
                return;

            RenderCard("cloud", _data.Cloud);
            RenderCard("device", _data.ThisDevice);
        }

        private void RenderCard(string prefix, CloudSaveConflictCardData data)
        {
            SetText($"cloud-conflict__{prefix}-levels", data.CompletedLevels.ToString(CultureInfo.CurrentCulture));
            SetText($"cloud-conflict__{prefix}-money", data.Money.ToString(CultureInfo.CurrentCulture));
            SetText($"cloud-conflict__{prefix}-crystals", data.Crystals.ToString(CultureInfo.CurrentCulture));
            SetText($"cloud-conflict__{prefix}-saved", data.SavedAt.ToLocalTime().ToString(DateFormat, CultureInfo.CurrentCulture));
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
    }
}
