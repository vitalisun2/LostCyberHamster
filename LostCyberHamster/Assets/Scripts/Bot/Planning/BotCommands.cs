using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Конкретные команды для forward simulation.
    /// Каждая делегирует в SimWorldState.Apply*() методы.
    /// </summary>
    public static class BotCommands
    {
        /// <summary>
        /// Прыжок: тратит 10 энергии, вызывает SimJumpPredictor для каждого
        /// препятствия в зоне прыжка. Определяет исход: JumpOnObstacle, JumpOver,
        /// JumpOnRoof или Damage.
        /// </summary>
        public struct JumpCommand : IBotCommand
        {
            public BotAction Action => BotAction.Jump;

            public bool CanExecute(ref SimWorldState state)
            {
                return state.Energy >= 10 &&
                       !state.IsDead &&
                       state.Phase == SimPhase.Running;
            }

            public void Execute(ref SimWorldState state)
            {
                state.ApplyJump();
            }
        }

        /// <summary>
        /// Суперпрыжок: тратит 20 энергии. В симуляции упрощённо —
        /// удаляем ближайшее опасное (superJump перелетает всё).
        /// </summary>
        public struct SuperJumpCommand : IBotCommand
        {
            public BotAction Action => BotAction.SuperJump;

            public bool CanExecute(ref SimWorldState state)
            {
                return state.Energy >= 20 &&
                       !state.IsDead &&
                       state.Phase == SimPhase.Running;
            }

            public void Execute(ref SimWorldState state)
            {
                state.Energy -= 20;
                // SuperJump перелетает всё — помечаем ближайшее опасное как handled
                int nearIdx = -1;
                float nearDist = float.MaxValue;
                for (int i = 0; i < state.Obstacles.Count; i++)
                {
                    var obs = state.Obstacles[i];
                    if (obs.Handled) continue;
                    if (obs.IsOnBottomLine != state.IsOnBottomLine) continue;
                    if (!obs.IsDangerous) continue;
                    float dist = obs.WorldLeftX - state.HamsterRightX;
                    if (dist > 0 && dist < nearDist)
                    {
                        nearDist = dist;
                        nearIdx = i;
                    }
                }
                if (nearIdx >= 0)
                {
                    var obs = state.Obstacles[nearIdx];
                    obs.Handled = true;
                    state.Obstacles[nearIdx] = obs;
                    state.Score += 15f;
                }
                state.DebugTrace?.Append("SuperJump ");
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
                       state.Phase == SimPhase.Running;
            }

            public void Execute(ref SimWorldState state)
            {
                state.ApplySwitchLane();
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
                state.ApplyUlta();
            }
        }

        /// <summary>
        /// Ничего не делать — просто ждать. Мир продвигается в Advance().
        /// </summary>
        public struct DoNothingCommand : IBotCommand
        {
            public BotAction Action => BotAction.None;

            public bool CanExecute(ref SimWorldState state) => !state.IsDead;

            public void Execute(ref SimWorldState state)
            {
                state.DebugTrace?.Append("Wait ");
            }
        }
    }
}
