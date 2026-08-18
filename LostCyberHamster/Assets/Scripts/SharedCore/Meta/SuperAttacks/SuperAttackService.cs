using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Assets.Scripts.System.Resources;
using GameManagement;
using UnityEngine;

namespace Vues.GameCore
{
    /// <summary>
    /// Загружает данные суперспособностей и управляет active selection.
    /// </summary>
    public static class SuperAttackService
    {
        public const string CatalogAddress = "super_attacks";

        private static IReadOnlyList<SuperAttackData> _items = Array.Empty<SuperAttackData>();
        private static Dictionary<int, SuperAttackData> _itemsById = new();

        /// <summary>
        /// Все суперудары в порядке каталога.
        /// </summary>
        public static IReadOnlyList<SuperAttackData> Items => _items;

        /// <summary>
        /// ID выбранного суперудара или пустое значение до первого выбора.
        /// </summary>
        public static int? ActiveSuperAttackId
        {
            get
            {
                int id = GameDataManager.PlayerData.ActiveSuperAttackId;
                return id == 0 ? null : id;
            }
        }

        /// <summary>
        /// Загружает каталог суперударов из Addressables.
        /// </summary>
        public static async Task InitAsync()
        {
            // Загружаем прежние данные суперударов из отдельного JSON.
            using var lease = await AddressableLoader.LoadAssetAsync<TextAsset>(CatalogAddress);
            TextAsset textAsset = lease.Value;
            if (textAsset == null)
            {
                throw new InvalidOperationException(
                    $"Addressable '{CatalogAddress}' не содержит TextAsset.");
            }

            var dataList = JsonUtility.FromJson<SuperAttackDataList>(textAsset.text);
            if (dataList?.SuperAttacks == null)
            {
                throw new InvalidOperationException(
                    $"Addressable '{CatalogAddress}' не содержит список суперударов.");
            }

            var items = new List<SuperAttackData>(dataList.SuperAttacks.Count);
            var itemsById = new Dictionary<int, SuperAttackData>();

            // Проверяем данные и строим быстрый поиск по прежнему ID.
            foreach (SuperAttackData data in dataList.SuperAttacks)
            {
                Validate(data);

                if (!itemsById.TryAdd(data.Id, data))
                {
                    throw new InvalidOperationException(
                        $"Идентификатор суперудара должен быть уникальным: {data.Id}.");
                }

                items.Add(data);
            }

            if (items.Count == 0)
            {
                throw new InvalidOperationException(
                    "Каталог должен содержать хотя бы один суперудар.");
            }

            _items = items.AsReadOnly();
            _itemsById = itemsById;
        }

        /// <summary>
        /// Ищет суперудар по прежнему числовому идентификатору.
        /// </summary>
        public static bool TryGet(int id, out SuperAttackData data)
        {
            return _itemsById.TryGetValue(id, out data);
        }

        /// <summary>
        /// Проверяет persisted-открытие суперспособности.
        /// </summary>
        public static bool IsUnlocked(int id)
        {
            return TryGet(id, out _) &&
                   CharacterDevelopmentService.IsSuperAttackUnlocked(id);
        }

        /// <summary>
        /// Выбирает открытый суперудар и сохраняет его вместе с прогрессом квеста.
        /// </summary>
        public static bool TrySelect(int id)
        {
            // Проверяем доступность до изменения данных игрока.
            var playerData = GameDataManager.PlayerData;
            if (!IsUnlocked(id))
            {
                return false;
            }

            // Обновляем выбор и зависимый прогресс до единственного checkpoint.
            playerData.ActiveSuperAttackId = id;
            GameEventsManager.PlayerStateChanged(
                Quests.PlayerStateIds.SuperAttackActive,
                id.ToString(CultureInfo.InvariantCulture));
            PlayerProgressCommitter.Commit(
                CheckpointReason.SuperAttackSelected);
            return true;
        }

        private static void Validate(SuperAttackData data)
        {
            // Проверяем прежние обязательные данные суперудара.
            if (data == null)
            {
                throw new InvalidOperationException(
                    "Каталог суперударов содержит пустую запись.");
            }

            if (data.Id <= 0)
            {
                throw new InvalidOperationException(
                    "Идентификатор суперудара должен быть положительным.");
            }

            if (string.IsNullOrWhiteSpace(data.NameLocalizationKey) ||
                string.IsNullOrWhiteSpace(data.DescriptionLocalizationKey) ||
                string.IsNullOrWhiteSpace(data.IconAddress))
            {
                throw new InvalidOperationException(
                    $"Суперудар {data.Id} содержит пустые обязательные данные.");
            }

            if (data.UltaDuration < 0f || data.UltaCharge <= 0)
            {
                throw new InvalidOperationException(
                    $"Суперудар {data.Id} содержит неверные runtime-параметры.");
            }
        }
    }
}
