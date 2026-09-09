using System;

namespace LostCyberHamster.UI
{
    /// <summary>Готовое локализованное сообщение; проверка факта и подтверждение принадлежат источнику.</summary>
    public sealed class NotificationMessage
    {
        public string Key { get; }
        public string ProfileId { get; }
        public string AttemptId { get; }
        public string Title { get; }
        public string Detail { get; }
        public int Priority { get; }
        public float DurationSeconds { get; }
        public bool AllowedDuringGameplay { get; }
        public bool IsProvisional { get; }
        /// <summary>Чистая проверка актуальности, включая профиль/попытку; вызывается повторно без изменения очереди.</summary>
        public Func<bool> IsValid { get; }
        public Action OnShown { get; }
        public Action OnAcknowledged { get; }

        public NotificationMessage(string key, string title, string detail = null,
            string profileId = null, string attemptId = null, int priority = 0,
            float durationSeconds = 4f, bool allowedDuringGameplay = false,
            bool isProvisional = false, Func<bool> isValid = null,
            Action onShown = null, Action onAcknowledged = null)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Notification key is required.", nameof(key));
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Notification title is required.", nameof(title));
            if (float.IsNaN(durationSeconds) || float.IsInfinity(durationSeconds) || durationSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(durationSeconds));
            Key = key;
            Title = title;
            Detail = detail ?? string.Empty;
            ProfileId = profileId;
            AttemptId = attemptId;
            Priority = priority;
            DurationSeconds = durationSeconds;
            AllowedDuringGameplay = allowedDuringGameplay;
            IsProvisional = isProvisional;
            IsValid = isValid;
            OnShown = onShown;
            OnAcknowledged = onAcknowledged;
        }
    }
}
