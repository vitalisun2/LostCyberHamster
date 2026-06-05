using Assets.Scripts.GameManagerLogic;
using UnityEngine;

namespace Assets.Scripts.GameEngine.Controllers
{
    public class ShiftTransformAnimatorController : MonoBehaviour,
        Listeners.IGamePauseListener,
        Listeners.IGameResumeListener,
        Listeners.IGameFinishListener
    {
        private const string _shiftedDownParameterName = "IsShiftedDown";
        private const string _defaultStateName = "Base Layer.transform_default";
        private const string _downStateName = "Base Layer.transform_down";
        private const string _transitionDefaultToDownName = "Base Layer.transform_default -> Base Layer.transform_down";
        private const string _transitionDownToDefaultName = "Base Layer.transform_down -> Base Layer.transform_default";

        private Animator _animator;

        private int _defaultStateHash;
        private int _downStateHash;
        private int _transitionDefaultToDownHash;
        private int _transitionDownToDefaultHash;
        private bool _hasPendingShift;
        private bool _pendingShiftedDown;

        private void Awake()
        {
            _animator = GetComponent<Animator>();

            _defaultStateHash = Animator.StringToHash(_defaultStateName);
            _downStateHash = Animator.StringToHash(_downStateName);
            _transitionDefaultToDownHash = Animator.StringToHash(_transitionDefaultToDownName);
            _transitionDownToDefaultHash = Animator.StringToHash(_transitionDownToDefaultName);
        }

        public void ToggleLane()
        {
            bool targetShiftedDown = !IsShiftedDown();
            _animator.SetBool(_shiftedDownParameterName, targetShiftedDown);
            _pendingShiftedDown = targetShiftedDown;
            _hasPendingShift = true;
        }

        public bool IsShiftedDown()
        {
            return _animator.GetBool(_shiftedDownParameterName);
        }

        public void OnPause()
        {
            _animator.enabled = false;
        }

        public void OnResume()
        {
            _animator.enabled = true;
        }

        public void OnFinish()
        {
            _animator.enabled = false;
        }

        public bool IsShifting()
        {
            if (IsKnownShiftTransition())
                return true;

            if (!_hasPendingShift)
                return false;

            if (!IsInTargetShiftState())
                return true;

            _hasPendingShift = false;
            return false;
        }

        private bool IsKnownShiftTransition()
        {
            if (!_animator.IsInTransition(0))
                return false;

            var transitionInfo = _animator.GetAnimatorTransitionInfo(0);
            int currentTransitionHash = transitionInfo.fullPathHash;

            return currentTransitionHash == _transitionDefaultToDownHash
                   || currentTransitionHash == _transitionDownToDefaultHash;
        }

        private bool IsInTargetShiftState()
        {
            int targetStateHash = _pendingShiftedDown
                ? _downStateHash
                : _defaultStateHash;

            AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            return stateInfo.fullPathHash == targetStateHash;
        }
    }
}
