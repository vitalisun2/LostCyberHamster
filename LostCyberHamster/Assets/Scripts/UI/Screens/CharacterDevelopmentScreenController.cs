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
        private const int VisibleCardsPerLine = 4;
        private const float DesignWidth = 1672f;
        private const float DesignHeight = 941f;
        private const string BackgroundAddress = "SkillsBackgroundSprite";

        private enum DevelopmentCardState
        {
            Unlocked,
            Preview,
            Hidden
        }

        private readonly List<AddressableLease<Sprite>> _abilityIconLeases =
            new();
        private readonly List<Sprite> _generatedPreviewSprites = new();
        private readonly List<Texture2D> _generatedPreviewTextures = new();
        private CancellationTokenSource _abilityIconCancellation;
        private readonly List<(string Address, VisualElement Icon, bool Grayscale)>
            _pendingAbilityIcons = new();

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

        protected override string ScreenBackgroundAddress => BackgroundAddress;

        protected override ScreenLayout CreateLayout(VisualElement content)
        {
            return ScreenLayout.Fit(
                content.Q<VisualElement>("development__viewport"),
                content.Q<VisualElement>("development__scale-frame"),
                content.Q<VisualElement>("development__design"),
                new Vector2(DesignWidth, DesignHeight));
        }

        protected override void BindView()
        {
            UpdatePlayerProgress();
            BuildCards();
        }

        protected override Task LoadDataAsync()
        {
            CancellationToken cancellationToken = BeginAbilityIconLoading();
            return Task.WhenAll(_pendingAbilityIcons.Select(icon =>
                LoadAbilityIconAsync(icon.Address, icon.Icon, icon.Grayscale, cancellationToken)));
        }

        protected override void OnSubscribeToEvents()
        {
            // Подключаем навигацию и карусели.
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
            // Отключаем screen-local callbacks.
            EquipmentButton?.UnregisterCallback<ClickEvent>(
                OnEquipmentClicked);
            SkinPreviousButton?.UnregisterCallback<ClickEvent>(
                OnSkinPreviousClicked);
            SkinNextButton?.UnregisterCallback<ClickEvent>(OnSkinNextClicked);
            AbilityPreviousButton?.UnregisterCallback<ClickEvent>(
                OnAbilityPreviousClicked);
            AbilityNextButton?.UnregisterCallback<ClickEvent>(
                OnAbilityNextClicked);
            ReleaseAbilityIcons();
            ReleaseGeneratedPreviewIcons();
            _pendingAbilityIcons.Clear();
        }

        private void UpdatePlayerProgress()
        {
            PlayerData playerData = GameDataManager.PlayerData;
            if (playerData == null)
            {
                return;
            }

            int threshold = Math.Max(
                1,
                PlayerExperienceService.PlayerLevelThreshold);
            int experience = Math.Max(0, playerData.ExperiencePoints);

            // Заполняем runtime-данные и ограничиваем прогресс рамками трека.
            PlayerLevelLabel.text = playerData.PlayerLevel.ToString();
            ExperienceLabel.text = $"{experience} / {threshold}";
            ExperienceProgress.lowValue = 0;
            ExperienceProgress.highValue = threshold;
            ExperienceProgress.value = Math.Min(experience, threshold);
            ExperienceProgress.title = string.Empty;
            PointsLabel.text = playerData.DevelopmentPoints.ToString();
        }

        private async Task RefreshCardsAsync()
        {
            BuildCards();
            await LoadDataAsync();
        }

        private void BuildCards()
        {
            ReleaseAbilityIcons();
            ReleaseGeneratedPreviewIcons();
            SkinCards.Clear();
            AbilityCards.Clear();
            _pendingAbilityIcons.Clear();

            // Группируем skins по состоянию.
            // Внутри группы сохраняем catalog order.
            int? nextSkinId = SkinManager.AvailableSkins
                .FirstOrDefault(
                    skin => !CharacterDevelopmentService.IsSkinUnlocked(
                        skin.Id))
                ?.Id;
            var orderedSkins = SkinManager.AvailableSkins
                .Select(skin => new
                {
                    Data = skin,
                    State = ResolveCardState(
                        CharacterDevelopmentService.IsSkinUnlocked(skin.Id),
                        skin.Id == nextSkinId)
                })
                .OrderBy(item => item.State);
            foreach (var item in orderedSkins)
            {
                SkinCards.Add(CreateSkinCard(item.Data, item.State));
            }
            UpdateCarouselNavigation(
                SkinPreviousButton,
                SkinNextButton,
                SkinCards.childCount);

            // Группируем abilities по состоянию.
            // Внутри группы сохраняем catalog order.
            int? nextAbilityId = SuperAttackService.Items
                .FirstOrDefault(
                    ability => !CharacterDevelopmentService
                        .IsSuperAttackUnlocked(ability.Id))
                ?.Id;
            var orderedAbilities = SuperAttackService.Items
                .Select(ability => new
                {
                    Data = ability,
                    State = ResolveCardState(
                        CharacterDevelopmentService.IsSuperAttackUnlocked(
                            ability.Id),
                        ability.Id == nextAbilityId)
                })
                .OrderBy(item => item.State);
            foreach (var item in orderedAbilities)
            {
                VisualElement card = CreateAbilityCard(
                    item.Data,
                    item.State);
                AbilityCards.Add(card);

                VisualElement icon = card.Q<VisualElement>(
                    className: "development-card__icon");
                if (icon != null)
                {
                    _pendingAbilityIcons.Add((
                        item.Data.IconAddress,
                        icon,
                        item.State == DevelopmentCardState.Preview));
                }
            }
            UpdateCarouselNavigation(
                AbilityPreviousButton,
                AbilityNextButton,
                AbilityCards.childCount);
        }

        private static void UpdateCarouselNavigation(
            Button previousButton,
            Button nextButton,
            int cardCount)
        {
            bool canScroll = cardCount > VisibleCardsPerLine;
            previousButton?.SetEnabled(canScroll);
            nextButton?.SetEnabled(canScroll);
        }

        private static DevelopmentCardState ResolveCardState(
            bool isUnlocked,
            bool isNextToUnlock)
        {
            if (isUnlocked)
            {
                return DevelopmentCardState.Unlocked;
            }

            return isNextToUnlock
                ? DevelopmentCardState.Preview
                : DevelopmentCardState.Hidden;
        }

        private VisualElement CreateSkinCard(
            Skin skin,
            DevelopmentCardState state)
        {
            Sprite iconSprite = state == DevelopmentCardState.Preview
                ? CreateGrayscaleSprite(skin.HamsterSprite)
                : skin.HamsterSprite;
            return CreateCard(
                $"development-skin-card-{skin.Id}",
                skin.Name,
                state,
                true,
                iconSprite,
                CharacterDevelopmentService.CanUnlockSkin(skin.Id),
                () => OnUnlockSkinClicked(skin.Id));
        }

        private VisualElement CreateAbilityCard(
            SuperAttackData ability,
            DevelopmentCardState state)
        {
            return CreateCard(
                $"development-ability-card-{ability.Id}",
                Localize(ability.NameLocalizationKey),
                state,
                false,
                null,
                CharacterDevelopmentService.CanUnlockSuperAttack(ability.Id),
                () => OnUnlockAbilityClicked(ability.Id));
        }

        private static VisualElement CreateCard(
            string name,
            string title,
            DevelopmentCardState state,
            bool usesBeigeNode,
            Sprite iconSprite,
            bool canUnlock,
            Action unlockAction)
        {
            var card = new VisualElement { name = name };
            card.AddToClassList("development-card");
            card.EnableInClassList(
                "development-card--unlocked",
                state == DevelopmentCardState.Unlocked);
            card.EnableInClassList(
                "development-card--preview",
                state == DevelopmentCardState.Preview);
            card.EnableInClassList(
                "development-card--hidden",
                state == DevelopmentCardState.Hidden);

            // Собираем круглый slot из подложки и независимых state-слоёв.
            var node = CreateNode(usesBeigeNode);
            if (state != DevelopmentCardState.Hidden)
            {
                var icon = new VisualElement
                {
                    pickingMode = PickingMode.Ignore
                };
                icon.AddToClassList("development-card__icon");
                if (iconSprite != null)
                {
                    icon.style.backgroundImage =
                        new StyleBackground(iconSprite);
                }
                node.Add(icon);
            }
            if (state == DevelopmentCardState.Hidden)
            {
                var lockIcon = new VisualElement
                {
                    pickingMode = PickingMode.Ignore
                };
                lockIcon.AddToClassList("development-card__lock");
                node.Add(lockIcon);
            }
            if (state == DevelopmentCardState.Unlocked)
            {
                var check = new VisualElement
                {
                    pickingMode = PickingMode.Ignore
                };
                check.AddToClassList("development-card__check");
                node.Add(check);
            }
            card.Add(node);

            // Название и unlock-действие остаются runtime UI.
            if (state != DevelopmentCardState.Hidden)
            {
                var nameLabel = new Label(title)
                {
                    pickingMode = PickingMode.Ignore
                };
                nameLabel.AddToClassList("development-card__name");
                card.Add(nameLabel);
            }
            if (state == DevelopmentCardState.Preview && canUnlock)
            {
                var unlockButton = new Button(unlockAction);
                unlockButton.AddToClassList("development-card__unlock");

                var actionLabel = new Label(
                    Localize("development_unlock_action"))
                {
                    pickingMode = PickingMode.Ignore
                };
                actionLabel.AddToClassList(
                    "development-card__unlock-action");
                unlockButton.Add(actionLabel);

                var costRow = new VisualElement
                {
                    pickingMode = PickingMode.Ignore
                };
                costRow.AddToClassList("development-card__unlock-cost-row");

                var costLabel = new Label(
                    Localize("development_unlock_cost"))
                {
                    pickingMode = PickingMode.Ignore
                };
                costLabel.AddToClassList("development-card__unlock-cost");
                costRow.Add(costLabel);

                var star = new Label("★")
                {
                    pickingMode = PickingMode.Ignore
                };
                star.AddToClassList("development-card__unlock-star");
                costRow.Add(star);
                unlockButton.Add(costRow);

                card.Add(unlockButton);
            }

            return card;
        }

        private static VisualElement CreateNode(bool usesBeigeNode)
        {
            var node = new VisualElement { pickingMode = PickingMode.Ignore };
            node.AddToClassList("development-card__node");
            node.AddToClassList(
                usesBeigeNode
                    ? "development-card__node--beige"
                    : "development-card__node--gray-blue");
            return node;
        }

        private CancellationToken BeginAbilityIconLoading()
        {
            _abilityIconCancellation = new CancellationTokenSource();
            return _abilityIconCancellation.Token;
        }

        private async Task LoadAbilityIconAsync(
            string iconAddress,
            VisualElement icon,
            bool useGrayscale,
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

                _abilityIconLeases.Add(lease);
                Sprite iconSprite = useGrayscale
                    ? CreateGrayscaleSprite(lease.Value)
                    : lease.Value;
                icon.style.backgroundImage = new StyleBackground(iconSprite);
            }
            catch (OperationCanceledException)
            {
                lease?.Dispose();
            }
            catch (Exception exception)
            {
                lease?.Dispose();
                Debug.LogError(
                    $"Could not load development ability icon " +
                    $"'{iconAddress}': {exception.Message}");
            }
        }

        private Sprite CreateGrayscaleSprite(Sprite source)
        {
            if (source == null)
            {
                return null;
            }

            // Копируем GPU-текстуру в читаемый runtime-фрагмент спрайта.
            RenderTexture renderTexture = RenderTexture.GetTemporary(
                source.texture.width,
                source.texture.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            RenderTexture previousRenderTexture = RenderTexture.active;
            Texture2D grayscaleTexture = null;
            try
            {
                Graphics.Blit(source.texture, renderTexture);
                RenderTexture.active = renderTexture;

                int width = Mathf.Max(1, Mathf.RoundToInt(source.rect.width));
                int height = Mathf.Max(
                    1,
                    Mathf.RoundToInt(source.rect.height));
                grayscaleTexture = new Texture2D(
                    width,
                    height,
                    TextureFormat.RGBA32,
                    false)
                {
                    name = $"{source.name}_grayscale_runtime",
                    filterMode = source.texture.filterMode,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.DontSave
                };
                grayscaleTexture.ReadPixels(source.rect, 0, 0, false);

                // Убираем цвет, сохраняя исходную яркость и alpha.
                Color32[] pixels = grayscaleTexture.GetPixels32();
                for (int index = 0; index < pixels.Length; index++)
                {
                    Color32 pixel = pixels[index];
                    byte luminance = (byte)(
                        (77 * pixel.r + 150 * pixel.g + 29 * pixel.b) >> 8);
                    pixels[index] = new Color32(
                        luminance,
                        luminance,
                        luminance,
                        pixel.a);
                }
                grayscaleTexture.SetPixels32(pixels);
                grayscaleTexture.Apply(false, true);

                Vector2 pivot = new(
                    source.pivot.x / source.rect.width,
                    source.pivot.y / source.rect.height);
                Sprite grayscaleSprite = Sprite.Create(
                    grayscaleTexture,
                    new Rect(0f, 0f, width, height),
                    pivot,
                    source.pixelsPerUnit);
                grayscaleSprite.name = grayscaleTexture.name;
                grayscaleSprite.hideFlags = HideFlags.DontSave;

                _generatedPreviewTextures.Add(grayscaleTexture);
                _generatedPreviewSprites.Add(grayscaleSprite);
                return grayscaleSprite;
            }
            catch (Exception exception)
            {
                if (grayscaleTexture != null)
                {
                    UnityEngine.Object.Destroy(grayscaleTexture);
                }

                Debug.LogWarning(
                    $"Could not create grayscale development icon " +
                    $"'{source.name}': {exception.Message}");
                return source;
            }
            finally
            {
                RenderTexture.active = previousRenderTexture;
                RenderTexture.ReleaseTemporary(renderTexture);
            }
        }

        private void ReleaseAbilityIcons()
        {
            _abilityIconCancellation?.Cancel();
            _abilityIconCancellation?.Dispose();
            _abilityIconCancellation = null;

            foreach (AddressableLease<Sprite> lease in _abilityIconLeases)
            {
                lease.Dispose();
            }

            _abilityIconLeases.Clear();
        }

        private void ReleaseGeneratedPreviewIcons()
        {
            foreach (Sprite sprite in _generatedPreviewSprites)
            {
                UnityEngine.Object.Destroy(sprite);
            }
            _generatedPreviewSprites.Clear();

            foreach (Texture2D texture in _generatedPreviewTextures)
            {
                UnityEngine.Object.Destroy(texture);
            }
            _generatedPreviewTextures.Clear();
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

        private async void RefreshAfterUnlock()
        {
            UpdatePlayerProgress();
            await RefreshCardsAsync();
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
    }
}
