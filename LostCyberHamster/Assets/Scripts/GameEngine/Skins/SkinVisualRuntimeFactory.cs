using System;
using System.Threading.Tasks;
using Assets.Scripts.Gameplay;
using UnityEngine;

namespace Assets.Scripts.GameEngine.Skins
{
    /// <summary>
    /// Создаёт normal и skateboard visuals выбранного скина с fallback на default.
    /// </summary>
    public static class SkinVisualRuntimeFactory
    {
        /// <summary>
        /// Загружает оба mode-visual; отсутствующий или ошибочный вариант заменяет default.
        /// </summary>
        public static async Task<(SkinVisualRuntime Normal, SkinVisualRuntime Skateboard)>
            CreateSelectedAsync(Hamster hamster)
        {
            if (hamster == null)
                throw new ArgumentNullException(nameof(hamster));

            // Разрешаем выбранный и обязательный default descriptors обоих modes.
            Skin selected = SkinManager.CurrentSkin ?? SkinManager.DefaultSkin;
            Skin defaultSkin = SkinManager.DefaultSkin;
            if (selected == null || defaultSkin == null)
                throw new InvalidOperationException("Skin catalog has no default skin.");

            if (string.IsNullOrWhiteSpace(defaultSkin.SkinVisualAddress) ||
                string.IsNullOrWhiteSpace(defaultSkin.SkateboardSkinVisualAddress))
            {
                throw new InvalidOperationException(
                    "Default skin must define normal and skateboard visual addresses.");
            }

            // Normal visual создаётся первым и освобождается при ошибке второго mode.
            SkinVisualRuntime normal = await CreateModeAsync(
                selected.SkinVisualAddress,
                defaultSkin.SkinVisualAddress,
                hamster.NormalSkinVisualHost,
                "normal");

            try
            {
                SkinVisualRuntime skateboard = await CreateModeAsync(
                    selected.SkateboardSkinVisualAddress,
                    defaultSkin.SkateboardSkinVisualAddress,
                    hamster.SkateboardSkinVisualHost,
                    "skateboard");
                return (normal, skateboard);
            }
            catch
            {
                normal.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Загружает visual одного mode и один раз пробует default при ошибке.
        /// </summary>
        private static async Task<SkinVisualRuntime> CreateModeAsync(
            string selectedAddress,
            string defaultAddress,
            SkinVisualHost host,
            string modeName)
        {
            // Пустой адрес означает готовый fallback descriptor, а не ошибку загрузки.
            string resolvedAddress = string.IsNullOrWhiteSpace(selectedAddress)
                ? defaultAddress
                : selectedAddress;
            if (string.Equals(resolvedAddress, defaultAddress, StringComparison.Ordinal))
                return await SkinVisualRuntime.CreateAsync(defaultAddress, host);

            // Ошибка non-default visual даёт одну попытку загрузить default.
            try
            {
                return await SkinVisualRuntime.CreateAsync(resolvedAddress, host);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Failed to load {modeName} skin visual '{resolvedAddress}'. " +
                    $"Default visual will be used. {exception.Message}");
                return await SkinVisualRuntime.CreateAsync(defaultAddress, host);
            }
        }
    }
}
