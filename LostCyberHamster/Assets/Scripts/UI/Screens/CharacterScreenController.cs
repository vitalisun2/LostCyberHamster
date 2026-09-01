using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Assets.Scripts.System.Resources;
using UnityEngine;
using UnityEngine.UIElements;
using Vues.GameCore;

namespace LostCyberHamster.UI
{
    /// <summary>
    /// Управляет единым Hero-экраном скинов и суперспособностей.
    /// </summary>
    public sealed class CharacterScreenController : ScreenController
    {
        private const float DesignWidth = 1672f;
        private const float DesignHeight = 941f;
        private const float ScrollEpsilon = 0.5f;
        private const string BackgroundAddress = "HeroBackgroundSprite";

        private enum HeroTab
        {
            Skins,
            Abilities
        }

        private readonly List<AddressableLease<Sprite>> _abilityLeases =
            new();
        private readonly Dictionary<int, Sprite> _abilityIcons = new();
        private readonly Dictionary<int, Sprite> _abilityPreviews = new();

        private CancellationTokenSource _visualCancellation;
        private AddressableLease<Texture2D> _priceIconLease;
        private Texture2D _priceIcon;
        private HeroTab _activeTab;
        private int _selectedSkinId;
        private int _selectedAbilityId;
        private int _visualVersion;
        private bool _screenLoaded;

        private VisualElement Viewport =>
            _contentRoot.Q<VisualElement>("hero-viewport");
        private VisualElement ScaleFrame =>
            _contentRoot.Q<VisualElement>("hero-scale-frame");
        private VisualElement Design =>
            _contentRoot.Q<VisualElement>("hero-design");
        private Button SkinTabButton =>
            _contentRoot.Q<Button>("hero-tab-skins");
        private Button AbilityTabButton =>
            _contentRoot.Q<Button>("hero-tab-abilities");
        private VisualElement SkinsPage =>
            _contentRoot.Q<VisualElement>("hero-skins-page");
        private VisualElement AbilitiesPage =>
            _contentRoot.Q<VisualElement>("hero-abilities-page");
        private VisualElement SkinPreviewImage =>
            _contentRoot.Q<VisualElement>("hero-skin-preview-image");
        private Label SkinPreviewName =>
            _contentRoot.Q<Label>("hero-skin-preview-name");
        private VisualElement SkinSlots =>
            _contentRoot.Q<VisualElement>("hero-skin-slots");
        private ScrollView SkinScroll =>
            _contentRoot.Q<ScrollView>("hero-skin-scroll");
        private Button SkinScrollUpButton =>
            _contentRoot.Q<Button>("hero-skin-scroll-up");
        private Button SkinScrollDownButton =>
            _contentRoot.Q<Button>("hero-skin-scroll-down");
        private VisualElement AbilityPreviewImage =>
            _contentRoot.Q<VisualElement>("hero-ability-preview-image");
        private Label AbilityPreviewDescription =>
            _contentRoot.Q<Label>("hero-ability-preview-description");
        private Button AbilitySelectButton =>
            _contentRoot.Q<Button>("hero-ability-select");
        private VisualElement AbilitySlots =>
            _contentRoot.Q<VisualElement>("hero-ability-slots");
        private ScrollView AbilityScroll =>
            _contentRoot.Q<ScrollView>("hero-ability-scroll");
        private Button AbilityScrollUpButton =>
            _contentRoot.Q<Button>("hero-ability-scroll-up");
        private Button AbilityScrollDownButton =>
            _contentRoot.Q<Button>("hero-ability-scroll-down");

        protected override ScreenEnum _screenAssetName =>
            ScreenEnum.CharacterScreen;

        public CharacterScreenController(UIDocument uiDocument)
            : base(uiDocument)
        {
        }

        protected override async Task OnLoadAsync()
        {
            _screenLoaded = true;
            await ChangeBackgroundAsync(
                BackgroundAddress,
                ScaleMode.ScaleAndCrop);
            _activeTab = HeroTab.Skins;
            _selectedSkinId = SkinManager.CurrentSkin?.Id ??
                              CharacterDevelopmentService.DefaultSkinId;
            _selectedAbilityId = SuperAttackService.ActiveSuperAttackId ??
                                 GetFirstUnlockedAbilityId();

            ApplyActiveTab();
            BuildSkinSlots();
            ShowSelectedSkin();
            BuildAbilitySlots();
            ShowSelectedAbility();
            await LoadVisualsAsync();
        }

