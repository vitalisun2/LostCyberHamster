using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.Gameplay.Enums;
using Atomic.Elements;

namespace Assets.Scripts.GameEngine.Mechanics
{
    public class TapMechanics
    {
        private readonly AtomicEvent _tapRequest;
        private readonly AtomicVariable<bool> _isOnBottomLine;
        private readonly ShiftTransformAnimatorController _shiftTransformAnimatorController;
        private readonly AtomicVariable<HamsterStateEnum> _hamsterState;
        private readonly AtomicVariable<bool> _isShifting;
        private readonly AtomicVariable<bool> _isDamaged;

        public TapMechanics(AtomicEvent tapRequest, AtomicVariable<bool> isOnBottomLine,
            ShiftTransformAnimatorController shiftTransformAnimatorController,
            AtomicVariable<HamsterStateEnum> hamsterState,
            AtomicVariable<bool> isShifting,
            AtomicVariable<bool> isDamaged)
        {
            _tapRequest = tapRequest;
            _isOnBottomLine = isOnBottomLine;
            _shiftTransformAnimatorController = shiftTransformAnimatorController;
            _hamsterState = hamsterState;
            _isShifting = isShifting;
            _isDamaged = isDamaged;
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
            if (_hamsterState.Value != HamsterStateEnum.Run &&
                _hamsterState.Value != HamsterStateEnum.RoofRun &&
                !_isDamaged.Value)
                return;

            if (_isShifting.Value)
                return;

            _shiftTransformAnimatorController.ShiftToBottomLine();
            _isOnBottomLine.Value = _shiftTransformAnimatorController.IsShiftedDown();
        }

    }
}
