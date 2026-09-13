#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Account;
using Assets.Scripts.DevTools.ExperienceProgressTesting;
using Assets.Scripts.DevTools.GameProgressTesting;
using Assets.Scripts.DevTools.Gameplay;
using Assets.Scripts.DevTools.Networking;
using Assets.Scripts.DevTools.QuestTesting;
using Assets.Scripts.DevTools.ReturnActivityTesting;
using Assets.Scripts.DevTools.SkateboardTesting;
using Assets.Scripts.DevTools.SkinTesting;
using Assets.Scripts.Online;
using GameManagement;
using LostCyberHamster.UI;
using UnityEngine;
using UnityEngine.UIElements;
using Vues.GameCore;
using Vues.GameCore.Quests;

namespace Assets.Scripts.DevTools.UiToolkit
{
    public static class DevToolsUiToolkitPageIds
    {
        public const string Root = "root";
        public const string Account = "account";
        public const string CloudSave = "cloud-save";
        public const string Gameplay = "gameplay";
        public const string GameProgress = "game-progress";
        public const string ExperienceProgress = "experience-progress";
        public const string Quests = "quests";
        public const string Skateboard = "skateboard";
        public const string Skin = "skin";
        public const string Resources = "resources";
        public const string Networking = "networking";
    }

    public readonly struct DevToolsNavigationLink
    {
        public DevToolsNavigationLink(string pageId, string title, string description, Color? cardColor = null)
        {
            PageId = pageId;
            Title = title;
            Description = description;
            CardColor = cardColor;
        }

        public string PageId { get; }
        public string Title { get; }
        public string Description { get; }
        public Color? CardColor { get; }
    }

    public abstract class DevToolsUiPageBase : IDevToolsUiPage
    {
        protected readonly DevToolsUiToolkitFactory Factory;

        protected DevToolsUiPageBase(DevToolsUiToolkitFactory factory, string title)
        {
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
            Title = title;
            Root = new VisualElement();
            Root.style.flexDirection = FlexDirection.Column;
        }

        public string Title { get; }
        public VisualElement Root { get; }

        public virtual void OnShown()
        {
        }

        public virtual void OnHidden()
        {
        }

        public abstract void Refresh();

        public virtual void Dispose()
        {
        }

        protected VisualElement CreateValueCard(string title, out Label valueLabel)
        {
            VisualElement card = Factory.CreateCard(title, background: DevToolsUiToolkitTheme.Summary);
            valueLabel = Factory.CreateStatus(string.Empty);
            card.Add(valueLabel);
            return card;
        }

        protected VisualElement CreateSummaryCard(string title, string description = null)
        {
            return Factory.CreateCard(title, description, DevToolsUiToolkitTheme.Summary);
        }

        protected VisualElement CreateSectionCard(string title, string description = null, Color? background = null)
        {
            return Factory.CreateCard(title, description, background ?? DevToolsUiToolkitTheme.Surface);
        }

        protected Label AddInfoRow(VisualElement parent, string label, string initialValue = "")
        {
            VisualElement row = Factory.CreateRow(wrap: true);
            row.style.alignItems = Align.FlexStart;
            row.style.marginTop = 8f;

            Label keyLabel = Factory.CreateCaption(label);
            keyLabel.style.minWidth = 180f;
            keyLabel.style.marginRight = 16f;
            keyLabel.style.color = DevToolsUiToolkitTheme.TextMuted;
            row.Add(keyLabel);

            Label valueLabel = Factory.CreateStatus(initialValue);
            valueLabel.style.flexGrow = 1f;
            valueLabel.style.unityTextAlign = TextAnchor.UpperLeft;
            row.Add(valueLabel);

            parent.Add(row);
            return valueLabel;
        }

        protected Button AddActionRow(
            VisualElement parent,
            string title,
            string description,
            string actionLabel,
            Action action,
            Color buttonBackground)
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.alignItems = Align.FlexStart;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.paddingTop = 14f;
            row.style.paddingBottom = 14f;
            if (parent.childCount > 0 && parent[parent.childCount - 1] is not Label)
            {
                row.style.borderTopWidth = 1f;
                row.style.borderTopColor = DevToolsUiToolkitTheme.Border;
                row.style.marginTop = 8f;
                row.style.paddingTop = 18f;
            }

            VisualElement textColumn = new VisualElement();
            textColumn.style.flexDirection = FlexDirection.Column;
            textColumn.style.flexGrow = 1f;
            textColumn.style.minWidth = 320f;
            textColumn.style.marginRight = 18f;
            Label titleLabel = Factory.CreateTitle(title, pageTitle: false);
            titleLabel.style.fontSize = 24f;
            textColumn.Add(titleLabel);
            if (!string.IsNullOrWhiteSpace(description))
            {
                Label descriptionLabel = Factory.CreateCaption(description);
                descriptionLabel.style.marginTop = 6f;
                textColumn.Add(descriptionLabel);
            }

            Button button = Factory.CreateActionButton(actionLabel, buttonBackground, action, compact: true);
            button.style.width = DevToolsUiToolkitTheme.ActionButtonWidth;
            button.style.minWidth = 220f;
            button.style.marginTop = 4f;
            row.Add(textColumn);
            row.Add(button);
            parent.Add(row);
            return button;
        }

