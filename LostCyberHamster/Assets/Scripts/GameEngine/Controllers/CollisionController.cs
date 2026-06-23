using Assets.Scripts.Common.Models;
using Assets.Scripts.Common;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Assets.Scripts;

/// <summary>
/// Обрабатывает триггерные столкновения хомяка с препятствиями и подбираемыми объектами.
/// </summary>
public class CollisionController : MonoBehaviour
{
    /// <summary>
    /// Ссылка на хомяка, состояние которого используется при проверке столкновений.
    /// </summary>
    [SerializeField] private Hamster _hamster;

    /// <summary>
    /// Типы препятствий, которые считаются подбираемыми объектами.
    /// </summary>
    private static readonly List<ObstacleTypeEnum> _collectableTypes = new()
    {
        ObstacleTypeEnum.collectableEnergetic,
        ObstacleTypeEnum.collectablePizza,
        ObstacleTypeEnum.collectableCrystal,
        ObstacleTypeEnum.collectableLife,
        ObstacleTypeEnum.collectableCoin,
    };

    /// <summary>
    /// Порог перекрытия с BigAlive в некоторых стейтах прыжка 
    /// </summary>
    public const float BigAliveJumpDamageOverlapThreshold = 0.3f;

    /// <summary>
    /// Запускает обработку столкновения при первом входе в триггер препятствия.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!TryResolveSameLaneObstacle(other, out Obstacle obstacle))
            return;

