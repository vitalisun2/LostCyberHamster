using Assets.Scripts.System;
using GameAds;
using UnityEngine;
using Vues.GameCore;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System.Linq;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem.UI;
#endif

public class GameEventSystemController : MonoBehaviour
{
    private static GameObject _globalEventSystem;

    void Awake()
    {
        DontDestroyOnLoad(this.gameObject);
        EnsureSingleEventSystem();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureSingleEventSystem();
    }

    void OnEnable()
    {
        AdsManager.OnEnable();
        QuestManager.OnEnable();
        DebugManager.OnEnable();
        AnalyticsManager.OnEnable();
        LevelManager.OnEnable();
        MoneyStorage.OnEnable();
        CrystalStorage.OnEnable();
        VibrationManager.OnEnable();
    }

    void OnDisable()
    {
        AdsManager.OnDisable();
        QuestManager.OnDisable();
        DebugManager.OnDisable();
        AnalyticsManager.OnDisable();
        LevelManager.OnDisable();
        MoneyStorage.OnDisable();
        CrystalStorage.OnDisable();
        VibrationManager.OnDisable();
    }

    private static void EnsureSingleEventSystem()
    {
        var all = Object.FindObjectsOfType<EventSystem>(true);

        if (all.Length == 0)
        {
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            go.AddComponent<InputSystemUIInputModule>();
            Debug.Log("[UI] EventSystem создан (InputSystemUIInputModule).");
#else
        go.AddComponent<StandaloneInputModule>();
        Debug.Log("[UI] EventSystem создан (StandaloneInputModule).");
#endif
            _globalEventSystem = go;
            Object.DontDestroyOnLoad(go);
            return;
        }

        // выбрать кого оставить (предпочтение модулю текущей input-системы)
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        EventSystem keep = all.FirstOrDefault(e => e.GetComponent<InputSystemUIInputModule>() != null) ?? all[0];
#else
    EventSystem keep = all.FirstOrDefault(e => e.GetComponent<StandaloneInputModule>() != null) ?? all[0];
#endif

        // если уже есть глобальный — предпочесть его
        if (_globalEventSystem != null)
        {
            var globalEs = _globalEventSystem.GetComponent<EventSystem>();
            if (globalEs != null) keep = globalEs;
            else _globalEventSystem = keep.gameObject;
        }
        else
        {
            _globalEventSystem = keep.gameObject;
        }

        // удалить лишние
        int removed = 0;
        foreach (var es in all)
        {
            if (es != keep)
            {
                Object.Destroy(es.gameObject);
                removed++;
            }
        }

        Object.DontDestroyOnLoad(keep.gameObject);
        Debug.Log($"[UI] EventSystem унифицирован. Оставлен: {keep.name}. Удалено лишних: {removed}. Модуль: {keep.currentInputModule?.GetType().Name}");
    }
}
