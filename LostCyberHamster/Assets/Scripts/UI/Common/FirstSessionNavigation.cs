using System;
using GameManagement;
using UnityEngine.SceneManagement;

namespace LostCyberHamster.UI
{
    /// <summary>Сохраняет выбранный маршрут на время урока развития персонажа.</summary>
    internal static class FirstSessionNavigation
    {
        public static bool HasReturnRoute => GameDataManager.PlayerData != null &&
            (!string.IsNullOrEmpty(GameDataManager.PlayerData.FirstSessionReturnScreen) ||
             !string.IsNullOrEmpty(GameDataManager.PlayerData.FirstSessionReturnLevel));

        public static void Begin(UIManager ui, string level = null,
            ScreenEnum screen = ScreenEnum.HomeScreen, string location = null, string part = null)
        {
            GameDataManager.ExecuteTransaction(CheckpointReason.FirstSessionTutorialProgressed, () =>
            {
                SetReturnRoute(level, screen, location, part);
                GameDataManager.PlayerData.IsShieldTutorialStarted = true;
            });
            OpenShield(ui);
        }

        // Эти prepare-методы вызываются внутри транзакции подтверждения Level Up.
        internal static void SetReturnRoute(string level, ScreenEnum screen, string location = null,
            string part = null, bool awaitingLevelUp = false, bool startShieldLesson = false)
        {
            var data = GameDataManager.PlayerData;
            data.FirstSessionReturnLevel = level;
            data.FirstSessionReturnScreen = screen.ToString();
            data.FirstSessionReturnLocation = location;
            data.FirstSessionReturnPart = part;
            data.FirstSessionReturnFromLevelUp = awaitingLevelUp;
            data.FirstSessionReturnToShield = startShieldLesson;
        }

        internal static Action PrepareShield(UIManager ui)
        {
            GameDataManager.PlayerData.IsShieldTutorialStarted = true;
            GameDataManager.PlayerData.FirstSessionReturnFromLevelUp = false;
            GameDataManager.PlayerData.FirstSessionReturnToShield = false;
            return () => OpenShield(ui);
        }

        /// <summary>Сохраняет обратный маршрут общего развития без запуска урока щита.</summary>
        internal static Action PrepareDevelopment(UIManager ui)
        {
            if (!HasReturnRoute) SetReturnRoute(null, ui.CurrentScreen);
            GameDataManager.PlayerData.FirstSessionReturnFromLevelUp = false;
            GameDataManager.PlayerData.FirstSessionReturnToShield = false;
            return () => OpenShield(ui);
        }

        private static void OpenShield(UIManager ui)
        {
            if (ui.CurrentScreen == ScreenEnum.GameScreen)
            {
                MenuNavigationRequest.OpenCharacterDevelopment();
                SceneManager.LoadScene("Menu");
            }
            else
                UIManager.OnScreenShow?.Invoke(ScreenEnum.CharacterDevelopmentScreen);
        }

        public static void Resume(UIManager ui)
        {
            Action navigate = null;
            GameDataManager.ExecuteTransaction(CheckpointReason.FirstSessionTutorialProgressed,
                () => navigate = PrepareResume(ui));
            navigate?.Invoke();
        }

        internal static Action PrepareResume(UIManager ui)
        {
            var data = GameDataManager.PlayerData;
            string level = data.FirstSessionReturnLevel;
            string location = data.FirstSessionReturnLocation;
            string part = data.FirstSessionReturnPart;
            if (!Enum.TryParse(data.FirstSessionReturnScreen, out ScreenEnum screen) ||
                screen == ScreenEnum.GameScreen || screen.ToString().EndsWith("Modal", StringComparison.Ordinal))
                screen = ScreenEnum.HomeScreen;
            if (!string.IsNullOrEmpty(level)) data.CurrentLevel = level;
            data.FirstSessionReturnLevel = null;
            data.FirstSessionReturnScreen = null;
            data.FirstSessionReturnLocation = null;
            data.FirstSessionReturnPart = null;
            data.FirstSessionReturnFromLevelUp = false;
            data.FirstSessionReturnToShield = false;
            return () =>
            {
                if (!string.IsNullOrEmpty(level))
                {
                    SceneManager.LoadScene("Game");
                    return;
                }
                if (ui.CurrentScreen == ScreenEnum.GameScreen)
                {
                    if (screen == ScreenEnum.LeaderboardScreen && !string.IsNullOrEmpty(location) && !string.IsNullOrEmpty(part))
                        MenuNavigationRequest.OpenLeaderboard(location, part);
                    else MenuNavigationRequest.OpenScreen(screen);
                    SceneManager.LoadScene("Menu");
                    return;
                }
                if (screen == ScreenEnum.LeaderboardScreen &&
                    !string.IsNullOrEmpty(location) && !string.IsNullOrEmpty(part))
                    ui.GetController<LeaderboardScreenController>().SetInitialSelection(location, part);
                UIManager.OnScreenShow?.Invoke(screen);
            };
        }
    }
}
