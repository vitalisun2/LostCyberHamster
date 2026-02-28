using System.Collections.Generic;
using System.Text;
using Assets.Scripts.Common.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Фаза хомяка в симуляции.
    /// </summary>
    public enum SimPhase
    {
        Running,
        Jumping,
        OnRoof,
        Protected
    }

    /// <summary>
    /// Легковесный снимок мира для forward simulation.
    /// Никакой физики, никаких GameObjects — чистая арифметика.
    /// </summary>
    public struct SimWorldState
    {
        // ──── Hamster ────
        public bool IsOnBottomLine;
        public SimPhase Phase;
        public int Energy;
        public int Lives;
        public int UltaCharge;
        public bool IsProtected;

        // ──── Hamster geometry (неизменные за симуляцию) ────
        public float HamsterLeftX;
        public float HamsterRightX;
        public float HamsterCenterX;
        public float HamsterWidth;
        public float HamsterHeight;
        public float JumpShift;
        public float JumpMidY;

        // ──── Obstacles ────
        public List<SimObstacle> Obstacles;

        // ──── Scoring ────
        public float Score;
        public int CoinsCollected;
        public bool IsDead;

        // ──── Debug ────
        public StringBuilder DebugTrace;

        /// <summary>
        /// Создать снимок из текущих данных бота.
        /// </summary>
        public static SimWorldState FromCurrent(
            Assets.Scripts.Gameplay.Hamster hamster,
            IReadOnlyList<ThreatInfo> allThreats,
            BotJumpPredictor jumpPredictor)
        {
            float hLeft = hamster.LeftX;
            float hRight = hamster.RightX;
            float hCenter = hamster.transform.position.x;
            float hWidth = hamster.ColliderWidth;
            float hHeight = hamster.ColliderHeight;

            var state = new SimWorldState
            {
                IsOnBottomLine = hamster.IsOnBottomLine.Value,
                Phase = MapPhase(hamster.HamsterState.Value),
                Energy = hamster.Energy.Value,
                Lives = hamster.Lives.Value,
                UltaCharge = hamster.UltaChargeAmount.Value,
                IsProtected = hamster.IsProtected.Value,
                HamsterLeftX = hLeft,
                HamsterRightX = hRight,
                HamsterCenterX = hCenter,
                HamsterWidth = hWidth,
                HamsterHeight = hHeight,
                JumpShift = jumpPredictor?.JumpShiftDistance ?? 2.5f,
                JumpMidY = jumpPredictor?.JumpMidY ?? 1.5f,
                Score = 0,
                CoinsCollected = 0,
                IsDead = false,
                DebugTrace = new StringBuilder(256),
                Obstacles = new List<SimObstacle>(allThreats.Count)
            };

            for (int i = 0; i < allThreats.Count; i++)
            {
                var t = allThreats[i];
                // WorldLeftX = hamsterRightX + distanceX
                // (distanceX — от правого края хомяка до левого края препятствия)
                float worldLeftX = hRight + t.DistanceX;
                float obsWidth = t.Obstacle != null ? t.Obstacle.ColliderWidth : 0.5f;
                float obsHeight = t.Obstacle != null ? t.Obstacle.ColliderHeight : 0.5f;

                state.Obstacles.Add(new SimObstacle
                {
                    Type = t.Type,
                    WorldLeftX = worldLeftX,
                    Width = obsWidth,
                    Height = obsHeight,
                    IsOnBottomLine = t.IsOnCurrentLane ? state.IsOnBottomLine : !state.IsOnBottomLine,
                    IsCollectable = t.IsCollectable,
                    IsRoofable = t.IsRoofable,
                    IsDangerous = t.IsDangerous,
                    IsSmallAlive = t.IsSmallAlive,
                    Handled = false
                });
            }

            return state;
        }

        /// <summary>
        /// Глубокая копия для ветвления дерева решений.
        /// </summary>
        public SimWorldState Clone()
        {
            var clone = this;
            clone.Obstacles = new List<SimObstacle>(Obstacles.Count);
            for (int i = 0; i < Obstacles.Count; i++)
                clone.Obstacles.Add(Obstacles[i]);
            clone.DebugTrace = new StringBuilder(DebugTrace.ToString());
            return clone;
        }

        /// <summary>
        /// Применяет действие Jump: расходует энергию, вызывает SimJumpPredictor
        /// для каждого препятствия в зоне прыжка на текущей линии.
        /// </summary>
        public void ApplyJump()
        {
            if (Energy < 10 || Phase != SimPhase.Running) return;
            Energy -= 10;

            // Проверяем каждое препятствие на текущей линии в пределах jumpShift
            JumpPrediction bestResult = JumpPrediction.NoHit;
            int bestIdx = -1;

            for (int i = 0; i < Obstacles.Count; i++)
            {
                var obs = Obstacles[i];
                if (obs.Handled) continue;
                if (obs.IsOnBottomLine != IsOnBottomLine) continue;
                if (obs.IsCollectable) continue;

                // Препятствие должно быть впереди хомяка и в пределах jumpShift
                if (obs.WorldLeftX < HamsterLeftX) continue;
                float distFromRight = obs.WorldLeftX - HamsterRightX;
                if (distFromRight > JumpShift + obs.Width) continue;

                var prediction = SimJumpPredictor.Predict(
                    HamsterLeftX, HamsterRightX, HamsterCenterX,
                    HamsterWidth, HamsterHeight,
                    JumpShift, JumpMidY, obs);

                // Приоритет: JumpOnObstacle > JumpOnRoof > JumpOver > Damage > NoHit
                if (prediction > bestResult ||
                    (prediction == bestResult && bestIdx >= 0 &&
                     obs.WorldLeftX < Obstacles[bestIdx].WorldLeftX))
                {
                    bestResult = prediction;
                    bestIdx = i;
                }
            }

            ApplyJumpResult(bestResult, bestIdx);
        }

        /// <summary>
        /// Применяет результат прыжка к состоянию.
        /// </summary>
        private void ApplyJumpResult(JumpPrediction result, int obsIdx)
        {
            switch (result)
            {
                case JumpPrediction.JumpOnObstacle:
                    Score += 50f;
                    if (obsIdx >= 0) MarkHandled(obsIdx);
                    Phase = SimPhase.Running;
                    DebugTrace?.Append($"Jump→OnObstacle(+50) ");
                    break;

                case JumpPrediction.JumpOnRoof:
                    Score += 30f;
                    Phase = SimPhase.OnRoof;
                    // На крыше — не помечаем как handled (хомяк НА нём)
                    DebugTrace?.Append($"Jump→OnRoof(+30) ");
                    break;

                case JumpPrediction.JumpOver:
                    Score += 10f;
                    if (obsIdx >= 0) MarkHandled(obsIdx);
                    Phase = SimPhase.Running;
                    DebugTrace?.Append($"Jump→Over(+10) ");
                    break;

                case JumpPrediction.Damage:
                    Score -= 100f;
                    Lives--;
                    if (obsIdx >= 0) MarkHandled(obsIdx);
                    if (Lives <= 0) { IsDead = true; Score -= 1000f; }
                    Phase = SimPhase.Running;
                    DebugTrace?.Append($"Jump→Damage(-100, lives={Lives}) ");
                    break;

                default: // NoHit
                    Phase = SimPhase.Running;
                    DebugTrace?.Append("Jump→NoHit ");
                    break;
            }
        }

        /// <summary>
        /// Смена полосы.
        /// </summary>
        public void ApplySwitchLane()
        {
            if (Phase != SimPhase.Running) return;
            IsOnBottomLine = !IsOnBottomLine;
            DebugTrace?.Append($"SwitchLane→{(IsOnBottomLine ? "bot" : "top")} ");
        }

        /// <summary>
        /// Ульта: защита + уничтожение опасных впереди.
        /// </summary>
        public void ApplyUlta()
        {
            if (UltaCharge < 100) return;
            UltaCharge = 0;
            IsProtected = true;
            int destroyed = 0;
            for (int i = Obstacles.Count - 1; i >= 0; i--)
            {
                var obs = Obstacles[i];
                if (obs.IsDangerous && !obs.Handled &&
                    obs.WorldLeftX - HamsterRightX < 8f)
                {
                    MarkHandled(i);
                    destroyed++;
                    Score += 10f;
                }
            }
            DebugTrace?.Append($"Ulta(destroyed={destroyed}) ");
        }

        /// <summary>
        /// Продвигает мир вперёд на deltaTime.
        /// Сдвигает препятствия к хомяку, проверяет лобовые столкновения
        /// (хомяк бежит, не прыгает — врезается в опасное).
        /// </summary>
        public void Advance(float deltaTime, float worldSpeed)
        {
            if (IsDead) return;

            float dx = worldSpeed * deltaTime;

            for (int i = Obstacles.Count - 1; i >= 0; i--)
            {
                var obs = Obstacles[i];
                if (obs.Handled) { Obstacles.RemoveAt(i); continue; }

                obs.WorldLeftX -= dx;
                Obstacles[i] = obs;

                // Ушло за хомяка — удалить
                float obsRightX = obs.WorldLeftX + obs.Width;
                if (obsRightX < HamsterLeftX - 1f)
                {
                    Obstacles.RemoveAt(i);
                    continue;
                }

                // Проверяем лобовое столкновение (хомяк бежит, препятствие доехало)
                if (obs.WorldLeftX <= HamsterRightX && obsRightX >= HamsterLeftX)
                {
                    // На другой линии — не задевает
                    if (obs.IsOnBottomLine != IsOnBottomLine) continue;

                    if (obs.IsCollectable)
                    {
                        CoinsCollected++;
                        Score += 5f;
                        Obstacles.RemoveAt(i);
                        continue;
                    }

                    if (obs.IsDangerous && !IsProtected && Phase == SimPhase.Running)
                    {
                        Lives--;
                        Score -= 100f;
                        if (Lives <= 0) { IsDead = true; Score -= 1000f; }
                        Obstacles.RemoveAt(i);
                        DebugTrace?.Append($"Collision({obs.Type},-100) ");
                    }
                }
            }

            // Восстановление энергии (1/sec)
            if (Phase == SimPhase.Running || Phase == SimPhase.OnRoof)
            {
                int restore = (int)(deltaTime); // 1 за каждую целую секунду
                if (restore > 0 && Energy < 100)
                    Energy = Energy + restore > 100 ? 100 : Energy + restore;
            }
        }

        // ──── Helpers ────

        private void MarkHandled(int idx)
        {
            var obs = Obstacles[idx];
            obs.Handled = true;
            Obstacles[idx] = obs;
        }

        private static SimPhase MapPhase(HamsterStateEnum state)
        {
            if (state == HamsterStateEnum.RoofRun)
                return SimPhase.OnRoof;
            // Все Jump-стейты в симуляции считаем Running
            // (бот принимает решение ДО прыжка)
            return SimPhase.Running;
        }
    }

    /// <summary>
    /// Легковесное представление препятствия для симуляции.
    /// </summary>
    public struct SimObstacle
    {
        public ObstacleTypeEnum Type;

        /// <summary>Мировая X-координата левого края коллайдера.</summary>
        public float WorldLeftX;

        /// <summary>Ширина коллайдера.</summary>
        public float Width;

        /// <summary>Высота коллайдера (для bigAlive Y-проверки).</summary>
        public float Height;

        public bool IsOnBottomLine;
        public bool IsCollectable;
        public bool IsRoofable;
        public bool IsDangerous;
        public bool IsSmallAlive;

        /// <summary>Уже обработано в этой ветке симуляции (не проверять повторно).</summary>
        public bool Handled;
    }
}
