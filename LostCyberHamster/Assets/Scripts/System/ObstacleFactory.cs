using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Scripts.Common.Models;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Installers.Roots;
using Assets.Scripts.System;
using Assets.Scripts.System.Rendering;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Assets.Scripts.Entry_Points.GameLoadingTasks
{
    public static class ObstacleFactory
    {
        public static List<InstantiatedObstacle> CreateObstacles(EnvironmentRoot environmentRoot)
        {
            var levelInfo = LevelController.Instance.LevelData.LevelInfo;

            List<InstantiatedObstacle> instantiatedObstacles = new();

            for (int patternIndex = 0; patternIndex < levelInfo.patterns.Count; patternIndex++)
            {
                foreach (var model in levelInfo.patterns[patternIndex].obstacles)
                {
                    var spriteName = Path.GetFileNameWithoutExtension(model.spriteName);
                    var spawnPosition = new Vector3(model.x, model.y, 0);
                    var obstacleScript = CreateObstacle(model, spriteName, spawnPosition, environmentRoot);
                    LevelController.Instance.LevelData.GameManager.AddListener(obstacleScript);

                    instantiatedObstacles.Add(new InstantiatedObstacle(obstacleScript, spriteName, spawnPosition, patternIndex));
                }
            }

            return instantiatedObstacles;
        }

        private static Obstacle CreateObstacle(ObstacleModel model, string spriteName, Vector3 spawnPosition,
            EnvironmentRoot environmentRoot)
        {
            var prefab = GetPrefab(model.type);

            var obstacleInst = Object.Instantiate(prefab, spawnPosition, Quaternion.identity, environmentRoot.ObstaclesPool);

            ShiftUpYPosForBonuses(obstacleInst, model.type);

            var renderer = obstacleInst.GetComponentInChildren<SpriteRenderer>();
            var rendererSprite = GetRendererSpriteByModelTypeAndName(model.type, spriteName);
            SpriteRendererMaterialHelper.ApplySpriteWithDefaultMaterial(renderer, rendererSprite);
            renderer.sortingLayerName = GetSortingLayer(model.y);

            var boxCollider = renderer.GetComponentInChildren<BoxCollider2D>();
            boxCollider.size = renderer.sprite.bounds.size;
            boxCollider.offset = renderer.sprite.bounds.center;

            var obstacleScript = obstacleInst.AddComponent<Obstacle>();

            obstacleScript.Init((ObstacleTypeEnum)model.type, LevelController.Instance.LevelData.GameManager, spriteName);

            return obstacleScript;
        }

        private static Sprite GetRendererSpriteByModelTypeAndName(int modelType, string spriteName)
        {
            Sprite sprite = null;

            if (new List<ObstacleTypeEnum>
                {
                    ObstacleTypeEnum.smallAlive,
                    ObstacleTypeEnum.smallNotAliveRoad,
                    ObstacleTypeEnum.smallNotAliveRoadAndRoof,
                    ObstacleTypeEnum.bigAlive,
                    ObstacleTypeEnum.bigNotAlive
                }.Contains((ObstacleTypeEnum)modelType))
                sprite = LevelController.Instance.LevelData.ObstaclesSprites.FirstOrDefault(s =>
                    string.Equals(s.name, spriteName, StringComparison.OrdinalIgnoreCase));

            var collectableTypes = new List<ObstacleTypeEnum>
            {
                ObstacleTypeEnum.collectableEnergetic,
                ObstacleTypeEnum.collectablePizza,
                ObstacleTypeEnum.collectableCrystal,
                ObstacleTypeEnum.collectableLife,
                ObstacleTypeEnum.collectableCoin
            };

            if (collectableTypes.Contains((ObstacleTypeEnum)modelType))
                sprite = LevelController.Instance.LevelData.CollectablesSprites.FirstOrDefault(s =>
                    string.Equals(s.name, spriteName, StringComparison.OrdinalIgnoreCase));

            if (ObstacleTypeEnum.decor == (ObstacleTypeEnum)modelType)
                sprite = LevelController.Instance.LevelData.DecorSprites.FirstOrDefault(s =>
                    string.Equals(s.name, spriteName, StringComparison.OrdinalIgnoreCase));

            LevelDataValidator.ValidateObstacleSprite((ObstacleTypeEnum)modelType, spriteName, sprite);

            return sprite;
        }

        private static GameObject GetPrefab(int type)
        {
            var levelData = LevelController.Instance.LevelData;

            List<ObstacleTypeEnum> smallObstacleTypes = new List<ObstacleTypeEnum>
            {
                ObstacleTypeEnum.smallAlive,
                ObstacleTypeEnum.smallNotAliveRoad,
                ObstacleTypeEnum.smallNotAliveRoadAndRoof,
                ObstacleTypeEnum.collectableEnergetic,
                ObstacleTypeEnum.collectablePizza,
                ObstacleTypeEnum.collectableCrystal,
                ObstacleTypeEnum.collectableLife,
                ObstacleTypeEnum.collectableCoin
            };

            if (smallObstacleTypes.Contains((ObstacleTypeEnum)type))
                return levelData.SmallCitizenPrefab;

            if (type == (int)ObstacleTypeEnum.bigAlive)
                return levelData.BigCitizenPrefab;

            return levelData.BigNotAlivePrefab;
        }

        private static string GetSortingLayer(float yPosition)
        {
            return Math.Abs(yPosition - Consts.ObstacleY0Pos) < 0.01f ? "UpperSprites" : "LowerSprites";
        }

        private static void ShiftUpYPosForBonuses(GameObject obstacleInst, int modelType)
        {
            var firstChild = obstacleInst.transform.GetChild(0).gameObject;

            if (new List<ObstacleTypeEnum>()
                {
                    ObstacleTypeEnum.collectableEnergetic,
                    ObstacleTypeEnum.collectablePizza,
                    ObstacleTypeEnum.collectableCrystal,
                    ObstacleTypeEnum.collectableLife,
                    ObstacleTypeEnum.collectableCoin
                }.Contains((ObstacleTypeEnum)modelType))
            {
                firstChild.transform.position += new Vector3(0, Consts.BonusYOffset, 0);
            }
        }
    }
}
