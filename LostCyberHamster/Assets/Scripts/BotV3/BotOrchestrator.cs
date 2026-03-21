using UnityEngine;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Оркестратор BotV3. Вешается на GameObject в сцене.
    /// Горячая клавиша F1: вкл/выкл.
    /// Логика pipeline будет добавляться поэтапно.
    /// </summary>
    public class BotOrchestrator : MonoBehaviour
    {
        public bool IsEnabled { get; private set; }
        public string HudText { get; private set; } = "Plan: none";

        private BotHud _hud;

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
            _hud = new BotHud(this);
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
            HudText = "Plan: none";
            Debug.Log("[BotV3] Enabled");
        }

        private void Disable()
        {
            IsEnabled = false;
            Debug.Log("[BotV3] Disabled");
        }

        private void OnGUI()
        {
            _hud?.Draw();
        }
    }
}
