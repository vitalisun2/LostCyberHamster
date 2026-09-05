using System.Collections.Generic;
using System.Threading.Tasks;
using Assets.Scripts.System;
using Vues.GameCore;
using LoadingTasks;
using Assets.Scripts.Online;

namespace Assets.Scripts.Entry_Points.BootstrapLoadingTasks
{
    public class InitAnalyticsManagerLoadingTask : ILoadingTaskSequence
    {
        public string Name => "Инициализация системы аналитики";

        public List<ILoadingTask> Children { get; }

        public InitAnalyticsManagerLoadingTask()
        {
        }

        public Task LoadAsync(Dictionary<string, object> bundle)
        {
            if (AutomationRuntimePrefs.IsTestLevelAutomationRun())
            {
                DebugManager.DiagStability("[AUTOMATION] Unity Analytics skipped for test-level run.");
                return Task.CompletedTask;
            }

            OnlineServicesCoordinator.Register("analytics", AnalyticsManager.InitializeAsync,
                () => OnlineServicesCoordinator.UnityServicesReady && !AnalyticsManager.IsInitialized);
            return Task.CompletedTask;
        }
    }
}
