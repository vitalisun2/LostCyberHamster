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
        private const string HamsterPrefabPath = "Assets/Content/prefabs/Hamster.prefab";
        private const string SkinCatalogPath = "Assets/Content/skins/skins.json";
        private const string NormalVisualPrefabRoot = "Assets/Content/prefabs/skins/normal_mode";
        private const string SkateboardVisualPrefabRoot = "Assets/Content/prefabs/skins/skateboard_mode";
        private const string VisualAddressablesGroup = "Skin Visuals";

        private static readonly SkinVisualAction[] NormalActions =
        {
            SkinVisualAction.GroundRun,
            SkinVisualAction.RoofRun,
            SkinVisualAction.RunFromRoof,
            SkinVisualAction.GroundJump,
            SkinVisualAction.JumpOnObstacle,
            SkinVisualAction.JumpOnRoof,
            SkinVisualAction.RoofJump,
            SkinVisualAction.JumpFromRoof,
            SkinVisualAction.JumpOnObstacleFromRoof,
        };

        private static readonly SkinVisualAction[] SkateboardActions =
        {
            SkinVisualAction.SkateboardRideA,
            SkinVisualAction.SkateboardRideB,
            SkinVisualAction.SkateboardPush,
            SkinVisualAction.SkateboardJump,
        };

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
            TextAsset catalog = AssetDatabase.LoadAssetAtPath<TextAsset>(SkinCatalogPath);
            SkinDataList data = catalog != null
                ? JsonUtility.FromJson<SkinDataList>(catalog.text)
                : null;
            if (data?.skins == null)
            {
                errors.Add($"Skin catalog is missing or invalid: {SkinCatalogPath}");
                return Array.Empty<SkinData>();
            }

            return data.skins;
        }

        private static void ValidateCatalog(
            ICollection<string> errors,
            IReadOnlyList<SkinData> skins)
        {
            foreach (IGrouping<int, SkinData> duplicate in skins
                         .GroupBy(skin => skin.Id)
                         .Where(group => group.Count() > 1))
            {
                errors.Add($"Duplicate skin ID: {duplicate.Key}.");
            }

            if (skins.Count(skin => skin.Id == 0) != 1)
                errors.Add("Skin catalog must contain exactly one default skin with ID 0.");

            var normalAddresses = new HashSet<string>();
            var skateboardAddresses = new HashSet<string>();
            foreach (SkinData skin in skins)
            {
                if (string.IsNullOrWhiteSpace(skin.SkinVisualAddress))
                {
                    errors.Add($"Skin {skin.Id}: SkinVisualAddress is empty.");
                }
                else if (!normalAddresses.Add(skin.SkinVisualAddress))
                {
                    errors.Add($"Duplicate normal visual address: {skin.SkinVisualAddress}.");
                }

                if (string.IsNullOrWhiteSpace(skin.SkateboardSkinVisualAddress))
                    continue;

                if (!skateboardAddresses.Add(skin.SkateboardSkinVisualAddress))
                    errors.Add($"Duplicate skateboard visual address: {skin.SkateboardSkinVisualAddress}.");
                if (GetSlug(skin.SkinVisualAddress) != GetSlug(skin.SkateboardSkinVisualAddress))
                    errors.Add($"Skin {skin.Id}: normal and skateboard visual slugs differ.");
            }
        }

        private static void ValidatePrefabs(
            ICollection<string> errors,
            IReadOnlyList<SkinData> skins)
        {
            foreach (SkinData skin in skins)
            {
                string normalSlug = GetSlug(skin.SkinVisualAddress);
                ValidatePrefab(
                    errors,
                    NormalVisualPrefabRoot,
                    normalSlug,
                    NormalActions,
                    false);

                if (string.IsNullOrWhiteSpace(skin.SkateboardSkinVisualAddress))
                    continue;

                string skateboardSlug = GetSlug(skin.SkateboardSkinVisualAddress);
                ValidatePrefab(
                    errors,
                    SkateboardVisualPrefabRoot,
                    skateboardSlug,
                    SkateboardActions,
                    true);
            }
        }

        private static void ValidatePrefab(
            ICollection<string> errors,
            string root,
            string slug,
            IReadOnlyList<SkinVisualAction> requiredActions,
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

            foreach (SkinVisualAction action in requiredActions)
            {
                if (visual.Mappings.All(mapping => mapping == null || mapping.Action != action))
                    errors.Add($"{slug}: mapping for {action} is missing.");
            }

            if (isSkateboard)
            {
                ValidateSkateboardJumpVariant(errors, visual, slug, SkinVisualVariant.Normal);
                ValidateSkateboardJumpVariant(errors, visual, slug, SkinVisualVariant.Super);
            }

            foreach (SkinVisualActionMapping mapping in visual.Mappings
                         .Where(mapping =>
                             mapping != null &&
                             requiredActions.Contains(mapping.Action)))
            {
                ValidateMapping(errors, mapping, controller, slug, isSkateboard);
            }
        }

        private static void ValidateSkateboardJumpVariant(
            ICollection<string> errors,
            SkinVisual visual,
            string slug,
            SkinVisualVariant variant)
        {
            bool exists = visual.Mappings.Any(mapping =>
                mapping != null &&
                mapping.Action == SkinVisualAction.SkateboardJump &&
                !mapping.MatchAnyVariant &&
                mapping.Variant == variant);
            if (!exists)
                errors.Add($"{slug}: SkateboardJump/{variant} mapping is missing.");
        }

        private static void ValidateMapping(
            ICollection<string> errors,
            SkinVisualActionMapping mapping,
            AnimatorController controller,
            string slug,
            bool isSkateboard)
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

            if (isSkateboard && !Mathf.Approximately(mapping.Clip.frameRate, 12f))
                errors.Add($"{slug}: clip '{mapping.Clip.name}' must use 12 FPS.");

            bool expectedLoop = mapping.Action is SkinVisualAction.SkateboardRideA
                or SkinVisualAction.SkateboardRideB
                or SkinVisualAction.SkateboardPush;
            if (isSkateboard && mapping.Loop != expectedLoop)
                errors.Add($"{slug}: {mapping.Action} loop flag is invalid.");
        }

        private static void ValidateHamster(ICollection<string> errors)
        {
            GameObject hamster = AssetDatabase.LoadAssetAtPath<GameObject>(HamsterPrefabPath);
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
            AddressableAssetGroup group = AddressableAssetSettingsDefaultObject.Settings?
                .FindGroup(VisualAddressablesGroup);
            BundledAssetGroupSchema schema = group?.GetSchema<BundledAssetGroupSchema>();
            if (schema == null || schema.BundleMode != BundledAssetGroupSchema.BundlePackingMode.PackSeparately)
            {
                errors.Add("Addressables group 'Skin Visuals' must use Pack Separately.");
                return;
            }

            foreach (SkinData skin in skins)
            {
                ValidateAddress(errors, group, skin.SkinVisualAddress);
                if (!string.IsNullOrWhiteSpace(skin.SkateboardSkinVisualAddress))
                    ValidateAddress(errors, group, skin.SkateboardSkinVisualAddress);
            }

            SkinData defaultSkin = skins.FirstOrDefault(skin => skin.Id == 0);
            if (defaultSkin == null ||
                string.IsNullOrWhiteSpace(defaultSkin.SkateboardSkinVisualAddress))
            {
                errors.Add("Default skin must define SkateboardSkinVisualAddress.");
            }
        }

        private static void ValidateAddress(
            ICollection<string> errors,
            AddressableAssetGroup group,
            string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                errors.Add("Normal SkinVisualAddress must not be empty.");
                return;
            }

            List<AddressableAssetEntry> entries = group.entries
                .Where(entry => entry.address == address)
                .ToList();
            if (entries.Count == 0)
            {
                errors.Add($"Addressable entry '{address}' is missing.");
                return;
            }
            if (entries.Count > 1)
            {
                errors.Add($"Addressable entry '{address}' is duplicated.");
                return;
            }

            string expectedPath = GetExpectedPrefabPath(address);
            string expectedGuid = AssetDatabase.AssetPathToGUID(expectedPath);
            string actualGuid = entries[0].guid;
            if (string.IsNullOrEmpty(expectedGuid) || actualGuid != expectedGuid)
                errors.Add($"Addressable entry '{address}' points to wrong prefab.");
        }

        private static string GetExpectedPrefabPath(string address)
        {
            bool isSkateboard = address.StartsWith(
                "skin-visual/skateboard/",
                StringComparison.Ordinal);
            string root = isSkateboard
                ? SkateboardVisualPrefabRoot
                : NormalVisualPrefabRoot;
            string slug = GetSlug(address);
            return $"{root}/{slug}/{slug}-skin-visual.prefab";
        }

        private static string GetSlug(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return string.Empty;

            int separatorIndex = address.LastIndexOf('/');
            return separatorIndex >= 0 ? address[(separatorIndex + 1)..] : address;
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
