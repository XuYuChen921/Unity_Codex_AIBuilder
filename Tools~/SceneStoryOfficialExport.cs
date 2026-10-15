// 仅用于独立发布工程，调用官方 Asset Store Tools；不随商品包分发。
using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public static class SceneStoryOfficialExport
{
    public static async void Run()
    {
        try
        {
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
            var assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "asset-store-tools-editor");
            var settingsType = assembly.GetType("AssetStoreTools.Exporter.DefaultExporterSettings");
            var settings = Activator.CreateInstance(settingsType);
            settingsType.GetField("OutputFilename").SetValue(settings, Environment.GetEnvironmentVariable("SCENE_STORY_EXPORT"));
            settingsType.GetField("ExportPaths").SetValue(settings, new[] { "Assets/SceneStory" });
            settingsType.GetField("Dependencies").SetValue(settings, new[] { "com.unity.timeline" });
            var exporterType = assembly.GetType("AssetStoreTools.Exporter.DefaultPackageExporter");
            var exporter = Activator.CreateInstance(exporterType, new[] { settings });
            var task = (Task)exporterType.GetMethod("Export").Invoke(exporter, null);
            await task;
            var result = task.GetType().GetProperty("Result").GetValue(task);
            if (!(bool)result.GetType().GetField("Success").GetValue(result))
                throw new Exception("Official export failed: " + result.GetType().GetField("Exception").GetValue(result));
            Debug.Log("SCENE_STORY_OFFICIAL_EXPORT_SUCCESS");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }
}
