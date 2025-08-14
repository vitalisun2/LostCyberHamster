using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public static class TextureLoader
{
    private const string TexturePath = "Assets/Editor/LevelEditor/TileTextures"; // Путь для текстур

    public static Texture2D[] LoadTextures()
    {
        if (!Directory.Exists(TexturePath))
        {
            Debug.LogWarning($"Папка {TexturePath} не найдена.");
            return Array.Empty<Texture2D>();
        }

        var textures = new List<Texture2D>();
        var textureFiles = Directory.GetFiles(TexturePath, "*.png");

        foreach (var file in textureFiles)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(file);
            if (texture != null)
            {
                textures.Add(texture);
            }
            else
            {
                Debug.LogWarning($"Не удалось загрузить текстуру: {file}");
            }
        }

        return textures.ToArray();
    }
}

