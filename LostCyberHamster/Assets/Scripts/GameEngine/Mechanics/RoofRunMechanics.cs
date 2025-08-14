using Assets.Scripts.Common.Models;
using Assets.Scripts.Common;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Installers.Roots;
using Assets.Scripts;
using Atomic.Elements;
using UnityEngine;
using Assets.Scripts.GameEngine.Controllers;

public class RoofRunMechanics
{
    private readonly Transform _transform;
    private readonly AtomicVariable<Obstacle> _lastObstacle;
    private readonly AtomicVariable<bool> _isOnBottomLine;
    private readonly AtomicVariable<HamsterStateEnum> _hamsterState;
    private readonly EnvironmentRoot _environmentRoot;
    private TransformAnimatorController _transformAnimatorController;

    public RoofRunMechanics(Transform transform,
        AtomicVariable<Obstacle> lastObstacle,
        AtomicVariable<HamsterStateEnum> hamsterState,
        AtomicVariable<bool> isOnBottomLine,
        TransformAnimatorController transformAnimatorController)
    {
        _transform = transform;
        _lastObstacle = lastObstacle;
        _hamsterState = hamsterState;
        _isOnBottomLine = isOnBottomLine;
        _transformAnimatorController = transformAnimatorController;

        _environmentRoot = GameObject.FindWithTag("EnvironmentRoot").GetComponent<EnvironmentRoot>();
    }

    public void OnUpdate()
    {
        if (_hamsterState.Value != HamsterStateEnum.RoofRun)
            return;

        if (_lastObstacle.Value == null)
            return;

        CheckRoofRunState();
    }

    /// <summary>
    /// Последовательно проверяем «сдвиг по вертикали» и «конец препятствия по горизонтали».
    /// </summary>
    private void CheckRoofRunState()
    {
        if (CheckRoofShifted())
            return;

        CheckRoofEnd();
    }

    /// <summary>
    /// Проверяем, не сместился ли Хомяк так, что перестал совпадать с линией большого препятствия.
    /// Возвращает true, если произошёл спрыг или переключение.
    /// </summary>
    private bool CheckRoofShifted()
    {
        // Ещё совпадаем по линии?
        if (HelpMethods.IsOnSameLine(_isOnBottomLine.Value, _lastObstacle.Value))
        {
            return false;
        }

        // Иначе пытаемся найти новое bigNotAlive прямо под Хомяком
        var newBigNotAlive = HelpMethods.FindBigNotAliveUnderHamster(
            _transform,
            _environmentRoot,
            _isOnBottomLine.Value
        );

        // Если не нашли, то спрыгиваем
        if (newBigNotAlive == null)
        {
            ToRunFromRoof();
            return true;
        }

        // Иначе переключаемся на новое препятствие
        _lastObstacle.Value = newBigNotAlive;
        return true;
    }

    /// <summary>
    /// Проверяем, не дошёл ли Хомяк до конца текущего препятствия по X. 
    /// Если дошёл - пытаемся перейти к следующему bigNotAlive. 
    /// Возвращает true, если произошёл спрыг или переключение.
    /// </summary>
    private void CheckRoofEnd()
    {
        float distance = _transform.position.x - _lastObstacle.Value.transform.position.x;
        if (distance < 0f) return;


        var hamsterWidthUnits = HelpMethods.FromPixelsToUnitsWidth(Consts.HAMSTER_WIDTH);
        // Пусть есть дополнительный запас
        float extendedEdge = hamsterWidthUnits * 0.75f;
        if (distance > extendedEdge)
        {
            var nextObstacle = FindNextBigNotAliveOnSameLine(
                _lastObstacle.Value,
                _environmentRoot,
                _isOnBottomLine.Value
            );
            if (nextObstacle != null)
            {
                // Переключаемся на следующее препятствие
                _lastObstacle.Value = nextObstacle;
            }
            else
            {
                // Спрыгиваем
                ToRunFromRoof();
            }
        }
    }

    /// <summary>
    /// Метод «спрыгивания» с крыши (перевод в состояние RunFromRoof).
    /// </summary>
    private void ToRunFromRoof()
    {
        _hamsterState.Value = HamsterStateEnum.RunFromRoof;
        _transformAnimatorController.SetRunAnimationTrigger(_hamsterState);
    }

    /// <summary>
    /// Finds the closest "bigNotAlive" obstacle to the right on the same line.
    /// </summary>
    public static Obstacle FindNextBigNotAliveOnSameLine(
        Obstacle currentObstacle,
        EnvironmentRoot environmentRoot,
        bool isOnBottomLine
    )
    {
        var obstacles = environmentRoot
            .ObstaclesSpawnedContainer
            .GetComponentsInChildren<Obstacle>();

        // Зазор между препятствиями из ширины большого неживого препятствия и допуском
        var tolerance = Consts.BigNotAliveEdgeTolerance;
        float maxGap = Consts.BIG_NOTALIVE_WIDTH_UNITS + tolerance;

        foreach (var obstacle in obstacles)
        {
            // 1) Это должно быть большое неживое препятствие
            if (obstacle.ObstacleType.ObstacleTypeEnum != ObstacleTypeEnum.bigNotAlive)
                continue;

            // 2) Должно быть на той же линии (top/bottom)
            if (!HelpMethods.IsOnSameLine(isOnBottomLine, obstacle))
                continue;

            // 3) Координата x должна быть больше, чем у текущего (иначе «позади»)
            float offset = obstacle.transform.position.x - currentObstacle.transform.position.x;
            if (offset <= 0f)
                continue;

            // 4) И не дальше нашего расширенного maxGap
            if (offset <= maxGap)
            {
                // Сразу возвращаем первое подходящее препятствие
                return obstacle;
            }
        }

        // Не нашли подходящего — возвращаем null
        return null;
    }
}
