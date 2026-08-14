using System;
using System.Threading;
using System.Threading.Tasks;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
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
        private const int SkateboardId = 3;

        /// <summary>
        /// Создаёт runtime и загружает effect prefab только для атак, которым он нужен.
        /// </summary>
        public static async Task<ISuperAttackRuntime> CreateAsync(
            SuperAttackData data,
            Hamster hamster,
            GameManager gameManager,
            CancellationToken cancellationToken = default)
        {
            // Проверяем регистрацию runtime до загрузки ресурсов.
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            ValidateSupportedId(data.Id);

            // Skateboard использует actor Hamster и не требует отдельного effect prefab.
            if (data.Id == SkateboardId)
            {
                return new SkateboardAttack(
                    hamster,
                    gameManager,
                    data.UltaDuration,
                    data.UltaCharge);
            }

            if (string.IsNullOrWhiteSpace(data.UltaPrefab))
            {
                throw new InvalidOperationException(
                    $"Суперудар {data.Id} не содержит адрес prefab эффекта.");
            }

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
            if (id != EnergyShieldId &&
                id != ElectricStrikeId &&
                id != SkateboardId)
            {
                throw new NotSupportedException(
                    $"Не поддерживается ID суперудара: {id}.");
            }
        }
    }
}
