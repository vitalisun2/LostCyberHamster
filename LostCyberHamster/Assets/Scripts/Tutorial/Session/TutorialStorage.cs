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
        internal const string FirstLevelBypassKey = "Tutorial_FirstLevel_BypassOnce";
        internal const string ForcedReplayKey = "Tutorial_ForcedReplayOnce";
        internal const string ResetCompletedOnceKey = "Tutorial_ResetCompletedOnce";
        internal const string AutoPlayKey = "Tutorial_AutoPlay";
        internal const string StopAfterStepKey = "Tutorial_StopAfterStep";

        public static bool HasPlayerDataBackup => PlayerPrefs.HasKey(PlayerDataBackupKey);

        public static bool IsPlayerDataBackupActive => PlayerPrefs.HasKey(PlayerDataBackupActiveKey);

        public static bool HasFirstLevelBypass => PlayerPrefs.HasKey(FirstLevelBypassKey);

        public static bool HasForcedReplay => PlayerPrefs.HasKey(ForcedReplayKey);

        public static bool HasCompletedResetRequest => PlayerPrefs.HasKey(ResetCompletedOnceKey);

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

            if (HasPlayerDataBackup)
            {
                throw new InvalidOperationException("Tutorial player data backup already exists.");
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
        /// Удаляет backup и active marker после успешного restore либо rejected recovery.
        /// </summary>
        public static void ClearPlayerDataBackup()
        {
            PlayerPrefs.DeleteKey(PlayerDataBackupKey);
            PlayerPrefs.DeleteKey(PlayerDataBackupActiveKey);
            PlayerPrefs.Save();
        }

        public static void RequestFirstLevelBypass()
        {
            SetFlag(FirstLevelBypassKey);
        }

        public static bool ConsumeFirstLevelBypass()
        {
            return ConsumeFlag(FirstLevelBypassKey);
        }

        public static void ClearFirstLevelBypass()
        {
            ClearKey(FirstLevelBypassKey);
        }

        public static void RequestForcedReplay()
        {
            SetFlag(ForcedReplayKey);
        }

        public static bool ConsumeForcedReplay()
        {
            return ConsumeFlag(ForcedReplayKey);
        }

        public static void ClearForcedReplay()
        {
            ClearKey(ForcedReplayKey);
        }

        public static void RequestCompletedReset()
        {
            SetFlag(ResetCompletedOnceKey);
        }

        public static bool ConsumeCompletedReset()
        {
            return ConsumeFlag(ResetCompletedOnceKey);
        }

        public static void ClearCompletedReset()
        {
            ClearKey(ResetCompletedOnceKey);
        }

        public static bool IsAutoPlayEnabled()
        {
            return PlayerPrefs.GetInt(AutoPlayKey, 0) == 1;
        }

        public static void SetAutoPlay(bool enabled)
        {
            if (!enabled)
            {
                ClearAutoPlay();
                return;
            }

            SetFlag(AutoPlayKey);
        }

        public static void ClearAutoPlay()
        {
            ClearKey(AutoPlayKey);
        }

        public static bool TryGetStopAfterStep(out int step)
        {
            step = PlayerPrefs.GetInt(StopAfterStepKey, 0);
            return step > 0;
        }

        public static void SetStopAfterStep(int step)
        {
            if (step <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(step), step, "Tutorial stop step must be positive.");
            }

            PlayerPrefs.SetInt(StopAfterStepKey, step);
            PlayerPrefs.Save();
        }

        public static void ClearStopAfterStep()
        {
            ClearKey(StopAfterStepKey);
        }

        private static void SetFlag(string key)
        {
            PlayerPrefs.SetInt(key, 1);
            PlayerPrefs.Save();
        }

        private static bool ConsumeFlag(string key)
        {
            if (!PlayerPrefs.HasKey(key))
            {
                return false;
            }

            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            return true;
        }

        private static void ClearKey(string key)
        {
            if (!PlayerPrefs.HasKey(key))
            {
                return;
            }

            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }
}
