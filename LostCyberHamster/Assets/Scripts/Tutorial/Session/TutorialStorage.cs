using System;
using UnityEngine;

namespace Assets.Scripts.Tutorial
{
    /// <summary>
    /// Хранит persistent-состояние tutorial и владеет всеми его PlayerPrefs-ключами.
    /// </summary>
    public static class TutorialStorage
    {
        internal const string PlayerDataBackupKey = "Tutorial.PlayerDataBackup";
        internal const string PlayerDataBackupActiveKey = "Tutorial.PlayerDataBackupActive";

        public static bool HasPlayerDataBackup => PlayerPrefs.HasKey(PlayerDataBackupKey);

        public static bool IsPlayerDataBackupActive => PlayerPrefs.HasKey(PlayerDataBackupActiveKey);

        /// <summary>
        /// Возвращает сохранённый snapshot данных игрока без изменения persistent-состояния.
        /// </summary>
        public static bool TryGetPlayerDataBackup(out string playerDataJson)
        {
            if (!HasPlayerDataBackup)
            {
                playerDataJson = string.Empty;
                return false;
            }

            playerDataJson = PlayerPrefs.GetString(PlayerDataBackupKey, string.Empty);
            return !string.IsNullOrWhiteSpace(playerDataJson);
        }

        /// <summary>
        /// Создаёт новый backup и активный marker. Существующий backup никогда не перезаписывает.
        /// </summary>
        public static void CreatePlayerDataBackup(string playerDataJson)
        {
            if (string.IsNullOrWhiteSpace(playerDataJson))
            {
                throw new ArgumentException("Tutorial player data backup cannot be empty.", nameof(playerDataJson));
            }

            if (HasPlayerDataBackup || IsPlayerDataBackupActive)
            {
                throw new InvalidOperationException("Tutorial player data backup state already exists.");
            }

            PlayerPrefs.SetString(PlayerDataBackupKey, playerDataJson);
            PlayerPrefs.SetInt(PlayerDataBackupActiveKey, 1);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Обновляет существующий backup перед финальным restore.
        /// Это сохраняет completion intent, если приложение завершится между SaveData и очисткой backup.
        /// </summary>
        public static void UpdatePlayerDataBackup(string playerDataJson)
        {
            if (string.IsNullOrWhiteSpace(playerDataJson))
            {
                throw new ArgumentException("Tutorial player data backup cannot be empty.", nameof(playerDataJson));
            }

            if (!HasPlayerDataBackup)
            {
                throw new InvalidOperationException("Tutorial player data backup does not exist.");
            }

            PlayerPrefs.SetString(PlayerDataBackupKey, playerDataJson);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Помечает существующий backup активным после восстановления session в памяти.
        /// </summary>
        public static void MarkPlayerDataBackupActive()
        {
            if (!HasPlayerDataBackup)
            {
                throw new InvalidOperationException("Tutorial player data backup does not exist.");
            }

            PlayerPrefs.SetInt(PlayerDataBackupActiveKey, 1);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Удаляет backup и active marker только после успешного restore.
        /// </summary>
        public static void ClearPlayerDataBackup()
        {
            PlayerPrefs.DeleteKey(PlayerDataBackupKey);
            PlayerPrefs.DeleteKey(PlayerDataBackupActiveKey);
            PlayerPrefs.Save();
        }

    }
}
