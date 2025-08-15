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

        if(!HelpMethods.IsOnSameLine(_hamster.IsOnBottomLine.Value, obstacle))
            return;

        // Если можем подбирать
        if (IsCollectableState() && IsObstacleCollectable(obstacle))
        {
            HandleCollectable(obstacle);
            return;
        }

        if (CanDamageHamsterState(obstacle) || CanDamageOnRoofRun(obstacle))
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

    /// <summary>
    /// Проверяет, может ли хомяк получить урон во время бега по крыше.
    /// Возвращает true, если хомяк находится в состоянии RoofRun и препятствие не является типом bigNotAlive.
    /// </summary>
    private bool CanDamageOnRoofRun(Obstacle obstacle)
    {
        if (_hamster.HamsterState.Value != HamsterStateEnum.RoofRun)
            return false;

        // true, если препятствие НЕ BigNotAlive
        return obstacle.ObstacleType.ObstacleTypeEnum != ObstacleTypeEnum.bigNotAlive;
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
