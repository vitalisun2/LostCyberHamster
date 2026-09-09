using System;
using Assets.Scripts.System;
using GameManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LostCyberHamster.UI
{
    /// <summary>Передаёт конкретную цель существующим экранам; не выдаёт наград и не тратит ресурсы.</summary>
    internal static class NextGoalNavigation
    {
        private static NextGoalCandidate _pending;

        public static void Prepare(NextGoalCandidate goal)
        {
            _pending = goal?.IsCurrentProfile == true ? goal : null;
            if (_pending?.Action == NextGoalAction.ActivityReward)
                ReturnActivitiesScreenController.InitialKind = _pending.ActivityKind;
        }

        public static bool TryConsume(ScreenEnum screen, out NextGoalCandidate goal)
        {
            goal = _pending;
            _pending = null;
            return goal?.IsCurrentProfile == true && goal.Destination == screen;
        }

        public static void DiscardOtherDestination(ScreenEnum screen)
        {
            if (_pending?.Destination != screen || _pending?.IsCurrentProfile != true) _pending = null;
        }

        public static void Open(UIManager ui, NextGoalCandidate goal, string returnLevel = null)
        {
            if (goal?.IsCurrentProfile != true) return;
            // Урок и развитие пользуются существующим сохранённым обратным маршрутом.
            if (goal.StartsShieldLesson)
            {
                FirstSessionNavigation.Begin(ui, returnLevel, ui.CurrentScreen);
                if (ui.CurrentScreen != ScreenEnum.GameScreen && goal.Destination == ScreenEnum.CharacterScreen)
                    UIManager.OnScreenShow?.Invoke(ScreenEnum.CharacterScreen);
                return;
            }
            if ((goal.Destination == ScreenEnum.CharacterDevelopmentScreen || goal.Destination == ScreenEnum.CharacterScreen) &&
                !FirstSessionNavigation.HasReturnRoute)
                GameDataManager.ExecuteTransaction(CheckpointReason.FirstSessionTutorialProgressed,
                    () => FirstSessionNavigation.SetReturnRoute(returnLevel,
                        ui.CurrentScreen == ScreenEnum.GameScreen ? ScreenEnum.HomeScreen : ui.CurrentScreen));

            // Одноразовый target переживает загрузку Menu, сохраняя защиту профиля и поколения.
            Prepare(goal);
            if (ui.CurrentScreen == ScreenEnum.GameScreen)
            {
                MenuNavigationRequest.OpenScreen(goal.Destination);
                SceneManager.LoadScene("Menu");
            }
            else if (ui.CurrentScreen == ScreenEnum.SelectLevelScreen && goal.Destination == ScreenEnum.SelectLevelScreen)
                ui.GetController<SelectLevelScreenController>().ShowNextGoalTarget();
            else UIManager.OnScreenShow?.Invoke(goal.Destination);
        }

        public static string GetStageAddress(NextGoalCandidate goal)
        {
            if (goal.Action == NextGoalAction.Stage) return goal.Target;
            if (goal.IsOnboarding && goal.Destination == ScreenEnum.SelectLevelScreen &&
                LevelManager.TryGetContinueLevelKey(out var next)) return next;
            return null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => _pending = null;
    }
}
