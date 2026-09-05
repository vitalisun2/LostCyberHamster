using System;

namespace GameManagement
{
    /// <summary>Хранит технический журнал функции для конкретного владельца.</summary>
    [Serializable]
    public sealed class LocalFeatureJournal
    {
        public string Feature;
        public string Owner;
        public string Json;
    }
}
