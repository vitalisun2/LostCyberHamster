using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpOnFromRoof
{
    /// <summary>
    /// Хранит runtime-дистанции roof-to-road jump-on действия.
    /// </summary>
    internal readonly struct JumpOnFromRoofTravel
    {
        public JumpOnFromRoofTravel(
            float runFromRoofTravel,
            float roofJumpTravel,
            float resolveTravel,
            float actionTravel,
            float resolveFireShiftOffset)
        {
            RunFromRoofTravel = runFromRoofTravel;
            RoofJumpTravel = roofJumpTravel;
            ResolveTravel = resolveTravel;
            ActionTravel = actionTravel;
            ResolveFireShiftOffset = resolveFireShiftOffset;
        }

        /// <summary>
        /// Дистанция автоматического схода с крыши.
        /// </summary>
        public float RunFromRoofTravel { get; }

        /// <summary>
        /// Дистанция roof-jump части, по которой resolver проверяет посадку на крышу.
        /// </summary>
        public float RoofJumpTravel { get; }

        /// <summary>
        /// Дистанция jump-from-roof части, по которой resolver проверяет напрыгивание на obstacle.
        /// </summary>
        public float ResolveTravel { get; }

        /// <summary>
        /// Полная дистанция до завершения jump-on-from-roof action и возврата в Run.
        /// </summary>
        public float ActionTravel { get; }

        /// <summary>
        /// Смещение между первым input action и моментом вызова runtime resolver-а.
        /// </summary>
        public float ResolveFireShiftOffset { get; }

        /// <summary>
        /// Возвращает world-shift до runtime resolver-точки.
        /// </summary>
        public float GetResolveFireShift(float fireShift)
        {
            return fireShift + ResolveFireShiftOffset;
        }
    }

    /// <summary>
    /// Описывает runtime-отличия конкретного roof-to-road jump-on варианта.
    /// </summary>
    internal interface IJumpOnFromRoofPolicy
    {
        /// <summary>
        /// Тип action для конкретного варианта.
        /// </summary>
        BotActionKind ActionKind { get; }

        /// <summary>
        /// Стоимость action в энергии.
        /// </summary>
        int EnergyCost { get; }

        /// <summary>
        /// Префикс человекочитаемого описания action.
        /// </summary>
        string DescriptionPrefix { get; }

        /// <summary>
        /// Тег диагностических сообщений стратегии.
        /// </summary>
        string LogTag { get; }

        /// <summary>
        /// Runtime-состояние, которое подтверждает успешное напрыгивание с крыши.
        /// </summary>
        HamsterStateEnum ExpectedJumpOnState { get; }

        /// <summary>
        /// Возвращает runtime-дистанции для текущего варианта roof-to-road jump-on.
        /// </summary>
        bool TryGetTravel(out JumpOnFromRoofTravel travel);

        /// <summary>
        /// Вызывает runtime roof-jump resolver для конкретного варианта прыжка.
        /// </summary>
        JumpResolveResult Resolve(
            IReadOnlyList<JumpObstacleData> obstacles,
            RoofJumpResolveContext context);
    }
}
