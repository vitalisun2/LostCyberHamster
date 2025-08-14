using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.System;
using LoadingTasks;
using UnityEngine;

namespace Assets.Scripts.Entry_Points.GameLoadingTasks
{
    [Serializable]
    public class InitCurrentLevelAssetsLoadingTask : ILoadingTaskSequence
    {
        public string Name => "Инициализация текущего уровня";

        // Use SerializeField to allow setting this in the Unity Editor
        [SerializeReference]
        private List<ILoadingTask> _children = new();
        public List<ILoadingTask> Children => _children;

        public async Task LoadAsync(Dictionary<string, object> bundle)
        {
            await LevelController.Instance.LoadLevelData();
        }
    }
}
