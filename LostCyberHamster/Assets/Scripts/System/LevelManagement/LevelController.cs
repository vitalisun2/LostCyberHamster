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
        private const float BotEnabledDefaultTimeScale = 1.0f;

        [SerializeField]
        [FormerlySerializedAs("timeScale")]
        [Range(0.1f, 4.0f)] // Настройка диапазона для удобства в инспекторе
        private float _timeScale = DefaultTimeScale; // Скорость рантайма

        // True если timescale был инициализирован из PlayerPrefs override при старте play mode.
        // В этом случае bot-detection не применяется: _timeScale отражает явное
        // намерение лаунчера, и пользователь может менять его через инспектор напрямую.
        private bool _launcherOverrideConsumed;

        public LevelData LevelData { get; private set; } = new();
        public bool IsLevelLoaded { get; private set; }

        public static LevelController Instance { get; private set; }

        private Intro _introComponent;
        private global::Assets.Scripts.Bot.RuntimeBotController _botController;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Если TestLevelLauncher выставил override через PlayerPrefs —
            // применяем его к _timeScale и сразу удаляем ключ.
            // Флаг _launcherOverrideConsumed позволяет GetConfiguredTimeScale
            // пропустить bot-detection и давать пользователю менять _timeScale в инспекторе.
            if (AutomationRuntimePrefs.TryGetTimeScaleOverride(out float overrideTs))
            {
                _timeScale = Mathf.Clamp(overrideTs, 0.1f, 4.0f);
                _launcherOverrideConsumed = true;
                PlayerPrefs.DeleteKey(AutomationRuntimePrefs.TimeScaleOverrideKey);
                PlayerPrefs.Save();
            }

            // Устанавливаем начальную скорость игры
            Time.timeScale = _timeScale;
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
           }
            else
            {
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
            // Если при старте был применён override из TestLevelLauncher, _timeScale уже
            // проинициализирован нужным значением и PlayerPrefs-ключ удалён.
            // Просто возвращаем _timeScale — это позволяет пользователю менять его
            // через инспектор в любую сторону без ограничений.
            if (_launcherOverrideConsumed)
                return _timeScale;

            // Fallback для запуска без TestLevelLauncher (напрямую через Play):
            // если пользователь явно изменил поле — уважаем это значение.
            if (!Mathf.Approximately(_timeScale, DefaultTimeScale))
                return _timeScale;

            // Авто-спидап при включённом боте (только без явного override).
            var bot = GetBotController();
            if (bot != null && bot.IsEnabled)
                return BotEnabledDefaultTimeScale;

            return _timeScale;
        }

        private global::Assets.Scripts.Bot.RuntimeBotController GetBotController()
        {
            if (_botController == null)
                _botController = FindAnyObjectByType<global::Assets.Scripts.Bot.RuntimeBotController>(FindObjectsInactive.Include);

            return _botController;
        }
    }
}

