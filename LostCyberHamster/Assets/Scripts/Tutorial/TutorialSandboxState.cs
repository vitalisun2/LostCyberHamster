using System.Collections.Generic;
using GameManagement;
using UnityEngine;
using Vues.GameCore;

namespace Assets.Scripts.Tutorial
{
    public static class TutorialSandboxState
    {
        private const int _defaultSkinId = 0;
        private const string _playerDataPrefsKey = "PlayerData";

        private static Snapshot _snapshot;

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
            GameDataManager.PlayerData.Crystals = trainingCrystals;
            CrystalStorage.Init(trainingCrystals);
            Log($"prepared skin purchase crystals={trainingCrystals}");
        }

        public static void PrepareSuperHitLesson(int electricStrikeSkinId)
        {
            EnsureSnapshot();
            IsActive = true;
            GameDataManager.PlayerData.Money = 0;
            GameDataManager.PlayerData.Crystals = 0;
            GameDataManager.PlayerData.AppliedSkinId = electricStrikeSkinId;
            GameDataManager.PlayerData.PurchasedSkinIds = new List<int> { _defaultSkinId, electricStrikeSkinId };
            MoneyStorage.Init(0);
            CrystalStorage.Init(0);
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

            GameDataManager.PlayerData.Money = _snapshot.Money;
            GameDataManager.PlayerData.Crystals = _snapshot.Crystals;
            GameDataManager.PlayerData.AppliedSkinId = _snapshot.AppliedSkinId;
            GameDataManager.PlayerData.PurchasedSkinIds = new List<int>(_snapshot.PurchasedSkinIds);
            GameDataManager.PlayerData.CurrentLevel = _snapshot.CurrentLevel;
            GameDataManager.PlayerData.IsTutorialCompleted = _snapshot.IsTutorialCompleted || markTutorialCompleted;
            MoneyStorage.Init(_snapshot.Money);
            CrystalStorage.Init(_snapshot.Crystals);
            GameDataManager.SaveData();
            IsActive = false;
            Log("restored real state");
        }

        public static void PreserveRealPersistentState()
        {
            if (!IsActive)
            {
                return;
            }

            if (_snapshot.HasPlayerDataPrefs)
            {
                PlayerPrefs.SetString(_playerDataPrefsKey, _snapshot.PlayerDataPrefs);
            }
            else
            {
                PlayerPrefs.DeleteKey(_playerDataPrefsKey);
            }

            PlayerPrefs.Save();
        }

        private static void ApplyDefaultTrainingState()
        {
            GameDataManager.PlayerData.AppliedSkinId = _defaultSkinId;
            GameDataManager.PlayerData.PurchasedSkinIds = new List<int> { _defaultSkinId };
            GameDataManager.PlayerData.Money = 0;
            GameDataManager.PlayerData.Crystals = 0;
            MoneyStorage.Init(0);
            CrystalStorage.Init(0);
        }

        private static void EnsureSnapshot()
        {
            if (IsActive)
            {
                return;
            }

            _snapshot = new Snapshot(
                GameDataManager.PlayerData.Money,
                GameDataManager.PlayerData.Crystals,
                GameDataManager.PlayerData.AppliedSkinId,
                GameDataManager.PlayerData.PurchasedSkinIds,
                GameDataManager.PlayerData.CurrentLevel,
                GameDataManager.PlayerData.IsTutorialCompleted,
                PlayerPrefs.HasKey(_playerDataPrefsKey),
                PlayerPrefs.GetString(_playerDataPrefsKey, string.Empty));
        }

        private static void Log(string message)
        {
            DebugManager.DiagStability($"[TUTORIAL SANDBOX] {message}");
        }

        private readonly struct Snapshot
        {
            public Snapshot(
                int money,
                int crystals,
                int appliedSkinId,
                IEnumerable<int> purchasedSkinIds,
                string currentLevel,
                bool isTutorialCompleted,
                bool hasPlayerDataPrefs,
                string playerDataPrefs)
            {
                Money = money;
                Crystals = crystals;
                AppliedSkinId = appliedSkinId;
                PurchasedSkinIds = new List<int>(purchasedSkinIds ?? new[] { _defaultSkinId });
                CurrentLevel = currentLevel;
                IsTutorialCompleted = isTutorialCompleted;
                HasPlayerDataPrefs = hasPlayerDataPrefs;
                PlayerDataPrefs = playerDataPrefs;
            }

            public int Money { get; }
            public int Crystals { get; }
            public int AppliedSkinId { get; }
            public IReadOnlyList<int> PurchasedSkinIds { get; }
            public string CurrentLevel { get; }
            public bool IsTutorialCompleted { get; }
            public bool HasPlayerDataPrefs { get; }
            public string PlayerDataPrefs { get; }
        }
    }
}
