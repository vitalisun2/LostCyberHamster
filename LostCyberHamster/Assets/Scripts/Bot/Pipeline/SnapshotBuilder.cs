using System.Collections.Generic;
using Assets.Scripts.Common.Models;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;
using UnityEngine;
using Vues.GameCore;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Единственный компонент бота с прямым доступом к Unity-объектам.
    /// Строит снимок состояния сцены (BotSceneSnapshot) по запросу HamsterBot'а.
    /// Все остальные компоненты пайплайна работают только со snapshot-данными.
    /// </summary>
    public class SnapshotBuilder
    {
        private const float ScanBehindMargin = 1.0f;

        /// <summary>
        /// Читает состояние хомяка и сцены, строит и возвращает BotSceneSnapshot.
        /// ObstacleInfo создаются с Category = Neutral — классификация выполняется отдельно.
        /// </summary>
        public BotSceneSnapshot Build(Hamster hamster, float scanRange)
        {
            var snapshot = new BotSceneSnapshot();

            snapshot.HamsterOnBottom = hamster.IsOnBottomLine.Value;
            snapshot.HamsterOnRoof   = IsRoofState(hamster.HamsterState.Value);
            snapshot.HamsterRightX   = hamster.RightX;
            snapshot.Energy          = hamster.Energy.Value;
            snapshot.Lives           = hamster.Lives.Value;
            snapshot.UltaCharge      = hamster.UltaChargeAmount.Value;
            snapshot.Coins           = ResourceManager.GetCurrentBalance(ResourceType.Coins);

            ScanObstacles(hamster, scanRange, snapshot);

            return snapshot;
        }

        private static void ScanObstacles(Hamster hamster, float scanRange, BotSceneSnapshot snapshot)
        {
            var spawner = ObstacleSpawner.Instance;
            if (spawner == null) return;

            var spawned = spawner.SpawnedObstacles;
            float hamsterRightX = hamster.RightX;
            float hamsterLeftX  = hamster.LeftX;
            float maxX = hamsterRightX + scanRange;
            float minX = hamsterLeftX  - ScanBehindMargin;

            var list = snapshot.VisibleObjects;

            for (int i = 0; i < spawned.Count; i++)
            {
                var inst = spawned[i];
                if (inst?.ObstacleScript == null) continue;

                var obs = inst.ObstacleScript;
                var pos = obs.transform.position;
                float halfW  = obs.ColliderWidth * 0.5f;
                float leftX  = pos.x - halfW;
                float rightX = pos.x + halfW;

                if (rightX < minX || leftX > maxX) continue;

                var typeEnum  = obs.ObstacleType.ObstacleTypeEnum;
                bool isTopLane = obs.ObstacleType.IsTop;
                bool isOnRoof  = IsOnRoof(pos.y, isTopLane);

                float distance    = leftX - hamsterRightX;
                float timeToReach = distance > 0 ? distance / Consts.GameSpeedBase : 0f;

                // Category = Neutral: классификация — задача ObjectClassifier (Этап 3)
                list.Add(new ObstacleInfo(
                    typeEnum, leftX, rightX, pos.x,
                    isTopLane, isOnRoof,
                    distance, timeToReach,
                    ObjectCategory.Neutral,
                    obs,
                    stableId: obs.GetInstanceID()));
            }

            list.Sort((a, b) => a.LeftX.CompareTo(b.LeftX));
        }

        private static bool IsRoofState(HamsterStateEnum state)
        {
            return state == HamsterStateEnum.RoofRun
                || state == HamsterStateEnum.RoofJump
                || state == HamsterStateEnum.RoofJumpDamage
                || state == HamsterStateEnum.SuperRoofJump
                || state == HamsterStateEnum.SuperRoofJumpDamage;
        }

        private static bool IsOnRoof(float yPos, bool isTopLane)
        {
            float roofY = isTopLane ? Consts.ObstacleRoofY0Pos : Consts.ObstacleRoofY1Pos;
            return Mathf.Abs(yPos - roofY) < Consts.ObstacleLineTolerance;
        }
    }
}
