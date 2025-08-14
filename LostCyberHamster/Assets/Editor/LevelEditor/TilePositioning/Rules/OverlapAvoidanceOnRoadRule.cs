using Assets.Scripts;
using UnityEngine;
using UnityEngine.Tilemaps;

public class OverlapAvoidanceOnRoadRule : ITilePlacementRule
{
    public bool TryApplyRule(Tilemap tilemap, Tile tile, ref Vector3 position)
    {
        // Шаг 1: Ищем первое пересечение и (при наличии) сдвигаем тайл
        bool didShift = TryResolveOverlapOnce(tilemap, tile, ref position);

        // Шаг 2: Если действительно сдвигались — проверяем, не появилось ли новое пересечение
        if (didShift)
        {
            // Если теперь пересекаемся — места на линии действительно не хватило
            if (OverlapsAnyTile(tilemap, tile, position))
            {
                Debug.LogWarning(
                    "[OverlapAvoidanceRule] Второе пересечение! " +
                    "Недостаточно места для установки тайла."
                );
                return false; // Запрещаем установку
            }
        }

        // Если дошли сюда, значит пересечения либо не было, либо оно решилось одним сдвигом
        // и не породило нового конфликта. Привязываем позицию к шагу 0.2 (убираем дробные хвосты)
        SnapPositionToGrid(ref position, Consts.GridSnapStep);
        return true;
    }

    /// <summary>
    /// Пытается найти первое пересечение (по X) и сдвигает тайл.
    /// Возвращает true, если сдвиг был, и false, если пересечений не нашлось.
    /// </summary>
    private bool TryResolveOverlapOnce(Tilemap tilemap, Tile tile, ref Vector3 position)
    {
        var currentY = position.y;
        var (newXMin, newXMax) = GetHorizontalBounds(tile, position);

        foreach (var posInBounds in tilemap.cellBounds.allPositionsWithin)
        {
            var otherTile = tilemap.GetTile<Tile>(posInBounds);
            if (otherTile?.sprite == null)
                continue;

            var otherWorldPos = tilemap.CellToWorld(posInBounds);
            if (!Mathf.Approximately(otherWorldPos.y, currentY))
                continue;

            var (oldXMin, oldXMax) = GetHorizontalBounds(otherTile, otherWorldPos);

            // Проверяем пересечение
            if (newXMax >= oldXMin && newXMin <= oldXMax)
            {
                // Находим, на сколько нужно сдвинуться
                var shiftX = CalculateShiftX(
                    oldXMin, oldXMax,
                    newXMin, newXMax, Consts.GapBetweenTiles,
                    position.x,
                    otherWorldPos.x
                );

                // Применяем сдвиг
                position.x += shiftX;
                Debug.Log(
                    $"[OverlapAvoidanceRule] Первое пересечение с '{otherTile.sprite.name}'. " +
                    $"Сдвиг = {shiftX:F3}, новая X={position.x:F3}, зазор={Consts.GapBetweenTiles}"
                );

                return true; // Сдвиг сделан
            }
        }

        return false; // Пересечений нет — сдвиг не нужен
    }

    /// <summary>
    /// Проверяет, пересекается ли (по X) указанный тайл с каким-либо другим
    /// после сдвига (или изначально, если нужно).
    /// Если найдёт пересечение — возвращает true.
    /// </summary>
    private bool OverlapsAnyTile(Tilemap tilemap, Tile tile, Vector3 position)
    {
        float currentY = position.y;
        var (newXMin, newXMax) = GetHorizontalBounds(tile, position);

        foreach (var posInBounds in tilemap.cellBounds.allPositionsWithin)
        {
            var otherTile = tilemap.GetTile<Tile>(posInBounds);
            if (otherTile?.sprite == null)
                continue;

            var otherWorldPos = tilemap.CellToWorld(posInBounds);
            if (!Mathf.Approximately(otherWorldPos.y, currentY))
                continue;

            var (oldXMin, oldXMax) = GetHorizontalBounds(otherTile, otherWorldPos);

            if (newXMax >= oldXMin && newXMin <= oldXMax)
            {
                // Нашли "второе" пересечение
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Считает левую / правую границу тайла (Pivot = Bottom-Center).
    /// </summary>
    private static (float xMin, float xMax) GetHorizontalBounds(Tile tile, Vector3 centerPos)
    {
        float spriteWidth = tile.sprite.rect.width / tile.sprite.pixelsPerUnit;
        float xMin = centerPos.x - spriteWidth * 0.5f;
        float xMax = centerPos.x + spriteWidth * 0.5f;
        return (xMin, xMax);
    }

    /// <summary>
    /// Определяем величину сдвига при пересечении,
    /// чтобы между тайлами оставался фиксированный зазор.
    /// </summary>
    private static float CalculateShiftX(
        float oldXMin, float oldXMax,
        float newXMin, float newXMax,
        float gap,
        float newCenterX,
        float oldCenterX)
    {
        if (newCenterX < oldCenterX)
        {
            // «Прижимаем» правую границу нового к (левой границе старого - gap)
            return (oldXMin - gap) - newXMax;
        }
        else
        {
            // «Прижимаем» левую границу нового к (правой границе старого + gap)
            return (oldXMax + gap) - newXMin;
        }
    }

    /// <summary>
    /// Привязка позиции к шагу GridSnapStep (0.2).
    /// </summary>
    private static void SnapPositionToGrid(ref Vector3 pos, float step)
    {
        pos.x = Mathf.Round(pos.x / step) * step;
        pos.y = Mathf.Round(pos.y / step) * step;
    }
}
