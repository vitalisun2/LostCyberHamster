using System;
using System.Collections.Generic;
using GameManagement;
using GameManagement.Progress;
using GameManagement.Leaderboard;
using UnityEngine;
using Vues.GameCore;
using Vues.GameCore.Quests;

namespace Assets.Scripts.Tutorial
{
    /// <summary>
    /// Изолирует учебное состояние игрока и восстанавливает исходный snapshot.
    /// </summary>
    public sealed class TutorialSession
    {
        private const int DefaultSkinId = 0;

        private PlayerData _snapshot;
        private bool _pendingLevelChange;

        public bool IsActive => _snapshot != null;
        public int CompletionExperienceAwarded { get; private set; }

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
                _snapshot = ReadPersistentSnapshot(out bool wasRepaired);
                if (wasRepaired)
                {
                    TutorialStorage.UpdatePlayerDataBackup(_snapshot.ToJson());
                }

                if (!TutorialStorage.IsPlayerDataBackupActive)
                {
                    TutorialStorage.MarkPlayerDataBackupActive();
                }

                return;
            }

            // Новый snapshot становится active только после успешной persistent-записи.
            PlayerData snapshot = CloneValidatedPlayerData(GameDataManager.PlayerData);
            TutorialStorage.CreatePlayerDataBackup(snapshot.ToJson());
            _snapshot = snapshot;
            FirstSessionTelemetry.Record(snapshot.IsTutorialCompleted ? "tutorial_replay" : "onboarding_start", "core");
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
        /// Восстанавливает исходные данные и фиксирует завершение tutorial.
        /// </summary>
        public void Complete(string nextLevelAddress, bool skipped = false)
        {
            if (string.IsNullOrWhiteSpace(nextLevelAddress))
            {
                throw new ArgumentException("Tutorial completion level cannot be empty.", nameof(nextLevelAddress));
            }

            RestoreSnapshot(markTutorialCompleted: true, nextLevelAddress, skipped);
        }

        /// <summary>
        /// Восстанавливает исходные данные без фиксации завершения tutorial.
        /// </summary>
        public void Rollback()
        {
            RestoreSnapshot(markTutorialCompleted: false, nextLevelAddress: null);
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
                Debug.LogError("Tutorial recovery failed: active snapshot marker has no backup data.");
                return false;
            }

            PlayerData recoveredPlayerData;
            bool wasRepaired;
            try
            {
                recoveredPlayerData = DeserializeValidatedPlayerData(playerDataJson, out wasRepaired);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Tutorial recovery preserved invalid snapshot: {exception.Message}");
                return false;
            }

            if (wasRepaired)
            {
                TutorialStorage.UpdatePlayerDataBackup(recoveredPlayerData.ToJson());
            }

