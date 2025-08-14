using LoadingTasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Assets.Scripts.GameManagerLogic;
using UnityEngine;
using Zenject;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Installers.Roots;
using Assets.Scripts.System;
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
            if (taskPipeline != null && taskPipeline.Root != null)
            {
                await ExecuteTask(taskPipeline.Root, _bundle);
            }

            Debug.Log("Game loaded");

           
        }

        private async Task ExecuteTask(ILoadingTask task, Dictionary<string, object> bundle)
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

            Debug.Log($"Finished task: {task.Name}");
        }
    }


}

