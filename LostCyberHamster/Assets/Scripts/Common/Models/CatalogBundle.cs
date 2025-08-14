using System;

namespace Assets.Scripts.Common.Models
{
    /// <summary>
    /// Описание бандла в катологе
    /// </summary>
    [Serializable]
    public class CatalogBundle
    {
        public string name;
        public uint crc;
        public string version;
        public string url;
        public string hash;
        public string[] assets;
    }
}
