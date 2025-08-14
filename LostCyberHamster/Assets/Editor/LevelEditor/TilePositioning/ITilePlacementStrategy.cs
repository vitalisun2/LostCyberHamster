using UnityEngine.Tilemaps;
using UnityEngine;

public interface ITilePlacementStrategy
{
    bool TryPlaceTile(Tilemap tilemap, Tile tile, Vector3 initialPos, out Vector3 finalPos);
}
