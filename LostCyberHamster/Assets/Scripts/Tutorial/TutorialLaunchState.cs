using GameManagement;
using UnityEngine;

namespace Assets.Scripts.Tutorial
{
    /// <summary>
    /// Управляет одноразовым переходом из tutorial в настоящий первый уровень.
    /// </summary>
    public static class TutorialLaunchState
    {
        public const string ResetCompletedOnceKey = "Tutorial_ResetCompletedOnce";

        private const string _firstLevelBypassKey = "Tutorial_FirstLevel_BypassOnce";
        private const string _forcedReplayKey = "Tutorial_ForcedReplayOnce";

        /// <summary>
        /// Подменяет первый уровень на tutorial, если нет одноразового bypass.
        /// </summary>
        public static bool RedirectFirstLevelToTutorialIfNeeded()
        {
            var currentLevel = GameDataManager.PlayerData?.CurrentLevel;
            ApplyCompletedResetIfRequested();

            if (!ShouldRedirectToTutorial(currentLevel))
            {
                return false;
            }

            GameDataManager.PlayerData.CurrentLevel = TutorialConstants.TutorialLevelAddress;

            return true;
        }

        public static void StartReplayFromMenu()
        {
            PlayerPrefs.SetInt(_forcedReplayKey, 1);
            GameDataManager.PlayerData.CurrentLevel = TutorialConstants.TutorialLevelAddress;
        }

        /// <summary>
        /// Разрешает один запуск настоящего первого уровня без tutorial-редиректа.
        /// </summary>
        public static void AllowFirstGameplayLevelOnce()
        {
            PlayerPrefs.SetInt(_firstLevelBypassKey, 1);
            PlayerPrefs.Save();
        }

        private static bool ShouldRedirectToTutorial(string levelAddress)
        {
            if (ConsumeForcedReplay())
            {
                return true;
            }

            if (TutorialConstants.IsTutorialLevel(levelAddress))
            {
                return false;
            }

            if (!TutorialConstants.IsFirstGameplayLevel(levelAddress))
            {
                return false;
            }

            if (GameDataManager.PlayerData.IsTutorialCompleted)
            {
                return false;
            }

            return !ConsumeFirstLevelBypass();
        }

        private static void ApplyCompletedResetIfRequested()
        {
            if (!PlayerPrefs.HasKey(ResetCompletedOnceKey) || GameDataManager.PlayerData == null)
            {
                return;
            }

            PlayerPrefs.DeleteKey(ResetCompletedOnceKey);
            PlayerPrefs.Save();
            GameDataManager.PlayerData.IsTutorialCompleted = false;
        }

        private static bool ConsumeForcedReplay()
        {
            if (!PlayerPrefs.HasKey(_forcedReplayKey))
            {
                return false;
            }

            PlayerPrefs.DeleteKey(_forcedReplayKey);
            PlayerPrefs.Save();
            return true;
        }

        private static bool ConsumeFirstLevelBypass()
        {
            if (!PlayerPrefs.HasKey(_firstLevelBypassKey))
            {
                return false;
            }

            PlayerPrefs.DeleteKey(_firstLevelBypassKey);
            PlayerPrefs.Save();
            return true;
        }
    }
}
