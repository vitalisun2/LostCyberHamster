#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.GameEngine.Skins;
using UnityEditor;
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
        private const string VisualPrefabRoot = "Assets/Content/prefabs/skins/normal_mode";
        private const string VisualAddressablesGroup = "Skin Visuals";

        private static readonly string[] SkinSlugs =
        {
            "default",
            "neon-runner",
            "quantum-scout",
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
            ValidatePrefabs(errors);
            ValidateHamster(errors);
            ValidateAddressables(errors);
            return errors;
        }

        private static void ValidatePrefabs(ICollection<string> errors)
        {
            foreach (string slug in SkinSlugs)
            {
                string prefabPath = $"{VisualPrefabRoot}/{slug}/{slug}-skin-visual.prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                SkinVisual visual = prefab != null ? prefab.GetComponent<SkinVisual>() : null;
                if (visual == null)
                {
                    errors.Add($"Missing SkinVisual prefab: {prefabPath}");
                    continue;
                }

                foreach (SkinVisualAction action in Enum.GetValues(typeof(SkinVisualAction)))
                {
                    if (visual.Mappings.All(mapping => mapping.Action != action))
                        errors.Add($"{slug}: mapping for {action} is missing.");
                }
            }
        }

        private static void ValidateHamster(ICollection<string> errors)
        {
            GameObject hamster = AssetDatabase.LoadAssetAtPath<GameObject>(HamsterPrefabPath);
            Transform collisionBody = FindChild(hamster, "collision_body");
            Transform skinSlot = FindChild(hamster, "skin_slot");
            if (collisionBody == null || collisionBody.GetComponent<BoxCollider2D>() == null)
                errors.Add("Hamster collision_body with BoxCollider2D is missing.");
            if (collisionBody != null && collisionBody.GetComponent<SpriteRenderer>() != null)
                errors.Add("Hamster collision_body still owns SpriteRenderer.");
            if (collisionBody != null && collisionBody.GetComponent<Animator>() != null)
                errors.Add("Hamster collision_body still owns visual Animator.");
            if (skinSlot == null || skinSlot.GetComponent<SkinVisualHost>() == null)
                errors.Add("Hamster skin_slot with SkinVisualHost is missing.");
        }

        private static void ValidateAddressables(ICollection<string> errors)
        {
            AddressableAssetGroup group = AddressableAssetSettingsDefaultObject.Settings?
                .FindGroup(VisualAddressablesGroup);
            BundledAssetGroupSchema schema = group?.GetSchema<BundledAssetGroupSchema>();
            if (schema == null || schema.BundleMode != BundledAssetGroupSchema.BundlePackingMode.PackSeparately)
            {
                errors.Add("Addressables group 'Skin Visuals' must use Pack Separately.");
                return;
            }

            foreach (string slug in SkinSlugs)
            {
                string expectedAddress = $"skin-visual/{slug}";
                if (group.entries.All(entry => entry.address != expectedAddress))
                    errors.Add($"Addressable entry '{expectedAddress}' is missing.");
            }
        }

        private static Transform FindChild(GameObject root, string childName)
        {
            if (root == null)
                return null;

            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.name == childName);
        }
    }
}
#endif
