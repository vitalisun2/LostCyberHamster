using System;
using UnityEngine;

namespace GameManagement
{
    public static class PlayerProgressCommitter
    {
        /// <summary>Возникает после успешного завершения локального checkpoint.</summary>
        public static event Action<CheckpointReason> CommitCompleted;

        /// <summary>Сохраняет текущий прогресс локально и уведомляет потребителей checkpoint.</summary>
        public static void Commit(CheckpointReason reason)
        {
            // Сначала завершаем обязательное локальное сохранение.
            GameDataManager.SaveData();
            Debug.Log($"[GameData] Commit: {reason}.");

            // Затем независимо уведомляем каждого фонового потребителя.
            var handlers = CommitCompleted;
            if (handlers == null)
                return;

            foreach (Action<CheckpointReason> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(reason);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[GameData] Commit subscriber failed ({exception.GetType().Name}).");
                }
            }
        }
    }
}
