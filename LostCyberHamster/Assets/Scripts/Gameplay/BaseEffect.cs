using System;
using System.Collections.Generic;
using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.System;
using UnityEngine;

namespace Assets.Scripts.Gameplay
{
    public abstract class BaseEffect : MonoBehaviour,
        Listeners.IGamePauseListener,
        Listeners.IGameResumeListener
    {
        private TransformAnimatorEventsDispatcher _transformAnimatorEventsDispatcher;
        private Animator[] _animators;
        private GameManager _gameManager;

        private void Awake()
        {
            _gameManager = LevelController.Instance.LevelData.GameManager;
            _gameManager.AddListener(this);

            _transformAnimatorEventsDispatcher = GetAnimatorEventsDispatcher();
            _animators = GetComponentsInChildren<Animator>();
        }

        private void OnEnable()
        {
            _transformAnimatorEventsDispatcher.OnEvent += OnAnimationEvent;
        }

        private void OnDisable()
        {
            _transformAnimatorEventsDispatcher.OnEvent -= OnAnimationEvent;
        }

        private void OnAnimationEvent(string animEvent)
        {
            if (animEvent == "effectAnimEnd")
            {

                _gameManager.RemoveListener(this);

                Destroy(gameObject);
            }
        }

        private TransformAnimatorEventsDispatcher GetAnimatorEventsDispatcher()
        {
            var dispatcher = GetComponentInChildren<TransformAnimatorEventsDispatcher>();

            if (dispatcher == null)
                throw new Exception("TransformAnimatorEventsDispatcher not found");

            return dispatcher;
        }

        public void OnPause()
        {
            _animators ??= GetComponentsInChildren<Animator>();

            foreach (var animator in _animators)
            {
                animator.enabled = false;
            }
        }

        public void OnResume()
        {
            _animators ??= GetComponentsInChildren<Animator>();

            foreach (var animator in _animators)
            {
                animator.enabled = true;
            }
        }
    }
}
