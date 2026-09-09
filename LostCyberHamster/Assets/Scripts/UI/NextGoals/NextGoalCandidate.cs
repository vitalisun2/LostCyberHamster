using GameManagement;
using UnityEngine;

namespace LostCyberHamster.UI
{
    /// <summary>Неизменяемая рекомендация с адресом действия и владельцем предъявленного состояния.</summary>
    public sealed class NextGoalCandidate
    {
        public NextGoalKind Kind { get; }
        public NextGoalAction Action { get; }
        public string Target { get; }
        public string Id => $"{Kind}:{Action}:{Target}";
        public string Text { get; }
        public string Detail { get; }
        public string ActionText { get; }
        public ScreenEnum Destination { get; }
        public string ActivityKind { get; }
        public Sprite Icon { get; }
        public float? Progress { get; }
        public bool StartsShieldLesson { get; }
        public bool IsOnboarding => Action == NextGoalAction.Onboarding;
        public string ProfileId { get; } = GameDataManager.ProfileId;
        public long Generation { get; } = GameDataManager.Generation;
        public bool IsCurrentProfile => ProfileId == GameDataManager.ProfileId && Generation == GameDataManager.Generation;

        public NextGoalCandidate(NextGoalKind kind, NextGoalAction action, string target, string text,
            string detail, string actionText, ScreenEnum destination, float? progress = null,
            Sprite icon = null, string activityKind = null, bool startsShieldLesson = false)
        {
            Kind = kind; Action = action; Target = target; Text = text; Detail = detail;
            ActionText = actionText; Destination = destination; Progress = progress;
            Icon = icon; ActivityKind = activityKind; StartsShieldLesson = startsShieldLesson;
        }
    }
}
