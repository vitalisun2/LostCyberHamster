using System;
using System.Threading;
using System.Threading.Tasks;
using Assets.Scripts.System.Resources;
using UnityEngine;

namespace Vues.GameCore
{
    /// <summary>
    /// Создаёт runtime существующего суперудара по стабильному ID каталога.
    /// </summary>
    public static class SuperAttackFactory
    {
        private const int EnergyShieldId = 1;
        private const int ElectricStrikeId = 2;

        /// <summary>
        /// Загружает prefab эффекта и передаёт runtime параметры из каталога.
        /// </summary>
        public static async Task<ISuperAttackRuntime> CreateAsync(
            SuperAttackData data,
            CancellationToken cancellationToken = default)
        {
            // Проверяем регистрацию runtime до загрузки ресурсов.
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            ValidateSupportedId(data.Id);

            // Загружаем эффект на время жизни забега.
            AddressableLease<GameObject> effectPrefabLease =
                await AddressableLoader.LoadAssetAsync<GameObject>(
                    data.UltaPrefab,
                    cancellationToken);

            try
            {
                // Создаём прежнюю gameplay-реализацию с параметрами каталога.
                if (effectPrefabLease.Value == null)
                {
                    throw new InvalidOperationException(
                        $"Addressable '{data.UltaPrefab}' не содержит prefab.");
                }

                return data.Id switch
                {
                    EnergyShieldId => new EnergyShieldAttack(
                        effectPrefabLease,
                        data.UltaDuration,
                        data.UltaCharge),
                    ElectricStrikeId => new ElectricStrikeAttack(
                        effectPrefabLease,
                        data.UltaCharge),
                    _ => throw new InvalidOperationException(
                        $"Не поддерживается ID суперудара: {data.Id}.")
                };
            }
            catch
            {
                effectPrefabLease.Dispose();
                throw;
            }
        }

        private static void ValidateSupportedId(int id)
        {
            if (id != EnergyShieldId && id != ElectricStrikeId)
            {
                throw new NotSupportedException(
                    $"Не поддерживается ID суперудара: {id}.");
            }
        }
    }
}
