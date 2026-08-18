using System;

namespace Vues.GameCore
{
    /// <summary>
    /// Сериализуемые данные одного суперудара.
    /// </summary>
    [Serializable]
    public sealed class SuperAttackData
    {
        /// <summary>
        /// Уникальный идентификатор суперудара.
        /// </summary>
        public int Id;

        /// <summary>
        /// Ключ локализации названия.
        /// </summary>
        public string NameLocalizationKey;

        /// <summary>
        /// Ключ локализации описания.
        /// </summary>
        public string DescriptionLocalizationKey;

        /// <summary>
        /// Адрес ресурса иконки.
        /// </summary>
        public string IconAddress;

        /// <summary>
        /// Адрес prefab эффекта для суперудара с отдельным visual effect.
        /// </summary>
        public string UltaPrefab;

        /// <summary>
        /// Длительность суперудара в секундах.
        /// </summary>
        public float UltaDuration;

        /// <summary>
        /// Заряд за одно уничтоженное препятствие.
        /// </summary>
        public int UltaCharge;

    }
}
