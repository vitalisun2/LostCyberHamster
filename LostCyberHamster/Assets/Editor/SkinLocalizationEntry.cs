#if UNITY_EDITOR
using System;

namespace LostCyberHamster.Editor
{
    /// <summary>
    /// Представляет одну строку localization JSON.
    /// </summary>
    [Serializable]
    internal sealed class SkinLocalizationEntry
    {
        public string key;
        public string value;
    }
}
#endif
