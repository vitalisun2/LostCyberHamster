#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.GameEngine.Skins;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace LostCyberHamster.Editor
{
    /// <summary>
    /// Проверяет prefab contract, action mappings и Addressables-настройки SkinVisual-контента.
    /// </summary>
    public static class SkinVisualContentValidator
    {
        [MenuItem("Tools/Skins/Validate Skin Visuals")]
        public static void ValidateMenu()
        {
            IReadOnlyList<string> errors = Validate();
            if (errors.Count == 0)
            {
                Debug.Log("[Skin Visuals] Validation passed.");
                return;
            }

            Debug.LogError("[Skin Visuals] Validation failed:\n" + string.Join("\n", errors));
        }

        public static IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            IReadOnlyList<SkinData> skins = LoadSkinCatalog(errors);
            ValidateCatalog(errors, skins);
            ValidatePrefabs(errors, skins);
            ValidateHamster(errors);
            ValidateAddressables(errors, skins);
            return errors;
        }

        private static IReadOnlyList<SkinData> LoadSkinCatalog(ICollection<string> errors)
        {
            TextAsset catalog = AssetDatabase.LoadAssetAtPath<TextAsset>(
                SkinVisualContentLayout.SkinCatalogPath);
            SkinDataList data = null;
            try
            {
                if (catalog != null)
                    data = JsonUtility.FromJson<SkinDataList>(catalog.text);
            }
            catch (Exception exception)
            {
                errors.Add(
                    $"Skin catalog JSON cannot be read: {exception.Message}");
                return Array.Empty<SkinData>();
            }
            if (data?.skins == null)
            {
                errors.Add(
                    $"Skin catalog is missing or invalid: " +
                    SkinVisualContentLayout.SkinCatalogPath);
                return Array.Empty<SkinData>();
            }

            return data.skins;
        }

        private static void ValidateCatalog(
            ICollection<string> errors,
            IReadOnlyList<SkinData> skins)
        {
            foreach (IGrouping<int, SkinData> duplicate in skins
                         .Where(skin => skin != null)
                         .GroupBy(skin => skin.Id)
                         .Where(group => group.Count() > 1))
            {
                errors.Add($"Duplicate skin ID: {duplicate.Key}.");
            }

            if (skins.Count(skin =>
                    skin != null &&
                    skin.Id == SkinVisualContentLayout.DefaultSkinId) != 1)
            {
                errors.Add("Skin catalog must contain exactly one default skin with ID 0.");
            }

            var localizationKeys = new HashSet<string>(StringComparer.Ordinal);
            var normalAddresses = new HashSet<string>(StringComparer.Ordinal);
            var skateboardAddresses = new HashSet<string>(StringComparer.Ordinal);
            foreach (SkinData skin in skins)
            {
                if (skin == null)
                {
                    errors.Add("Skin catalog contains a null entry.");
                    continue;
                }

                if (skin.Id < 0)
                    errors.Add($"Skin {skin.Id}: ID must not be negative.");
                if (string.IsNullOrWhiteSpace(skin.NameLocalizationKey))
                {
                    errors.Add(
                        $"Skin {skin.Id}: NameLocalizationKey is empty.");
                }
                else if (!localizationKeys.Add(skin.NameLocalizationKey))
                {
                    errors.Add(
                        $"Duplicate skin localization key: " +
                        skin.NameLocalizationKey);
                }
                if (skin.Price < 0)
                    errors.Add($"Skin {skin.Id}: Price must not be negative.");
                if (string.IsNullOrWhiteSpace(skin.SkinSprite))
                    errors.Add($"Skin {skin.Id}: SkinSprite is empty.");

                if (string.IsNullOrWhiteSpace(skin.SkinVisualAddress))
                {
                    errors.Add($"Skin {skin.Id}: SkinVisualAddress is empty.");
                }
                else if (!normalAddresses.Add(skin.SkinVisualAddress))
                {
                    errors.Add($"Duplicate normal visual address: {skin.SkinVisualAddress}.");
                }
                else if (SkinVisualContentLayout.IsSkateboardAddress(
                             skin.SkinVisualAddress))
                {
                    errors.Add(
                        $"Skin {skin.Id}: normal visual address has " +
                        "skateboard namespace.");
                }

                string normalSlug = SkinVisualContentLayout.GetSlug(
                    skin.SkinVisualAddress);
                if (!SkinVisualContentLayout.IsValidSlug(normalSlug))
                {
                    errors.Add($"Skin {skin.Id}: normal slug is invalid.");
                }
                else if (!string.Equals(
                             skin.SkinVisualAddress,
                             SkinVisualContentLayout.GetVisualAddress(
                                 normalSlug,
                                 isSkateboard: false),
                             StringComparison.Ordinal))
                {
                    errors.Add(
                        $"Skin {skin.Id}: normal visual address has wrong " +
                        "format.");
                }

                if (string.IsNullOrWhiteSpace(skin.SkateboardSkinVisualAddress))
                {
                    errors.Add(
                        $"Skin {skin.Id}: SkateboardSkinVisualAddress is empty.");
                    continue;
                }

                if (!SkinVisualContentLayout.IsSkateboardAddress(
                        skin.SkateboardSkinVisualAddress))
                {
                    errors.Add(
                        $"Skin {skin.Id}: skateboard visual address has wrong namespace.");
                }

                if (!skateboardAddresses.Add(skin.SkateboardSkinVisualAddress))
                    errors.Add($"Duplicate skateboard visual address: {skin.SkateboardSkinVisualAddress}.");
                if (SkinVisualContentLayout.GetSlug(skin.SkinVisualAddress) !=
                    SkinVisualContentLayout.GetSlug(
                        skin.SkateboardSkinVisualAddress))
                    errors.Add($"Skin {skin.Id}: normal and skateboard visual slugs differ.");
                string skateboardSlug = SkinVisualContentLayout.GetSlug(
                    skin.SkateboardSkinVisualAddress);
                if (!SkinVisualContentLayout.IsValidSlug(skateboardSlug))
                {
                    errors.Add($"Skin {skin.Id}: skateboard slug is invalid.");
                }
                else if (!string.Equals(
                             skin.SkateboardSkinVisualAddress,
                             SkinVisualContentLayout.GetVisualAddress(
                                 skateboardSlug,
                                 isSkateboard: true),
                             StringComparison.Ordinal))
                {
                    errors.Add(
                        $"Skin {skin.Id}: skateboard visual address has " +
                        "wrong format.");
                }
            }
        }

        private static void ValidatePrefabs(
            ICollection<string> errors,
            IReadOnlyList<SkinData> skins)
        {
            List<SkinData> defaults = skins.Where(skin =>
                    skin != null &&
                    skin.Id == SkinVisualContentLayout.DefaultSkinId)
                .ToList();
            if (defaults.Count != 1)
                return;
            SkinData defaultSkin = defaults[0];

            SkinVisual normalTemplate = LoadVisual(
                errors,
                defaultSkin.SkinVisualAddress,
                "normal default");
            SkinVisual skateboardTemplate = LoadVisual(
                errors,
                defaultSkin.SkateboardSkinVisualAddress,
                "skateboard default");
            if (normalTemplate == null || skateboardTemplate == null)
                return;
            if (normalTemplate.Mappings == null ||
                normalTemplate.Mappings.Count == 0 ||
                skateboardTemplate.Mappings == null ||
                skateboardTemplate.Mappings.Count == 0)
            {
                errors.Add(
                    "Default SkinVisual templates must contain action mappings.");
                return;
            }

            foreach (SkinData skin in skins)
            {
                if (skin == null)
                    continue;

                string normalSlug = SkinVisualContentLayout.GetSlug(
                    skin.SkinVisualAddress);
                ValidatePrefab(
                    errors,
                    SkinVisualContentLayout.NormalVisualPrefabRoot,
                    normalSlug,
                    normalTemplate,
                    false);

                if (string.IsNullOrWhiteSpace(skin.SkateboardSkinVisualAddress))
                    continue;

                string skateboardSlug = SkinVisualContentLayout.GetSlug(
                    skin.SkateboardSkinVisualAddress);
                ValidatePrefab(
                    errors,
                    SkinVisualContentLayout.SkateboardVisualPrefabRoot,
                    skateboardSlug,
                    skateboardTemplate,
                    true);
            }
        }

        private static void ValidatePrefab(
            ICollection<string> errors,
            string root,
            string slug,
            SkinVisual template,
            bool isSkateboard)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                errors.Add($"Skin visual slug is missing under '{root}'.");
                return;
            }

            string prefabPath = $"{root}/{slug}/{slug}-skin-visual.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            SkinVisual visual = prefab != null ? prefab.GetComponent<SkinVisual>() : null;
            if (visual == null)
            {
                errors.Add($"Missing SkinVisual prefab: {prefabPath}");
                return;
            }

            SpriteRenderer spriteRenderer = prefab.GetComponent<SpriteRenderer>();
            Animator animator = prefab.GetComponent<Animator>();
            AnimatorController controller = animator?.runtimeAnimatorController as AnimatorController;
            if (spriteRenderer?.sprite == null)
                errors.Add($"{slug}: SpriteRenderer has no sprite in {prefabPath}.");
            if (controller == null)
                errors.Add($"{slug}: AnimatorController is missing in {prefabPath}.");
            if (visual.Mappings == null || template.Mappings == null)
            {
                errors.Add($"{slug}: SkinVisual mappings are missing.");
                return;
            }

            foreach (SkinVisualActionMapping expected in template.Mappings)
            {
                if (expected == null)
                {
                    errors.Add("Default SkinVisual contains a null mapping.");
                    continue;
                }

                List<SkinVisualActionMapping> matches = visual.Mappings
                    .Where(mapping =>
                        HasSameSemanticContract(mapping, expected))
                    .ToList();
                if (matches.Count != 1)
                {
                    errors.Add(
                        $"{slug}: mapping for {Describe(expected)} is " +
                        "missing or duplicated.");
                    continue;
                }

                ValidateMapping(
                    errors,
                    matches[0],
                    expected,
                    controller,
                    slug);
            }

            if (visual.Mappings.Count != template.Mappings.Count)
            {
                errors.Add(
                    $"{slug}: mapping count differs from default template.");
            }

            if (isSkateboard)
                ValidatePhysicsShapeSprites(
                    errors,
                    visual,
                    template,
                    slug);
        }

        private static void ValidatePhysicsShapeSprites(
            ICollection<string> errors,
            SkinVisual visual,
            SkinVisual template,
            string slug)
        {
            if (visual.PhysicsShapeSprites == null ||
                template.PhysicsShapeSprites == null)
            {
                errors.Add($"{slug}: Physics Shape manifest is missing.");
                return;
            }
            if (visual.PhysicsShapeSprites.Count !=
                template.PhysicsShapeSprites.Count)
            {
                errors.Add(
                    $"{slug}: Physics Shape manifest count differs from " +
                    "default template.");
            }

            // Проверяем manifest и наличие импортированной формы у каждого sprite.
            var configuredSprites = new HashSet<Sprite>();
            foreach (Sprite sprite in visual.PhysicsShapeSprites)
            {
                if (sprite == null)
                {
                    errors.Add($"{slug}: Physics Shape sprite manifest contains null.");
                    continue;
                }

                if (!configuredSprites.Add(sprite))
                    errors.Add($"{slug}: sprite '{sprite.name}' is duplicated in Physics Shape manifest.");
                if (sprite.GetPhysicsShapeCount() == 0)
                    errors.Add($"{slug}: sprite '{sprite.name}' has no custom Physics Shape.");
            }

            // Каждый sprite из animation clips должен заранее попасть в runtime cache manifest.
            foreach (SkinVisualActionMapping mapping in visual.Mappings.Where(mapping => mapping?.Clip != null))
            {
                EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(mapping.Clip);
                foreach (EditorCurveBinding binding in bindings.Where(binding =>
                             binding.type == typeof(SpriteRenderer) &&
                             binding.propertyName == "m_Sprite"))
                {
                    ObjectReferenceKeyframe[] keyframes =
                        AnimationUtility.GetObjectReferenceCurve(mapping.Clip, binding);
                    foreach (Sprite sprite in keyframes.Select(keyframe => keyframe.value).OfType<Sprite>())
                    {
                        if (!configuredSprites.Contains(sprite))
                            errors.Add($"{slug}: animated sprite '{sprite.name}' is missing from Physics Shape manifest.");
                    }
                }
            }
        }

        private static void ValidateMapping(
            ICollection<string> errors,
            SkinVisualActionMapping mapping,
            SkinVisualActionMapping expected,
            AnimatorController controller,
            string slug)
        {
            if (string.IsNullOrWhiteSpace(mapping.StateName) || mapping.Clip == null)
            {
                errors.Add($"{slug}: {mapping.Action} mapping has no state or clip.");
                return;
            }

            AnimatorState state = controller == null
                ? null
                : controller.layers
                    .SelectMany(layer => layer.stateMachine.states)
                    .Select(child => child.state)
                    .FirstOrDefault(candidate => candidate.name == mapping.StateName);
            if (state == null || state.motion != mapping.Clip)
                errors.Add($"{slug}: state '{mapping.StateName}' does not use mapped clip.");

            if (expected.Clip != null &&
                !Mathf.Approximately(
                    mapping.Clip.frameRate,
                    expected.Clip.frameRate))
            {
                errors.Add(
                    $"{slug}: clip '{mapping.Clip.name}' frame rate differs " +
                    "from default template.");
            }

            if (mapping.Loop != expected.Loop)
                errors.Add($"{slug}: {mapping.Action} loop flag is invalid.");
        }

        private static SkinVisual LoadVisual(
            ICollection<string> errors,
            string address,
            string label)
        {
            string path = SkinVisualContentLayout.GetVisualPrefabPath(address);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            SkinVisual visual = prefab != null
                ? prefab.GetComponent<SkinVisual>()
                : null;
            if (visual == null)
                errors.Add($"Missing {label} SkinVisual prefab: {path}.");
            return visual;
        }

        private static bool HasSameSemanticContract(
            SkinVisualActionMapping candidate,
            SkinVisualActionMapping expected)
        {
            return candidate != null &&
                   candidate.Action == expected.Action &&
                   candidate.MatchAnyVariant == expected.MatchAnyVariant &&
                   candidate.Variant == expected.Variant &&
                   candidate.MatchAnyOutcome == expected.MatchAnyOutcome &&
                   candidate.Outcome == expected.Outcome &&
                   string.Equals(
                       candidate.StateName,
                       expected.StateName,
                       StringComparison.Ordinal);
        }

        private static string Describe(SkinVisualActionMapping mapping)
        {
            string variant = mapping.MatchAnyVariant
                ? "AnyVariant"
                : mapping.Variant.ToString();
            string outcome = mapping.MatchAnyOutcome
                ? "AnyOutcome"
                : mapping.Outcome.ToString();
            return $"{mapping.Action}/{variant}/{outcome}";
        }

        private static void ValidateHamster(ICollection<string> errors)
        {
            GameObject hamster = AssetDatabase.LoadAssetAtPath<GameObject>(
                SkinVisualContentLayout.HamsterPrefabPath);
            Transform collisionBody = FindChild(hamster, "collision_body");
            Transform normalActor = FindChild(hamster, "normal_actor");
            Transform skateboardActor = FindChild(hamster, "skateboard_actor");
            Transform normalSkinSlot = FindChild(normalActor, "skin_slot");
            Transform skateboardSkinSlot = FindChild(skateboardActor, "skin_slot");
            if (collisionBody == null || collisionBody.GetComponent<BoxCollider2D>() == null)
                errors.Add("Hamster collision_body with BoxCollider2D is missing.");
            if (collisionBody != null && collisionBody.GetComponent<SpriteRenderer>() != null)
                errors.Add("Hamster collision_body still owns SpriteRenderer.");
            if (collisionBody != null && collisionBody.GetComponent<Animator>() != null)
                errors.Add("Hamster collision_body still owns visual Animator.");
            if (normalSkinSlot == null || normalSkinSlot.GetComponent<SkinVisualHost>() == null)
                errors.Add("Hamster normal_actor skin_slot with SkinVisualHost is missing.");
            if (skateboardSkinSlot == null || skateboardSkinSlot.GetComponent<SkinVisualHost>() == null)
                errors.Add("Hamster skateboard_actor skin_slot with SkinVisualHost is missing.");
        }

        private static void ValidateAddressables(
            ICollection<string> errors,
            IReadOnlyList<SkinData> skins)
        {
            AddressableAssetSettings settings =
                AddressableAssetSettingsDefaultObject.Settings;
            AddressableAssetGroup group = settings?.FindGroup(
                SkinVisualContentLayout.VisualAddressablesGroup);
            BundledAssetGroupSchema schema = group?.GetSchema<BundledAssetGroupSchema>();
            if (schema == null || schema.BundleMode != BundledAssetGroupSchema.BundlePackingMode.PackSeparately)
            {
                errors.Add("Addressables group 'Skin Visuals' must use Pack Separately.");
                return;
            }

            AddressableAssetGroup skinSpritesGroup =
                settings?.FindGroup(
                    SkinVisualContentLayout.SkinSpritesAddressablesGroup);
            if (skinSpritesGroup == null)
            {
                errors.Add("Addressables group 'skins' is missing.");
                return;
            }

            foreach (SkinData skin in skins)
            {
                if (skin == null)
                    continue;

                ValidateAddress(
                    errors,
                    settings,
                    group,
                    skin.SkinVisualAddress);
                if (!string.IsNullOrWhiteSpace(skin.SkateboardSkinVisualAddress))
                {
                    ValidateAddress(
                        errors,
                        settings,
                        group,
                        skin.SkateboardSkinVisualAddress);
                }
                ValidateSkinSpriteAddress(
                    errors,
                    settings,
                    skinSpritesGroup,
                    skin.SkinSprite);
            }

        }

        private static void ValidateAddress(
            ICollection<string> errors,
            AddressableAssetSettings settings,
            AddressableAssetGroup group,
            string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                errors.Add("SkinVisual address must not be empty.");
                return;
            }

            List<AddressableAssetEntry> entries = settings.groups
                .Where(candidateGroup => candidateGroup != null)
                .SelectMany(candidateGroup => candidateGroup.entries)
                .Where(entry => entry != null)
                .Where(entry => entry.address == address)
                .ToList();
            if (entries.Count == 0)
            {
                errors.Add($"Addressable entry '{address}' is missing.");
                return;
            }
            if (entries[0].parentGroup != group)
            {
                errors.Add(
                    $"Addressable entry '{address}' is in wrong group.");
                return;
            }
            if (entries.Count > 1)
            {
                errors.Add($"Addressable entry '{address}' is duplicated.");
                return;
            }

            string expectedPath =
                SkinVisualContentLayout.GetVisualPrefabPath(address);
            string expectedGuid = AssetDatabase.AssetPathToGUID(expectedPath);
            string actualGuid = entries[0].guid;
            if (string.IsNullOrEmpty(expectedGuid) || actualGuid != expectedGuid)
                errors.Add($"Addressable entry '{address}' points to wrong prefab.");
        }

        private static void ValidateSkinSpriteAddress(
            ICollection<string> errors,
            AddressableAssetSettings settings,
            AddressableAssetGroup group,
            string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                errors.Add("SkinSprite address must not be empty.");
                return;
            }

            int subObjectStart = address.IndexOf('[');
            bool hasSubObject = subObjectStart >= 0;
            if (hasSubObject && !address.EndsWith("]", StringComparison.Ordinal))
            {
                errors.Add($"SkinSprite address '{address}' is malformed.");
                return;
            }

            string mainAddress = hasSubObject
                ? address[..subObjectStart]
                : address;
            string subObjectName = hasSubObject
                ? address[(subObjectStart + 1)..^1]
                : string.Empty;
            List<AddressableAssetEntry> entries = settings.groups
                .Where(candidateGroup => candidateGroup != null)
                .SelectMany(candidateGroup => candidateGroup.entries)
                .Where(entry => entry != null)
                .Where(entry => entry.address == mainAddress)
                .ToList();
            if (entries.Count != 1)
            {
                errors.Add(
                    $"SkinSprite address '{mainAddress}' must have one entry.");
                return;
            }
            if (entries[0].parentGroup != group)
            {
                errors.Add(
                    $"SkinSprite address '{mainAddress}' is in wrong group.");
                return;
            }

            string assetPath = AssetDatabase.GUIDToAssetPath(entries[0].guid);
            List<Sprite> sprites = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<Sprite>()
                .ToList();
            bool spriteExists = hasSubObject
                ? sprites.Any(sprite => sprite.name == subObjectName)
                : sprites.Count > 0;
            if (!spriteExists)
            {
                errors.Add(
                    $"SkinSprite '{address}' does not resolve to a Sprite.");
            }
        }

        private static Transform FindChild(GameObject root, string childName)
        {
            if (root == null)
                return null;

            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.name == childName);
        }

        private static Transform FindChild(Transform root, string childName)
        {
            if (root == null)
                return null;

            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.name == childName);
        }
    }
}
#endif
