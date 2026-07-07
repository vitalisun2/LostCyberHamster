using LoadingTasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Assets.Scripts.Diagnostics;
using Assets.Scripts.GameManagerLogic;
using UnityEngine;
using Zenject;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Installers.Roots;
using Assets.Scripts.System;
using GameManagement;
using Vues.GameCore;

namespace Assets.Scripts.Entry_Points
{
    public class GameEntryPoint : MonoBehaviour
    {
        public LoadingTaskPipeline taskPipeline;

        private Dictionary<string, object> _bundle = new();

        [Inject]
        public void Construct(
            GameManager gameManager,
            GameObject introObject,
            EnvironmentRoot environmentRoot,
            Hamster characterPrefab,
            UiRoot uiRoot
            )
        {
            _bundle.Add("gameManager", gameManager);
            _bundle.Add("introObject", introObject);
            _bundle.Add("environmentRoot", environmentRoot);
            _bundle.Add("characterPrefab", characterPrefab);
            _bundle.Add("uiRoot", uiRoot);

        }

        private async Task Start()
        {
            try
            {
                if (taskPipeline != null && taskPipeline.Root != null)
                {
                    await ExecuteTask(taskPipeline.Root, _bundle);
                }
            }
            catch (Exception exception)
            {
                LogException("pipeline", exception);
                throw;
            }

            // Если интро не было (например, тестовый уровень), игра ещё в OFF — запускаем.
            // Если интро было, состояние INTRO или PLAYING — Intro.EndIntro() сам вызовет StartGame().
            var gm = (GameManager)_bundle["gameManager"];
            if (gm.State == GameState.OFF)
            {
                try
                {
                    gm.StartGame();
                }
                catch (Exception exception)
                {
                    LogException("auto_start_game", exception);
                    throw;
                }
            }
        }

        private async Task ExecuteTask(ILoadingTask task, Dictionary<string, object> bundle)
        {
            var taskName = GetTaskName(task);
            try
            {
                await task.LoadAsync(bundle);

                if (task is ILoadingTaskParallel parallelTask)
                {
                    await Task.WhenAll(parallelTask.Children.Select(c => ExecuteTask(c, bundle)));
                }
                else if (task is ILoadingTaskSequence sequenceTask)
                {
                    foreach (ILoadingTask child in sequenceTask.Children)
                    {
                        await ExecuteTask(child, bundle);
                    }
                }
            }
            catch (Exception exception)
            {
                LogException($"task={taskName}", exception);
                throw;
            }
        }

        private static string GetTaskName(ILoadingTask task)
        {
            if (task == null)
            {
                return "<null>";
            }

            return $"{task.GetType().FullName}/{task.Name}";
        }

        private static void LogException(string context, Exception exception)
        {
            DebugManager.DiagStability(
                $"[GAME ENTRY] exception context={context} " +
                $"type={exception.GetType().FullName} message={exception.Message} stack={exception.StackTrace}");
            Debug.LogException(exception);
            DeviceLogUploader.UploadDiagnosticLog("game_entry_exception");
        }

    }


}
