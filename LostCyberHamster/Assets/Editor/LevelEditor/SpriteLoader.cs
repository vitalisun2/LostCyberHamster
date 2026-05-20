using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public static class SpriteLoader
{
    private static readonly Dictionary<string, Sprite> _spriteCache = new();
    private static readonly Dictionary<string, AsyncOperationHandle> _handleCache = new();

    private static readonly Regex _frameSuffixRegex = new(@"-\d+$", RegexOptions.Compiled);

    /// <summary>
    /// Удаляет суффикс кадра анимации (-0, -1, ...) из имени спрайта.
    /// Конвенция: кадры анимации всегда через дефис (ObstacleAnimationImporter).
    /// </summary>
    public static string StripFrameSuffix(string spriteName)
    {
        return string.IsNullOrEmpty(spriteName) ? spriteName : _frameSuffixRegex.Replace(spriteName, "");
    }

    /// <summary>
    /// Регистрирует ранее загруженный спрайт в кеше (например, из label-загрузки).
    /// </summary>
    public static void RegisterSprite(string name, Sprite sprite)
    {
        if (!string.IsNullOrEmpty(name) && sprite != null)
            _spriteCache[name] = sprite;
    }

    public static Sprite LoadSpriteSync(string spriteName)
    {
        if (string.IsNullOrWhiteSpace(spriteName))
        {
            Debug.LogWarning("SpriteLoader.LoadSpriteSync called with empty sprite key.");
            return null;
        }

        if (_spriteCache.TryGetValue(spriteName, out var cachedSprite))
            return cachedSprite;

        // Адресуемся к Addressables и ждём завершения
        var handle = Addressables.LoadAssetAsync<Sprite>(spriteName);
        var loadedSprite = handle.WaitForCompletion();

        if (loadedSprite != null)
        {
            _spriteCache[spriteName] = loadedSprite;
            _handleCache[spriteName] = handle;
            return loadedSprite;
        }

        Debug.LogError($"Не удалось синхронно загрузить спрайт: {spriteName}");
        return null;
    }


    public static void LoadSpritesByTag(string tag, System.Action<List<Sprite>> callback)
    {
        var sprites = new List<Sprite>();

        // Load all assets with the specified tag
        AsyncOperationHandle<IList<Sprite>> handle = Addressables.LoadAssetsAsync<Sprite>(tag, null);

        handle.Completed += h =>
        {
            if (h.Status == AsyncOperationStatus.Succeeded)
            {
                foreach (var sprite in h.Result)
                {
                    if (!_spriteCache.ContainsKey(sprite.name))
                    {
                        _spriteCache[sprite.name] = sprite;
                    }
                    sprites.Add(sprite);
                }

                // Cache the handle for the list of sprites using the tag as the key
                _handleCache[tag] = handle;

                callback?.Invoke(sprites);
            }
            else
            {
                Debug.LogError($"Failed to load sprites with tag: {tag}");
                callback?.Invoke(null);
                Addressables.Release(handle);
            }
        };
    }

    public static List<Sprite> LoadSpritesSyncByTag(string tag)
    {
        // Синхронная загрузка всех спрайтов с указанным тегом
        var sprites = new List<Sprite>();
        var handle = Addressables.LoadAssetsAsync<Sprite>(tag, null);
        handle.WaitForCompletion(); // Ожидание завершения загрузки

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            sprites.AddRange(handle.Result);
        }
        else
        {
            Debug.LogError($"Failed to load sprites with tag: {tag}");
        }

        Addressables.Release(handle);
        return sprites;
    }


    // Дополнительно: метод для освобождения конкретного спрайта
    public static void ReleaseSprite(string spriteName)
    {
        if (_handleCache.TryGetValue(spriteName, out AsyncOperationHandle handle))
        {
            Addressables.Release(handle);
            _handleCache.Remove(spriteName);
            _spriteCache.Remove(spriteName);
        }
    }

    public static void ReleaseSpritesAndClearCache()
    {
        // Освобождаем каждый хэндл (иначе спрайты останутся в памяти)
        foreach (var handle in _handleCache.Values)
        {
            Addressables.Release(handle);
        }

        _handleCache.Clear();
        _spriteCache.Clear();
    }
}
