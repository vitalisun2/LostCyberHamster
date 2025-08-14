using Assets.Scripts.Common.Models;
using Assets.Scripts.Common;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.Gameplay;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Assets.Scripts;

public class CollisionController : MonoBehaviour
{
    [SerializeField] private Hamster _hamster;

    // статический список collectable
    private static readonly List<ObstacleTypeEnum> CollectableTypes = new()
    {
        ObstacleTypeEnum.collectableEnergetic,
        ObstacleTypeEnum.collectablePizza,
        ObstacleTypeEnum.collectableCrystal,
        ObstacleTypeEnum.collectableLife,
        ObstacleTypeEnum.collectableCoin,
    };

    private void OnTriggerEnter2D(Collider2D other)
    {
        ProcessTrigerEnter(other);
    }

    // on trigger stay 2d
    private void OnTriggerStay2D(Collider2D other)
    {
        if(_hamster.NeedCheckCollisionInRunFromRoofAfterShift.Value)
        {
            ProcessTrigerEnter(other);
            _hamster.NeedCheckCollisionInRunFromRoofAfterShift.Value = false;
        }
    }

    // processes the collision with the obstacle
    private void ProcessTrigerEnter(Collider2D other)
    {
        if (_hamster.IsDamaged.Value)
            return;

        var otherRoot = other.transform.parent?.parent?.gameObject;
        if (otherRoot == null)
        {
            Debug.LogError("Root object not found");
            return;
        }

        var obstacle = otherRoot.GetComponent<Obstacle>();
        if (obstacle == null)
        {
            Debug.LogError("Obstacle component not found");
            return;
        }

        var onSameLine = HelpMethods.IsOnSameLine(_hamster.IsOnBottomLine.Value, obstacle);

        // Если можем подбирать
        var isCollectableState = IsCollectableState();
        var isObstacleCollectable = IsObstacleCollectable(obstacle);
        if (isCollectableState && isObstacleCollectable)
        {
            HandleCollectable(obstacle);
            return;
        }

        if (CanDamageHamsterState(obstacle) && onSameLine)
        {
            HandleDamage(obstacle);
        }
    }

    private bool IsCollectableState()
    {
        return !_hamster.IsDamaged.Value && _hamster.HamsterState.Value != HamsterStateEnum.Dead;
    }

    /// <summary>
    /// Проверяет, может ли препятствие нанести урон хомяку в его текущем состоянии.
    /// </summary>
    private bool CanDamageHamsterState(Obstacle obstacle)
    {
        var canDamageHamsterState = new[]
        {
            HamsterStateEnum.Run,
            HamsterStateEnum.RunFromRoof
        }.Contains(_hamster.HamsterState.Value);

        return canDamageHamsterState;
    }

    private bool IsObstacleCollectable(Obstacle obstacle)
    {
        return CollectableTypes.Contains(obstacle.ObstacleType.ObstacleTypeEnum);
    }

    private void HandleCollectable(Obstacle obstacle)
    {
        _hamster.CollectCoinsOrBonusAction.Invoke(obstacle);
        UnspawnObstacle(obstacle);
    }

    private void HandleDamage(Obstacle obstacle)
    {
        if (!_hamster.IsProtected.Value)
        {
            _hamster.DamageEvent.Invoke();
        }

        if (_hamster.IsDestructiveOnCollision.Value)
        {
            UnspawnObstacle(obstacle);
            _hamster.DestroyObstacleEvent?.Invoke();
        }
    }

    private static void UnspawnObstacle(Obstacle obstacle)
    {
        obstacle.OnObstacleUnspawned.Invoke(obstacle.gameObject);
    }
}
