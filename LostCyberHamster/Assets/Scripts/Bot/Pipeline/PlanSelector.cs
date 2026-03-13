using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Собирает итоговый CurrentPlan из решения PlanValidator и списка кандидатов.
    /// Два режима: KeepTail (хвост + новые шаги) и FullRebuild (лучший кандидат целиком).
    /// Не обращается к Unity-объектам — работает только с данными pipeline.
    /// </summary>
    public class PlanSelector
    {
        // ══════════════════════════════════════════════
        //  Публичный API
        // ══════════════════════════════════════════════

        /// <summary>
        /// Формирует обновлённый план.
        /// </summary>
        /// <param name="decision">Решение PlanValidator: KeepTail или FullRebuild.</param>
        /// <param name="currentPlan">Текущий план (может быть null или пустым).</param>
        /// <param name="candidates">Оценённые ChainScorer'ом кандидаты (отсортированы по убыванию Score).
        /// При KeepTail — сгенерированы с ProjectedState конца хвоста;
        /// при FullRebuild — с начального ProjectedState.</param>
        public CurrentPlan Select(
            PlanDecision decision,
            CurrentPlan currentPlan,
            List<ChainCandidate> candidates)
        {
            if (decision == PlanDecision.KeepTail)
                return BuildKeepTailPlan(currentPlan, candidates);

            return BuildFullRebuildPlan(candidates);
        }

        // ══════════════════════════════════════════════
        //  KeepTail
        // ══════════════════════════════════════════════

        private static CurrentPlan BuildKeepTailPlan(
            CurrentPlan currentPlan,
            List<ChainCandidate> candidates)
        {
            var plan = new CurrentPlan
            {
                Strategy = "keep-tail"
            };

            // Сохраняем хвост (все шаги кроме выполненных и головы InProgress)
            if (currentPlan != null && !currentPlan.IsEmpty)
            {
                var tail = currentPlan.GetTail();
                foreach (var step in tail)
                {
                    if (step.Status == ChainStepStatus.Completed) continue;
                    plan.Steps.Add(step);
                }

                // Голова может всё ещё исполняться — держим её первой
                var head = currentPlan.Head;
                if (head != null && head.Status != ChainStepStatus.Completed)
                    plan.Steps.Insert(0, head);
            }

            // Достраиваем шаги лучшего кандидата в конец хвоста
            AppendBestCandidateSteps(plan, candidates, "keep-tail+extend");
            return plan;
        }

        // ══════════════════════════════════════════════
        //  FullRebuild
        // ══════════════════════════════════════════════

        private static CurrentPlan BuildFullRebuildPlan(List<ChainCandidate> candidates)
        {
            var plan = new CurrentPlan
            {
                Strategy = "rebuild"
            };

            if (candidates == null || candidates.Count == 0)
            {
                Debug.LogWarning("[PlanSelector] No safe candidates — plan is empty (no safe path).");
                plan.Strategy = "rebuild:no-candidates";
                return plan;
            }

            var best = candidates[0]; // ChainScorer уже отсортировал по убыванию Score
            foreach (var step in best.Steps)
                plan.Steps.Add(step);

            plan.Strategy = $"rebuild:score={best.Score:F1}";
            return plan;
        }

        // ══════════════════════════════════════════════
        //  Вспомогательный
        // ══════════════════════════════════════════════

        private static void AppendBestCandidateSteps(
            CurrentPlan plan,
            List<ChainCandidate> candidates,
            string strategyTag)
        {
            if (candidates == null || candidates.Count == 0) return;

            var best = candidates[0];
            foreach (var step in best.Steps)
                plan.Steps.Add(step);

            plan.Strategy = $"{strategyTag}:score={best.Score:F1}";
        }
    }
}
