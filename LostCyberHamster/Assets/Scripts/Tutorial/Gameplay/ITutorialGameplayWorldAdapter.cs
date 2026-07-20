using System;
using System.Collections.Generic;
using Assets.Scripts;
using Assets.Scripts.Common.Models;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Tutorial
{
    public interface ITutorialGameplayWorldAdapter
    {
        GameState State { get; }
        HamsterStateEnum HamsterState { get; }

        event Action UltraUsed;

        void Prepare(TutorialGameplayScenario scenario);
        Obstacle FindNearestSameLineObstacle(IReadOnlyList<ObstacleTypeEnum> targetTypes);
        float GetDistanceToHamster(Obstacle obstacle);
        bool HasObstacleLeftPlay(Obstacle obstacle);
        void CaptureSuperHitTargets(List<Obstacle> targets);
        bool IsElectricStrikeEffectPlaying();
        bool HasCapturedSuperHitTargetInPlay(IReadOnlyList<Obstacle> targets);
        void PerformAction(TutorialAction action);
        void Pause();
        void Resume();
    }
}
