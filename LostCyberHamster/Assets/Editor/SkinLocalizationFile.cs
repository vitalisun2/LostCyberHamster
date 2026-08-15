#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace LostCyberHamster.Editor
{
    /// <summary>
    /// Представляет штатный localization JSON проекта.
    /// </summary>
    [Serializable]
    internal sealed class SkinLocalizationFile
    {
        public List<SkinLocalizationEntry> localizationStrings;
    }
}
#endif
