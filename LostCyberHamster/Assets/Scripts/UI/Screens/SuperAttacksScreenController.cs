using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Assets.Scripts.System.Resources;
using GameManagement;
using GameManagement.Progress;
using UnityEngine;
using UnityEngine.UIElements;
using Vues.GameCore;

namespace LostCyberHamster.UI
{
    /// <summary>
    /// Показывает суперудары в порядке каталога и позволяет выбрать открытый суперудар.
    /// </summary>
    public sealed class SuperAttacksScreenController : ScreenController
    {
        private readonly List<AddressableLease<Sprite>> _iconLeases = new();
        private CancellationTokenSource _iconLoadCancellation;

        private Button ButtonBack =>
            _contentRoot.Q<Button>("super-attacks__btn-back");

        private Label PlayerLevelLabel =>
            _contentRoot.Q<Label>("super-attacks__player-level");

        private ProgressBar ExperienceProgress =>
            _contentRoot.Q<ProgressBar>("super-attacks__xp-progress");

        private Label ExperienceLabel =>
            _contentRoot.Q<Label>("super-attacks__xp-label");

        private VisualElement Cards =>
            _contentRoot.Q<VisualElement>("super-attacks__cards");

        protected override ScreenEnum _screenAssetName =>
            ScreenEnum.SuperAttacksScreen;

        /// <summary>
        /// Создаёт контроллер экрана суперударов.
        /// </summary>
        public SuperAttacksScreenController(UIDocument uiDocument)
            : base(uiDocument)
        {
        }

        /// <summary>
        /// Загружает фон, прогресс игрока и карточки суперударов.
        /// </summary>
        protected override async Task OnLoadAsync()
        {
            // Загрузка общего фона меню.
            await ChangeBackgroundAsync("BackgroundScreenSprite");

            // Отображение текущего уровня и XP.
            UpdatePlayerProgress();

            // Карточки появляются сразу, а иконки догружаются после активации навигации.
            _ = ObserveRefreshCardsAsync(RefreshCardsAsync());
        }

        /// <summary>
        /// Подписывает кнопку возврата на переход домой.
        /// </summary>
        protected override void OnSubscribeToEvents()
        {
            ButtonBack?.RegisterCallback<ClickEvent>(OnBackClicked);
        }

        /// <summary>
        /// Снимает подписку кнопки возврата и освобождает загруженные иконки.
        /// </summary>
        protected override void OnUnsubscribeFromEvents()
        {
            ButtonBack?.UnregisterCallback<ClickEvent>(OnBackClicked);
            ReleaseIconResources();
        }

        private void UpdatePlayerProgress()
        {
            var playerData = GameDataManager.PlayerData;

            // Отображение числовых значений прогресса.
            PlayerLevelLabel.text = FormatLocalized(
                "super_attacks_level",
                playerData.PlayerLevel.ToString());
            ExperienceLabel.text = string.Join(
                " ",
                $"{playerData.ExperiencePoints} / " +
                PlayerExperienceService.PlayerLevelThreshold,
                Localize("super_attacks_xp_marker"));

            // Настройка шкалы XP внутри текущего уровня.
            ExperienceProgress.lowValue = 0;
            ExperienceProgress.highValue =
                PlayerExperienceService.PlayerLevelThreshold;
            ExperienceProgress.value = playerData.ExperiencePoints;
        }

        private async Task RefreshCardsAsync()
        {
            // Сброс предыдущих загрузок и карточек.
            CancellationToken cancellationToken = BeginIconLoading();
            Cards.Clear();
            var iconLoadTasks = new List<Task>();

            // Создание карточек в исходном порядке JSON.
            foreach (SuperAttackData superAttack in SuperAttackService.Items)
            {
                var card = CreateCard(
                    superAttack,
                    out VisualElement icon);
                Cards.Add(card);

                if (icon != null)
                {
                    iconLoadTasks.Add(LoadIconAsync(
                        icon,
                        superAttack.IconAddress,
                        cancellationToken));
                }
            }

            await Task.WhenAll(iconLoadTasks);
        }

