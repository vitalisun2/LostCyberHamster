using System;
using System.Collections.Generic;
using System.Linq;
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
    /// Показывает прогресс персонажа и открывает элементы за Development Points.
    /// </summary>
    public sealed class CharacterDevelopmentScreenController :
        ScreenController
    {
        private const int MinimumCardsPerLine = 5;

        private readonly List<AddressableLease<Sprite>> _iconLeases = new();
        private CancellationTokenSource _iconLoadCancellation;

        private Button BackButton =>
            _contentRoot.Q<Button>("development__btn-back");
        private Button EquipmentButton =>
            _contentRoot.Q<Button>("development__btn-equipment");
        private Label PlayerLevelLabel =>
            _contentRoot.Q<Label>("development__player-level");
        private ProgressBar ExperienceProgress =>
            _contentRoot.Q<ProgressBar>("development__xp-progress");
        private Label ExperienceLabel =>
            _contentRoot.Q<Label>("development__xp-label");
        private Label PointsLabel =>
            _contentRoot.Q<Label>("development__points-value");
        private ScrollView SkinScroll =>
            _contentRoot.Q<ScrollView>("development__skin-scroll");
        private ScrollView AbilityScroll =>
            _contentRoot.Q<ScrollView>("development__ability-scroll");
        private Button SkinPreviousButton =>
            _contentRoot.Q<Button>("development__skin-prev");
        private Button SkinNextButton =>
            _contentRoot.Q<Button>("development__skin-next");
        private Button AbilityPreviousButton =>
            _contentRoot.Q<Button>("development__ability-prev");
        private Button AbilityNextButton =>
            _contentRoot.Q<Button>("development__ability-next");
        private VisualElement SkinCards =>
            _contentRoot.Q<VisualElement>("development__skin-cards");
        private VisualElement AbilityCards =>
            _contentRoot.Q<VisualElement>("development__ability-cards");
        protected override ScreenEnum _screenAssetName =>
            ScreenEnum.CharacterDevelopmentScreen;

        public CharacterDevelopmentScreenController(UIDocument uiDocument)
            : base(uiDocument)
        {
        }

        protected override async Task OnLoadAsync()
        {
            await ChangeBackgroundAsync("BackgroundScreenSprite");
            UpdatePlayerProgress();
            _ = ObserveRefreshCardsAsync(RefreshCardsAsync());
        }

        protected override void OnSubscribeToEvents()
        {
            BackButton?.RegisterCallback<ClickEvent>(OnBackClicked);
            EquipmentButton?.RegisterCallback<ClickEvent>(
                OnEquipmentClicked);
            SkinPreviousButton?.RegisterCallback<ClickEvent>(
                OnSkinPreviousClicked);
            SkinNextButton?.RegisterCallback<ClickEvent>(OnSkinNextClicked);
            AbilityPreviousButton?.RegisterCallback<ClickEvent>(
                OnAbilityPreviousClicked);
            AbilityNextButton?.RegisterCallback<ClickEvent>(
                OnAbilityNextClicked);
        }

        protected override void OnUnsubscribeFromEvents()
        {
            BackButton?.UnregisterCallback<ClickEvent>(OnBackClicked);
            EquipmentButton?.UnregisterCallback<ClickEvent>(
                OnEquipmentClicked);
            SkinPreviousButton?.UnregisterCallback<ClickEvent>(
                OnSkinPreviousClicked);
            SkinNextButton?.UnregisterCallback<ClickEvent>(OnSkinNextClicked);
            AbilityPreviousButton?.UnregisterCallback<ClickEvent>(
                OnAbilityPreviousClicked);
            AbilityNextButton?.UnregisterCallback<ClickEvent>(
                OnAbilityNextClicked);
            ReleaseIconResources();
        }

        private void UpdatePlayerProgress()
        {
            PlayerData playerData = GameDataManager.PlayerData;
            int threshold = PlayerExperienceService.PlayerLevelThreshold;

            PlayerLevelLabel.text = playerData.PlayerLevel.ToString();
            ExperienceLabel.text =
                $"{playerData.ExperiencePoints} / {threshold}";
            ExperienceProgress.lowValue = 0;
            ExperienceProgress.highValue = threshold;
            ExperienceProgress.value = playerData.ExperiencePoints;
            PointsLabel.text = FormatLocalized(
                "development_points_value",
                playerData.DevelopmentPoints.ToString());
        }

        private async Task RefreshCardsAsync()
        {
            CancellationToken cancellationToken = BeginIconLoading();
            SkinCards.Clear();
            AbilityCards.Clear();

            // Строим skin line из production catalog.
            int skinCount = 0;
            foreach (Skin skin in SkinManager.AvailableSkins
                         .Where(
                             skin => skin.Id != CharacterDevelopmentService
                                 .DefaultSkinId)
                         .OrderBy(
                             skin =>
                                 !CharacterDevelopmentService.IsSkinUnlocked(
                                     skin.Id)))
            {
                SkinCards.Add(CreateSkinCard(skin));
                skinCount++;
            }
            AddLockedPlaceholders(SkinCards, "skin", skinCount);
            UpdateCarouselNavigation(
                SkinPreviousButton,
                SkinNextButton,
                SkinCards.childCount);

            // Строим ability line и параллельно загружаем production icons.
            var iconTasks = new List<Task>();
            int abilityCount = 0;
            foreach (SuperAttackData ability in
                     SuperAttackService.Items.OrderBy(
                         ability =>
                             !CharacterDevelopmentService
                                 .IsSuperAttackUnlocked(ability.Id)))
            {
                Button card = CreateAbilityCard(
                    ability,
                    out VisualElement icon);
                AbilityCards.Add(card);
                if (icon != null)
                {
                    iconTasks.Add(LoadIconAsync(
                        icon,
                        ability.IconAddress,
                        cancellationToken));
                }

                abilityCount++;
            }
            AddLockedPlaceholders(AbilityCards, "ability", abilityCount);
            UpdateCarouselNavigation(
                AbilityPreviousButton,
                AbilityNextButton,
                AbilityCards.childCount);

            await Task.WhenAll(iconTasks);
        }

        private static void UpdateCarouselNavigation(
            Button previousButton,
            Button nextButton,
            int cardCount)
        {
            DisplayStyle display = cardCount > MinimumCardsPerLine
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            previousButton.style.display = display;
            nextButton.style.display = display;
        }

        private Button CreateSkinCard(Skin skin)
        {
            bool isUnlocked =
                CharacterDevelopmentService.IsSkinUnlocked(skin.Id);
            var card = CreateCard(
                $"development-skin-card-{skin.Id}",
                skin.Name,
                isUnlocked,
                out VisualElement icon);
            if (icon != null)
            {
                icon.style.backgroundImage = new StyleBackground(
                    skin.HamsterSprite);
            }

            if (!isUnlocked)
            {
                card.RegisterCallback<ClickEvent>(
                    _ => OnUnlockSkinClicked(skin.Id));
            }

            return card;
        }

        private Button CreateAbilityCard(
            SuperAttackData ability,
            out VisualElement icon)
        {
            bool isUnlocked =
                CharacterDevelopmentService.IsSuperAttackUnlocked(
                    ability.Id);
            var card = CreateCard(
                $"development-ability-card-{ability.Id}",
                Localize(ability.NameLocalizationKey),
                isUnlocked,
                out icon);

            if (!isUnlocked)
            {
                card.RegisterCallback<ClickEvent>(
                    _ => OnUnlockAbilityClicked(ability.Id));
            }

            return card;
        }

        private static Button CreateCard(
            string name,
            string title,
            bool isUnlocked,
            out VisualElement icon)
        {
            var card = new Button { name = name };
            card.AddToClassList("development-card");
            card.EnableInClassList(
                "development-card--locked",
                !isUnlocked);

            if (isUnlocked)
            {
                icon = new VisualElement();
                icon.AddToClassList("development-card__icon");
                card.Add(icon);

                var nameLabel = new Label(title);
                nameLabel.AddToClassList("development-card__name");
                card.Add(nameLabel);
            }
            else
            {
                icon = null;
                card.Add(CreateLock("development-card__lock"));
            }

            var status = new Label(Localize(
                isUnlocked
                    ? "development_unlocked"
                    : "development_unlock_cost"));
            status.AddToClassList("development-card__status");
            card.Add(status);

            if (!isUnlocked)
            {
                card.SetEnabled(
                    CharacterDevelopmentService.DevelopmentPoints > 0);
            }

            return card;
        }

        private static void AddLockedPlaceholders(
            VisualElement container,
            string catalogName,
            int existingCount)
        {
            for (int index = existingCount;
                 index < MinimumCardsPerLine;
                 index++)
            {
                var card = new Button
                {
                    name = $"development-{catalogName}-placeholder-{index}"
                };
                card.AddToClassList("development-card");
                card.AddToClassList("development-card--locked");
                card.AddToClassList("development-card--placeholder");
                card.Add(CreateLock("development-card__lock"));
                card.SetEnabled(false);
                container.Add(card);
            }
        }

        private static VisualElement CreateLock(string className)
        {
            var lockIcon = new VisualElement
            {
                pickingMode = PickingMode.Ignore
            };
            lockIcon.AddToClassList(className);
            return lockIcon;
        }

        private void OnUnlockSkinClicked(int skinId)
        {
            if (CharacterDevelopmentService.TryUnlockSkin(skinId))
            {
                RefreshAfterUnlock();
            }
        }

        private void OnUnlockAbilityClicked(int abilityId)
        {
            if (CharacterDevelopmentService.TryUnlockSuperAttack(abilityId))
            {
                RefreshAfterUnlock();
            }
        }

        private void RefreshAfterUnlock()
        {
            UpdatePlayerProgress();
            _ = ObserveRefreshCardsAsync(RefreshCardsAsync());
        }

        private async Task LoadIconAsync(
            VisualElement icon,
            string iconAddress,
            CancellationToken cancellationToken)
        {
            AddressableLease<Sprite> lease = null;
            try
            {
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
                Debug.LogError(FormatLocalized(
                    "development_icon_load_error",
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
                    "development_refresh_error",
                    exception.Message));
            }
        }

        private CancellationToken BeginIconLoading()
        {
            ReleaseIconResources();
            _iconLoadCancellation = new CancellationTokenSource();
            return _iconLoadCancellation.Token;
        }

        private void ReleaseIconResources()
        {
            _iconLoadCancellation?.Cancel();
            _iconLoadCancellation?.Dispose();
            _iconLoadCancellation = null;

            foreach (AddressableLease<Sprite> lease in _iconLeases)
            {
                lease.Dispose();
            }

            _iconLeases.Clear();
        }

        private void OnBackClicked(ClickEvent clickEvent)
        {
            UIManager.OnScreenShow(ScreenEnum.HomeScreen);
        }

        private void OnEquipmentClicked(ClickEvent clickEvent)
        {
            UIManager.OnScreenShow(ScreenEnum.CharacterScreen);
        }

        private void OnSkinPreviousClicked(ClickEvent clickEvent)
        {
            ScrollCards(SkinScroll, -1f);
        }

        private void OnSkinNextClicked(ClickEvent clickEvent)
        {
            ScrollCards(SkinScroll, 1f);
        }

        private void OnAbilityPreviousClicked(ClickEvent clickEvent)
        {
            ScrollCards(AbilityScroll, -1f);
        }

        private void OnAbilityNextClicked(ClickEvent clickEvent)
        {
            ScrollCards(AbilityScroll, 1f);
        }

        private static void ScrollCards(ScrollView scrollView, float direction)
        {
            if (scrollView == null)
            {
                return;
            }

            VisualElement firstCard = scrollView.Q<VisualElement>(
                className: "development-card");
            if (firstCard == null)
            {
                return;
            }

            // Сдвигаем ряд на вычисленную ширину карточки с её отступами.
            var style = firstCard.resolvedStyle;
            float cardStep = style.width +
                             style.marginLeft +
                             style.marginRight;
            if (cardStep <= 0f || float.IsNaN(cardStep))
            {
                return;
            }

            float nextOffset =
                scrollView.scrollOffset.x + direction * cardStep;
            scrollView.scrollOffset = new Vector2(
                Mathf.Clamp(
                    nextOffset,
                    0f,
                    scrollView.horizontalScroller.highValue),
                0f);
        }

        private static string Localize(string key)
        {
            string value = LocalizationManager.GetLocalizedString(key);
            return string.IsNullOrWhiteSpace(value) ? key : value;
        }

        private static string FormatLocalized(
            string key,
            params string[] values)
        {
            return string.Format(Localize(key), values);
        }
    }
}
