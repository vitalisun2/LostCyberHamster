using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts;
using Assets.Scripts.Common;
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
        public int SpawnLookaheadPatterns
        {
            get => _spawnLookaheadPatterns;
            set => _spawnLookaheadPatterns = Mathf.Max(_defaultSpawnLookaheadPatterns, value);
        }

        public event Action<int, string> PatternSpawned;

        private const int _defaultSpawnLookaheadPatterns = 1;
        private readonly float _delayBetweenPatterns = 2.0f;
        private float _timeSinceLastPattern;
        private int _reliefDelayPatternIndex = -1;
        private int _currentPatternIndex;
        private int _spawnLookaheadPatterns = _defaultSpawnLookaheadPatterns;
        private List<InstantiatedObstacle> _spawnedObstacles = new();
        private EnvironmentRoot _environmentRoot;
        private List<InstantiatedObstacle> _intantiatedObstacles = new();
        private readonly string _reliefPatternName = "relief";
        private float ScreenRightEdge =>
            Camera.main.transform.position.x +
            Camera.main.orthographicSize * Camera.main.aspect;

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
            var patterns = LevelController.Instance.LevelData.LevelInfo.patterns;

            while (_currentPatternIndex < patterns.Count && IsCurrentPatternReadyToSpawn())
            {
                CurrPatternName = patterns[_currentPatternIndex].name;
                bool needsDelay = CurrPatternName == _reliefPatternName;

                if (needsDelay && _reliefDelayPatternIndex != _currentPatternIndex)
                {
                    _reliefDelayPatternIndex = _currentPatternIndex;
                    _timeSinceLastPattern = 0f;
                    break;
                }

                if (needsDelay && _timeSinceLastPattern < _delayBetweenPatterns)
                    break;

                SpawnPattern(_currentPatternIndex);
                _currentPatternIndex++;
                _reliefDelayPatternIndex = -1;
                _timeSinceLastPattern = 0f;
            }

            // завершаем, когда все паттерны отспавнены и сцена пуста
            if (_currentPatternIndex >= patterns.Count
                && !_spawnedObstacles.Any()
                && _timeSinceLastPattern >= _delayBetweenPatterns)
                LevelController.Instance.LevelData.GameManager.Finish();
        }

        // Проверяет gate-паттерн для текущего lookahead-окна.
        private bool IsCurrentPatternReadyToSpawn()
        {
            int effectiveLookaheadPatterns = GetEffectiveSpawnLookaheadPatterns(_currentPatternIndex);
            if (_currentPatternIndex < effectiveLookaheadPatterns)
                return true;

            int gatePatternIndex = _currentPatternIndex - effectiveLookaheadPatterns;
            return IsPatternFullyOnScreen(gatePatternIndex);
        }

        // Empty delay patterns must keep the player-facing spacing from the default spawn mode.
        // Bot lookahead may preload real patterns, but it must not start spacer delay earlier.
        private int GetEffectiveSpawnLookaheadPatterns(int patternIndex)
        {
            var patterns = LevelController.Instance.LevelData.LevelInfo.patterns;
            if (patternIndex >= 0
                && patternIndex < patterns.Count
                && IsDelaySpacerPattern(patterns[patternIndex].name))
            {
                return _defaultSpawnLookaheadPatterns;
            }

            return _spawnLookaheadPatterns;
        }

        private bool IsDelaySpacerPattern(string patternName)
        {
            return string.Equals(patternName, _reliefPatternName, StringComparison.Ordinal);
        }

        // проверяем, что правый край паттерна не дальше правого края экрана
        private bool IsPatternFullyOnScreen(int patternIndex)
        {
            var pattern = _spawnedObstacles.Where(o => o.PatternIndex == patternIndex).ToList();
            if (!pattern.Any()) return true;                      // весь паттерн уже despawn’ился

            return GetPatternRightEdge(pattern) <= ScreenRightEdge;
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

            // динамический оффсет, левый край на нужном расстоянии от предыдущего паттерна
            float patternLeftEdge = GetPatternLeftEdgeAtSpawnPosition(patternObstacles);
            float offset = GetNextPatternTargetLeftEdge() - patternLeftEdge;

            foreach (var obstacle in patternObstacles)
            {
                var pos = obstacle.SpawnPosition;
                pos.x += offset;
                obstacle.ObstacleScript.transform.position = pos;
                obstacle.ObstacleScript.transform.SetParent(_environmentRoot.ObstaclesSpawnedContainer);
                obstacle.ObstacleScript.InitializeMechanics();
                _spawnedObstacles.Add(obstacle);
            }

            PatternSpawned?.Invoke(patternIndex, CurrPatternName);
        }

        /// <summary>
        /// Возвращает целевую позицию левого края следующего паттерна.
        /// </summary>
        private float GetNextPatternTargetLeftEdge()
        {
            int prevIndex = _currentPatternIndex - 1;
            var prev = _spawnedObstacles.Where(o => o.PatternIndex == prevIndex).ToList();
            if (!prev.Any())
                return ScreenRightEdge + Consts.PatternEdgeGap;

            return GetPatternRightEdge(prev) + Consts.PatternEdgeGap;
        }

        /// <summary>
        /// Возвращает левый край паттерна в исходных позициях из LevelInfo.
        /// </summary>
        private static float GetPatternLeftEdgeAtSpawnPosition(List<InstantiatedObstacle> obstacles)
        {
            return obstacles.Min(GetObstacleLeftEdgeAtSpawnPosition);
        }

        /// <summary>
        /// Возвращает текущий правый край паттерна.
        /// </summary>
        private static float GetPatternRightEdge(List<InstantiatedObstacle> obstacles)
        {
            return obstacles.Max(GetObstacleRightEdge);
        }

        /// <summary>
        /// Возвращает левый край obstacle в его исходной позиции из LevelInfo.
        /// </summary>
        private static float GetObstacleLeftEdgeAtSpawnPosition(InstantiatedObstacle obstacle)
        {
            CollisionUtils.GetObstacleXIntervalAtPosition(
                obstacle.ObstacleScript,
                obstacle.SpawnPosition,
                out float left,
                out _);

            return left;
        }

        /// <summary>
        /// Возвращает текущий правый край obstacle по его collider bounds.
        /// </summary>
        private static float GetObstacleRightEdge(InstantiatedObstacle obstacle)
        {
            CollisionUtils.GetObstacleXInterval(
                obstacle.ObstacleScript,
                obstacle.ObstacleScript.ColliderWidth,
                0f,
                out _,
                out float right);

            return right;
        }
    }

}
