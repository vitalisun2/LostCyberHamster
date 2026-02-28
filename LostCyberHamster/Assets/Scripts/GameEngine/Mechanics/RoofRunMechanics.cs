using Assets.Scripts.Common.Models;
using Assets.Scripts.Common;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Installers.Roots;
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
    private readonly float _hamsterWidthInUnits;

    public RoofRunMechanics(Transform transform,
        AtomicVariable<Obstacle> lastObstacle,
        AtomicVariable<HamsterStateEnum> hamsterState,
        AtomicVariable<bool> isOnBottomLine,
        TransformAnimatorController transformAnimatorController,
        float hamsterWidthInUnits)
    {
        _transform = transform;
        _lastObstacle = lastObstacle;
        _hamsterState = hamsterState;
        _isOnBottomLine = isOnBottomLine;
        _transformAnimatorController = transformAnimatorController;
        _hamsterWidthInUnits = hamsterWidthInUnits;

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
        if (HelpMethods.IsOnSameLine(_isOnBottomLine.Value, _lastObstacle.Value))
        {
            return false;
        }

        var newBigNotAlive = HelpMethods.FindBigNotAliveUnderHamster(
            _transform,
            _environmentRoot,
            _isOnBottomLine.Value
        );

        if (newBigNotAlive == null)
        {
            ToRunFromRoof();
            return true;
        }

        _lastObstacle.Value = newBigNotAlive;
        SwapRoofClipsForObstacle(newBigNotAlive);
        return true;
    }

    /// <summary>
    /// Проверяем, не дошёл ли Хомяк до конца текущего препятствия по X. 
    /// Если дошёл - пытаемся перейти к следующему bigNotAlive. 
    /// Возвращает true, если произошёл спрыг или переключение.
    /// </summary>
    private void CheckRoofEnd()
    {
        var current = _lastObstacle.Value;

        CollisionUtils.GetObstacleXInterval(current, current.ColliderWidth, 0f, out _, out var roofRight);

        CollisionUtils.GetHamsterXBounds(_transform, out var hamsterLeft, out var hamsterRight);

        if (!HasReachedNextRoofCheckPoint(hamsterRight, roofRight))
            return;

        var nextObstacle = FindNextBigNotAliveOnSameLine(
            current, _environmentRoot, _isOnBottomLine.Value, hamsterLeft, hamsterRight);

        if (nextObstacle != null)
        {
            _lastObstacle.Value = nextObstacle;
            SwapRoofClipsForObstacle(nextObstacle);
        }
        else
        {
            ToRunFromRoof();
        }
    }

    /// <summary>
    /// Переключает roof-клипы аниматора в соответствии с типом препятствия.
    /// </summary>
    private void SwapRoofClipsForObstacle(Obstacle obstacle)
    {
        bool isMedium = obstacle.ObstacleType.ObstacleTypeEnum == ObstacleTypeEnum.mediumNotAlive;
        _transformAnimatorController.SwapRoofClips(isMedium);
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
    /// Находит ближайшую справа "bigNotAlive" на той же линии.
    /// Возвращает плиту, которая находится ПОД левой кромкой хомяка (hamsterLeftX)
    /// в момент, когда хомяк полностью вышел за правый край текущей крыши.
    /// </summary>
    public Obstacle FindNextBigNotAliveOnSameLine(
        Obstacle currentObstacle,
        EnvironmentRoot environmentRoot,
        bool isOnBottomLine,
        float hamsterLeftX,
        float hamsterRightX
    )
    {
        var obstaclesAhead = CollisionUtils.GetValidObstaclesAhead(_transform, isOnBottomLine);

        foreach (var obstacle in obstaclesAhead)
        {
            if (!CollisionUtils.IsRoofObstacle(obstacle.ObstacleType.ObstacleTypeEnum))
                continue;

            CollisionUtils.GetObstacleXInterval(obstacle, obstacle.ColliderWidth, 0f, out var nextLeft, out var nextRight);

            bool overlaps = (hamsterRightX > nextLeft) && (hamsterLeftX < nextRight);

            if (overlaps)
            {
                return obstacle;
            }
        }

        return null;
    }

    /// <summary>
    /// Определяет момент, когда ПРАВАЯ кромка хомяка ушла за правый край текущего препятствия
    /// на заданный процент его ширины (по умолчанию 70%).
    /// В этот момент нужно проверять наличие следующей крыши.
    /// </summary>
    /// <param name="hamsterRight">Правая кромка хомяка.</param>
    /// <param name="roofRight">Правая кромка текущей крыши.</param>
    /// <returns>True — если наступил момент проверки следующей крыши.</returns>
    private bool HasReachedNextRoofCheckPoint(float hamsterRight, float roofRight)
    {
        float threshold = _hamsterWidthInUnits * 0.7f; // 70% ширины хомяка
        return hamsterRight >= roofRight + threshold;
    }

}
