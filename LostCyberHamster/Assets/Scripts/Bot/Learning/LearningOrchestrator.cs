using System;
using System.Collections.Generic;
using Assets.Scripts.Common;
using Assets.Scripts.Gameplay;
using UnityEngine;
using Vues.GameCore;

namespace Assets.Scripts.Bot.Learning
{
    /// <summary>
    /// Управляет полным циклом обучения бота:
    /// Загрузка генома → Мутация → Геймплей → Сбор данных → Оценка → Сохранение → Рестарт.
    /// Подключается к GameEventsManager для сбора данных сессии.
    /// </summary>
    public class LearningOrchestrator : IDisposable
    {
        // ──────────────── Public State ────────────────

        /// <summary>Включён ли режим обучения.</summary>
        public bool IsTrainingMode { get; private set; }

        /// <summary>Текущее поколение генома.</summary>
        public int CurrentGeneration => _currentGenome?.Generation ?? 0;

        /// <summary>Лучший fitness для текущего уровня/стиля.</summary>
        public float BestFitness => _currentGenome?.BestFitness ?? 0f;

        /// <summary>Fitness последней сессии.</summary>
        public float LastFitness => _currentGenome?.LastFitness ?? 0f;

        /// <summary>Текстовое описание последней мутации.</summary>
        public string LastMutationInfo { get; private set; } = "";

        /// <summary>Улучшился ли результат в последней сессии.</summary>
        public bool LastSessionImproved { get; private set; }

        /// <summary>Текущий отчёт сессии (для UI).</summary>
        public BotSessionReport CurrentReport => _currentReport;

        // ──────────────── Internals ────────────────

        private readonly GenomeManager _genomeManager;
        private BotGenome _currentGenome;
        private BotSessionReport _currentReport;
        private bool _subscribed;

        private Hamster _hamster;
        private BotPlayStyle _playStyle;
        private string _levelName;

        public LearningOrchestrator()
        {
            _genomeManager = new GenomeManager();
        }

        // ──────────────── Public API ────────────────

        /// <summary>Включить/выключить режим обучения.</summary>
        public void ToggleTraining()
        {
            IsTrainingMode = !IsTrainingMode;
            DebugManager.DiagLog($"[Learning] Training mode: {(IsTrainingMode ? "ON" : "OFF")}");
        }

        /// <summary>Сбросить геном к пресету (удалить JSON).</summary>
        public void ResetGenome(BotPlayStyle style, string levelName)
        {
            _genomeManager.Delete(style, levelName);
            _currentGenome = null;
            LastMutationInfo = "Genome reset to preset";
            DebugManager.DiagLog($"[Learning] Genome reset for {style}/{levelName}");
        }

        /// <summary>
        /// Инициализация перед началом уровня.
        /// Загружает или создаёт геном, при обучении — мутирует.
        /// Возвращает BotPlayStyleConfig для BotBrain.
        /// </summary>
        public BotPlayStyleConfig InitForLevel(Hamster hamster, BotPlayStyle style, string levelName)
        {
            _hamster = hamster;
            _playStyle = style;
            _levelName = levelName;

            // Загрузить лучший геном или создать новый из пресета
            _currentGenome = _genomeManager.Load(style, levelName);
            if (_currentGenome == null)
            {
                var preset = BotPlayStylePresets.Get(style);
                _currentGenome = BotGenome.FromConfig(preset, levelName);
                DebugManager.DiagLog($"[Learning] Created new genome for {style}/{levelName}");
            }
            else
            {
                DebugManager.DiagLog($"[Learning] Loaded genome gen={_currentGenome.Generation} " +
                                     $"best={_currentGenome.BestFitness:F1} for {style}/{levelName}");
            }

            BotPlayStyleConfig config;

            if (IsTrainingMode)
            {
                // Мутируем на основе fail reasons предыдущей сессии
                var failReasons = _currentReport?.FailReasons ?? new List<FailReason>();
                var mutated = ParameterTuner.Mutate(_currentGenome, failReasons);
                _currentGenome = mutated;
                config = mutated.ToConfig();

                LastMutationInfo = $"Gen {mutated.Generation}";
                if (failReasons.Count > 0)
                {
                    LastMutationInfo += $": {failReasons.Count} targeted mutations";
                    foreach (var fr in failReasons)
                        LastMutationInfo += $"\n  {fr} -> {ParameterTuner.DescribeMutation(fr)}";
                }
                else
                {
                    LastMutationInfo += ": random mutations only";
                }

                DebugManager.DiagLog($"[Learning] {LastMutationInfo}");

                // Консольный вывод для удобства наблюдения
                Debug.Log($"<color=#FFA500>[TRAINING]</color> <b>Gen {mutated.Generation}</b> | {_playStyle}/{_levelName}\n" +
                          $"  Mutations: {(failReasons.Count > 0 ? string.Join(", ", failReasons) : "random only")}\n" +
                          FormatConfigChanges(mutated.ToConfig()));
            }
            else
            {
                config = _currentGenome.ToConfig();
            }

            // Начинаем сбор данных о сессии
            StartSessionReport(style, levelName);

            return config;
        }

