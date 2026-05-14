using Assets.Scripts.Common.Models;
using Assets.Scripts;
using UnityEngine;
using UnityEngine.Tilemaps;

public class SnapToRoofRule : ITilePlacementRule
{
    public bool TryApplyRule(Tilemap tilemap, Tile tile, ref Vector3 position)
    {
        var distToY0 = Mathf.Abs(position.y - Consts.ObstacleY0Pos);
        var distToY1 = Mathf.Abs(position.y - Consts.ObstacleY1Pos);
        var isCloserToY0 = distToY0 < distToY1;
        var mainLineY = isCloserToY0 ? Consts.ObstacleY0Pos : Consts.ObstacleY1Pos;
        var otherLineY = isCloserToY0 ? Consts.ObstacleY1Pos : Consts.ObstacleY0Pos;

        // 1) Пробуем «привязаться» к BigNotAlive / MediumNotAlive на ближайшей линии
        if (TrySnapObjectToRoof(tilemap, tile, ref position, mainLineY))
        {
            return true;
        }

        // 2) Если не получилось — пробуем другую линию
        if (TrySnapObjectToRoof(tilemap, tile, ref position, otherLineY))
        {
            return true;
        }

        // 3) Ни на одной линии не нашли, куда поместить объект
        Debug.Log("[SnapToRoofRule] Не нашли подходящий BigNotAlive / MediumNotAlive. Объект на крышу не ставим.");
        return false;
    }

    /// <summary>
    /// Пытается поставить объект на крышу BigNotAlive или MediumNotAlive,
    /// если он полностью влезает по горизонтали. Возвращает true при успехе.
    /// </summary>
    private bool TrySnapObjectToRoof(
        Tilemap tilemap,
        Tile newTile,
        ref Vector3 newPos,
        float lineY)
    {
        // Получаем мировые границы нового объекта (xMin, xMax, top)
        var (newXMin, newXMax, _) = GetSpriteWorldBounds(newTile, newPos);

        foreach (var cellPos in tilemap.cellBounds.allPositionsWithin)
        {
            var existingTile = tilemap.GetTile<Tile>(cellPos);
            if (existingTile == null || existingTile.sprite == null)
                continue;

            if (!ObstacleSpriteTypeMappingsManager.TryGetType(existingTile.sprite.name, out var obstacleType))
                continue;

            if (obstacleType != ObstacleTypeEnum.bigNotAlive && obstacleType != ObstacleTypeEnum.mediumNotAlive)
                continue;

            // Проверяем, что этот объект на нужной линии
            var existingPivotPos = TilemapPositionUtility.GetExactTileWorldPosition(tilemap, cellPos);
            if (Mathf.Abs(existingPivotPos.y - lineY) > 0.001f)
                continue;

            // Берём границы родительского тайла
            var (oldXMin, oldXMax, oldTopY) = GetSpriteWorldBounds(existingTile, existingPivotPos);

            // Проверяем полное вхождение по горизонтали
            // (не торчит за левый или правый край)
            var fullyInside = (newXMin >= oldXMin && newXMax <= oldXMax);
            if (!fullyInside)
            {
                // Не влезает — пробуем следующий тайл
                continue;
            }

            // Если полностью влезает, «ставим» объект на крышу
            newPos.y = oldTopY + Consts.RoofOffset;
            Debug.Log($"[SnapToRoofRule] Объект целиком влез. Ставим на крышу: X={newPos.x:F2}, Y={newPos.y:F2}");
            return true;
        }

        // Ни один BigNotAlive / MediumNotAlive на этой линии не подошёл
        return false;
    }

    /// <summary>
    /// Унифицированный метод для получения мировых границ (xMin, xMax) 
    /// и верхней точки (yTop) спрайта с учётом pivot.
    /// </summary>
    private (float xMin, float xMax, float yTop) GetSpriteWorldBounds(Tile tile, Vector3 pivotPos)
    {
        var b = tile.sprite.bounds;

        var localCenterX = b.center.x;
        var localHalfWidth = b.extents.x;
        var localCenterY = b.center.y;
        var localHalfHeight = b.extents.y;

        // Перевод локальных координат спрайта (учитывающих pivot) в мировые
        var xMin = pivotPos.x + (localCenterX - localHalfWidth);
        var xMax = pivotPos.x + (localCenterX + localHalfWidth);
        var yTop = pivotPos.y + (localCenterY + localHalfHeight);

        return (xMin, xMax, yTop);
    }
}
