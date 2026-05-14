using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TilePlacementPipeline : ITilePlacementStrategy
{
    private readonly List<ITilePlacementRule> _rules = new();

    public void AddRule(ITilePlacementRule rule)
    {
        _rules.Add(rule);
    }

    public bool TryPlaceTile(Tilemap tilemap, Tile tile, Vector3 initialPos, out Vector3 finalPos)
    {
        finalPos = initialPos;

        foreach (var rule in _rules)
        {
            if (!rule.TryApplyRule(tilemap, tile, ref finalPos))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// Общие операции с позициями тайлов в Tilemap.
/// </summary>
public static class TilemapPositionUtility
{
    /// <summary>
    /// Возвращает фактическую world-позицию тайла с учётом transform matrix клетки.
    /// </summary>
    public static Vector3 GetExactTileWorldPosition(Tilemap tilemap, Vector3Int cellPos)
    {
        var baseWorldPos = tilemap.CellToWorld(cellPos);
        var matrix = tilemap.GetTransformMatrix(cellPos);
        var offset = matrix.GetColumn(3);
        return new Vector3(
            baseWorldPos.x + offset.x,
            baseWorldPos.y + offset.y,
            baseWorldPos.z + offset.z);
    }
}
