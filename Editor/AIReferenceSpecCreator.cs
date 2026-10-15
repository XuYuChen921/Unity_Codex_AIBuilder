using System.IO;
using AIBuilder;
using UnityEditor;
using UnityEngine;

namespace AIBuilder.EditorTools
{
    public static class AIReferenceSpecCreator
    {
        private const string DataFolder = "Assets/AIBuilder/Data";

        public static void CreateFromSelectedTexture()
        {
            var texture = Selection.activeObject as Texture2D;
            if (texture == null)
            {
                EditorUtility.DisplayDialog("AI Scene Builder", "Select a reference Texture2D in the Project window first.", "OK");
                return;
            }

            CreateSceneSpecTemplate(texture, $"{DataFolder}/SceneSpec_FromReference.json");
        }

        public static TextAsset CreateSceneSpecTemplate(Texture2D referenceImage, string outputPath)
        {
            if (referenceImage == null)
            {
                return null;
            }

            outputPath = string.IsNullOrWhiteSpace(outputPath) ? string.Empty : outputPath.Trim().Replace('\\', '/');
            var outputFolder = Path.GetDirectoryName(outputPath)?.Replace('\\', '/');
            if (!string.Equals(Path.GetExtension(outputPath), ".json", System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError($"AI Scene Builder 配置输出路径必须是 Assets 下的 .json 文件：\n{outputPath}");
                return null;
            }

            if (!AIBuilderAssetPathUtility.TryNormalizeAssetsFolder(outputFolder, out outputFolder, out var pathError))
            {
                Debug.LogError($"AI Scene Builder 配置输出路径无效：{pathError}\n{outputPath}");
                return null;
            }

            AIBuilderAssetPathUtility.EnsureAssetFolder(outputFolder);
            outputPath = AssetDatabase.GenerateUniqueAssetPath(outputPath);

            var assetPath = AssetDatabase.GetAssetPath(referenceImage);
            var spec = CreateSceneSpecTemplate(assetPath, referenceImage.width, referenceImage.height);
            var json = JsonUtility.ToJson(spec, true);

            File.WriteAllText(outputPath, json);
            AssetDatabase.ImportAsset(outputPath);
            AssetDatabase.Refresh();

            var created = AssetDatabase.LoadAssetAtPath<TextAsset>(outputPath);
            Selection.activeObject = created;
            Debug.Log($"AI Scene Builder created clean SceneSpec template from reference image: {outputPath}");
            return created;
        }

        public static SceneSpec CreateSceneSpecTemplate(string referenceAssetPath, int width, int height)
        {
            return new SceneSpec
            {
                sceneName = "ReferenceScene",
                generatedRootName = "AI_Generated_Scene",
                theme = "custom_reference_scene",
                schemaVersion = 2,
                generatorVersion = "scene_v2-template",
                seed = 20260503,
                referenceImage = new ReferenceImageSpec
                {
                    assetPath = referenceAssetPath,
                    width = width,
                    height = height,
                    analysisPreset = "custom_reference_scene"
                },
                visualNotes = new[]
                {
                    "Clean template only. A planner or artist should fill world analysis from the supplied reference image.",
                    "No default forest clearing, altar, heart, magic pools, or foreground framing is enabled.",
                    "Prefer exact prefab GUID or path bindings; enable keyword/procedural fallback only when intended."
                },
                terrain = new TerrainSpec
                {
                    enabled = false,
                    createColorPatches = false,
                    grassPatchCount = 0
                },
                features = new SceneFeatureSpec
                {
                    centralFeature = false,
                    magicPools = false,
                    heroForegroundTrees = false,
                    clouds = false,
                    postProcessing = false,
                    fog = false
                },
                objects = new System.Collections.Generic.List<SceneObjectSpec>(),
                scatterGroups = new System.Collections.Generic.List<ScatterGroupSpec>()
            };
        }

        private static void EnsureFolder(string parent, string child)
        {
            var path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
