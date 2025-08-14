using Assets.Scripts;
using UnityEngine;
using UnityEngine.Tilemaps;

public class DecorPlacementRule : ITilePlacementRule
{
    public bool TryApplyRule(Tilemap tilemap, Tile tile, ref Vector3 position)
    {
        // Проверяем, что декор располагаем только выше RoadUpperEdgeYPos
        if (position.y < Consts.RoadUpperEdgeYPos)
            return false;

        // Случайный сдвиг по Z: чем выше Y, тем ниже будет рисоваться (и добавляем рандом)
        var zOffset = position.y + Random.Range(0f, 1f);

        // Получаем координаты ячейки, чтобы обновить TransformMatrix
        var cellPos = tilemap.WorldToCell(position);

        var matrix = tilemap.GetTransformMatrix(cellPos);
        matrix *= Matrix4x4.Translate(new Vector3(0f, 0f, zOffset));
        tilemap.SetTransformMatrix(cellPos, matrix);

        return true;
    }
}

