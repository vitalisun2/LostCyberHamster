using GameManagement;

namespace Assets.Scripts.Tutorial
{
    /// <summary>
    /// Управляет production-входом в tutorial и повторным запуском из меню.
    /// </summary>
    public static class TutorialLaunchService
    {
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

            TutorialRoutingDecision decision = DecideRouting(
                playerData.CurrentLevel,
                playerData.IsTutorialCompleted);
            if (!decision.ShouldRedirect)
            {
                return false;
            }

            ApplyRoutingDecision(playerData, decision);
            return true;
        }

        /// <summary>
        /// Проверяет автоматический tutorial redirect без чтения и потребления одноразовых flags.
        /// </summary>
        public static bool ShouldRedirectToTutorial(string levelAddress, bool isTutorialCompleted)
        {
            return DecideRouting(levelAddress, isTutorialCompleted).ShouldRedirect;
        }

        public static TutorialRoutingDecision DecideRouting(string levelAddress, bool isTutorialCompleted)
        {
            bool shouldRedirect = !isTutorialCompleted
                                  && !TutorialConstants.IsTutorialLevel(levelAddress)
                                  && TutorialConstants.IsFirstGameplayLevel(levelAddress);
            return shouldRedirect
                ? TutorialRoutingDecision.RedirectTo(TutorialConstants.CoreLessonLevelAddress)
                : TutorialRoutingDecision.None;
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
            GameDataManager.PlayerData.CurrentLevel = TutorialConstants.CoreLessonLevelAddress;
        }

        private static void ApplyRoutingDecision(PlayerData playerData, TutorialRoutingDecision decision)
        {
            EnsureSessionSnapshot();
            playerData.CurrentLevel = decision.TargetLevelAddress;
        }

        private static void EnsureSessionSnapshot()
        {
            new TutorialSession().Begin();
        }
    }
}
