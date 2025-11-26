using System;
using System.IO;
using Unity.VisualScripting;
using UnityEngine;
using Vues.GameCore;

public static class DebugManager
{
    private const string DiagLogFileName = "diagnostic_log.txt";
    private static string _diagLogPath;
    private static bool _fileLoggingEnabled = true;

    static DebugManager()
    {
        // Initialize diagnostic log file path
#if UNITY_EDITOR
        // In Editor: save to project root/EditorLogs/
        _diagLogPath = Path.Combine(Application.dataPath, "../EditorLogs", DiagLogFileName);
#else
        // In Build: save to persistent data path
        _diagLogPath = Path.Combine(Application.persistentDataPath, DiagLogFileName);
#endif
        
        // Ensure directory exists
        var directory = Path.GetDirectoryName(_diagLogPath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        // Clear old log on startup
        if (File.Exists(_diagLogPath))
        {
            File.Delete(_diagLogPath);
        }
        
        WriteDiagLogToFile($"=== Diagnostic Log Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
    }

    public static void Log(string message)
    {
        Debug.Log(message);
    }

    /// <summary>
    /// Diagnostic log that writes both to Unity console and to file.
    /// Use this for debugging issues that require examining logs after the fact.
    /// </summary>
    public static void DiagLog(string message)
    {
        var formattedMessage = $"[DIAG] {message}";
        Debug.Log(formattedMessage);
        WriteDiagLogToFile($"[{DateTime.Now:HH:mm:ss.fff}] {formattedMessage}");
    }

    /// <summary>
    /// Get the full path to the diagnostic log file.
    /// Use this to read the log programmatically or show path to user.
    /// </summary>
    public static string GetDiagLogPath() => _diagLogPath;

    /// <summary>
    /// Read the entire diagnostic log file content.
    /// </summary>
    public static string ReadDiagLog()
    {
        if (!File.Exists(_diagLogPath))
        {
            return "Diagnostic log file not found.";
        }
        
        try
        {
            return File.ReadAllText(_diagLogPath);
        }
        catch (Exception ex)
        {
            return $"Failed to read diagnostic log: {ex.Message}";
        }
    }

    /// <summary>
    /// Clear the diagnostic log file.
    /// </summary>
    public static void ClearDiagLog()
    {
        if (File.Exists(_diagLogPath))
        {
            File.Delete(_diagLogPath);
        }
        WriteDiagLogToFile($"=== Log Cleared at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
    }

    private static void WriteDiagLogToFile(string message)
    {
        if (!_fileLoggingEnabled) return;
        
        try
        {
            File.AppendAllText(_diagLogPath, message + Environment.NewLine);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DebugManager] Failed to write to diagnostic log: {ex.Message}");
            _fileLoggingEnabled = false; // Disable if writing fails
        }
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
