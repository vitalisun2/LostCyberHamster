using System;
using Unity.VisualScripting;
using UnityEngine;
using Vues.GameCore;

public static class DebugManager
{
    public static void Log(string message)
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        Debug.Log(message);
#endif
    }

    public static void OnEnable()
    {
        GameEventsManager.OnCrystalsCollected += CrystallCollected;
        GameEventsManager.OnObstacleJumpedOver += ObstacleJumpedOver;
        GameEventsManager.OnObstacleJumpedOn += ObstacleJumpedOn;
        GameEventsManager.OnCoinCollected += CoinCollected;
        GameEventsManager.OnQuestCompleted += QuestCompleted;
        GameEventsManager.OnSkinPurchased += SkinPurchased;

    }

    public static void OnDisable()
    {
        GameEventsManager.OnCrystalsCollected -= CrystallCollected;
        GameEventsManager.OnObstacleJumpedOver -= ObstacleJumpedOver;
        GameEventsManager.OnObstacleJumpedOn -= ObstacleJumpedOn;
        GameEventsManager.OnCoinCollected -= CoinCollected;
        GameEventsManager.OnQuestCompleted -= QuestCompleted;
        GameEventsManager.OnSkinPurchased -= SkinPurchased;
    }

    private static void SkinPurchased(int skinId, ResourceType type, int price)
    {
        Log("Skin purchased: " + skinId + " " + type + " " + price);
    }


    private static void QuestCompleted(string obj)
    {
        Log("Quest completed: " + obj);
    }


    private static void CoinCollected(int obj)
    {
        Log("Coin collected: " + obj);
    }


    private static void ObstacleJumpedOn(string obj)
    {
        Log("Obstacle jumped on: " + obj);
    }


    private static void CrystallCollected(int obj)
    {
        Log("Crystall collected: " + obj);
    }

    private static void ObstacleJumpedOver(string obj)
    {
        Log("Obstacle jumped over: " + obj);
    }
}