        protected static void SetToggleButtonVisual(Button button, bool active, bool enabled)
        {
            button.style.backgroundColor = active
                ? new Color(0.78f, 1f, 0.82f, 1f)
                : new Color(1f, 0.82f, 0.78f, 1f);
            button.SetEnabled(enabled);
        }
    }

    internal sealed class DevToolsCollapsibleSection
    {
        private readonly Button _toggleButton;
        private bool _expanded;

        public DevToolsCollapsibleSection(
            DevToolsUiToolkitFactory factory,
            string title,
            string description = null,
            bool expanded = false,
            Color? background = null)
        {
            Root = new VisualElement();
            DevToolsUiToolkitTheme.ApplyCard(Root, background ?? DevToolsUiToolkitTheme.Surface);

            VisualElement header = factory.CreateRow(wrap: true);
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;

            VisualElement textColumn = new VisualElement();
            textColumn.style.flexDirection = FlexDirection.Column;
            textColumn.style.flexGrow = 1f;
            textColumn.style.minWidth = 320f;
            Label titleLabel = factory.CreateTitle(title, pageTitle: false);
            titleLabel.style.fontSize = 28f;
            textColumn.Add(titleLabel);
            if (!string.IsNullOrWhiteSpace(description))
            {
                Label descriptionLabel = factory.CreateCaption(description);
                descriptionLabel.style.marginTop = 6f;
                textColumn.Add(descriptionLabel);
            }

            _toggleButton = factory.CreateActionButton(string.Empty, DevToolsUiToolkitTheme.SurfaceAccent, null, compact: true);
            _toggleButton.style.width = 180f;
            _toggleButton.clicked += Toggle;

            header.Add(textColumn);
            header.Add(_toggleButton);
            Root.Add(header);

            Content = new VisualElement();
            Content.style.flexDirection = FlexDirection.Column;
            Content.style.marginTop = 18f;
            Root.Add(Content);

            SetExpanded(expanded);
        }

        public VisualElement Root { get; }
        public VisualElement Content { get; }

        public void SetExpanded(bool expanded)
        {
            _expanded = expanded;
            Content.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            _toggleButton.text = expanded ? "Свернуть" : "Развернуть";
        }

        private void Toggle()
        {
            SetExpanded(!_expanded);
        }
    }

    public sealed class DevToolsNavigationPage : DevToolsUiPageBase
    {
        public DevToolsNavigationPage(
            DevToolsUiToolkitFactory factory,
            string title,
            string description,
            IReadOnlyList<DevToolsNavigationLink> links,
            Action<string> navigate)
            : base(factory, title)
        {
            if (!string.IsNullOrWhiteSpace(description))
            {
                Label intro = factory.CreateBody(description);
                intro.style.marginBottom = 8f;
                Root.Add(intro);
            }

            VisualElement grid = factory.CreateRow(wrap: true);
            grid.style.alignItems = Align.Stretch;

            foreach (DevToolsNavigationLink link in links)
            {
                grid.Add(CreateTile(link, navigate));
            }

            Root.Add(grid);
        }

        public override void Refresh()
        {
        }

        private VisualElement CreateTile(DevToolsNavigationLink link, Action<string> navigate)
        {
            VisualElement tile = new VisualElement();
            DevToolsUiToolkitTheme.ApplyCard(tile, link.CardColor ?? DevToolsUiToolkitTheme.Surface);
            tile.style.flexGrow = 1f;
            tile.style.minWidth = DevToolsUiToolkitTheme.DashboardTileMinWidth;
            tile.style.maxWidth = DevToolsUiToolkitTheme.DashboardTileMaxWidth;
            tile.style.minHeight = DevToolsUiToolkitTheme.DashboardTileMinHeight;
            tile.style.marginRight = 18f;
            tile.style.paddingBottom = 20f;
            tile.pickingMode = PickingMode.Position;
            tile.AddManipulator(new Clickable(() => navigate?.Invoke(link.PageId)));

            Label title = Factory.CreateTitle(link.Title, pageTitle: false);
            title.style.fontSize = 32f;
            tile.Add(title);

            Label description = Factory.CreateCaption(link.Description);
            description.style.marginTop = 8f;
            tile.Add(description);

            VisualElement spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            tile.Add(spacer);

            Label openLabel = Factory.CreateCaption("Открыть");
            openLabel.style.color = DevToolsUiToolkitTheme.TextStrong;
            openLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            openLabel.style.marginTop = 12f;
            tile.Add(openLabel);
            return tile;
        }
    }

    public sealed class GameplayDevToolsUiPage : DevToolsUiPageBase
    {
        private readonly GameplayDevToolsService _service = new();
        private readonly Label _statusLabel;
        private readonly Label _botStateLabel;
        private readonly Label _unlockStateLabel;
        private readonly Button _botButton;
        private readonly Button _unlockAllButton;
        private string _statusOverride = string.Empty;
        private bool _isBusy;

        public GameplayDevToolsUiPage(DevToolsUiToolkitFactory factory)
            : base(factory, "Игровые переключатели")
        {
            VisualElement summaryCard = CreateSummaryCard(
                "СВОДКА",
                "Быстрые runtime-переключатели для текущей игровой сессии.");
            _statusLabel = AddInfoRow(summaryCard, "Статус");
            _botStateLabel = AddInfoRow(summaryCard, "Бот");
            _unlockStateLabel = AddInfoRow(summaryCard, "Уровни");
            Root.Add(summaryCard);

            VisualElement actionsCard = CreateSectionCard(
                "КОМАНДЫ",
                "Меняют только текущую runtime-сессию и не требуют перехода в отдельный раздел.");
            _botButton = AddActionRow(
                actionsCard,
                "Бот",
                "Автопилот для текущей сцены и тестовых прогонов.",
                "Переключить",
                ToggleBot,
                DevToolsUiToolkitTheme.SurfaceDanger);
            _unlockAllButton = AddActionRow(
                actionsCard,
                "Временно открыть уровни",
                "Переключает локальный override открытия уровней.",
                "Переключить",
                ToggleUnlockAll,
                DevToolsUiToolkitTheme.SurfaceAccent);
            Root.Add(actionsCard);
        }

        public override void Refresh()
        {
            GameplayDevToolsSnapshot snapshot = _service.GetSnapshot();
            _botButton.text = snapshot.BotEnabled ? "Выключить" : "Включить";
            _unlockAllButton.text = snapshot.UnlockAllLevels ? "Отключить" : "Включить";
            SetToggleButtonVisual(_botButton, snapshot.BotEnabled, snapshot.BotAvailable && !_isBusy);
            SetToggleButtonVisual(_unlockAllButton, snapshot.UnlockAllLevels, !_isBusy);
            _statusLabel.text = _isBusy
                ? "Выполняется..."
                : !string.IsNullOrWhiteSpace(_statusOverride)
                    ? _statusOverride
                    : snapshot.BotAvailable ? "Готово." : "Бот недоступен на текущей сцене.";
            _botStateLabel.text = snapshot.BotEnabled ? "Включён" : "Выключен";
            _unlockStateLabel.text = snapshot.UnlockAllLevels ? "Override включён" : "Override выключен";
        }

        private void ToggleBot() => RunAction(_service.ToggleBot);
        private void ToggleUnlockAll() => RunAction(_service.ToggleUnlockAll);

        private void RunAction(Func<GameplayDevToolsActionResult> action)
        {
            if (_isBusy)
                return;

            _isBusy = true;
            Refresh();
            try
            {
                GameplayDevToolsActionResult result = action();
                _statusOverride = result.Message;
            }
            catch (Exception exception)
            {
                _statusOverride = $"Ошибка: {exception.Message}";
            }
            finally
            {
                _isBusy = false;
                Refresh();
            }
        }
    }

    public sealed class AccountDevToolsUiPage : DevToolsUiPageBase
    {
        private readonly Func<AccountService> _accountProvider;
        private readonly Button _localResetButton;
        private readonly Button _freshGuestButton;
        private readonly Button _unlinkButton;
        private readonly Label _stateLabel;
        private readonly Label _resultLabel;
        private bool _isBusy;
        private string _resultText = "Результат появится после действия.";

        public AccountDevToolsUiPage(DevToolsUiToolkitFactory factory, Func<AccountService> accountProvider)
            : base(factory, "Аккаунт")
        {
            _accountProvider = accountProvider ?? throw new ArgumentNullException(nameof(accountProvider));

            VisualElement summaryCard = CreateSummaryCard(
                "СОСТОЯНИЕ",
                "Короткая сводка по аккаунту и последнему действию.");
            _stateLabel = AddInfoRow(summaryCard, "Аккаунт");
            _resultLabel = AddInfoRow(summaryCard, "Результат");
            Root.Add(summaryCard);

            VisualElement sessionCard = CreateSectionCard(
                "СЕССИЯ",
                "Локальные операции без изменения облачной привязки.");
            _localResetButton = AddActionRow(
                sessionCard,
                "Очистить локальную сессию",
                "Сбрасывает Unity Authentication и Player Accounts только на этом устройстве.",
                "Очистить",
                ResetLocalState,
                DevToolsUiToolkitTheme.SurfaceAccent);
            Root.Add(sessionCard);

            VisualElement resetCard = CreateSectionCard(
                "СБРОС И ОТВЯЗКА",
                "Действия меняют текущий профиль или его привязку. Использовать осознанно.",
                DevToolsUiToolkitTheme.SurfaceDanger);
            _freshGuestButton = AddActionRow(
                resetCard,
                "Чистый старт",
                "Создаёт нового гостя с нулевым прогрессом. Настройки сохраняются.",
                "Создать гостя",
                StartFreshGuest,
                new Color(0.93f, 0.68f, 0.68f, 1f));
            _unlinkButton = AddActionRow(
                resetCard,
                "Отвязать аккаунт",
                "Удаляет серверную привязку и очищает локальную сессию прежнего аккаунта.",
                "Отвязать",
                UnlinkAccountAsync,
                new Color(0.88f, 0.63f, 0.63f, 1f));
            Root.Add(resetCard);
        }

        public override void Refresh()
        {
            AccountService account = _accountProvider();
            bool canMutate = account != null && !_isBusy && account.CanStartFreshGuestForTesting;
            bool canUnlink = canMutate && account.TryGetLinkedPlayerId(out _);

            _localResetButton.SetEnabled(canMutate);
            _freshGuestButton.SetEnabled(canMutate);
            _unlinkButton.SetEnabled(canUnlink);
            _stateLabel.text = account == null
                ? "Запустите Play Mode через Bootstrap/Menu."
                : $"Состояние аккаунта: {account.State}";
            _resultLabel.text = _resultText;
        }

        private void ResetLocalState()
        {
            AccountService account = _accountProvider();
            if (account == null || _isBusy)
                return;

            try
            {
                account.ResetLocalAccountStateForTesting();
                _resultText = "Локальная сессия очищена. Прогресс сохраняет прежнего владельца.";
            }
            catch (Exception exception)
            {
                _resultText = $"Ошибка: {exception.Message}";
            }

            Refresh();
        }

        private void StartFreshGuest()
        {
            AccountService account = _accountProvider();
            if (account == null || _isBusy)
                return;

            _isBusy = true;
            _resultText = "Создаём чистый профиль…";
            Refresh();

            try
            {
                account.StartFreshGuestForTesting();
                _resultText = "Новый прогресс готов. Гостевой аккаунт подключится при доступной сети.";
            }
            catch (Exception exception)
            {
                _resultText = $"Чистый старт не завершён. {exception.Message}";
            }
            finally
            {
                _isBusy = false;
                Refresh();
            }
        }

        private async void UnlinkAccountAsync()
        {
            AccountService account = _accountProvider();
            if (account == null || _isBusy)
                return;

            _isBusy = true;
            _resultText = "Выполняется отвязка…";
            Refresh();

            try
            {
                await account.FullResetTestAccountAsync();
                _resultText = "Привязка и локальная сессия очищены. Для новой игры используйте чистый старт.";
            }
            catch (OperationCanceledException)
            {
                _resultText = "Отвязка отменена.";
            }
            catch (Exception exception)
            {
                _resultText = $"Ошибка: {exception.Message}";
            }
            finally
            {
                _isBusy = false;
                Refresh();
            }
        }
    }

    public sealed class ResourcesDevToolsUiPage : DevToolsUiPageBase
    {
        private readonly IntegerField _amountField;
        private readonly Button _addMoneyButton;
        private readonly Label _balanceLabel;
        private readonly Label _statusLabel;
        private string _statusText = "Укажите Amount и нажмите Add Money.";

        public ResourcesDevToolsUiPage(DevToolsUiToolkitFactory factory)
            : base(factory, "Ресурсы")
        {
            VisualElement summaryCard = CreateSummaryCard(
                "СВОДКА",
                "Быстрое точное DEV-начисление Money без лишних переходов.");
            _balanceLabel = AddInfoRow(summaryCard, "Баланс");
            _statusLabel = AddInfoRow(summaryCard, "Статус");
            Root.Add(summaryCard);

            VisualElement moneyCard = CreateSectionCard(
                "НАЧИСЛЕНИЕ",
                "Введите amount и выполните точное DEV-начисление Money.");
            _amountField = factory.CreateIntegerField("Amount");
            _amountField.value = 100;
            _amountField.RegisterValueChangedCallback(_ =>
            {
                _statusText = "Укажите Amount и нажмите Add Money.";
                Refresh();
            });
            _amountField.style.flexGrow = 1f;
            _amountField.style.minWidth = 340f;
            VisualElement formRow = factory.CreateRow(wrap: true);
            formRow.style.alignItems = Align.Center;
            formRow.Add(_amountField);

            _addMoneyButton = factory.CreateActionButton("Начислить", DevToolsUiToolkitTheme.SurfaceAccent, AddMoney, compact: true);
            _addMoneyButton.style.width = 240f;
            _addMoneyButton.style.marginLeft = 16f;
            formRow.Add(_addMoneyButton);
            moneyCard.Add(formRow);
            Root.Add(moneyCard);
        }

        public override void Refresh()
        {
            bool isReady = Application.isPlaying && ResourceManager.IsReady;
            int balance = isReady ? ResourceManager.GetCurrentBalance(ResourceType.Coins) : 0;
            bool amountValid = _amountField.value > 0 && balance <= int.MaxValue - _amountField.value;
            _amountField.SetEnabled(isReady);
            _addMoneyButton.SetEnabled(isReady && amountValid);
            _balanceLabel.text = isReady ? balance.ToString() : "—";

            _statusLabel.text = !isReady
                ? "Resources доступны в Play Mode после загрузки PlayerData."
                : _amountField.value <= 0
                    ? "Amount должен быть больше 0."
                    : !amountValid
                        ? "Amount переполняет Money balance."
                        : _statusText;
        }

        private void AddMoney()
        {
            if (!Application.isPlaying || !int.TryParse(_amountField.text, out int amount))
                return;

            bool added = ResourceManager.TryAddMoneyForDevelopment(amount, out int newBalance);
            _statusText = added
                ? $"PASS: добавлено {amount} Money. Balance={newBalance}."
                : "FAIL: Money не добавлены.";
            Refresh();
        }
    }

    public sealed class NetworkingDevToolsUiPage : DevToolsUiPageBase
    {
        private readonly Func<GameNetworkFacade> _networkProvider;
        private readonly Button _toggleButton;
        private readonly Label _modeLabel;
        private readonly Label _statusLabel;
        private string _error;

        public NetworkingDevToolsUiPage(DevToolsUiToolkitFactory factory, Func<GameNetworkFacade> networkProvider)
            : base(factory, "Сеть")
        {
            _networkProvider = networkProvider ?? throw new ArgumentNullException(nameof(networkProvider));

            VisualElement summaryCard = CreateSummaryCard(
                "СВОДКА",
                "Режим сети сохраняется между перезапусками и совпадает с Tools/Testing.");
            _modeLabel = AddInfoRow(summaryCard, "Режим");
            _statusLabel = AddInfoRow(summaryCard, "Статус");
            Root.Add(summaryCard);

            VisualElement modeCard = CreateSectionCard(
                "FORCED OFFLINE",
                "Быстрый переключатель сетевого режима для текущего тестового сценария.");
            _toggleButton = AddActionRow(
                modeCard,
                "Сетевой режим",
                "Переключает доступность сетевых обращений для клиента.",
                "Переключить",
                Toggle,
                DevToolsUiToolkitTheme.SurfaceDanger);
            Root.Add(modeCard);
        }

        public override void Refresh()
        {
            GameNetworkFacade network = _networkProvider();
            bool offline = network != null && network.IsForcedOffline;
            _toggleButton.text = offline ? "Включить сеть" : "Отключить сеть";
            _toggleButton.style.backgroundColor = offline
                ? new Color(0.78f, 1f, 0.82f, 1f)
                : new Color(1f, 0.82f, 0.78f, 1f);
            _modeLabel.text = offline ? "Forced offline" : "Online";
            _statusLabel.text = _error ?? (offline
                ? "Симуляция офлайна включена. Режим сохраняется после перезапуска."
                : "Сетевые обращения разрешены. Режим сохраняется после перезапуска.");
        }

        private void Toggle()
        {
            GameNetworkFacade network = _networkProvider();
            if (network == null)
                return;

            try
            {
                network.SetForcedOffline(!network.IsForcedOffline);
                _error = null;
            }
            catch (Exception exception)
            {
                _error = $"Не удалось сохранить режим: {exception.Message}";
            }

            Refresh();
        }
    }

    public sealed class GameProgressTestingUiPage : DevToolsUiPageBase
    {
        private readonly GameProgressTestRunner _runner = GameProgressTestRunner.Shared;
        private readonly Action _closeBeforeWinAction;
        private readonly Button _prepareButton;
        private readonly Button _resetButton;
        private readonly Button _winLevelButton;
        private readonly Button _winPartButton;
        private readonly Button _winLocationButton;
        private readonly Label _currentLevelLabel;
        private readonly Label _statusLabel;
        private readonly Label _actionLabel;

        public GameProgressTestingUiPage(DevToolsUiToolkitFactory factory, Action closeBeforeWinAction = null)
            : base(factory, "Прогресс забега")
        {
            _closeBeforeWinAction = closeBeforeWinAction;
            VisualElement summaryCard = CreateSummaryCard(
                "СВОДКА",
                "Текущий target, статус и последнее действие production-flow сценария.");
            _currentLevelLabel = AddInfoRow(summaryCard, "Текущий уровень");
            _statusLabel = AddInfoRow(summaryCard, "Статус");
            _actionLabel = AddInfoRow(summaryCard, "Последнее действие");
            Root.Add(summaryCard);

            VisualElement actionsCard = CreateSectionCard(
                "КОМАНДЫ",
                "Короткие action rows вместо отдельных полноразмерных карточек на каждую команду.");
            _prepareButton = AddActionRow(
                actionsCard,
                "Подготовить Level Up",
                "Ставит XP на 239/240, чтобы следующая награда открыла Level Up modal.",
                "Подготовить",
                _runner.PrepareLevelUp,
                DevToolsUiToolkitTheme.Surface);
            _resetButton = AddActionRow(
                actionsCard,
                "Сбросить прогресс",
                "Очищает локальный прогресс игрока на этом устройстве.",
                "Сбросить",
                _runner.ResetProgress,
                DevToolsUiToolkitTheme.SurfaceDanger);
            _winLevelButton = AddActionRow(
                actionsCard,
                "Закрыть текущий уровень",
                "Проходит текущий target-уровень через реальный production flow.",
                "Пройти",
                () => RunWinAction(_runner.WinCurrentLevel),
                DevToolsUiToolkitTheme.SurfaceAccent);
            _winPartButton = AddActionRow(
                actionsCard,
                "Закрыть текущую часть дня",
                "Последовательно проходит оставшиеся уровни в текущей части дня.",
                "Пройти",
                () => RunWinAction(_runner.WinCurrentPartOfDay),
                DevToolsUiToolkitTheme.SurfaceAccent);
            _winLocationButton = AddActionRow(
                actionsCard,
                "Закрыть текущую локацию",
                "Последовательно проходит оставшиеся уровни текущей локации.",
                "Пройти",
                () => RunWinAction(_runner.WinCurrentLocation),
                DevToolsUiToolkitTheme.SurfaceAccent);
            Root.Add(actionsCard);
        }

        public override void Refresh()
        {
            _prepareButton.SetEnabled(_runner.CanPrepareLevelUp);
            _resetButton.SetEnabled(_runner.CanResetProgress);
            _winLevelButton.SetEnabled(_runner.CanWinCurrentLevel);
            _winPartButton.SetEnabled(_runner.CanWinCurrentPartOfDay);
            _winLocationButton.SetEnabled(_runner.CanWinCurrentLocation);
            _currentLevelLabel.text = _runner.CurrentPoint;
            _statusLabel.text = _runner.Status;
            _actionLabel.text = _runner.CurrentAction;
        }

        private void RunWinAction(Action action)
        {
            _closeBeforeWinAction?.Invoke();
            action?.Invoke();
        }
    }

    public sealed class ExperienceProgressTestingUiPage : DevToolsUiPageBase
    {
        private readonly ExperienceProgressTestRunner _runner = ExperienceProgressTestRunner.Shared;
        private readonly ReturnActivityTestingRunner _returnRunner = ReturnActivityTestingRunner.Shared;
        private readonly DevToolsCollapsibleSection _runSection;
        private readonly DevToolsCollapsibleSection _firstSessionSection;
        private readonly DevToolsCollapsibleSection _progressionSection;
        private readonly DevToolsCollapsibleSection _returnSection;
        private readonly Button _prepareButton;
        private readonly Button _completeButton;
        private readonly Label _targetLabel;
        private readonly Label _statusLabel;
        private readonly Button _grantButton;
        private readonly Button _inspectButton;
        private readonly Label _firstSessionLabel;
        private readonly List<(Button Button, Func<bool> Enabled)> _progressionButtons = new();
        private readonly Label _progressionSnapshot;
        private readonly Button _returnBeginButton;
        private readonly Button _returnRestoreButton;
        private readonly Button _returnWinButton;
        private readonly Button _returnDayButton;
        private readonly Button _returnBackDayButton;
        private readonly Button _returnWeekButton;
        private readonly Button _returnClaimButton;
        private readonly Button _returnInspectButton;
        private readonly Label _returnStatus;

        public ExperienceProgressTestingUiPage(DevToolsUiToolkitFactory factory)
            : base(factory, "XP и уровни")
        {
            VisualElement summaryCard = CreateSummaryCard(
                "СВОДКА",
                "Сначала target и текущее состояние. Редкие и длинные блоки спрятаны в сворачиваемые секции.");
            _targetLabel = AddInfoRow(summaryCard, "Target Level");
            _statusLabel = AddInfoRow(summaryCard, "Статус");
            Root.Add(summaryCard);

            _runSection = new DevToolsCollapsibleSection(
                factory,
                "Новый рекорд",
                "Подготовка weekly best и completion следующего непройденного уровня.",
                expanded: true);
            _prepareButton = AddActionRow(
                _runSection.Content,
                "Подготовить рекорд",
                "Ставит следующий score на 10 выше реального weekly best текущего target.",
                "Подготовить",
                _runner.PrepareNewRecord,
                DevToolsUiToolkitTheme.Surface);
            _completeButton = AddActionRow(
                _runSection.Content,
                "Закрыть следующий уровень",
                "Завершает ближайший непройденный уровень через production flow.",
                "Завершить",
                _runner.CompleteNextLevel,
                DevToolsUiToolkitTheme.SurfaceAccent);
            Root.Add(_runSection.Root);

            _firstSessionSection = new DevToolsCollapsibleSection(
                factory,
                "Первая сессия",
                "Разовые действия и снимок состояния первой сессии.",
                expanded: false);
            _grantButton = AddActionRow(
                _firstSessionSection.Content,
                "Выдать tutorial-бонус",
                "Повторная выдача проходит через production-дедупликацию.",
                "Выдать",
                _runner.GrantTutorialBonus,
                DevToolsUiToolkitTheme.Surface);
            _inspectButton = AddActionRow(
                _firstSessionSection.Content,
                "Обновить снимок",
                "Читает текущее состояние первой сессии без изменения профиля.",
                "Обновить",
                _runner.InspectFirstSessionState,
                DevToolsUiToolkitTheme.SurfaceAccent);
            _firstSessionLabel = factory.CreateStatus(string.Empty);
            _firstSessionLabel.style.marginTop = 12f;
            _firstSessionSection.Content.Add(_firstSessionLabel);
            Root.Add(_firstSessionSection.Root);

            _progressionSection = new DevToolsCollapsibleSection(
                factory,
                "DEV-профиль и прогрессия",
                "Редкие команды для изолированного профиля и runtime-снимка.",
                expanded: false);
            foreach ((string Label, Action Execute, Func<bool> Enabled) command in AbilityProgressTestingRunner.Shared.Commands)
            {
                Button button = AddActionRow(
                    _progressionSection.Content,
                    command.Label,
                    null,
                    "Выполнить",
                    command.Execute,
                    DevToolsUiToolkitTheme.Surface);
                _progressionButtons.Add((button, command.Enabled));
            }

            _progressionSnapshot = factory.CreateStatus(string.Empty);
            _progressionSnapshot.style.marginTop = 12f;
            _progressionSection.Content.Add(_progressionSnapshot);
            Root.Add(_progressionSection.Root);

            _returnSection = new DevToolsCollapsibleSection(
                factory,
                "Активности / Return",
                "Изолированная сессия активностей и связанные мутации для ручной проверки.",
                expanded: false);
            _returnBeginButton = AddActionRow(
                _returnSection.Content,
                "Начать изолированную сессию",
                "Создаёт отдельный sandbox для ручной проверки активностей.",
                "Начать",
                _returnRunner.Begin,
                DevToolsUiToolkitTheme.Surface);
            _returnRestoreButton = AddActionRow(
                _returnSection.Content,
                "Вернуть исходное сохранение",
                "Восстанавливает базовый save после тестовой сессии активностей.",
                "Вернуть",
                _returnRunner.Restore,
                DevToolsUiToolkitTheme.SurfaceAccent);
            _returnWinButton = AddActionRow(
                _returnSection.Content,
                "Победа без XP",
                "Фиксирует победу, не меняя опыт игрока.",
                "Запустить",
                _returnRunner.Win,
                DevToolsUiToolkitTheme.Surface);
            _returnDayButton = AddActionRow(
                _returnSection.Content,
                "Следующий локальный день",
                "Сдвигает локальный день вперёд на один.",
                "+1 день",
                _returnRunner.NextDay,
                DevToolsUiToolkitTheme.Surface);
            _returnBackDayButton = AddActionRow(
                _returnSection.Content,
                "Предыдущий локальный день",
                "Сдвигает локальный день назад на один.",
                "-1 день",
                _returnRunner.PreviousDay,
                DevToolsUiToolkitTheme.Surface);
            _returnWeekButton = AddActionRow(
                _returnSection.Content,
                "+7 локальных дней",
                "Быстрый сдвиг календаря вперёд на неделю.",
                "+7 дней",
                _returnRunner.NextWeek,
                DevToolsUiToolkitTheme.Surface);
            _returnClaimButton = AddActionRow(
                _returnSection.Content,
                "Claim одной награды",
                "Пробует выдать одну текущую награду активностей.",
                "Claim",
                _returnRunner.Claim,
                DevToolsUiToolkitTheme.Surface);
            _returnInspectButton = AddActionRow(
                _returnSection.Content,
                "Снимок активностей",
                "Читает текущее состояние activity-сессии и наград.",
                "Прочитать",
                _returnRunner.Inspect,
                DevToolsUiToolkitTheme.SurfaceAccent);
            _returnStatus = factory.CreateStatus(string.Empty);
            _returnStatus.style.marginTop = 12f;
            _returnSection.Content.Add(_returnStatus);
            Root.Add(_returnSection.Root);
        }

        public override void Refresh()
        {
            _statusLabel.text = _runner.IsMainMenuReady
                ? _runner.Status
                : "Откройте Main Menu и держите страницу на нём перед запуском команд.";
            _prepareButton.text = _runner.PrepareNewRecordTitle;
            _prepareButton.SetEnabled(_runner.CanPrepareNewRecord);
            _completeButton.SetEnabled(_runner.CanCompleteNextLevel);
            _targetLabel.text = _runner.TargetLevel;
            _grantButton.SetEnabled(_runner.CanGrantTutorialBonus);
            _inspectButton.SetEnabled(_runner.CanInspectFirstSession);
            _firstSessionLabel.text = _runner.FirstSessionState;

            AbilityProgressTestingRunner progression = AbilityProgressTestingRunner.Shared;
            for (int index = 0; index < _progressionButtons.Count; index++)
            {
                (Button button, Func<bool> enabled) = _progressionButtons[index];
                button.SetEnabled(enabled());
            }
            _progressionSnapshot.text = progression.Snapshot();

            _returnBeginButton.SetEnabled(_returnRunner.CanBegin);
            _returnRestoreButton.SetEnabled(GameDataManager.HasProgressionTestingBackup);
            _returnWinButton.SetEnabled(_returnRunner.CanChange);
            _returnDayButton.SetEnabled(_returnRunner.CanChange);
            _returnBackDayButton.SetEnabled(_returnRunner.CanChange);
            _returnWeekButton.SetEnabled(_returnRunner.CanChange);
            _returnClaimButton.SetEnabled(_returnRunner.CanChange);
            _returnInspectButton.SetEnabled(true);
            _returnStatus.text = _returnRunner.Status;
        }
    }

    public sealed class QuestTestingUiPage : DevToolsUiPageBase
    {
        private readonly QuestTestRunner _runner = QuestTestRunner.Shared;
        private readonly DevToolsCollapsibleSection _daySection;
        private readonly DevToolsCollapsibleSection _stateSection;
        private readonly Button _dailyButton;
        private readonly Button _storyButton;
        private readonly DropdownField _questDropdown;
        private readonly Label _questTitle;
        private readonly Label _questKind;
        private readonly Label _questDayInfo;
        private readonly Button _questDayButton;
        private readonly Label _statusLabel;
        private readonly Label _beforeLabel;
        private readonly Label _afterLabel;
        private readonly Button _resetButton;
        private readonly Button _advanceButton;
        private readonly Button _completeButton;
        private readonly Button _claimButton;
        private bool _syncingSelection;
        private List<string> _choices = new();

        public QuestTestingUiPage(DevToolsUiToolkitFactory factory)
            : base(factory, "Квесты")
        {
            VisualElement summaryCard = CreateSummaryCard(
                "СВОДКА",
                "Только выбранный квест получает прогресс. Сначала видно его статус, потом редкие детали.");
            _statusLabel = AddInfoRow(summaryCard, "Статус");
            _questTitle = AddInfoRow(summaryCard, "Квест");
            _questKind = AddInfoRow(summaryCard, "Тип");
            Root.Add(summaryCard);

            _daySection = new DevToolsCollapsibleSection(
                factory,
                "Квестовый день",
                "Временные маркеры и ручной сдвиг quest time.",
                expanded: false);
            _questDayInfo = factory.CreateStatus(string.Empty);
            _daySection.Content.Add(_questDayInfo);
            _questDayButton = AddActionRow(
                _daySection.Content,
                "Сдвинуть квестовый день",
                "Переключает только quest time и даёт QuestManager отреагировать обычным daily check.",
                "+1 день",
                _runner.SimulateNextQuestDay,
                DevToolsUiToolkitTheme.SurfaceAccent);
            Root.Add(_daySection.Root);

            VisualElement selectionCard = CreateSectionCard(
                "ВЫБОР КВЕСТА",
                "Сначала категория, потом активная карточка для ручного прогона.");
            VisualElement categoryRow = factory.CreateRow();
            _dailyButton = factory.CreateActionButton("Ежедневные", DevToolsUiToolkitTheme.SurfaceAccent, () => SelectCategory(QuestCategory.Daily), compact: true);
            _storyButton = factory.CreateActionButton("Сюжетные", DevToolsUiToolkitTheme.SurfaceAccent, () => SelectCategory(QuestCategory.Story), compact: true);
            _dailyButton.style.flexGrow = 1f;
            _storyButton.style.flexGrow = 1f;
            _storyButton.style.marginLeft = 16f;
            categoryRow.Add(_dailyButton);
            categoryRow.Add(_storyButton);
            selectionCard.Add(categoryRow);

            _questDropdown = factory.CreateDropdown("Активный квест");
            _questDropdown.style.marginTop = 14f;
            _questDropdown.RegisterValueChangedCallback(OnQuestSelected);
            selectionCard.Add(_questDropdown);
            Root.Add(selectionCard);

            _stateSection = new DevToolsCollapsibleSection(
                factory,
                "Состояние до / после",
                "Детальные снимки состояния выбранного квеста до и после действия.",
                expanded: false,
                background: DevToolsUiToolkitTheme.Summary);
            _beforeLabel = AddInfoRow(_stateSection.Content, "До");
            _afterLabel = AddInfoRow(_stateSection.Content, "После");
            Root.Add(_stateSection.Root);

            VisualElement actionsCard = CreateSectionCard(
                "КОМАНДЫ",
                "Короткие действия для выбранного квеста через реальные production paths.");
            _resetButton = AddActionRow(actionsCard, "Сбросить квест", "Возвращает выбранный квест и его внешнюю цель в исходное состояние.", "Сбросить", _runner.ResetQuest, DevToolsUiToolkitTheme.Surface);
            _advanceButton = AddActionRow(actionsCard, "Сделать шаг", "Даёт частичный прогресс без полного completion.", "Шаг", _runner.Advance, DevToolsUiToolkitTheme.Surface);
            _completeButton = AddActionRow(actionsCard, "Завершить квест", "Проводит квест через его штатный путь completion.", "Завершить", _runner.Complete, DevToolsUiToolkitTheme.SurfaceAccent);
            _claimButton = AddActionRow(actionsCard, "Забрать награду", "Пробует выдать reward выбранного квеста.", "Забрать", _runner.ClaimReward, DevToolsUiToolkitTheme.SurfaceAccent);
            Root.Add(actionsCard);
        }

        public override void Refresh()
        {
            SelectCategoryVisual(_runner.SelectedCategory);
            _questDayInfo.text =
                $"Device: {_runner.DeviceLocalTime}\n" +
                $"Quest: {_runner.QuestLocalTime}\n" +
                $"Mode: {_runner.QuestTimeMode}\n" +
                $"Daily: {_runner.DailyGenerationDate}\n" +
                $"Story: {_runner.StoryGenerationDate}";
            _questDayButton.SetEnabled(_runner.CanSimulateNextQuestDay);

            UpdateQuestChoices();
            _statusLabel.text = _runner.Status;
            _questTitle.text = _runner.Title;
            _questKind.text = _runner.Kind;
            _beforeLabel.text = _runner.BeforeState;
            _afterLabel.text = _runner.AfterState;

            _resetButton.SetEnabled(_runner.CanResetQuest);
            _advanceButton.SetEnabled(_runner.CanAdvance);
            _completeButton.SetEnabled(_runner.CanComplete);
            _claimButton.SetEnabled(_runner.CanClaimReward);
        }

        private void SelectCategory(QuestCategory category)
        {
            _runner.SelectCategory(category);
            Refresh();
        }

        private void OnQuestSelected(ChangeEvent<string> evt)
        {
            if (_syncingSelection)
                return;

            int index = _choices.IndexOf(evt.newValue);
            if (index >= 0)
            {
                _runner.SelectQuest(index);
                Refresh();
            }
        }

        private void UpdateQuestChoices()
        {
            _choices = _runner.GetQuestOptions().ToList();
            _syncingSelection = true;
            try
            {
                if (_choices.Count == 0)
                {
                    _questDropdown.choices = new List<string> { "Нет активных квестов" };
                    _questDropdown.SetValueWithoutNotify("Нет активных квестов");
                    _questDropdown.SetEnabled(false);
                    return;
                }

                _questDropdown.choices = _choices;
                int index = Mathf.Clamp(_runner.SelectedQuestIndex, 0, _choices.Count - 1);
                _questDropdown.SetValueWithoutNotify(_choices[index]);
                _questDropdown.SetEnabled(true);
            }
            finally
            {
                _syncingSelection = false;
            }
        }

        private void SelectCategoryVisual(QuestCategory category)
        {
            _dailyButton.style.backgroundColor = category == QuestCategory.Daily
                ? DevToolsUiToolkitTheme.SurfaceAccent
                : DevToolsUiToolkitTheme.Surface;
            _storyButton.style.backgroundColor = category == QuestCategory.Story
                ? DevToolsUiToolkitTheme.SurfaceAccent
                : DevToolsUiToolkitTheme.Surface;
        }
    }

    public sealed class SkateboardTestingUiPage : DevToolsUiPageBase
    {
        private readonly SkateboardTestingRunner _runner = SkateboardTestingRunner.Shared;
        private readonly Action _closeBeforeGameplayAction;
        private readonly DevToolsCollapsibleSection _preparationSection;
        private readonly DevToolsCollapsibleSection _scriptedSection;
        private readonly DevToolsCollapsibleSection _guidedSection;
        private readonly DevToolsCollapsibleSection _checklistSection;
        private readonly Button _prepareButton;
        private readonly Button _pauseButton;
        private readonly Button _stopButton;
        private readonly Button _jumpButton;
        private readonly Button _superJumpButton;
        private readonly Button _timeoutButton;
        private readonly Button _rideButton;
        private readonly Button _jumpCollisionButton;
        private readonly Button _laneShiftButton;
        private readonly Label _statusLabel;
        private readonly Label _instructionLabel;
        private readonly Label _liveStatusLabel;
        private readonly VisualElement _checklistContainer;

        public SkateboardTestingUiPage(DevToolsUiToolkitFactory factory, Action closeBeforeGameplayAction = null)
            : base(factory, "Скейтборд")
        {
            _closeBeforeGameplayAction = closeBeforeGameplayAction;
            VisualElement summaryCard = CreateSummaryCard(
                "СВОДКА",
                "Сначала текущее состояние и подсказка, затем уже действия по сценариям и проверкам.");
            _statusLabel = AddInfoRow(summaryCard, "Статус");
            _instructionLabel = AddInfoRow(summaryCard, "Подсказка");
            _liveStatusLabel = AddInfoRow(summaryCard, "Live status");
            Root.Add(summaryCard);

            _preparationSection = new DevToolsCollapsibleSection(
                factory,
                "Подготовка",
                "Открытие и выбор Skateboard перед живыми прогонами.",
                expanded: true);
            _prepareButton = AddActionRow(_preparationSection.Content, "Разблокировать и выбрать", "Открывает Skateboard через development owner и делает его активным.", "Подготовить", _runner.PrepareUnlockAndSelectSkateboard, DevToolsUiToolkitTheme.SurfaceAccent);
            _pauseButton = AddActionRow(_preparationSection.Content, "Пауза игры", "Ставит или снимает паузу в текущем runtime-сценарии.", "Пауза", _runner.TogglePause, DevToolsUiToolkitTheme.Surface);
            _stopButton = AddActionRow(_preparationSection.Content, "Остановить проверку", "Завершает текущий guided/scripted check и закрывает mode.", "Стоп", _runner.StopCheck, DevToolsUiToolkitTheme.SurfaceDanger);
            Root.Add(_preparationSection.Root);

            _scriptedSection = new DevToolsCollapsibleSection(
                factory,
                "Сценарии",
                "Короткие scripted-прогоны без ручного ввода.",
                expanded: false);
            _jumpButton = AddActionRow(_scriptedSection.Content, "Jump", "Базовый scripted jump-сценарий на активной сцене.", "Запустить", () => RunGameplayAction(_runner.RunJumpScenario), DevToolsUiToolkitTheme.SurfaceAccent);
            _superJumpButton = AddActionRow(_scriptedSection.Content, "Super Jump", "Scripted-сценарий для super jump варианта.", "Запустить", () => RunGameplayAction(_runner.RunSuperJumpScenario), DevToolsUiToolkitTheme.SurfaceAccent);
            Root.Add(_scriptedSection.Root);

            _guidedSection = new DevToolsCollapsibleSection(
                factory,
                "Guided checks",
                "Ручные проверки с короткими инструкциями и live-наблюдением.",
                expanded: false);
            _timeoutButton = AddActionRow(_guidedSection.Content, "Timeout", "Автоматическая проверка истечения времени skateboard mode.", "Запустить", () => RunGameplayAction(_runner.RunTimeoutCheck), DevToolsUiToolkitTheme.Surface);
            _rideButton = AddActionRow(_guidedSection.Content, "Ride Collision", "Проверка столкновения в режиме ride.", "Запустить", () => RunGameplayAction(_runner.StartRideCollisionCheck), DevToolsUiToolkitTheme.Surface);
            _jumpCollisionButton = AddActionRow(_guidedSection.Content, "Jump Collision", "Проверка столкновения во время прыжка.", "Запустить", () => RunGameplayAction(_runner.StartJumpCollisionCheck), DevToolsUiToolkitTheme.Surface);
            _laneShiftButton = AddActionRow(_guidedSection.Content, "Lane Shift", "Проверка смены линии и реакции на jump/tap.", "Запустить", () => RunGameplayAction(_runner.StartLaneShiftCheck), DevToolsUiToolkitTheme.Surface);
            Root.Add(_guidedSection.Root);

            _checklistSection = new DevToolsCollapsibleSection(
                factory,
                "Чеклист",
                "Появляется во время активных проверок и показывает PASS/FAIL по шагам.",
                expanded: false,
                background: DevToolsUiToolkitTheme.Summary);
            _checklistContainer = new VisualElement();
            _checklistContainer.style.flexDirection = FlexDirection.Column;
            _checklistSection.Content.Add(_checklistContainer);
            Root.Add(_checklistSection.Root);
        }

        public override void Refresh()
        {
            _prepareButton.SetEnabled(_runner.CanPrepare);
            _pauseButton.text = _runner.PauseButtonLabel;
            _pauseButton.SetEnabled(_runner.CanTogglePause);
            _stopButton.SetEnabled(_runner.CanStopCheck);
            _jumpButton.SetEnabled(_runner.CanRunScenario);
            _superJumpButton.SetEnabled(_runner.CanRunScenario);
            _timeoutButton.SetEnabled(_runner.CanStartGuidedCheck);
            _rideButton.SetEnabled(_runner.CanStartGuidedCheck);
            _jumpCollisionButton.SetEnabled(_runner.CanStartGuidedCheck);
            _laneShiftButton.SetEnabled(_runner.CanStartGuidedCheck);
            _statusLabel.text = _runner.Status;
            _instructionLabel.text = string.IsNullOrWhiteSpace(_runner.Instruction) ? "Инструкции появятся после запуска guided check." : _runner.Instruction;
            _liveStatusLabel.text = _runner.LiveStatus;
            _checklistSection.Root.style.display = _runner.Checklist.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            _checklistContainer.Clear();
            foreach (SkateboardTestingRunner.ChecklistItem item in _runner.Checklist)
            {
                Label row = Factory.CreateStatus($"{BuildChecklistPrefix(item.State)} {item.Label}{BuildDetails(item.Details)}");
                row.style.color = item.State switch
                {
                    SkateboardTestingRunner.ChecklistState.Pass => new Color(0.18f, 0.55f, 0.22f, 1f),
                    SkateboardTestingRunner.ChecklistState.Fail => new Color(0.72f, 0.15f, 0.12f, 1f),
                    _ => DevToolsUiToolkitTheme.TextStrong
                };
                _checklistContainer.Add(row);
            }
        }

        private static string BuildChecklistPrefix(SkateboardTestingRunner.ChecklistState state)
        {
            return state switch
            {
                SkateboardTestingRunner.ChecklistState.Pass => "PASS",
                SkateboardTestingRunner.ChecklistState.Fail => "FAIL",
                _ => "[ ]"
            };
        }

        private static string BuildDetails(string details)
        {
            return string.IsNullOrWhiteSpace(details) ? string.Empty : $" — {details}";
        }

        private void RunGameplayAction(Action action)
        {
            _closeBeforeGameplayAction?.Invoke();
            action?.Invoke();
        }
    }

    public sealed class SkinTestingUiPage : DevToolsUiPageBase
    {
        private readonly SkinTestingRunner _runner = SkinTestingRunner.Shared;
        private readonly VisualElement _detailsCard;
        private readonly Button _runButton;
        private readonly Label _availabilityLabel;
        private readonly Label _statusLabel;
        private readonly Label _targetLabel;
        private readonly Label _priceLabel;
        private readonly Label _grantedLabel;
        private readonly Label _purchaseLabel;
        private readonly Label _appliedLabel;

        public SkinTestingUiPage(DevToolsUiToolkitFactory factory)
            : base(factory, "Скины")
        {
            VisualElement summaryCard = CreateSummaryCard(
                "СВОДКА",
                "Сначала readiness и общий результат. Детали операции появляются только после запуска.");
            _availabilityLabel = AddInfoRow(summaryCard, "Готовность");
            _statusLabel = AddInfoRow(summaryCard, "Статус");
            Root.Add(summaryCard);

            VisualElement actionCard = CreateSectionCard(
                "ПОКУПКА И ЭКИПИРОВКА",
                "Начисляет shortfall, покупает и надевает следующий доступный скин production-путём.");
            _runButton = AddActionRow(
                actionCard,
                "Следующий доступный скин",
                "Одна команда на весь flow unlock / buy / equip.",
                "Запустить",
                _runner.UnlockBuyAndEquipNextSkin,
                DevToolsUiToolkitTheme.SurfaceAccent);
            Root.Add(actionCard);

            _detailsCard = CreateSectionCard("ДЕТАЛИ ОПЕРАЦИИ", background: DevToolsUiToolkitTheme.Summary);
            _targetLabel = AddInfoRow(_detailsCard, "Target");
            _priceLabel = AddInfoRow(_detailsCard, "Price");
            _grantedLabel = AddInfoRow(_detailsCard, "Granted");
            _purchaseLabel = AddInfoRow(_detailsCard, "Purchase");
            _appliedLabel = AddInfoRow(_detailsCard, "Applied");
            Root.Add(_detailsCard);
        }

        public override void Refresh()
        {
            _availabilityLabel.text = _runner.AvailabilityStatus;
            _runButton.SetEnabled(_runner.CanRun);
            _statusLabel.text = _runner.Status;
            _targetLabel.text = _runner.TargetStatus;
            _priceLabel.text = _runner.PriceStatus;
            _grantedLabel.text = _runner.GrantedStatus;
            _purchaseLabel.text = _runner.PurchaseStatus;
            _appliedLabel.text = _runner.AppliedStatus;
            bool hasOperationDetails = _runner.TargetStatus != "—" || _runner.PriceStatus != "—" ||
                                      _runner.GrantedStatus != "—" || _runner.PurchaseStatus != "—" ||
                                      _runner.AppliedStatus != "—";
            _detailsCard.style.display = hasOperationDetails ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
#endif