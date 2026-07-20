using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LoadingTasks;

namespace Assets.Scripts.Tutorial
{
    [Serializable]
    public sealed class TutorialLevelRoutingLoadingTask : ILoadingTask
    {
        public string Name => "Tutorial level routing";

        public List<ILoadingTask> Children { get; } = new();

        public Task LoadAsync(Dictionary<string, object> bundle)
        {
            TutorialLaunchService.RedirectFirstLevelToTutorialIfNeeded();
            return Task.CompletedTask;
        }
    }
}
