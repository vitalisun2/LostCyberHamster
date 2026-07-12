#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Assets.Scripts.Bot;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;
using GameManagement;
using LostCyberHamster.UI;
using UnityEngine;

namespace Assets.Scripts.DevTools.Gameplay
{
    /// <summary>
    /// Изолирует gameplay DEV-инструменты от конкретных runtime-компонентов и статических хранилищ игры.
    /// </summary>
    internal sealed class GameplayDevToolsService
    {
        public GameplayDevToolsSnapshot GetSnapshot()
        {
            RuntimeBotController bot = FindBot();
            bool canCompleteLevel =
                LevelController.Instance?.LevelData?.GameManager?.State == GameState.PLAYING;

            return new GameplayDevToolsSnapshot(
                bot != null,
                bot != null && bot.IsEnabled,
                DevToolsRuntimeState.UnlockAllLevels,
                canCompleteLevel);
        }

        public GameplayDevToolsActionResult ToggleBot()
        {
            RuntimeBotController bot = FindBot();
            if (bot == null)
                return GameplayDevToolsActionResult.Unavailable("Бот недоступен на текущей сцене");

            bot.SetEnabled(!bot.IsEnabled);
            return GameplayDevToolsActionResult.Success(bot.IsEnabled ? "Бот включён" : "Бот выключен");
        }

        public GameplayDevToolsActionResult ToggleUnlockAll()
        {
            DevToolsRuntimeState.UnlockAllLevels = !DevToolsRuntimeState.UnlockAllLevels;
            return GameplayDevToolsActionResult.Success(
                DevToolsRuntimeState.UnlockAllLevels
                    ? "Все уровни временно открыты"
                    : "Временное открытие уровней выключено");
        }

        public GameplayDevToolsActionResult ResetProgress()
        {
            // Очищаем сохраняемый прогресс и отдельно снимаем несохраняемый DEV override.
            GameDataManager.ResetLocalData();
            DevToolsRuntimeState.UnlockAllLevels = false;
            UIManager.OnRepaintScreen?.Invoke();
            return GameplayDevToolsActionResult.Success("Локальный прогресс сброшен");
        }

        public GameplayDevToolsActionResult CompleteLevelWithThreeStars()
        {
            LevelController levelController = LevelController.Instance;
            if (levelController?.LevelData?.GameManager?.State != GameState.PLAYING)
                return GameplayDevToolsActionResult.Unavailable("Завершение доступно только во время уровня");

            Hamster hamster = Object.FindAnyObjectByType<Hamster>(FindObjectsInactive.Include);
            if (hamster == null)
                return GameplayDevToolsActionResult.Unavailable("Хомяк не найден на текущей сцене");

            hamster.Lives.Value = 3;
            levelController.Finish();
            return GameplayDevToolsActionResult.Success("Уровень завершён с тремя звёздами", closePanel: true);
        }

        private static RuntimeBotController FindBot()
        {
            return Object.FindAnyObjectByType<RuntimeBotController>(FindObjectsInactive.Include);
        }
    }
}
#endif
