using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameManagement;
using LoadingTasks;

namespace Assets.Scripts.TutorialOld
{
    [Serializable]
    public sealed class InitTutorialRuntimeLoadingTask : ILoadingTask
    {
        public string Name => "Init tutorial runtime";

        public List<ILoadingTask> Children { get; } = new();

        public Task LoadAsync(Dictionary<string, object> bundle)
        {
            if (TutorialConstants.IsTutorialLevel(GameDataManager.PlayerData?.CurrentLevel))
            {
                TutorialRuntimeHost.Create();
            }

            return Task.CompletedTask;
        }
    }
}
