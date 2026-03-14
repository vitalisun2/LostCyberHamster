using System.Collections.Generic;
using Assets.Scripts.Common.Models;
using UnityEngine;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Вычисляет ProjectedState — состояние хомяка после выполнения одного шага.
    /// Не обращается к Unity-объектам, работает только со snapshot-данными.
    /// Используется ChainGenerator'ом для симуляции последствий каждого варианта.
    /// </summary>
    public class StateProjector
    {
        // ──────────────── Константы движения ────────────────

        private const float LaneSwitchDuration      = 0.3f;
        private const float LaneSwitchTravel         = LaneSwitchDuration * Assets.Scripts.Consts.GameSpeedBase;

        private const float JumpLandingTravel        = 3.8f;   // ~1с * 3.8 m/s
        private const float SuperJumpLandingTravel   = 4.6f;   // ~1.2с * 3.8 m/s

        // JumpOn SmallAlive: отскок (1.817 - 0.85) * 3.8 ≈ 3.5 юн. (хомяк неуязвим)
        private const float JumpOnBounceTravel       = 3.5f;

        // Зона уязвимости при автоспуске с крыши (~0.5с * 3.8)
        private const float RunFromRoofVulnerability = 1.9f;

        // Максимальный зазор между двумя крышами для автоперехода
        private const float RoofAutoTransferGap      = 0.3f;

        private const int JumpEnergyCost             = 10;
        private const int SuperJumpEnergyCost        = 20;

        // ══════════════════════════════════════════════
        //  Основной метод
        // ══════════════════════════════════════════════

        /// <summary>
        /// Вычисляет новое ProjectedState после выполнения <paramref name="step"/> в <paramref name="current"/>.
        /// Параметр <paramref name="target"/> — объект, на который направлен шаг (null для UseUlta/SwitchLane без цели).
        /// Возвращает null, если шаг приводит к гибели хомяка (небезопасное приземление).
        /// </summary>
        public ProjectedState Project(ProjectedState current, ChainStep step, ObstacleInfo? target)
        {
            var next = current.Clone();

            switch (step.Action)
            {
                case BotAction.SwitchLane:
                    ProjectSwitchLane(next);
                    break;

                case BotAction.Jump:
                    ProjectJump(next, target, isSuper: false, fromRoof: false);
                    break;

                case BotAction.SuperJump:
                    ProjectJump(next, target, isSuper: true, fromRoof: false);
                    break;

                case BotAction.RoofJump:
                    ProjectJump(next, target, isSuper: false, fromRoof: true);
                    break;

                case BotAction.SuperRoofJump:
                    ProjectJump(next, target, isSuper: true, fromRoof: true);
                    break;

                case BotAction.UseUlta:
                    ProjectUlta(next);
                    break;
            }

            // После каждого прыжка на крышу — проверяем автоматические переходы
            if (next.OnRoof)
                ResolveRoofAutoTransitions(next);

            return next;
        }

        // ══════════════════════════════════════════════
        //  Смена линии
        // ══════════════════════════════════════════════

        private static void ProjectSwitchLane(ProjectedState state)
        {
            state.OnBottom = !state.OnBottom;
            state.ApproxX += LaneSwitchTravel;
            // Энергия не тратится
        }

        // ══════════════════════════════════════════════
        //  Прыжки
        // ══════════════════════════════════════════════

        private static void ProjectJump(
            ProjectedState state, ObstacleInfo? target,
            bool isSuper, bool fromRoof)
        {
            float travel = isSuper ? SuperJumpLandingTravel : JumpLandingTravel;
            int   cost   = isSuper ? SuperJumpEnergyCost    : JumpEnergyCost;

            state.Energy = Mathf.Max(0, state.Energy - cost);

            if (target.HasValue)
            {
                var t = target.Value;

                // JumpOn SmallAlive с дороги: приземление НА цель → отскок
                bool isJumpOn = t.Type == ObstacleTypeEnum.smallAlive && !fromRoof && !isSuper;
                if (isJumpOn)
                {
                    // Приземление после отскока: target.RightX + JumpOnBounceTravel
                    state.ApproxX = t.RightX + JumpOnBounceTravel;
                    // Хомяк возвращается на ту же линию, с которой прыгал (дорога)
                    state.OnRoof = false;
                    RemoveFromRemaining(state, t);
                    return;
                }

                // Запрыгивание на крышу BigNotAlive/MediumNotAlive
                bool isJumpOnRoof = !fromRoof && !isSuper &&
                    (t.Type == ObstacleTypeEnum.bigNotAlive ||
                     t.Type == ObstacleTypeEnum.mediumNotAlive);
                if (isJumpOnRoof)
                {
                    state.ApproxX = t.LeftX + travel;
                    state.OnRoof  = true;
                    state.OnBottom = t.IsTopLane ? false : true; // сохраняем принадлежность линии
                    return;
                }

                // Перепрыгивание через объект (JumpOver): приземляемся за целью
                state.ApproxX = t.RightX + (travel * 0.4f); // приблизительно за целью
                if (t.Category == ObjectCategory.Target)
                    RemoveFromRemaining(state, t);
            }
            else
            {
                // Нет конкретной цели — просто движемся вперёд
                state.ApproxX += travel;
            }

            // Прыжок с крыши возвращает на дорогу
            if (fromRoof)
                state.OnRoof = false;
        }

        // ══════════════════════════════════════════════
        //  Ульта
        // ══════════════════════════════════════════════

        private static void ProjectUlta(ProjectedState state)
        {
            state.UltaCharge = 0;

            // Удаляем все Threat-объекты в ближайшей зоне (примерно 6 юнитов)
            state.RemainingObjects.RemoveAll(o =>
                  (o.Category == ObjectCategory.Threat || o.Category == ObjectCategory.Target) &&
                o.DistanceToHamster >= 0 &&
                o.DistanceToHamster <= 6f);
        }

        // ══════════════════════════════════════════════
        //  Автоматические переходы на крыше
        // ══════════════════════════════════════════════

        /// <summary>
        /// Проверяет и применяет автоматические переходы:
        /// a) RunFromRoof — автоспуск с крыши когда хомяк доходит до правого края
        /// b) Автопереход на следующую крышу, если gap достаточно мал
        /// </summary>
        private static void ResolveRoofAutoTransitions(ProjectedState state)
        {
            // Найти крышу под хомяком (ближайший roof-объект)
            ObstacleInfo? currentRoof = FindCurrentRoof(state);
            if (!currentRoof.HasValue)
            {
                // Крыша не найдена — спускаемся
                state.OnRoof = false;
                return;
            }

            var roof = currentRoof.Value;

            // Хомяк ещё на крыше — проверяем автопереход на следующую крышу
            if (state.ApproxX <= roof.RightX)
            {
                ObstacleInfo? nextRoof = FindNextRoof(state, roof);
                if (nextRoof.HasValue)
                {
                    float gap = nextRoof.Value.LeftX - roof.RightX;
                    if (gap <= RoofAutoTransferGap)
                    {
                        // Автопереход: бесплатно и безопасно
                        // Хомяк остаётся на крыше — ничего не меняем в OnRoof
                    }
                }
                return; // остаёмся на крыше
            }

            // ApproxX > roof.RightX → хомяк сошёл с крыши: RunFromRoof
            state.OnRoof  = false;
            state.ApproxX = roof.RightX + RunFromRoofVulnerability;
            // RemainingObjects не изменяем — проверку зоны уязвимости делает IsSafeAfterProjection
        }

        // ══════════════════════════════════════════════
        //  Проверка безопасности
        // ══════════════════════════════════════════════

        /// <summary>
        /// Проверяет, нет ли Threat непосредственно по траектории хомяка
        /// в проецируемом состоянии. Учитывает иммунные зоны.
        /// </summary>
        public bool IsSafeAfterProjection(ProjectedState state)
        {
            // ApproxX - это правый край хомяка (Hamster.RightX) в конечной точке
            float rightBound = state.ApproxX;
            float leftBound = state.ApproxX - state.HamsterWidth;

            foreach (var obs in state.RemainingObjects)
            {
                  if (obs.Category != ObjectCategory.Threat && obs.Category != ObjectCategory.Target) continue;

                // Объект на той же линии?
                if (!IsOnSameLane(obs, state)) continue;

                // Пересечение баундинг-боксов
                if (Assets.Scripts.Common.CollisionUtils.IsOverlap(leftBound, rightBound, obs.LeftX, obs.RightX))
                    return false;
            }
            return true;
        }

        // ══════════════════════════════════════════════
        //  Вспомогательные
        // ══════════════════════════════════════════════

        private static ObstacleInfo? FindCurrentRoof(ProjectedState state)
        {
            ObstacleInfo? found = null;
            foreach (var obs in state.RemainingObjects)
            {
                if (!obs.IsOnRoof) continue;
                if (obs.Type != ObstacleTypeEnum.bigNotAlive &&
                    obs.Type != ObstacleTypeEnum.mediumNotAlive) continue;

                // Хомяк находится над этим объектом?
                if (state.ApproxX >= obs.LeftX && state.ApproxX <= obs.RightX + 0.5f)
                {
                    if (!found.HasValue || obs.LeftX < found.Value.LeftX)
                        found = obs;
                }
            }
            return found;
        }

        private static ObstacleInfo? FindNextRoof(ProjectedState state, ObstacleInfo currentRoof)
        {
            ObstacleInfo? found = null;
            foreach (var obs in state.RemainingObjects)
            {
                if (!obs.IsOnRoof) continue;
                if (obs.Type != ObstacleTypeEnum.bigNotAlive &&
                    obs.Type != ObstacleTypeEnum.mediumNotAlive) continue;
                if (obs.IsTopLane != currentRoof.IsTopLane) continue;
                if (obs.LeftX <= currentRoof.RightX) continue; // должна быть СЛЕДУЮЩЕЙ

                if (!found.HasValue || obs.LeftX < found.Value.LeftX)
                    found = obs;
            }
            return found;
        }

        private static void RemoveFromRemaining(ProjectedState state, ObstacleInfo target)
        {
            for (int i = state.RemainingObjects.Count - 1; i >= 0; i--)
            {
                if (state.RemainingObjects[i].StableId == target.StableId)
                {
                    state.RemainingObjects.RemoveAt(i);
                    return;
                }
            }
        }

        private static bool IsOnSameLane(ObstacleInfo obs, ProjectedState state)
        {
            if (state.OnRoof)
                return obs.IsOnRoof;

            bool hamsterIsTop = !state.OnBottom;
            return !obs.IsOnRoof && obs.IsTopLane == hamsterIsTop;
        }
    }
}
