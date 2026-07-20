using System;
using System.Collections.Generic;
using GameManagement;
using Vues.GameCore;

namespace Assets.Scripts.Tutorial
{
    /// <summary>
    /// Изолирует учебное состояние игрока и восстанавливает исходный snapshot.
    /// </summary>
    public sealed class TutorialSession
    {
        private const int DefaultSkinId = 0;

        private PlayerData _snapshot;

        public bool IsActive => _snapshot != null;

        /// <summary>
        /// Сохраняет исходные данные игрока в памяти и persistent backup.
        /// Повторный вызов использует уже сохранённый snapshot.
        /// </summary>
        public void Begin()
        {
            if (_snapshot != null)
            {
                return;
            }

            EnsurePlayerDataAvailable();

            // Существующий backup имеет приоритет: новый запуск не должен затереть реальные данные игрока.
            if (TutorialStorage.HasPlayerDataBackup)
            {
                _snapshot = ReadPersistentSnapshot();
                if (!TutorialStorage.IsPlayerDataBackupActive)
                {
                    TutorialStorage.MarkPlayerDataBackupActive();
                }

                return;
            }

            // Новый snapshot становится active только после успешной persistent-записи.
            PlayerData snapshot = ClonePlayerData(GameDataManager.PlayerData);
            TutorialStorage.CreatePlayerDataBackup(snapshot.ToJson());
            _snapshot = snapshot;
        }

        /// <summary>
        /// Готовит чистое состояние для уроков базового управления.
        /// </summary>
        public void PrepareCoreLesson()
        {
            Begin();
            ApplyDefaultTrainingState();
        }

        /// <summary>
        /// Готовит состояние для покупки учебного скина.
        /// </summary>
        public void PrepareSkinLesson(int trainingCrystals)
        {
            if (trainingCrystals < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(trainingCrystals),
                    trainingCrystals,
                    "Tutorial training crystals cannot be negative.");
            }

            Begin();
            ApplyDefaultTrainingState();
            ResourceManager.SetResourceBalance(ResourceType.Crystals, trainingCrystals);
        }

        /// <summary>
        /// Готовит состояние для урока суперудара с указанным скином.
        /// </summary>
        public void PrepareSuperHitLesson(int skinId)
        {
            if (skinId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(skinId), skinId, "Tutorial skin id cannot be negative.");
            }

            Begin();

            ResourceManager.SetResourceBalance(ResourceType.Coins, 0);
            ResourceManager.SetResourceBalance(ResourceType.Crystals, 0);
            GameDataManager.PlayerData.AppliedSkinId = skinId;
            GameDataManager.PlayerData.PurchasedSkinIds = CreateTrainingSkinIds(skinId);
        }

        /// <summary>
        /// Восстанавливает исходные данные и фиксирует завершение tutorial.
        /// </summary>
        public void Complete()
        {
            RestoreSnapshot(markTutorialCompleted: true);
        }

        /// <summary>
        /// Восстанавливает исходные данные без фиксации завершения tutorial.
        /// </summary>
        public void Rollback()
        {
            RestoreSnapshot(markTutorialCompleted: false);
        }

        /// <summary>
        /// Восстанавливает данные после прерванной persistent tutorial-session.
        /// </summary>
        public static bool TryRecoverInterruptedTutorial()
        {
            if (!TutorialStorage.IsPlayerDataBackupActive)
            {
                return false;
            }

            if (!TutorialStorage.TryGetPlayerDataBackup(out string playerDataJson))
            {
                TutorialStorage.ClearPlayerDataBackup();
                return false;
            }

            PlayerData recoveredPlayerData;
            try
            {
                recoveredPlayerData = DeserializePlayerData(playerDataJson);
            }
            catch (Exception)
            {
                TutorialStorage.ClearPlayerDataBackup();
                return false;
            }

            GameDataManager.PlayerData = recoveredPlayerData;
            GameDataManager.SaveData();
            TutorialStorage.ClearPlayerDataBackup();
            return true;
        }

        private static void EnsurePlayerDataAvailable()
        {
            if (GameDataManager.PlayerData == null)
            {
                throw new InvalidOperationException("Cannot start tutorial before player data is loaded.");
            }
        }

        private static PlayerData ClonePlayerData(PlayerData playerData)
        {
            return DeserializePlayerData(playerData.ToJson());
        }

        private static PlayerData DeserializePlayerData(string playerDataJson)
        {
            PlayerData playerData = PlayerData.FromJson(playerDataJson);
            if (playerData == null)
            {
                throw new InvalidOperationException("Tutorial player data backup is empty.");
            }

            return playerData;
        }

        private static List<int> CreateTrainingSkinIds(int skinId)
        {
            var skinIds = new List<int> { DefaultSkinId };
            if (skinId != DefaultSkinId)
            {
                skinIds.Add(skinId);
            }

            return skinIds;
        }

        private void ApplyDefaultTrainingState()
        {
            GameDataManager.PlayerData.AppliedSkinId = DefaultSkinId;
            GameDataManager.PlayerData.PurchasedSkinIds = new List<int> { DefaultSkinId };
            GameDataManager.PlayerData.IsAccountPromptPending = false;
            ResourceManager.SetResourceBalance(ResourceType.Coins, 0);
            ResourceManager.SetResourceBalance(ResourceType.Crystals, 0);
        }

        private PlayerData ReadPersistentSnapshot()
        {
            if (!TutorialStorage.TryGetPlayerDataBackup(out string playerDataJson))
            {
                throw new InvalidOperationException("Tutorial player data backup exists but has no data.");
            }

            try
            {
                return DeserializePlayerData(playerDataJson);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException("Tutorial player data backup is invalid.", exception);
            }
        }

        private void RestoreSnapshot(bool markTutorialCompleted)
        {
            PlayerData snapshot = GetSnapshotForRestore();
            if (snapshot == null)
            {
                return;
            }

            // Persistent backup удаляется только после успешного сохранения восстановленных данных.
            snapshot.IsTutorialCompleted |= markTutorialCompleted;
            TutorialStorage.UpdatePlayerDataBackup(snapshot.ToJson());
            GameDataManager.PlayerData = snapshot;
            GameDataManager.SaveData();
            TutorialStorage.ClearPlayerDataBackup();
            _snapshot = null;
        }

        private PlayerData GetSnapshotForRestore()
        {
            if (_snapshot != null)
            {
                return _snapshot;
            }

            if (!TutorialStorage.HasPlayerDataBackup)
            {
                if (TutorialStorage.IsPlayerDataBackupActive)
                {
                    TutorialStorage.ClearPlayerDataBackup();
                }

                return null;
            }

            _snapshot = ReadPersistentSnapshot();
            return _snapshot;
        }
    }
}
