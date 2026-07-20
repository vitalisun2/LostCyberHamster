using UnityEngine;

namespace GameManagement
{
    public static class PlayerProgressCommitter
    {
        public static void Commit(CheckpointReason reason)
        {
            GameDataManager.SaveData();
            Debug.Log($"[GameData] Commit: {reason}.");
        }
    }
}
