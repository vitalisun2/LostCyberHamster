using System.Collections.Generic;
using Assets.Scripts.Common.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Генерирует множество кандидатных цепочек действий через рекурсивный перебор вариантов.
    /// Использует StateProjector для симуляции последствий каждого шага.
    /// Не обращается к Unity-объектам — работает только со snapshot-данными.
    /// </summary>
    public class ChainGenerator
    {
        // ──────────────── Ограничения генерации ────────────────

        private const int DefaultMaxDepth       = 5;
        private const int MaxCandidates         = 50;
        private const int MaxBranchingPerObject = 4;

        // Расстояния исполнения шагов (как в BotChainPlanner legacy)
        private const float SafeMargin          = 1.5f;
        private const float JumpLandingTravel   = 3.8f;
        private const int   JumpEnergyCost      = 10;
        private const int   SuperJumpEnergyCost = 20;

        // SwitchLane — стратегическое действие, выполнять раньше, чем Jump.
        // Даёт время на Jump/другие действия на целевой полосе после переключения.
        private const float SwitchLaneExecuteDistance = 4.0f;

        // Минимальный заряд ульты
        private const int UltaReadyCharge       = 100;
        // Минимум угроз вблизи для применения ульты
        private const int UltaMinNearThreats    = 2;
        private const float UltaCheckRange      = 6f;

        // ──────────────── Зависимости ────────────────

        private readonly StateProjector _projector = new StateProjector();

        // ──────────────── Рабочие буферы ────────────────

        private readonly List<ChainCandidate> _results = new List<ChainCandidate>(MaxCandidates);
        private readonly List<ChainStep>      _stepBuffer = new List<ChainStep>(8);

        // ══════════════════════════════════════════════
        //  Публичный API
        // ══════════════════════════════════════════════

        /// <summary>
        /// Генерирует список кандидатных цепочек из начального состояния.
        /// </summary>
        public List<ChainCandidate> Generate(
            List<ObstacleInfo> classified,
            ProjectedState initial,
            int maxDepth = DefaultMaxDepth)
        {
            _results.Clear();

            var currentSteps = new List<ChainStep>(maxDepth);
            Recurse(classified, initial, currentSteps, maxDepth);

            // Если ни одной безопасной цепочки не найдено — добавляем пустую (стоять/ждать)
            if (_results.Count == 0)
            {
                _results.Add(BuildCandidate(new List<ChainStep>(), initial));
            }

            return _results;
        }

        // ══════════════════════════════════════════════
        //  Рекурсия
        // ══════════════════════════════════════════════

        private void Recurse(
            List<ObstacleInfo> objects,
            ProjectedState state,
            List<ChainStep> currentSteps,
            int depthLeft)
        {
            if (_results.Count >= MaxCandidates) return;

            // ── Ульта как первый вариант (если готова и впереди кластер угроз) ──
            if (currentSteps.Count == 0 && ShouldConsiderUlta(state))
            {
                TryUltaVariant(objects, state, currentSteps, depthLeft);
                if (_results.Count >= MaxCandidates) return;
            }

            // ── Найти ближайший объект, требующий реакции ──
            int nextIdx = FindNextActionableObject(objects, state);

            if (nextIdx < 0)
            {
                // Дорога чистая — сохранить текущую цепочку как кандидата
                _results.Add(BuildCandidate(currentSteps, state));

                // Попытаться подобрать бонус с другой линии
                TryCollectibleVariant(objects, state, currentSteps, depthLeft);
                return;
            }

            var target = objects[nextIdx];

            // ── Генерировать варианты для этого объекта ──
            var variants = GenerateVariants(target, state);

            bool anyVariantAdded = false;

            foreach (var variant in variants)
            {
                if (_results.Count >= MaxCandidates) return;

                var nextState = _projector.Project(state, variant, target);

                // Отсечение: приземление в опасной зоне
                if (!_projector.IsSafeAfterProjection(nextState)) continue;

                // SwitchLane: verify no threats along the full path on destination lane
                if (variant.Action == BotAction.SwitchLane &&
                    !IsSwitchLanePathSafe(state.ApproxX, nextState))
                    continue;

                // Обновить RemainingObjects по факту шага
                var updatedObjects = BuildUpdatedObjects(objects, nextState, variant, target);

                var newSteps = new List<ChainStep>(currentSteps) { variant };

                if (depthLeft > 1)
                {
                    Recurse(updatedObjects, nextState, newSteps, depthLeft - 1);
                }
                else
                {
                    _results.Add(BuildCandidate(newSteps, nextState));
                }

                anyVariantAdded = true;
            }

            // Если ни один вариант не прошёл — цепочка тупиковая (не добавляем)
        }

        // ══════════════════════════════════════════════
        //  Определение следующего объекта
        // ══════════════════════════════════════════════

        /// <summary>
        /// Возвращает индекс ближайшего впереди Threat или Target на текущей линии.
        /// -1 если таких нет.
        /// </summary>
        private static int FindNextActionableObject(List<ObstacleInfo> objects, ProjectedState state)
        {
            for (int i = 0; i < objects.Count; i++)
            {
                var obs = objects[i];
                if (obs.Category != ObjectCategory.Threat &&
                    obs.Category != ObjectCategory.Target) continue;

                if (obs.LeftX < state.ApproxX - 0.1f) continue; // позади хомяка

                if (!IsOnSameLane(obs, state)) continue;

                return i;
            }
            return -1;
        }

        // ══════════════════════════════════════════════
        //  Генерация вариантов для объекта
        // ══════════════════════════════════════════════

        private List<ChainStep> GenerateVariants(ObstacleInfo target, ProjectedState state)
        {
            var variants = new List<ChainStep>(MaxBranchingPerObject);

            bool fromRoof = state.OnRoof;
            int energy    = state.Energy;

            // ── Ульта не генерируется здесь — только в TryUltaVariant ──

            switch (target.Type)
            {
                case ObstacleTypeEnum.smallAlive:
                    AddSwitchLaneVariant(variants, target, state);
                    // JumpOn: напрыгнуть на цель (не с крыши)
                    if (!fromRoof && energy >= JumpEnergyCost)
                        variants.Add(MakeStep(BotAction.Jump, target, JumpLandingTravel, JumpEnergyCost, "JumpOn smallAlive"));
                    // JumpOver: перепрыгнуть
                    if (energy >= JumpEnergyCost)
                        variants.Add(MakeStep(fromRoof ? BotAction.RoofJump : BotAction.Jump,
                            target, SafeMargin, JumpEnergyCost, "JumpOver smallAlive"));
                    // SuperJump: перелететь
                    if (energy >= SuperJumpEnergyCost)
                        variants.Add(MakeStep(fromRoof ? BotAction.SuperRoofJump : BotAction.SuperJump,
                            target, SafeMargin, SuperJumpEnergyCost, "SuperJump smallAlive"));
                    break;

                case ObstacleTypeEnum.bigAlive:
                    if (target.Category == ObjectCategory.Target)
                    {
                        // С крыши — атакуем
                        if (energy >= JumpEnergyCost)
                            variants.Add(MakeStep(BotAction.RoofJump, target, SafeMargin,
                                JumpEnergyCost, "RoofJump bigAlive Target"));
                        AddSwitchLaneVariant(variants, target, state);
                    }
                    else
                    {
                        // С дороги — только SuperJump или смена линии
                        AddSwitchLaneVariant(variants, target, state);
                        if (energy >= SuperJumpEnergyCost)
                            variants.Add(MakeStep(fromRoof ? BotAction.SuperRoofJump : BotAction.SuperJump,
                                target, SafeMargin, SuperJumpEnergyCost, "SuperJump bigAlive"));
                    }
                    break;

                case ObstacleTypeEnum.bigNotAlive:
                case ObstacleTypeEnum.mediumNotAlive:
                    AddSwitchLaneVariant(variants, target, state);
                    // Прыжок на крышу только с дороги
                    if (!fromRoof && energy >= JumpEnergyCost)
                        variants.Add(MakeStep(BotAction.Jump, target, SafeMargin,
                            JumpEnergyCost, $"Jump on roof {target.Type}"));
                    break;

                case ObstacleTypeEnum.smallNotAliveRoad:
                    AddSwitchLaneVariant(variants, target, state);
                    if (energy >= JumpEnergyCost)
                        variants.Add(MakeStep(fromRoof ? BotAction.RoofJump : BotAction.Jump,
                            target, SafeMargin, JumpEnergyCost, "Jump smallNotAliveRoad"));
                    if (energy >= SuperJumpEnergyCost)
                        variants.Add(MakeStep(fromRoof ? BotAction.SuperRoofJump : BotAction.SuperJump,
                            target, SafeMargin, SuperJumpEnergyCost, "SuperJump smallNotAliveRoad"));
                    break;

                case ObstacleTypeEnum.smallNotAliveRoadAndRoof:
                    AddSwitchLaneVariant(variants, target, state);
                    if (energy >= JumpEnergyCost)
                        variants.Add(MakeStep(fromRoof ? BotAction.RoofJump : BotAction.Jump,
                            target, SafeMargin, JumpEnergyCost, "Jump smallNotAliveRoadAndRoof"));
                    if (energy >= SuperJumpEnergyCost)
                        variants.Add(MakeStep(fromRoof ? BotAction.SuperRoofJump : BotAction.SuperJump,
                            target, SafeMargin, SuperJumpEnergyCost, "SuperJump smallNotAliveRoadAndRoof"));
                    break;

                default:
                    // Fallback: смена линии
                    AddSwitchLaneVariant(variants, target, state);
                    break;
            }

            return variants;
        }

        // ══════════════════════════════════════════════
        //  Ульта
        // ══════════════════════════════════════════════

        private bool ShouldConsiderUlta(ProjectedState state)
        {
            if (state.UltaCharge < UltaReadyCharge) return false;

            int nearThreats = 0;
            foreach (var obs in state.RemainingObjects)
            {
                if (obs.Category != ObjectCategory.Threat) continue;
                if (obs.DistanceToHamster < 0 || obs.DistanceToHamster > UltaCheckRange) continue;
                if (!IsOnSameLane(obs, state)) continue;
                nearThreats++;
                if (nearThreats >= UltaMinNearThreats) return true;
            }
            return false;
        }

        private void TryUltaVariant(
            List<ObstacleInfo> objects,
            ProjectedState state,
            List<ChainStep> currentSteps,
            int depthLeft)
        {
            var ultaStep = new ChainStep(BotAction.UseUlta, -1, 0, 0,
                $"Ulta: cluster threats");

            var nextState     = _projector.Project(state, ultaStep, null);
            var updatedObjects = BuildUpdatedObjects(objects, nextState, ultaStep, null);

            var newSteps = new List<ChainStep>(currentSteps) { ultaStep };

            if (depthLeft > 1)
                Recurse(updatedObjects, nextState, newSteps, depthLeft - 1);
            else
                _results.Add(BuildCandidate(newSteps, nextState));
        }

        // ══════════════════════════════════════════════
        //  Collectible-вариант
        // ══════════════════════════════════════════════

        private void TryCollectibleVariant(
            List<ObstacleInfo> objects,
            ProjectedState state,
            List<ChainStep> currentSteps,
            int depthLeft)
        {
            if (_results.Count >= MaxCandidates) return;

            // Найти лучший бонус на другой линии
            ObstacleInfo? bestBonus = null;
            int bestPriority = 0;

            foreach (var obs in objects)
            {
                if (obs.Category != ObjectCategory.Bonus) continue;
                if (obs.LeftX < state.ApproxX - 0.5f) continue;
                if (IsOnSameLane(obs, state)) continue; // на текущей линии хомяк соберёт сам
                if (obs.CollectiblePriority <= bestPriority) continue;
                if (!IsOtherLaneSafe(obs, objects, state)) continue;

                bestBonus = obs;
                bestPriority = obs.CollectiblePriority;
            }

            if (!bestBonus.HasValue || bestPriority < 2) return; // монеты не стоят смены линии

            var switchStep = MakeStep(BotAction.SwitchLane, bestBonus.Value,
                SafeMargin + 1f, 0, $"Collect bonus {bestBonus.Value.Type} p={bestPriority}");

            var nextState     = _projector.Project(state, switchStep, bestBonus);
            var updatedObjects = BuildUpdatedObjects(objects, nextState, switchStep, bestBonus);

            var newSteps = new List<ChainStep>(currentSteps) { switchStep };

            _results.Add(BuildCandidate(newSteps, nextState));
        }

        // ══════════════════════════════════════════════
        //  Вспомогательные
        // ══════════════════════════════════════════════

        private void AddSwitchLaneVariant(List<ChainStep> variants, ObstacleInfo target, ProjectedState state)
        {
            variants.Add(MakeStep(BotAction.SwitchLane, target, SwitchLaneExecuteDistance, 0,
                $"SwitchLane evade {target.Type}"));
        }

        private static ChainStep MakeStep(
            BotAction action, ObstacleInfo target,
            float execDist, int energyCost, string reason)
        {
            return new ChainStep(action, -1, execDist, energyCost, reason)
            {
                TargetObstacle = target
            };
        }

        private static ChainCandidate BuildCandidate(List<ChainStep> steps, ProjectedState finalState)
        {
            int totalEnergy = 0;
            int targets     = 0;
            int collectibles = 0;

            foreach (var s in steps)
            {
                totalEnergy += s.EnergyCost;
                if (s.TargetObstacle.HasValue)
                {
                    var t = s.TargetObstacle.Value;
                    if (t.Category == ObjectCategory.Target)   targets++;
                    if (t.Category == ObjectCategory.Bonus)    collectibles++;
                }
            }

            return new ChainCandidate
            {
                Steps               = new List<ChainStep>(steps),
                FinalState          = finalState.Clone(),
                TotalEnergyCost     = totalEnergy,
                AllStepsSafe        = true, // в генераторе отсекаем небезопасные
                TargetsDestroyed    = targets,
                CollectiblesGathered = collectibles,
                Score               = 0f    // заполнит ChainScorer
            };
        }

        /// <summary>
        /// Формирует обновлённый список объектов: удаляет уже обработанные
        /// (те, что StateProjector убрал из RemainingObjects).
        /// </summary>
        private static List<ObstacleInfo> BuildUpdatedObjects(
            List<ObstacleInfo> original,
            ProjectedState nextState,
            ChainStep step,
            ObstacleInfo? target)
        {
            // RemainingObjects в nextState уже содержит актуальный список после проекции
            // Используем его как основу, но оставляем стабильные ID из оригинала
            var remaining = nextState.RemainingObjects;
            var idSet = new HashSet<int>(remaining.Count);
            foreach (var o in remaining) idSet.Add(o.StableId);

            var result = new List<ObstacleInfo>(remaining.Count);
            // Сохраняем порядок из original (уже отсортированы по LeftX)
            foreach (var o in original)
            {
                if (idSet.Contains(o.StableId))
                    result.Add(o);
            }
            return result;
        }

        private static bool IsOnSameLane(ObstacleInfo obs, ProjectedState state)
        {
            if (state.OnRoof)
                return obs.IsOnRoof;
            bool hamsterIsTop = !state.OnBottom;
            return !obs.IsOnRoof && obs.IsTopLane == hamsterIsTop;
        }

        /// <summary>
        /// Checks that there are no threats between the hamster's current position
        /// and the landing position after a SwitchLane on the destination lane.
        /// Jumpable threats (small obstacles) only block the immediate landing zone —
        /// the chain generator will add a Jump for them in subsequent steps.
        /// Unjumpable threats (big obstacles) use an extended buffer since they
        /// cannot be resolved by follow-up actions.
        /// </summary>
        private static bool IsSwitchLanePathSafe(float preStepX, ProjectedState postState)
        {
            foreach (var obs in postState.RemainingObjects)
            {
                if (obs.Category != ObjectCategory.Threat) continue;
                if (!IsOnSameLane(obs, postState)) continue;

                // Jumpable threats: only block immediate landing zone (chain will handle them)
                // Unjumpable threats: extended buffer — no follow-up can save us
                float aheadBuffer = IsJumpableType(obs.Type) ? 0.3f : SafeMargin;

                if (obs.RightX >= preStepX - 0.3f && obs.LeftX <= postState.ApproxX + aheadBuffer)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Returns true if the obstacle type can be jumped over by the hamster.
        /// </summary>
        private static bool IsJumpableType(ObstacleTypeEnum type)
        {
            return type == ObstacleTypeEnum.smallNotAliveRoad
                || type == ObstacleTypeEnum.smallNotAliveRoadAndRoof
                || type == ObstacleTypeEnum.smallAlive;
        }

        private static bool IsOtherLaneSafe(
            ObstacleInfo bonus,
            List<ObstacleInfo> objects,
            ProjectedState state)
        {
            bool otherIsTop = state.OnBottom; // если хомяк внизу, то другая — верхняя
            float checkFrom = bonus.LeftX - 1.14f; // LaneSwitchTravel
            float checkTo   = bonus.RightX + 1.14f;

            foreach (var obs in objects)
            {
                if (obs.Category != ObjectCategory.Threat &&
                    obs.Category != ObjectCategory.Target) continue;
                if (obs.IsOnRoof) continue;
                if (obs.IsTopLane != otherIsTop) continue;
                if (obs.RightX > checkFrom && obs.LeftX < checkTo) return false;
            }
            return true;
        }
    }
}
