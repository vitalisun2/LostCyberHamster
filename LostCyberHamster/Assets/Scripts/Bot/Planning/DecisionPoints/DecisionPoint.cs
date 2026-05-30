using System;
using Assets.Scripts.Bot.Perception;

namespace Assets.Scripts.Bot.Planning.DecisionPoints
{
    /// <summary>
    /// Описывает текущую planning-ситуацию перед ботом.
    /// </summary>
    public sealed class DecisionPoint
    {
        /// <summary>
        /// Создаёт новую точку решения из готовой chain.
        /// </summary>
        public DecisionPoint(ObstacleChain chain)
            : this(chain, DecisionPointKind.BlockingThreat, isDecisionRequired: true, null)
        {
        }

        /// <summary>
        /// Создаёт новую точку решения из готовой chain и причины её появления.
        /// </summary>
        public DecisionPoint(ObstacleChain chain, DecisionPointKind kind)
            : this(chain, kind, isDecisionRequired: true, null)
        {
        }

        /// <summary>
        /// Создаёт новую точку решения из готовой chain, её типа и обязательности обработки.
        /// </summary>
        public DecisionPoint(ObstacleChain chain, DecisionPointKind kind, bool isDecisionRequired)
            : this(chain, kind, isDecisionRequired, null)
        {
        }

        /// <summary>
        /// Создаёт новую точку решения из готовой chain, причины её появления и optional fire-deadline.
        /// </summary>
        public DecisionPoint(
            ObstacleChain chain,
            DecisionPointKind kind,
            bool isDecisionRequired,
            ObstacleSnapshot fireBeforeObstacle)
        {
            Chain = chain ?? throw new ArgumentNullException(nameof(chain));
            Kind = kind;
            IsDecisionRequired = isDecisionRequired;
            FireBeforeObstacle = fireBeforeObstacle;
        }

        public ObstacleChain Chain { get; }
        public DecisionPointKind Kind { get; }

        /// <summary>
        /// Возвращает true, если planner не должен оставлять эту ситуацию без действия.
        /// </summary>
        public bool IsDecisionRequired { get; }

        /// <summary>
        /// Возвращает true, если optional jump-on objective требует раннее окно смены линии.
        /// </summary>
        public bool UsesObjectiveSwitchLaneTiming => !IsDecisionRequired
            && (Kind == DecisionPointKind.GroundJumpOnTarget
                || Kind == DecisionPointKind.RoofJumpOnTarget);

        public ObstacleSnapshot FireBeforeObstacle { get; }
        public bool HasFireBeforeObstacle => FireBeforeObstacle != null;

        /// <summary>
        /// Временный compatibility-доступ к первому obstacle chain.
        /// </summary>
        public ObstacleSnapshot Obstacle => Chain?.FirstObstacle;

        /// <summary>
        /// Временный compatibility-доступ к world index первого obstacle chain.
        /// </summary>
        public int ObstacleIndex => Chain?.FirstIndex ?? -1;

        /// <summary>
        /// Временный compatibility-доступ к первой крыше внутри chain.
        /// </summary>
        public ObstacleSnapshot RoofLandingObstacle
        {
            get
            {
                return Chain != null && Chain.TryFindFirstRoof(out ObstacleSnapshot roofObstacle, out _, out _)
                    ? roofObstacle
                    : null;
            }
        }

        /// <summary>
        /// Временный compatibility-доступ к world index первой крыши внутри chain.
        /// </summary>
        public int RoofLandingObstacleIndex
        {
            get
            {
                return Chain != null && Chain.TryFindFirstRoof(out _, out int roofWorldIndex, out _)
                    ? roofWorldIndex
                    : -1;
            }
        }

        /// <summary>
        /// Возвращает true, если точка решения содержит roof obstacle в chain.
        /// </summary>
        public bool HasRoofLandingObstacle => RoofLandingObstacle != null && RoofLandingObstacleIndex >= 0;
    }
}
