using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.Gameplay.Enums;
using Atomic.Elements;
using Unity.Profiling;

namespace Assets.Scripts.GameEngine.Mechanics
{
    public class TapMechanics
    {
        private readonly AtomicEvent _tapRequest;
        private readonly AtomicVariable<bool> _isOnBottomLine;
        private readonly ShiftTransformAnimatorController _shiftTransformAnimatorController;
        private readonly AtomicVariable<HamsterStateEnum> _hamsterState;
        private readonly AtomicVariable<bool> _isShifting;

        private static readonly ProfilerMarker s_TapLogicMarker = new ProfilerMarker("TapLogic");

        public TapMechanics(AtomicEvent tapRequest, AtomicVariable<bool> isOnBottomLine,
            ShiftTransformAnimatorController shiftTransformAnimatorController,
            AtomicVariable<HamsterStateEnum> hamsterState,
            AtomicVariable<bool> isShifting)
        {
            _tapRequest = tapRequest;
            _isOnBottomLine = isOnBottomLine;
            _shiftTransformAnimatorController = shiftTransformAnimatorController;
            _hamsterState = hamsterState;
            _isShifting = isShifting;
        }

        public void OnUpdate()
        {
            _isShifting.Value = _shiftTransformAnimatorController.IsShifting();
        }

        public void OnEnable()
        {
            _tapRequest.Subscribe(OnTap);
        }

        public void OnDisable()
        {
            _tapRequest.Unsubscribe(OnTap);
        }

        private void OnTap()
        {
            using (s_TapLogicMarker.Auto())
            {
                // Игнорируем tap, если общее runtime-правило его отклоняет.
                if (!TapOutcomeResolver.CanAcceptTap(
                    _hamsterState.Value,
                    _isShifting.Value))
                {
                    return;
                }

                // Запускаем смену линии и синхронизируем публичное состояние.
                _shiftTransformAnimatorController.ToggleLane();
                _isOnBottomLine.Value = _shiftTransformAnimatorController.IsShiftedDown();
                _isShifting.Value = _shiftTransformAnimatorController.IsShifting();
            }
        }

    }
}
