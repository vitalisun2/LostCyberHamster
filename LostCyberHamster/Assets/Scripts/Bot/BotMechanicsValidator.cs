using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Assets.Scripts.Common.Models;
using Assets.Scripts.GameEngine;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;
using UnityEngine;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Автоматический валидатор механик — отслеживает переходы стейтов хомяка
    /// и проверяет их корректность по таблице из hamster_collision_test_scenarios.md.
    /// Работает только в режиме BotMode.Test.
    /// </summary>
    public class BotMechanicsValidator : IDisposable
    {
        private readonly List<BotTestScenario> _scenarios;
        private readonly Dictionary<HamsterStateEnum, List<int>> _stateToScenarioIndices;

        private Hamster _hamster;
        private HamsterStateEnum _previousState;
        private float _sessionStartTime;
        private bool _subscribed;
        private bool _disposed;

        // Tracking for obstacle context
        private string _lastCollisionObstacle;
        private string _lastJumpedOnObstacle;
        private string _lastJumpedOverObstacle;
        private float _lastCollisionTime;

        public IReadOnlyList<BotTestScenario> Scenarios => _scenarios;

        public BotMechanicsValidator()
        {
            _scenarios = new List<BotTestScenario>();
            _stateToScenarioIndices = new Dictionary<HamsterStateEnum, List<int>>();
            BuildScenarios();
        }

        /// <summary>
        /// Начинает валидационную сессию.
        /// </summary>
        public void Start(Hamster hamster)
        {
            _hamster = hamster;
            _sessionStartTime = Time.time;
            _previousState = hamster.HamsterState.Value;

            ResetResults();
            Subscribe();

            DebugManager.DiagLog("[BotMechanicsValidator] Validation session started.");
        }

        /// <summary>
        /// Останавливает валидацию и генерирует отчёт.
        /// </summary>
        public void Stop()
        {
            Unsubscribe();
            WriteReport();
            DebugManager.DiagLog("[BotMechanicsValidator] Validation session stopped. Report written.");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Unsubscribe();
        }

        // ──────────────── Event Handlers ────────────────

        private void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;

            GameEventsManager.OnObstacleCollision += OnCollision;
            GameEventsManager.OnObstacleJumpedOn += OnJumpedOn;
            GameEventsManager.OnObstacleJumpedOver += OnJumpedOver;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;

            GameEventsManager.OnObstacleCollision -= OnCollision;
            GameEventsManager.OnObstacleJumpedOn -= OnJumpedOn;
            GameEventsManager.OnObstacleJumpedOver -= OnJumpedOver;
        }

        /// <summary>
        /// Вызывается из HamsterBot.Update() каждый кадр для отслеживания стейт-переходов.
        /// </summary>
        public void Tick()
        {
            if (_hamster == null) return;

            var currentState = _hamster.HamsterState.Value;
            if (currentState != _previousState)
            {
                OnStateChanged(_previousState, currentState);
                _previousState = currentState;
            }
        }

        private void OnCollision()
        {
            _lastCollisionTime = Time.time;
        }

        private void OnJumpedOn(string obstacleName)
        {
            _lastJumpedOnObstacle = obstacleName;
        }

        private void OnJumpedOver(string obstacleName)
        {
            _lastJumpedOverObstacle = obstacleName;
        }

        private void OnStateChanged(HamsterStateEnum oldState, HamsterStateEnum newState)
        {
            float t = Time.time - _sessionStartTime;

            if (!_stateToScenarioIndices.TryGetValue(newState, out var indices))
                return;

            for (int i = 0; i < indices.Count; i++)
            {
                var scenario = _scenarios[indices[i]];
                if (scenario.Status == TestStatus.Pass)
                    continue; // Уже пройден

                // Проверяем, что пришли из ожидаемого стейта
                bool contextMatches = ValidateTransitionContext(scenario, oldState, newState);
                if (contextMatches)
                {
                    scenario.MarkPassed(t, $"from={oldState}");
                    DebugManager.DiagLog(
                        $"[BotMechanicsValidator] PASS: {scenario.Group} #{scenario.ScenarioId} " +
                        $"| {oldState}->{newState} | t={t:F2}");
                }
            }
        }

        /// <summary>
        /// Проверяет, что переход стейтов произошёл из корректного контекста.
        /// </summary>
        private bool ValidateTransitionContext(
            BotTestScenario scenario, HamsterStateEnum from, HamsterStateEnum to)
        {
            switch (scenario.Group)
            {
                case "JumpMechanics":
                    // Прыжок с земли: должны были быть в Run или Jump
                    return from == HamsterStateEnum.Run ||
                           from == HamsterStateEnum.Jump;

                case "RoofJumpMechanics":
                    // Прыжок с крыши: должны были быть в RoofRun или RoofJump
                    return from == HamsterStateEnum.RoofRun ||
                           from == HamsterStateEnum.RoofJump;

                case "RoofRunMechanics":
                    // Работа на крыше
                    return from == HamsterStateEnum.JumpOnRoof ||
                           from == HamsterStateEnum.RoofRun ||
                           from == HamsterStateEnum.SuperJumpOnRoof;

                case "SuperJumpMechanics":
                    // Суперпрыжок с земли
                    return from == HamsterStateEnum.Jump ||
                           from == HamsterStateEnum.SuperJump ||
                           from == HamsterStateEnum.Run;

                case "SuperRoofJumpMechanics":
                    // Суперпрыжок с крыши
                    return from == HamsterStateEnum.RoofRun ||
                           from == HamsterStateEnum.SuperRoofJump ||
                           from == HamsterStateEnum.RoofJump;

                default:
                    return true;
            }
        }

        // ──────────────── Scenario Definitions ────────────────

        private void BuildScenarios()
        {
            _scenarios.Clear();
            _stateToScenarioIndices.Clear();

            // === 1. Jump Mechanics (9 scenarios) ===
            Add("JumpMechanics", 1, "Напрыгнули на smallAlive",
                HamsterStateEnum.JumpOnObstacle, InteractionType.JumpOn, ObstacleTypeEnum.smallAlive);
            Add("JumpMechanics", 2, "Перепрыгнули smallAlive",
                HamsterStateEnum.JumpOver, InteractionType.JumpOver, ObstacleTypeEnum.smallAlive);
            Add("JumpMechanics", 3, "Столкнулись с smallNotAliveRoad на дороге",
                HamsterStateEnum.JumpDamageForSmallNotAlive, InteractionType.Collision, ObstacleTypeEnum.smallNotAliveRoad);
            Add("JumpMechanics", 4, "Перепрыгнули smallNotAliveRoad",
                HamsterStateEnum.JumpOver, InteractionType.JumpOver, ObstacleTypeEnum.smallNotAliveRoad);
            Add("JumpMechanics", 5, "Столкнулись с smallNotAliveRoadAndRoof, запрыгивая на крышу",
                HamsterStateEnum.JumpOnRoofDamage, InteractionType.Collision, ObstacleTypeEnum.smallNotAliveRoadAndRoof);
            Add("JumpMechanics", 6, "Столкнулись с smallNotAliveRoadAndRoof на дороге",
                HamsterStateEnum.JumpDamageForSmallNotAlive, InteractionType.Collision, ObstacleTypeEnum.smallNotAliveRoadAndRoof);
            Add("JumpMechanics", 7, "Столкнулись с bigAlive",
                HamsterStateEnum.JumpDamageForBigAlive, InteractionType.Collision, ObstacleTypeEnum.bigAlive);
            Add("JumpMechanics", 8, "Запрыгнули на крышу чистого bigNotAlive",
                HamsterStateEnum.JumpOnRoof, InteractionType.LandOnRoof, ObstacleTypeEnum.bigNotAlive);
            Add("JumpMechanics", 9, "Запрыгнули на bigNotAlive, на крыше мелкое препятствие",
                HamsterStateEnum.JumpOnRoofDamage, InteractionType.Collision, ObstacleTypeEnum.bigNotAlive);

            // === 2. Roof Jump Mechanics (7 scenarios) ===
            Add("RoofJumpMechanics", 1, "Прыгнули находясь на крыше чистого bigNotAlive",
                HamsterStateEnum.RoofJump, InteractionType.RoofJump, ObstacleTypeEnum.bigNotAlive);
            Add("RoofJumpMechanics", 2, "Прыгнули с крыши, столкнулись с smallNotAliveRoadAndRoof",
                HamsterStateEnum.RoofJumpDamage, InteractionType.Collision, ObstacleTypeEnum.smallNotAliveRoadAndRoof);
            Add("RoofJumpMechanics", 3, "Напрыгнули на bigAlive, спрыгивая с крыши",
                HamsterStateEnum.JumpOnObstacleFromRoof, InteractionType.JumpOn, ObstacleTypeEnum.bigAlive);
            Add("RoofJumpMechanics", 4, "Напрыгнули на smallAlive, спрыгивая с крыши",
                HamsterStateEnum.JumpOnObstacleFromRoof, InteractionType.JumpOn, ObstacleTypeEnum.smallAlive);
            Add("RoofJumpMechanics", 5, "Спрыгивая с крыши, столкнулись с smallNotAliveRoad",
                HamsterStateEnum.JumpFromRoofDamage, InteractionType.Collision, ObstacleTypeEnum.smallNotAliveRoad);
            Add("RoofJumpMechanics", 6, "Спрыгивая с крыши, столкнулись с smallNotAliveRoadAndRoof",
                HamsterStateEnum.JumpFromRoofDamage, InteractionType.Collision, ObstacleTypeEnum.smallNotAliveRoadAndRoof);
            Add("RoofJumpMechanics", 7, "Спрыгивая с крыши, не задели препятствий",
                HamsterStateEnum.JumpFromRoof, InteractionType.FallFromRoof, ObstacleTypeEnum.bigNotAlive);

            // === 3. Roof Run Mechanics (2 scenarios) ===
            Add("RoofRunMechanics", 1, "Крыша закончилась — хомяк спрыгнул на дорогу",
                HamsterStateEnum.RunFromRoof, InteractionType.FallFromRoof, ObstacleTypeEnum.bigNotAlive);
            Add("RoofRunMechanics", 2, "Перебежали на следующую крышу bigNotAlive",
                HamsterStateEnum.RoofRun, InteractionType.RoofRun, ObstacleTypeEnum.bigNotAlive);

            // === 4. Super Jump Mechanics (12 scenarios) ===
            Add("SuperJumpMechanics", 1, "Запрыгнули на крышу чистого bigNotAlive",
                HamsterStateEnum.SuperJumpOnRoof, InteractionType.LandOnRoof, ObstacleTypeEnum.bigNotAlive);
            Add("SuperJumpMechanics", 2, "Запрыгнули на bigNotAlive, столкнулись с smallNotAliveRoadAndRoof",
                HamsterStateEnum.SuperJumpOnRoofDamage, InteractionType.Collision, ObstacleTypeEnum.smallNotAliveRoadAndRoof);
            Add("SuperJumpMechanics", 3, "Столкнулись с bigAlive",
                HamsterStateEnum.SuperJumpDamage, InteractionType.SuperJumpCollision, ObstacleTypeEnum.bigAlive);
            Add("SuperJumpMechanics", 4, "Перепрыгнули bigAlive",
                HamsterStateEnum.SuperJumpOver, InteractionType.SuperJumpOver, ObstacleTypeEnum.bigAlive);
            Add("SuperJumpMechanics", 5, "Напрыгнули на smallAlive",
                HamsterStateEnum.SuperJumpOnObstacle, InteractionType.SuperJumpOn, ObstacleTypeEnum.smallAlive);
            Add("SuperJumpMechanics", 6, "Перепрыгнули smallAlive",
                HamsterStateEnum.SuperJumpOver, InteractionType.SuperJumpOver, ObstacleTypeEnum.smallAlive);
            Add("SuperJumpMechanics", 7, "Столкнулись с smallNotAliveRoad",
                HamsterStateEnum.SuperJumpDamage, InteractionType.SuperJumpCollision, ObstacleTypeEnum.smallNotAliveRoad);
            Add("SuperJumpMechanics", 8, "Перепрыгнули smallNotAliveRoad",
                HamsterStateEnum.SuperJumpOver, InteractionType.SuperJumpOver, ObstacleTypeEnum.smallNotAliveRoad);
            Add("SuperJumpMechanics", 9, "Столкнулись с smallNotAliveRoadAndRoof, запрыгивая на крышу",
                HamsterStateEnum.SuperJumpOnRoofDamage, InteractionType.Collision, ObstacleTypeEnum.smallNotAliveRoadAndRoof);
            Add("SuperJumpMechanics", 10, "Столкнулись с smallNotAliveRoadAndRoof на дороге",
                HamsterStateEnum.SuperJumpDamage, InteractionType.SuperJumpCollision, ObstacleTypeEnum.smallNotAliveRoadAndRoof);
            Add("SuperJumpMechanics", 11, "Перепрыгнули smallNotAliveRoadAndRoof",
                HamsterStateEnum.SuperJumpOver, InteractionType.SuperJumpOver, ObstacleTypeEnum.smallNotAliveRoadAndRoof);
            Add("SuperJumpMechanics", 12, "Не задели препятствий",
                HamsterStateEnum.SuperJump, InteractionType.NoContact);

            // === 5. Super Roof Jump Mechanics (7 scenarios) ===
            Add("SuperRoofJumpMechanics", 1, "Прыгнули находясь на крыше чистого bigNotAlive",
                HamsterStateEnum.SuperRoofJump, InteractionType.RoofJump, ObstacleTypeEnum.bigNotAlive);
            Add("SuperRoofJumpMechanics", 2, "Прыгнули с крыши, столкнулись с smallNotAliveRoadAndRoof",
                HamsterStateEnum.SuperRoofJumpDamage, InteractionType.Collision, ObstacleTypeEnum.smallNotAliveRoadAndRoof);
            Add("SuperRoofJumpMechanics", 3, "Напрыгнули на bigAlive, спрыгивая с крыши",
                HamsterStateEnum.SuperJumpOnObstacleFromRoof, InteractionType.JumpOn, ObstacleTypeEnum.bigAlive);
            Add("SuperRoofJumpMechanics", 4, "Напрыгнули на smallAlive, спрыгивая с крыши",
                HamsterStateEnum.SuperJumpOnObstacleFromRoof, InteractionType.JumpOn, ObstacleTypeEnum.smallAlive);
            Add("SuperRoofJumpMechanics", 5, "Спрыгивая с крыши, столкнулись с smallNotAliveRoad",
                HamsterStateEnum.SuperJumpFromRoofDamage, InteractionType.Collision, ObstacleTypeEnum.smallNotAliveRoad);
            Add("SuperRoofJumpMechanics", 6, "Спрыгивая с крыши, столкнулись с smallNotAliveRoadAndRoof",
                HamsterStateEnum.SuperJumpFromRoofDamage, InteractionType.Collision, ObstacleTypeEnum.smallNotAliveRoadAndRoof);
            Add("SuperRoofJumpMechanics", 7, "Спрыгивая с крыши, не задели препятствий",
                HamsterStateEnum.SuperJumpFromRoof, InteractionType.FallFromRoof, ObstacleTypeEnum.bigNotAlive);
        }

        private void Add(
            string group, int id, string desc,
            HamsterStateEnum expectedState, InteractionType interaction,
            ObstacleTypeEnum obstacleType = ObstacleTypeEnum.smallAlive)
        {
            int index = _scenarios.Count;
            _scenarios.Add(new BotTestScenario(group, id, desc, expectedState, interaction, obstacleType));

            if (!_stateToScenarioIndices.ContainsKey(expectedState))
                _stateToScenarioIndices[expectedState] = new List<int>();
            _stateToScenarioIndices[expectedState].Add(index);
        }

        private void ResetResults()
        {
            for (int i = 0; i < _scenarios.Count; i++)
            {
                _scenarios[i].Status = TestStatus.NotTested;
                _scenarios[i].ResultComment = "";
                _scenarios[i].TestedAtTimestamp = 0f;
            }
        }

        // ──────────────── Report ────────────────

        private void WriteReport()
        {
            string baseDir;
#if UNITY_EDITOR
            baseDir = System.IO.Path.Combine(Application.dataPath, "..", "EditorLogs", "bot_sessions");
#else
            baseDir = System.IO.Path.Combine(Application.persistentDataPath, "bot_sessions");
#endif
            Directory.CreateDirectory(baseDir);

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string path = System.IO.Path.Combine(baseDir, $"validation_{timestamp}.txt");

            var sb = new StringBuilder();
            sb.AppendLine("=== HamsterBot Mechanics Validation Report ===");
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Duration: {(Time.time - _sessionStartTime):F1}s");
            sb.AppendLine();

            int pass = 0, fail = 0, skip = 0, notTested = 0;
            string currentGroup = "";

            for (int i = 0; i < _scenarios.Count; i++)
            {
                var s = _scenarios[i];

                if (s.Group != currentGroup)
                {
                    currentGroup = s.Group;
                    sb.AppendLine($"--- {currentGroup} ---");
                }

                sb.AppendLine(s.ToString());

                switch (s.Status)
                {
                    case TestStatus.Pass: pass++; break;
                    case TestStatus.Fail: fail++; break;
                    case TestStatus.Skip: skip++; break;
                    default: notTested++; break;
                }
            }

            sb.AppendLine();
            sb.AppendLine($"=== SUMMARY: {pass} PASS | {fail} FAIL | {skip} SKIP | {notTested} NOT TESTED ===");
            sb.AppendLine($"Coverage: {(pass + fail) * 100 / Math.Max(_scenarios.Count, 1)}%");

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            DebugManager.DiagLog($"[BotMechanicsValidator] Report: {path}");
            Debug.Log($"[BotMechanicsValidator] Report saved: {path}");
        }
    }
}
