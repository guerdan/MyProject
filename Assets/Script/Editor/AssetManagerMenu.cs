using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Script.Util;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using Object = UnityEngine.Object;

namespace Script.Editor
{

    public class AssetManager
    {
        private const string MenuItemPath = Utils.CustomToolsPath + "Addressable/";
        private const string RefreshSpriteAddressJsonFileString = "Refresh Sprite Address Json File";
        private const string Sprite2AtlasAddressPath = "Assets/AssetBundles/Other/Sprite2AtlasAddress.txt";
        private static readonly List<string> NotIncludeInAddressStrings = new List<string>
        {
            "Assets/AssetBundles/",
        };


        /// <summary>
        /// 刷新Sprite2AtlasAddress.txt文件。用于优化动态加载图集图片的流程，自动转换加载方式。
        /// </summary>
        [MenuItem(MenuItemPath + RefreshSpriteAddressJsonFileString)]
        public static void RefreshSpriteAddressJsonFile()
        {
            Debug.LogWarning(string.Format(Utils.StartOperationStringFormat, RefreshSpriteAddressJsonFileString));
            var spriteAtlasDictionary = new Dictionary<SpriteAtlas, AddressableAssetEntry>();
            var texture2DDictionary = new Dictionary<Texture2D, string>();
            var spriteAtlasType = typeof(SpriteAtlas);
            var texture2DType = typeof(Texture2D);
            var allAssetPaths = AssetDatabase.GetAllAssetPaths();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var dic = new Dictionary<string, string[]>();
            foreach (var assetPath in allAssetPaths)
            {
                // 查找Addressables下的资源
                var obj = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                if (obj == null)
                {
                    continue;
                }

                var type = obj.GetType();
                if (type == spriteAtlasType)
                {
                    var assetEntry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(assetPath));
                    if (assetEntry != null)
                        spriteAtlasDictionary.Add((SpriteAtlas)obj, assetEntry);
                }
                else if (type == texture2DType)
                {
                    texture2DDictionary.Add((Texture2D)obj, assetPath);
                }
            }

            foreach (var spriteAtlas in spriteAtlasDictionary)
            {
                foreach (var packable in spriteAtlas.Key.GetPackables())
                {
                    var packableAssetPath = $"{AssetDatabase.GetAssetPath(packable)}/";
                    foreach (var texture2DPath in texture2DDictionary.Values)
                    {
                        if (!texture2DPath.StartsWith(packableAssetPath))
                            continue;

                        var key = $"{AssetPathToAddressPath(texture2DPath)}";
                        string[] info = new string[2];
                        info[0] = spriteAtlas.Value.address;
                        info[1] = Path.GetFileNameWithoutExtension(texture2DPath);
                        dic.Add(key, info);
                    }
                }
            }

            File.WriteAllText(Sprite2AtlasAddressPath, JsonConvert.SerializeObject(dic));
            AssetDatabase.Refresh();
            Debug.LogWarning(string.Format(Utils.EndOperationStringFormat, RefreshSpriteAddressJsonFileString));
        }

        private static string AssetPathToAddressPath(string assetPath)
        {
            foreach (var str in NotIncludeInAddressStrings)
            {
                assetPath = assetPath.Replace(str, "");
            }
            assetPath = $"{Path.GetDirectoryName(assetPath)}/{Path.GetFileNameWithoutExtension(assetPath)}";
            assetPath = assetPath.Replace(@"\", "/");
            return assetPath;
        }

        [MenuItem(Utils.CustomToolsPath + "Build")]
        public static void BuildProject()
        {
            var folderName = "Auto";
            var projectName = "My project";

            var folderPath = $"Builds/{folderName}";
            PathUtil.DeleteDirectory(folderPath);
            PathUtil.DeleteDirectory($"{folderPath}_Run");

            var path = $"{folderPath}/{projectName}";
            // 设置输出路径
            string buildPath = $"{path}.exe";


            // 场景列表
            string[] scenes = new string[]
            {
            "Assets/Scenes/Auto/ExampleScene.unity", // 替换为你的场景路径
            };

            // 构建选项
            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = buildPath,
                target = BuildTarget.StandaloneWindows64, // 目标平台
                options = BuildOptions.None // 构建选项
            };

            // 执行打包
            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);

            // 检查打包结果
            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log("Build succeeded: " + report.summary.totalSize + " bytes");
            }
            else
            {
                Debug.LogError("Build failed: " + report.summary.result);
                return;
            }

            // 拷贝一份到_Run
            PathUtil.CopyDirectory(folderPath, $"{folderPath}_Run");


            // 剩下的用于打包，删除重复文件，减少传输耗时
            List<string> deleteFolders = new List<string>()
            {
                $"{path}_Data/StreamingAssets/Script",
                $"{path}_Data/StreamingAssets/SmallMap",
                $"{folderPath}/MonoBleedingEdge",
                $"{folderPath}/UnityCrashHandler64.exe",
                $"{folderPath}/UnityPlayer.dll",
                $"{path}_Data/Resources",
                $"{path}_Data/Plugins",
            };

            foreach (var deletePath in deleteFolders)
            {
                if (deletePath.IndexOf(".") >= 0)
                {
                    File.Delete(deletePath);
                }
                else
                    PathUtil.DeleteDirectory(deletePath);
            }

            var path1 = $"{path}_Data/Managed";
            foreach (var file in Directory.GetFiles(path1))
            {
                string name = Path.GetFileName(file);
                if (name == "Assembly-CSharp.dll" ||
                    name == "Assembly-CSharp-firstpass.dll")
                    continue;
                File.Delete(file);
            }
        }
    }
}