        /// <summary>
        /// Вызывается по окончании уровня (GameManager.OnFinish).
        /// Считает Fitness, сравнивает с лучшим, сохраняет геном.
        /// </summary>
        public void OnGameFinished(bool won)
        {
            if (_currentReport == null) return;

            // Финализация отчёта
            _currentReport.Won = won;
            _currentReport.TimeAlive = Time.time - _currentReport.SessionStartTime;
            _currentReport.LivesAtEnd = _hamster != null ? _hamster.Lives.Value : 0;
            _currentReport.CoinsAtEnd = ResourceManager.GetCurrentBalance(ResourceType.Coins);

            // Вычисляем fitness
            float fitness = SessionAnalyzer.Evaluate(_currentReport);

            DebugManager.DiagLog(
                $"[Learning] Session complete: fitness={fitness:F1} | won={won} " +
                $"| lives={_currentReport.LivesAtEnd} | time={_currentReport.TimeAlive:F1}s " +
                $"| coins={_currentReport.CoinsCollected} | crystals={_currentReport.CrystalsCollected} " +
                $"| collisions={_currentReport.ObstacleCollisions} " +
                $"| ulta={_currentReport.UltaUsesCount} | fails=[{string.Join(", ", _currentReport.FailReasons)}]");

            // Консольный резюме сессии
            string outcomeTag = won ? "<color=#00FF00>WIN</color>" : "<color=#FF4444>LOST</color>";
            string failsStr = _currentReport.FailReasons.Count > 0
                ? $"  Problems: {string.Join(", ", _currentReport.FailReasons)}"
                : "  No problems detected";
            Debug.Log($"<color=#FFA500>[TRAINING]</color> Session Result: {outcomeTag} | " +
                      $"fitness=<b>{fitness:F0}</b> (best={_currentGenome?.BestFitness ?? 0:F0})\n" +
                      $"  {_playStyle}/{_levelName} | time={_currentReport.TimeAlive:F1}s\n" +
                      $"  Lives: {_currentReport.LivesAtStart} -> {_currentReport.LivesAtEnd} | " +
                      $"Collisions: {_currentReport.ObstacleCollisions} | " +
                      $"JumpOver: {_currentReport.ObstaclesJumpedOver} | JumpOn: {_currentReport.ObstaclesJumpedOn}\n" +
                      $"  Coins: {_currentReport.CoinsCollected} | Crystals: {_currentReport.CrystalsCollected} | " +
                      $"Ulta: {_currentReport.UltaUsesCount}\n" +
                      failsStr);

            // Сохраняем геном
            if (_currentGenome != null)
            {
                LastSessionImproved = _genomeManager.SaveIfBetter(_currentGenome, fitness);

                if (LastSessionImproved)
                {
                    DebugManager.DiagLog($"[Learning] NEW BEST! fitness={fitness:F1} (gen {_currentGenome.Generation})");
                    Debug.Log($"<color=#00FF00>[TRAINING] >>> NEW BEST! fitness={fitness:F0} (gen {_currentGenome.Generation}) <<<</color>");
                }
                else
                {
                    DebugManager.DiagLog($"[Learning] No improvement. best={_currentGenome.BestFitness:F1}, " +
                                         $"current={fitness:F1}");
                    Debug.Log($"<color=#FF8800>[TRAINING]</color> No improvement. best={_currentGenome.BestFitness:F0}, current={fitness:F0}");
                }
            }

            UnsubscribeFromEvents();
        }

        /// <summary>
        /// Трекинг действий бота (для подсчёта покупок, прыжков, и т.д.).
        /// Вызывается из HamsterBot.ExecuteAction.
        /// </summary>
        public void TrackAction(BotAction action)
        {
            if (_currentReport == null) return;

            switch (action)
            {
                case BotAction.Jump:
                    _currentReport.JumpsExecuted++;
                    break;
                case BotAction.SuperJump:
                    _currentReport.SuperJumpsExecuted++;
                    break;
                case BotAction.SwitchLane:
                    _currentReport.LaneSwitches++;
                    break;
                case BotAction.BuyEnergy:
                    _currentReport.EnergyPurchases++;
                    break;
                case BotAction.BuyUlta:
                    _currentReport.UltaPurchases++;
                    break;
            }
        }

