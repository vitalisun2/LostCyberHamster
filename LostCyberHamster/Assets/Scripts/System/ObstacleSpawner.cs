using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Entry_Points.GameLoadingTasks;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Installers.Roots;
using Assets.Scripts.Gameplay;
using UnityEngine;

namespace Assets.Scripts.System
{
    public class ObstacleSpawner : MonoBehaviour,
    Listeners.IGameStartListener,
    Listeners.IGameUpdateListener,
    Listeners.IGamePauseListener,
    Listeners.IGameResumeListener,
    Listeners.IGameFinishListener
    {
        public static ObstacleSpawner Instance { get; private set; }

        public List<InstantiatedObstacle> SpawnedObstacles => _spawnedObstacles;
        public string CurrPatternName { get; private set; }
        public int CurrPatternIndex => _currentPatternIndex;

        private readonly float _delayBetweenPatterns = 2.0f;
        private float _timeSinceLastPattern;
        private int _reliefDelayPatternIndex = -1;
        private int _currentPatternIndex;
        private List<InstantiatedObstacle> _spawnedObstacles = new();
        private EnvironmentRoot _environmentRoot;
        private List<InstantiatedObstacle> _intantiatedObstacles = new();
        private readonly string _reliefPatternName = "relief";
        private float ScreenRightEdge =>
            Camera.main.transform.position.x +
            Camera.main.orthographicSize * Camera.main.aspect;

        private readonly float _spawnPadding = 5f;           // зазор за экраном

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void Init(EnvironmentRoot environmentRoot)
        {
            LevelController.Instance.LevelData.GameManager.AddListener(this);

            _environmentRoot = environmentRoot;
            _intantiatedObstacles = ObstacleFactory.CreateObstacles(_environmentRoot);

            foreach (var obstacle in _intantiatedObstacles)
            {
                obstacle.ObstacleScript.OnObstacleUnspawned.Subscribe(UnspawnObstacle);
            }
        }

        public void OnStart()
        {
            enabled = true;
        }

        public void OnUpdate(float deltaTime)
        {
            if (!LevelController.Instance.IsLevelLoaded)
            {
                return;
            }

            SpawnPatterns();
        }

        public void OnPause()
        {
            enabled = false;
        }

        public void OnResume()
        {
           enabled = true;
        }

        public void OnFinish()
        {
            enabled = false;
        }

        // ---------- ГЛАВНАЯ ЛОГИКА SPAWN ----------
        private void SpawnPatterns()
        {
            _timeSinceLastPattern += Time.deltaTime;

            // завершаем, когда все паттерны отспавнены и сцена пуста
            if (_currentPatternIndex >= LevelController.Instance.LevelData.LevelInfo.patterns.Count)
            {
                if (!_spawnedObstacles.Any() && _timeSinceLastPattern >= _delayBetweenPatterns)
                    LevelController.Instance.LevelData.GameManager.Finish();
                return;
            }

            CurrPatternName =
                LevelController.Instance.LevelData.LevelInfo.patterns[_currentPatternIndex].name;
            bool needsDelay = CurrPatternName == _reliefPatternName;

            // готовность зависит от того, полностью ли предыдущий паттерн в кадре
            bool readyToSpawn = _currentPatternIndex == 0 || IsPreviousPatternFullyOnScreen();

            if (readyToSpawn && needsDelay && _reliefDelayPatternIndex != _currentPatternIndex)
            {
                _reliefDelayPatternIndex = _currentPatternIndex;
                _timeSinceLastPattern = 0f;
            }

            if (readyToSpawn && (!needsDelay || _timeSinceLastPattern >= _delayBetweenPatterns))
            {
                SpawnPattern(_currentPatternIndex);
                _currentPatternIndex++;
                _reliefDelayPatternIndex = -1;
                _timeSinceLastPattern = 0f;
            }
        }

        // проверяем, что самый правый объект предыдущего паттерна не дальше правого края
        private bool IsPreviousPatternFullyOnScreen()
        {
            int prevIndex = _currentPatternIndex - 1;
            var prev = _spawnedObstacles.Where(o => o.PatternIndex == prevIndex).ToList();
            if (!prev.Any()) return true;                      // весь паттерн уже despawn’ился

            float maxX = prev.Max(o => o.ObstacleScript.transform.position.x);
            return maxX <= ScreenRightEdge;
        }

        // ---------- UNSPAWN ----------
        private void UnspawnObstacle(GameObject obstacle)
        {
            var inst = _spawnedObstacles
                .FirstOrDefault(x => x.ObstacleScript.gameObject == obstacle);
            if (inst == null) return;

            inst.ObstacleScript.transform.SetParent(_environmentRoot.ObstaclesPool);
            _spawnedObstacles.Remove(inst);
        }

        // ---------- SPAWN ОДНОГО ПАТТЕРНА ----------
        private void SpawnPattern(int patternIndex)
        {
            var patternObstacles = _intantiatedObstacles
                .Where(o => o.PatternIndex == patternIndex)
                .ToList();
            if (!patternObstacles.Any()) return;

            // динамический оффсет, левый край сразу за правым краем экрана
            float patternMinX = patternObstacles.Min(o => o.SpawnPosition.x);
            float offset = ScreenRightEdge + _spawnPadding - patternMinX;

            foreach (var obstacle in patternObstacles)
            {
                var pos = obstacle.SpawnPosition;
                pos.x += offset;
                obstacle.ObstacleScript.transform.position = pos;
                obstacle.ObstacleScript.transform.SetParent(_environmentRoot.ObstaclesSpawnedContainer);
                obstacle.ObstacleScript.InitializeMechanics();
                _spawnedObstacles.Add(obstacle);
            }
        }
    }

}
