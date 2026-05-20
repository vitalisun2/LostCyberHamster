using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Scripts.Common.Models;
using Assets.Scripts.System;
using log4net.Core;
using UnityEditor;
using UnityEngine;
using LocationInfo = Assets.Scripts.Common.Models.LocationInfo;

public class AssetBundlesManager
{
    public const string contentDirectory = "Assets/Content";

    [MenuItem("Tools/Build Assets (Android)",priority = 2)]
    static void BuildAndroidAssetBundles()
    {
        BuildAssetBundle(BuildTarget.Android);
    }

    [MenuItem("Tools/Build Assets (IOS)", priority = 3)]
    static void BuildIosAssetBundles()
    {
        BuildAssetBundle(BuildTarget.iOS);
    }

    [MenuItem("Tools/Build Assets (Editor only)", priority = 1)]
    static void BuildEditorAssetBundles()
    {
        if(Application.platform == RuntimePlatform.OSXEditor)
            BuildAssetBundle(BuildTarget.StandaloneOSX);
        if(Application.platform == RuntimePlatform.WindowsEditor)
            BuildAssetBundle(BuildTarget.StandaloneWindows);
    }

    static void BuildAssetBundle(BuildTarget target)
    {
        var folder = UserSettings.GetPlatformFolder(target);

        var assetBundleDirectory = $"Assets/AssetBundles/{folder}";

        if (!Directory.Exists(assetBundleDirectory))
        {        
            Directory.CreateDirectory(assetBundleDirectory);
        }

        DirectoryInfo directory = new DirectoryInfo(assetBundleDirectory);

        foreach (FileInfo file in directory.GetFiles())
        {
            file.Delete();
        }

        SetAssetNames();

        BuildPipeline.BuildAssetBundles(assetBundleDirectory, BuildAssetBundleOptions.None, target);
        //await Awaitable.WaitForSecondsAsync(2);
        CreateCatalog(target);
        AssetDatabase.Refresh();
    }

    private static void SetAssetNames()
    {
        var directoriesAndBindlesNames = new Dictionary<string, string>()
        {
            { $"{contentDirectory}/ui/uxml", "ui" },
            { $"{contentDirectory}/ui/styles", "ui" },
            { $"{contentDirectory}/ui/sprites", "ui" },
            { $"{contentDirectory}/ui/audio", "ui-audio" },
            { $"{contentDirectory}/locations/previews", "location-previews" },
            { $"{contentDirectory}/prefabs", "prefabs" },
            { $"{contentDirectory}/shared/sprites", "shared-sprites" }
        };

        var locations = UserSettings.GetSubfolderNames($"{contentDirectory}/locations")
                             .Where(x => x.Contains('_'))
                             .OrderBy(x => x);

        foreach(var location in locations)
        {
            directoriesAndBindlesNames.Add($"{contentDirectory}/locations/{location}/audio", $"{location}-audio");
            directoriesAndBindlesNames.Add($"{contentDirectory}/locations/{location}/levels", $"{location}-levels");
            directoriesAndBindlesNames.Add($"{contentDirectory}/locations/{location}/sprites", $"{location}-sprites");
        }

        foreach(var dir in directoriesAndBindlesNames.Keys)
        {
            if (!Directory.Exists(dir))
            {
                Debug.LogWarning($"Asset bundle source directory does not exist, skipping: {dir}");
                continue;
            }

            var files = Directory.GetFiles(dir).Where(x=> !x.EndsWith(".meta"));

            foreach(var file in files)
            {
                AssetImporter.GetAtPath(file)?.SetAssetBundleNameAndVariant(directoriesAndBindlesNames[dir], string.Empty);
            }
        }
    }

