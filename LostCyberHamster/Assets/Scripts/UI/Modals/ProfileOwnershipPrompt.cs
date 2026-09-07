using System;
using System.Linq;
using GameManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Vues.GameCore;

namespace LostCyberHamster.UI
{
    /// <summary>Показывает явное принятие локального прогресса и предупреждение перед восстановлением аккаунта.</summary>
    public sealed class ProfileOwnershipPrompt : IDisposable
    {
        private readonly VisualElement _root;
        private readonly VisualElement _overlay;
        private readonly VisualElement _actions;
        private readonly Label _status;
        private readonly ScreenLayout _layout;
        private bool _disposed;

        /// <summary>Фиксирует показанные прогресс и гостя; возвращённое окно закрывается вместе с экраном.</summary>
        public static IDisposable Show(VisualElement root, Action openExistingAccount, Action completed = null)
        {
            var service = ProfileOwnershipService.Instance;
            if (service == null) throw new InvalidOperationException("Profile ownership service is unavailable.");
            var profile = GameDataManager.ProfileId;
            var generation = GameDataManager.Generation;
            var revision = GameDataManager.LocalRevision;
            var player = service.GuestPlayerId;
            var data = GameDataManager.PlayerData;
            var guestName = string.IsNullOrWhiteSpace(service.GuestName)
                ? Text("account_state_guest") : service.GuestName;
            var summary = string.Format(Text("profile_ownership_summary"), data.PlayerLevel,
                data.Progress.Entries.Count(entry => entry.IsCompleted), data.Money, data.Crystals);
            var prompt = new ProfileOwnershipPrompt(root, "profile_ownership_title",
                string.Format(Text("profile_ownership_body"), guestName), summary);

            // Явное подтверждение действует только для снимка, который игрок видел при открытии.
            prompt.AddButton("profile-ownership__use-local", "profile_ownership_use", () =>
            {
                if (GameDataManager.HasOwnerJournalConflict(player))
                {
                    prompt._status.text = Text("profile_ownership_conflict");
                    prompt._actions.Q<Button>("profile-ownership__use-local").SetEnabled(false);
                    return;
                }
                try
                {
                    if (!service.TryAdoptGuestProgress(profile, generation, player, revision))
                    {
                        prompt._status.text = Text("profile_ownership_changed");
                        return;
                    }
                    prompt.Dispose();
                    completed?.Invoke();
                }
                catch (Exception)
                {
                    prompt._status.text = Text("profile_ownership_retry");
                }
            }, primary: true);
            if (openExistingAccount != null)
                prompt.AddButton("profile-ownership__existing", "profile_ownership_existing", () =>
                {
                    prompt.Dispose();
                    openExistingAccount();
                });
            prompt.AddButton("profile-ownership__later", "btn_later", prompt.Dispose);
            return prompt;
        }

        /// <summary>Предупреждает о замене локального прогресса до запуска браузерного входа.</summary>
        public static IDisposable ShowExistingAccountConfirmation(VisualElement root, Action confirmed)
        {
            var prompt = new ProfileOwnershipPrompt(root, "profile_existing_title",
                Text("profile_existing_warning"), null);
            prompt.AddButton("profile-ownership__confirm-sign-in", "btn_sign_in", () =>
            {
                prompt.Dispose();
                confirmed?.Invoke();
            }, primary: true);
            prompt.AddButton("profile-ownership__later", "btn_later", prompt.Dispose);
            return prompt;
        }

        private ProfileOwnershipPrompt(VisualElement root, string titleKey, string body, string summary)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));

            // Повторно используем художественные классы и адаптивный масштаб существующего account prompt.
            _overlay = new VisualElement { name = "profile-ownership-prompt" };
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = 0;
            _overlay.style.right = 0;
            _overlay.style.top = 0;
            _overlay.style.bottom = 0;
            _overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.78f);
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.Center;
            var frame = new VisualElement();
            frame.style.position = Position.Relative;
            var design = new VisualElement();
            design.style.width = 1180;
            design.style.height = 850;
            design.style.position = Position.Absolute;
            design.style.transformOrigin = new TransformOrigin(0, 0);
            design.style.alignItems = Align.Center;
            design.style.justifyContent = Justify.Center;
            var panel = new VisualElement();
            panel.AddToClassList("account-prompt");
            panel.style.height = 768;
            panel.style.paddingTop = 84;
            panel.style.paddingBottom = 24;
            var title = new Label(Text(titleKey));
            title.AddToClassList("account-prompt__title");
            panel.Add(title);
            var bodyPanel = new VisualElement();
            bodyPanel.AddToClassList("account-prompt__body-panel");
            bodyPanel.style.height = 384;
            bodyPanel.style.paddingTop = 16;
            bodyPanel.style.paddingBottom = 16;
            // Инструкция и ошибка занимают одну зону; отдельная сводка сохраняет место под ней.
            _status = new Label(body) { name = "profile-ownership__status" };
            _status.AddToClassList("account-prompt__body");
            _status.style.fontSize = 29;
            _status.style.minHeight = 0;
            _status.style.flexShrink = 0;
            bodyPanel.Add(_status);
            if (!string.IsNullOrEmpty(summary))
            {
                var details = new Label(summary);
                details.AddToClassList("account-prompt__body");
                details.style.fontSize = 25;
                details.style.marginTop = 18;
                details.style.minHeight = 0;
                bodyPanel.Add(details);
            }
            panel.Add(bodyPanel);
            _actions = new VisualElement();
            _actions.AddToClassList("account-prompt__actions");
            _actions.style.flexDirection = FlexDirection.Column;
            _actions.style.width = 860;
            _actions.style.height = 260;
            _actions.style.marginTop = 10;
            panel.Add(_actions);
            design.Add(panel);
            frame.Add(design);
            _overlay.Add(frame);
            _root.Add(_overlay);
            _root.RegisterCallback<DetachFromPanelEvent>(OnRootDetached);
            _layout = ScreenLayout.Fit(_overlay, frame, design, new Vector2(1180, 850));
        }

        private void AddButton(string name, string key, Action action, bool primary = false)
        {
            var button = new Button(() => { if (!_disposed) action(); }) { name = name, text = Text(key) };
            button.AddToClassList("lcs_btn");
            button.AddToClassList("account-prompt__button");
            button.AddToClassList(primary ? "account-prompt__button--primary" : "account-prompt__button--secondary");
            button.style.width = 850;
            button.style.minWidth = 0;
            button.style.height = 78;
            button.style.minHeight = 78;
            button.style.fontSize = 27;
            _actions.Add(button);
            if (primary) button.schedule.Execute(button.Focus);
        }

        private static string Text(string key) => LocalizationManager.GetLocalizedString(key);

        private void OnRootDetached(DetachFromPanelEvent evt)
        {
            if (evt.target == _root) Dispose();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _root.UnregisterCallback<DetachFromPanelEvent>(OnRootDetached);
            _layout.Dispose();
            _overlay.RemoveFromHierarchy();
        }
    }
}
