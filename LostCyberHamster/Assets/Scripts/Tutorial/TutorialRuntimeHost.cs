using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;
using GameManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Assets.Scripts.Tutorial
{
    public sealed class TutorialRuntimeHost : MonoBehaviour
    {
        private static TutorialRuntimeHost _instance;

        private TutorialGameController _controller;
        private TutorialExternalInputSuppressor _externalInputSuppressor;
        private string _initializedLevel;
        private float _nextPersistentStatePreserveTime;

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
            _externalInputSuppressor = new TutorialExternalInputSuppressor();
            UpdateExternalInputSuppression();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _controller?.Dispose();
            _externalInputSuppressor?.Dispose();
            _controller = null;
            _externalInputSuppressor = null;

            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _controller?.Dispose();
            _controller = null;
            _initializedLevel = null;
        }

        private void Update()
        {
            UpdateExternalInputSuppression();
            PreserveSandboxPersistentStateIfNeeded();
            TutorialUiRuntime.Tick();
            UpdateGameplayTutorial();
        }

        private void UpdateExternalInputSuppression()
        {
            var currentLevel = GameDataManager.PlayerData?.CurrentLevel;
            bool tutorialActive =
                TutorialConstants.IsTutorialLevel(currentLevel)
                || TutorialMetaCoordinator.IsActive
                || TutorialSandboxState.IsActive;

            if (tutorialActive)
            {
                _externalInputSuppressor?.Suppress();
                return;
            }

            _externalInputSuppressor?.Restore();
        }

        private void PreserveSandboxPersistentStateIfNeeded()
        {
            if (!TutorialSandboxState.IsActive || Time.unscaledTime < _nextPersistentStatePreserveTime)
            {
                return;
            }

            _nextPersistentStatePreserveTime = Time.unscaledTime + 0.5f;
            TutorialSandboxState.PreserveRealPersistentState();
        }

        private void UpdateGameplayTutorial()
        {
            var currentLevel = GameDataManager.PlayerData?.CurrentLevel;
            if (!TutorialConstants.IsTutorialLevel(currentLevel))
            {
                if (!TutorialMetaCoordinator.IsActive && !TutorialSandboxState.IsActive)
                {
                    Destroy(gameObject);
                }

                return;
            }

            if (!TryGetGameplayContext(out var gameManager, out var hamster))
            {
                return;
            }

            _controller ??= new TutorialGameController(gameManager, hamster);
            AttachGameplayRootIfAvailable();

            if (_initializedLevel != currentLevel)
            {
                _controller.InitializeIfNeeded();
                _initializedLevel = currentLevel;
            }

            _controller.OnUpdate();
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

        private void AttachGameplayRootIfAvailable()
        {
            if (!TryFindGameplayRoot(out var root))
            {
                return;
            }

            _controller?.AttachGameplayRoot(root);
        }

        private static bool TryFindGameplayRoot(out VisualElement root)
        {
            root = null;
            foreach (var uiDocument in UnityEngine.Object.FindObjectsByType<UIDocument>(
                         FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
            {
                var candidateRoot = uiDocument.rootVisualElement;
                if (candidateRoot?.panel == null)
                {
                    continue;
                }

                if (candidateRoot.Q<VisualElement>("tap") == null
                    || candidateRoot.Q<VisualElement>("btn_jump") == null)
                {
                    continue;
                }

                root = candidateRoot;
                return true;
            }

            return false;
        }
    }
}
