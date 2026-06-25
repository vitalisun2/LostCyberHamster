using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts;
using Assets.Scripts.Common;
using Assets.Scripts.Entry_Points.GameLoadingTasks;
using Assets.Scripts.GameEngine.Mechanics;
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
        private const float _patternEdgeTolerance = 0.01f;
        private readonly float _delayBetweenPatterns = 2.0f;
        private float _timeSinceLastPattern;
        private int _reliefDelayPatternIndex = -1;
        private int _currentPatternIndex;
        private int _spawnLookaheadPatterns = _defaultSpawnLookaheadPatterns;
        private List<InstantiatedObstacle> _spawnedObstacles = new();
        private readonly List<SpawnedPatternXRange> _spawnedPatternRanges = new();
        private EnvironmentRoot _environmentRoot;
        private List<InstantiatedObstacle> _intantiatedObstacles = new();
        private const string _reliefPatternName = "test_relief";
        private float ScreenLeftEdge =>
            Camera.main.transform.position.x -
            Camera.main.orthographicSize * Camera.main.aspect;

        private float ScreenRightEdge =>
            Camera.main.transform.position.x +
            Camera.main.orthographicSize * Camera.main.aspect;

        /// <summary>
        /// Возвращает индекс видимого паттерна, который сейчас является актуальным для игрока.
        /// </summary>
        public int GetCurrentVisiblePatternIndex(float leftX, float rightX)
        {
            if (_spawnedPatternRanges.Count == 0)
            {
                return -1;
            }

            if (leftX > rightX)
            {
                (leftX, rightX) = (rightX, leftX);
            }

            int overlappingPatternIndex = -1;
            float overlappingPatternLeftEdge = float.PositiveInfinity;
            int upcomingPatternIndex = -1;
            float upcomingPatternLeftEdge = float.PositiveInfinity;
            int trailingPatternIndex = -1;
            float trailingPatternRightEdge = float.NegativeInfinity;
            float screenLeftEdge = ScreenLeftEdge;
            float screenRightEdge = ScreenRightEdge;

            PruneSpawnedPatternRanges(screenLeftEdge);

            foreach (var patternRange in _spawnedPatternRanges)
            {
                int patternIndex = patternRange.PatternIndex;
                float patternLeftEdge = patternRange.LeftEdge;
                float patternRightEdge = patternRange.RightEdge;

                bool isVisible =
                    patternLeftEdge <= screenRightEdge + _patternEdgeTolerance &&
                    patternRightEdge >= screenLeftEdge - _patternEdgeTolerance;

                if (!isVisible)
                {
                    continue;
                }

                bool overlapsPlayerRange =
                    patternLeftEdge <= rightX + _patternEdgeTolerance &&
                    patternRightEdge >= leftX - _patternEdgeTolerance;

                if (overlapsPlayerRange)
                {
                    if (patternLeftEdge < overlappingPatternLeftEdge)
                    {
                        overlappingPatternLeftEdge = patternLeftEdge;
                        overlappingPatternIndex = patternIndex;
                    }

                    continue;
                }

                bool isNotPassedByPlayer = patternRightEdge >= leftX - _patternEdgeTolerance;
                if (isNotPassedByPlayer)
                {
                    if (patternLeftEdge < upcomingPatternLeftEdge)
                    {
                        upcomingPatternLeftEdge = patternLeftEdge;
                        upcomingPatternIndex = patternIndex;
                    }

                    continue;
                }

                if (patternRightEdge > trailingPatternRightEdge)
                {
                    trailingPatternRightEdge = patternRightEdge;
                    trailingPatternIndex = patternIndex;
                }
            }

            if (overlappingPatternIndex >= 0)
            {
                return overlappingPatternIndex;
            }

            if (upcomingPatternIndex >= 0)
            {
                return upcomingPatternIndex;
            }

            return trailingPatternIndex;
        }

        private void RegisterSpawnedPatternRange(int patternIndex, float leftEdge, float rightEdge)
        {
            if (leftEdge > rightEdge)
            {
                (leftEdge, rightEdge) = (rightEdge, leftEdge);
            }

            _spawnedPatternRanges.Add(new SpawnedPatternXRange(patternIndex, leftEdge, rightEdge));
        }

        private void UpdateSpawnedPatternRanges(float deltaTime)
        {
            if (_spawnedPatternRanges.Count == 0)
            {
                return;
            }

            float shift = Consts.RoadScrollSpeed * ScrollLeftMechanics.SpeedMultiplier * deltaTime;
            for (int i = 0; i < _spawnedPatternRanges.Count; i++)
            {
                _spawnedPatternRanges[i].ShiftLeft(shift);
            }

            PruneSpawnedPatternRanges(ScreenLeftEdge);
        }

        private void PruneSpawnedPatternRanges(float screenLeftEdge)
        {
            _spawnedPatternRanges.RemoveAll(range =>
                range.RightEdge < screenLeftEdge - _patternEdgeTolerance);
        }

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
            UpdateSpawnedPatternRanges(deltaTime);
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
            float targetLeftEdge = GetNextPatternTargetLeftEdge();
            float offset = targetLeftEdge - patternLeftEdge;
            float targetRightEdge = GetPatternRightEdgeAtSpawnPosition(patternObstacles) + offset + Consts.PatternEdgeGap;

            foreach (var obstacle in patternObstacles)
            {
                var pos = obstacle.SpawnPosition;
                pos.x += offset;
                obstacle.ObstacleScript.transform.position = pos;
                obstacle.ObstacleScript.transform.SetParent(_environmentRoot.ObstaclesSpawnedContainer);
                obstacle.ObstacleScript.InitializeMechanics();
                _spawnedObstacles.Add(obstacle);
            }

            RegisterSpawnedPatternRange(patternIndex, targetLeftEdge, targetRightEdge);
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
        /// Возвращает правый край паттерна в исходных позициях из LevelInfo.
        /// </summary>
        private static float GetPatternRightEdgeAtSpawnPosition(List<InstantiatedObstacle> obstacles)
        {
            return obstacles.Max(GetObstacleRightEdgeAtSpawnPosition);
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
        /// Возвращает правый край obstacle в его исходной позиции из LevelInfo.
        /// </summary>
        private static float GetObstacleRightEdgeAtSpawnPosition(InstantiatedObstacle obstacle)
        {
            CollisionUtils.GetObstacleXIntervalAtPosition(
                obstacle.ObstacleScript,
                obstacle.SpawnPosition,
                out _,
                out float right);

            return right;
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

        private sealed class SpawnedPatternXRange
        {
            public SpawnedPatternXRange(int patternIndex, float leftEdge, float rightEdge)
            {
                PatternIndex = patternIndex;
                LeftEdge = leftEdge;
                RightEdge = rightEdge;
            }

            public int PatternIndex { get; }
            public float LeftEdge { get; private set; }
            public float RightEdge { get; private set; }

            public void ShiftLeft(float shift)
            {
                LeftEdge -= shift;
                RightEdge -= shift;
            }
        }
    }

}
