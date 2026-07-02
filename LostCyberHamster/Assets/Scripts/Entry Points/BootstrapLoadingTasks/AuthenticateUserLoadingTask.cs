using System.Collections.Generic;
using System.Threading.Tasks;
using Assets.Scripts.System;
using LostCyberHamster.Account;
using LoadingTasks;
using UnityEngine;

namespace Assets.Scripts.Entry_Points.BootstrapLoadingTasks
{
    public class AuthenticateUserLoadingTask : ILoadingTaskSequence
    {
        public string Name => "Аутентификация";
        public List<ILoadingTask> Children { get; }

        public AuthenticateUserLoadingTask()
        {
        }

        public async Task LoadAsync(Dictionary<string, object> bundle)
        {
            if (AutomationRuntimePrefs.IsTestLevelAutomationRun())
            {
                DebugManager.DiagStability("[AUTOMATION] Unity Authentication skipped for test-level run.");
                return;
            }

            try
            {
                await AccountServiceProvider.Current.EnsureSignedInAsync();
            }
            catch
            {
                Debug.LogError("Authentication failed");
            }
        }
    }
}
