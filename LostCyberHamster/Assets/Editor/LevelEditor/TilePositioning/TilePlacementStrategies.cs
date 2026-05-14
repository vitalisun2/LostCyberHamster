using Assets.Scripts.Common.Models;
using System;
using UnityEngine.Tilemaps;

/// <summary>
/// Предоставляет стратегии расстановки тайлов для различных типов препятствий и объектов.
/// </summary>
public static class TilePlacementStrategies
{
    /// <summary>
    /// Стандартная стратегия для обычных препятствий, включает правила смещения по Y и избегания наложений.
    /// </summary>
    public static ITilePlacementStrategy CreateObstacleStrategy()
    {
        var pipeline = new TilePlacementPipeline();
        pipeline.AddRule(new YPositionRule());
        pipeline.AddRule(new OverlapAvoidanceOnRoadRule());
        return pipeline;
    }

    /// <summary>
    /// Стратегия для собираемых объектов (collectables): привязка к одной из линий дороги и избегание наложений.
    /// </summary>
    /// <returns></returns>
    public static ITilePlacementStrategy CreateCollectableOnRoadStrategy()
    {
        var pipeline = new TilePlacementPipeline();
        pipeline.AddRule(new YPositionRule());
        pipeline.AddRule(new OverlapAvoidanceOnRoadRule());
        return pipeline;
    }

    /// <summary>
    /// Стратегия для объектов на крыше: привязка к крыше и избегание наложений.
    /// </summary>
    public static ITilePlacementStrategy CreateObjectOnRoofStrategy()
    {
        var pipeline = new TilePlacementPipeline();
        pipeline.AddRule(new SnapToRoofRule());
        pipeline.AddRule(new OverlapAvoidanceOnRoofRule());
        return pipeline;
    }

    /// <summary>
    /// Стратегия для декоративных объектов (decor). Разрешает ставить тайл, если он выше RoadUpperEdgeYPos.
    /// Добавляет случайное смещение по Z при одинаковом Y, чтобы обеспечить случайный порядок отрисовки.
    /// </summary>
    public static ITilePlacementStrategy CreateDecorStrategy()
    {
        var pipeline = new TilePlacementPipeline();
        pipeline.AddRule(new DecorPlacementRule());
        return pipeline;
    }

    /// <summary>
    /// Возвращает подходящую стратегию расстановки тайла в зависимости от типа препятствия.
    /// </summary>
    public static ITilePlacementStrategy GetStrategyForType(ObstacleTypeEnum type, bool isObjectOnRoof)
    {
        switch (type)
        {
            case ObstacleTypeEnum.smallAlive:
            case ObstacleTypeEnum.bigAlive:
            case ObstacleTypeEnum.bigNotAlive:
            case ObstacleTypeEnum.mediumNotAlive:
            case ObstacleTypeEnum.smallNotAliveRoad:
                return CreateObstacleStrategy();

            case ObstacleTypeEnum.collectableEnergetic:
            case ObstacleTypeEnum.collectablePizza:
            case ObstacleTypeEnum.collectableCrystal:
            case ObstacleTypeEnum.collectableLife:
            case ObstacleTypeEnum.collectableCoin:
            case ObstacleTypeEnum.smallNotAliveRoadAndRoof:
                return isObjectOnRoof ? CreateObjectOnRoofStrategy() : CreateCollectableOnRoadStrategy();



            case ObstacleTypeEnum.decor:
                return CreateDecorStrategy();

            default:
                throw new InvalidOperationException($"No strategy defined for obstacle type '{type}'.");
        }
    }
}
