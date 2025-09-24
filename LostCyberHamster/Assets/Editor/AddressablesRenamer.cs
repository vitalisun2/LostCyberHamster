using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

public static class AddressablesRenamer
{
    private const string MenuPath = "Tools/Content/Rename Level Addresses";
    private const string LevelsGroupName = "Levels";

    [MenuItem(MenuPath, false, 1010)]
    public static void RenameLevelAddresses()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            EditorUtility.DisplayDialog("Addressables Renamer", "Addressable settings not found.", "OK");
            return;
        }

        var group = settings.groups.FirstOrDefault(g => g != null && g.Name == LevelsGroupName);
        if (group == null)
        {
            EditorUtility.DisplayDialog("Addressables Renamer", $"Group '{LevelsGroupName}' not found.", "OK");
            return;
        }

        var entries = group.entries.ToArray();
        var updatedCount = 0;

        foreach (var entry in entries)
        {
            if (entry == null)
            {
                continue;
            }

            if (!TryBuildAddress(entry.AssetPath, out var locationId, out var partOfDay, out var address))
            {
                Debug.LogWarning($"[AddressablesRenamer] Unable to build address for '{entry.AssetPath}'.");
                continue;
            }

            if (!string.Equals(entry.address, address, StringComparison.Ordinal))
            {
                entry.address = address;
                updatedCount++;
            }

            entry.SetLabel($"Location_{locationId}", true, true);
            entry.SetLabel($"PartOfDay_{partOfDay}", true, true);
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entry, true);
        }

        if (updatedCount > 0)
        {
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        EditorUtility.DisplayDialog(
            "Addressables Renamer",
            $"Processed {entries.Length} entries. Updated {updatedCount} addresses.",
            "OK");
    }

    private static bool TryBuildAddress(string assetPath, out string locationId, out string partOfDay, out string address)
    {
        locationId = string.Empty;
        partOfDay = string.Empty;
        address = string.Empty;

        if (string.IsNullOrEmpty(assetPath))
        {
            return false;
        }

        var normalizedPath = assetPath.Replace('\\', '/');
        var segments = normalizedPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        var locationIndex = Array.FindIndex(segments, s => string.Equals(s, "locations", StringComparison.OrdinalIgnoreCase));

        if (locationIndex < 0 || locationIndex + 3 > segments.Length)
        {
            return false;
        }

        var locationSegment = segments[locationIndex + 1];
        var partSegment = segments[locationIndex + 2];
        var fileName = Path.GetFileName(normalizedPath);

        if (string.IsNullOrEmpty(fileName))
        {
            return false;
        }

        locationId = NormalizeLocation(locationSegment);
        partOfDay = partSegment;
        address = $"{locationId}/{partSegment.ToLowerInvariant()}/{fileName}";
        return true;
    }

    private static string NormalizeLocation(string segment)
    {
        if (string.IsNullOrEmpty(segment))
        {
            return string.Empty;
        }

        var index = 0;
        while (index < segment.Length && char.IsDigit(segment[index]))
        {
            index++;
        }

        if (index < segment.Length && segment[index] == '_')
        {
            index++;
        }

        var trimmed = segment.Substring(index);
        return trimmed.Replace(' ', '_').ToLowerInvariant();
    }
}
