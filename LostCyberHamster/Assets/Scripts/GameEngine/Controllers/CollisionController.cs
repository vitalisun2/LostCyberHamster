using Assets.Scripts.Common.Models;
using Assets.Scripts.Common;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.Gameplay;
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
        ProcessTrigerEnter(other);
    }

    /// <summary>
    /// Повторно проверяет столкновение во время нахождения в триггере после смещения с крыши.
    /// </summary>
    private void OnTriggerStay2D(Collider2D other)
    {
        if (_hamster.NeedCheckCollisionInRunFromRoofAfterShift.Value)
        {
            ProcessTrigerEnter(other);
            _hamster.NeedCheckCollisionInRunFromRoofAfterShift.Value = false;
        }
    }

    /// <summary>
    /// Определяет результат столкновения хомяка с объектом триггера.
    /// </summary>
    private void ProcessTrigerEnter(Collider2D other)
    {
        // Прерываем обработку для уже повреждённого хомяка.
        if (_hamster.IsDamaged.Value)
            return;

        // Находим корневой объект препятствия и валидируем его состав.
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

        // Игнорируем объекты на другой линии движения.
        if (!HelpMethods.IsOnSameLine(_hamster.IsOnBottomLine.Value, obstacle))
            return;

        // Подбираем бонусы и коллекционные объекты.
        if (IsCollectableState() && IsObstacleCollectable(obstacle))
        {
            HandleCollectable(obstacle);
            return;
        }

        // Применяем урон, если текущее состояние допускает столкновение.
        if (HasCollisionInRunState(obstacle)
            || HasCollisionInJumpOnState()
            || HasCollisionWithBigAliveInJumpState(obstacle))
        {
            HandleDamage(obstacle);
        }
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
    /// Проверяет, должен ли хомяк получать урон во второй фазе анимации напрыгивания.
    /// </summary>
    private bool HasCollisionInJumpOnState()
    {
        if (_hamster.HamsterState.Value != HamsterStateEnum.JumpOnObstacle
            && _hamster.HamsterState.Value != HamsterStateEnum.SuperJumpOnObstacle
            && _hamster.HamsterState.Value != HamsterStateEnum.JumpOnObstacleFromRoof
            && _hamster.HamsterState.Value != HamsterStateEnum.SuperJumpOnObstacleFromRoof)
        {
            return false;
        }

        if (_hamster.PendingJumpedOnObstacle.Value != null)
        {
            return false;
        }

        return true;
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
        _hamster.CollectCoinsOrBonusAction.Invoke(obstacle);
        UnspawnObstacle(obstacle);
    }

    /// <summary>
    /// Наносит урон хомяку и при необходимости уничтожает препятствие после столкновения.
    /// </summary>
    private void HandleDamage(Obstacle obstacle)
    {
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
}
