using Assets.Scripts;
using Assets.Scripts.Common.Models;
using UnityEditor;
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

/// <summary>
/// Выводит roof-to-roof gaps из визуально спорного диапазона между tight continuation и явным разрывом.
/// </summary>
public class RoofPlatformGapSnapRule : ITilePlacementRule
{
    private const float SameLineTolerance = Consts.GridSnapStep * 0.5f + 0.001f;
    private const float GapTolerance = 0.001f;
    private const float FallbackHamsterColliderWidth = 1.64f;
    private const string HamsterPrefabPath = "Assets/Content/prefabs/Hamster.prefab";

    private static float? _cachedHamsterWidth;

    public bool TryApplyRule(Tilemap tilemap, Tile tile, ref Vector3 position)
    {
        if (!IsRoofPlatform(tile))
        {
            return true;
        }

        float hamsterWidth = GetHamsterColliderWidth();
        if (TryFindBestSnap(tilemap, tile, position, hamsterWidth, out var snap))
        {
            position.x = snap.PositionX;
        }

        return true;
    }

    /// <summary>
    /// Ищет ближайший спорный gap на той же линии и возвращает минимальный корректирующий сдвиг.
    /// </summary>
    private static bool TryFindBestSnap(
        Tilemap tilemap,
        Tile newTile,
        Vector3 newPosition,
        float hamsterWidth,
        out RoofGapSnapCandidate bestSnap)
    {
        bestSnap = default;
        bool hasBestSnap = false;
        var newBounds = GetHorizontalBounds(newTile, newPosition);

        foreach (var cellPosition in tilemap.cellBounds.allPositionsWithin)
        {
            var otherTile = tilemap.GetTile<Tile>(cellPosition);
            if (!IsRoofPlatform(otherTile))
            {
                continue;
            }

            var otherPosition = TilemapPositionUtility.GetExactTileWorldPosition(tilemap, cellPosition);
            if (IsSameTilePosition(newTile, newPosition, otherTile, otherPosition)
                || !IsSameLine(newPosition.y, otherPosition.y))
            {
                continue;
            }

            var otherBounds = GetHorizontalBounds(otherTile, otherPosition);
            if (!TryCreateSnapCandidate(
                    newPosition.x,
                    newBounds,
                    otherBounds,
                    hamsterWidth,
                    out var candidate))
            {
                continue;
            }

            if (!hasBestSnap || candidate.MoveDistance < bestSnap.MoveDistance)
            {
                bestSnap = candidate;
                hasBestSnap = true;
            }
        }

        return hasBestSnap;
    }

    /// <summary>
    /// Создаёт candidate-сдвиг, если текущий gap попадает между tight и passive roof thresholds.
    /// </summary>
    private static bool TryCreateSnapCandidate(
        float newPositionX,
        HorizontalBounds newBounds,
        HorizontalBounds otherBounds,
        float hamsterWidth,
        out RoofGapSnapCandidate candidate)
    {
        candidate = default;

        if (!TryGetGap(newBounds, otherBounds, out bool otherIsLeft, out float currentGap)
            || !RoofGapSnapMath.TryGetTargetGap(
                currentGap,
                hamsterWidth,
                out float targetGap,
                out RoofGapSnapTarget target))
        {
            return false;
        }

        float newLocalLeft = newBounds.Left - newPositionX;
        float newLocalRight = newBounds.Right - newPositionX;
        float desiredPositionX = otherIsLeft
            ? otherBounds.Right + targetGap - newLocalLeft
            : otherBounds.Left - targetGap - newLocalRight;

        if (!TrySnapPositionForTargetGap(
                desiredPositionX,
                newLocalLeft,
                newLocalRight,
                otherBounds,
                otherIsLeft,
                hamsterWidth,
                target,
                out float snappedPositionX))
        {
            return false;
        }

        candidate = new RoofGapSnapCandidate(
            snappedPositionX,
            Mathf.Abs(snappedPositionX - newPositionX));
        return true;
    }

    /// <summary>
    /// Привязывает X к сетке и корректирует на один шаг, если grid rounding оставил gap на неверной стороне threshold.
    /// </summary>
    private static bool TrySnapPositionForTargetGap(
        float desiredPositionX,
        float newLocalLeft,
        float newLocalRight,
        HorizontalBounds otherBounds,
        bool otherIsLeft,
        float hamsterWidth,
        RoofGapSnapTarget target,
        out float snappedPositionX)
    {
        snappedPositionX = SnapX(desiredPositionX);
        float tightGap = RoofGapSnapMath.GetTightGap(hamsterWidth);
        float passiveGap = Consts.GetRoofRunPassiveContinuationGap(hamsterWidth);

        for (int attempt = 0; attempt < 8; attempt++)
        {
            float actualGap = GetGapAtPosition(
                snappedPositionX,
                newLocalLeft,
                newLocalRight,
                otherBounds,
                otherIsLeft);

            if (target == RoofGapSnapTarget.Tight)
            {
                if (actualGap >= Consts.GapBetweenTiles - GapTolerance
                    && actualGap <= tightGap + GapTolerance)
                {
                    return true;
                }

                snappedPositionX += actualGap > tightGap
                    ? GetStepTowardOther(otherIsLeft)
                    : GetStepAwayFromOther(otherIsLeft);
                continue;
            }

            if (actualGap > passiveGap + GapTolerance)
            {
                return true;
            }

            snappedPositionX += GetStepAwayFromOther(otherIsLeft);
        }

        return false;
    }

