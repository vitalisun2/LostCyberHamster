using Assets.Scripts.Common.Models;
using Assets.Scripts.Bot.Diagnostics;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Actors;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;
using System;
using System.Linq;
using UnityEngine;
using Assets.Scripts;
using Vues.GameCore;

/// <summary>
/// Обрабатывает триггерные столкновения хомяка с препятствиями и подбираемыми объектами.
/// </summary>
public class CollisionController : MonoBehaviour
{
    /// <summary>
    /// Результат контакта active Skateboard с gameplay-объектом.
    /// </summary>
    public enum SkateboardCollisionOutcome
    {
        Damage,
        Destroy,
        Support,
        Collect,
        Ignored,
    }

    /// <summary>
    /// Снимок результата Skateboard-контакта для DEV-наблюдателей.
    /// </summary>
    public readonly struct SkateboardCollisionDiagnostic
    {
        public SkateboardCollisionDiagnostic(
            Hamster hamster,
            ObstacleTypeEnum obstacleType,
            SkateboardCollisionOutcome outcome,
            int livesBefore,
            int livesAfter,
            bool obstacleActiveAfter,
            bool wasJumpCollisionActive,
            bool roofSupportActiveAfter)
        {
            Hamster = hamster;
            ObstacleType = obstacleType;
            Outcome = outcome;
            LivesBefore = livesBefore;
            LivesAfter = livesAfter;
            ObstacleActiveAfter = obstacleActiveAfter;
            WasJumpCollisionActive = wasJumpCollisionActive;
            RoofSupportActiveAfter = roofSupportActiveAfter;
        }

        public Hamster Hamster { get; }
        public ObstacleTypeEnum ObstacleType { get; }
        public SkateboardCollisionOutcome Outcome { get; }
        public int LivesBefore { get; }
        public int LivesAfter { get; }
        public bool ObstacleActiveAfter { get; }
        public bool WasJumpCollisionActive { get; }
        public bool RoofSupportActiveAfter { get; }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>
    /// Сообщает точный итог обработанного Skateboard-контакта DEV-инструментам.
    /// </summary>
    public static event Action<SkateboardCollisionDiagnostic>
        SkateboardCollisionProcessed;
#endif

    /// <summary>
    /// Ссылка на хомяка, состояние которого используется при проверке столкновений.
    /// </summary>
    [SerializeField] private Hamster _hamster;

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

        ProcessTriggerEnter(obstacle, "MainPolygon/Enter");
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

        ProcessTriggerStay(obstacle, "MainPolygon/Stay");

        if (checkRunFromRoofAfterShift)
            _hamster.NeedCheckCollisionInRunFromRoofAfterShift.Value = false;
    }

    private bool ShouldCheckHeldRunCollision(bool checkRunFromRoofAfterShift)
    {
        if (_hamster.IsSkateboardModeActive)
            return true;

        HamsterStateEnum state = _hamster.HamsterState.Value;

        return state == HamsterStateEnum.Run
            || state == HamsterStateEnum.RoofRun
            || checkRunFromRoofAfterShift;
    }

