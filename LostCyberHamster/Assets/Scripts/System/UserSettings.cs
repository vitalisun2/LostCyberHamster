using Assets.Scripts.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Scripts.Common.Models;
using UnityEditor;
using UnityEngine.Networking;
using UnityEngine;
using LocationInfo = Assets.Scripts.Common.Models.LocationInfo;

namespace Assets.Scripts.System
{
    public static class UserSettings
    {
        private static readonly string FolderAndroid = "Android";
        private static readonly string FolderIOS = "iOS";
        private static readonly string FolderWindows = "Windows";
        private static readonly string FolderOSX = "OSX";

        private static string _catalogFilePath = $"{Consts.BaseSettingsPath}/catalog.json";
        private static Dictionary<int, CatalogBundle> CatalogDictionary = new();


        public static string CurrentLocation { get; set; }
        public static string CurrentLevel { get; set; }
        public static List<LocationInfo> Locations { get; set; } = new();

        /// <summary>
        /// Сохранение каталога локально на устройство
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        public static async Awaitable SaveLocalCatalogAsync(string json)
        {
           if (!Directory.Exists(Consts.BaseSettingsPath))
                Directory.CreateDirectory(Consts.BaseSettingsPath);
            await File.WriteAllTextAsync(_catalogFilePath, json);
        }

        /// <summary>
        /// Загрузка локального каталога (инициализация UserSettings)
        /// </summary>
        /// <returns></returns>
        public static async Awaitable LoadLocalCatalogAsync(bool downloadAllBundles = false)
        {
            CatalogDictionary.Clear();
            Locations.Clear();

           var json = await File.ReadAllTextAsync(_catalogFilePath);
            var catalog = JsonUtility.FromJson<Catalog>(json);

            foreach (var bundle in catalog.bundles)
            {
                UserSettings.CatalogDictionary.Add(bundle.name.GetHashCode(), bundle);
            }

            foreach (var locationInfo in catalog.locations)
            {
                UserSettings.Locations.Add(locationInfo);
            }
       }

        private static async Awaitable DownloadBundles()
        {
            foreach (var bundle in CatalogDictionary.Values)
            {
                var assetbundle = await GetAssetBundleAsync($"{Consts.BaseUrl}/{bundle.url}", Hash128.Parse(bundle.hash), bundle.crc);
                await assetbundle.UnloadAsync(false);
            }
        }

        public static string GetPlatformFolder()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.Android:
                    return FolderAndroid;
                case RuntimePlatform.IPhonePlayer:
                    return FolderIOS;
                case RuntimePlatform.WindowsEditor:
                    return FolderWindows;
                case RuntimePlatform.OSXEditor:
                    return FolderOSX;
                default:
                    return FolderAndroid;
            }
        }

        public static async Awaitable<AssetBundle> GetAssetBundleAsync(string bundleURL, Hash128 hash128, uint crc)
        {
            UnityWebRequest request = UnityWebRequestAssetBundle.GetAssetBundle(bundleURL, hash128, crc);

            await request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
               return null;
            }

            if (request.result == UnityWebRequest.Result.DataProcessingError)
            {
               return null;
            }

            if (request.result == UnityWebRequest.Result.ProtocolError)
            {
               return null;
            }


            var bundle = DownloadHandlerAssetBundle.GetContent(request);

            return bundle;
        }

        public static async Awaitable<AssetBundle> GetAssetBundleAsync(string name)
        {
            var key = name.GetHashCode();
            if (!UserSettings.CatalogDictionary.ContainsKey(key))
            {
                Debug.LogError("Ассет бандл не найден - " + name);
            }

            var bundle = UserSettings.CatalogDictionary[key];

            if (bundle is null)
            {
                Debug.LogError("Ассет бандл не загружен - " + name);
            }

            return await GetAssetBundleAsync($"{Consts.BaseUrl}/{bundle.url}", Hash128.Parse(bundle.hash), bundle.crc);
        }

#if UNITY_EDITOR
        public static string GetPlatformFolder(BuildTarget target)
        {

            switch (target)
            {
                case BuildTarget.Android:
                    return FolderAndroid;
                case BuildTarget.iOS:
                    return FolderIOS;
                case BuildTarget.StandaloneWindows:
                    return FolderWindows;
                case BuildTarget.StandaloneOSX:
                    return FolderOSX;
                default:
                    throw new NotSupportedException("Ошибка, платформа не поддерживается");
            }
        }

        /// <summary>
        /// Получение списка папок (без пути к ним) по заданному пути
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string[] GetSubfolderNames(string path)
        {
            return Directory.GetDirectories(path)
                            .Select(dir => Path.GetRelativePath(path, dir))
                            .ToArray();
        }
#endif

    }
}
