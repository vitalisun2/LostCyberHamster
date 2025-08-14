using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.System;
using GameAds;
using UnityEngine;
using Vues.GameCore;

public class GameEventSystemController : MonoBehaviour
{
    void Awake()
    {
        DontDestroyOnLoad(this.gameObject);
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
}