    private static bool TryGetGap(
        HorizontalBounds newBounds,
        HorizontalBounds otherBounds,
        out bool otherIsLeft,
        out float gap)
    {
        otherIsLeft = false;
        gap = 0f;

        if (otherBounds.Right <= newBounds.Left)
        {
            otherIsLeft = true;
            gap = newBounds.Left - otherBounds.Right;
            return true;
        }

        if (newBounds.Right <= otherBounds.Left)
        {
            gap = otherBounds.Left - newBounds.Right;
            return true;
        }

        return false;
    }

    private static float GetGapAtPosition(
        float newPositionX,
        float newLocalLeft,
        float newLocalRight,
        HorizontalBounds otherBounds,
        bool otherIsLeft)
    {
        return otherIsLeft
            ? newPositionX + newLocalLeft - otherBounds.Right
            : otherBounds.Left - (newPositionX + newLocalRight);
    }

    private static float GetStepTowardOther(bool otherIsLeft)
    {
        return otherIsLeft ? -Consts.GridSnapStep : Consts.GridSnapStep;
    }

    private static float GetStepAwayFromOther(bool otherIsLeft)
    {
        return otherIsLeft ? Consts.GridSnapStep : -Consts.GridSnapStep;
    }

    private static bool IsRoofPlatform(Tile tile)
    {
        if (tile?.sprite == null)
        {
            return false;
        }

        if (!ObstacleSpriteTypeMappingsManager.TryGetType(tile.sprite.name, out var foundType))
        {
            return false;
        }

        return foundType == ObstacleTypeEnum.bigNotAlive
            || foundType == ObstacleTypeEnum.mediumNotAlive;
    }

    private static bool IsSameLine(float leftY, float rightY)
    {
        return Mathf.Abs(leftY - rightY) <= SameLineTolerance;
    }

    private static bool IsSameTilePosition(Tile newTile, Vector3 newPosition, Tile otherTile, Vector3 otherPosition)
    {
        return ReferenceEquals(newTile, otherTile)
            && Mathf.Abs(newPosition.x - otherPosition.x) <= GapTolerance
            && Mathf.Abs(newPosition.y - otherPosition.y) <= GapTolerance;
    }

    private static HorizontalBounds GetHorizontalBounds(Tile tile, Vector3 position)
    {
        Bounds spriteBounds = tile.sprite.bounds;
        return new HorizontalBounds(
            position.x + spriteBounds.center.x - spriteBounds.extents.x,
            position.x + spriteBounds.center.x + spriteBounds.extents.x);
    }

    private static float SnapX(float positionX)
    {
        return Mathf.Round(positionX / Consts.GridSnapStep) * Consts.GridSnapStep;
    }

    private static float GetHamsterColliderWidth()
    {
        if (_cachedHamsterWidth.HasValue)
        {
            return _cachedHamsterWidth.Value;
        }

        var hamsterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HamsterPrefabPath);
        var hamsterCollider = hamsterPrefab != null
            ? hamsterPrefab.GetComponentInChildren<BoxCollider2D>()
            : null;

        _cachedHamsterWidth = hamsterCollider != null && hamsterCollider.size.x > 0f
            ? hamsterCollider.size.x
            : FallbackHamsterColliderWidth;

        return _cachedHamsterWidth.Value;
    }

    private readonly struct HorizontalBounds
    {
        public HorizontalBounds(float left, float right)
        {
            Left = left;
            Right = right;
        }

        public float Left { get; }

        public float Right { get; }
    }

    private readonly struct RoofGapSnapCandidate
    {
        public RoofGapSnapCandidate(float positionX, float moveDistance)
        {
            PositionX = positionX;
            MoveDistance = moveDistance;
        }

        public float PositionX { get; }

        public float MoveDistance { get; }
    }
}

internal enum RoofGapSnapTarget
{
    Tight,
    Wide
}

internal static class RoofGapSnapMath
{
    private const float TightRoofGapFactor = 0.3f;

    public static bool TryGetTargetGap(
        float currentGap,
        float hamsterWidth,
        out float targetGap,
        out RoofGapSnapTarget target)
    {
        targetGap = 0f;
        target = RoofGapSnapTarget.Tight;

        float tightGap = GetTightGap(hamsterWidth);
        float passiveGap = Consts.GetRoofRunPassiveContinuationGap(hamsterWidth);
        if (currentGap <= tightGap || currentGap >= passiveGap)
        {
            return false;
        }

        float midpointGap = (tightGap + passiveGap) * 0.5f;
        if (currentGap <= midpointGap)
        {
            targetGap = tightGap;
            target = RoofGapSnapTarget.Tight;
            return true;
        }

        targetGap = passiveGap + Consts.GridSnapStep;
        target = RoofGapSnapTarget.Wide;
        return true;
    }

    public static float GetTightGap(float hamsterWidth)
    {
        return hamsterWidth * TightRoofGapFactor;
    }
}
