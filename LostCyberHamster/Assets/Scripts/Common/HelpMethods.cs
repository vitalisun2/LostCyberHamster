using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Common.Models;
using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Installers.Roots;
using GameManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using Object = UnityEngine.Object;

namespace Assets.Scripts.Common
{
    public static class HelpMethods
    {
#if UNITY_EDITOR
        private static int _worldShiftClipCalls;
        private static float _worldShiftClipTotalMs;
        private static float _worldShiftClipMaxMs;
        private static string _worldShiftClipMaxName;
#endif

        public static bool IsOnSameLine(bool isHamsterOnBottomLine, Obstacle obstacle)
        {
            bool isTopObstacle = obstacle.ObstacleType.IsTop;
            bool isHamsterOnTopLine = !isHamsterOnBottomLine;

            return (isTopObstacle && isHamsterOnTopLine) ||
                   (!isTopObstacle && !isHamsterOnTopLine);
        }

        public static void ApplyOverrideController(Hamster hamster)
        {
            var spriteAnimatorController = hamster.GetComponentInChildren<SpriteAnimatorController>();
            var animator = spriteAnimatorController.gameObject.GetComponent<Animator>();

            if (SkinManager.CurrentSkin.HamsterOverrideController == null)
            {
                Debug.LogWarning("Hamster override controller is null, default skin applied");
                GameDataManager.PlayerData.AppliedSkinId = 0;
            }

            animator.runtimeAnimatorController = SkinManager.CurrentSkin.HamsterOverrideController;
            animator.enabled = true;
        }

        public static GameObject CreateUltaEffect(GameObject ultaPrefab, Hamster hamster)
        {
            GameObject ultaEffect = null;

            if (ultaPrefab != null)
            {
                ultaEffect = GameObject.Instantiate(ultaPrefab, hamster.EffectsSlot);
            }

            return ultaEffect;
        }

        /// <summary>
        /// Gets the list of level names for a specified location number.
        /// Each location has 4 levels in the sequence: morning, afternoon, evening, and night.
        /// The level names follow the format "level_XX" where "XX" is the sequential level number.
        /// </summary>
        /// <param name="locationNumber">The number of the location (starting from 1).</param>
        /// <returns>A list of level names for the specified location.</returns>
        /// <exception cref="ArgumentException">Thrown when the location number is less than or equal to zero.</exception>
        public static IEnumerable<string> GetLevelNames(int locationNumber)
        {
            if (locationNumber <= 0)
            {
                throw new ArgumentException("Location number must be a positive integer.");
            }

            int levelsPerLocation = 4;

            int startLevel = (locationNumber - 1) * levelsPerLocation + 1;

            return Enumerable.Range(startLevel, levelsPerLocation)
                .Select(level => $"level_{level:D2}");
        }

        /// <summary>
        /// Logs an error and stops the game. In Editor: exits Play Mode. In a build: quits the application.
        /// </summary>
        public static void LogAndStopGame(string message)
        {
            Debug.LogError(message);
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
        }

        /// <summary>
        /// Ищет roof-препятствие (bigNotAlive или mediumNotAlive) «под хомяком»,
        /// если тот стоит на верхней или нижней линии в соответствующей координате X.
        /// Возвращает <see cref="Obstacle"/> или null, если препятствие не найдено.
        /// </summary>
        /// <param name="hamsterTransform">Transform хомяка.</param>
        /// <param name="environmentRoot">Ссылка на EnvironmentRoot, чтобы получить препятствия.</param>
        /// <param name="isOnBottomLine">Признак того, что хомяк находится на нижней линии.</param>
        /// <returns>Найденное препятствие или null.</returns>
        public static Obstacle FindBigNotAliveUnderHamster(
            Transform hamsterTransform,
            EnvironmentRoot environmentRoot,
            bool isOnBottomLine)
        {
            var hamsterX = hamsterTransform.position.x;

            // Берём все препятствия, которые уже "spawned"
            var obstacles = environmentRoot.ObstaclesSpawnedContainer
                .GetComponentsInChildren<Obstacle>();

            foreach (var obstacle in obstacles)
            {
                // Нас интересуют только roof-препятствия (bigNotAlive, mediumNotAlive)
                if (!CollisionUtils.IsRoofObstacle(obstacle.ObstacleType.ObstacleTypeEnum))
                    continue;

                // Проверяем, совпадает ли линия (верх/низ) с нашей позицией
                if (!IsOnSameLine(isOnBottomLine, obstacle))
                    continue;

                // Смотрим, пересекается ли хомяк по X с этим препятствием
                float distX = Mathf.Abs(hamsterX - obstacle.transform.position.x);

                // Используем фактическую ширину коллайдера вместо хардкода
                float extendedEdge = obstacle.ColliderWidth / 2 + Consts.BigNotAliveEdgeTolerance;
                if (distX <= extendedEdge)
                {
                    return obstacle;
                }
            }

            // Если подходящего roof-препятствия не нашли, возвращаем null
            return null;
        }