        ProcessTriggerEnter(obstacle);
    }

    /// <summary>
    /// Проверяет удерживаемое пересечение после смены runtime-состояния без нового входа в триггер.
    /// </summary>
    private void OnTriggerStay2D(Collider2D other)
    {
        bool checkRunFromRoofAfterShift = _hamster.NeedCheckCollisionInRunFromRoofAfterShift.Value;

        if (!ShouldCheckHeldRunCollision(checkRunFromRoofAfterShift))
            return;

        if (!TryResolveSameLaneObstacle(other, out Obstacle obstacle))
            return;

        ProcessTriggerStay(obstacle);

        if (checkRunFromRoofAfterShift)
            _hamster.NeedCheckCollisionInRunFromRoofAfterShift.Value = false;
    }

    private bool ShouldCheckHeldRunCollision(bool checkRunFromRoofAfterShift)
    {
        HamsterStateEnum state = _hamster.HamsterState.Value;

        return state == HamsterStateEnum.Run
            || state == HamsterStateEnum.RoofRun
            || checkRunFromRoofAfterShift;
    }

    /// <summary>
    /// Определяет результат первого входа хомяка в триггер препятствия.
    /// </summary>
    private void ProcessTriggerEnter(Obstacle obstacle)
    {
        // Прерываем обработку для уже повреждённого хомяка.
        if (_hamster.IsDamaged.Value)
            return;

        // Подбираем бонусы и коллекционные объекты.
        if (IsCollectableState() && IsObstacleCollectable(obstacle))
        {
            HandleCollectable(obstacle);
            return;
        }

        // Применяем урон, если текущее состояние допускает столкновение на входе в триггер.
        if (HasCollisionInRunState(obstacle))
        {
            HandleDamage(obstacle, "Enter", "RunState");
            return;
        }

        if (HasCollisionWithRoofHazardInJumpOnRoofState(obstacle))
        {
            HandleDamage(obstacle, "Enter", "JumpOnRoofRoofHazard");
            return;
        }

        if (HasCollisionWithBigAliveInJumpState(obstacle))
        {
            HandleDamage(obstacle, "Enter", "BigAliveJumpOverlap");
        }
    }

    /// <summary>
    /// Определяет результат удерживаемого пересечения с препятствием.
    /// </summary>
    private void ProcessTriggerStay(Obstacle obstacle)
    {
        if (_hamster.IsDamaged.Value)
            return;

        if (IsCollectableState() && IsObstacleCollectable(obstacle))
        {
            HandleCollectable(obstacle);
            return;
        }

        if (HasCollisionInRunState(obstacle))
        {
            HandleDamage(obstacle, "Stay", "RunState");
        }
    }

    /// <summary>
    /// Находит obstacle из collider-а и отсекает объекты с другой линии.
    /// </summary>
    private bool TryResolveSameLaneObstacle(Collider2D other, out Obstacle obstacle)
    {
        obstacle = null;

        var otherRoot = other.transform.parent?.parent?.gameObject;
        if (otherRoot == null)
        {
            Debug.LogError("Root object not found");
            return false;
        }

        obstacle = otherRoot.GetComponent<Obstacle>();
        if (obstacle == null)
        {
            Debug.LogError("Obstacle component not found");
            return false;
        }

        // Игнорируем объекты на другой линии движения.
        if (!HelpMethods.IsOnSameLine(_hamster.IsOnBottomLine.Value, obstacle))
            return false;

        return true;
    }

    /// <summary>
    /// Проверяет, может ли хомяк подбирать бонусы в текущем состоянии.
    /// </summary>
    private bool IsCollectableState()
    {
        return !_hamster.IsDamaged.Value && _hamster.HamsterState.Value != HamsterStateEnum.Dead;
    }

    /// <summary>
    /// Проверяет, должен ли хомяк получать урон от препятствия во время бега.
    /// </summary>
    private bool HasCollisionInRunState(Obstacle obstacle)
    {
        bool result = false;

        // Разрешаем обычное столкновение во время бега по земле.
        if (_hamster.HamsterState.Value == HamsterStateEnum.Run)
        {
            result = true;
        }

        // Разрешаем коллизии с препятствиями, не являющимися крышами, во время бега с крыши или по крыше.
        if (_hamster.HamsterState.Value == HamsterStateEnum.RunFromRoof
        || _hamster.HamsterState.Value == HamsterStateEnum.RoofRun)
        {
            result = !CollisionUtils.IsRoofObstacle(obstacle.ObstacleType.ObstacleTypeEnum);
        }

        return result;
    }

    /// <summary>
    /// Проверяет, пересекается ли хомяк в прыжке с большим живым препятствием достаточно глубоко для урона.
    /// </summary>
    private bool HasCollisionWithBigAliveInJumpState(Obstacle obstacle)
    {
        // Определяем прыжковые состояния, в которых столкновение может быть опасным.
        var isDamagableJumpState = new[]
        {
            HamsterStateEnum.JumpFromRoof,
            HamsterStateEnum.SuperJumpFromRoof,
            HamsterStateEnum.JumpOnRoof,
            HamsterStateEnum.SuperJumpOnRoof,
            HamsterStateEnum.JumpOver,
            HamsterStateEnum.SuperJumpOver,
        }.Contains(_hamster.HamsterState.Value);

        if (!isDamagableJumpState)
            return false;

        // Фильтруем все препятствия, кроме большого живого.
        if (obstacle.ObstacleType.ObstacleTypeEnum != ObstacleTypeEnum.bigAlive)
            return false;

        // Сравниваем глубину перекрытия по оси X с порогом урона.
        CollisionUtils.GetObstacleXInterval(obstacle, obstacle.ColliderWidth, 0f, out float obstacleLeftX, out float obstacleRightX);
        float overlap = Mathf.Min(_hamster.RightX, obstacleRightX) - Mathf.Max(_hamster.LeftX, obstacleLeftX);

        return overlap > _hamster.ColliderWidth * BigAliveJumpDamageOverlapThreshold;
    }

    private bool HasCollisionWithRoofHazardInJumpOnRoofState(Obstacle obstacle)
    {
        HamsterStateEnum state = _hamster.HamsterState.Value;
        if (state != HamsterStateEnum.JumpOnRoof && state != HamsterStateEnum.SuperJumpOnRoof)
            return false;

        if (ObstacleSpawner.Instance == null)
            return false;

        var obstacles = ObstacleSpawner.Instance.SpawnedObstacles
            .Select(spawnedObstacle => spawnedObstacle?.ObstacleScript)
            .Where(spawnedObstacle => spawnedObstacle != null);

        return CollisionUtils.IsRoofHazard(
            obstacle,
            obstacles);
    }

    /// <summary>
    /// Проверяет, относится ли препятствие к подбираемым объектам.
    /// </summary>
    private bool IsObstacleCollectable(Obstacle obstacle)
    {
        return _collectableTypes.Contains(obstacle.ObstacleType.ObstacleTypeEnum);
    }

    /// <summary>
    /// Выдаёт награду за подобранный объект и убирает его со сцены.
    /// </summary>
    private void HandleCollectable(Obstacle obstacle)
    {
        DebugManager.DiagLog(
            $"[CollisionController] collect obstacle={FormatObstacle(obstacle)} " +
            $"state={_hamster.HamsterState.Value} lives={_hamster.Lives.Value} " +
            $"lane={(_hamster.IsOnBottomLine.Value ? "bottom" : "top")}");

        _hamster.CollectCoinsOrBonusAction.Invoke(obstacle);
        UnspawnObstacle(obstacle);
    }

    /// <summary>
    /// Наносит урон хомяку и при необходимости уничтожает препятствие после столкновения.
    /// </summary>
    private void HandleDamage(Obstacle obstacle, string triggerSource, string reason)
    {
        DebugManager.DiagLog(
            $"[CollisionController] damage source={triggerSource} reason={reason} " +
            $"state={_hamster.HamsterState.Value} livesBefore={_hamster.Lives.Value} " +
            $"isDamaged={_hamster.IsDamaged.Value} protected={_hamster.IsProtected.Value} " +
            $"destructive={_hamster.IsDestructiveOnCollision.Value} " +
            $"lane={(_hamster.IsOnBottomLine.Value ? "bottom" : "top")} " +
            $"{FormatHamsterBounds()} " +
            $"obstacle={FormatObstacle(obstacle)} " +
            $"pending={FormatObstacle(_hamster.PendingJumpedOnObstacle.Value)}");

        // Отправляем событие урона, если защита не активна.
        if (!_hamster.IsProtected.Value)
        {
            _hamster.DamageEvent.Invoke();
        }

        // Удаляем препятствие, если хомяк умеет ломать его при столкновении.
        if (_hamster.IsDestructiveOnCollision.Value)
        {
            _hamster.DestroyObstacleEvent?.Invoke(obstacle);
        }
    }

    /// <summary>
    /// Возвращает препятствие в пул через событие снятия со сцены.
    /// </summary>
    private static void UnspawnObstacle(Obstacle obstacle)
    {
        obstacle.OnObstacleUnspawned.Invoke(obstacle.gameObject);
    }

    /// <summary>
    /// Форматирует bounds хомяка для диагностики столкновений.
    /// </summary>
    private string FormatHamsterBounds()
    {
        return $"hamsterX=[{_hamster.LeftX:F2},{_hamster.RightX:F2}]";
    }

    /// <summary>
    /// Форматирует obstacle для диагностики столкновений.
    /// </summary>
    private static string FormatObstacle(Obstacle obstacle)
    {
        if (obstacle == null)
            return "null";

        CollisionUtils.GetObstacleXInterval(
            obstacle,
            obstacle.ColliderWidth,
            0f,
            out float obstacleLeftX,
            out float obstacleRightX);

        return $"{obstacle.ObstacleType.ObstacleTypeEnum}#" +
               $"{obstacle.GetInstanceID()} " +
               $"gameObjectId={obstacle.gameObject.GetInstanceID()} " +
               $"name={obstacle.name} " +
               $"x=[{obstacleLeftX:F2},{obstacleRightX:F2}] " +
               $"lane={(obstacle.ObstacleType.IsTop ? "top" : "bottom")}";
    }
}