            GameDataManager.PlayerData = recoveredPlayerData;
            PlayerProgressCommitter.Commit(CheckpointReason.CurrentLevelChanged);
            TutorialStorage.ClearPlayerDataBackup();
            // Повторная публикация текущего состояния безопасна для state-квестов.
            if (recoveredPlayerData.PlayerLevel > 1)
                GameEventsManager.PlayerStateChanged(PlayerStateIds.PlayerLevel, PlayerStateEntityIds.Player);
            WeeklyLeaderboardCoordinator.Instance?.RetryPendingRewards();
            return true;
        }

        private static void EnsurePlayerDataAvailable()
        {
            if (GameDataManager.PlayerData == null)
            {
                throw new InvalidOperationException("Cannot start tutorial before player data is loaded.");
            }
        }

        private static PlayerData CloneValidatedPlayerData(PlayerData playerData)
        {
            return DeserializeValidatedPlayerData(playerData.ToJson(), out _);
        }

        private static PlayerData DeserializeValidatedPlayerData(string playerDataJson, out bool wasRepaired)
        {
            PlayerData playerData = PlayerData.FromJson(playerDataJson);
            if (playerData == null)
            {
                throw new InvalidOperationException("Tutorial player data backup is empty.");
            }

            PlayerDataValidationResult validation = PlayerDataValidator.Validate(playerData);
            wasRepaired = validation.Status == PlayerDataValidationStatus.Repairable;
            if (wasRepaired)
            {
                PlayerDataValidator.RepairSafe(playerData, validation);
                validation = PlayerDataValidator.Validate(playerData);
            }

            if (validation.Status != PlayerDataValidationStatus.Valid)
            {
                throw new InvalidOperationException($"Tutorial player data snapshot rejected: {validation.Reason}");
            }

            return playerData;
        }

        private void ApplyDefaultTrainingState()
        {
            GameDataManager.PlayerData.AppliedSkinId = DefaultSkinId;
            GameDataManager.PlayerData.PurchasedSkinIds = new List<int> { DefaultSkinId };
            GameDataManager.PlayerData.UnlockedSkinIds = new List<int> { DefaultSkinId };
            GameDataManager.PlayerData.IsAccountPromptPending = false;
            ResourceManager.SetResourceBalance(ResourceType.Coins, 0);
            ResourceManager.SetResourceBalance(ResourceType.Crystals, 0);
        }

        private PlayerData ReadPersistentSnapshot(out bool wasRepaired)
        {
            if (!TutorialStorage.TryGetPlayerDataBackup(out string playerDataJson))
            {
                throw new InvalidOperationException("Tutorial player data backup exists but has no data.");
            }

            try
            {
                return DeserializeValidatedPlayerData(playerDataJson, out wasRepaired);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException("Tutorial player data backup is invalid.", exception);
            }
        }

        private void RestoreSnapshot(bool markTutorialCompleted, string nextLevelAddress, bool skipped = false)
        {
            PlayerData snapshot = GetSnapshotForRestore();
            if (snapshot == null)
            {
                return;
            }

            // Подготавливаем отдельный итоговый профиль; учебные ресурсы остаются в изоляции.
            snapshot = CloneValidatedPlayerData(snapshot);
            if (markTutorialCompleted)
            {
                bool hadReward = snapshot.HasReceivedTutorialExperience;
                _pendingLevelChange |= new PlayerExperienceService().GrantExperienceForTutorialCompletion(snapshot);
                if (!hadReward)
                    CompletionExperienceAwarded = PlayerExperienceService.TutorialExperienceReward;
                if (!snapshot.IsTutorialCompleted)
                    snapshot.IsTutorialSkipped = skipped;
            }
            snapshot.IsTutorialCompleted |= markTutorialCompleted;
            if (!string.IsNullOrWhiteSpace(nextLevelAddress))
            {
                snapshot.CurrentLevel = nextLevelAddress;
            }

            snapshot = CloneValidatedPlayerData(snapshot);
            TutorialStorage.UpdatePlayerDataBackup(snapshot.ToJson());
            // Retry использует тот же записанный intent, включая однократную выдачу.
            _snapshot = snapshot;
            GameDataManager.PlayerData = snapshot;
            if (markTutorialCompleted)
            {
                PlayerProgressCommitter.Commit(CheckpointReason.TutorialCompleted);
            }
            else
            {
                PlayerProgressCommitter.Commit(CheckpointReason.CurrentLevelChanged);
            }

            TutorialStorage.ClearPlayerDataBackup();
            _snapshot = null;
            Assets.Scripts.Diagnostics.EconomyTelemetry.Committed(markTutorialCompleted
                ? CheckpointReason.TutorialCompleted : CheckpointReason.CurrentLevelChanged);
            Assets.Scripts.Diagnostics.EconomyTelemetry.FinishRun(markTutorialCompleted ? "tutorial_completed" : "exit");
            bool publishLevelChange = _pendingLevelChange;
            _pendingLevelChange = false;
            PlayerExperienceService.PublishCommittedLevelChange(publishLevelChange, "tutorial");
            if (markTutorialCompleted)
            {
                FirstSessionTelemetry.Record(skipped ? "tutorial_skip" : "tutorial_completed", "core");
                if (CompletionExperienceAwarded > 0)
                    FirstSessionTelemetry.Record("bonus_committed", "tutorial_start_150", CompletionExperienceAwarded);
            }
            WeeklyLeaderboardCoordinator.Instance?.RetryPendingRewards();
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
                    throw new InvalidOperationException(
                        "Tutorial snapshot marker exists, but the protected player data backup is missing.");
                }

                return null;
            }

            _snapshot = ReadPersistentSnapshot(out bool wasRepaired);
            if (wasRepaired)
            {
                TutorialStorage.UpdatePlayerDataBackup(_snapshot.ToJson());
            }

            return _snapshot;
        }
    }
}
