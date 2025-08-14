using Assets.Scripts.GameManagerLogic;
using Sirenix.OdinInspector;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using GameManagement;

namespace Assets.Scripts.System
{
    public class LevelController : MonoBehaviour,
        Listeners.IGameIntroListener,
        Listeners.IGameFinishListener
    {
        [SerializeField]
        [Range(0.1f, 2.0f)] // Настройка диапазона для удобства в инспекторе
        private float timeScale = 1.0f; // Скорость рантайма

        public LevelData LevelData { get; private set; } = new();
        public bool IsLevelLoaded { get; private set; }

        public static LevelController Instance { get; private set; }

        private Intro _introComponent;

        private async void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }

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
            GameDataManager.PlayerData.CurrentLevel = levelName;

            Debug.Log($"Current level set to {levelName}");
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
                return;

            var currentLevelNumber = LevelManager.GetCurrentLevelNumber();

            if (currentLevelNumber == -1)
            {
                Debug.LogError("Invalid level number");
                return;
            }

            var nextLevelNumber = currentLevelNumber + 1;

            if (nextLevelNumber > LevelManager.LocationInfoList.locations.Length * 4)
            {
                Debug.Log("All levels completed");
                return;
            }

            var nextLevelName = $"level_{nextLevelNumber:D2}";

            SetCurrentLevel(nextLevelName);

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
