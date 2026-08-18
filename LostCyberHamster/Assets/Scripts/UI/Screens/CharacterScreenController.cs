using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Assets.Scripts.System.Resources;
using GameManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Vues.GameCore;

namespace LostCyberHamster.UI
{
    /// <summary>
    /// Управляет единым экраном экипировки skins и super abilities.
    /// </summary>
    public sealed class CharacterScreenController : ScreenController
    {
        private const int MinimumCardsPerTab = 8;

        private enum EquipmentTab
        {
            Skins,
            Abilities
        }

        private readonly List<AddressableLease<Sprite>> _abilityIconLeases =
            new();
        private readonly Dictionary<int, Sprite> _abilityIcons = new();
        private CancellationTokenSource _abilityIconCancellation;
        private AddressableLease<Texture2D> _priceIconLease;

        private EquipmentTab _activeTab;
        private int _selectedSkinId;
        private int _selectedAbilityId;
        private int _tabRefreshVersion;
        private bool _screenLoaded;
        private Button SettingsButton =>
            _contentRoot.Q<Button>("btn_settings");
        private Button HomeButton =>
            _contentRoot.Q<Button>("btn_home");
        private Button SkinTabButton =>
            _contentRoot.Q<Button>("equipment-tab-skins");
        private Button AbilityTabButton =>
            _contentRoot.Q<Button>("equipment-tab-abilities");
        private Button SkinActionButton =>
            _contentRoot.Q<Button>("skin-btn-change");
        private VisualElement Tabs =>
            _contentRoot.Q<VisualElement>("equipment-tabs");
        private VisualElement PagesTrack =>
            _contentRoot.Q<VisualElement>("equipment-pages-track");
        private VisualElement SkinsPage =>
            _contentRoot.Q<VisualElement>("equipment-skins-page");
        private VisualElement AbilitiesPage =>
            _contentRoot.Q<VisualElement>("equipment-abilities-page");
        private Button AddMoneyButton =>
            _contentRoot.Q<MoneyStorageUI>()?.ButtonAdd;
        private Button AddCrystalsButton =>
            _contentRoot.Q<CrystalStorageUI>()?.ButtonAdd;
        private Label SkinPreviewName =>
            _contentRoot.Q<Label>("equipment-skin-name");
        private Label SkinPreviewStatus =>
            _contentRoot.Q<Label>("equipment-skin-status");
        private VisualElement SkinPreviewImage =>
            _contentRoot.Q<VisualElement>("equipment-skin-image");
        private VisualElement SkinCards =>
            _contentRoot.Q<VisualElement>("equipment-skin-cards");
        private Label AbilityPreviewName =>
            _contentRoot.Q<Label>("equipment-ability-name");
        private Label AbilityPreviewDescription =>
            _contentRoot.Q<Label>("equipment-ability-description");
        private Label AbilityPreviewStatus =>
            _contentRoot.Q<Label>("equipment-ability-status");
        private VisualElement AbilityPreviewImage =>
            _contentRoot.Q<VisualElement>("equipment-ability-image");
        private VisualElement AbilityCards =>
            _contentRoot.Q<VisualElement>("equipment-ability-cards");

        protected override ScreenEnum _screenAssetName =>
            ScreenEnum.CharacterScreen;

        public CharacterScreenController(UIDocument uiDocument)
            : base(uiDocument)
        {
        }

        protected override async Task OnLoadAsync()
        {
            await ChangeBackgroundAsync("BackgroundScreenSprite");
            _screenLoaded = true;
            _activeTab = EquipmentTab.Skins;
            _selectedSkinId = SkinManager.CurrentSkin?.Id ??
                              CharacterDevelopmentService.DefaultSkinId;
            _selectedAbilityId =
                SuperAttackService.ActiveSuperAttackId ??
                GetFirstUnlockedAbilityId();
            await LoadPagesAsync();
        }

