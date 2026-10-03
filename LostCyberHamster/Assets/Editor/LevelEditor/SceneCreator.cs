using System;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System.LevelManagement;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class SceneCreator
{
    /// <summary>
    /// Создаёт tilemap и четыре фона из готового префаба выбранной локации и времени суток.
    /// </summary>
    public static GameObject CreateSceneWithTilemap(int targetWidth, string locationName, string daypartSlug)
    {
        // Проверяем окружение до замены текущего редактируемого уровня.
        var environmentPrefab = LoadEnvironmentPrefab(locationName, daypartSlug);
        environmentPrefab.GetComponent<LocationEnvironment>().ValidateConfiguration();
        var scene = SceneManager.GetActiveScene();
        CleanupOldSceneObjects(scene);

        // Подготавливаем сетку размещения объектов уровня.
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
        tilemapRenderer.sortOrder = TilemapRenderer.SortOrder.TopLeft;
        tilemapRenderer.sortingLayerName = "SpecialEffects";
        tilemap.tileAnchor = Vector3.zero;

        // Сохраняем авторскую композицию и повторяем каждый слой на ширину уровня.
        var environmentObject = (GameObject)PrefabUtility.InstantiatePrefab(environmentPrefab, scene);
        environmentObject.name = "LevelEnvironment";
        environmentObject.transform.position = new Vector3(0f, LocationEnvironment.RoadBottomWorldY, 0f);
        environmentObject.GetComponent<LocationEnvironment>().PopulateStatic(0f, Mathf.Max(1, targetWidth));
        return tilemapGameObject;
    }

    /// <summary>
    /// Удаляет сетку уровня и ранее созданные фоны из текущей сцены редактора.
    /// </summary>
    public static void CleanupOldSceneObjects(Scene scene)
    {
        var rootObjects = scene.GetRootGameObjects();
        foreach (var obj in rootObjects)
        {
            if (obj.name == "Grid" || obj.GetComponent<LocationEnvironment>() != null ||
                obj.name.StartsWith("BackgroundSegment_", StringComparison.Ordinal) ||
                obj.name.StartsWith("RoadSegment_", StringComparison.Ordinal))
            {
                UnityEngine.Object.DestroyImmediate(obj);
            }
        }
    }

    /// <summary>
    /// Находит подготовленный префаб по его точному адресу в настройках Addressables.
    /// </summary>
    private static GameObject LoadEnvironmentPrefab(string locationName, string daypartSlug)
    {
        // В редакторе используем сами assets, чтобы видеть изменения до сборки каталога.
        var address = EnvironmentKeyResolver.BuildEnvironmentKey(locationName, daypartSlug);
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        GameObject prefab = null;
        if (settings != null)
        {
            foreach (var group in settings.groups)
            {
                if (group == null)
                    continue;
                foreach (var entry in group.entries)
                {
                    if (!string.Equals(entry.address, address, StringComparison.Ordinal))
                        continue;
                    if (prefab != null)
                        throw new InvalidOperationException($"Адрес окружения зарегистрирован несколько раз: {address}");
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.AssetPath);
                }
            }
        }

        // Отсутствующий набор требует подготовки через инструмент фонов.
        if (prefab == null || prefab.GetComponent<LocationEnvironment>() == null)
            throw new InvalidOperationException(
                $"Окружение не подготовлено: локация '{locationName}', время '{daypartSlug}', адрес '{address}'. " +
                "Сохраните четыре фона через Tools/Backgrounds/Procreate.");
        return prefab;
    }
}
