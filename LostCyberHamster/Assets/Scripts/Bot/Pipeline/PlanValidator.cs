using System.Collections.Generic;
using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Bot
{
    public enum PlanDecision { KeepTail, FullRebuild }

    /// <summary>
    /// Проверяет, можно ли сохранить хвост текущего плана, или нужен полный пересчёт.
    /// Четыре критерия: объекты на месте, путь до головы свободен,
    /// нет более приоритетной цели, хвост перепроецировался успешно.
    /// </summary>
    public class PlanValidator
    {
        // Граница "близко" для сравнения целей (новая Threat/Target ближе чем первый шаг)
        private const float NewThreatLookAhead = 0.5f;

        private readonly StateProjector _projector = new StateProjector();

        // ══════════════════════════════════════════════
        //  Публичный API
        // ══════════════════════════════════════════════

        /// <summary>
        /// Возвращает решение: сохранить хвост или перестроить план целиком.
        /// </summary>
        public PlanDecision Validate(BotSceneSnapshot snapshot, CurrentPlan currentPlan)
        {
            // Пустой план → всегда rebuild
            if (currentPlan == null || currentPlan.IsEmpty)
                return PlanDecision.FullRebuild;

            // Хвост — все шаги кроме Head (Head уже исполняется или завершён)
            var tail = currentPlan.GetTail();

            // a) Объекты хвоста всё ещё видны?
            if (!TailTargetsStillVisible(tail, snapshot.VisibleObjects))
                return PlanDecision.FullRebuild;

            // b) Путь до первого шага хвоста не перегорожен новой угрозой?
            if (NewThreatBlocksPathToFirstTailStep(tail, snapshot))
                return PlanDecision.FullRebuild;

            // c) Появилась более приоритетная цель ближе текущего плана?
            if (HigherPriorityTargetAppeared(tail, snapshot))
                return PlanDecision.FullRebuild;

            // d) Хвост перепроецируется безопасно с текущего состояния?
            if (!TailReprojectionSafe(tail, snapshot))
                return PlanDecision.FullRebuild;

            return PlanDecision.KeepTail;
        }

        // ══════════════════════════════════════════════
        //  a) Объекты на месте
        // ══════════════════════════════════════════════

        private static bool TailTargetsStillVisible(
            List<ChainStep> tail,
            List<ObstacleInfo> visibleObjects)
        {
            // Строим set StableId видимых объектов
            var idSet = new HashSet<int>(visibleObjects.Count);
            foreach (var obs in visibleObjects)
                idSet.Add(obs.StableId);

            foreach (var step in tail)
            {
                if (!step.TargetObstacle.HasValue) continue;

                int stableId = step.TargetObstacle.Value.StableId;
                if (stableId == 0) continue; // не отслеживаем объекты без StableId

                if (!idSet.Contains(stableId))
                    return false; // цель исчезла
            }

            return true;
        }

        // ══════════════════════════════════════════════
        //  b) Новая угроза на пути до первого шага хвоста
        // ══════════════════════════════════════════════

        private static bool NewThreatBlocksPathToFirstTailStep(
            List<ChainStep> tail,
            BotSceneSnapshot snapshot)
        {
            if (tail.Count == 0) return false;

            var firstStep = tail[0];
            float distToFirstStep = firstStep.TargetObstacle.HasValue
                ? firstStep.TargetObstacle.Value.DistanceToHamster
                : firstStep.ExecuteAtDistance;

            bool hamsterOnRoof = snapshot.HamsterOnRoof;

            foreach (var obs in snapshot.VisibleObjects)
            {
                if (obs.Category != ObjectCategory.Threat) continue;

                // Угроза спереди, до первого шага хвоста, на той же линии
                if (obs.DistanceToHamster < 0) continue;
                if (obs.DistanceToHamster > distToFirstStep + NewThreatLookAhead) continue;

                // Проверяем линию
                if (hamsterOnRoof && !obs.IsOnRoof) continue;
                if (!hamsterOnRoof && obs.IsOnRoof) continue;

                return true; // новая угроза на пути
            }

            return false;
        }

        // ══════════════════════════════════════════════
        //  c) Более приоритетная цель
        // ══════════════════════════════════════════════

        private static bool HigherPriorityTargetAppeared(
            List<ChainStep> tail,
            BotSceneSnapshot snapshot)
        {
            // Находим расстояние до ближайшей цели в текущем хвосте
            float currentPlanTargetDist = float.MaxValue;
            foreach (var step in tail)
            {
                if (!step.TargetObstacle.HasValue) continue;
                if (step.TargetObstacle.Value.Category != ObjectCategory.Target) continue;

                float d = step.TargetObstacle.Value.DistanceToHamster;
                if (d < currentPlanTargetDist)
                    currentPlanTargetDist = d;
            }

            if (currentPlanTargetDist == float.MaxValue) return false; // плана с целями нет

            // Ищем новую Target ближе, чем в текущем плане
            bool hamsterOnRoof = snapshot.HamsterOnRoof;
            foreach (var obs in snapshot.VisibleObjects)
            {
                if (obs.Category != ObjectCategory.Target) continue;
                if (obs.DistanceToHamster < 0) continue;

                // Должна быть достижима с текущей линии
                bool onSameLane = hamsterOnRoof ? obs.IsOnRoof : !obs.IsOnRoof;
                if (!onSameLane) continue;

                if (obs.DistanceToHamster < currentPlanTargetDist)
                    return true; // есть ближняя цель, которой нет в плане
            }

            return false;
        }

        // ══════════════════════════════════════════════
        //  d) Перепроекция хвоста
        // ══════════════════════════════════════════════

        private bool TailReprojectionSafe(List<ChainStep> tail, BotSceneSnapshot snapshot)
        {
            if (tail.Count == 0) return true;

            var state = ProjectedState.FromSnapshot(snapshot);

            foreach (var step in tail)
            {
                var obsOpt = step.TargetObstacle;
                var nextState = _projector.Project(state, step, obsOpt.HasValue ? obsOpt : null);

                if (!_projector.IsSafeAfterProjection(nextState))
                    return false;

                state = nextState;
            }

            return true;
        }
    }
}
