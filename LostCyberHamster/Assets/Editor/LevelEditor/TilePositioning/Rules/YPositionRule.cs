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

        return true;
    }
}
