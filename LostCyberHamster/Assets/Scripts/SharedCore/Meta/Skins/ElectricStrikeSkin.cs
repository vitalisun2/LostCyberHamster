using Assets.Scripts.Common;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;
using Assets.Scripts;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ElectricStrikeSkin : Skin
{
    protected override void ApplyUltaLogic()
    {
        ElectricStrike();
    }

    protected override void UpdateUltaLogic()
    {
    }

    private void ElectricStrike()
    {
        var hamster = LevelController.Instance.LevelData.Hamster;

        // Create the electric strike effect
        HelpMethods.CreateUltaEffect(UltaPrefab, hamster);

        // Найти все препятствия на той же линии в пределах StrikeRangeMax
        var obstaclesInRange = FindObstaclesOnSameLaneInRange(hamster);

        if (obstaclesInRange.Any())
        {
            // Уничтожить все найденные препятствия с задержкой
            hamster.StartCoroutine(DestroyObstaclesWithDelay(obstaclesInRange, 0.1f));
        }
        else
        {
            Debug.Log("No obstacles to strike.");
        }
    }

    private System.Collections.IEnumerator DestroyObstaclesWithDelay(List<Obstacle> obstacles, float delay)
    {
        foreach (var obstacle in obstacles)
        {
            obstacle.OnObstacleUnspawned.Invoke(obstacle.gameObject);
            obstacle.BoomEffectAction.Invoke(obstacle.transform.position, obstacle.GameManager);

            Debug.Log("Electric strike destroyed an obstacle!");

            // Ждем указанное время перед уничтожением следующего препятствия
            yield return new WaitForSeconds(delay);
        }

        // Событие уничтожения всех препятствий
        var hamster = LevelController.Instance.LevelData.Hamster;
        hamster.DestroyObstacleEvent?.Invoke();
    }

    private List<Obstacle> FindObstaclesOnSameLaneInRange(Hamster hamster)
    {
        var spawnedObstacles =
            ObstacleSpawner.Instance.SpawnedObstacles
                .Select(x => x.ObstacleScript)
                .ToList();

        List<Obstacle> obstaclesInRange = new List<Obstacle>();

        foreach (var obstacle in spawnedObstacles)
        {
            if (obstacle.transform.position.x < hamster.transform.position.x)
                continue;  // Пропустить препятствия, которые позади хомяка

            if (!HelpMethods.IsOnSameLine(hamster.IsOnBottomLine.Value, obstacle))
                continue;  // Пропустить препятствия, которые не на той же линии

            float distX = Mathf.Abs(hamster.transform.position.x - obstacle.transform.position.x);

            // Проверить, что препятствие находится в пределах StrikeRangeMax
            if (distX <= Consts.StrikeRangeMax)
            {
                obstaclesInRange.Add(obstacle);
            }
        }

        return obstaclesInRange;  // Вернуть все препятствия в пределах зоны действия
    }
}