        protected override void OnSubscribeToEvents()
        {
            _screenLoaded = true;
            SettingsButton?.RegisterCallback<ClickEvent>(OnSettingsClicked);
            HomeButton?.RegisterCallback<ClickEvent>(OnHomeClicked);
            SkinTabButton?.RegisterCallback<ClickEvent>(OnSkinTabClicked);
            AbilityTabButton?.RegisterCallback<ClickEvent>(
                OnAbilityTabClicked);
            SkinActionButton?.RegisterCallback<ClickEvent>(OnActionClicked);
            AddMoneyButton?.RegisterCallback<ClickEvent>(OnAddResourceClicked);
            AddCrystalsButton?.RegisterCallback<ClickEvent>(
                OnAddResourceClicked);
        }

        protected override void OnUnsubscribeFromEvents()
        {
            _screenLoaded = false;
            _tabRefreshVersion++;
            SettingsButton?.UnregisterCallback<ClickEvent>(OnSettingsClicked);
            HomeButton?.UnregisterCallback<ClickEvent>(OnHomeClicked);
            SkinTabButton?.UnregisterCallback<ClickEvent>(OnSkinTabClicked);
            AbilityTabButton?.UnregisterCallback<ClickEvent>(
                OnAbilityTabClicked);
            SkinActionButton?.UnregisterCallback<ClickEvent>(OnActionClicked);
            AddMoneyButton?.UnregisterCallback<ClickEvent>(
                OnAddResourceClicked);
            AddCrystalsButton?.UnregisterCallback<ClickEvent>(
                OnAddResourceClicked);
            ReleaseAbilityIcons();
            ReleasePriceIcon();
        }

        private async Task LoadPagesAsync()
        {
            int refreshVersion = ++_tabRefreshVersion;

            // Выставляем начальное положение вкладок и ленты.
            ApplyActiveTabState();

            // Заполняем обе страницы до первого переключения.
            BuildSkinCards();
            Task skinPreviewTask = ShowSelectedSkinAsync(refreshVersion);
            Task abilityCardsTask = BuildAbilityCardsAsync();
            await Task.WhenAll(skinPreviewTask, abilityCardsTask);
            if (IsCurrentRefresh(refreshVersion))
            {
                ShowSelectedAbility();
            }
        }

        private void ApplyActiveTabState()
        {
            bool showSkins = _activeTab == EquipmentTab.Skins;

            // Синхронно анимируем вкладки и горизонтальную ленту страниц.
            SkinTabButton.EnableInClassList("equipment-tab--active", showSkins);
            AbilityTabButton.EnableInClassList(
                "equipment-tab--active",
                !showSkins);
            Tabs.EnableInClassList("equipment__tabs--skins", showSkins);
            Tabs.EnableInClassList("equipment__tabs--abilities", !showSkins);
            PagesTrack.EnableInClassList(
                "equipment__pages-track--abilities",
                !showSkins);
            SkinsPage.pickingMode = showSkins
                ? PickingMode.Position
                : PickingMode.Ignore;
            AbilitiesPage.pickingMode = showSkins
                ? PickingMode.Ignore
                : PickingMode.Position;
        }

        private void BuildSkinCards()
        {
            SkinCards.Clear();
            int skinCount = 0;
            foreach (Skin skin in SkinManager.AvailableSkins.OrderBy(
                         skin => !CharacterDevelopmentService.IsSkinUnlocked(
                             skin.Id)))
            {
                bool isLocked =
                    !CharacterDevelopmentService.IsSkinUnlocked(skin.Id);
                var card = CreateCard(
                    $"skin-card-{skin.Id}",
                    skin.Name,
                    skin.HamsterSprite,
                    skin.Id == _selectedSkinId,
                    isLocked);
                if (!isLocked)
                {
                    card.RegisterCallback<ClickEvent>(
                        _ => OnSkinSelected(skin.Id));
                }

                SkinCards.Add(card);
                skinCount++;
            }

            AddLockedPlaceholders(SkinCards, "skin", skinCount);
        }

