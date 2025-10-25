using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameManagerLogic;
using Atomic.Objects;
using NotImplementedException = System.NotImplementedException;

namespace Assets.Scripts.Gameplay
{
    public class ScrollingEnvironment : AtomicObject,
        Listeners.IGameStartListener,
        Listeners.IGameUpdateListener,
        Listeners.IGamePauseListener,
        Listeners.IGameResumeListener
    {
        private float _scrollSpeed;
        private ScrollLeftMechanics _scrollLeftMechanics;
        private ScrollRepeatMechanic _scrollRepeatMechanic;

        public void Awake()
        {
            enabled = false;
        }

        public void Initialize(float scrollSpeed)
        {
            _scrollSpeed = scrollSpeed;
            _scrollLeftMechanics = new ScrollLeftMechanics(transform, scrollSpeed);
            _scrollRepeatMechanic = new ScrollRepeatMechanic(transform);
        }

        public void OnStart()
        {
            enabled = true;
            RefreshScrollBounds();
        }

        public void OnUpdate(float deltaTime)
        {
            if (_scrollLeftMechanics == null)
            {
                UnityEngine.Debug.LogError("[ScrollingEnvironment] Not initialized! Call Initialize() first.");
                return;
            }

            _scrollLeftMechanics.Update(deltaTime);
            _scrollRepeatMechanic.Update();
        }

        public void OnPause()
        {
            enabled = false;
        }

        public void OnResume()
        {
            enabled = true;
        }

        // Addressable sprites are plugged in after Awake; refresh bounds once the real sprite is present.
        public void RefreshScrollBounds()
        {
            _scrollRepeatMechanic?.RefreshBounds();
        }

    }
}
