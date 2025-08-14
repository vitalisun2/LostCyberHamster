using System;

namespace Assets.Scripts.Common.Models
{
    /// <summary>
    /// Информация о локациях
    /// </summary>
    [Serializable]
    public class LocationInfo
    {
        /// <summary>
        /// Название локации
        /// </summary>
        public string name;

        /// <summary>
        /// Системное название локации
        /// </summary>
        public string sysname;

        /// <summary>
        /// Изображение локации
        /// </summary>
        public string image;

        /// <summary>
        /// Список уровней
        /// </summary>
        public string[] levels; 
    }
}