        private async Task BuildAbilityCardsAsync()
        {
            ReleaseAbilityIcons();
            AbilityCards.Clear();
            CancellationToken cancellationToken = BeginAbilityIconLoading();
            var loadTasks = new List<Task>();

            int abilityCount = 0;
            foreach (SuperAttackData ability in
                     SuperAttackService.Items.OrderBy(
                         ability => !SuperAttackService.IsUnlocked(
                             ability.Id)))
            {
                bool isLocked = !SuperAttackService.IsUnlocked(ability.Id);
                var card = CreateCard(
                    $"ability-card-{ability.Id}",
                    Localize(ability.NameLocalizationKey),
                    null,
                    ability.Id == _selectedAbilityId,
                    isLocked,
                    out VisualElement icon);
                if (!isLocked)
                {
                    card.RegisterCallback<ClickEvent>(
                        _ => OnAbilitySelected(ability.Id));
                }

                AbilityCards.Add(card);
                if (icon != null)
                {
                    loadTasks.Add(LoadAbilityIconAsync(
                        ability.Id,
                        ability.IconAddress,
                        icon,
                        cancellationToken));
                }

                abilityCount++;
            }
            AddLockedPlaceholders(AbilityCards, "ability", abilityCount);

            await Task.WhenAll(loadTasks);
        }

        private async Task ShowSelectedSkinAsync(int refreshVersion)
        {
            Skin skin = SkinManager.AvailableSkins.FirstOrDefault(
                candidate => candidate.Id == _selectedSkinId &&
                             CharacterDevelopmentService.IsSkinUnlocked(
                                 candidate.Id));
            skin ??= SkinManager.DefaultSkin;
            if (skin == null)
            {
                ShowEmptyPreview(
                    SkinPreviewName,
                    null,
                    SkinPreviewStatus,
                    SkinPreviewImage,
                    SkinActionButton,
                    "equipment_no_skins");
                return;
            }

            _selectedSkinId = skin.Id;
            ReleasePriceIcon();
            SkinPreviewName.text = skin.Name;
            SkinPreviewImage.style.backgroundImage =
                new StyleBackground(skin.HamsterSprite);
            SkinPreviewStatus.style.display = DisplayStyle.None;
            SkinActionButton.style.display = DisplayStyle.Flex;

            // Показываем одну action для текущей стадии skin flow.
            if (SkinManager.CurrentSkin?.Id == skin.Id)
            {
                SkinPreviewStatus.text = Localize("equipment_equipped");
                SkinPreviewStatus.style.display = DisplayStyle.Flex;
                SkinActionButton.style.display = DisplayStyle.None;
                return;
            }

            if (skin.IsPurchased)
            {
                SkinPreviewStatus.text = string.Empty;
                SkinActionButton.text = Localize("equipment_equip");
                SkinActionButton.SetEnabled(true);
                return;
            }

            SkinPreviewStatus.text = string.Empty;
            SkinActionButton.text =
                $"{Localize("equipment_buy")} {skin.Price}";
            SkinActionButton.SetEnabled(SkinManager.CanPurchaseSkin(skin.Id));
            await LoadPriceIconAsync(
                skin.PriceType,
                skin.Id,
                refreshVersion);
        }

        private void ShowSelectedAbility()
        {
            SuperAttackData ability = SuperAttackService.Items.FirstOrDefault(
                candidate => candidate.Id == _selectedAbilityId &&
                             SuperAttackService.IsUnlocked(candidate.Id));
            if (ability == null)
            {
                ShowEmptyPreview(
                    AbilityPreviewName,
                    AbilityPreviewDescription,
                    AbilityPreviewStatus,
                    AbilityPreviewImage,
                    null,
                    "equipment_no_abilities");
                return;
            }

            AbilityPreviewName.text = Localize(ability.NameLocalizationKey);
            AbilityPreviewDescription.text = Localize(
                ability.DescriptionLocalizationKey);
            AbilityPreviewStatus.text = string.Empty;
            AbilityPreviewStatus.style.display = DisplayStyle.None;
            if (_abilityIcons.TryGetValue(ability.Id, out Sprite icon))
            {
                AbilityPreviewImage.style.backgroundImage =
                    new StyleBackground(icon);
            }
            else
            {
                AbilityPreviewImage.style.backgroundImage = null;
            }
        }