        /// <summary>
        /// Преобразует ширину в пикселях в ширину в Unity-юнитах,
        /// используя фиксированный коэффициент PIXELS_TO_UNITS_RATIO.
        /// </summary>
        /// <param name="pixelWidth">Ширина объекта в пикселях (как в оригинальном спрайте).</param>
        /// <returns>Ширина объекта в юнитах Unity.</returns>
        public static float FromPixelsToUnitsWidth(int pixelWidth)
        {
            return pixelWidth * Consts.PIXELS_TO_UNITS_RATIO;
        }

        /// <summary>
        /// Вычисляет, на какое расстояние сместится мир 
        /// при заданной длительности анимации (в кадрах).
        /// По умолчанию считается, что FPS = 60.
        /// Скорость берём из Consts.GameSpeed.
        /// </summary>
        /// <param name="frames">Длительность анимации в кадрах (напр. 78).</param>
        /// <param name="fps">Частота кадров (обычно 60).</param>
        /// <returns>Расстояние в юнитах, на которое сместится мир.</returns>
        private static float CalculateWorldShiftDistance(int frames, int fps = Consts.FPS)
        {
            float timeSec = frames / (float)fps;
            // Используем константу скорости:
            return Consts.GameSpeedBase * timeSec;
        }

        /// <summary>
        /// Возвращает горизонтальное смещение мира за время клипа <paramref name="clipName"/>.
        /// </summary>
        public static float GetWorldShiftForClip(TransformAnimatorController animController, string clipName)
        {
#if UNITY_EDITOR
            long diagnosticsStartTimestamp = global::System.Diagnostics.Stopwatch.GetTimestamp();
#endif
            float worldShift;
            AnimationClip clip = FindClipByName(animController, clipName);
            if (clip != null)
            {
                int clipFrames = Mathf.RoundToInt(clip.frameRate * clip.length);
                worldShift = CalculateWorldShiftDistance(clipFrames);
            }
            else
            {
                int frames = animController.GetClipFrameCount(clipName);
                worldShift = CalculateWorldShiftDistance(frames);
            }

#if UNITY_EDITOR
            RecordWorldShiftClipDiagnostics(clipName, diagnosticsStartTimestamp);
#endif
            return worldShift;
        }

#if UNITY_EDITOR
        public static void ConsumeWorldShiftClipDiagnostics(
            out int calls,
            out float totalMs,
            out float maxMs,
            out string maxName)
        {
            calls = _worldShiftClipCalls;
            totalMs = _worldShiftClipTotalMs;
            maxMs = _worldShiftClipMaxMs;
            maxName = _worldShiftClipMaxName;

            _worldShiftClipCalls = 0;
            _worldShiftClipTotalMs = 0f;
            _worldShiftClipMaxMs = 0f;
            _worldShiftClipMaxName = null;
        }

        private static void RecordWorldShiftClipDiagnostics(string clipName, long startTimestamp)
        {
            long elapsedTicks = global::System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp;
            float elapsedMs = elapsedTicks * 1000f / global::System.Diagnostics.Stopwatch.Frequency;

            _worldShiftClipCalls++;
            _worldShiftClipTotalMs += elapsedMs;
            if (elapsedMs <= _worldShiftClipMaxMs)
                return;

            _worldShiftClipMaxMs = elapsedMs;
            _worldShiftClipMaxName = clipName;
        }
#endif

        /// <summary>Возвращает AnimationClip из TransformAnimatorController по имени.</summary>
        public static AnimationClip? FindClipByName(
        TransformAnimatorController ctrl,
        string clipName)
        {
            if (ctrl == null)
                return null;

            return ctrl.TryFindClip(clipName, out AnimationClip clip)
                ? clip
                : null;
        }

        /// <summary>
        /// Возвращает root-Y в середине указанного клипа.
        /// При ошибке — останавливает игру и выводит сообщение.
        /// Работает одинаково в редакторе и в билде.
        /// </summary>
        public static float GetClipRootYAtHalf(
            TransformAnimatorController ctrl,
            string clipName)
        {
            if (ctrl == null)
            {
                LogAndStopGame("[GetClipRootYAtHalf] ctrl == null.");
                return 0f;
            }

            var clip = FindClipByName(ctrl, clipName);
            if (clip == null)
            {
                LogAndStopGame($"[GetClipRootYAtHalf] Клип '{clipName}' не найден.");
                return 0f;
            }

            var go = new GameObject("ClipSampler_TMP");
            try
            {
                var animator = go.AddComponent<Animator>();
                if (ctrl.Animator?.runtimeAnimatorController != null)
                {
                    animator.runtimeAnimatorController = ctrl.Animator.runtimeAnimatorController;
                }

                var t = go.transform;
                var midTime = clip.length * 0.8667f;

                clip.SampleAnimation(go, midTime);
                return t.localPosition.y;
            }
            finally
            {
                Object.Destroy(go);
            }
        }

    }
}
