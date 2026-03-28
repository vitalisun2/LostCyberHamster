using Assets.Scripts.GameManagerLogic;
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using GameManagement;
using Assets.Scripts;
using UnityEngine.Serialization;

namespace Assets.Scripts.System
{
    public class LevelController : MonoBehaviour,
        Listeners.IGameIntroListener,
        Listeners.IGameFinishListener
    {
        private const float DefaultTimeScale = 1.0f;
        private const float BotEnabledDefaultTimeScale = 2.0f;

        [SerializeField]
        [FormerlySerializedAs("timeScale")]
        [Range(0.1f, 2.0f)] // Настройка диапазона для удобства в инспекторе
        private float _timeScale = DefaultTimeScale; // Скорость рантайма

        public LevelData LevelData { get; private set; } = new();
        public bool IsLevelLoaded { get; private set; }

        public static LevelController Instance { get; private set; }

        private Intro _introComponent;
        private global::Assets.Scripts.BotV3.BotOrchestrator _botOrchestrator;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Устанавливаем начальную скорость игры
            Time.timeScale = DefaultTimeScale;
        }

        private void Update()
        {
            // гард: ждём, пока GameManager будет инициализирован
            if (Instance?.LevelData?.GameManager == null) return;

            // Обновляем скорость игры, если изменено значение в инспекторе
            var newTs = GetConfiguredTimeScale() * Instance.LevelData.GameManager.TimeScaleCoefficient;
            if (!Mathf.Approximately(Time.timeScale, newTs))
                Time.timeScale = newTs;

        }

        public void Init(GameManager gameManager)
        {
            LevelData.GameManager = gameManager;
            LevelData.GameManager.AddListener(this);
        }

        public void OnIntro()
        {
            _introComponent = LevelData.IntroObject.GetComponent<Intro>();
            _introComponent.Initialize(LevelData.IntroSprites);
        }

        public void OnFinish()
        {
            Debug.Log("Level complete screen appeared");
        }

        public void SetCurrentLevel(string levelName)
        {
            if (string.IsNullOrWhiteSpace(levelName))
            {
                Debug.LogError("[LevelController] Attempted to set an empty level name.");
                return;
            }

            var canonical = ResolveCanonicalLevelIdentifier(levelName);
            if (string.IsNullOrEmpty(canonical))
            {
                Debug.LogError("[LevelController] Could not resolve level identifier '" + levelName + "'.");
                return;
            }

            SetCurrentLevelInternal(canonical, levelName);
        }

        public void SetCurrentLevel(int locationIndex, string partOfDayKey, int levelOrder = 0)
        {
            var levels = LevelManager.GetLevelsForPartOfDay(locationIndex, partOfDayKey)?.ToList() ?? new List<string>();

            if (levels.Count == 0)
            {
                Debug.LogWarning("[LevelController] No levels found for location " + locationIndex + " and part '" + partOfDayKey + "'.");
                return;
            }

            if (levelOrder < 0)
            {
                levelOrder = 0;
            }

            if (levelOrder >= levels.Count)
            {
                Debug.LogWarning("[LevelController] Requested level index " + levelOrder + " exceeds available levels (" + levels.Count + "). Using last available.");
                levelOrder = levels.Count - 1;
            }

            SetCurrentLevel(levels[levelOrder]);
        }

        private static string ResolveCanonicalLevelIdentifier(string levelIdentifier)
        {
            if (!LevelCatalogService.TryFindLevel(levelIdentifier, out var descriptor))
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(descriptor.Address)
                ? string.Empty
                : descriptor.Address.Trim();
        }

        private static void SetCurrentLevelInternal(string normalizedLevelIdentifier, string sourceIdentifier)
        {
            GameDataManager.PlayerData.CurrentLevel = normalizedLevelIdentifier;

            if (!string.Equals(normalizedLevelIdentifier, sourceIdentifier, StringComparison.Ordinal))
            {
                Debug.Log("Current level set to " + normalizedLevelIdentifier + " (source '" + sourceIdentifier + "').");
            }
            else
            {
                Debug.Log("Current level set to " + normalizedLevelIdentifier);
            }
        }

        [Button]
        public void SkipIntro()
        {
            _introComponent?.SkipIntro();
        }

        [Button]
        public void Replay()
        {
            SceneManager.LoadScene("Game");
        }

        [Button]
        public void PlayNextLevel()
        {
            if (Instance.LevelData.GameManager.State != GameState.FINISHED)
            {
                return;
            }

            var currentLevelKey = GameDataManager.PlayerData.CurrentLevel;

            if (!LevelManager.TryGetNextLevelKey(currentLevelKey, out var nextLevelKey))
            {
                Debug.Log("All levels completed");
                return;
            }

            if (!LevelManager.TryParseLevelNumber(nextLevelKey, out var nextLevelNumber))
            {
                Debug.LogError($"[LevelController] Invalid next level identifier '{nextLevelKey}'.");
                return;
            }

            var totalLevels = LevelManager.GetTotalLevelsCount();
            if (nextLevelNumber > totalLevels)
            {
                Debug.Log("All levels completed");
                return;
            }

            SetCurrentLevel(nextLevelKey);

            SceneManager.LoadScene("Game");
        }

        [Button]
        public void Finish()
        {
            LevelData.GameManager.Finish();
        }

        public async Task LoadLevelData()
        {
            await LevelManager.LoadLevelData();

            IsLevelLoaded = true;
        }

        public async Task<bool> LoadIntroData()
        {
            var hasIntroSprites = await LevelDataProvider.LoadIntroSprites();
            //await LevelDataProvider.LoadSkipButtonSprite();
            return hasIntroSprites;
        }

        private float GetConfiguredTimeScale()
        {
            if (!Mathf.Approximately(_timeScale, DefaultTimeScale))
                return _timeScale;

            var bot = GetBotOrchestrator();
            if (bot != null && bot.IsEnabled)
                return BotEnabledDefaultTimeScale;

            return _timeScale;
        }

        private global::Assets.Scripts.BotV3.BotOrchestrator GetBotOrchestrator()
        {
            if (_botOrchestrator == null)
                _botOrchestrator = FindAnyObjectByType<global::Assets.Scripts.BotV3.BotOrchestrator>(FindObjectsInactive.Include);

            return _botOrchestrator;
        }
    }
}

