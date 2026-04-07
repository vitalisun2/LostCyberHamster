using Assets.Scripts;
using Assets.Scripts.Common.Models;
using System.Collections.Generic;
using System.IO;
using System;

public static class ObstacleSpriteTypeMappingsManager
{
    private static System.Collections.Generic.Dictionary<string, ObstacleTypeEnum> _bindings = new();
    private static string _currentLocation;

    private static readonly System.Text.RegularExpressions.Regex _frameSuffixRegex =
        new(@"(?:[-_]\d+)$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static string NormalizeSpriteName(string spriteName)
    {
        var shortName = System.IO.Path.GetFileNameWithoutExtension(spriteName.ToLower());
        // Убираем суффикс кадра (-0, -1, _0, _1, ...) из имён sub-sprite анимаций
        return _frameSuffixRegex.Replace(shortName, "");
    }

    /// <summary>
    /// Загружает маппинги из Addressables и нормализует имена.
    /// </summary>
    public static void LoadBindings(string location, Action<bool> callback)
    {
        _currentLocation = location;

        LevelDataManager.LoadMappingsFromAddressables(location, result =>
        {
            /* если файл отсутствует — как раньше */
            if (result == null)
            {
                _bindings = new();
                callback?.Invoke(false);
                return;
            }

            /* ─── отсеиваем записи без спрайтов ─── */
            var filtered = new Dictionary<string, ObstacleTypeEnum>();
            foreach (var kvp in result)
            {
                // Проверяем, существует ли файл спрайта
                var locSpritePath = Path.Combine(Consts.LocationsPath, location, "sprites", kvp.Key + ".png");
                var sharedSpritePath = Path.Combine(Consts.SharedSpritesPath, kvp.Key + ".png");
                if (File.Exists(locSpritePath) || File.Exists(sharedSpritePath))
                    filtered[NormalizeSpriteName(kvp.Key)] = kvp.Value; 
            }

            _bindings = filtered; 
            callback?.Invoke(true);

            /* если были удалённые записи — сразу сохраняем «очищенный» файл */
            if (filtered.Count < result.Count) SaveBindings(); 
        });
    }


    /// <summary>
    /// Возвращает тип препятствия для указанного спрайта.
    /// </summary>
    public static bool TryGetType(string spriteName, out ObstacleTypeEnum type)
    {
        var normKey = NormalizeSpriteName(spriteName);
        return _bindings.TryGetValue(normKey, out type);
    }

    /// <summary>
    /// Устанавливает тип препятствия для указанного спрайта.
    /// </summary>
    public static void SetType(string spriteName, ObstacleTypeEnum type)
    {
        var normKey = NormalizeSpriteName(spriteName);
        _bindings[normKey] = type;

        SaveBindings();
    }

    /// <summary>
    /// Сохраняет текущие биндинги в Addressables.
    /// </summary>
    public static void SaveBindings()
    {
        LevelDataManager.SaveMappingsToAddressables(_currentLocation, _bindings);
    }
}