        private VisualElement CreateCard(
            SuperAttackData superAttack,
            out VisualElement icon)
        {
            bool isActive =
                SuperAttackService.ActiveSuperAttackId == superAttack.Id;
            bool isUnlocked = SuperAttackService.IsUnlocked(
                superAttack.Id,
                GameDataManager.PlayerData.PlayerLevel);
            string localizedName = LocalizationManager.GetLocalizedString(
                superAttack.NameLocalizationKey);
            string displayName = string.IsNullOrWhiteSpace(localizedName)
                ? superAttack.NameLocalizationKey
                : localizedName;

            // Создание структуры карточки.
            var card = new VisualElement
            {
                name = $"super-attack-card-{superAttack.Id}"
            };
            card.AddToClassList("super-attacks__card");

            icon = null;
            if (isActive || isUnlocked)
            {
                icon = new VisualElement
                {
                    name = $"super-attack-card__icon-{superAttack.Id}"
                };
                icon.AddToClassList("super-attacks__card-icon");
                card.Add(icon);
            }
            else
            {
                var lockLabel = new Label("🔒")
                {
                    name = $"super-attack-card__lock-{superAttack.Id}"
                };
                lockLabel.AddToClassList("super-attacks__card-lock");
                card.Add(lockLabel);
            }

            var nameLabel = new Label(displayName)
            {
                name = $"super-attack-card__name-{superAttack.Id}"
            };
            nameLabel.AddToClassList("super-attacks__card-name");
            card.Add(nameLabel);

            // Применение одного из трёх состояний карточки.
            if (isActive)
            {
                card.AddToClassList("super-attacks__card--active");

                var activeStatus = new Label(
                    Localize("super_attacks_active"));
                activeStatus.AddToClassList("super-attacks__card-status");
                activeStatus.AddToClassList(
                    "super-attacks__card-status--active");
                card.Add(activeStatus);
                AddUnlockDetails(card, superAttack, true);
                return card;
            }

            if (isUnlocked)
            {
                var availableStatus = new Label(
                    Localize("super_attacks_available"));
                availableStatus.AddToClassList(
                    "super-attacks__card-status");
                card.Add(availableStatus);
                AddUnlockDetails(card, superAttack, true);

                var selectButton = new Button
                {
                    name = $"super-attack-card__select-{superAttack.Id}",
                    text = Localize("super_attacks_select")
                };
                selectButton.AddToClassList("lcs_btn");
                selectButton.AddToClassList(
                    "super-attacks__select-button");
                selectButton.RegisterCallback<ClickEvent>(
                    _ => OnSelectClicked(superAttack.Id));
                card.Add(selectButton);
                return card;
            }

            card.AddToClassList("super-attacks__card--locked");

            var lockedStatus = new Label(
                Localize("super_attacks_locked"));
            lockedStatus.AddToClassList("super-attacks__card-status");
            card.Add(lockedStatus);
            AddUnlockDetails(card, superAttack, false);

            return card;
        }

        private static void AddUnlockDetails(
            VisualElement card,
            SuperAttackData superAttack,
            bool isUnlocked)
        {
            string levelText = isUnlocked
                ? FormatLocalized(
                    "super_attacks_unlocked_at_level",
                    superAttack.RequiredPlayerLevel.ToString())
                : FormatLocalized(
                    "super_attacks_requires_level",
                    superAttack.RequiredPlayerLevel.ToString());
            var requirement = new Label(levelText);
            requirement.AddToClassList(
                "super-attacks__card-requirement");
            card.Add(requirement);

            int experienceThreshold = checked(
                (superAttack.RequiredPlayerLevel - 1) *
                PlayerExperienceService.PlayerLevelThreshold);
            var threshold = new Label(FormatLocalized(
                "super_attacks_xp_threshold",
                experienceThreshold.ToString()));
            threshold.AddToClassList("super-attacks__card-threshold");
            card.Add(threshold);
        }

        private async Task LoadIconAsync(
            VisualElement icon,
            string iconAddress,
            CancellationToken cancellationToken)
        {
            AddressableLease<Sprite> lease = null;
            try
            {
                // Возвращаем управление screen lifecycle до начала фоновой загрузки.
                await Task.Yield();

                // Загрузка и удержание иконки на время жизни экрана.
                lease = await AddressableLoader.LoadAssetAsync<Sprite>(
                    iconAddress,
                    cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();
                if (lease.Value == null)
                {
                    lease.Dispose();
                    return;
                }

                _iconLeases.Add(lease);
                icon.style.backgroundImage =
                    new StyleBackground(lease.Value);
            }
            catch (OperationCanceledException)
            {
                lease?.Dispose();
            }
            catch (Exception exception)
            {
                lease?.Dispose();
                Debug.LogError(
                    FormatLocalized(
                        "super_attacks_icon_load_error",
                        iconAddress,
                        exception.Message));
            }
        }

        private static async Task ObserveRefreshCardsAsync(Task refreshTask)
        {
            try
            {
                await refreshTask;
            }
            catch (Exception exception)
            {
                Debug.LogError(FormatLocalized(
                    "super_attacks_refresh_error",
                    exception.Message));
            }
        }

        private CancellationToken BeginIconLoading()
        {
            // Завершение предыдущего цикла загрузки.
            ReleaseIconResources();

            // Создание токена нового цикла карточек.
            _iconLoadCancellation = new CancellationTokenSource();
            return _iconLoadCancellation.Token;
        }

        private void ReleaseIconResources()
        {
            // Остановка незавершённой загрузки.
            _iconLoadCancellation?.Cancel();
            _iconLoadCancellation?.Dispose();
            _iconLoadCancellation = null;

            // Освобождение всех удерживаемых Addressables lease.
            foreach (AddressableLease<Sprite> lease in _iconLeases)
            {
                lease.Dispose();
            }

            _iconLeases.Clear();
        }

        private async void OnSelectClicked(int superAttackId)
        {
            try
            {
                // Выбор только открытого суперудара через CTA.
                if (!SuperAttackService.TrySelect(superAttackId))
                {
                    return;
                }

                // Обновление active-состояния всех карточек.
                await RefreshCardsAsync();
            }
            catch (Exception exception)
            {
                Debug.LogError(FormatLocalized(
                    "super_attacks_selection_error",
                    exception.Message));
            }
        }

        private void OnBackClicked(ClickEvent clickEvent)
        {
            // Освобождение иконок до замены visual tree.
            ReleaseIconResources();

            // Возврат на главный экран.
            UIManager.OnScreenShow(ScreenEnum.HomeScreen);
        }

        private static string Localize(string key)
        {
            return LocalizationManager.GetLocalizedString(key) ?? key;
        }

        private static string FormatLocalized(
            string key,
            params string[] values)
        {
            return string.Format(Localize(key), values);
        }
    }
}
