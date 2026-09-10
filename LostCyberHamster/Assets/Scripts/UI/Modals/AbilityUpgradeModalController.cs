using System;
using System.Threading;
using System.Threading.Tasks;
using Assets.Scripts.System.Resources;
using GameManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Vues.GameCore;

namespace LostCyberHamster.UI
{
    /// <summary>Сравнивает уровни способности и улучшает её после подтверждения игроком.</summary>
    public sealed class AbilityUpgradeModalController : ModalController
    {
        private readonly Action<bool> _close;
        private int _abilityId;
        private int _level;
        private string _profile;
        private long _generation;
        private bool _busy;
        private AddressableLease<Sprite> _icon;
        private CancellationTokenSource _iconLoading;
        private GameResultModalPresentation _presentation;
        private IVisualElementScheduledItem _refresh;
        protected override ScreenEnum _modalAssetName => ScreenEnum.AbilityUpgradeModal;
        private Button Upgrade => _modalContent.Q<Button>("ability-upgrade-confirm");
        private Button CloseButton => _modalContent.Q<Button>("ability-upgrade-close");
        private bool IsCurrent => _profile == GameDataManager.ProfileId && _generation == GameDataManager.Generation;

        public AbilityUpgradeModalController(UIDocument document, Action<bool> close) : base(document) => _close = close;

        public void SetAbility(int abilityId)
        {
            _abilityId = abilityId;
            _level = SuperAttackLevelResolver.GetLevel(GameDataManager.PlayerData, abilityId);
            _profile = GameDataManager.ProfileId;
            _generation = GameDataManager.Generation;
        }

        protected override Task OnShowAsync()
        {
            // Сравниваем закреплённый уровень; повторное нажатие старого окна отклонит сервис.
            _buttonCloseModal.style.display = DisplayStyle.None;
            _busy = false;
            if (!SuperAttackService.TryGet(_abilityId, out var ability)) return Task.CompletedTask;
            bool maximum = _level >= SuperAttackLevelResolver.MaximumLevel;
            _modalContent.Q("ability-upgrade").EnableInClassList("ability-upgrade--maximum", maximum);
            int next = maximum ? _level : _level + 1;
            Label("ability-upgrade-title").text = Text(ability.NameLocalizationKey).ToUpperInvariant();
            Label("ability-current-tier").text = SuperAttackDescriptionFormatter.Roman(_level);
            Label("ability-next-tier").text = SuperAttackDescriptionFormatter.Roman(next);
            Label("ability-current-heading").text = Text("retention_now");
            Label("ability-next-heading").text = Text(maximum ? "progression_maximum" : "retention_after");
            Label("ability-current-description").text = SuperAttackDescriptionFormatter.Primary(ability, _level);
            Label("ability-next-description").text = SuperAttackDescriptionFormatter.Primary(ability, next);
            Label("ability-upgrade-bonus").text = SuperAttackDescriptionFormatter.Bonus(ability, next);
            Label("ability-upgrade-cost").text = Text("retention_cost");
            _modalContent.Q("ability-upgrade-price").style.display = maximum ? DisplayStyle.None : DisplayStyle.Flex;
            _modalContent.Q("ability-upgrade-tiers").style.display = maximum ? DisplayStyle.None : DisplayStyle.Flex;
            _modalContent.Q("ability-current-panel").style.display = maximum ? DisplayStyle.None : DisplayStyle.Flex;
            Upgrade.EnableInClassList("ability-upgrade-confirm--maximum", maximum);
            Upgrade.text = Text(maximum ? "progression_maximum" : "retention_upgrade_title");
            Refresh();

            return Task.CompletedTask;
        }

        protected override void OnSubscribeToEvents()
        {
            Upgrade.clicked += Confirm;
            CloseButton.clicked += Close;
            GameDataManager.ProfileChanged += Close;
            _refresh = _modalContent.schedule.Execute(Refresh).Every(500);
            _iconLoading = new CancellationTokenSource();
            LoadIcon(_iconLoading.Token);
            _presentation ??= GameResultModalPresentation.Apply(_root,
                _modalContent.Q("ability-upgrade-viewport"), _modalContent.Q("ability-upgrade-frame"),
                _modalContent.Q("ability-upgrade-design"), new Vector2(1844, 853), ModalScaleMode.Contain, true);
        }

        protected override void OnUnsubscribeFromEvents()
        {
            // Останавливаем callbacks до освобождения общего modal host и иконки.
            Upgrade.clicked -= Confirm;
            CloseButton.clicked -= Close;
            GameDataManager.ProfileChanged -= Close;
            _refresh?.Pause(); _refresh = null;
            _iconLoading?.Cancel(); _iconLoading?.Dispose(); _iconLoading = null;
            var image = _modalContent.Q("ability-upgrade-icon");
            if (image != null) image.style.backgroundImage = StyleKeyword.None;
            _icon?.Dispose(); _icon = null;
            _presentation?.Restore(); _presentation = null;
        }

        private void Refresh()
        {
            if (!IsCurrent) { Upgrade.SetEnabled(false); return; }
            bool maximum = _level >= SuperAttackLevelResolver.MaximumLevel;
            Upgrade.SetEnabled(!_busy && CharacterDevelopmentService.CanUpgradeSuperAttack(_abilityId, _level));
            Label("ability-upgrade-owned").text = Text("retention_owned_points", GameDataManager.PlayerData.DevelopmentPoints);
            string reason = maximum ? Text("progression_maximum") :
                GameDataManager.PlayerData.PlayerLevel < SuperAttackLevelResolver.UpgradePlayerLevel
                    ? Text("progression_upgrade_level", SuperAttackLevelResolver.UpgradePlayerLevel) :
                GameDataManager.PlayerData.DevelopmentPoints < 1 ? Text("progression_upgrade_points") : string.Empty;
            Label("ability-upgrade-status").text = reason;
        }

        private async void LoadIcon(CancellationToken cancellationToken)
        {
            if (!SuperAttackService.TryGet(_abilityId, out var ability)) return;
            AddressableLease<Sprite> lease = null;
            try
            {
                // Повторный OnEnable восстанавливает ресурс, отменённый показ не меняет новое дерево.
                lease = await AddressableLoader.LoadAssetAsync<Sprite>(ability.IconAddress, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                _icon = lease;
                _modalContent.Q("ability-upgrade-icon").style.backgroundImage = new StyleBackground(lease.Value);
            }
            catch (OperationCanceledException) { lease?.Dispose(); }
            catch (Exception exception) { lease?.Dispose(); Debug.LogException(exception); }
        }

        private void Confirm()
        {
            if (_busy || !IsCurrent) return;
            _busy = true;
            Upgrade.SetEnabled(false);
            try
            {
                // Сервис атомарно проверяет цену, профиль и ожидаемый уровень, затем сохраняет покупку.
                if (CharacterDevelopmentService.TryUpgradeSuperAttack(_abilityId, _level))
                {
                    _close(true);
                    return;
                }
                _busy = false;
                Refresh();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _busy = false;
                Refresh();
                Label("ability-upgrade-status").text = Text("retention_upgrade_failed");
            }
        }

        private void Close() { if (!_busy) _close(false); }
        private Label Label(string name) => _modalContent.Q<Label>(name);
        private static string Text(string key, params object[] values) => string.Format(LocalizationManager.GetLocalizedString(key), values);
    }
}
