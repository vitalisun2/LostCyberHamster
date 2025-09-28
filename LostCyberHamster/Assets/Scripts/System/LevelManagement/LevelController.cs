using Assets.Scripts.GameManagerLogic;
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using GameManagement;
using Assets.Scripts;
using Assets.Scripts.System.FeatureFlags;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace Assets.Scripts.System
{
    public class LevelController : MonoBehaviour,
        Listeners.IGameIntroListener,
        Listeners.IGameFinishListener
    {
        [SerializeField]
        [Tooltip("DEV: переключает иерархическую схему выбора уровней по времени суток.")]
        private bool useDayPartLevelSelection = false;

        [SerializeField]
        [Range(0.1f, 2.0f)] // Настройка диапазона для удобства в инспекторе
        private float timeScale = 1.0f; // Скорость рантайма

        public LevelData LevelData { get; private set; } = new();
        public bool IsLevelLoaded { get; private set; }

        public static LevelController Instance { get; private set; }

        private Intro _introComponent;

        private async void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            LevelCatalogRuntimeConfigurator.SetInspectorOverride(useDayPartLevelSelection);

            // Устанавливаем начальную скорость игры
            Time.timeScale = 1f;
        }

        private void Update()
        {
            // гард: ждём, пока GameManager будет инициализирован
            if (Instance?.LevelData?.GameManager == null) return;

            // Обновляем скорость игры, если изменено значение в инспекторе
            var newTs = timeScale * Instance.LevelData.GameManager.TimeScaleCoefficient;
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

            var normalized = NormalizeLevelIdentifier(levelName);
            if (string.IsNullOrEmpty(normalized))
            {
                Debug.LogError("[LevelController] Could not normalize level identifier '" + levelName + "'.");
                return;
            }

            GameDataManager.PlayerData.CurrentLevel = normalized;

            if (!string.Equals(normalized, levelName, StringComparison.Ordinal))
            {
                Debug.Log("Current level set to " + normalized + " (source '" + levelName + "').");
            }
            else
            {
                Debug.Log("Current level set to " + normalized);
            }
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

        private static string NormalizeLevelIdentifier(string levelIdentifier)
        {
            if (string.IsNullOrWhiteSpace(levelIdentifier))
            {
                return string.Empty;
            }

            var trimmed = levelIdentifier.Trim();
            var fileName = Path.GetFileNameWithoutExtension(trimmed);
            if (!string.IsNullOrEmpty(fileName))
            {
                trimmed = fileName;
            }

            if (trimmed.Contains("/"))
            {
                trimmed = trimmed.Split('/').LastOrDefault() ?? trimmed;
            }

            return trimmed;
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

        public async Task LoadIntroData()
        {
            await LevelDataProvider.LoadIntroSprites();
            //await LevelDataProvider.LoadSkipButtonSprite();
        }

    }
}

