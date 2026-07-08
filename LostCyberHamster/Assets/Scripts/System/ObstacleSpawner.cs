using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts;
using Assets.Scripts.Common;
using Assets.Scripts.Diagnostics;
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
        public VisiblePatternTracker VisiblePatternTracker => _visiblePatternTracker;
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
        private readonly VisiblePatternTracker _visiblePatternTracker = new();
        private EnvironmentRoot _environmentRoot;
        private List<InstantiatedObstacle> _intantiatedObstacles = new();
        private const string _reliefPatternName = "test_relief";

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

            _visiblePatternTracker.Clear();
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
            _visiblePatternTracker.Update(deltaTime);
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
            long allocationSample = RuntimePerformanceDiagnostics.BeginAllocationSample(
                RuntimePerformanceScope.ObstacleSpawnerSpawnPatterns);
            _timeSinceLastPattern += Time.deltaTime;
            var patterns = LevelController.Instance.LevelData.LevelInfo.patterns;

            while (_currentPatternIndex < patterns.Count && IsCurrentPatternReadyToSpawn())
            {
                CurrPatternName = patterns[_currentPatternIndex].name;
                bool needsDelay = IsDelaySpacerPattern(CurrPatternName);

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

            RuntimePerformanceDiagnostics.EndAllocationSample(
                RuntimePerformanceScope.ObstacleSpawnerSpawnPatterns,
                allocationSample);
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
            return string.Equals(patternName, _reliefPatternName, StringComparison.OrdinalIgnoreCase);
        }

        // проверяем, что правый край паттерна не дальше правого края экрана
        private bool IsPatternFullyOnScreen(int patternIndex)
        {
            long allocationSample = RuntimePerformanceDiagnostics.BeginAllocationSample(
                RuntimePerformanceScope.ObstacleSpawnerIsPatternFullyOnScreen);
            bool hasPatternObstacles = false;
            float patternRightEdge = float.NegativeInfinity;

            for (int obstacleIndex = 0; obstacleIndex < _spawnedObstacles.Count; obstacleIndex++)
            {
                InstantiatedObstacle obstacle = _spawnedObstacles[obstacleIndex];
                if (obstacle.PatternIndex != patternIndex)
                    continue;

                hasPatternObstacles = true;
                float obstacleRightEdge = GetObstacleRightEdge(obstacle);
                if (obstacleRightEdge > patternRightEdge)
                    patternRightEdge = obstacleRightEdge;
            }

            bool result = !hasPatternObstacles ||                       // весь паттерн уже despawn’ился
                          patternRightEdge <= ScreenRightEdge;

            RuntimePerformanceDiagnostics.EndAllocationSample(
                RuntimePerformanceScope.ObstacleSpawnerIsPatternFullyOnScreen,
                allocationSample);

            return result;
        }

        // ---------- UNSPAWN ----------
        private void UnspawnObstacle(GameObject obstacle)
        {
            long allocationSample = RuntimePerformanceDiagnostics.BeginAllocationSample(
                RuntimePerformanceScope.ObstacleSpawnerUnspawnObstacle);
            var inst = _spawnedObstacles
                .FirstOrDefault(x => x.ObstacleScript.gameObject == obstacle);
            if (inst == null)
            {
                RuntimePerformanceDiagnostics.EndAllocationSample(
                    RuntimePerformanceScope.ObstacleSpawnerUnspawnObstacle,
                    allocationSample);
                return;
            }

            inst.ObstacleScript.gameObject.SetActive(false);
            inst.ObstacleScript.transform.SetParent(_environmentRoot.ObstaclesPool);
            _spawnedObstacles.Remove(inst);
            RuntimePerformanceDiagnostics.EndAllocationSample(
                RuntimePerformanceScope.ObstacleSpawnerUnspawnObstacle,
                allocationSample);
        }

        // ---------- SPAWN ОДНОГО ПАТТЕРНА ----------
        private void SpawnPattern(int patternIndex)
        {
            long allocationSample = RuntimePerformanceDiagnostics.BeginAllocationSample(
                RuntimePerformanceScope.ObstacleSpawnerSpawnPattern);
            var patternObstacles = _intantiatedObstacles
                .Where(o => o.PatternIndex == patternIndex)
                .ToList();
            if (!patternObstacles.Any())
            {
                RuntimePerformanceDiagnostics.EndAllocationSample(
                    RuntimePerformanceScope.ObstacleSpawnerSpawnPattern,
                    allocationSample);
                return;
            }

            // динамический оффсет, левый край на нужном расстоянии от предыдущего паттерна
            float patternLeftEdge = GetPatternLeftEdgeAtSpawnPosition(patternObstacles);
            float targetLeftEdge = GetNextPatternTargetLeftEdge();
            float offset = targetLeftEdge - patternLeftEdge;

            foreach (var obstacle in patternObstacles)
            {
                var pos = obstacle.SpawnPosition;
                pos.x += offset;
                obstacle.ObstacleScript.transform.position = pos;
                obstacle.ObstacleScript.transform.SetParent(_environmentRoot.ObstaclesSpawnedContainer);
                obstacle.ObstacleScript.gameObject.SetActive(true);
                obstacle.ObstacleScript.InitializeMechanics();
                _spawnedObstacles.Add(obstacle);
            }

            _visiblePatternTracker.RegisterPattern(patternIndex, patternObstacles, offset);
            PatternSpawned?.Invoke(patternIndex, CurrPatternName);
            RuntimePerformanceDiagnostics.EndAllocationSample(
                RuntimePerformanceScope.ObstacleSpawnerSpawnPattern,
                allocationSample);
        }

        /// <summary>
        /// Возвращает целевую позицию левого края следующего паттерна.
        /// </summary>
        private float GetNextPatternTargetLeftEdge()
        {
            long allocationSample = RuntimePerformanceDiagnostics.BeginAllocationSample(
                RuntimePerformanceScope.ObstacleSpawnerGetNextPatternTargetLeftEdge);
            int prevIndex = _currentPatternIndex - 1;
            var prev = _spawnedObstacles.Where(o => o.PatternIndex == prevIndex).ToList();
            if (!prev.Any())
            {
                RuntimePerformanceDiagnostics.EndAllocationSample(
                    RuntimePerformanceScope.ObstacleSpawnerGetNextPatternTargetLeftEdge,
                    allocationSample);
                return ScreenRightEdge + Consts.PatternEdgeGap;
            }

            float result = GetPatternRightEdge(prev) + Consts.PatternEdgeGap;
            RuntimePerformanceDiagnostics.EndAllocationSample(
                RuntimePerformanceScope.ObstacleSpawnerGetNextPatternTargetLeftEdge,
                allocationSample);

            return result;
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

        /// <summary>
        /// Возвращает текущий левый край obstacle по его collider bounds.
        /// </summary>
        private static float GetObstacleLeftEdge(InstantiatedObstacle obstacle)
        {
            CollisionUtils.GetObstacleXInterval(
                obstacle.ObstacleScript,
                obstacle.ObstacleScript.ColliderWidth,
                0f,
                out float left,
                out _);

            return left;
        }
    }

}
