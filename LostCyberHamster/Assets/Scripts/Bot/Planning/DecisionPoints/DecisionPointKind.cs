namespace Assets.Scripts.Bot.Planning.DecisionPoints
{
    /// <summary>
    /// Описывает причину, по которой planner создал точку решения.
    /// </summary>
    public enum DecisionPointKind
    {
        /// <summary>
        /// Chain с обычной угрозой, которую нужно безопасно пройти без target-oriented jump-on цели.
        /// </summary>
        BlockingThreat,

        /// <summary>
        /// Chain, которая ведет к ground jump-on target.
        /// </summary>
        GroundJumpOnTarget,

        /// <summary>
        /// Дорожная target-chain после passive roof-chain для JumpOnFromRoof.
        /// </summary>
        JumpOnFromRoofTarget,

        /// <summary>
        /// Chain, которая начинается с будущей крыши и ведет к JumpOnFromRoof target.
        /// </summary>
        RoofJumpOnTarget
    }
}
