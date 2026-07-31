using System;
using UnityEngine;
using Vues.GameCore;
using Vues.GameCore.Quests;

public static class GameEventsManager
{
    public static event Action OnShowShopScreen;
    public static event Action OnShowLevelsScreen;
    public static event Action OnStartGame;

    public static event Action<string> OnErrorOccurred;
    public static event Action<string> OnPutSkin;


    public static event Action OnShowAd;
    public static void ShowAd() => OnShowAd?.Invoke();

    public static event Action OnAdCompleted;
    public static void AdCompleted() => OnAdCompleted?.Invoke();

    public static void ShowShopScreen() => OnShowShopScreen?.Invoke();
    public static void ShowLevelsScreen() => OnShowLevelsScreen?.Invoke();
    public static void StartGame() => OnStartGame?.Invoke();
    public static void ErrorOccurred(string errorMessage) => OnErrorOccurred?.Invoke(errorMessage);
    public static void PutSkin(string skinId) => OnPutSkin?.Invoke(skinId);
    public static event Action<int, ResourceType, int> OnItemBought;
    public static void ItemBought(int itemId, ResourceType resourceType, int amount) => OnItemBought?.Invoke(itemId, resourceType, amount);

#region Квесты
    /// <summary>
    /// Прыжок через препятствие
    /// </summary>
    public static event Action<string> OnObstacleJumpedOver;

    /// <summary>
    /// Типизированные события действий для квестов-счётчиков.
    /// </summary>
    public static event Action<ActionCounterQuestEvent>
        OnActionCounterQuestEvent;

    /// <summary>
    /// Триггер события OnObstacleJumpedOver
    /// </summary>
    /// <param name="obstacleName">Идентификатор препятствия, через которое был совершен прыжок</param>
    public static void ObstacleJumpedOver(string obstacleName)
    {
        OnObstacleJumpedOver?.Invoke(obstacleName);
        OnActionCounterQuestEvent?.Invoke(
            new ActionCounterQuestEvent(
                GameplayActionIds.ObstacleJumpedOver,
                1));
    }

    /// <summary>
    /// Прыжок на препятствие
    /// </summary>
    public static event Action<string> OnObstacleJumpedOn;

    /// <summary>
    /// Триггер события OnObstacleJumpedOn
    /// </summary>
    /// <param name="obstacleNames">Идентификатор препятствия, на которое был совершен прыжок</param>
    public static void ObstacleJumpedOn(string obstacleName)
    {
        OnObstacleJumpedOn?.Invoke(obstacleName);
        OnActionCounterQuestEvent?.Invoke(
            new ActionCounterQuestEvent(
                GameplayActionIds.ObstacleJumpedOn,
                1));
    }

    /// <summary>
    /// Событие завершения квеста
    /// </summary>
    public static event Action<string> OnQuestCompleted;

    /// <summary>
    /// Триггер события OnQuestCompleted
    /// </summary>
    /// <param name="questId"></param>
    public static void QuestCompleted(string questId) => OnQuestCompleted?.Invoke(questId);

    /// <summary>
    /// Событие получения награды за квест
    /// </summary>
    public static event Action<string> OnQuestRewardReceived;

    /// <summary>
    /// Триггер события OnQuestGetReward
    /// </summary>
    /// <param name="questId"></param>
    public static void QuestRewardReceived(string questId) =>
        OnQuestRewardReceived?.Invoke(questId);

    /// <summary>
    /// Состояние квеста изменилось после обработки игровой команды.
    /// </summary>
    public static event Action<string> OnQuestStateChanged;

    /// <summary>
    /// Сообщает подписчикам об актуальном состоянии квеста.
    /// </summary>
    public static void QuestStateChanged(string questId) =>
        OnQuestStateChanged?.Invoke(questId);

#endregion

#region Уровень
    /// <summary>
    /// Событие старта уровня
    /// </summary>
    public static event Action<int> OnLevelStarted;

    /// <summary>
    /// Триггер события OnLevelStarted
    /// </summary>
    /// <param name="levelId">Номер уровня</param>
    public static void LevelStarted(int levelId) => OnLevelStarted?.Invoke(levelId);

    /// <summary>
    /// Событие завершения уровня
    /// </summary>
    public static event Action<int, int> OnLevelCompleted;

    /// <summary>
    /// Триггер события OnLevelCompleted
    /// </summary>
    /// <param name="levelId">Номер уровня</param>
    /// <param name="stars">Количество звезд</param>
    public static void LevelCompleted(int levelId, int stars) => OnLevelCompleted?.Invoke(levelId, stars);

#endregion
    /// <summary>
    /// Событие покупки скина
    /// </summary>
    /// <param name="skinId">Идентификатор скина</param>
    /// <param name="resourceType">Тип ресурса</param>
    /// <param name="amount">Количество ресурса</param>
    public static event Action<int, ResourceType, int> OnSkinPurchased;

