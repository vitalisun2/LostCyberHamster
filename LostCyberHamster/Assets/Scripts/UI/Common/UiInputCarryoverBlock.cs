using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LostCyberHamster.UI
{
    /// <summary>Завершает текущую обработку ввода до смены UI или сцены.</summary>
    internal static class UiInputCarryoverBlock
    {
        public static async Task RunAfterCurrentEventAsync(Func<Task> transition)
        {
            if (transition == null)
                throw new ArgumentNullException(nameof(transition));

            using (UiInputBlock.Acquire())
            {
                // Не меняем дерево UI внутри dispatch исходного pointer/click события.
                await Task.Yield();
                await transition();
            }
        }

        public static async void LoadScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                throw new ArgumentException("Scene name must be provided.", nameof(sceneName));

            try
            {
                await RunAfterCurrentEventAsync(() =>
                {
                    SceneManager.LoadScene(sceneName);
                    return Task.CompletedTask;
                });
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
