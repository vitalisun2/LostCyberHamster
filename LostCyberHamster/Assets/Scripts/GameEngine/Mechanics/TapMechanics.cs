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
        private readonly LaneSwitchContactGrace _contactGrace;
        private readonly float _hamsterLeftX;
        private readonly float _hamsterRightX;

        private static readonly ProfilerMarker s_TapLogicMarker = new ProfilerMarker("TapLogic");

        public TapMechanics(AtomicEvent tapRequest, AtomicVariable<bool> isOnBottomLine,
            ShiftTransformAnimatorController shiftTransformAnimatorController,
            AtomicVariable<HamsterStateEnum> hamsterState,
            AtomicVariable<bool> isShifting,
            LaneSwitchContactGrace contactGrace,
            float hamsterLeftX,
            float hamsterRightX)
        {
            _tapRequest = tapRequest;
            _isOnBottomLine = isOnBottomLine;
            _shiftTransformAnimatorController = shiftTransformAnimatorController;
            _hamsterState = hamsterState;
            _isShifting = isShifting;
            _contactGrace = contactGrace;
            _hamsterLeftX = hamsterLeftX;
            _hamsterRightX = hamsterRightX;
        }

        /// <summary>Обновляет состояние перехода и его льготу контакта.</summary>
        public void OnUpdate()
        {
            // Синхронизируем состояние с animator.
            _isShifting.Value = _shiftTransformAnimatorController.IsShifting();
            // Завершаем льготу после перехода.
            if (!_isShifting.Value)
                _contactGrace.Clear();
        }

        public void OnEnable()
        {
            _tapRequest.Subscribe(OnTap);
        }

        /// <summary>Отключает обработку tap и завершает льготу контакта.</summary>
        public void OnDisable()
        {
            _tapRequest.Unsubscribe(OnTap);
            _contactGrace.Clear();
        }

        /// <summary>Запускает допустимую смену линии и фиксирует уходящие препятствия.</summary>
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

                // Запоминаем разрешённые контакты до публикации новой линии.
                bool targetBottomLine = !_shiftTransformAnimatorController.IsShiftedDown();
                _contactGrace.Begin(targetBottomLine, _hamsterLeftX, _hamsterRightX);

                // Запускаем смену линии и синхронизируем публичное состояние.
                _shiftTransformAnimatorController.ToggleLane();
                _isOnBottomLine.Value = _shiftTransformAnimatorController.IsShiftedDown();
                _isShifting.Value = _shiftTransformAnimatorController.IsShifting();
            }
        }

    }
}
