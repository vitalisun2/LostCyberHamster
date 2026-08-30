#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Vues.GameCore;

namespace LostCyberHamster.Editor
{
    /// <summary>
    /// Проверяет и сохраняет skin catalog и localization entries.
    /// </summary>
    internal static class SkinCatalogAuthoring
    {
        /// <summary>
        /// Загружает реальный runtime skin catalog.
        /// </summary>
        public static SkinDataList LoadCatalog()
        {
            TextAsset catalogAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(
                SkinVisualContentLayout.SkinCatalogPath);
            SkinDataList catalog = catalogAsset != null
                ? JsonUtility.FromJson<SkinDataList>(catalogAsset.text)
                : null;
            if (catalog?.skins == null)
            {
                throw new InvalidOperationException(
                    "Skin catalog is missing or invalid: " +
                    SkinVisualContentLayout.SkinCatalogPath);
            }

            return catalog;
        }

        /// <summary>
        /// Возвращает единственную default catalog entry.
        /// </summary>
        public static SkinData GetDefaultSkin(SkinDataList catalog)
        {
            List<SkinData> defaults = catalog.skins
                .Where(skin =>
                    skin != null &&
                    skin.Id == SkinVisualContentLayout.DefaultSkinId)
                .ToList();
            if (defaults.Count != 1)
            {
                throw new InvalidOperationException(
                    "Skin catalog must contain exactly one default skin " +
                    $"with ID {SkinVisualContentLayout.DefaultSkinId}.");
            }

            return defaults[0];
        }

        /// <summary>
        /// Преобразует display name в стабильный lowercase kebab-case slug.
        /// </summary>
        public static string NormalizeSlug(string skinName)
        {
            if (string.IsNullOrWhiteSpace(skinName))
                throw new InvalidOperationException("Skin Name is required.");

            string normalizedName = skinName.Trim().Normalize(
                NormalizationForm.FormD);
            var slug = new StringBuilder();
            bool separatorPending = false;
            foreach (char character in normalizedName)
            {
                UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(
                    character);
                if (category == UnicodeCategory.NonSpacingMark)
                    continue;

                bool isAsciiLetter =
                    character is >= 'a' and <= 'z' or >= 'A' and <= 'Z';
                bool isDigit = character is >= '0' and <= '9';
                if (!isAsciiLetter && !isDigit)
                {
                    separatorPending = slug.Length > 0;
                    continue;
                }

                if (separatorPending)
                    slug.Append('-');
                slug.Append(char.ToLowerInvariant(character));
                separatorPending = false;
            }

            if (slug.Length == 0)
            {
                throw new InvalidOperationException(
                    "Skin Name must contain Latin letters or digits.");
            }

            string result = slug.ToString();
            if (!SkinVisualContentLayout.IsValidSlug(result))
            {
                throw new InvalidOperationException(
                    "Skin Name cannot produce a valid technical slug.");
            }

            return result;
        }

        /// <summary>
        /// Проверяет конфликты slug/key и возвращает новый стабильный ID.
        /// </summary>
        public static int ValidateNewIdentity(
            SkinDataList catalog,
            string slug)
        {
            string localizationKey =
                SkinVisualContentLayout.GetLocalizationKey(slug);
            foreach (SkinData skin in catalog.skins)
            {
                if (skin == null)
                    continue;

                bool slugConflict = string.Equals(
                    SkinVisualContentLayout.GetSlug(skin.SkinVisualAddress),
                    slug,
                    StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        SkinVisualContentLayout.GetSlug(
                            skin.SkateboardSkinVisualAddress),
                        slug,
                        StringComparison.OrdinalIgnoreCase);
                bool keyConflict = string.Equals(
                    skin.NameLocalizationKey,
                    localizationKey,
                    StringComparison.OrdinalIgnoreCase);
                if (slugConflict || keyConflict)
                {
                    throw new InvalidOperationException(
                        $"Skin name conflicts with existing skin '{slug}'.");
                }
            }

            int maximumId = catalog.skins
                .Where(skin => skin != null)
                .Select(skin => skin.Id)
                .DefaultIfEmpty(SkinVisualContentLayout.DefaultSkinId)
                .Max();
            if (maximumId == int.MaxValue)
            {
                throw new InvalidOperationException(
                    "Skin catalog has no available integer ID.");
            }

            int candidateId = Math.Max(
                SkinIdentity.FirstActiveSkinId,
                maximumId + 1);
            while (SkinIdentity.IsRetired(candidateId))
            {
                if (candidateId == int.MaxValue)
                {
                    throw new InvalidOperationException(
                        "Skin catalog has no available integer ID.");
                }

                candidateId++;
            }

            return candidateId;
        }

        /// <summary>
        /// Проверяет localization JSON и отсутствие generated key.
        /// </summary>
        public static void ValidateLocalizationFiles(
            IReadOnlyList<string> paths,
            string localizationKey)
        {
            foreach (string path in paths)
            {
                SkinLocalizationFile localization = LoadLocalization(path);
                if (localization.localizationStrings.Any(entry =>
                        entry != null &&
                        string.Equals(
                            entry.key,
                            localizationKey,
                            StringComparison.Ordinal)))
                {
                    throw new InvalidOperationException(
                        $"Localization key '{localizationKey}' already exists " +
                        $"in '{path}'.");
                }
            }
        }