    private static void CreateCatalog(BuildTarget target)
    {
        var folder = UserSettings.GetPlatformFolder(target);
        var folderPath = $"Assets/AssetBundles/{folder}";

        var catalogBundles = new List<CatalogBundle>();

        string[] files = Directory.GetFiles(folderPath);

        foreach (string filePath in files)
        {
            if (Path.GetExtension(filePath) == string.Empty && !filePath.EndsWith(folder))
                File.Move(filePath, $"{filePath}.unity3d");
        }

        files = Directory.GetFiles(folderPath);

        foreach (string filePath in files)
        {
            if (Path.GetExtension(filePath) != ".unity3d")
                continue;

            string fileName = Path.GetFileNameWithoutExtension(filePath);
            string manifestPath = Path.Combine(folderPath, fileName + ".manifest");

            if (File.Exists(manifestPath))
            {
                var manifest = ParseManifest(manifestPath);
                var item = new CatalogBundle
                {
                    name = fileName,
                    version = "1.0",
                    crc = manifest.crc,
                    url = fileName+ ".unity3d",
                    hash = manifest.Hash,
                    assets = manifest.Assets.ToArray()
                };
                catalogBundles.Add(item);

                File.Delete(manifestPath);
            }

            string maifestMetaPath= Path.Combine(folderPath, fileName + ".manifest.meta");
            if (File.Exists(maifestMetaPath))
                File.Delete(manifestPath);

            string assetMetaPath = Path.Combine(folderPath, fileName + ".unity3d.meta");
            if (File.Exists(assetMetaPath))
                File.Delete(assetMetaPath);
        }


        

        
        var locations = UserSettings.GetSubfolderNames($"{contentDirectory}/locations")
                             .Where(x => x.Contains('_'))
                             .OrderBy(x => x)
                             .Select(x => new LocationInfo()
                             {
                                 name = x.Substring(3).Replace("_", " "),
                                 sysname = x
                             })
                             .ToArray();

        foreach (var location in locations)
        {
            var levelsArray = new List<string>();
            var levels = catalogBundles.FirstOrDefault(x => x.name == $"{location.sysname.ToLower()}-levels")?.assets ?? new string[] { };
            foreach(var level in levels)
            {
                levelsArray.Add(level.Substring(level.Length - 7, 2));
            }

            location.levels = levelsArray.ToArray();
        }
        
        string catalogJson = JsonUtility.ToJson(new Catalog()
        {
            bundles = catalogBundles.ToArray(),
            locations = locations
        });

        File.WriteAllText(Path.Combine(folderPath, "catalog.json"), catalogJson);

    }

    public static (string Hash, List<string> Assets, uint crc) ParseManifest(string filePath)
    {
        uint crc = 0;
        string hash = string.Empty;
        bool findAssetFileHash = false;
        bool findAssets = false;
        List<string> assets = new List<string>();

        var lines = File.ReadAllLines(filePath);
        foreach(var line in lines)
        {
            if (line.StartsWith("CRC:"))
            {
                var crcStr = line.Split(" ")[1];
                crc = UInt32.Parse(crcStr);
                continue;
            }

            if (line.Trim().StartsWith("AssetFileHash:"))
            {
                findAssetFileHash = true;
                continue;
            }

            if (line.Trim().StartsWith("Hash:") && findAssetFileHash)
            {
                findAssetFileHash = false;
                hash = line.Trim().Split(" ")[1];
                continue;
            }

            if (line.StartsWith("Assets:"))
            {
                findAssets = true;
                continue;
            }

            if(line.Trim().StartsWith("- ") && findAssets)
            {
                assets.Add(line.Split(" ")[1]);
                continue;
            }
            else
            {
                findAssets = false;
            }

        }


        /*
        string crcPattern = @"CRC:\s*([0-9]+)";
        string assetFileHashPattern = @"AssetFileHash:\s*\n\s*Hash:\s*([a-fA-F0-9]+)";
        string assetsPattern = @"Assets:\s*\n((?:-\s*[\w./]+(?:\s*\n)*)+)";

        Match crcMatch = Regex.Match(text, crcPattern);
        Match assetHashMatch = Regex.Match(text, assetFileHashPattern);
        Match assetsMatch = Regex.Match(text, assetsPattern);

        var crc = crcMatch.Success ? UInt32.Parse(crcMatch.Groups[1].Value) : 0;
        string assetHash = assetHashMatch.Success ? assetHashMatch.Groups[1].Value : null;

        List<string> assets = new List<string>();
        if (assetsMatch.Success)
        {
            string assetsText = assetsMatch.Groups[1].Value;
            string[] assetLines = assetsText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string assetLine in assetLines)
            {
                string asset = assetLine.TrimStart('-', ' ').Trim();
                assets.Add(asset);
            }
        }
        */
        return (hash, assets.OrderBy(x=>x).ToList(), crc);
    }
}
