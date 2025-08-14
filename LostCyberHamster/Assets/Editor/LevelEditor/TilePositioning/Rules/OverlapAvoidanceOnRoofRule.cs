using UnityEngine;
using UnityEngine.Tilemaps;

public class OverlapAvoidanceOnRoofRule : ITilePlacementRule
{
    private const float GapBetweenTiles = 0.2f;
    private const float GridSnapStep = 0.2f;

    public bool TryApplyRule(Tilemap tilemap, Tile tile, ref Vector3 position)
    {
        // 1) Если это не коллектабл — пропускаем
        if (!IsCollectable(tile))
        {
            return true;
        }

        // 2) Пытаемся устранить пересечение (по X) сдвигом
        bool didShift = TryResolveOverlapOnce(tilemap, tile, ref position);

        // 3) Если мы сдвигались, то проверяем повторно
        if (didShift && OverlapsAnyTile(tilemap, tile, position))
        {
            Debug.LogWarning("[OverlapAvoidanceOnRoofRule] Повторное пересечение. Установка отменена.");
            return false;
        }

        // 4) Привязываем к сетке
        SnapPositionToGrid(ref position, GridSnapStep);
        return true;
    }

    /// <summary>
    /// Смотрим, нужно ли сдвинуть новый коллектабл, чтобы избежать пересечения
    /// с уже стоящими коллектаблами на той же «линии крыши».
    /// </summary>
    private bool TryResolveOverlapOnce(Tilemap tilemap, Tile newTile, ref Vector3 newPos)
    {
        // Координаты X-границ у нового тайла (после SnapToRoofRule)
        var (newXMin, newXMax) = GetHorizontalBounds(newTile, newPos);

        // Получаем «формальную» линию крыши для нового тайла
        float newRoofLine = ComputeRoofLineY(tilemap, newTile, newPos);

        foreach (var posInBounds in tilemap.cellBounds.allPositionsWithin)
        {
            var otherTile = tilemap.GetTile<Tile>(posInBounds);
            if (otherTile == null)
                continue;

            // Только с другими коллектаблами
            if (!IsCollectable(otherTile))
                continue;

            // Координата, по которой вычисляем «линию крыши» для уже стоящего тайла
            var otherPos = tilemap.CellToWorld(posInBounds);
            float oldRoofLine = ComputeRoofLineY(tilemap, otherTile, otherPos);

            // Если «линия крыши» у них отличается — пропускаем
            if (Mathf.Abs(newRoofLine - oldRoofLine) > 0.001f)
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
                Debug.Log($"[OverlapAvoidanceOnRoofRule] Пересечение с '{otherTile.sprite.name}', cдвиг={shiftX:F3}, новаяX={newPos.x:F3}");
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
        float newRoofLine = ComputeRoofLineY(tilemap, newTile, newPos);

        foreach (var posInBounds in tilemap.cellBounds.allPositionsWithin)
        {
            var otherTile = tilemap.GetTile<Tile>(posInBounds);
            if (otherTile == null)
                continue;

            if (!IsCollectable(otherTile))
                continue;

            var otherPos = tilemap.CellToWorld(posInBounds);
            float oldRoofLine = ComputeRoofLineY(tilemap, otherTile, otherPos);

            if (Mathf.Abs(newRoofLine - oldRoofLine) > 0.001f)
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
    /// Вычисляем «линию крыши» (или «уровень крыши») так же,
    /// как это делает SnapToRoofRule.
    /// </summary>
    private float ComputeRoofLineY(Tilemap tilemap, Tile tile, Vector3 pivotPos)
    {
        // Если мы предполагаем, что SnapToRoofRule просто ставит объект на pivotPos (top + 0.1f)
        // То можно считать "roofLine" = pivotPos.y (уже готовая координата)
        // Или же, если нужно пересчитывать, можно заново определить спрайтовую высоту,
        // как в SnapToRoofRule, но важнее, чтобы формула совпадала с тем, что делали там.

        // Здесь покажем пример, если мы хотим заново брать "верх объекта" + offset:
        // Но только если этот объект - collectable, установленный поверх BigNotAlive.
        // Если у нас нет простой функции, мы можем повторить логику SnapToRoofRule:

        // (1) Получаем сам спрайт
        // (2) Берём bounds, или rect.height / pivot
        // (3) Рассчитываем top = pivotPos.y + halfHeight
        // (4) Возвращаем top + offset. 
        // ИЛИ, если SnapToRoofRule уже переместил pivotPos.y,
        // то pivotPos.y сам по себе и есть нужная "линия".

        // Предположим, что SnapToRoofRule итогово ставит pivot на (top + 0.1f).
        // Тогда у collectable pivotPos.y = "линия крыши" прямо сейчас.

        // Поэтому можно просто вернуть pivotPos.y, 
        // и это будет "штамп" их положения по вертикали.
        return pivotPos.y;
    }

    /// <summary>
    /// Проверяем, является ли данный тайл коллектиблом (по сути, фильтр по типу).
    /// </summary>
    private bool IsCollectable(Tile tile)
    {
        if (tile?.sprite == null)
            return false;

        if (!ObstacleSpriteTypeMappingsManager.TryGetType(tile.sprite.name, out var foundType))
            return false;

        var typeString = foundType.ToString().ToLowerInvariant();
        return typeString.Contains("collectable");
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
    /// Привязка позиции к шагу GridSnapStep (0.2), округляя X/Y.
    /// </summary>
    private static void SnapPositionToGrid(ref Vector3 pos, float step)
    {
        pos.x = Mathf.Round(pos.x / step) * step;
        pos.y = Mathf.Round(pos.y / step) * step;
    }
}