        protected override void OnSubscribeToEvents()
        {
            SkinTabButton?.RegisterCallback<ClickEvent>(OnSkinTabClicked);
            AbilityTabButton?.RegisterCallback<ClickEvent>(
                OnAbilityTabClicked);
            AbilitySelectButton?.RegisterCallback<ClickEvent>(
                OnAbilitySelectClicked);
            SkinScrollUpButton?.RegisterCallback<ClickEvent>(
                OnSkinScrollUpClicked);
            SkinScrollDownButton?.RegisterCallback<ClickEvent>(
                OnSkinScrollDownClicked);
            AbilityScrollUpButton?.RegisterCallback<ClickEvent>(
                OnAbilityScrollUpClicked);
            AbilityScrollDownButton?.RegisterCallback<ClickEvent>(
                OnAbilityScrollDownClicked);
            Viewport?.RegisterCallback<GeometryChangedEvent>(
                OnViewportGeometryChanged);
            RegisterScrollCallbacks(SkinScroll, OnSkinScrollValueChanged);
            RegisterScrollCallbacks(
                AbilityScroll,
                OnAbilityScrollValueChanged);
            Viewport?.schedule.Execute(
                () => ApplyResponsiveLayout(Viewport.contentRect.size));
            Viewport?.schedule.Execute(UpdateScrollNavigation);
        }

        protected override void OnUnsubscribeFromEvents()
        {
            _screenLoaded = false;
            _visualVersion++;
            SkinTabButton?.UnregisterCallback<ClickEvent>(OnSkinTabClicked);
            AbilityTabButton?.UnregisterCallback<ClickEvent>(
                OnAbilityTabClicked);
            AbilitySelectButton?.UnregisterCallback<ClickEvent>(
                OnAbilitySelectClicked);
            SkinScrollUpButton?.UnregisterCallback<ClickEvent>(
                OnSkinScrollUpClicked);
            SkinScrollDownButton?.UnregisterCallback<ClickEvent>(
                OnSkinScrollDownClicked);
            AbilityScrollUpButton?.UnregisterCallback<ClickEvent>(
                OnAbilityScrollUpClicked);
            AbilityScrollDownButton?.UnregisterCallback<ClickEvent>(
                OnAbilityScrollDownClicked);
            Viewport?.UnregisterCallback<GeometryChangedEvent>(
                OnViewportGeometryChanged);
            UnregisterScrollCallbacks(SkinScroll, OnSkinScrollValueChanged);
            UnregisterScrollCallbacks(
                AbilityScroll,
                OnAbilityScrollValueChanged);
            ReleaseVisuals();
        }

        private async Task LoadVisualsAsync()
        {
            ReleaseVisuals();
            int visualVersion = ++_visualVersion;
            _visualCancellation = new CancellationTokenSource();
            CancellationToken cancellationToken = _visualCancellation.Token;

            var tasks = new List<Task>();
            foreach (SuperAttackData ability in SuperAttackService.Items)
            {
                tasks.Add(LoadAbilityVisualsAsync(
                    ability,
                    cancellationToken));
            }

            if (SkinManager.AvailableSkins.Any(
                    skin =>
                        CharacterDevelopmentService.IsSkinUnlocked(skin.Id) &&
                        !skin.IsPurchased))
            {
                tasks.Add(LoadPriceIconAsync(cancellationToken));
            }

            await Task.WhenAll(tasks);
            if (!IsCurrentVisual(visualVersion))
            {
                return;
            }

            BuildSkinSlots();
            BuildAbilitySlots();
            ShowSelectedAbility();
        }

