using System;
using System.Collections.Generic;
using System.Diagnostics;
using GameManagement;
using UnityEngine;
using Vues.GameCore;

namespace Assets.Scripts.TutorialOld
{
    public static class TutorialSandboxState
    {
        private const int _defaultSkinId = 0;
        private const string _backupKey = "Tutorial.PlayerDataBackup";
        private const string _activeMarkerKey = "Tutorial.PlayerDataBackupActive";

        private static PlayerData _snapshot;

        public static bool IsActive { get; private set; }

        public static void PrepareCoreLesson()
        {
            EnsureSnapshot();
            IsActive = true;
            ApplyDefaultTrainingState();
            Log("prepared core lesson");
        }

        public static void PrepareSkinPurchaseLesson(int trainingCrystals)
        {
            EnsureSnapshot();
            IsActive = true;
            ApplyDefaultTrainingState();
            ResourceManager.SetResourceBalance(ResourceType.Crystals, trainingCrystals);
            Log($"prepared skin purchase crystals={trainingCrystals}");
        }

        public static void PrepareSuperHitLesson(int electricStrikeSkinId)
        {
            EnsureSnapshot();
            IsActive = true;
            ResourceManager.SetResourceBalance(ResourceType.Coins, 0);
            ResourceManager.SetResourceBalance(ResourceType.Crystals, 0);
            GameDataManager.PlayerData.AppliedSkinId = electricStrikeSkinId;
            GameDataManager.PlayerData.PurchasedSkinIds = new List<int> { _defaultSkinId, electricStrikeSkinId };
            Log($"prepared super hit skin={electricStrikeSkinId}");
        }

        public static void RestoreRealState()
        {
            RestoreRealState(markTutorialCompleted: false);
        }

        public static void RestoreRealState(bool markTutorialCompleted)
        {
            if (!IsActive)
            {
                return;
            }

            GameDataManager.PlayerData = _snapshot;
            GameDataManager.PlayerData.IsTutorialCompleted |= markTutorialCompleted;
            IsActive = false;
            GameDataManager.SaveData();
            ClearPersistentBackup();
            _snapshot = null;
            Log("restored real state");
        }

        public static bool TryRecoverInterruptedTutorial()
        {
            if (!PlayerPrefs.HasKey(_activeMarkerKey))
            {
                return false;
            }

            if (!PlayerPrefs.HasKey(_backupKey))
            {
                ClearPersistentBackup();
                return false;
            }

            PlayerData recoveredPlayerData;
            try
            {
                recoveredPlayerData = PlayerData.FromJson(PlayerPrefs.GetString(_backupKey));
                if (recoveredPlayerData == null)
                {
                    throw new InvalidOperationException("Tutorial backup is empty.");
                }
            }
            catch (Exception)
            {
                ClearPersistentBackup();
                return false;
            }

            GameDataManager.PlayerData = recoveredPlayerData;
            IsActive = false;
            GameDataManager.SaveData();
            ClearPersistentBackup();
            _snapshot = null;
            return true;
        }

        private static void ApplyDefaultTrainingState()
        {
            GameDataManager.PlayerData.AppliedSkinId = _defaultSkinId;
            GameDataManager.PlayerData.PurchasedSkinIds = new List<int> { _defaultSkinId };
            ResourceManager.SetResourceBalance(ResourceType.Coins, 0);
            ResourceManager.SetResourceBalance(ResourceType.Crystals, 0);
        }

        private static void EnsureSnapshot()
        {
            if (IsActive)
            {
                return;
            }

            _snapshot = ClonePlayerData(GameDataManager.PlayerData);
            PlayerPrefs.SetString(_backupKey, _snapshot.ToJson());
            PlayerPrefs.SetInt(_activeMarkerKey, 1);
            PlayerPrefs.Save();
        }

        private static PlayerData ClonePlayerData(PlayerData playerData)
        {
            return PlayerData.FromJson(playerData.ToJson());
        }

        private static void ClearPersistentBackup()
        {
            PlayerPrefs.DeleteKey(_backupKey);
            PlayerPrefs.DeleteKey(_activeMarkerKey);
            PlayerPrefs.Save();
        }

        [Conditional("LCH_VERBOSE_TUTORIAL_DIAGNOSTICS")]
        private static void Log(string message)
        {
        }

    }
}
