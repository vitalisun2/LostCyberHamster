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

/// <summary>Версионная схема одного перехода первой сессии; durable ID передаётся в detail.</summary>
public sealed class FirstSessionEvent : Event
{
    public FirstSessionEvent(string phase, string detail, int value, string sessionId, int sequence,
        double elapsedSeconds, string profileId, string levelAddress, int playerLevel, string tutorialOutcome,
        string appVersion, string networkMode) : base("first_session")
    {
        SetParameter("fs_schema", 1);
        SetParameter("fs_phase", phase);
        SetParameter("fs_detail", detail);
        SetParameter("fs_value", value);
        SetParameter("fs_session", sessionId);
        SetParameter("fs_sequence", sequence);
        SetParameter("fs_elapsed_seconds", elapsedSeconds);
        SetParameter("fs_profile", profileId);
        SetParameter("fs_level_address", levelAddress);
        SetParameter("fs_player_level", playerLevel);
        SetParameter("fs_tutorial_outcome", tutorialOutcome);
        SetParameter("fs_app_version", appVersion);
        SetParameter("fs_cohort", "production");
        SetParameter("fs_network_mode", networkMode);
    }
}
