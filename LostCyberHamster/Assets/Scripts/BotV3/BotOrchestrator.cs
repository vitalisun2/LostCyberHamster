using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.GameManagerLogic;
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
        public Hamster Hamster { get; private set; }
        public GameManager GameManager { get; private set; }
        public bool Initialized { get; private set; }

        private BotHud _hud;
        private GameEventTracker _eventTracker;
        private float _nextInitRetryTime;

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

        private void Update()
        {
            if (!IsEnabled)
                return;

            if (!Initialized)
            {
                if (Time.time >= _nextInitRetryTime)
                {
                    TryInit();
                    _nextInitRetryTime = Time.time + 0.5f;
                }
                return;
            }

            if (GameManager == null || GameManager.State != GameState.PLAYING)
                return;

            if (Hamster.HamsterState.Value == HamsterStateEnum.Dead)
                return;

            // Pipeline будет здесь
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
            if (!Initialized)
                TryInit();
            DebugManager.DiagLog("[BotV3] Enabled");
        }

        private void Disable()
        {
            IsEnabled = false;
            DebugManager.DiagLog("[BotV3] Disabled");
        }

        private void TryInit()
        {
            Hamster = FindObjectOfType<Hamster>();
            GameManager = FindObjectOfType<GameManager>();

            if (Hamster == null || GameManager == null)
                return;

            _eventTracker = new GameEventTracker(Hamster, GameManager);

            Initialized = true;
            DebugManager.DiagLog("[BotV3] Initialized");
        }

        private void OnDestroy()
        {
            _eventTracker?.Dispose();
        }

        private void OnGUI()
        {
            _hud?.Draw();
        }
    }
}
