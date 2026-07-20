using GameManagement;

namespace Assets.Scripts.Tutorial
{
    /// <summary>
    /// Управляет входом в tutorial, replay и одноразовым bypass первого уровня.
    /// </summary>
    public static class TutorialLaunchService
    {
        public const string ResetCompletedOnceKey = TutorialStorage.ResetCompletedOnceKey;

        /// <summary>
        /// Подменяет первый gameplay level на основной tutorial level при активных условиях запуска.
        /// </summary>
        public static bool RedirectFirstLevelToTutorialIfNeeded()
        {
            PlayerData playerData = GameDataManager.PlayerData;
            if (playerData == null)
            {
                return false;
            }

            bool isForcedReplay = TutorialStorage.HasForcedReplay;
            bool isFirstGameplayLevel = TutorialConstants.IsFirstGameplayLevel(playerData.CurrentLevel);
            bool hasFirstLevelBypass = isFirstGameplayLevel
                                       && !isForcedReplay
                                       && TutorialStorage.HasFirstLevelBypass;
            bool willRedirect = isForcedReplay
                                || (isFirstGameplayLevel
                                    && !hasFirstLevelBypass
                                    && (!playerData.IsTutorialCompleted
                                        || TutorialStorage.HasCompletedResetRequest));
            if (willRedirect)
            {
                EnsureSessionSnapshot();
            }

            ApplyCompletedResetIfRequested(playerData);

            // Forced replay имеет приоритет над текущим level и completion flag.
            if (TutorialStorage.ConsumeForcedReplay())
            {
                playerData.CurrentLevel = TutorialConstants.CoreLessonLevelAddress;
                return true;
            }

            if (TutorialConstants.IsFirstGameplayLevel(playerData.CurrentLevel)
                && TutorialStorage.ConsumeFirstLevelBypass())
            {
                return false;
            }

            if (!ShouldRedirectToTutorial(playerData.CurrentLevel, playerData.IsTutorialCompleted))
            {
                return false;
            }

            playerData.CurrentLevel = TutorialConstants.CoreLessonLevelAddress;
            return true;
        }

        /// <summary>
        /// Проверяет автоматический tutorial redirect без чтения и потребления одноразовых flags.
        /// </summary>
        public static bool ShouldRedirectToTutorial(string levelAddress, bool isTutorialCompleted)
        {
            return !isTutorialCompleted
                   && !TutorialConstants.IsTutorialLevel(levelAddress)
                   && TutorialConstants.IsFirstGameplayLevel(levelAddress);
        }

        /// <summary>
        /// Запрашивает replay и сразу выбирает основной tutorial level.
        /// </summary>
        public static void StartReplayFromMenu()
        {
            if (GameDataManager.PlayerData == null)
            {
                return;
            }

            EnsureSessionSnapshot();
            TutorialStorage.RequestForcedReplay();
            GameDataManager.PlayerData.CurrentLevel = TutorialConstants.CoreLessonLevelAddress;
        }

        /// <summary>
        /// Разрешает один запуск настоящего первого gameplay level без tutorial redirect.
        /// </summary>
        public static void AllowFirstGameplayLevelOnce()
        {
            TutorialStorage.RequestFirstLevelBypass();
        }

        /// <summary>
        /// Запрашивает одноразовый сброс completion flag перед следующим routing pass.
        /// </summary>
        public static void RequestCompletedResetOnce()
        {
            TutorialStorage.RequestCompletedReset();
        }

        public static void ClearCompletedResetRequest()
        {
            TutorialStorage.ClearCompletedReset();
        }

        private static void ApplyCompletedResetIfRequested(PlayerData playerData)
        {
            if (TutorialStorage.ConsumeCompletedReset())
            {
                playerData.IsTutorialCompleted = false;
            }
        }

        private static void EnsureSessionSnapshot()
        {
            new TutorialSession().Begin();
        }
    }
}
