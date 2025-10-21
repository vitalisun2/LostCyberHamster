using System;
using System.Collections.Generic;
using Assets.Scripts.Gameplay;
using Sirenix.OdinInspector;
using UnityEngine;
using Zenject;
using static Assets.Scripts.GameManagerLogic.Listeners;
using UnityEngine.Rendering;

namespace Assets.Scripts.GameManagerLogic
{
    public sealed class GameManager : MonoBehaviour
    {
        public GameState State => this._state;

        private GameState _state;

        public event Action OnFinish;

        private List<Listeners.IGameListener> _listeners = new();
        private List<Listeners.IGameUpdateListener> _updateListeners = new();
        private List<Listeners.IGameFixedUpdateListener> _fixedUpdateListeners = new();
        private List<Listeners.IGameLateUpdateListener> _lateUpdateListeners = new();

        /// <summary>
        /// Коэффициент масштабирования времени для игры.
        /// </summary>
        public float TimeScaleCoefficient = 1f;

        [Inject]
        public void Construct()
        {
            // Initialization logic if needed
            _state = GameState.OFF;
        }

        private static GameManager _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogError("Multiple instances of GameManager detected. Destroying duplicate instance.");
                Destroy(gameObject);
                return;
            }

            _instance = this;

            // Управление частотой кадров: устанавливаем 60 FPS и включаем VSync 1 для стабилизации обновлений
            Application.targetFrameRate = Consts.FPS;
            QualitySettings.vSyncCount = 0;
#if UNITY_ANDROID
            SetupFramePacing();
#endif
        }

        private void Update()
        {
            if (_state != GameState.PLAYING)
                return;

            var deltaTime = Time.deltaTime;
            for (int i = 0; i < _updateListeners.Count; i++)
            {
                var listener = _updateListeners[i];
                listener.OnUpdate(deltaTime);
            }
        }

        private void FixedUpdate()
        {
            if (_state != GameState.PLAYING)
                return;

            var deltaTime = Time.fixedDeltaTime;
            for (int i = 0; i < _fixedUpdateListeners.Count; i++)
            {
                var listener = _fixedUpdateListeners[i];
                listener.OnFixedUpdate(deltaTime);
            }
        }

        private void LateUpdate()
        {
            if (_state != GameState.PLAYING)
                return;

            var deltaTime = Time.deltaTime;
            for (int i = 0; i < _lateUpdateListeners.Count; i++)
            {
                var listener = _lateUpdateListeners[i];
                listener.OnLateUpdate(deltaTime);
            }
        }

        public void AddListener(Listeners.IGameListener listener)
        {
            if (listener == null)
            {
                return;
            }

            _listeners.Add(listener);

            if (listener is Listeners.IGameUpdateListener updateListener)
            {
                _updateListeners.Add(updateListener);
            }


            if (listener is Listeners.IGameFixedUpdateListener fixedUpdateListener)
            {
                _fixedUpdateListeners.Add(fixedUpdateListener);
            }

            if (listener is Listeners.IGameLateUpdateListener lateUpdateListener)
            {
                _lateUpdateListeners.Add(lateUpdateListener);
            }
        }

        public void RemoveListener(IGameListener listener)
        {
            if (listener == null)
            {
                return;
            }

            _listeners.Remove(listener);

            if (listener is IGameUpdateListener updateListener)
            {
                _updateListeners.Remove(updateListener);
            }

            if (listener is IGameFixedUpdateListener fixedUpdateListener)
            {
                _fixedUpdateListeners.Remove(fixedUpdateListener);
            }

            if (listener is IGameLateUpdateListener lateUpdateListener)
            {
                _lateUpdateListeners.Remove(lateUpdateListener);
            }
        }

        public void StartIntro()
        {
            Debug.Log("Game Intro");

            foreach (var listener in _listeners)
            {
                if (listener is Listeners.IGameIntroListener introListener)
                {
                    introListener.OnIntro();
                }
            }

            _state = GameState.INTRO;
        }

        [Button]
        public void StartGame()
        {
            Debug.Log("Game Started");

            foreach (var listener in _listeners)
            {
                if (listener is Listeners.IGameStartListener startListener)
                {
                    startListener.OnStart();
                }
            }

            TimeScaleCoefficient = 1f; 
            _state = GameState.PLAYING;
        }

        [Button]
        public void Finish()
        {
            Debug.Log("Game Finished");

            foreach (var listener in _listeners)
            {
                if (listener is Listeners.IGameFinishListener finishListener)
                {
                    finishListener.OnFinish();
                }
            }

            _state = GameState.FINISHED;
            OnFinish?.Invoke();
        }

        [Button]
        public void Pause()
        {

            Debug.Log("Game Paused");

            foreach (var listener in _listeners)
            {
                if (listener is Listeners.IGamePauseListener pauseListener)
                {
                    pauseListener.OnPause();
                }
            }

            TimeScaleCoefficient = 0; // Останавливаем время в игре
            _state = GameState.PAUSED;
        }

        [Button]
        public void Resume()
        {

            Debug.Log("Game Resumed");

            foreach (var listener in _listeners)
            {
                if (listener is Listeners.IGameResumeListener resumeListener)
                {
                    resumeListener.OnResume();
                }
            }

            TimeScaleCoefficient = 1f;
            _state = GameState.PLAYING;
        }

#if UNITY_ANDROID
        // +++ ADD: вызывать один раз при запуске
        private void SetupFramePacing()
        {
            int rr = 60;
            try
            {
#if UNITY_2022_1_OR_NEWER
                rr = (int)Mathf.Round(Screen.currentResolution.refreshRateRatio.value); // e.g., 60, 90, 120
#else
                rr = Screen.currentResolution.refreshRate;
#endif
            }
            catch { rr = 60; }

            QualitySettings.vSyncCount = 1;            // sync to display
            Application.targetFrameRate = rr;          // target real refresh rate

            // If you want to always 60 even on 120 Hz – enable:
            OnDemandRendering.renderFrameInterval = Mathf.Max(1, rr / 60); // 2 on 120 Hz
        }
#endif
    }
}
