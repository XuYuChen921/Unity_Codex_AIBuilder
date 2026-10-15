using System;
using System.IO;
using AIBuilder;
using UnityEditor;
using UnityEngine;

namespace AIBuilder.EditorTools
{
    public enum AIBlockoutKind
    {
        Scene,
        Object
    }

    public sealed class AIBlockoutSpecContext
    {
        public TextAsset Asset;
        public string AssetPath;
        public ProBuilderModelSpec Spec;
        public AIBlockoutKind Kind;
        public DateTime LastWriteUtc;

        public string DisplayName => string.IsNullOrWhiteSpace(Spec?.displayName)
            ? Spec?.modelName ?? "未命名白盒"
            : Spec.displayName;
    }

    public static class AIBlockoutSpecRepository
    {
        public const string DataFolder = "Assets/AIBuilder/Data";
        public const string SceneBlockout = "scene_blockout";
        public const string ObjectBlockout = "object_blockout";

        public static AIBlockoutSpecContext FindLatest(AIBlockoutKind kind)
        {
            if (!AssetDatabase.IsValidFolder(DataFolder))
            {
                return null;
            }

            AIBlockoutSpecContext latest = null;
            foreach (var guid in AssetDatabase.FindAssets("t:TextAsset", new[] { DataFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (!TryLoad(asset, out var candidate, out _) || candidate.Kind != kind)
                {
                    continue;
                }

                if (latest == null || candidate.LastWriteUtc > latest.LastWriteUtc)
                {
                    latest = candidate;
                }
            }

            return latest;
        }

        public static AIBlockoutSpecContext FindLatestAny()
        {
            var scene = FindLatest(AIBlockoutKind.Scene);
            var model = FindLatest(AIBlockoutKind.Object);
            if (scene == null)
            {
                return model;
            }

            return model == null || scene.LastWriteUtc >= model.LastWriteUtc ? scene : model;
        }

        public static bool TryLoad(TextAsset asset, out AIBlockoutSpecContext context, out string error)
        {
            context = null;
            error = string.Empty;
            if (asset == null)
            {
                error = "白盒配置为空。";
                return false;
            }

            ProBuilderModelSpec spec;
            try
            {
                spec = JsonUtility.FromJson<ProBuilderModelSpec>(asset.text);
            }
            catch (Exception exception)
            {
                error = "白盒配置 JSON 解析失败：" + exception.Message;
                return false;
            }

            if (spec == null || !TryParseKind(spec.blockoutType, out var kind))
            {
                error = $"blockoutType 必须是 {SceneBlockout} 或 {ObjectBlockout}。";
                return false;
            }

            var outputFolder = string.IsNullOrWhiteSpace(spec.outputFolder)
                ? "Assets/AIBuilder/GeneratedModels"
                : spec.outputFolder;
            var validation = AIBuilderSpecValidator.ValidateModelSpec(spec, outputFolder);
            if (!validation.IsValid)
            {
                error = validation.ToDisplayString();
                return false;
            }

            if (kind == AIBlockoutKind.Object && !spec.savePrefab)
            {
                error = "物体白盒需要将 savePrefab 设为 true。";
                return false;
            }

            var assetPath = AssetDatabase.GetAssetPath(asset);
            context = new AIBlockoutSpecContext
            {
                Asset = asset,
                AssetPath = assetPath,
                Spec = spec,
                Kind = kind,
                LastWriteUtc = GetLastWriteUtc(assetPath)
            };
            return true;
        }

        public static string SanitizeFileName(string value)
        {
            value = string.IsNullOrWhiteSpace(value) ? "Blockout" : value.Trim();
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return value.Replace(' ', '_');
        }

        private static bool TryParseKind(string value, out AIBlockoutKind kind)
        {
            if (string.Equals(value, SceneBlockout, StringComparison.OrdinalIgnoreCase))
            {
                kind = AIBlockoutKind.Scene;
                return true;
            }

            if (string.Equals(value, ObjectBlockout, StringComparison.OrdinalIgnoreCase))
            {
                kind = AIBlockoutKind.Object;
                return true;
            }

            kind = default;
            return false;
        }

        private static DateTime GetLastWriteUtc(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return DateTime.MinValue;
            }

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var absolutePath = Path.GetFullPath(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
            return File.Exists(absolutePath) ? File.GetLastWriteTimeUtc(absolutePath) : DateTime.MinValue;
        }
    }
}
