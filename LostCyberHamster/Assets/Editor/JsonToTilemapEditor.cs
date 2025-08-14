using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using System.IO;

public class JsonToTilemapEditor : EditorWindow
{
    [MenuItem("Tools/Import Tilemap from JSON")]
    public static void ShowWindow()
    {
        GetWindow<JsonToTilemapEditor>("Import Tilemap");
    }

    public Tilemap tilemap;
    public List<Tile> tilePrefabs = new List<Tile>();
    public string jsonFileName = "TilemapData.json";

    private void OnGUI()
    {
        GUILayout.Label("Import Tilemap from JSON", EditorStyles.boldLabel);

        tilemap = (Tilemap)EditorGUILayout.ObjectField("Tilemap:", tilemap, typeof(Tilemap), true);

        EditorGUILayout.LabelField("Tile Prefabs:");
        for (int i = 0; i < tilePrefabs.Count; i++)
        {
            tilePrefabs[i] = (Tile)EditorGUILayout.ObjectField(tilePrefabs[i], typeof(Tile), false);
        }

        if (GUILayout.Button("Add Tile"))
        {
            tilePrefabs.Add(null);
        }

        if (GUILayout.Button("Remove Tile"))
        {
            if (tilePrefabs.Count > 0)
            {
                tilePrefabs.RemoveAt(tilePrefabs.Count - 1);
            }
        }

        jsonFileName = EditorGUILayout.TextField("JSON File Name:", jsonFileName);

        if (GUILayout.Button("Import"))
        {
            ImportFromJson();
        }
    }

    public void ImportFromJson()
    {
        if (tilemap == null || tilePrefabs == null)
        {
            Debug.LogError("Tilemap or Tile Prefabs are not assigned");
            return;
        }

        string path = Application.dataPath + "/" + jsonFileName;
        if (!File.Exists(path))
        {
            Debug.LogError("File does not exist: " + path);
            return;
        }

        string json = File.ReadAllText(path);

        if (string.IsNullOrEmpty(json))
        {
            Debug.LogError("JSON data is empty");
            return;
        }

        Wrapper wrapper = JsonUtility.FromJson<Wrapper>(json);

        if (wrapper == null || wrapper.tiles == null)
        {
            Debug.LogError("Failed to deserialize JSON");
            return;
        }

        foreach (TileData tileData in wrapper.tiles)
        {
            Tile matchingTile = null;
            foreach (Tile tilePrefab in tilePrefabs)
            {
                if (tilePrefab.name == tileData.tileName)
                {
                    matchingTile = tilePrefab;
                    break;
                }
            }

            if (matchingTile == null)
            {
                Debug.LogError("No matching tile found for: " + tileData.tileName);
                continue;
            }

            // Convert the world position to local position within the tilemap
            Vector3Int localPosition = tilemap.WorldToCell(tileData.position);

            tilemap.SetTile(localPosition, matchingTile);
        }
    }

    [System.Serializable]
    public class TileData
    {
        public Vector3 position; // Store the world position
        public string tileName;
    }

    [System.Serializable]
    public class Wrapper
    {
        public List<TileData> tiles;
    }
}







