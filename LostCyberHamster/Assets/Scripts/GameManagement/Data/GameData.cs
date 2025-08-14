using System;
using System.Collections.Generic;
using Assets.Scripts.Common.Models;
using GameManagement;
using UnityEngine;
using Vues.GameCore;

namespace Vues.GameCore
{
    public partial class GameData
    {
        /// <summary>
        /// Данные игрока.
        /// </summary>
        public PlayerData PlayerData = new();

        public SettingsData SettingsData = new();
    }
}
