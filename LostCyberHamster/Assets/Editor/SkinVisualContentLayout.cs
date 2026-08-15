#if UNITY_EDITOR
using System;
using System.Linq;

namespace LostCyberHamster.Editor
{
    /// <summary>
    /// Хранит единые пути и соглашения authoring-контента SkinVisual.
    /// </summary>
    internal static class SkinVisualContentLayout
    {
        public const int DefaultSkinId = 0;
        public const string HamsterPrefabPath =
            "Assets/Content/prefabs/Hamster.prefab";
        public const string SkinCatalogPath =
            "Assets/Content/skins/skins.json";
        public const string NormalVisualPrefabRoot =
            "Assets/Content/prefabs/skins/normal_mode";
        public const string SkateboardVisualPrefabRoot =
            "Assets/Content/prefabs/skins/skateboard_mode";
        public const string NormalAnimationRoot =
            "Assets/Animations/Hamster/normal_mode/skin_visuals";
        public const string SkateboardAnimationRoot =
            "Assets/Animations/Hamster/skateboard_mode/skin_visuals";
        public const string NormalSpriteRoot =
            "Assets/Content/skins/normal_mode";
        public const string SkateboardSpriteRoot =
            "Assets/Content/skins/skateboard_mode";
        public const string VisualAddressablesGroup = "Skin Visuals";
        public const string SkinSpritesAddressablesGroup = "skins";
        public const string LocalizationAddressablesGroup = "Localization";

        private const string NormalAddressPrefix = "skin-visual/";
        private const string SkateboardAddressPrefix =
            "skin-visual/skateboard/";
        private const string SkinSpriteAddressPrefix = "skin_";

        /// <summary>
        /// Проверяет, что address принадлежит skateboard visual namespace.
        /// </summary>
        public static bool IsSkateboardAddress(string address)
        {
            return !string.IsNullOrWhiteSpace(address) &&
                   address.StartsWith(
                       SkateboardAddressPrefix,
                       StringComparison.Ordinal);
        }

        /// <summary>
        /// Возвращает slug последнего сегмента address.
        /// </summary>
        public static string GetSlug(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return string.Empty;

            int separatorIndex = address.LastIndexOf('/');
            return separatorIndex >= 0
                ? address[(separatorIndex + 1)..]
                : address;
        }

        /// <summary>
        /// Возвращает ожидаемый prefab для normal или skateboard address.
        /// </summary>
        public static string GetVisualPrefabPath(string address)
        {
            bool isSkateboard = IsSkateboardAddress(address);
            string root = isSkateboard
                ? SkateboardVisualPrefabRoot
                : NormalVisualPrefabRoot;
            string slug = GetSlug(address);
            return $"{root}/{slug}/{slug}-skin-visual.prefab";
        }

        /// <summary>
        /// Возвращает visual address режима для slug.
        /// </summary>
        public static string GetVisualAddress(string slug, bool isSkateboard)
        {
            string prefix = isSkateboard
                ? SkateboardAddressPrefix
                : NormalAddressPrefix;
            return $"{prefix}{slug}";
        }

        /// <summary>
        /// Возвращает папку animation assets режима.
        /// </summary>
        public static string GetAnimationPath(string slug, bool isSkateboard)
        {
            string root = isSkateboard
                ? SkateboardAnimationRoot
                : NormalAnimationRoot;
            return $"{root}/{slug}";
        }

        /// <summary>
        /// Возвращает папку sprite sheets режима.
        /// </summary>
        public static string GetSpritePath(string slug, bool isSkateboard)
        {
            string root = isSkateboard
                ? SkateboardSpriteRoot
                : NormalSpriteRoot;
            return $"{root}/{slug}";
        }

        /// <summary>
        /// Возвращает Addressables address shop-спрайта для slug.
        /// </summary>
        public static string GetSkinSpriteAddress(string slug)
        {
            return SkinSpriteAddressPrefix + slug;
        }

        /// <summary>
        /// Возвращает localization key имени скина для slug.
        /// </summary>
        public static string GetLocalizationKey(string slug)
        {
            return $"skin_name_{slug.Replace('-', '_')}";
        }

        /// <summary>
        /// Проверяет lowercase ASCII kebab-case slug.
        /// </summary>
        public static bool IsValidSlug(string slug)
        {
            return !string.IsNullOrWhiteSpace(slug) &&
                   slug[0] != '-' &&
                   slug[^1] != '-' &&
                   slug.All(character =>
                       character is >= 'a' and <= 'z' or >= '0' and <= '9' ||
                       character == '-');
        }
    }
}
#endif
