using System.IO;
using AIBuilder;
using UnityEditor;
using UnityEngine;

namespace AIBuilder.EditorTools
{
    public static class AIReferenceSpecCreator
    {
        private const string DataFolder = "Assets/AIBuilder/Data";

        [MenuItem("AI构建器/场景生成/选中图片生成场景配置")]
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

            EnsureFolder("Assets", "AIBuilder");
            EnsureFolder("Assets/AIBuilder", "Data");

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
                generatorVersion = "v2-template",
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
                    "Clean template only. Codex should fill world analysis from the supplied reference image.",
                    "No default forest clearing, altar, heart, magic pools, or foreground framing is enabled.",
                    "Use scatterGroups and feature toggles only after the reference image has been analyzed."
                },
                features = new SceneFeatureSpec
                {
                    centralFeature = false,
                    magicPools = false,
                    heroForegroundTrees = false,
                    clouds = false,
                    postProcessing = true
                },
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
