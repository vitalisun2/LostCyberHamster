using System;
using System.Collections.Generic;

namespace Vues.GameCore
{
    /// <summary>
    /// Сериализуемый корневой список суперударов.
    /// </summary>
    [Serializable]
    public sealed class SuperAttackDataList
    {
        /// <summary>
        /// Сериализуемые данные суперударов.
        /// </summary>
        public List<SuperAttackData> SuperAttacks;
    }
}
