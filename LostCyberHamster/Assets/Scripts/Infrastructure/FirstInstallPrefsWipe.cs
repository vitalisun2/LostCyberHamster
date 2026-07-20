using GameManagement;
using UnityEngine;

// Handles wiping PlayerPrefs on first Android install so stale values do not survive reinstalls.
// TODO: Remove once a dedicated install-state manager replaces this bootstrap helper.
public static class FirstInstallPrefsWipe
{
    private const string Key = "INSTALL_TIME_ANDROID";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void WipeOnFreshInstall()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        long current = GetFirstInstallTime();
        long saved = 0;
        long.TryParse(PlayerPrefs.GetString(Key, "0"), out saved);

        if (saved != current)
        {
            GameDataManager.ResetPlayerProgress();
            GameDataManager.ResetSettings();
            PlayerPrefs.SetString(Key, current.ToString());
            PlayerPrefs.Save();
        }
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static long GetFirstInstallTime()
    {
        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        using (var pm = activity.Call<AndroidJavaObject>("getPackageManager"))
        {
            string pkg = activity.Call<string>("getPackageName");
            var pkgInfo = pm.Call<AndroidJavaObject>("getPackageInfo", pkg, 0);
            return pkgInfo.Get<long>("firstInstallTime");
        }
    }
#endif
}