    /// <summary>
    /// Триггер события OnSkinPurchased
    /// </summary>
    /// <param name="skinId">Идентификатор скина</param>
    /// <param name="resourceType">Тип ресурса</param>
    /// <param name="amount">Количество ресурса</param>
    public static void SkinPurchased(int skinId, ResourceType resourceType, int amount) => OnSkinPurchased?.Invoke(skinId, resourceType, amount);


    #region Монеты

    /// <summary>
    /// Событие траты монет
    /// </summary>
    public static event Action<int> OnCoinsSpent;

    /// <summary>
    /// Триггер события OnCoinsSpent
    /// </summary>
    /// <param name="amount"></param>
    public static void CoinsSpent(int amount) => OnCoinsSpent?.Invoke(amount);

    /// <summary>
    /// Событие собрать монету
    /// </summary>
    public static event Action<int> OnCoinCollected;

    /// <summary>
    /// Триггер события OnCoinCollected
    /// </summary>
    /// <param name="coinValue">Количество собранных монет</param>
    public static void CoinCollected(int coinValue) => OnCoinCollected?.Invoke(coinValue);

    /// <summary>
    /// Событие получения монет
    /// </summary>
    /// <param name="amount">Количество полученных монет</param>
    public static event Action<int> OnEarnCoins;

    /// <summary>
    /// Триггер события OnCoinsEarned
    /// </summary>
    /// <param name="amount">Количество полученных монет</param>
    public static void EarnCoins(int amount) => OnEarnCoins?.Invoke(amount);

    #endregion

    #region Кристалы

    /// <summary>
    /// Событие собрать кристалл
    /// </summary>
    public static event Action<int> OnCrystalsCollected;

    /// <summary>
    /// Триггер события OnCrystallCollected
    /// </summary>
    /// <param name="crystallValue">Количество собранных кристаллов</param>
    public static void CrystallCollected(int crystallValue) => OnCrystalsCollected?.Invoke(crystallValue);


    /// <summary>
    /// Событие траты кристаллов
    /// </summary>
    public static event Action<int> OnCrystalsSpent;

    /// <summary>
    /// Триггер события OnCrystalsSpent
    /// </summary>
    /// <param name="amount"></param>
    public static void CrystalsSpent(int amount) => OnCrystalsSpent?.Invoke(amount);


    /// <summary>
    /// Событие получения кристаллов
    /// </summary>
    public static event Action<int> OnEarnCrystals;

    /// <summary>
    /// Триггер события OnEarnCrystals
    /// </summary>
    /// <param name="amount"></param>
    public static void EarnCrystals(int amount) => OnEarnCrystals?.Invoke(amount);
    
    #endregion

    #region Жизни
    /// <summary>
    /// Событие получения жизни
    /// </summary>
    public static event Action<int> OnLivesAdded;

    /// <summary>
    /// Триггер события OnLivesAdded
    /// </summary>
    /// <param name="amount">Количество жизней</param>
    public static void LivesAdded(int amount) => OnLivesAdded?.Invoke(amount);

    /// <summary>
    /// Событие потери жизни
    /// </summary>
    public static event Action<int> OnLivesLost;

    /// <summary>
    /// Триггер события OnLivesLost
    /// </summary>
    /// <param name="amount">Количество жизней</param>
    public static void LivesLost(int amount) => OnLivesLost?.Invoke(amount);

    #endregion

    #region Энергия
    /// <summary>
    /// Событие получения энергии
    /// </summary>
    public static event Action<int> OnEnergyAdded;

    /// <summary>
    /// Триггер события OnEnergyAdded
    /// </summary>
    /// <param name="amount">Количество энергии</param>
    public static void EnergyAdded(int amount) => OnEnergyAdded?.Invoke(amount);

    /// <summary>
    /// Событие траты энергии
    /// </summary>
    public static event Action<int> OnEnergySpent;

    /// <summary>
    /// Триггер события OnEnergySpent
    /// </summary>
    /// <param name="amount">Количество энергии</param>
    public static void EnergySpent(int amount) => OnEnergySpent?.Invoke(amount);
    #endregion

    /// <summary>
    /// Событие использования ульта-способности
    /// </summary>
    public static event Action OnUltaUsed;

    /// <summary>
    /// Триггер события OnUltaUsed
    /// </summary>
    public static void UltaUsed() => OnUltaUsed?.Invoke();

    /// <summary>
    /// Событие активации ульта-способности (полной зарядки)
    /// </summary>
    public static event Action OnUltaActivated;

    /// <summary>
    /// Триггер события OnUltaActivated
    /// </summary>
    public static void UltaActivated() => OnUltaActivated?.Invoke();

    /// <summary>
    /// Событие столкновения с препятствием
    /// </summary>
    public static event Action OnObstacleCollision;

    /// <summary>
    /// Триггер события OnObstacleCollision
    /// </summary>
    public static void ObstacleCollision() => OnObstacleCollision?.Invoke();
}
