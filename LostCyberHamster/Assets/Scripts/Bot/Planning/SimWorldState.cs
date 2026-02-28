using System.Collections.Generic;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Легковесный снимок мира для forward simulation.
    /// Никакой физики, никаких GameObjects — чистая арифметика.
    /// </summary>
    public struct SimWorldState
    {
        // Hamster
        public bool IsOnBottomLine;
        public HamsterStateEnum HamsterState;
        public int Energy;
        public int Lives;
        public int UltaCharge;
        public bool IsProtected;
        public float PositionX;

        // Obstacles (копия, сдвигаемая по X)
        public List<SimObstacle> Obstacles;

        // Scoring
        public float Score;
        public int CoinsCollected;
        public bool IsDead;

        /// <summary>
        /// Создать снимок из текущих данных бота.
        /// </summary>
        public static SimWorldState FromCurrent(
            Assets.Scripts.Gameplay.Hamster hamster,
            IReadOnlyList<ThreatInfo> allThreats)
        {
            var state = new SimWorldState
            {
                IsOnBottomLine = hamster.IsOnBottomLine.Value,
                HamsterState = hamster.HamsterState.Value,
                Energy = hamster.Energy.Value,
                Lives = hamster.Lives.Value,
                UltaCharge = hamster.UltaChargeAmount.Value,
                IsProtected = hamster.IsProtected.Value,
                PositionX = hamster.transform.position.x,
                Score = 0,
                CoinsCollected = 0,
                IsDead = false,
                Obstacles = new List<SimObstacle>(allThreats.Count)
            };

            for (int i = 0; i < allThreats.Count; i++)
            {
                var t = allThreats[i];
                state.Obstacles.Add(new SimObstacle
                {
                    Type = t.Type,
                    DistanceX = t.DistanceX,
                    IsOnBottomLine = t.IsOnCurrentLane,
                    IsCollectable = t.IsCollectable,
                    IsRoofable = t.IsRoofable,
                    IsDangerous = t.IsDangerous,
                    IsSmallAlive = t.IsSmallAlive
                });
            }

            return state;
        }

        /// <summary>
        /// Глубокая копия для ветвления.
        /// </summary>
        public SimWorldState Clone()
        {
            var clone = this;
            clone.Obstacles = new List<SimObstacle>(Obstacles.Count);
            for (int i = 0; i < Obstacles.Count; i++)
                clone.Obstacles.Add(Obstacles[i]);
            return clone;
        }

        /// <summary>
        /// Продвигает мир вперёд на deltaTime.
        /// Сдвигает препятствия к хомяку, проверяет столкновения.
        /// </summary>
        public void Simulate(float deltaTime, float worldSpeed)
        {
            float dx = worldSpeed * deltaTime;

            for (int i = Obstacles.Count - 1; i >= 0; i--)
            {
                var obs = Obstacles[i];
                obs.DistanceX -= dx;

                if (obs.DistanceX < -1f)
                {
                    Obstacles.RemoveAt(i);
                    continue;
                }

                // Проверяем столкновение (простая проверка: дистанция ≈ 0)
                if (obs.DistanceX <= 0.3f && obs.DistanceX >= -0.3f)
                {
                    if (obs.IsCollectable)
                    {
                        CoinsCollected++;
                        Score += 5f;
                        Obstacles.RemoveAt(i);
                        continue;
                    }

                    if (obs.IsDangerous && !IsProtected)
                    {
                        Lives--;
                        Score -= 50f;
                        if (Lives <= 0)
                        {
                            IsDead = true;
                            Score -= 1000f;
                        }
                        Obstacles.RemoveAt(i);
                        continue;
                    }
                }

                Obstacles[i] = obs;
            }
        }
    }

    /// <summary>
    /// Легковесное представление препятствия для симуляции.
    /// </summary>
    public struct SimObstacle
    {
        public ObstacleTypeEnum Type;
        public float DistanceX;
        public bool IsOnBottomLine;
        public bool IsCollectable;
        public bool IsRoofable;
        public bool IsDangerous;
        public bool IsSmallAlive;
    }
}
