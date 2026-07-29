#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;
using Vues.GameCore;
using Vues.GameCore.Quests;

namespace Assets.Scripts.DevTools.QuestTesting
{
    /// <summary>
    /// Проводит единственный MVP-квест через реальный QuestManager.
    /// </summary>
    public sealed class QuestTestRunner
    {
        private bool _isBusy;
        private string _beforeState = "—";
        private string _afterState = "Ожидание QuestManager.Init.";
        private string _status =
            "Generate/Reset начнёт новый прогон активного MVP-квеста.";

        private QuestTestRunner()
        {
        }

        public static QuestTestRunner Shared { get; } = new();

        public event Action Changed;

        public bool IsBusy => _isBusy;

        public bool IsReady =>
            Application.isPlaying &&
            Definition != null &&
            State != null &&
            QuestView != null;

        public string Title => Definition == null
            ? "MVP-квест не загружен"
            : $"{Definition.Title} ({Definition.Id})";

        public string Kind => Definition == null
            ? "—"
            : $"{Definition.Type} / {Definition.ActionId}";

        public string BeforeState => _beforeState;

        public string AfterState => _afterState;

        public string Status => _status;

        public bool CanGenerateOrReset => IsReady && !_isBusy;

        public bool CanAdvance =>
            IsReady &&
            !_isBusy &&
            Definition.TargetAmount > 1 &&
            State.CurrentProgress == 0 &&
            !State.IsCompleted;

        public bool CanComplete =>
            IsReady &&
            !_isBusy &&
            !State.IsCompleted;

        public bool CanClaimReward =>
            IsReady &&
            !_isBusy &&
            State.IsCompleted &&
            !QuestView.IsRewardRecieved;

        private static QuestDefinition Definition =>
            QuestManager.MvpQuestDefinitionForTesting;

        private static QuestState State =>
            QuestManager.MvpQuestStateForTesting;

        private static Quest QuestView =>
            QuestManager.MvpQuestViewForTesting;

        /// <summary>
        /// Сбрасывает реальное сохранённое состояние активного MVP-квеста.
        /// </summary>
        public void GenerateOrReset()
        {
            RunAction(
                "Generate/Reset",
                () =>
                {
                    if (!QuestManager.ResetMvpQuestForTesting())
                    {
                        throw new InvalidOperationException(
                            "QuestManager не инициализирован.");
                    }
                });
        }

        /// <summary>
        /// Публикует реальное действие до частичного прогресса.
        /// </summary>
        public void Advance()
        {
            if (!CanAdvance)
            {
                return;
            }

            int partialProgress = Math.Min(
                Math.Max(1, Definition.TargetAmount / 2),
                Definition.TargetAmount - 1);
            PublishProgress(partialProgress);
        }

        /// <summary>
        /// Публикует точный остаток реальных действий до завершения.
        /// </summary>
        public void Complete()
        {
            if (!CanComplete)
            {
                return;
            }

            int remainingProgress =
                Definition.TargetAmount - State.CurrentProgress;
            RunAction(
                "Complete",
                () => PublishActionQuestEvent(remainingProgress));
        }

        /// <summary>
        /// Выдаёт награду через основной QuestManager.
        /// </summary>
        public void ClaimReward()
        {
            if (!CanClaimReward)
            {
                return;
            }

            RunAction(
                "Claim Reward",
                () =>
                {
                    if (!QuestManager.GetReward(QuestView))
                    {
                        throw new InvalidOperationException(
                            "QuestManager отклонил получение награды.");
                    }
                });
        }

        /// <summary>
        /// Очищает отображаемое состояние после остановки Play Mode.
        /// </summary>
        public void HandlePlayModeStopped()
        {
            ResetTransientState(
                "Play Mode остановлен.",
                "QuestManager недоступен.");
        }

        /// <summary>
        /// Готовит страницу к загрузке QuestManager после запуска Play Mode.
        /// </summary>
        public void HandlePlayModeStarted()
        {
            ResetTransientState(
                "Play Mode запущен. Ожидание QuestManager.Init.",
                "Ожидание QuestManager.Init.");
        }

        private void PublishProgress(int progress)
        {
            RunAction(
                "Advance",
                () => PublishActionQuestEvent(progress));
        }

        private static void PublishActionQuestEvent(int progress)
        {
            GameEventsManager.PublishActionQuestEvent(
                new ActionQuestEvent(
                    Definition.ActionId,
                    progress));
        }

        private void RunAction(string actionName, Action action)
        {
            if (!IsReady || _isBusy)
            {
                return;
            }

            _isBusy = true;
            _beforeState = FormatState();

            try
            {
                action();
                _afterState = FormatState();
                _status =
                    $"{actionName}: {_beforeState} → {_afterState}.";
            }
            catch (Exception exception)
            {
                _status = $"Ошибка {actionName}: {exception.Message}";
                Debug.LogError($"[Quest Testing] {_status}");
            }
            finally
            {
                _isBusy = false;
                Changed?.Invoke();
            }
        }

        private void ResetTransientState(
            string status,
            string afterState)
        {
            _isBusy = false;
            _beforeState = "—";
            _afterState = afterState;
            _status = status;
            Changed?.Invoke();
        }

        private static string FormatState()
        {
            if (Definition == null || State == null || QuestView == null)
            {
                return "QuestManager не инициализирован";
            }

            string progress =
                $"{State.CurrentProgress}/{Definition.TargetAmount}";
            if (QuestView.IsRewardRecieved)
            {
                return $"Награда получена, {progress}";
            }

            if (State.IsCompleted)
            {
                return $"Выполнен, {progress}";
            }

            if (State.CurrentProgress > 0)
            {
                return $"Частично выполнен, {progress}";
            }

            return $"Сгенерирован, {progress}";
        }
    }
}
#endif
