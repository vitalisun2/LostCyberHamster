using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Strategies.SwitchLane;

namespace Assets.Scripts.Bot.Strategies.RoofSwitchLaneExit
{
    /// <summary>
    /// Описывает один candidate схода с крыши через смену линии.
    /// </summary>
    internal readonly struct RoofSwitchLaneExitModel
    {
        public RoofSwitchLaneExitModel(
            ObstacleSnapshot contextObstacle,
            int contextObstacleIndex,
            bool targetBottomLine,
            SwitchLaneFireWindowSample fireWindowSample,
            float runFromRoofTravel)
        {
            ContextObstacle = contextObstacle;
            ContextObstacleIndex = contextObstacleIndex;
            TargetBottomLine = targetBottomLine;
            FireWindowSample = fireWindowSample;
            RunFromRoofTravel = runFromRoofTravel;
        }

        public ObstacleSnapshot ContextObstacle { get; }

        public int ContextObstacleIndex { get; }

        public bool TargetBottomLine { get; }

        public SwitchLaneFireWindowSample FireWindowSample { get; }

        public float RunFromRoofTravel { get; }

        public float FireShift => FireWindowSample.FireShift;

        public float CompletionWorldShift => FireShift + RunFromRoofTravel;
    }
}
