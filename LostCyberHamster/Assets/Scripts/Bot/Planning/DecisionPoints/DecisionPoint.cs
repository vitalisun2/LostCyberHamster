namespace Assets.Scripts.Bot.Planning.DecisionPoints
{
    /// <summary>
    /// Тип planning-ситуации, которую generator передает strategy-слою.
    /// </summary>
    public enum DecisionPointKind
    {
        ObstacleChain,
        MovingBoundary
    }

    /// <summary>
    /// Тип естественной границы движения, которую нужно представить в planning-графе.
    /// </summary>
    public enum MovingBoundaryKind
    {
        None,
        PassiveRoofExit
    }

    /// <summary>
    /// Описывает role-based planning-ситуацию: obstacle chain или естественную границу движения.
    /// </summary>
    public sealed class DecisionPoint
    {
        /// <summary>
        /// Создает role-based decision point из текущей chain.
        /// </summary>
        public DecisionPoint(ObstacleChain chain)
        {
            Kind = DecisionPointKind.ObstacleChain;
            Chain = chain;
            MovingBoundaryKind = MovingBoundaryKind.None;
        }

        private DecisionPoint(MovingBoundaryKind movingBoundaryKind)
        {
            Kind = DecisionPointKind.MovingBoundary;
            Chain = null;
            MovingBoundaryKind = movingBoundaryKind;
        }

        public DecisionPointKind Kind { get; }
        public ObstacleChain Chain { get; }
        public MovingBoundaryKind MovingBoundaryKind { get; }

        /// <summary>
        /// Создает decision point для естественной границы движения без obstacle chain.
        /// </summary>
        public static DecisionPoint MovingBoundary(MovingBoundaryKind movingBoundaryKind)
        {
            return new DecisionPoint(movingBoundaryKind);
        }
    }
}
