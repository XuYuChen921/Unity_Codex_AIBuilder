// 独立发布工程验证入口，不随商品导出；仅调用官方工具，不触碰用户工程。
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class SceneStoryStoreAudit
{
    [Serializable] private class Report { public string tool = "Unity Asset Store Tools 12.0.0"; public string category = "tools/animation"; public string status; public List<Row> tests = new List<Row>(); }
    [Serializable] private class Row { public string title; public string status; public List<string> messages = new List<string>(); }
    private static object Field(object o, string name) => o.GetType().GetField(name).GetValue(o);
    public static void Run()
    {
        var assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "asset-store-tools-editor");
        var type = assembly.GetType("AssetStoreTools.Validator.Data.CurrentProjectValidationSettings");
        var settings = Activator.CreateInstance(type);
        type.GetField("ValidationPaths").SetValue(settings, new List<string> { "Assets/SceneStory" });
        type.GetField("Category").SetValue(settings, "tools/animation");
        var vt = type.GetField("ValidationType"); vt.SetValue(settings, Enum.Parse(vt.FieldType, "UnityPackage"));
        var validatorType = assembly.GetType("AssetStoreTools.Validator.CurrentProjectValidator");
        var validator = Activator.CreateInstance(validatorType, new[] { settings });
        var result = validatorType.GetMethod("Validate").Invoke(validator, null);
        var report = new Report { status = Field(result, "Status").ToString() };
        foreach (var test in (IEnumerable)Field(result, "Tests"))
        {
            var details = Field(test, "Result");
            var row = new Row { title = (string)Field(test, "Title"), status = Field(details, "Status").ToString() };
            var count = (int)details.GetType().GetProperty("MessageCount").GetValue(details);
            for (int i = 0; i < count; i++)
            {
                var msg = details.GetType().GetMethod("GetMessage").Invoke(details, new object[] { i });
                row.messages.Add((string)msg.GetType().GetMethod("GetText").Invoke(msg, null));
            }
            report.tests.Add(row);
        }
        File.WriteAllText("StoreValidator.json", JsonUtility.ToJson(report, true));
        if (report.tests.Count == 0) throw new Exception("Official validator returned no tests: " + Field(result, "Exception"));
    }
}
