using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;
using UnityEngine;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Единственный компонент pipeline с прямым доступом к Unity-объектам.
    /// Читает Hamster и ObstacleSpawner, строит BotSceneSnapshot.
    /// Видимость определяется границами камеры — бот видит ровно то, что видит игрок.
    /// </summary>
    public class SnapshotBuilder
    {
        /// <summary>
        /// Строит snapshot: состояние хомяка + видимые объекты в пределах камеры.
        /// </summary>
        public BotSceneSnapshot Build(Hamster hamster)
        {
            var snapshot = new BotSceneSnapshot
            {
                HamsterOnBottom = hamster.IsOnBottomLine.Value,
                HamsterOnRoof = IsRoofState(hamster.HamsterState.Value),
                HamsterRightX = hamster.RightX,
                HamsterWidth = hamster.RightX - hamster.LeftX,
                Energy = hamster.Energy.Value,
                Lives = hamster.Lives.Value,
                SnapshotTime = Time.time
            };

            ScanObstacles(hamster, snapshot);
            return snapshot;
        }

        /// <summary>
        /// Сканирует spawned-препятствия в пределах камеры и добавляет в snapshot.
        /// </summary>
        private static void ScanObstacles(Hamster hamster, BotSceneSnapshot snapshot)
        {
            var spawner = ObstacleSpawner.Instance;
            if (spawner == null) return;

            // Определить видимую область камеры
            var cam = Camera.main;
            if (cam == null) return;

            float camX = cam.transform.position.x;
            float halfWidth = cam.orthographicSize * cam.aspect;
            float screenLeftX = camX - halfWidth;
            float screenRightX = camX + halfWidth;

            float hamsterRightX = hamster.RightX;

            // Собрать видимые препятствия
            var spawned = spawner.SpawnedObstacles;
            for (int i = 0; i < spawned.Count; i++)
            {
                var inst = spawned[i];
                if (inst?.ObstacleScript == null) continue;

                var obs = inst.ObstacleScript;
                var pos = obs.transform.position;
                var obsBounds = obs.GetComponentInChildren<BoxCollider2D>().bounds;
                float leftX = obsBounds.min.x;
                float rightX = obsBounds.max.x;

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

            // Отсортировать по LeftX для стабильного порядка обработки
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
