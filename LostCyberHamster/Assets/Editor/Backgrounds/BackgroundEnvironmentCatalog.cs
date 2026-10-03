using System;
using System.Collections.Generic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System.LevelManagement;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace LostCyberHamster.Editor.Backgrounds
{
    /// <summary>Проверяет готовые префабы окружений по существующей структуре контента.</summary>
    public static class BackgroundEnvironmentCatalog
    {
        /// <summary>Читает статусы пар локация/время суток без изменений ассетов и сцен.</summary>
        public static IReadOnlyList<BackgroundEnvironmentCatalogEntry> Read(IReadOnlyList<string> locationIds,
            IReadOnlyList<string> dayparts)
        {
            // Одни настройки Addressables обслуживают весь снимок каталога.
            var entries = new List<BackgroundEnvironmentCatalogEntry>(locationIds.Count * dayparts.Count);
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            // Проверяем каждую каноническую пару локации и времени суток.
            foreach (var locationId in locationIds)
            foreach (var daypart in dayparts)
                entries.Add(ReadEntry(locationId, daypart, settings));
            return entries;
        }

        /// <summary>Проверяет четыре роли и адрес одного сохранённого префаба.</summary>
        private static BackgroundEnvironmentCatalogEntry ReadEntry(string locationId, string daypart,
            AddressableAssetSettings settings)
        {
            // Канонические пути совпадают с путями экспорта инструмента.
            var prefabPath = BackgroundAssetExporter.GetPrefabPath(locationId, daypart);
            var spriteDirectory = BackgroundAssetExporter.GetSpriteDirectory(locationId);
            var address = EnvironmentKeyResolver.BuildEnvironmentKey(locationId, daypart);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var sprites = new Sprite[BackgroundAuthoringSession.Roles.Length];
            var spritePaths = new string[sprites.Length];
            string error = null;

            // Отсутствующий префаб означает ещё не подготовленный вариант.
            if (prefab == null)
            {
                if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(prefabPath)))
                    error = "Ассет по ожидаемому пути не является префабом GameObject.";
            }
            else
            {
                try
                {
                    // Ссылки на четыре рисунка остаются доступны и при ошибке конфигурации.
                    var environment = prefab.GetComponent<LocationEnvironment>();
                    if (environment == null)
                        throw new InvalidOperationException("В префабе отсутствует компонент LocationEnvironment.");
                    for (var i = 0; i < sprites.Length; i++)
                    {
                        sprites[i] = environment.GetLayer(BackgroundAuthoringSession.Roles[i])?.sprite;
                        spritePaths[i] = sprites[i] != null ? AssetDatabase.GetAssetPath(sprites[i]) : string.Empty;
                    }
                    environment.ValidateConfiguration();

                    // Готовый префаб должен загружаться по тому же адресу, что использует игра.
                    if (settings == null)
                        throw new InvalidOperationException("Настройки Addressables не найдены.");
                    var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(prefabPath));
                    if (entry == null)
                        throw new InvalidOperationException($"Префаб не зарегистрирован в Addressables: {address}.");
                    if (!string.Equals(entry.address, address, StringComparison.Ordinal))
                        throw new InvalidOperationException($"Адрес Addressables '{entry.address}'; требуется '{address}'.");
                }
                catch (Exception exception)
                {
                    error = exception.Message;
                }
            }
            return new BackgroundEnvironmentCatalogEntry(locationId, daypart, prefabPath, spriteDirectory,
                address, prefab, sprites, spritePaths, error);
        }
    }
}
