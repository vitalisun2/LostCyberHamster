using System.Collections.Generic;
using System.Threading.Tasks;
using LoadingTasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;

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
