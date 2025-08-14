using UnityEngine;
using UnityEngine.Tilemaps;

public interface ITilePlacementRule
{
    /// <summary>
    /// Попытаться применить правило к позиции тайла.
    /// Если правило успешно применено, позиция может быть изменена.
    /// Если правило примениться не может (или не удалось скорректировать позицию), вернуть false.
    /// </summary>
    bool TryApplyRule(Tilemap tilemap, Tile tile, ref Vector3 position);
}
