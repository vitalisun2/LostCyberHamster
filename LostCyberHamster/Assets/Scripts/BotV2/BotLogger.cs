using System.Collections.Generic;
using System.Text;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;

/// <summary>
/// Уровень детализации логов бота.
/// </summary>
public enum BotLogLevel
{
    /// <summary>SELECT + EXECUTE + RESULT + DAMAGE</summary>
    Normal,
    /// <summary>Normal + SNAPSHOT + CLASSIFY + GENERATE</summary>
    Verbose
}

namespace Assets.Scripts.BotV2
{
    /// <summary>
    /// Статический логгер BotV2. Пишет через DebugManager.DiagLog() в EditorLogs/diagnostic_log.txt.
    /// </summary>
    public static class BotLogger
    {
        public static BotLogLevel Level = BotLogLevel.Normal;

        public static void Log(BotLogLevel level, string message)
        {
            if (level == BotLogLevel.Verbose && Level == BotLogLevel.Normal) return;
            DebugManager.DiagLog(message);
        }

        public static string FormatHamster(Hamster hamster)
        {
            if (hamster == null)
                return "hamster=null";

            return $"lane={(hamster.IsOnBottomLine.Value ? "bottom" : "top")} state={hamster.HamsterState.Value} x=[{hamster.LeftX:F2}..{hamster.RightX:F2}] energy={hamster.Energy.Value} lives={hamster.Lives.Value}";
        }

        public static string FormatStep(ChainStep step)
        {
            if (step == null)
                return "step=none";

            return $"action={step.Action} status={step.Status} rank={step.Rank} reason=\"{step.Reason}\" profit={step.ProfitScore} execAt={step.ExecuteAtDistance:F2} target={FormatSnapshotObstacle(step.TargetObstacle)}";
        }

        public static string FormatSnapshotObstacles(IReadOnlyList<ObstacleInfo> obstacles)
        {
            if (obstacles == null || obstacles.Count == 0)
                return "none";

            var builder = new StringBuilder();
            for (int i = 0; i < obstacles.Count; i++)
            {
                if (i > 0)
                    builder.Append(" | ");

                builder.Append('#');
                builder.Append(i);
                builder.Append(' ');
                builder.Append(FormatSnapshotObstacle(obstacles[i]));
            }

            return builder.ToString();
        }

        public static string FormatSnapshotObstacle(ObstacleInfo obstacle)
        {
            return $"{obstacle.Type}/{obstacle.Category} lane={(obstacle.IsTopLane ? "top" : "bottom")} left={obstacle.LeftX:F2} right={obstacle.RightX:F2} dist={obstacle.DistanceToHamster:F2} id={obstacle.StableId}";
        }

        public static string FormatLiveObstacles(Hamster hamster, int highlightStableId = 0)
        {
            var spawner = ObstacleSpawner.Instance;
            if (spawner == null)
                return "spawner=null";

            var entries = new List<string>();
            float hamsterRightX = hamster != null ? hamster.RightX : 0f;
            var spawned = spawner.SpawnedObstacles;

            for (int i = 0; i < spawned.Count; i++)
            {
                var inst = spawned[i];
                if (inst?.ObstacleScript == null)
                    continue;

                var obstacle = inst.ObstacleScript;
                float leftX = obstacle.transform.position.x - obstacle.ColliderWidth * 0.5f;
                float rightX = obstacle.transform.position.x + obstacle.ColliderWidth * 0.5f;
                float dist = leftX - hamsterRightX;
                string marker = obstacle.GetInstanceID() == highlightStableId ? "*" : string.Empty;

                entries.Add($"{marker}{obstacle.ObstacleType.ObstacleTypeEnum} lane={(obstacle.ObstacleType.IsTop ? "top" : "bottom")} left={leftX:F2} right={rightX:F2} dist={dist:F2} id={obstacle.GetInstanceID()}");
            }

            if (entries.Count == 0)
                return "none";

            return string.Join(" | ", entries);
        }
    }
}
