using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AIBuilder.EditorTools.CreatureStage
{
    public static class StageClipNaming
    {
        public static void RenameSelected()
        {
            var paths = Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath)
                .SelectMany(p => AssetDatabase.IsValidFolder(p)
                    ? AssetDatabase.FindAssets("t:Model", new[] { p }).Select(AssetDatabase.GUIDToAssetPath)
                    : new[] { p });
            Debug.Log(Rename(paths.ToArray()));
        }

        public static string Rename(string[] paths)
        {
            var report = new StringBuilder();
            var backup = "Library/CreatureStage/ClipRenameBackup_" + DateTime.Now.ToString("yyyyMMdd_HHmmssfff");
            foreach (var path in paths.Distinct().OrderBy(p => p))
            {
                if (!path.StartsWith("Assets/", StringComparison.Ordinal) || !path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) continue;
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;
                var clips = importer.clipAnimations;
                if (clips.Length == 0) clips = importer.defaultClipAnimations;
                // 多动画文件不能都同名，保留人工分段名称。
                if (clips.Length != 1) { report.AppendLine("跳过多片段/无动画：" + path); continue; }
                var name = Path.GetFileNameWithoutExtension(path);
                if (clips[0].name == name) { report.AppendLine("已命名：" + path); continue; }
                var old = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
                if (old.Length != 1) throw new InvalidOperationException("子资产数量不明确：" + path);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(old[0], out string guid, out long id);
                var backupPath = backup + "/" + path + ".meta";
                Directory.CreateDirectory(Path.GetDirectoryName(backupPath));
                File.Copy(path + ".meta", backupPath);
                try
                {
                    clips[0].name = name;
                    importer.clipAnimations = clips;
                    // 显式保留旧动画子资产 ID，避免场景、Timeline、配置与 Controller 的引用断开。
                    var serialized = new SerializedObject(importer);
                    var table = serialized.FindProperty("m_InternalIDToNameTable");
                    if (table == null) table = serialized.FindProperty("internalIDToNameTable");
                    if (table == null) throw new InvalidOperationException("当前导入器没有子资产 ID 映射，未执行改名。");
                    var found = false;
                    for (var i = 0; i < table.arraySize; i++)
                    {
                        var item = table.GetArrayElementAtIndex(i);
                        if (item.FindPropertyRelative("first.second").longValue != id) continue;
                        item.FindPropertyRelative("second").stringValue = name;
                        found = true;
                    }
                    if (!found)
                    {
                        table.InsertArrayElementAtIndex(table.arraySize);
                        var item = table.GetArrayElementAtIndex(table.arraySize - 1);
                        item.FindPropertyRelative("first.first").intValue = 74;
                        item.FindPropertyRelative("first.second").longValue = id;
                        item.FindPropertyRelative("second").stringValue = name;
                    }
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    importer.SaveAndReimport();
                    var renamed = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single(c => c.name == name);
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renamed, out string newGuid, out long newId);
                    if (newGuid != guid || newId != id) throw new InvalidOperationException("动画引用 ID 发生变化。");
                    report.AppendLine("OK " + name + " | " + guid + ":" + id);
                }
                catch
                {
                    File.Copy(backupPath, path + ".meta", true);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    throw;
                }
            }
            report.AppendLine("导入设置备份：" + backup);
            return report.ToString();
        }
    }
}
