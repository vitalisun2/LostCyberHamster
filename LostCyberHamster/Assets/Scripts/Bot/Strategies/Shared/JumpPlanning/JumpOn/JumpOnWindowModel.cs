using Assets.Scripts.Bot.Perception;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpOn
{
    /// <summary>
    /// Хранит границы fire-window для напрыгивания на дорожный smallAlive.
    /// </summary>
    internal readonly struct JumpOnWindowModel
    {
        public JumpOnWindowModel(
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float firstFireShift,
            float lastFireShift,
            float selectedFireShift)
        {
            TargetObstacle = targetObstacle;
            TargetObstacleIndex = targetObstacleIndex;
            FirstFireShift = firstFireShift;
            LastFireShift = lastFireShift;
            SelectedFireShift = selectedFireShift;
        }

        public ObstacleSnapshot TargetObstacle { get; }
        public int TargetObstacleIndex { get; }
        public float FirstFireShift { get; }
        public float LastFireShift { get; }
        public float SelectedFireShift { get; }
    }
}
