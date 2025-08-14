using UnityEngine;
using Assets.Scripts.GameManagerLogic;
using NotImplementedException = System.NotImplementedException;

namespace Assets.Scripts.GameEngine.Controllers
{
    public class SpriteAnimatorController : MonoBehaviour,
        Listeners.IGamePauseListener,
        Listeners.IGameResumeListener,
        Listeners.IGameFinishListener,
        Listeners.IGameIntroListener,
        Listeners.IGameStartListener
    {
        private Animator _animator;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        public void Jump()
        {
            _animator.SetTrigger("IsJump");
        }

        public void Blink()
        {
            _animator.SetTrigger("IsBlink");
        }

        public void OnPause()
        {
            _animator.enabled = false;
        }

        public void OnResume()
        {
            _animator.enabled = true;
        }

        public void OnIntro()
        {
            _animator.enabled = false;
        }

        public void OnStart()
        {
            _animator.enabled = true;
        }

        public void OnFinish()
        {
            _animator.enabled = false;
        }
    }
}
