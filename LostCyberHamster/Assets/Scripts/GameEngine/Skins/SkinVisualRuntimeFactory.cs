using System;
using System.Threading.Tasks;
using Assets.Scripts.Gameplay;
using UnityEngine;

namespace Assets.Scripts.GameEngine.Skins
{
    /// <summary>
    /// Создаёт visual выбранного скина с обязательным fallback на default.
    /// </summary>
    public static class SkinVisualRuntimeFactory
    {
        /// <summary>
        /// Загружает выбранный visual; при его ошибке один раз пробует default.
        /// </summary>
        public static async Task<SkinVisualRuntime> CreateSelectedAsync(Hamster hamster)
        {
            if (hamster == null)
                throw new ArgumentNullException(nameof(hamster));

            // Разрешаем выбранный и обязательный default descriptors.
            Skin selected = SkinManager.CurrentSkin ?? SkinManager.DefaultSkin;
            Skin defaultSkin = SkinManager.DefaultSkin;
            if (selected == null || defaultSkin == null)
                throw new InvalidOperationException("Skin catalog has no default skin.");

            // Ошибка non-default visual не должна ломать создание Hamster.
            try
            {
                return await SkinVisualRuntime.CreateAsync(
                    selected.SkinVisualAddress,
                    hamster.SkinVisualHost);
            }
            catch (Exception exception) when (selected.Id != defaultSkin.Id)
            {
                Debug.LogWarning(
                    $"Failed to load skin visual '{selected.SkinVisualAddress}'. " +
                    $"Default visual will be used. {exception.Message}");
                return await SkinVisualRuntime.CreateAsync(
                    defaultSkin.SkinVisualAddress,
                    hamster.SkinVisualHost);
            }
        }
    }
}