        private async Task LoadAbilityVisualsAsync(
            SuperAttackData ability,
            CancellationToken cancellationToken)
        {
            Sprite icon = await LoadAbilitySpriteAsync(
                ability.IconAddress,
                cancellationToken);
            if (icon != null)
            {
                _abilityIcons[ability.Id] = icon;
            }

            string previewAddress = string.IsNullOrWhiteSpace(
                ability.EquipmentPreviewAddress)
                ? ability.IconAddress
                : ability.EquipmentPreviewAddress;
            if (string.Equals(
                    previewAddress,
                    ability.IconAddress,
                    StringComparison.Ordinal))
            {
                if (icon != null)
                {
                    _abilityPreviews[ability.Id] = icon;
                }

                return;
            }

            Sprite preview = await LoadAbilitySpriteAsync(
                previewAddress,
                cancellationToken);
            if (preview != null)
            {
                _abilityPreviews[ability.Id] = preview;
            }
        }

        private async Task<Sprite> LoadAbilitySpriteAsync(
            string address,
            CancellationToken cancellationToken)
        {
            AddressableLease<Sprite> lease = null;
            try
            {
                lease = await AddressableLoader.LoadAssetAsync<Sprite>(
                    address,
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (lease.Value == null)
                {
                    lease.Dispose();
                    return null;
                }

                _abilityLeases.Add(lease);
                return lease.Value;
            }
            catch (OperationCanceledException)
            {
                lease?.Dispose();
                return null;
            }
            catch (Exception exception)
            {
                lease?.Dispose();
                Debug.LogError(
                    $"Could not load Hero sprite '{address}': " +
                    exception.Message);
                return null;
            }
        }

        private async Task LoadPriceIconAsync(
            CancellationToken cancellationToken)
        {
            AddressableLease<Texture2D> lease = null;
            try
            {
                lease = await AddressableLoader.LoadAssetAsync<Texture2D>(
                    "crystal",
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (lease.Value == null)
                {
                    lease.Dispose();
                    return;
                }

                _priceIconLease = lease;
                _priceIcon = lease.Value;
            }
            catch (OperationCanceledException)
            {
                lease?.Dispose();
            }
            catch (Exception exception)
            {
                lease?.Dispose();
                Debug.LogError(
                    "Could not load Hero price icon 'crystal': " +
                    exception.Message);
            }
        }

        private void BuildSkinSlots()
        {
            SkinSlots.Clear();
            IEnumerable<Skin> orderedSkins = SkinManager.AvailableSkins
                .OrderBy(skin => CharacterDevelopmentService
                    .IsSkinUnlocked(skin.Id)
                    ? 0
                    : 1);
            foreach (Skin skin in orderedSkins)
            {
                SkinSlots.Add(CreateSkinSlot(skin));
            }

            SkinScroll?.schedule.Execute(UpdateScrollNavigation);
        }

        private VisualElement CreateSkinSlot(Skin skin)
        {
            bool isUnlocked =
                CharacterDevelopmentService.IsSkinUnlocked(skin.Id);
            var slot = CreateSlot(
                $"hero-skin-slot-{skin.Id}",
                skin.Id == _selectedSkinId);
            if (!isUnlocked)
            {
                slot.Add(CreateStateElement("hero-slot__lock"));
                return slot;
            }

            slot.Add(CreateIcon(skin.HamsterSprite));
            slot.Add(CreateNameLabel(skin.Name));
            slot.RegisterCallback<ClickEvent>(_ => SelectSkin(skin.Id));

            if (SkinManager.CurrentSkin?.Id == skin.Id)
            {
                slot.Add(CreateStateElement("hero-slot__check"));
                return slot;
            }

            Button action;
            if (skin.IsPurchased)
            {
                action = CreateSlotAction(
                    Localize("equipment_equip"),
                    false,
                    clickEvent =>
                    {
                        clickEvent.StopPropagation();
                        EquipSkin(skin.Id);
                    });
            }
            else
            {
                action = CreateSlotAction(
                    skin.Price.ToString(),
                    true,
                    clickEvent =>
                    {
                        clickEvent.StopPropagation();
                        PurchaseSkin(skin.Id);
                    });
                action.SetEnabled(SkinManager.CanPurchaseSkin(skin.Id));
            }

            slot.Add(action);
            return slot;
        }

        private void BuildAbilitySlots()
        {
            AbilitySlots.Clear();
            IEnumerable<SuperAttackData> orderedAbilities =
                SuperAttackService.Items.OrderBy(
                    ability => SuperAttackService.IsUnlocked(ability.Id)
                        ? 0
                        : 1);
            foreach (SuperAttackData ability in orderedAbilities)
            {
                AbilitySlots.Add(CreateAbilitySlot(ability));
            }

            AbilityScroll?.schedule.Execute(UpdateScrollNavigation);
        }

        private VisualElement CreateAbilitySlot(SuperAttackData ability)
        {
            bool isUnlocked = SuperAttackService.IsUnlocked(ability.Id);
            var slot = CreateSlot(
                $"hero-ability-slot-{ability.Id}",
                ability.Id == _selectedAbilityId);
            if (!isUnlocked)
            {
                slot.Add(CreateStateElement("hero-slot__lock"));
                return slot;
            }

            _abilityIcons.TryGetValue(ability.Id, out Sprite icon);
            slot.Add(CreateIcon(icon));
            slot.Add(CreateNameLabel(Localize(ability.NameLocalizationKey)));
            slot.RegisterCallback<ClickEvent>(
                _ => SelectAbility(ability.Id));

            if (SuperAttackService.ActiveSuperAttackId == ability.Id)
            {
                slot.Add(CreateStateElement("hero-slot__check"));
            }

            return slot;
        }

        private static VisualElement CreateSlot(string name, bool isSelected)
        {
            var slot = new VisualElement { name = name };
            slot.AddToClassList("hero-slot");
            slot.EnableInClassList("hero-slot--selected", isSelected);
            return slot;
        }

        private static VisualElement CreateIcon(Sprite sprite)
        {
            var icon = new VisualElement
            {
                pickingMode = PickingMode.Ignore
            };
            icon.AddToClassList("hero-slot__icon");
            if (sprite != null)
            {
                icon.style.backgroundImage = new StyleBackground(sprite);
            }

            return icon;
        }

        private static Label CreateNameLabel(string text)
        {
            var label = new Label(text)
            {
                pickingMode = PickingMode.Ignore
            };
            label.AddToClassList("hero-slot__name");
            return label;
        }

        private static VisualElement CreateStateElement(string className)
        {
            var element = new VisualElement
            {
                pickingMode = PickingMode.Ignore
            };
            element.AddToClassList(className);
            return element;
        }

        private Button CreateSlotAction(
            string text,
            bool isPrice,
            EventCallback<ClickEvent> action)
        {
            var button = new Button { text = text };
            button.AddToClassList("hero-slot__action");
            button.RegisterCallback<ClickEvent>(action);
            if (!isPrice)
            {
                return button;
            }

            button.AddToClassList("hero-slot__action--price");
            var icon = new VisualElement
            {
                pickingMode = PickingMode.Ignore
            };
            icon.AddToClassList("hero-slot__price-icon");
            if (_priceIcon != null)
            {
                icon.style.backgroundImage = new StyleBackground(_priceIcon);
            }

            button.Add(icon);
            return button;
        }

        private void SelectSkin(int skinId)
        {
            _selectedSkinId = skinId;
            BuildSkinSlots();
            ShowSelectedSkin();
        }

        private void PurchaseSkin(int skinId)
        {
            _selectedSkinId = skinId;
            SkinManager.PurchaseSkin(skinId);
            BuildSkinSlots();
            ShowSelectedSkin();
        }

        private void EquipSkin(int skinId)
        {
            _selectedSkinId = skinId;
            SkinManager.PutOnSkin(skinId);
            BuildSkinSlots();
            ShowSelectedSkin();
        }

        private void ShowSelectedSkin()
        {
            Skin skin = SkinManager.AvailableSkins.FirstOrDefault(
                candidate =>
                    candidate.Id == _selectedSkinId &&
                    CharacterDevelopmentService.IsSkinUnlocked(candidate.Id));
            skin ??= SkinManager.DefaultSkin;
            if (skin == null)
            {
                SkinPreviewImage.style.backgroundImage = null;
                SkinPreviewName.text = Localize("equipment_no_skins");
                return;
            }

            _selectedSkinId = skin.Id;
            SkinPreviewImage.style.backgroundImage =
                new StyleBackground(skin.HamsterSprite);
            SkinPreviewName.text = skin.Name;
        }

        private void SelectAbility(int abilityId)
        {
            _selectedAbilityId = abilityId;
            BuildAbilitySlots();
            ShowSelectedAbility();
        }

        private void ShowSelectedAbility()
        {
            SuperAttackData ability = SuperAttackService.Items.FirstOrDefault(
                candidate =>
                    candidate.Id == _selectedAbilityId &&
                    SuperAttackService.IsUnlocked(candidate.Id));
            if (ability == null)
            {
                AbilityPreviewImage.style.backgroundImage = null;
                AbilityPreviewDescription.text = Localize(
                    "equipment_no_abilities");
                AbilitySelectButton.style.display = DisplayStyle.None;
                return;
            }

            _selectedAbilityId = ability.Id;
            AbilityPreviewDescription.text = Localize(
                ability.DescriptionLocalizationKey);
            if (_abilityPreviews.TryGetValue(ability.Id, out Sprite preview))
            {
                AbilityPreviewImage.style.backgroundImage =
                    new StyleBackground(preview);
            }
            else
            {
                AbilityPreviewImage.style.backgroundImage = null;
            }

            bool isActive =
                SuperAttackService.ActiveSuperAttackId == ability.Id;
            AbilitySelectButton.text = Localize("equipment_equip");
            AbilitySelectButton.style.display = isActive
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        private void OnAbilitySelectClicked(ClickEvent clickEvent)
        {
            if (_activeTab != HeroTab.Abilities || _selectedAbilityId <= 0)
            {
                return;
            }

            if (SuperAttackService.TrySelect(_selectedAbilityId))
            {
                BuildAbilitySlots();
                ShowSelectedAbility();
            }
        }

        private void OnSkinTabClicked(ClickEvent clickEvent)
        {
            SetActiveTab(HeroTab.Skins);
        }

        private void OnAbilityTabClicked(ClickEvent clickEvent)
        {
            SetActiveTab(HeroTab.Abilities);
        }

        private void SetActiveTab(HeroTab tab)
        {
            if (_activeTab == tab)
            {
                return;
            }

            _activeTab = tab;
            ApplyActiveTab();
        }

        private void ApplyActiveTab()
        {
            bool showSkins = _activeTab == HeroTab.Skins;
            SkinTabButton.EnableInClassList("hero-tab--active", showSkins);
            AbilityTabButton.EnableInClassList(
                "hero-tab--active",
                !showSkins);
            SkinsPage.style.display = showSkins
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            AbilitiesPage.style.display = showSkins
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            UpdateScrollNavigation();
            ScrollView activeScroll = showSkins ? SkinScroll : AbilityScroll;
            activeScroll?.schedule.Execute(UpdateScrollNavigation);
        }

        private void RegisterScrollCallbacks(
            ScrollView scrollView,
            Action<float> valueChanged)
        {
            if (scrollView == null)
            {
                return;
            }

            scrollView.RegisterCallback<GeometryChangedEvent>(
                OnScrollGeometryChanged);
            scrollView.verticalScroller.valueChanged += valueChanged;
        }

        private void UnregisterScrollCallbacks(
            ScrollView scrollView,
            Action<float> valueChanged)
        {
            if (scrollView == null)
            {
                return;
            }

            scrollView.UnregisterCallback<GeometryChangedEvent>(
                OnScrollGeometryChanged);
            scrollView.verticalScroller.valueChanged -= valueChanged;
        }

        private void OnScrollGeometryChanged(
            GeometryChangedEvent _)
        {
            UpdateScrollNavigation();
        }

        private void OnSkinScrollValueChanged(float _)
        {
            UpdateScrollNavigation();
        }

        private void OnAbilityScrollValueChanged(float _)
        {
            UpdateScrollNavigation();
        }

        private void OnSkinScrollUpClicked(ClickEvent _)
        {
            ScrollSlots(SkinScroll, -1f);
        }

        private void OnSkinScrollDownClicked(ClickEvent _)
        {
            ScrollSlots(SkinScroll, 1f);
        }

        private void OnAbilityScrollUpClicked(ClickEvent _)
        {
            ScrollSlots(AbilityScroll, -1f);
        }

        private void OnAbilityScrollDownClicked(ClickEvent _)
        {
            ScrollSlots(AbilityScroll, 1f);
        }

        private void ScrollSlots(ScrollView scrollView, float direction)
        {
            if (scrollView == null)
            {
                return;
            }

            float maximum = Mathf.Max(
                0f,
                scrollView.verticalScroller.highValue);
            float scrollStep = ResolveSlotScrollStep(scrollView);
            if (scrollStep <= 0f)
            {
                return;
            }

            float nextOffset = Mathf.Clamp(
                scrollView.scrollOffset.y + direction * scrollStep,
                0f,
                maximum);
            scrollView.scrollOffset = new Vector2(
                scrollView.scrollOffset.x,
                nextOffset);
            UpdateScrollNavigation();
        }

        private static float ResolveSlotScrollStep(ScrollView scrollView)
        {
            VisualElement slots = scrollView.Q<VisualElement>(
                className: "hero-slots");
            if (slots == null || slots.childCount == 0)
            {
                return 0f;
            }

            VisualElement firstSlot = slots[0];
            float step = slots.childCount > 1
                ? slots[1].layout.y - firstSlot.layout.y
                : firstSlot.layout.height;
            return step > 0f && !float.IsNaN(step) && !float.IsInfinity(step)
                ? step
                : 0f;
        }

        private void UpdateScrollNavigation()
        {
            UpdateScrollNavigation(
                SkinScroll,
                SkinScrollUpButton,
                SkinScrollDownButton,
                _activeTab == HeroTab.Skins);
            UpdateScrollNavigation(
                AbilityScroll,
                AbilityScrollUpButton,
                AbilityScrollDownButton,
                _activeTab == HeroTab.Abilities);
        }

        private static void UpdateScrollNavigation(
            ScrollView scrollView,
            Button upButton,
            Button downButton,
            bool isActive)
        {
            if (scrollView == null || upButton == null || downButton == null)
            {
                return;
            }

            float maximum = Mathf.Max(
                0f,
                scrollView.verticalScroller.highValue);
            bool isVisible = isActive && maximum > ScrollEpsilon;
            DisplayStyle display = isVisible
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            upButton.style.display = display;
            downButton.style.display = display;
            if (!isVisible)
            {
                return;
            }

            float offset = Mathf.Clamp(
                scrollView.scrollOffset.y,
                0f,
                maximum);
            upButton.SetEnabled(offset > ScrollEpsilon);
            downButton.SetEnabled(offset < maximum - ScrollEpsilon);
        }

        private void OnViewportGeometryChanged(GeometryChangedEvent evt)
        {
            ApplyResponsiveLayout(evt.newRect.size);
        }

        private void ApplyResponsiveLayout(Vector2 viewportSize)
        {
            float width = Mathf.Max(1f, viewportSize.x);
            float height = Mathf.Max(1f, viewportSize.y);
            float scale = Mathf.Min(
                width / DesignWidth,
                height / DesignHeight);

            ScaleFrame.style.width = DesignWidth * scale;
            ScaleFrame.style.height = DesignHeight * scale;
            Design.style.scale = new Scale(new Vector3(scale, scale, 1f));
        }

        private bool IsCurrentVisual(int visualVersion)
        {
            return _screenLoaded && visualVersion == _visualVersion;
        }

        private void ReleaseVisuals()
        {
            _visualCancellation?.Cancel();
            _visualCancellation?.Dispose();
            _visualCancellation = null;

            foreach (AddressableLease<Sprite> lease in _abilityLeases)
            {
                lease.Dispose();
            }

            _abilityLeases.Clear();
            _abilityIcons.Clear();
            _abilityPreviews.Clear();
            _priceIconLease?.Dispose();
            _priceIconLease = null;
            _priceIcon = null;
        }

        private static int GetFirstUnlockedAbilityId()
        {
            return SuperAttackService.Items.FirstOrDefault(
                ability => SuperAttackService.IsUnlocked(ability.Id))?.Id ?? 0;
        }

        private static string Localize(string key)
        {
            string value = LocalizationManager.GetLocalizedString(key);
            return string.IsNullOrWhiteSpace(value) ? key : value;
        }
    }
}
