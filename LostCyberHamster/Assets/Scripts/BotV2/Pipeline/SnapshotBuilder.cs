using System.Collections.Generic;
using Assets.Scripts.Common.Models;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;
using UnityEngine;

namespace Assets.Scripts.BotV2
{
    /// <summary>
    /// Единственный компонент BotV2 с прямым доступом к Unity-объектам.
    /// Читает Hamster и ObstacleSpawner, строит BotSceneSnapshot.
    /// Все остальные компоненты pipeline работают только со snapshot-данными.
    /// </summary>
    public class SnapshotBuilder
    {
        private const float ScanBehindMargin = 1.0f;

        /// <summary>
        /// Строит снимок состояния сцены.
        /// ObstacleInfo создаются с Category = Neutral — классификация выполняется ObjectClassifier'ом.
        /// </summary>
        public BotSceneSnapshot Build(Hamster hamster, float scanRange)
        {
            var snapshot = new BotSceneSnapshot();
            snapshot.HamsterOnBottom = hamster.IsOnBottomLine.Value;
            snapshot.HamsterOnRoof   = IsRoofState(hamster.HamsterState.Value);
            snapshot.HamsterRightX   = hamster.RightX;
            snapshot.HamsterWidth    = hamster.ColliderWidth;
            snapshot.Energy          = hamster.Energy.Value;
            snapshot.Lives           = hamster.Lives.Value;
            snapshot.SnapshotTime    = Time.time;

            ScanObstacles(hamster, scanRange, snapshot);
            return snapshot;
        }

        private static void ScanObstacles(Hamster hamster, float scanRange, BotSceneSnapshot snapshot)
        {
            var spawner = ObstacleSpawner.Instance;
            if (spawner == null) return;

            float hamsterRightX = hamster.RightX;
            float maxX = hamsterRightX + scanRange;
            float minX = hamster.LeftX - ScanBehindMargin;

            var spawned = spawner.SpawnedObstacles;
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

                float dist = leftX - hamsterRightX;

                snapshot.VisibleObjects.Add(new ObstacleInfo(
                    obs.ObstacleType.ObstacleTypeEnum,
                    obs.ObstacleType.IsTop,
                    leftX, rightX, pos.x,
                    dist,
                    ObjectCategory.Neutral,
                    obs.GetInstanceID()));
            }

            snapshot.VisibleObjects.Sort((a, b) => a.LeftX.CompareTo(b.LeftX));
        }

        private static bool IsRoofState(HamsterStateEnum state)
        {
            return state == HamsterStateEnum.RoofRun
                || state == HamsterStateEnum.RoofJump
                || state == HamsterStateEnum.RoofJumpDamage
                || state == HamsterStateEnum.SuperRoofJump
                || state == HamsterStateEnum.SuperRoofJumpDamage;
        }
    }
}