        private static void ShowEmptyPreview(
            Label name,
            Label description,
            Label status,
            VisualElement image,
            Button action,
            string localizationKey)
        {
            name.text = Localize(localizationKey);
            if (description != null)
            {
                description.text = string.Empty;
            }

            status.text = string.Empty;
            status.style.display = DisplayStyle.None;
            image.style.backgroundImage = null;
            if (action != null)
            {
                action.style.display = DisplayStyle.None;
            }
        }

        private static Button CreateCard(
            string name,
            string title,
            Sprite sprite,
            bool isSelected,
            bool isLocked)
        {
            return CreateCard(
                name,
                title,
                sprite,
                isSelected,
                isLocked,
                out _);
        }

        private static Button CreateCard(
            string name,
            string title,
            Sprite sprite,
            bool isSelected,
            bool isLocked,
            out VisualElement icon)
        {
            var card = new Button { name = name };
            card.AddToClassList("equipment-card");
            card.EnableInClassList(
                "equipment-card--selected",
                isSelected && !isLocked);
            card.EnableInClassList("equipment-card--locked", isLocked);

            if (isLocked)
            {
                icon = null;
                card.Add(CreateLock("equipment-card__lock"));
                card.SetEnabled(false);
                return card;
            }

            icon = new VisualElement();
            icon.AddToClassList("equipment-card__icon");
            if (sprite != null)
            {
                icon.style.backgroundImage = new StyleBackground(sprite);
            }
            card.Add(icon);

            var label = new Label(title);
            label.AddToClassList("equipment-card__name");
            card.Add(label);
            return card;
        }

        private void AddLockedPlaceholders(
            VisualElement container,
            string catalogName,
            int existingCount)
        {
            for (int index = existingCount;
                 index < MinimumCardsPerTab;
                 index++)
            {
                var card = new Button
                {
                    name = $"equipment-{catalogName}-placeholder-{index}"
                };
                card.AddToClassList("equipment-card");
                card.AddToClassList("equipment-card--locked");
                card.AddToClassList("equipment-card--placeholder");
                card.Add(CreateLock("equipment-card__lock"));
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

        private async void OnSkinSelected(int skinId)
        {
            _selectedSkinId = skinId;
            int refreshVersion = ++_tabRefreshVersion;
            BuildSkinCards();
            await ShowSelectedSkinAsync(refreshVersion);
        }

        private void OnAbilitySelected(int abilityId)
        {
            _selectedAbilityId = abilityId;
            if (SuperAttackService.ActiveSuperAttackId == abilityId)
            {
                ShowSelectedAbility();
                UpdateSelectedCardClasses(
                    AbilityCards,
                    $"ability-card-{abilityId}");
                return;
            }

            if (SuperAttackService.TrySelect(abilityId))
            {
                ShowSelectedAbility();
                UpdateSelectedCardClasses(
                    AbilityCards,
                    $"ability-card-{abilityId}");
            }
        }

        private async void OnActionClicked(ClickEvent clickEvent)
        {
            if (_activeTab != EquipmentTab.Skins)
            {
                return;
            }

            Skin skin = SkinManager.AvailableSkins.FirstOrDefault(
                candidate => candidate.Id == _selectedSkinId);
            if (skin == null)
            {
                return;
            }

            if (skin.IsPurchased)
            {
                SkinManager.PutOnSkin(skin.Id);
            }
            else
            {
                SkinManager.PurchaseSkin(skin.Id);
            }

            int refreshVersion = ++_tabRefreshVersion;
            BuildSkinCards();
            await ShowSelectedSkinAsync(refreshVersion);
        }

        private void OnSkinTabClicked(ClickEvent clickEvent)
        {
            if (_activeTab == EquipmentTab.Skins)
            {
                return;
            }

            _activeTab = EquipmentTab.Skins;
            ApplyActiveTabState();
        }

        private void OnAbilityTabClicked(ClickEvent clickEvent)
        {
            if (_activeTab == EquipmentTab.Abilities)
            {
                return;
            }

            _activeTab = EquipmentTab.Abilities;
            ApplyActiveTabState();
        }

        private static void UpdateSelectedCardClasses(
            VisualElement container,
            string selectedName)
        {
            foreach (VisualElement child in container.Children())
            {
                child.EnableInClassList(
                    "equipment-card--selected",
                    child.name == selectedName);
            }
        }

        private async Task LoadAbilityIconAsync(
            int abilityId,
            string iconAddress,
            VisualElement cardIcon,
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
                _abilityIcons[abilityId] = lease.Value;
                cardIcon.style.backgroundImage =
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
                    $"Could not load equipment ability icon " +
                    $"'{iconAddress}': {exception.Message}");
            }
        }

