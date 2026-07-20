using System;

namespace GameManagement.Progress
{
    public interface IUnlockPolicy
    {
        bool CanUnlockNextLevel(LevelProgressSnapshot snapshot, LevelProgressKey currentLevel, LevelProgressKey nextLevel);

        bool CanUnlockNextLocation(LevelProgressSnapshot snapshot, string currentLocationId, string nextLocationId);

        int GetRequiredStarsForNextLocation(LevelProgressSnapshot snapshot, string currentLocationId);
    }
}
