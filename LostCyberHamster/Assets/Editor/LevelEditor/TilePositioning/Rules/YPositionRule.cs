using Assets.Scripts;
using UnityEngine;
using UnityEngine.Tilemaps;

public class YPositionRule : ITilePlacementRule
{
    public bool TryApplyRule(Tilemap tilemap, Tile tile, ref Vector3 position)
    {
        Debug.Log($"[YPositionRule] Checking tile '{tile.sprite.name}' with initial worldPos={position}");

        // Определяем, какая из двух константной позиций Y ближе
        float distanceToY0 = Mathf.Abs(position.y - Consts.ObstacleY0Pos);
        float distanceToY1 = Mathf.Abs(position.y - Consts.ObstacleY1Pos);

        bool closerToY0 = distanceToY0 < distanceToY1;
        position.y = closerToY0 ? Consts.ObstacleY0Pos : Consts.ObstacleY1Pos;

        // Чтобы сместить Z-координату через TransformMatrix, 
        // нужно перейти из worldPos в клеточные координаты
        var cellPos = tilemap.WorldToCell(position);

        // Устанавливаем ZOffset в зависимости от выбранной линии
        SetTileZOffset(tilemap, tile, cellPos, !closerToY0);
        return true;
    }

    private void SetTileZOffset(Tilemap tilemap, Tile tile, Vector3Int cellPos, bool isLower)
    {
        float zOffset = isLower ? -0.1f : 0f;
        var matrix = tilemap.GetTransformMatrix(cellPos);
        matrix *= Matrix4x4.Translate(new Vector3(0f, 0f, zOffset));
        tilemap.SetTransformMatrix(cellPos, matrix);
    }
}
