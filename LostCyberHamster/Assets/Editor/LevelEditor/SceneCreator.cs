using Assets.Scripts;
using Assets.Scripts.Common.Models;
using UnityEditor.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine;
using System;
using UnityEngine.SceneManagement;

public static class SceneCreator
{
    private const float PixelsPerUnit = 100.0f;
    private const float BackgroundZPosition = 1.0f;
    private const string BackgroundSortingLayer = "Background";
    private static readonly Vector2 SpritePivot = new Vector2(0.5f, 0.5f);

    public static GameObject CreateSceneWithTilemap(int targetWidth, LevelInfo currentLevelInfo)
    {
        // Work in the currently active scene
        var scene = SceneManager.GetActiveScene();

        // Очистка старых объектов Grid и фона при смене локации/файла
        CleanupOldSceneObjects(scene);

        var gridGameObject = new GameObject("Grid");
        SceneManager.MoveGameObjectToScene(gridGameObject, scene);
        var grid = gridGameObject.AddComponent<Grid>();
        grid.cellSize = new Vector3(0.2f, 0.2f, 1f);

        var tilemapGameObject = new GameObject("Tilemap", typeof(Tilemap), typeof(TilemapRenderer));
        SceneManager.MoveGameObjectToScene(tilemapGameObject, scene);
        tilemapGameObject.transform.SetParent(gridGameObject.transform);

        var tilemap = tilemapGameObject.GetComponent<Tilemap>();
        var tilemapRenderer = tilemapGameObject.GetComponent<TilemapRenderer>();
        tilemapRenderer.mode = TilemapRenderer.Mode.Individual;
        tilemapRenderer.sortingLayerName = "SpecialEffects";
        tilemap.tileAnchor = Vector3.zero;

        CreateBackground(targetWidth, currentLevelInfo, scene);
        return tilemapGameObject;
    }

    /// <summary>
    /// Удаляет все старые объекты Grid, Tilemap и BackgroundSegment из сцены.
    /// </summary>
    private static void CleanupOldSceneObjects(Scene scene)
    {
        var rootObjects = scene.GetRootGameObjects();
        int removedCount = 0;
        
        foreach (var obj in rootObjects)
        {
            // Удаляем старые Grid (содержат Tilemap как дочерний)
            if (obj.name == "Grid")
            {
                UnityEngine.Object.DestroyImmediate(obj);
                removedCount++;
            }
            // Удаляем старые сегменты фона
            else if (obj.name.StartsWith("BackgroundSegment_"))
            {
                UnityEngine.Object.DestroyImmediate(obj);
                removedCount++;
            }
        }
        
        if (removedCount > 0)
        {
            Debug.Log($"[SceneCreator] Removed {removedCount} old scene objects (Grid/Background)");
        }
    }

    private static void CreateBackground(int targetWidth, LevelInfo currentLevelInfo, Scene scene)
    {
        var backgroundTextureName = currentLevelInfo.backgroundTexture;
        if (string.IsNullOrEmpty(backgroundTextureName))
        {
            Debug.LogWarning("Background texture is empty.");
            return;
        }

        // СИНХРОННАЯ загрузка
        var sprite = SpriteLoader.LoadSpriteSync(backgroundTextureName);
        if (sprite == null)
        {
            Debug.LogError($"Failed to load background sprite: {backgroundTextureName}");
            return;
        }

        var backgroundSprite = Sprite.Create(
            sprite.texture,
            new Rect(0, 0, sprite.texture.width, sprite.texture.height),
            SpritePivot
        );

        float textureWidthInUnits = backgroundSprite.texture.width / PixelsPerUnit;
        int numberOfCopies = Mathf.CeilToInt(targetWidth / textureWidthInUnits);

        for (int i = 0; i < numberOfCopies; i++)
        {
            CreateBackgroundSegment(backgroundSprite, i * textureWidthInUnits, scene);
        }
    }


    private static void CreateBackgroundSegment(Sprite sprite, float xPosition, Scene scene)
    {
        var segment = new GameObject($"BackgroundSegment_{xPosition}");
        SceneManager.MoveGameObjectToScene(segment, scene);
        var renderer = segment.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        segment.transform.position = new Vector3(xPosition, Consts.BackgroundYPos, BackgroundZPosition);
        renderer.sortingLayerName = BackgroundSortingLayer;
    }
}
