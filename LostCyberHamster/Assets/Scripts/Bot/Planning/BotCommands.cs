using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Конкретные команды для forward simulation.
    /// Каждая модифицирует SimWorldState арифметически.
    /// </summary>
    public static class BotCommands
    {
        /// <summary>
        /// Прыжок: тратит 10 энергии, перескакивает ближайшее мелкое
        /// или запрыгивает на roofable.
        /// </summary>
        public struct JumpCommand : IBotCommand
        {
            public BotAction Action => BotAction.Jump;

            public bool CanExecute(ref SimWorldState state)
            {
                return state.Energy >= 10 &&
                       !state.IsDead &&
                       (state.HamsterState == HamsterStateEnum.Run ||
                        state.HamsterState == HamsterStateEnum.RoofRun);
            }

            public void Execute(ref SimWorldState state)
            {
                state.Energy -= 10;
                state.HamsterState = HamsterStateEnum.Jump;
                // Упрощённо: удаляем ближайшую опасность на текущей линии
                RemoveNearestOnCurrentLane(ref state);
            }
        }

        /// <summary>
        /// Суперпрыжок: тратит 20 энергии, перелетает больше.
        /// </summary>
        public struct SuperJumpCommand : IBotCommand
        {
            public BotAction Action => BotAction.SuperJump;

            public bool CanExecute(ref SimWorldState state)
            {
                return state.Energy >= 20 &&
                       !state.IsDead &&
                       IsInJumpState(state.HamsterState);
            }

            public void Execute(ref SimWorldState state)
            {
                state.Energy -= 20;
                state.HamsterState = HamsterStateEnum.SuperJump;
                RemoveNearestOnCurrentLane(ref state);
            }
        }

        /// <summary>
        /// Смена линии: переключает IsOnBottomLine.
        /// </summary>
        public struct SwitchLaneCommand : IBotCommand
        {
            public BotAction Action => BotAction.SwitchLane;

            public bool CanExecute(ref SimWorldState state)
            {
                return !state.IsDead &&
                       (state.HamsterState == HamsterStateEnum.Run);
            }

            public void Execute(ref SimWorldState state)
            {
                state.IsOnBottomLine = !state.IsOnBottomLine;
            }
        }

        /// <summary>
        /// Ульта: уничтожает всё впереди, тратит 100% заряда.
        /// </summary>
        public struct UseUltaCommand : IBotCommand
        {
            public BotAction Action => BotAction.UseUlta;

            public bool CanExecute(ref SimWorldState state)
            {
                return state.UltaCharge >= 100 && !state.IsDead;
            }

            public void Execute(ref SimWorldState state)
            {
                state.UltaCharge = 0;
                state.IsProtected = true;
                // Уничтожаем все опасные в зоне
                for (int i = state.Obstacles.Count - 1; i >= 0; i--)
                {
                    if (state.Obstacles[i].IsDangerous && state.Obstacles[i].DistanceX < 8f)
                    {
                        state.Score += 10f;
                        state.Obstacles.RemoveAt(i);
                    }
                }
            }
        }

        /// <summary>
        /// Ничего не делать — просто ждать.
        /// </summary>
        public struct DoNothingCommand : IBotCommand
        {
            public BotAction Action => BotAction.None;

            public bool CanExecute(ref SimWorldState state) => !state.IsDead;

            public void Execute(ref SimWorldState state)
            {
                // Ничего — мир продвигается сам в Simulate()
            }
        }

        // ──────────────── Helpers ────────────────

        private static void RemoveNearestOnCurrentLane(ref SimWorldState state)
        {
            int nearIdx = -1;
            float nearDist = float.MaxValue;

            for (int i = 0; i < state.Obstacles.Count; i++)
            {
                var obs = state.Obstacles[i];
                if (obs.DistanceX > 0 && obs.DistanceX < nearDist &&
                    obs.IsOnBottomLine == state.IsOnBottomLine)
                {
                    nearDist = obs.DistanceX;
                    nearIdx = i;
                }
            }

            if (nearIdx >= 0)
            {
                var obs = state.Obstacles[nearIdx];
                if (obs.IsCollectable)
                    state.CoinsCollected++;

                state.Score += obs.IsDangerous ? 20f : 5f;
                state.Obstacles.RemoveAt(nearIdx);
            }
        }

        private static bool IsInJumpState(HamsterStateEnum state)
        {
            return state == HamsterStateEnum.Jump ||
                   state == HamsterStateEnum.JumpOver ||
                   state == HamsterStateEnum.JumpOnObstacle ||
                   state == HamsterStateEnum.JumpOnRoof;
        }
    }
}
