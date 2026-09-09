using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace LostCyberHamster.UI
{
    /// <summary>Ограниченная очередь одного неблокирующего показа; время чтения движется только из Tick.</summary>
    public sealed class NotificationCoordinator : IDisposable
    {
        private const int Capacity = 5;
        private const double ProvisionalLifetime = 10;
        private const double GameplayStartInterval = 12;
        private const double GameplayWindow = 60;
        private readonly NotificationView _view;
        private readonly List<PendingMessage> _pending = new(Capacity);
        private readonly Queue<double> _gameplayStarts = new(3);
        private readonly List<Rect> _excludedRects = new();
        private PendingMessage _active;
        private double _lastTick;
        private double _lastGameplayStart = double.NegativeInfinity;
        private bool _wasVisible;
        private bool _disposed;

        private sealed class PendingMessage
        {
            public NotificationMessage Message;
            public double EnqueuedAt;
            public double ReadableSeconds;
            public bool WasShown;
            public bool HasGameplayStart;
            public double RetryAfter;
        }

        public NotificationCoordinator(VisualElement root)
        {
            _view = new NotificationView(root);
            _lastTick = Time.realtimeSinceStartupAsDouble;
        }

        /// <summary>Добавляет уникальное актуальное сообщение; false оставляет повтор за его источником.</summary>
        public bool Enqueue(NotificationMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (_disposed || !IsValid(message)) return false;
            double now = Time.realtimeSinceStartupAsDouble;
            Purge(now);
            if (Matches(_active, message)) return false;
            foreach (var item in _pending)
                if (Matches(item, message)) return false;
            if (_pending.Count + (_active == null ? 0 : 1) >= Capacity) return false;
            _pending.Add(new PendingMessage { Message = message, EnqueuedAt = now });
            return true;
        }

        /// <summary>Копирует занятые HUD области в координатах panel; их обновляет владелец экрана.</summary>
        public void SetExcludedRects(IReadOnlyList<Rect> rects)
        {
            _excludedRects.Clear();
            if (rects == null) return;
            for (int i = 0; i < rects.Count; i++) _excludedRects.Add(rects[i]);
        }

        /// <summary>Обслуживает очередь по unscaled времени; blocked скрывает показ без подтверждения.</summary>
        public void Tick(bool gameplay, bool blocked)
        {
            if (_disposed) return;
            double now = Time.realtimeSinceStartupAsDouble;
            // Долгая остановка кадров/background не считается временем чтения.
            double delta = Math.Max(0, Math.Min(0.25, now - _lastTick));
            _lastTick = now;
            Purge(now);
            while (_gameplayStarts.Count > 0 && now - _gameplayStarts.Peek() >= GameplayWindow)
                _gameplayStarts.Dequeue();
            if (blocked)
            {
                Hide();
                return;
            }

            // Отложенное меню-сообщение не препятствует допустимым gameplay-подсказкам.
            if (_active != null && gameplay && !_active.Message.AllowedDuringGameplay)
            {
                _pending.Insert(0, _active);
                _active = null;
                Hide();
            }
            if (_active == null)
            {
                int selected = SelectNext(gameplay, now);
                if (selected < 0) return;
                _active = _pending[selected];
                _pending.RemoveAt(selected);
            }

            // Лимиты относятся к началу реального показа, а не к постановке в очередь.
            bool needsGameplayStart = gameplay && !_active.HasGameplayStart;
            if (needsGameplayStart && (now - _lastGameplayStart < GameplayStartInterval || _gameplayStarts.Count >= 3))
            {
                Hide();
                return;
            }
            var current = _active;
            if (!_view.TryShow(current.Message, _excludedRects))
            {
                // Неподходящая геометрия одного текста не удерживает очередь остальных уведомлений.
                current.RetryAfter = now + 1;
                _pending.Add(current);
                _active = null;
                Hide();
                return;
            }
            if (needsGameplayStart)
            {
                current.HasGameplayStart = true;
                _lastGameplayStart = now;
                _gameplayStarts.Enqueue(now);
            }
            if (_wasVisible) current.ReadableSeconds += delta;
            _wasVisible = true;
            if (!current.WasShown)
            {
                current.WasShown = true;
                Invoke(current.Message.OnShown);
            }

            // Сначала освобождаем слот, затем вызываем источник: callback может поставить новое сообщение.
            if (_disposed || !ReferenceEquals(_active, current)) return;
            if (current.ReadableSeconds < current.Message.DurationSeconds) return;
            _active = null;
            Hide();
            if (IsValid(current.Message)) Invoke(current.Message.OnAcknowledged);
        }

        private int SelectNext(bool gameplay, double now)
        {
            int selected = -1;
            for (int i = 0; i < _pending.Count; i++)
            {
                if (gameplay && !_pending[i].Message.AllowedDuringGameplay) continue;
                if (now < _pending[i].RetryAfter) continue;
                if (selected < 0 || _pending[i].Message.Priority > _pending[selected].Message.Priority)
                    selected = i;
            }
            return selected;
        }

        private void Purge(double now)
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
                if (IsExpired(_pending[i], now)) _pending.RemoveAt(i);
            if (_active == null || !IsExpired(_active, now)) return;
            _active = null;
            Hide();
        }

        private static bool IsExpired(PendingMessage item, double now) =>
            item.Message.IsProvisional && now - item.EnqueuedAt >= ProvisionalLifetime || !IsValid(item.Message);

        private static bool Matches(PendingMessage item, NotificationMessage message) =>
            item != null && item.Message.Key == message.Key && item.Message.ProfileId == message.ProfileId &&
            item.Message.AttemptId == message.AttemptId;

        private static bool IsValid(NotificationMessage message)
        {
            try { return message.IsValid?.Invoke() ?? true; }
            catch (Exception exception)
            {
                DebugManager.DiagStability($"[Notification] Validity callback failed: {exception.GetType().Name}.");
                return false;
            }
        }

        private static void Invoke(Action callback)
        {
            try { callback?.Invoke(); }
            catch (Exception exception)
            {
                DebugManager.DiagStability($"[Notification] Presentation callback failed: {exception.GetType().Name}.");
            }
        }

        private void Hide()
        {
            _view.Hide();
            _wasVisible = false;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _pending.Clear();
            _active = null;
            _view.Dispose();
        }
    }
}
