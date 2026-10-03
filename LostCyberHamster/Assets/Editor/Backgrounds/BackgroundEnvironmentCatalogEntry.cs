using System.Collections.Generic;
using UnityEngine;

namespace LostCyberHamster.Editor.Backgrounds
{
    /// <summary>Результат проверки сохранённого окружения для локации и времени суток.</summary>
    public sealed class BackgroundEnvironmentCatalogEntry
    {
        /// <summary>Сохраняет ссылки, пути и причину ошибки для редакторского каталога.</summary>
        internal BackgroundEnvironmentCatalogEntry(string locationId, string daypart, string prefabPath,
            string spriteDirectory, string address, GameObject prefab, Sprite[] sprites, string[] spritePaths, string error)
        {
            LocationId = locationId;
            Daypart = daypart;
            PrefabPath = prefabPath;
            SpriteDirectory = spriteDirectory;
            Address = address;
            Prefab = prefab;
            Sprites = sprites;
            SpritePaths = spritePaths;
            Error = error;
        }

        public string LocationId { get; }
        public string Daypart { get; }
        public string PrefabPath { get; }
        public string SpriteDirectory { get; }
        public string Address { get; }
        public GameObject Prefab { get; }
        public IReadOnlyList<Sprite> Sprites { get; }
        public IReadOnlyList<string> SpritePaths { get; }
        public string Error { get; }
        public bool IsReady => Prefab != null && string.IsNullOrEmpty(Error);
    }
}
