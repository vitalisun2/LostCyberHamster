using Assets.Scripts;
using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class LevelDataValidator
{
    /// <summary>
    /// Validates that a texture's width and height are divisible by 4 (required by ETC2 compression).
    /// </summary>
    /// <param name="texture">Texture to validate</param>
    /// <param name="context">Context string for error messages (e.g., asset purpose/name)</param>
    public static void ValidateTextureDivisibleBy4(Texture2D texture, string context)
    {
        if (texture == null)
        {
            HelpMethods.LogAndStopGame("[LevelDataValidator.ValidateTextureDivisibleBy4] Texture is null: " + context);
            return;
        }

        if ((texture.width % 4) != 0 || (texture.height % 4) != 0)
        {
            HelpMethods.LogAndStopGame(
                $"[LevelDataValidator.ValidateTextureDivisibleBy4] {context}: texture '{texture.name}' has size {texture.width}x{texture.height}. For ETC2 compression both width and height must be divisible by 4."
            );
        }
    }

    public static void ValidateCollectableSprites(List<Sprite> sprites)
    {
        if (sprites == null || sprites.Count == 0)
        {
            HelpMethods.LogAndStopGame("[LevelDataValidator.ValidateCollectableSprites] The sprite list is null or empty.");
            return;
        }

        foreach (var sprite in sprites)
        {
            if (sprite == null)
            {
                HelpMethods.LogAndStopGame("[LevelDataValidator.ValidateCollectableSprites] A null sprite was found.");
                return;
            }
            if (sprite.texture.width != Consts.BONUS_WIDTH || sprite.texture.height != Consts.BONUS_HEIGHT)
            {
                HelpMethods.LogAndStopGame(
                    $"[LevelDataValidator.ValidateCollectableSprites] Sprite '{sprite.name}' has resolution {sprite.texture.width}x{sprite.texture.height}, expected {Consts.BONUS_WIDTH}x{Consts.BONUS_HEIGHT}."
                );
                return;
            }

            // ETC2 requirement
            ValidateTextureDivisibleBy4(sprite.texture, $"Collectable sprite '{sprite.name}'");
        }
    }

    public static void ValidateDecorSprites(List<Sprite> levelDataDecorSprites)
    {
        const string methodTag = "[LevelDataValidator.ValidateDecorSprites]";

        if (levelDataDecorSprites?.Count is null or 0)
        {
            HelpMethods.LogAndStopGame($"{methodTag} The sprite list is null or empty.");
            return;
        }

        var nullSprite = levelDataDecorSprites.FirstOrDefault(sprite => sprite == null);
        if (nullSprite != null)
        {
            HelpMethods.LogAndStopGame($"{methodTag} A null sprite was found.");
        }

        // Decor sprites: only validate ETC2 divisibility, no fixed size requirement
        foreach (var sprite in levelDataDecorSprites)
        {
            if (sprite != null)
            {
                ValidateTextureDivisibleBy4(sprite.texture, $"Decor sprite '{sprite.name}'");
            }
        }
    }

    public static void ValidateBackgroundTexture(Sprite backgroundSprite)
    {
        if (backgroundSprite == null)
        {
            HelpMethods.LogAndStopGame("[LevelDataValidator.ValidateBackgroundTexture] Background sprite is null.");
            return;
        }

        if (backgroundSprite.texture.width != Consts.BACKGROUND_WIDTH || backgroundSprite.texture.height != Consts.BACKGROUND_HEIGHT)
        {
            HelpMethods.LogAndStopGame(
                $"[LevelDataValidator.ValidateBackgroundTexture] Background sprite '{backgroundSprite.name}' has resolution {backgroundSprite.texture.width}x{backgroundSprite.texture.height}, expected {Consts.BACKGROUND_WIDTH}x{Consts.BACKGROUND_HEIGHT}."
            );
        }

        // ETC2 requirement
        ValidateTextureDivisibleBy4(backgroundSprite.texture, $"Background sprite '{backgroundSprite.name}'");
    }

    public static void ValidateObstacleSprite(ObstacleTypeEnum obstacleType, string spriteName, Sprite sprite)
    {
        if (sprite == null)
        {
            HelpMethods.LogAndStopGame(
                $"[BaseStorageUI] Не удалось загрузить Sprite '{spriteName}'. Возможные причины:\n" +
                $"1) Текстура была перезаписана или изменена вне Unity, и Import Settings теперь не совпадают:\n" +
                $"   - Возможные симптомы:\n" +
                $"     • Размер текстуры не соответствует Sprite Rect (\"... is outside the bounds...\").\n" +
                $"     • Texture Type сбит с 'Sprite (2D and UI)' на 'Default' и т.п.\n" +
                $"   - Рекомендация: Поправьте настройки текстуры\n" +
                $"\n" +
                $"2) Ошибка в ключе: '{spriteName}' не совпадает с именем/адресом в Addressables.\n"
            );

            return;
        }

        void CheckSize(int requiredWidth, int requiredHeight, string typeName)
        {
            if (sprite.texture.width != requiredWidth || sprite.texture.height != requiredHeight)
            {
                HelpMethods.LogAndStopGame(
                    $"[LevelDataValidator.ValidateObstacleSprite] Sprite '{sprite.name}' " +
                    $"has resolution {sprite.texture.width}x{sprite.texture.height}, " +
                    $"expected {requiredWidth}x{requiredHeight} ({typeName})."
                );
            }
        }

        switch (obstacleType)
        {
            case ObstacleTypeEnum.bigNotAlive:
                CheckSize(Consts.BIG_NOTALIVE_WIDTH, Consts.BIG_NOTALIVE_HEIGHT, "bigNotAlive");
                break;

            case ObstacleTypeEnum.mediumNotAlive:
                CheckSize(Consts.MEDIUM_NOTALIVE_WIDTH, Consts.MEDIUM_NOTALIVE_HEIGHT, "mediumNotAlive");
                break;

            case ObstacleTypeEnum.bigAlive:
                CheckSize(Consts.BIG_ALIVE_WIDTH, Consts.BIG_ALIVE_HEIGHT, "bigAlive");
                break;

            case ObstacleTypeEnum.smallAlive:
                CheckSize(Consts.SMALL_ALIVE_WIDTH, Consts.SMALL_ALIVE_HEIGHT, "smallAlive");
                break;

            case ObstacleTypeEnum.smallNotAliveRoad:
                CheckSize(Consts.SMALL_NOTALIVE_WIDTH, Consts.SMALL_NOTALIVE_HEIGHT, "smallNotAliveRoad");
                break;

            case ObstacleTypeEnum.smallNotAliveRoadAndRoof:
                CheckSize(Consts.SMALL_NOTALIVE_WIDTH, Consts.SMALL_NOTALIVE_HEIGHT, "smallNotAliveRoadAndRoof");
                break;
        }

    // ETC2 requirement
    ValidateTextureDivisibleBy4(sprite.texture, $"Obstacle sprite '{sprite.name}'");
    }
}
