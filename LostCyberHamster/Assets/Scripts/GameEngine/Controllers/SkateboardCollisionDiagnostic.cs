#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Assets.Scripts.Common.Models;
using Assets.Scripts.Gameplay;

/// <summary>
/// Хранит результат Skateboard-контакта для DEV-наблюдателей.
/// </summary>
public readonly struct SkateboardCollisionDiagnostic
{
    public SkateboardCollisionDiagnostic(
        Hamster hamster,
        ObstacleTypeEnum obstacleType,
        SkateboardCollisionOutcome outcome,
        int livesBefore,
        int livesAfter,
        bool obstacleActiveAfter,
        bool wasJumpCollisionActive,
        bool roofSupportActiveAfter)
    {
        Hamster = hamster;
        ObstacleType = obstacleType;
        Outcome = outcome;
        LivesBefore = livesBefore;
        LivesAfter = livesAfter;
        ObstacleActiveAfter = obstacleActiveAfter;
        WasJumpCollisionActive = wasJumpCollisionActive;
        RoofSupportActiveAfter = roofSupportActiveAfter;
    }

    public Hamster Hamster { get; }
    public ObstacleTypeEnum ObstacleType { get; }
    public SkateboardCollisionOutcome Outcome { get; }
    public int LivesBefore { get; }
    public int LivesAfter { get; }
    public bool ObstacleActiveAfter { get; }
    public bool WasJumpCollisionActive { get; }
    public bool RoofSupportActiveAfter { get; }
}
#endif
