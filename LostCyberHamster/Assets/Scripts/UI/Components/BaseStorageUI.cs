using Assets.Scripts.Common;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UIElements;

namespace LostCyberHamster.UI
{
    public abstract class BaseStorageUI : VisualElement
    {
        protected Label _label => this.Q<Label>("storage__label");
        public Button ButtonAdd => this.Q<Button>("storage__btn-add");
        protected abstract string _imageAssetName { get; }

        public BaseStorageUI()
        {
            Init();
        }

        protected void Init()
        {
            var op = Addressables.LoadAssetAsync<VisualTreeAsset>("StorageUI");
            op.WaitForCompletion();
            var assetTree = op.Result;
            assetTree.CloneTree(this);
            Addressables.Release(op);

            var handle = Addressables.LoadAssetAsync<Sprite>(_imageAssetName.ToLower());
            handle.WaitForCompletion();

            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
            {
                HelpMethods.LogAndStopGame(
                    $"[BaseStorageUI] Не удалось загрузить Sprite '{_imageAssetName}'. Возможные причины:\n" +
                    $"1) Текстура была перезаписана или изменена вне Unity, и Import Settings теперь не совпадают:\n" +
                    $"   - Возможные симптомы:\n" +
                    $"     • Размер текстуры не соответствует Sprite Rect (\"... is outside the bounds...\").\n" +
                    $"     • Texture Type сбит с 'Sprite (2D and UI)' на 'Default' и т.п.\n" +
                    $"   - Рекомендация: Поправьте настройки текстуры\n" +
                    $"\n" +
                    $"2) Ошибка в ключе: '{_imageAssetName}' не совпадает с именем/адресом в Addressables.\n"
                );
                return;
            }

            var sprite = handle.Result;
            Addressables.Release(handle);

            schedule.Execute(() =>
            {
                var image = this.Q<VisualElement>("storage__image");
                image.style.backgroundImage = sprite.texture;

                if (_label == null)
                    return;

                UpdateText();
            }).Every(100);
        }

        protected abstract void UpdateText();
    }
}
