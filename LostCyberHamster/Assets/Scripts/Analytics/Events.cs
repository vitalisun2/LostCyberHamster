using Unity.Services.Analytics;
using System;

public class LevelStartEvent : Event
{
    public LevelStartEvent(int levelNumber) : base("level_start")
    {
        SetParameter("level_number", levelNumber);
    }
}

public class LevelCompleteEvent : Event
{
    public LevelCompleteEvent(int levelNumber, int stars) : base("level_complete")
    {
        SetParameter("level_number", levelNumber);
        SetParameter("stars_number", stars);
    }
}

public class SkinPurchasedEvent : Event
{
    public SkinPurchasedEvent(string skinName) : base("skin_purchased")
    {
        SetParameter("skin_name", skinName);
    }
}

public class FeatureFlagChangedEvent : Event
{
    public FeatureFlagChangedEvent(string flagName, bool enabled) : base("feature_flag_change")
    {
        SetParameter("flag_name", flagName);
        SetParameter("flag_enabled", enabled);
    }
}