        public void Dispose()
        {
            UnsubscribeFromEvents();
        }

        // ──────────────── Formatting ────────────────

        /// <summary>
        /// Форматирует ключевые параметры конфига для консольного вывода.
        /// </summary>
        private static string FormatConfigChanges(BotPlayStyleConfig cfg)
        {
            return $"  Params: Aggr={cfg.AggressionLevel:F2} | Window={cfg.UrgentWindowSec:F2} | " +
                   $"EnergyCons={cfg.EnergyConserveThreshold} | UltaCluster={cfg.UltaClusterThreshold}\n" +
                   $"  Weights: Surv={cfg.WeightSurvival:F1} Enrg={cfg.WeightEnergy:F1} " +
                   $"Coll={cfg.WeightCollectibles:F1} Ulta={cfg.WeightUlta:F1}";
        }

        // ──────────────── Session Data Collection ────────────────

        private void StartSessionReport(BotPlayStyle style, string levelName)
        {
            UnsubscribeFromEvents();

            _currentReport = new BotSessionReport
            {
                LevelName = levelName,
                PlayStyle = style,
                SessionStartTime = Time.time,
                LivesAtStart = _hamster != null ? _hamster.Lives.Value : 3,
                CoinsAtStart = ResourceManager.GetCurrentBalance(ResourceType.Coins),
            };

            SubscribeToEvents();
        }

        private void SubscribeToEvents()
        {
            if (_subscribed) return;
            _subscribed = true;

            GameEventsManager.OnCoinCollected += OnCoinCollected;
            GameEventsManager.OnCrystalsCollected += OnCrystalCollected;
            GameEventsManager.OnLivesLost += OnLivesLost;
            GameEventsManager.OnLivesAdded += OnLivesAdded;
            GameEventsManager.OnEnergySpent += OnEnergySpent;
            GameEventsManager.OnEnergyAdded += OnEnergyAdded;
            GameEventsManager.OnUltaUsed += OnUltaUsed;
            GameEventsManager.OnObstacleCollision += OnObstacleCollision;
            GameEventsManager.OnObstacleJumpedOver += OnObstacleJumpedOver;
            GameEventsManager.OnObstacleJumpedOn += OnObstacleJumpedOn;
        }

        private void UnsubscribeFromEvents()
        {
            if (!_subscribed) return;
            _subscribed = false;

            GameEventsManager.OnCoinCollected -= OnCoinCollected;
            GameEventsManager.OnCrystalsCollected -= OnCrystalCollected;
            GameEventsManager.OnLivesLost -= OnLivesLost;
            GameEventsManager.OnLivesAdded -= OnLivesAdded;
            GameEventsManager.OnEnergySpent -= OnEnergySpent;
            GameEventsManager.OnEnergyAdded -= OnEnergyAdded;
            GameEventsManager.OnUltaUsed -= OnUltaUsed;
            GameEventsManager.OnObstacleCollision -= OnObstacleCollision;
            GameEventsManager.OnObstacleJumpedOver -= OnObstacleJumpedOver;
            GameEventsManager.OnObstacleJumpedOn -= OnObstacleJumpedOn;
        }

        // ──────────────── Event Handlers ────────────────

        private void OnCoinCollected(int value)
        {
            if (_currentReport != null) _currentReport.CoinsCollected += value;
        }

        private void OnCrystalCollected(int value)
        {
            if (_currentReport != null) _currentReport.CrystalsCollected += value;
        }

        private void OnLivesLost(int amount)
        {
            if (_currentReport != null) _currentReport.LivesLost += amount;
        }

        private void OnLivesAdded(int amount)
        {
            if (_currentReport != null) _currentReport.LivesGained += amount;
        }

        private void OnEnergySpent(int amount)
        {
            if (_currentReport != null) _currentReport.EnergySpentTotal += amount;
        }

        private void OnEnergyAdded(int amount)
        {
            if (_currentReport != null) _currentReport.EnergyGainedTotal += amount;
        }

        private void OnUltaUsed()
        {
            if (_currentReport != null) _currentReport.UltaUsesCount++;
        }

        private void OnObstacleCollision()
        {
            if (_currentReport != null) _currentReport.ObstacleCollisions++;
        }

        private void OnObstacleJumpedOver(string name)
        {
            if (_currentReport != null) _currentReport.ObstaclesJumpedOver++;
        }

        private void OnObstacleJumpedOn(string name)
        {
            if (_currentReport != null) _currentReport.ObstaclesJumpedOn++;
        }
    }
}
