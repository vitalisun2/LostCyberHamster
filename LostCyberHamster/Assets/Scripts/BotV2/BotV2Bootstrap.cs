using UnityEngine;

namespace Assets.Scripts.BotV2
{
    /// <summary>
    /// Подключает BotOrchestrator как основной бот при загрузке сцены.
    /// </summary>
    public static class BotV2Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsurePrimaryBot()
        {
            EnsureOrchestrator().EnableAsPrimary();
        }

        private static BotOrchestrator EnsureOrchestrator()
        {
            var existing = Object.FindObjectOfType<BotOrchestrator>(true);
            if (existing != null)
            {
                existing.enabled = true;
                return existing;
            }

            var host = GameObject.Find("[Bot]");
            if (host == null)
                host = new GameObject("[Bot]");

            var orchestrator = host.GetComponent<BotOrchestrator>();
            if (orchestrator == null)
                orchestrator = host.AddComponent<BotOrchestrator>();

            orchestrator.enabled = true;
            return orchestrator;
        }
    }
}
