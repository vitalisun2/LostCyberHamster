using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using System.IO;

public class TilemapToJsonEditor : EditorWindow
{
    [MenuItem("Tools/Export Tilemap to JSON")]
    public static void ShowWindow()
    {
        GetWindow<TilemapToJsonEditor>("Export Tilemap");
    }

    public Tilemap tilemap;

    private void OnGUI()
    {
        GUILayout.Label("Export Tilemap to JSON", EditorStyles.boldLabel);
        tilemap = (Tilemap)EditorGUILayout.ObjectField("Tilemap:", tilemap, typeof(Tilemap), true);

        if (GUILayout.Button("Export"))
        {
            ExportToJson();
        }
    }

    public void ExportToJson()
    {
        if (tilemap == null)
        {
            Debug.LogError("Tilemap is not assigned");
            return;
        }

        List<TileData> tiles = new List<TileData>();
        BoundsInt bounds = tilemap.cellBounds;
        TileBase[] allTiles = tilemap.GetTilesBlock(bounds);

        for (int x = 0; x < bounds.size.x; x++)
        {
            for (int y = 0; y < bounds.size.y; y++)
            {
                TileBase tile = allTiles[x + y * bounds.size.x];
                if (tile != null)
                {
                    Vector3Int worldPosition = new Vector3Int(x + bounds.x, y + bounds.y, 0);
                    TileData tileData = new TileData { position = worldPosition, tileName = tile.name };
                    tiles.Add(tileData);
                }
            }
        }

        Wrapper wrapper = new Wrapper();
        wrapper.tiles = tiles;

        string json = JsonUtility.ToJson(wrapper);

        // Open Save File Dialog
        string filePath = EditorUtility.SaveFilePanel(
            "Save Tilemap as JSON",
            "",
            "TilemapData.json",
            "json"
        );

        // Check if a path was selected
        if (!string.IsNullOrEmpty(filePath))
        {
            File.WriteAllText(filePath, json);
        }
    }

    [System.Serializable]
    public class TileData
    {
        public Vector3Int position;
        public string tileName;
    }

    [System.Serializable]
    public class Wrapper
    {
        public List<TileData> tiles;
    }
}





