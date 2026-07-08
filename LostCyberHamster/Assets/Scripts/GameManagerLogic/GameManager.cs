using System;
using System.Collections.Generic;
using Assets.Scripts.Diagnostics;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;
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
            bool isAutomationRun = AutomationRuntimePrefs.IsTestLevelAutomationRun();
            RuntimePerformanceDiagnostics.SetEnabled(isAutomationRun);
        }

        private void Update()
        {
            if (_state != GameState.PLAYING)
                return;

            RuntimePerformanceDiagnostics.SampleFrame();
            long updateSample = RuntimePerformanceDiagnostics.BeginAllocationSample(
                RuntimePerformanceScope.GameManagerUpdateLoop);
            try
            {
                var deltaTime = Time.deltaTime;
                for (int i = 0; i < _updateListeners.Count; i++)
                {
                    var listener = _updateListeners[i];
                    RuntimePerformanceScope listenerScope = GetUpdateListenerScope(listener);
                    long listenerSample = RuntimePerformanceDiagnostics.BeginAllocationSample(listenerScope);
                    try
                    {
                        listener.OnUpdate(deltaTime);
                    }
                    finally
                    {
                        RuntimePerformanceDiagnostics.EndAllocationSample(listenerScope, listenerSample);
                    }
                }
            }
            finally
            {
                RuntimePerformanceDiagnostics.EndAllocationSample(
                    RuntimePerformanceScope.GameManagerUpdateLoop,
                    updateSample);
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

            long lateUpdateSample = RuntimePerformanceDiagnostics.BeginAllocationSample(
                RuntimePerformanceScope.GameManagerLateUpdateLoop);
            try
            {
                var deltaTime = Time.deltaTime;
                for (int i = 0; i < _lateUpdateListeners.Count; i++)
                {
                    var listener = _lateUpdateListeners[i];
                    RuntimePerformanceScope listenerScope = GetLateUpdateListenerScope(listener);
                    long listenerSample = RuntimePerformanceDiagnostics.BeginAllocationSample(listenerScope);
                    try
                    {
                        listener.OnLateUpdate(deltaTime);
                    }
                    finally
                    {
                        RuntimePerformanceDiagnostics.EndAllocationSample(listenerScope, listenerSample);
                    }
                }
            }
            finally
            {
                RuntimePerformanceDiagnostics.EndAllocationSample(
                    RuntimePerformanceScope.GameManagerLateUpdateLoop,
                    lateUpdateSample);
            }
        }

        private static RuntimePerformanceScope GetUpdateListenerScope(Listeners.IGameUpdateListener listener)
        {
            if (listener is Obstacle)
                return RuntimePerformanceScope.GameManagerUpdateObstacleListener;

            if (listener is Hamster)
                return RuntimePerformanceScope.GameManagerUpdateHamsterListener;

            if (listener is ScrollingEnvironment)
                return RuntimePerformanceScope.GameManagerUpdateScrollingEnvironmentListener;

            if (listener is GameUi)
                return RuntimePerformanceScope.GameManagerUpdateGameUiListener;

            if (listener is ObstacleSpawner)
                return RuntimePerformanceScope.GameManagerUpdateObstacleSpawnerListener;

            return RuntimePerformanceScope.GameManagerUpdateOtherListener;
        }

        private static RuntimePerformanceScope GetLateUpdateListenerScope(Listeners.IGameLateUpdateListener listener)
        {
            if (listener is Assets.Scripts.Bot.RuntimeBotController)
                return RuntimePerformanceScope.GameManagerLateUpdateRuntimeBotListener;

            return RuntimePerformanceScope.GameManagerLateUpdateOtherListener;
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
            foreach (var listener in _listeners)
            {
                if (listener is not Listeners.IGameStartListener startListener)
                {
                    continue;
                }

                string listenerName = GetListenerName(listener);
                try
                {
                    startListener.OnStart();
                }
                catch (Exception exception)
                {
                    LogStartGameException($"listener={listenerName}", exception);
                    throw;
                }
            }

            TimeScaleCoefficient = 1f; 
            _state = GameState.PLAYING;
            DebugManager.DiagStability($"[GAME START] completed state={_state}");
        }

        private static string GetListenerName(Listeners.IGameListener listener)
        {
            return listener == null
                ? "<null>"
                : listener.GetType().FullName;
        }

        private static void LogStartGameException(string context, Exception exception)
        {
            DebugManager.DiagStability(
                $"[GAME START] exception context={context} " +
                $"type={exception.GetType().FullName} message={exception.Message} stack={exception.StackTrace}");
            Debug.LogException(exception);
            DeviceLogUploader.UploadDiagnosticLog("game_start_exception");
        }

        [Button]
        public void Finish()
        {
            foreach (var listener in _listeners)
            {
                if (listener is Listeners.IGameFinishListener finishListener)
                {
                    finishListener.OnFinish();
                }
            }

            _state = GameState.FINISHED;
            RuntimePerformanceDiagnostics.LogSummary("finish");
            OnFinish?.Invoke();
        }

        [Button]
        public void Pause()
        {
            foreach (var listener in _listeners)
            {
                if (listener is Listeners.IGamePauseListener pauseListener)
                {
                    pauseListener.OnPause();
                }
            }

            TimeScaleCoefficient = 0; // Останавливаем время в игре
            _state = GameState.PAUSED;
            RuntimePerformanceDiagnostics.LogSummary("pause");
        }

        [Button]
        public void Resume()
        {
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
                rr = (int)global::System.Math.Round(Screen.currentResolution.refreshRateRatio.value); // e.g., 60, 90, 120
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
