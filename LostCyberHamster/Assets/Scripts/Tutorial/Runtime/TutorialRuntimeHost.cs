using System;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;
using GameManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Assets.Scripts.Tutorial
{
    /// <summary>
    /// Unity lifecycle-оболочка tutorial workflow.
    /// </summary>
    public sealed class TutorialRuntimeHost : MonoBehaviour
    {
        private static TutorialRuntimeHost _instance;

        private TutorialFlowController _flow;
        private TutorialExternalInputSuppressor _externalInputSuppressor;
        private IDisposable _analyticsSuppression;
        private VisualElement _attachedGameplayRoot;

        public static void Create()
        {
            if (_instance != null)
            {
                return;
            }

            var host = new GameObject(nameof(TutorialRuntimeHost));
            _instance = host.AddComponent<TutorialRuntimeHost>();
            DontDestroyOnLoad(host);
        }

        private void Awake()
        {
            _flow = new TutorialFlowController(new TutorialSession());
            _externalInputSuppressor = new TutorialExternalInputSuppressor();
            _externalInputSuppressor.SetSuppressed(true);
            _analyticsSuppression = AnalyticsManager.SuppressTracking();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Update()
        {
            AttachGameplayContextIfAvailable();
            _flow.Tick();
            if (_flow.CanShutdown)
            {
                ReleaseRuntimeGuards();
                Destroy(gameObject);
                return;
            }

            bool isTutorialLevel = TutorialConstants.IsTutorialLevel(GameDataManager.PlayerData?.CurrentLevel);
            _externalInputSuppressor?.SetSuppressed(isTutorialLevel || _flow.RequiresExclusiveInput);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _attachedGameplayRoot = null;
            _flow.OnSceneLoaded();
            if (_flow.CanShutdown)
            {
                ReleaseRuntimeGuards();
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            var flow = _flow;
            _flow = null;
            _attachedGameplayRoot = null;

            if (_instance == this)
            {
                _instance = null;
            }

            try
            {
                flow?.Dispose();
            }
            finally
            {
                ReleaseRuntimeGuards();
            }
        }

        /// <summary>Поддерживает привязку HUD и completion после восстановления CurrentLevel.</summary>
        private void AttachGameplayContextIfAvailable()
        {
            // Новый gameplay создаётся по tutorial address; completion живёт в существующем controller.
            string currentLevel = GameDataManager.PlayerData?.CurrentLevel;
            if (TutorialConstants.IsTutorialLevel(currentLevel)
                && TryGetGameplayContext(out var gameManager, out var hamster))
            {
                _flow.EnsureGameplay(currentLevel, gameManager, hamster);
            }

            if (!_flow.RequiresGameplayRoot)
            {
                return;
            }

            // UIDocument может пересобрать дочерние элементы при прежней ссылке на root.
            if (_attachedGameplayRoot?.panel == null
                || _attachedGameplayRoot.Q<VisualElement>("tap") == null
                || _attachedGameplayRoot.Q<VisualElement>("btn_jump") == null)
            {
                _attachedGameplayRoot = null;
            }

            if (_attachedGameplayRoot == null && TryFindGameplayRoot(out var root))
            {
                _attachedGameplayRoot = root;
            }

            if (_attachedGameplayRoot != null)
            {
                _flow.AttachGameplayRoot(_attachedGameplayRoot);
            }
        }

        private static bool TryGetGameplayContext(out GameManager gameManager, out Hamster hamster)
        {
            gameManager = null;
            hamster = null;

            var levelData = LevelController.Instance?.LevelData;
            if (levelData == null)
            {
                return false;
            }

            gameManager = levelData.GameManager;
            hamster = levelData.Hamster;
            return gameManager != null && hamster != null;
        }

        private static bool TryFindGameplayRoot(out VisualElement root)
        {
            root = null;
            foreach (var uiDocument in UnityEngine.Object.FindObjectsByType<UIDocument>(
                         FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
            {
                var candidateRoot = uiDocument.rootVisualElement;
                if (candidateRoot?.panel == null
                    || candidateRoot.Q<VisualElement>("tap") == null
                    || candidateRoot.Q<VisualElement>("btn_jump") == null)
                {
                    continue;
                }

                root = candidateRoot;
                return true;
            }

            return false;
        }

        private void ReleaseRuntimeGuards()
        {
            _externalInputSuppressor?.Dispose();
            _analyticsSuppression?.Dispose();
            _externalInputSuppressor = null;
            _analyticsSuppression = null;
        }
    }
}
