using Assets.Scripts.Common.Models;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Рисует зелёную рамку будущей позиции roof-platform тайла в Level Tilemap Editor.
/// </summary>
public sealed class RoofPlatformPlacementGhostRenderer
{
    private const float LineThickness = 3f;
    private static readonly Color OutlineColor = new Color(0.2f, 1f, 0.32f, 0.95f);
    private static readonly Color FillColor = new Color(0.2f, 1f, 0.32f, 0.12f);

    private Tile _activeTile;
    private ObstacleTypeEnum? _activeObstacleType;

    /// <summary>
    /// Запоминает активный tile кисти, если он может быть roof-platform.
    /// </summary>
    public void SetActiveTile(Tile tile, string spriteName)
    {
        _activeTile = tile;
        _activeObstacleType = ObstacleSpriteTypeMappingsManager.TryGetType(spriteName, out var obstacleType)
            ? obstacleType
            : null;
    }

    /// <summary>
    /// Сбрасывает активный tile кисти и скрывает ghost.
    /// </summary>
    public void ClearActiveTile()
    {
        _activeTile = null;
        _activeObstacleType = null;
    }

    /// <summary>
    /// Рисует ghost на позиции, которую вернёт placement pipeline для текущей мыши.
    /// </summary>
    public void Draw(SceneView sceneView, Tilemap tilemap, bool isObjectOnRoof)
    {
        if (tilemap == null || !TryGetActiveRoofPlatformTile(out var tile, out var obstacleType))
            return;

        var evt = Event.current;
        if (evt == null || evt.alt)
            return;

        sceneView.wantsMouseMove = true;
        if (evt.type == EventType.MouseMove)
        {
            sceneView.Repaint();
            return;
        }

        if (evt.type != EventType.Repaint)
            return;

        // Placement pipeline уже содержит Y snap, overlap avoidance и roof gap snap.
        if (TryGetGhostBounds(tilemap, tile, obstacleType, isObjectOnRoof, evt.mousePosition, out var bounds))
        {
            DrawBounds(bounds);
        }
    }

    private bool TryGetActiveRoofPlatformTile(out Tile tile, out ObstacleTypeEnum obstacleType)
    {
        tile = _activeTile;
        obstacleType = _activeObstacleType ?? default;
        return tile?.sprite != null
            && _activeObstacleType.HasValue
            && IsRoofPlatformType(obstacleType);
    }

    private static bool TryGetGhostBounds(
        Tilemap tilemap,
        Tile tile,
        ObstacleTypeEnum obstacleType,
        bool isObjectOnRoof,
        Vector2 mousePosition,
        out Bounds bounds)
    {
        var worldRay = HandleUtility.GUIPointToWorldRay(mousePosition);
        var mouseWorldPosition = worldRay.origin;
        mouseWorldPosition.z = 0f;

        var initialCellPosition = tilemap.WorldToCell(mouseWorldPosition);
        var initialWorldPosition = tilemap.CellToWorld(initialCellPosition);
        var strategy = TilePlacementStrategies.GetStrategyForType(obstacleType, isObjectOnRoof);
        if (!strategy.TryPlaceTile(tilemap, tile, initialWorldPosition, out var finalWorldPosition))
        {
            bounds = default;
            return false;
        }

        var spriteBounds = tile.sprite.bounds;
        bounds = new Bounds(finalWorldPosition + spriteBounds.center, spriteBounds.size);
        return true;
    }

    private static void DrawBounds(Bounds bounds)
    {
        var previousColor = Handles.color;
        var previousZTest = Handles.zTest;
        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

        var corners = new[]
        {
            new Vector3(bounds.min.x, bounds.min.y, 0f),
            new Vector3(bounds.min.x, bounds.max.y, 0f),
            new Vector3(bounds.max.x, bounds.max.y, 0f),
            new Vector3(bounds.max.x, bounds.min.y, 0f)
        };

        Handles.DrawSolidRectangleWithOutline(corners, FillColor, OutlineColor);
        Handles.color = OutlineColor;
        Handles.DrawAAPolyLine(LineThickness, corners[0], corners[1], corners[2], corners[3], corners[0]);

        Handles.zTest = previousZTest;
        Handles.color = previousColor;
    }

    private static bool IsRoofPlatformType(ObstacleTypeEnum obstacleType)
    {
        return obstacleType == ObstacleTypeEnum.bigNotAlive
            || obstacleType == ObstacleTypeEnum.mediumNotAlive;
    }
}
