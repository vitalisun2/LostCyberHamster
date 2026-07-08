using System.Collections.Generic;
using System.Threading.Tasks;
using Assets.Scripts.System;
using LoadingTasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
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
                await UnityServices.InitializeAsync();
                await AuthenticationManager.SignInCachedUserAsync();
            }
            catch
            {
                Debug.LogError("Authentication failed");
            }
        }
    }
}
