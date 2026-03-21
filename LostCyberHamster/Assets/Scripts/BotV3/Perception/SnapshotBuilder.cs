using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;
using UnityEngine;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Единственный компонент pipeline с прямым доступом к Unity-объектам.
    /// Читает Hamster и ObstacleSpawner, строит BotSceneSnapshot.
    /// Видимость определяется границами камеры — бот видит ровно то, что видит игрок.
    /// </summary>
    public class SnapshotBuilder
    {
        public BotSceneSnapshot Build(Hamster hamster)
        {
            var snapshot = new BotSceneSnapshot
            {
                HamsterOnBottom = hamster.IsOnBottomLine.Value,
                HamsterOnRoof = IsRoofState(hamster.HamsterState.Value),
                HamsterRightX = hamster.RightX,
                HamsterWidth = hamster.ColliderWidth,
                Energy = hamster.Energy.Value,
                Lives = hamster.Lives.Value,
                SnapshotTime = Time.time
            };

            ScanObstacles(hamster, snapshot);
            return snapshot;
        }

        private static void ScanObstacles(Hamster hamster, BotSceneSnapshot snapshot)
        {
            var spawner = ObstacleSpawner.Instance;
            if (spawner == null) return;

            var cam = Camera.main;
            if (cam == null) return;

            float camX = cam.transform.position.x;
            float halfWidth = cam.orthographicSize * cam.aspect;
            float screenLeftX = camX - halfWidth;
            float screenRightX = camX + halfWidth;

            float hamsterRightX = hamster.RightX;

            var spawned = spawner.SpawnedObstacles;
            for (int i = 0; i < spawned.Count; i++)
            {
                var inst = spawned[i];
                if (inst?.ObstacleScript == null) continue;

                var obs = inst.ObstacleScript;
                var pos = obs.transform.position;
                float halfW = obs.ColliderWidth * 0.5f;
                float leftX = pos.x - halfW;
                float rightX = pos.x + halfW;

                if (rightX < screenLeftX || leftX > screenRightX) continue;

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
