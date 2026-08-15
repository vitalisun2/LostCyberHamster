#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace LostCyberHamster.Editor
{
    /// <summary>
    /// Выполняет одну проверенную и откатываемую Add Skin операцию.
    /// </summary>
    internal static class SkinAddService
    {
        /// <summary>
        /// Возвращает ожидаемые sprite sheets из текущего default skin.
        /// </summary>
        public static IReadOnlyList<string> GetExpectedSpriteSheets(
            bool isSkateboard)
        {
            SkinDataList catalog = SkinCatalogAuthoring.LoadCatalog();
            SkinData defaultSkin = SkinCatalogAuthoring.GetDefaultSkin(
                catalog);
            return SkinSpriteSheetAuthoring.GetExpectedRelativePaths(
                defaultSkin,
                isSkateboard);
        }

        /// <summary>
        /// Создаёт полностью зарегистрированный skin после полного preflight.
        /// </summary>
        public static string AddSkin(SkinAddRequest request)
        {
            SkinAddPlan plan = BuildPlan(request);
            AddressableAssetSettings settings =
                SkinAddressablesAuthoring.GetSettings();
            var transaction = new SkinAssetTransaction(settings);

            try
            {
                // Все target roots новые; rollback удаляет только их.
                foreach (string root in GetTargetRoots(plan.Slug))
                {
                    CreateAssetRoot(root);
                    transaction.TrackCreatedAssetRoot(root);
                }

                // Копируем art поверх cloned default importer metadata.
                SkinSpriteSheetAuthoring.CopySheets(
                    plan.Request.NormalSourceFolder,
                    plan.DefaultSkin,
                    plan.Slug,
                    isSkateboard: false,
                    plan.NormalTemplateSheets);
                SkinSpriteSheetAuthoring.CopySheets(
                    plan.Request.SkateboardSourceFolder,
                    plan.DefaultSkin,
                    plan.Slug,
                    isSkateboard: true,
                    plan.SkateboardTemplateSheets);

                // Генерируем semantic mappings, clips, controllers и prefabs.
                Sprite shopSprite =
                    SkinVisualContentSynchronizer.CreateSkinVisuals(
                        plan.DefaultSkin,
                        plan.Slug);
                string normalAddress =
                    SkinVisualContentLayout.GetVisualAddress(
                        plan.Slug,
                        isSkateboard: false);
                string skateboardAddress =
                    SkinVisualContentLayout.GetVisualAddress(
                        plan.Slug,
                        isSkateboard: true);
                string shopMainAddress =
                    SkinVisualContentLayout.GetSkinSpriteAddress(plan.Slug);

                // Регистрируем оба prefabs и точный shop sprite sub-object.
                SkinAddressablesAuthoring.Register(
                    settings,
                    transaction,
                    SkinVisualContentLayout.VisualAddressablesGroup,
                    SkinVisualContentLayout.GetVisualPrefabPath(
                        normalAddress),
                    normalAddress);
                SkinAddressablesAuthoring.Register(
                    settings,
                    transaction,
                    SkinVisualContentLayout.VisualAddressablesGroup,
                    SkinVisualContentLayout.GetVisualPrefabPath(
                        skateboardAddress),
                    skateboardAddress);
                SkinAddressablesAuthoring.Register(
                    settings,
                    transaction,
                    SkinVisualContentLayout.SkinSpritesAddressablesGroup,
                    AssetDatabase.GetAssetPath(shopSprite),
                    shopMainAddress);

                string shopSpriteAddress =
                    $"{shopMainAddress}[{shopSprite.name}]";
                SkinCatalogAuthoring.SaveNewSkin(
                    plan,
                    shopSpriteAddress,
                    transaction);
                SkinAddressablesAuthoring.Save(settings);

                // Финальный gate проверяет уже сохранённый runtime content.
                ValidateCreatedContent(plan);
                return $"Skin '{plan.Request.SkinName.Trim()}' added " +
                       $"with ID {plan.Id} and slug '{plan.Slug}'.";
            }
            catch (Exception exception)
            {
                string rollbackError = transaction.Rollback();
                string message = "Skin was not added. " + exception.Message;
                if (!string.IsNullOrWhiteSpace(rollbackError))
                {
                    message += Environment.NewLine +
                               "Rollback needs attention:" +
                               Environment.NewLine +
                               rollbackError;
                }

                throw new InvalidOperationException(message, exception);
            }
        }

        private static SkinAddPlan BuildPlan(SkinAddRequest request)
        {
            // Проверяем ввод и запрещённое editor state.
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "Add Skin is available only outside Play Mode.");
            }
            if (request.Price < 0)
            {
                throw new InvalidOperationException(
                    "Price must be zero or greater.");
            }

            // Существующий production content должен быть целым до записи.
            IReadOnlyList<string> baselineErrors =
                SkinVisualContentValidator.Validate();
            if (baselineErrors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Existing skin content must pass validation first:\n- " +
                    string.Join("\n- ", baselineErrors));
            }

            // Identity и template schema вычисляются один раз до любой записи.
            string skinName = request.SkinName?.Trim();
            string slug = SkinCatalogAuthoring.NormalizeSlug(skinName);
            SkinDataList catalog = SkinCatalogAuthoring.LoadCatalog();
            SkinData defaultSkin = SkinCatalogAuthoring.GetDefaultSkin(
                catalog);
            if (string.Equals(
                    SkinVisualContentLayout.GetSlug(
                        defaultSkin.SkinVisualAddress),
                    slug,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Default skin cannot be replaced.");
            }

            int id = SkinCatalogAuthoring.ValidateNewIdentity(
                catalog,
                slug);
            IReadOnlyList<string> normalSheets =
                SkinSpriteSheetAuthoring.GetTemplateSheets(
                    defaultSkin,
                    isSkateboard: false);
            IReadOnlyList<string> skateboardSheets =
                SkinSpriteSheetAuthoring.GetTemplateSheets(
                    defaultSkin,
                    isSkateboard: true);
            string normalSourceFolder =
                SkinSpriteSheetAuthoring.ValidateSourceFolder(
                    request.NormalSourceFolder,
                    defaultSkin,
                    isSkateboard: false,
                    normalSheets);
            string skateboardSourceFolder =
                SkinSpriteSheetAuthoring.ValidateSourceFolder(
                    request.SkateboardSourceFolder,
                    defaultSkin,
                    isSkateboard: true,
                    skateboardSheets);

            // Проверяем localization, target paths и address conflicts.
            AddressableAssetSettings settings =
                SkinAddressablesAuthoring.GetSettings();
            SkinAddressablesAuthoring.ValidateSkinGroups(settings);
            IReadOnlyList<string> localizationPaths =
                SkinAddressablesAuthoring.GetLocalizationPaths(settings);
            string localizationKey =
                SkinVisualContentLayout.GetLocalizationKey(slug);
            SkinCatalogAuthoring.ValidateLocalizationFiles(
                localizationPaths,
                localizationKey);
            ValidateTargetsAvailable(slug);
            SkinAddressablesAuthoring.ValidateAddressAvailable(
                settings,
                SkinVisualContentLayout.GetVisualAddress(
                    slug,
                    isSkateboard: false));
            SkinAddressablesAuthoring.ValidateAddressAvailable(
                settings,
                SkinVisualContentLayout.GetVisualAddress(
                    slug,
                    isSkateboard: true));
            SkinAddressablesAuthoring.ValidateAddressAvailable(
                settings,
                SkinVisualContentLayout.GetSkinSpriteAddress(slug));

            var normalizedRequest = new SkinAddRequest(
                skinName,
                request.Price,
                normalSourceFolder,
                skateboardSourceFolder);
            return new SkinAddPlan(
                normalizedRequest,
                slug,
                id,
                catalog,
                defaultSkin,
                normalSheets,
                skateboardSheets,
                localizationPaths);
        }

        private static void ValidateCreatedContent(SkinAddPlan plan)
        {
            IReadOnlyList<string> errors =
                SkinVisualContentValidator.Validate();
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Skin content validation failed:\n- " +
                    string.Join("\n- ", errors));
            }

            SkinCatalogAuthoring.ValidateSavedLocalization(
                plan.LocalizationPaths,
                SkinVisualContentLayout.GetLocalizationKey(plan.Slug),
                plan.Request.SkinName.Trim());
        }

        private static void ValidateTargetsAvailable(string slug)
        {
            foreach (string root in GetTargetRoots(slug))
            {
                if (AssetDatabase.IsValidFolder(root) ||
                    System.IO.Directory.Exists(
                        FileUtil.GetPhysicalPath(root)))
                {
                    throw new InvalidOperationException(
                        $"Target folder already exists and is protected: " +
                        root);
                }
            }
        }

        private static IReadOnlyList<string> GetTargetRoots(string slug)
        {
            return new[]
            {
                SkinVisualContentLayout.GetSpritePath(
                    slug,
                    isSkateboard: false),
                SkinVisualContentLayout.GetSpritePath(
                    slug,
                    isSkateboard: true),
                SkinVisualContentLayout.GetAnimationPath(
                    slug,
                    isSkateboard: false),
                SkinVisualContentLayout.GetAnimationPath(
                    slug,
                    isSkateboard: true),
                $"{SkinVisualContentLayout.NormalVisualPrefabRoot}/{slug}",
                $"{SkinVisualContentLayout.SkateboardVisualPrefabRoot}/{slug}",
            };
        }

        private static void CreateAssetRoot(string assetPath)
        {
            int separatorIndex = assetPath.LastIndexOf('/');
            if (separatorIndex <= 0)
            {
                throw new InvalidOperationException(
                    $"Invalid target asset path: {assetPath}.");
            }

            string parent = assetPath[..separatorIndex];
            string folderName = assetPath[(separatorIndex + 1)..];
            if (!AssetDatabase.IsValidFolder(parent))
            {
                throw new InvalidOperationException(
                    $"Required asset folder is missing: {parent}.");
            }

            if (string.IsNullOrWhiteSpace(
                    AssetDatabase.CreateFolder(parent, folderName)))
            {
                throw new InvalidOperationException(
                    $"Cannot create target asset folder: {assetPath}.");
            }
        }

    }
}
#endif
