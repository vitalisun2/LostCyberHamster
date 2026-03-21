using UnityEngine;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Точка входа BotV3. Минимальный каркас: запуск, toggle по F1.
    /// Логика pipeline будет добавляться поэтапно.
    /// </summary>
    public class BotOrchestrator : MonoBehaviour
    {
        public bool IsEnabled { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            if (FindAnyObjectByType<BotOrchestrator>(FindObjectsInactive.Include) != null)
                return;

            var host = GameObject.Find("[Bot]");
            if (host == null)
                host = new GameObject("[Bot]");

            host.AddComponent<BotOrchestrator>();
        }

        private void Start()
        {
            Enable();
        }

        public void ToggleEnabledFromHotkey()
        {
            if (IsEnabled)
                Disable();
            else
                Enable();
        }

        private void Enable()
        {
            IsEnabled = true;
            Debug.Log("[BotV3] Enabled");
        }

        private void Disable()
        {
            IsEnabled = false;
            Debug.Log("[BotV3] Disabled");
        }
    }
}