        /// <summary>
        /// Добавляет полную catalog entry и fallback name во все языки.
        /// </summary>
        public static void SaveNewSkin(
            SkinAddPlan plan,
            string shopSpriteAddress,
            SkinAssetTransaction transaction)
        {
            var skin = new SkinData
            {
                Id = plan.Id,
                NameLocalizationKey =
                    SkinVisualContentLayout.GetLocalizationKey(plan.Slug),
                Price = plan.Request.Price,
                PriceType = plan.DefaultSkin.PriceType,
                SkinSprite = shopSpriteAddress,
                SkinVisualAddress =
                    SkinVisualContentLayout.GetVisualAddress(
                        plan.Slug,
                        isSkateboard: false),
                SkateboardSkinVisualAddress =
                    SkinVisualContentLayout.GetVisualAddress(
                        plan.Slug,
                        isSkateboard: true),
            };

            // Catalog остаётся единственным runtime source of truth.
            transaction.BackupTextAsset(
                SkinVisualContentLayout.SkinCatalogPath);
            plan.Catalog.skins.Add(skin);
            SaveJson(
                SkinVisualContentLayout.SkinCatalogPath,
                plan.Catalog);

            // Новое имя сразу разрешается во всех подключённых языках.
            foreach (string path in plan.LocalizationPaths)
            {
                transaction.BackupTextAsset(path);
                SkinLocalizationFile localization = LoadLocalization(path);
                var entry = new SkinLocalizationEntry
                {
                    key = skin.NameLocalizationKey,
                    value = plan.Request.SkinName.Trim(),
                };
                SaveLocalization(
                    path,
                    entry,
                    localization.localizationStrings.Count > 0);
            }
        }

        /// <summary>
        /// Проверяет сохранённое имя во всех localization JSON.
        /// </summary>
        public static void ValidateSavedLocalization(
            IReadOnlyList<string> paths,
            string localizationKey,
            string expectedValue)
        {
            foreach (string path in paths)
            {
                SkinLocalizationFile localization = LoadLocalization(path);
                int matchingEntries = localization.localizationStrings.Count(
                    entry =>
                        entry != null &&
                        string.Equals(
                            entry.key,
                            localizationKey,
                            StringComparison.Ordinal) &&
                        string.Equals(
                            entry.value,
                            expectedValue,
                            StringComparison.Ordinal));
                if (matchingEntries != 1)
                {
                    throw new InvalidOperationException(
                        $"Localization key '{localizationKey}' was not saved " +
                        $"correctly in '{path}'.");
                }
            }
        }

        private static SkinLocalizationFile LoadLocalization(string path)
        {
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            SkinLocalizationFile localization = asset != null
                ? JsonUtility.FromJson<SkinLocalizationFile>(asset.text)
                : null;
            if (localization?.localizationStrings == null)
            {
                throw new InvalidOperationException(
                    $"Localization JSON is missing or invalid: {path}.");
            }

            return localization;
        }

        private static void SaveJson(string path, object data)
        {
            string json = JsonUtility.ToJson(data, prettyPrint: true) +
                          Environment.NewLine;
            string physicalPath = FileUtil.GetPhysicalPath(path);
            File.WriteAllText(
                physicalPath,
                json,
                GetUtf8Encoding(physicalPath));
            AssetDatabase.ImportAsset(
                path,
                ImportAssetOptions.ForceUpdate);
        }

        private static void SaveLocalization(
            string path,
            SkinLocalizationEntry entry,
            bool hasExistingEntries)
        {
            string physicalPath = FileUtil.GetPhysicalPath(path);
            string source = File.ReadAllText(physicalPath);
            int arrayCloseIndex = source.LastIndexOf(']');
            int closingLineStart = arrayCloseIndex >= 0
                ? source.LastIndexOf('\n', arrayCloseIndex) + 1
                : -1;
            if (arrayCloseIndex < 0 || closingLineStart <= 0)
            {
                throw new InvalidOperationException(
                    $"Localization JSON array cannot be extended: {path}.");
            }

            string newline = source.Contains("\r\n", StringComparison.Ordinal)
                ? "\r\n"
                : "\n";
            string closingIndent = source[
                closingLineStart..arrayCloseIndex];
            string entryIndent = closingIndent + "    ";
            string serializedEntry = JsonUtility.ToJson(
                entry,
                prettyPrint: true);
            string indentedEntry = string.Join(
                newline,
                serializedEntry.Split('\n')
                    .Select(line =>
                    {
                        string trimmedLine = line.TrimEnd('\r');
                        if (trimmedLine.StartsWith(
                                "  ",
                                StringComparison.Ordinal))
                        {
                            trimmedLine = "  " + trimmedLine;
                        }

                        return entryIndent + trimmedLine;
                    }));
            string prefix = source[..closingLineStart]
                .TrimEnd(' ', '\t', '\r', '\n');
            string suffix = source[arrayCloseIndex..];
            string result = prefix +
                            (hasExistingEntries ? "," : string.Empty) +
                            newline +
                            indentedEntry +
                            newline +
                            closingIndent +
                            suffix;
            File.WriteAllText(
                physicalPath,
                result,
                GetUtf8Encoding(physicalPath));
            AssetDatabase.ImportAsset(
                path,
                ImportAssetOptions.ForceUpdate);
        }

        private static Encoding GetUtf8Encoding(string physicalPath)
        {
            byte[] bytes = File.ReadAllBytes(physicalPath);
            bool hasByteOrderMark = bytes.Length >= 3 &&
                                    bytes[0] == 0xEF &&
                                    bytes[1] == 0xBB &&
                                    bytes[2] == 0xBF;
            return new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: hasByteOrderMark);
        }
    }
}
#endif
