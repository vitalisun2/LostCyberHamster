using System.Collections.Generic;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Снимок состояния сцены в момент запуска pipeline.
    /// Строится SnapshotBuilder'ом — единственной точкой доступа к Unity-объектам.
    /// </summary>
    public class BotSceneSnapshot : BotStateBase
    {
        public float SnapshotTime;

        /// <summary>
        /// Видимые объекты, отсортированные по LeftX.
        /// Category = Neutral по умолчанию — классификация выполняется ObjectClassifier'ом.
        /// </summary>
        public List<ObstacleInfo> VisibleObjects = new List<ObstacleInfo>();
        public List<AvoidanceCommitment> ActiveAvoidanceCommitments = new List<AvoidanceCommitment>();

        /// <summary>
        /// Проверяет, находится ли препятствие на той же линии, что и хомяк.
        /// </summary>
        public bool IsOnSameLane(ObstacleInfo obstacle)
        {
            return HamsterOnBottom == !obstacle.IsTopLane;
        }

        public void ReplaceAvoidanceCommitments(List<AvoidanceCommitment> commitments)
        {
            ActiveAvoidanceCommitments.Clear();
            if (commitments == null || commitments.Count == 0)
                return;

            ActiveAvoidanceCommitments.AddRange(commitments);
        }

        public void PruneInactiveAvoidanceCommitments()
        {
            for (int i = ActiveAvoidanceCommitments.Count - 1; i >= 0; i--)
            {
                if (!ActiveAvoidanceCommitments[i].IsStillActive(this))
                    ActiveAvoidanceCommitments.RemoveAt(i);
            }
        }
    }
}
