using System;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;
using Vues.GameCore;

public static class DebugManager
{
    private const string DiagLogFileName = "diagnostic_log.txt";
    private const long MaxPersistentDiagLogBytes = 2 * 1024 * 1024;
    private static readonly object _diagLogFileSync = new object();
    private static string _diagLogPath;
    private static bool _fileLoggingEnabled = true;
    private static bool _verboseDiagLoggingEnabled = false;

    public enum DiagChannel
    {
        BotEvents,
        Economy,
        Stability
    }

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
        
        TrimExistingLogIfNeeded(_diagLogPath, MaxPersistentDiagLogBytes);
        WriteDiagLogToFile($"=== Diagnostic Log Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
    }

    public static void Log(string message)
    {
        return;
    }

    /// <summary>
    /// Diagnostic log that writes important events to the diagnostic file.
    /// </summary>
    public static void DiagLog(string message)
    {
        DiagLog(message, DiagChannel.BotEvents);
    }

    /// <summary>
    /// Diagnostic log that writes important events to the diagnostic file.
    /// </summary>
    public static void DiagLog(string message, DiagChannel channel)
    {
        WriteDiagLog(message, channel, forceWrite: true);
    }

    /// <summary>
    /// Diagnostic log that writes verbose events only when verbose diagnostics are enabled.
    /// </summary>
    public static void DiagLogVerbose(string message)
    {
        DiagLogVerbose(message, DiagChannel.BotEvents);
    }

    /// <summary>
    /// Diagnostic log that writes verbose events only when verbose diagnostics are enabled.
    /// </summary>
    public static void DiagLogVerbose(string message, DiagChannel channel)
    {
        if (!_verboseDiagLoggingEnabled)
            return;

        WriteDiagLog(message, channel, forceWrite: true);
    }

    /// <summary>
    /// Enables or disables verbose diagnostic file logging.
    /// </summary>
    public static void SetVerboseDiagLoggingEnabled(bool enabled)
    {
        _verboseDiagLoggingEnabled = enabled;
    }

    private static void WriteDiagLog(string message, DiagChannel channel, bool forceWrite)
    {
        string channelTag = GetChannelTag(channel);
        var formattedMessage = $"[DIAG][CH={channelTag}] {message}";
        if (forceWrite)
            WriteDiagLogToFile($"[{DateTime.Now:HH:mm:ss.fff}] {formattedMessage}");
    }

    public static void DiagEconomy(string message) => DiagLog(message, DiagChannel.Economy);

    public static void DiagStability(string message) => DiagLog(message, DiagChannel.Stability);

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
        lock (_diagLogFileSync)
        {
            if (File.Exists(_diagLogPath))
            {
                File.Delete(_diagLogPath);
            }

            WriteDiagLogToFile($"=== Log Cleared at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
        }
    }

    private static string GetChannelTag(DiagChannel channel)
    {
        switch (channel)
        {
            case DiagChannel.Economy:
                return "ECO";
            case DiagChannel.Stability:
                return "STAB";
            default:
                return "BOT";
        }
    }

    private static void WriteDiagLogToFile(string message)
    {
        if (!_fileLoggingEnabled) return;

        lock (_diagLogFileSync)
        {
            var line = message + Environment.NewLine;
            for (var attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    using var stream = new FileStream(
                        _diagLogPath,
                        FileMode.Append,
                        FileAccess.Write,
                        FileShare.ReadWrite);
                    var bytes = Encoding.UTF8.GetBytes(line);
                    stream.Write(bytes, 0, bytes.Length);
                    return;
                }
                catch (IOException) when (attempt < 2)
                {
                    Thread.Sleep(5 * (attempt + 1));
                }
                catch (UnauthorizedAccessException) when (attempt < 2)
                {
                    Thread.Sleep(5 * (attempt + 1));
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[DebugManager] Failed to write to diagnostic log: {ex.Message}");
                    return;
                }
            }
        }
    }

    private static void TrimExistingLogIfNeeded(string path, long maxBytes)
    {
        if (!File.Exists(path)) return;

        lock (_diagLogFileSync)
        {
            try
            {
                var fileInfo = new FileInfo(path);
                if (fileInfo.Length <= maxBytes)
                {
                    return;
                }

                var keepBytes = (int)Math.Min(maxBytes / 2, fileInfo.Length);
                var allBytes = File.ReadAllBytes(path);
                var tail = new byte[keepBytes];
                Buffer.BlockCopy(allBytes, allBytes.Length - keepBytes, tail, 0, keepBytes);
                File.WriteAllBytes(path, tail);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DebugManager] Failed to trim diagnostic log: {ex.Message}");
            }
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
