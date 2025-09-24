using Unity.Services.Analytics;
using System;

public class LevelStartEvent : Event
{
    public LevelStartEvent(string levelKey) : base("level_start")
    {
        SetParameter("level_key", levelKey);
    }
}

public class LevelCompleteEvent : Event
{
    public LevelCompleteEvent(string levelKey, int stars) : base("level_complete")
    {
        SetParameter("level_key", levelKey);
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