        private async Task LoadPriceIconAsync(
            ResourceType priceType,
            int skinId,
            int refreshVersion)
        {
            ReleasePriceIcon();
            string address = priceType switch
            {
                ResourceType.Crystals => "crystal",
                ResourceType.Coins => "coin",
                _ => string.Empty
            };
            if (string.IsNullOrEmpty(address))
            {
                return;
            }

            AddressableLease<Texture2D> lease = null;
            try
            {
                lease = await AddressableLoader.LoadAssetAsync<Texture2D>(
                    address);
                if (!IsCurrentRefresh(refreshVersion) ||
                    _selectedSkinId != skinId)
                {
                    lease.Dispose();
                    return;
                }

                _priceIconLease = lease;
                if (lease.Value != null)
                {
                    SkinActionButton.iconImage = Background.FromTexture2D(
                        lease.Value);
                }
            }
            catch (Exception exception)
            {
                lease?.Dispose();
                Debug.LogError(
                    $"Could not load equipment price icon " +
                    $"'{address}': {exception.Message}");
            }
        }

        private bool IsCurrentRefresh(int refreshVersion)
        {
            return _screenLoaded &&
                   refreshVersion == _tabRefreshVersion;
        }

        private CancellationToken BeginAbilityIconLoading()
        {
            _abilityIconCancellation = new CancellationTokenSource();
            return _abilityIconCancellation.Token;
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
            _abilityIcons.Clear();
        }

        private void ReleasePriceIcon()
        {
            _priceIconLease?.Dispose();
            _priceIconLease = null;
            if (SkinActionButton != null)
            {
                SkinActionButton.iconImage = null;
            }
        }

        private static int GetFirstUnlockedAbilityId()
        {
            return SuperAttackService.Items.FirstOrDefault(
                ability => SuperAttackService.IsUnlocked(ability.Id))?.Id ?? 0;
        }

        private void OnHomeClicked(ClickEvent clickEvent)
        {
            UIManager.OnScreenShow(ScreenEnum.HomeScreen);
        }

        private void OnSettingsClicked(ClickEvent clickEvent)
        {
            SettingsScreenController.OpenFrom(ScreenEnum.CharacterScreen);
        }

        private void OnAddResourceClicked(ClickEvent clickEvent)
        {
            UIManager.OnModalShow(ScreenEnum.ShopModal);
        }

        private static string Localize(string key)
        {
            string value = LocalizationManager.GetLocalizedString(key);
            return string.IsNullOrWhiteSpace(value) ? key : value;
        }
    }
}
