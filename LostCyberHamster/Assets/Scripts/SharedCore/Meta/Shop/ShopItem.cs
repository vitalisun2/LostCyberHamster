using System.Collections;
using UnityEngine;

namespace Vues.GameCore
{
    [System.Serializable]
    public class ShopItem
    {
        public int id;
        public string name;

        /// <summary>
        /// Цена покупки
        /// </summary>
        public int price;
        /// <summary>
        /// Количество получаемого ресурса
        /// </summary>
        public int amount;

        /// <summary>
        /// Тип получаемого ресурса
        /// </summary>
        public ResourceType type;

        /// <summary>
        /// Тип платы за покупку
        /// </summary>
        public ResourceType resource;
        public string imageAddress;  // Addressable image path
    }
}
