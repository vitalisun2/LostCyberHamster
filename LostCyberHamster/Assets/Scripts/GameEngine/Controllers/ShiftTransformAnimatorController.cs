using Assets.Scripts.GameManagerLogic;
using UnityEngine;

namespace Assets.Scripts.GameEngine.Controllers
{
    public class ShiftTransformAnimatorController : MonoBehaviour,
        Listeners.IGamePauseListener,
        Listeners.IGameResumeListener,
        Listeners.IGameFinishListener
    {
        private Animator _animator;
        private string _transitionDefaultToDownName = "Base Layer.transform_default -> Base Layer.transform_down";
        private string _transitionDownToDefaultName = "Base Layer.transform_down -> Base Layer.transform_default";

        private int _transitionDefaultToDownHash;
        private int _transitionDownToDefaultHash;

        private void Awake()
        {
            _animator = GetComponent<Animator>();

            _transitionDefaultToDownHash = Animator.StringToHash(_transitionDefaultToDownName);
            _transitionDownToDefaultHash = Animator.StringToHash(_transitionDownToDefaultName);
        }

        public void ShiftToBottomLine()
        {
            _animator.SetBool("IsShiftedDown", !IsShiftedDown());
        }

        public bool IsShiftedDown()
        {
            return _animator.GetBool("IsShiftedDown");
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
            if (!_animator.IsInTransition(0))
                return false;

            var transitionInfo = _animator.GetAnimatorTransitionInfo(0);
            int currentTransitionHash = transitionInfo.fullPathHash;

            return currentTransitionHash == _transitionDefaultToDownHash
                   || currentTransitionHash == _transitionDownToDefaultHash;
        }
    }
}
