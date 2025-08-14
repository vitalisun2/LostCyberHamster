using System;

namespace Assets.Scripts.Common.Models
{
    /// <summary>
    /// Каталог
    /// </summary>
    [Serializable]
    public struct Catalog
    {
        public CatalogBundle[] bundles;
        public LocationInfo[] locations;
    }
}