    /// <summary>
    /// Определяет результат первого входа хомяка в триггер препятствия.
    /// </summary>
    private void ProcessTriggerEnter(Obstacle obstacle, string skateboardSource)
    {
        if (TryHandleSkateboardCollision(obstacle, skateboardSource))
            return;

        if (TryHandleCollectable(obstacle))
            return;

        // Прерываем обработку для уже повреждённого хомяка.
        if (_hamster.IsDamaged.Value)
            return;

        // Применяем урон, если текущее состояние допускает столкновение на входе в триггер.
        if (HasCollisionInRunState(obstacle))
        {
            bool wasSkateboardModeActive = _hamster.IsSkateboardModeActive;
            int livesBefore = _hamster.Lives.Value;
            HandleDamage(obstacle, "Enter", "RunState");
            if (wasSkateboardModeActive)
            {
                PublishSkateboardCollision(
                    obstacle,
                    _hamster.Lives.Value == livesBefore - 1
                        ? SkateboardCollisionOutcome.Damage
                        : SkateboardCollisionOutcome.Ignored,
                    livesBefore,
                    wasJumpCollisionActive: false);
            }
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
    private void ProcessTriggerStay(Obstacle obstacle, string skateboardSource)
    {
        if (TryHandleSkateboardCollision(obstacle, skateboardSource))
            return;

        if (TryHandleCollectable(obstacle))
            return;

        if (_hamster.IsDamaged.Value)
            return;

        if (HasCollisionInRunState(obstacle))
        {
            bool wasSkateboardModeActive = _hamster.IsSkateboardModeActive;
            int livesBefore = _hamster.Lives.Value;
            HandleDamage(obstacle, "Stay", "RunState");
            if (wasSkateboardModeActive)
            {
                PublishSkateboardCollision(
                    obstacle,
                    _hamster.Lives.Value == livesBefore - 1
                        ? SkateboardCollisionOutcome.Damage
                        : SkateboardCollisionOutcome.Ignored,
                    livesBefore,
                    wasJumpCollisionActive: false);
            }
        }
    }

    /// <summary>
    /// Исполняет единое policy-решение active Skateboard без локальных mode rules.
    /// </summary>
    private bool TryHandleSkateboardCollision(
        Obstacle obstacle,
        string triggerSource)
    {
        if (!_hamster.TryGetActiveSkateboardAttack(out SkateboardAttack attack))
            return false;

        ObstacleTypeEnum obstacleType = obstacle.ObstacleType.ObstacleTypeEnum;
        bool isRide = attack.IsRiding;
        bool hasJumpSnapshot =
            attack.TryGetCurrentJumpSnapshot(
                out SkateboardAttack.JumpCycleSnapshot jumpSnapshot);
        bool isRideSupport =
            isRide &&
            SkateboardInteractionPolicy.IsRoof(obstacleType) &&
            _hamster.SkateboardSurfaceController.IsRideSupport(
                obstacle,
                _hamster.IsOnBottomLine.Value);

        SkateboardInteractionPolicy.Phase phase = isRide
            ? SkateboardInteractionPolicy.Phase.Ride
            : SkateboardInteractionPolicy.Phase.Jump;
        SkateboardInteractionPolicy.Outcome outcome =
            SkateboardInteractionPolicy.Decide(
                obstacleType,
                phase,
                hasJumpSnapshot && jumpSnapshot.StartedOnRoof,
                isRideSupport: isRideSupport);
        int livesBefore = _hamster.Lives.Value;
        switch (outcome)
        {
            case SkateboardInteractionPolicy.Outcome.Collect:
                TryHandleCollectable(obstacle);
                return true;
            case SkateboardInteractionPolicy.Outcome.Damage:
                bool damageApplied = !_hamster.IsDamaged.Value;
                if (damageApplied)
                    HandleDamage(obstacle, triggerSource, "SkateboardRide");
                PublishSkateboardCollision(
                    obstacle,
                    damageApplied && _hamster.Lives.Value == livesBefore - 1
                        ? SkateboardCollisionOutcome.Damage
                        : SkateboardCollisionOutcome.Ignored,
                    livesBefore,
                    wasJumpCollisionActive: false,
                    obstacleTypeOverride: obstacleType);
                return true;
            case SkateboardInteractionPolicy.Outcome.Destroy:
                _hamster.DestroyObstacleBySuperAttackEvent?.Invoke(obstacle);
                PublishSkateboardCollision(
                    obstacle,
                    SkateboardCollisionOutcome.Destroy,
                    livesBefore,
                    wasJumpCollisionActive: hasJumpSnapshot,
                    obstacleTypeOverride: obstacleType);
                return true;
            case SkateboardInteractionPolicy.Outcome.PreserveSupport:
                PublishSkateboardCollision(
                    obstacle,
                    SkateboardCollisionOutcome.Support,
                    livesBefore,
                    wasJumpCollisionActive: hasJumpSnapshot,
                    obstacleTypeOverride: obstacleType);
                return true;
            case SkateboardInteractionPolicy.Outcome.BumpOnly:
            case SkateboardInteractionPolicy.Outcome.Ignore:
            default:
                PublishSkateboardCollision(
                    obstacle,
                    SkateboardCollisionOutcome.Ignored,
                    livesBefore,
                    wasJumpCollisionActive: hasJumpSnapshot,
                    obstacleTypeOverride: obstacleType);
                return true;
        }
    }

    /// <summary>
    /// Находит obstacle из collider-а и отсекает объекты с другой линии.
    /// </summary>
    private bool TryResolveSameLaneObstacle(Collider2D other, out Obstacle obstacle)
    {
        if (!TryResolveObstacle(other, out obstacle))
            return false;

        // Игнорируем объекты на другой линии движения.
        if (!HelpMethods.IsOnSameLine(_hamster.IsOnBottomLine.Value, obstacle))
            return false;

        return true;
    }

    private static bool TryResolveObstacle(Collider2D other, out Obstacle obstacle)
    {
        obstacle = null;

        GameObject otherRoot = other.transform.parent?.parent?.gameObject;
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

        // Поглощаем второй callback после синхронного возврата obstacle в пул.
        return obstacle.isActiveAndEnabled;
    }

    /// <summary>
    /// Проверяет, может ли хомяк подбирать бонусы в текущем состоянии.
    /// </summary>
    private bool IsCollectableState()
    {
        return !_hamster.IsDamaged.Value && _hamster.HamsterState.Value != HamsterStateEnum.Dead;
    }

    /// <summary>
    /// Обрабатывает контакт сплошного skateboard sensor через общий collision policy.
    /// </summary>
    public bool TryHandleSkateboardSensorCollision(Collider2D other, bool isStay)
    {
        if (!_hamster.IsSkateboardModeActive ||
            !TryResolveObstacle(other, out Obstacle obstacle))
        {
            return false;
        }

        // Оба skateboard collider routes сначала используют одинаковый lane gate.
        if (!HelpMethods.IsOnSameLine(_hamster.IsOnBottomLine.Value, obstacle))
        {
            return false;
        }

        if (isStay)
            ProcessTriggerStay(obstacle, "Sensor/Stay");
        else
            ProcessTriggerEnter(obstacle, "Sensor/Enter");

        return true;
    }

    private bool TryHandleCollectable(Obstacle obstacle)
    {
        if (obstacle == null ||
            obstacle.ObstacleType == null ||
            !SkateboardInteractionPolicy.IsCollectable(
                obstacle.ObstacleType.ObstacleTypeEnum))
            return false;

        // Первый collider синхронно возвращает obstacle в пул. Второй callback только поглощается.
        if (!obstacle.isActiveAndEnabled || !IsCollectableState())
            return true;

        bool wasSkateboardModeActive = _hamster.IsSkateboardModeActive;
        bool wasJumpCollisionActive =
            _hamster.TryGetActiveSkateboardAttack(out SkateboardAttack attack) &&
            attack.TryGetCurrentJumpSnapshot(out _);
        int livesBefore = _hamster.Lives.Value;
        ObstacleTypeEnum obstacleType = obstacle.ObstacleType.ObstacleTypeEnum;
        HandleCollectable(obstacle);
        if (wasSkateboardModeActive)
        {
            PublishSkateboardCollision(
                obstacle,
                SkateboardCollisionOutcome.Collect,
                livesBefore,
                wasJumpCollisionActive,
                obstacleType);
        }
        return true;
    }

    /// <summary>
    /// Публикует один post-outcome снимок без изменения collision policy.
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void PublishSkateboardCollision(
        Obstacle obstacle,
        SkateboardCollisionOutcome outcome,
        int livesBefore,
        bool wasJumpCollisionActive,
        ObstacleTypeEnum? obstacleTypeOverride = null)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Obstacle roofSupport = _hamster.SkateboardSurfaceController.CurrentRoof;
        bool roofSupportActiveAfter =
            roofSupport == null || roofSupport.isActiveAndEnabled;
        SkateboardCollisionProcessed?.Invoke(
            new SkateboardCollisionDiagnostic(
                _hamster,
                obstacleTypeOverride ?? obstacle.ObstacleType.ObstacleTypeEnum,
                outcome,
                livesBefore,
                _hamster.Lives.Value,
                obstacle.isActiveAndEnabled,
                wasJumpCollisionActive,
                roofSupportActiveAfter));
#endif
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
    /// Выдаёт награду за подобранный объект и убирает его со сцены.
    /// </summary>
    private void HandleCollectable(Obstacle obstacle)
    {
        if (BotDiagnostics.IsEnabled(BotDiagnosticCategory.RuntimeEvents))
        {
            BotDiagnostics.Log(
                BotDiagnosticCategory.RuntimeEvents,
                BotDiagnosticLevel.Essential,
                $"[CollisionController] collect obstacle={FormatObstacle(obstacle)} " +
                $"state={_hamster.HamsterState.Value} lives={_hamster.Lives.Value} " +
                $"lane={(_hamster.IsOnBottomLine.Value ? "bottom" : "top")}");
        }

        var collectableType = obstacle.ObstacleType.ObstacleTypeEnum;

        // Выдаём награду за подтверждённый collectable.
        _hamster.CollectCoinsOrBonusAction.Invoke(obstacle);

        // Публикуем одно точное событие для счётчика очков забега.
        _hamster.CollectableCollectedEvent.Invoke(collectableType);

        // Убираем подобранный объект со сцены.
        UnspawnObstacle(obstacle);
    }

    /// <summary>
    /// Наносит урон хомяку и при необходимости уничтожает препятствие после столкновения.
    /// </summary>
    private void HandleDamage(Obstacle obstacle, string triggerSource, string reason)
    {
        if (BotDiagnostics.IsEnabled(BotDiagnosticCategory.RuntimeEvents))
        {
            BotDiagnostics.Log(
                BotDiagnosticCategory.RuntimeEvents,
                BotDiagnosticLevel.Essential,
                $"[CollisionController] damage source={triggerSource} reason={reason} " +
                $"state={_hamster.HamsterState.Value} livesBefore={_hamster.Lives.Value} " +
                $"isDamaged={_hamster.IsDamaged.Value} protected={_hamster.IsProtected.Value} " +
                $"superAttackDestructive={_hamster.IsSuperAttackDestructiveOnCollision.Value} " +
                $"lane={(_hamster.IsOnBottomLine.Value ? "bottom" : "top")} " +
                $"{FormatHamsterBounds()} " +
                $"obstacle={FormatObstacle(obstacle)} " +
                $"pending={FormatObstacle(_hamster.PendingJumpedOnObstacle.Value)}");
        }

        // Отправляем событие урона, если защита не активна.
        if (!_hamster.IsProtected.Value)
        {
            _hamster.DamageEvent.Invoke();
        }

        // Удаляем препятствие через поток суперудара без drops и нового заряда.
        if (_hamster.IsSuperAttackDestructiveOnCollision.Value)
        {
            _hamster.DestroyObstacleBySuperAttackEvent?.Invoke(obstacle);
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
