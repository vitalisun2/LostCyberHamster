using Assets.Scripts;
using Assets.Scripts.Common.Models;
using UnityEngine;
using UnityEngine.Tilemaps;

public class OverlapAvoidanceOnRoofRule : ITilePlacementRule
{
    private const float GapBetweenTiles = Consts.GapBetweenTiles;
    private const float GridSnapStep = Consts.GridSnapStep;
    private const float RoofLineTolerance = Consts.GridSnapStep * 0.5f + 0.001f;

    public bool TryApplyRule(Tilemap tilemap, Tile tile, ref Vector3 position)
    {
        // 1) Если это не объект для крыши — пропускаем
        if (!IsRoofObject(tile))
        {
            return true;
        }

        // 2) Приводим X к той же сетке, с которой работает road-rule
        SnapHorizontalPositionToGrid(ref position, GridSnapStep);

        // 3) Пытаемся устранить пересечение (по X) сдвигом
        bool didShift = TryResolveOverlapOnce(tilemap, tile, ref position);

        // 4) После сдвига снова привязываем X и проверяем итоговое положение
        if (didShift)
        {
            SnapHorizontalPositionToGrid(ref position, GridSnapStep);
        }

        if (didShift && OverlapsAnyTile(tilemap, tile, position))
        {
            Debug.LogWarning("[OverlapAvoidanceOnRoofRule] Повторное пересечение. Установка отменена.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Смотрим, нужно ли сдвинуть новый объект, чтобы избежать пересечения
    /// с уже стоящими объектами на той же «линии крыши».
    /// </summary>
    private bool TryResolveOverlapOnce(Tilemap tilemap, Tile newTile, ref Vector3 newPos)
    {
        // Координаты X-границ у нового тайла (после SnapToRoofRule)
        var (newXMin, newXMax) = GetHorizontalBounds(newTile, newPos);

        // Получаем «формальную» линию крыши для нового тайла
        float newRoofLine = GetRoofLineY(newPos);

        foreach (var posInBounds in tilemap.cellBounds.allPositionsWithin)
        {
            var otherTile = tilemap.GetTile<Tile>(posInBounds);
            if (otherTile == null)
                continue;

            // Только с другими объектами на крыше
            if (!IsRoofObject(otherTile))
                continue;

            // Координата, по которой вычисляем «линию крыши» для уже стоящего тайла
            var otherPos = TilemapPositionUtility.GetExactTileWorldPosition(tilemap, posInBounds);
            float oldRoofLine = GetRoofLineY(otherPos);

            // Если «линия крыши» у них отличается — пропускаем
            if (!IsSameRoofLine(newRoofLine, oldRoofLine))
                continue;

            // Теперь проверяем пересечение по X
            var (oldXMin, oldXMax) = GetHorizontalBounds(otherTile, otherPos);
            if (newXMax >= oldXMin && newXMin <= oldXMax)
            {
                // Находим, на сколько нужно сдвинуться
                float shiftX = CalculateShiftX(
                    oldXMin, oldXMax,
                    newXMin, newXMax,
                    GapBetweenTiles,
                    newPos.x,
                    otherPos.x
                );

                // Применяем сдвиг
                newPos.x += shiftX;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Проверяем, осталось ли пересечение после сдвига (аналогично TryResolveOverlapOnce),
    /// но без повторного сдвига.
    /// </summary>
    private bool OverlapsAnyTile(Tilemap tilemap, Tile newTile, Vector3 newPos)
    {
        // Координаты X-границ у нового тайла
        var (newXMin, newXMax) = GetHorizontalBounds(newTile, newPos);
        float newRoofLine = GetRoofLineY(newPos);

        foreach (var posInBounds in tilemap.cellBounds.allPositionsWithin)
        {
            var otherTile = tilemap.GetTile<Tile>(posInBounds);
            if (otherTile == null)
                continue;

            if (!IsRoofObject(otherTile))
                continue;

            var otherPos = TilemapPositionUtility.GetExactTileWorldPosition(tilemap, posInBounds);
            float oldRoofLine = GetRoofLineY(otherPos);

            if (!IsSameRoofLine(newRoofLine, oldRoofLine))
                continue;

            var (oldXMin, oldXMax) = GetHorizontalBounds(otherTile, otherPos);
            if (newXMax >= oldXMin && newXMin <= oldXMax)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Возвращает линию крыши для уже установленного объекта.
    /// </summary>
    private static float GetRoofLineY(Vector3 pivotPos)
    {
        return pivotPos.y;
    }

    /// <summary>
    /// Проверяет, относятся ли две Y-позиции к одной линии крыши.
    /// </summary>
    private static bool IsSameRoofLine(float leftY, float rightY)
    {
        return Mathf.Abs(leftY - rightY) <= RoofLineTolerance;
    }

    /// <summary>
    /// Проверяем, является ли данный тайл объектом, который можно ставить на крышу.
    /// </summary>
    private bool IsRoofObject(Tile tile)
    {
        if (tile?.sprite == null)
            return false;

        if (!ObstacleSpriteTypeMappingsManager.TryGetType(tile.sprite.name, out var foundType))
            return false;

        switch (foundType)
        {
            case ObstacleTypeEnum.collectableEnergetic:
            case ObstacleTypeEnum.collectablePizza:
            case ObstacleTypeEnum.collectableCrystal:
            case ObstacleTypeEnum.collectableLife:
            case ObstacleTypeEnum.collectableCoin:
            case ObstacleTypeEnum.smallNotAliveRoadAndRoof:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Возвращает левую и правую границы тайла (учитывая pivot = Bottom-Center).
    /// </summary>
    private static (float xMin, float xMax) GetHorizontalBounds(Tile tile, Vector3 centerPos)
    {
        float spriteWidth = tile.sprite.rect.width / tile.sprite.pixelsPerUnit;
        float halfWidth = spriteWidth * 0.5f;
        float xMin = centerPos.x - halfWidth;
        float xMax = centerPos.x + halfWidth;
        return (xMin, xMax);
    }

    /// <summary>
    /// Расчёт величины сдвига, чтобы выдержать зазор по X.
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
            return (oldXMin - gap) - newXMax;
        }
        else
        {
            return (oldXMax + gap) - newXMin;
        }
    }

    /// <summary>
    /// Привязка X к шагу GridSnapStep (0.2) без изменения Y, который задаёт линия крыши.
    /// </summary>
    private static void SnapHorizontalPositionToGrid(ref Vector3 pos, float step)
    {
        pos.x = Mathf.Round(pos.x / step) * step;
    }
}
