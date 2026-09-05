using UnityEngine;

/// <summary>Сохраняет совместимость старых сцен; игра использует локальный контент.</summary>
public sealed class LicenseManager : MonoBehaviour
{
    private void Awake() => enabled = false;

    /// <summary>Возвращает системную подсказку о подключении без проверки доступности сервисов.</summary>
    public static bool IsNetworkAvailable() =>
        Application.internetReachability != NetworkReachability.NotReachable;
}
