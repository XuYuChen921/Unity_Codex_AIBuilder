using System.IO;
using AIBuilder;
using UnityEditor;
using UnityEngine;

namespace AIBuilder.EditorTools
{
    public static class AIReferenceSpecCreator
    {
        private const string DataFolder = "Assets/AIBuilder/Data";
        private const string DefaultReferenceImagePath = "Assets/AIBuilder/References/00c0b5c0-1d08-4a11-94b2-db92ab1ed23a.png";
        private const string DefaultGeneratedSpecPath = "Assets/AIBuilder/Data/ForestHeartClearing_FromReference.json";

        [MenuItem("AIBuilder/Create V2 Spec From Selected Reference Image")]
        public static void CreateFromSelectedTexture()
        {
            var texture = Selection.activeObject as Texture2D;
            if (texture == null)
            {
                EditorUtility.DisplayDialog("AI Scene Builder", "Select a reference Texture2D in the Project window first.", "OK");
                return;
            }

            CreateForestHeartClearingSpec(texture, $"{DataFolder}/ForestHeartClearing_FromReference.json");
        }

        [MenuItem("AIBuilder/Create Default V2 Forest Spec")]
        public static void CreateDefaultForestHeartSpec()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(DefaultReferenceImagePath);
            if (texture == null)
            {
                EditorUtility.DisplayDialog("AI Scene Builder", $"Default reference image not found:\n{DefaultReferenceImagePath}", "OK");
                return;
            }

            CreateForestHeartClearingSpec(texture, DefaultGeneratedSpecPath);
        }

        [MenuItem("AIBuilder/Create Default V2 Forest Spec And Generate")]
        public static void CreateDefaultForestHeartSpecAndGenerate()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(DefaultReferenceImagePath);
            if (texture == null)
            {
                EditorUtility.DisplayDialog("AI Scene Builder", $"Default reference image not found:\n{DefaultReferenceImagePath}", "OK");
                return;
            }

            var spec = CreateForestHeartClearingSpec(texture, DefaultGeneratedSpecPath);
            if (spec != null)
            {
                AIReferenceSceneGenerator.GenerateFromTextAsset(spec, new AIReferenceSceneBuildOptions());
            }
        }

        public static TextAsset CreateForestHeartClearingSpec(Texture2D referenceImage, string outputPath)
        {
            if (referenceImage == null)
            {
                return null;
            }

            EnsureFolder("Assets", "AIBuilder");
            EnsureFolder("Assets/AIBuilder", "Data");

            var assetPath = AssetDatabase.GetAssetPath(referenceImage);
            var spec = CreateForestHeartClearingSpec(assetPath, referenceImage.width, referenceImage.height);
            var json = JsonUtility.ToJson(spec, true);

            File.WriteAllText(outputPath, json);
            AssetDatabase.ImportAsset(outputPath);
            AssetDatabase.Refresh();

            var created = AssetDatabase.LoadAssetAtPath<TextAsset>(outputPath);
            Selection.activeObject = created;
            Debug.Log($"AI Scene Builder created V2 SceneSpec from reference image: {outputPath}");
            return created;
        }

        public static SceneSpec CreateForestHeartClearingSpec(string referenceAssetPath, int width, int height)
        {
            return new SceneSpec
            {
                sceneName = "ForestHeartClearing_FromReference",
                generatedRootName = "AI_Generated_ForestHeartClearing",
                theme = "stylized_low_poly_forest_clearing",
                generatorVersion = "v2",
                seed = 20260502,
                referenceImage = new ReferenceImageSpec
                {
                    assetPath = referenceAssetPath,
                    width = width,
                    height = height,
                    analysisPreset = "forest_heart_clearing"
                },
                visualNotes = new[]
                {
                    "wide 16:9 stylized low-poly forest clearing",
                    "large open playable grass field framed by trees, shrubs, rocks, and flowers",
                    "rear-center circular altar with floating green glowing heart",
                    "warm upper-left sunlight, cyan sky, soft fog, and high color saturation",
                    "foreground corner glow pools and dense foliage frame the view"
                },
                camera = new CameraSpec
                {
                    position = new Vector3(0f, 8.2f, -17.5f),
                    lookAt = new Vector3(0f, 1.25f, 5.2f),
                    fieldOfView = 41f,
                    nearClipPlane = 0.1f,
                    farClipPlane = 140f
                },
                lighting = new LightingSpec
                {
                    sunEuler = new Vector3(36f, -38f, 0f),
                    sunColor = new Color(1f, 0.86f, 0.48f, 1f),
                    sunIntensity = 2.35f,
                    ambientSkyColor = new Color(0.58f, 0.82f, 0.78f, 1f),
                    ambientEquatorColor = new Color(0.48f, 0.62f, 0.35f, 1f),
                    ambientGroundColor = new Color(0.25f, 0.32f, 0.18f, 1f),
                    fogColor = new Color(0.68f, 0.9f, 0.82f, 1f),
                    fogDensity = 0.011f,
                    bloomIntensity = 0.62f
                },
                terrain = new TerrainSpec
                {
                    size = new Vector2(42f, 32f),
                    groundYOffset = 0f,
                    grassPatchCount = 105,
                    baseGrassColor = new Color(0.55f, 0.78f, 0.12f, 1f),
                    patchGrassA = new Color(0.67f, 0.84f, 0.16f, 1f),
                    patchGrassB = new Color(0.42f, 0.66f, 0.13f, 1f)
                },
                centralFeature = new CentralFeatureSpec
                {
                    position = new Vector3(0f, 0f, 7.4f),
                    altarRadius = 2.35f,
                    altarHeight = 0.62f,
                    heartHeight = 2.55f,
                    glowIntensity = 4.5f,
                    glowColor = new Color(0.47f, 1f, 0.08f, 1f)
                },
                scatterGroups =
                {
                    new ScatterGroupSpec
                    {
                        id = "rear_pines",
                        type = "pine_tree",
                        count = 24,
                        areaMin = new Vector3(-20f, 0f, 7f),
                        areaMax = new Vector3(20f, 0f, 15f),
                        scaleRange = new Vector2(0.9f, 1.55f),
                        edgeBias = 0.65f,
                        assetKeywords = new[] { "pine", "fir", "spruce", "tree" }
                    },
                    new ScatterGroupSpec
                    {
                        id = "side_broadleaf_trees",
                        type = "broadleaf_tree",
                        count = 14,
                        areaMin = new Vector3(-20.5f, 0f, -9.5f),
                        areaMax = new Vector3(20.5f, 0f, 9.5f),
                        scaleRange = new Vector2(0.95f, 1.65f),
                        edgeBias = 1f,
                        assetKeywords = new[] { "oak", "broadleaf", "tree", "big tree" }
                    },
                    new ScatterGroupSpec
                    {
                        id = "border_rocks",
                        type = "rock",
                        count = 44,
                        areaMin = new Vector3(-20.5f, 0f, -12.5f),
                        areaMax = new Vector3(20.5f, 0f, 13f),
                        scaleRange = new Vector2(0.55f, 1.8f),
                        edgeBias = 0.85f,
                        assetKeywords = new[] { "rock", "stone", "boulder" }
                    },
                    new ScatterGroupSpec
                    {
                        id = "border_bushes",
                        type = "bush",
                        count = 70,
                        areaMin = new Vector3(-20f, 0f, -13f),
                        areaMax = new Vector3(20f, 0f, 14f),
                        scaleRange = new Vector2(0.45f, 1.25f),
                        edgeBias = 0.95f,
                        assetKeywords = new[] { "bush", "shrub", "shrubs", "plant" }
                    },
                    new ScatterGroupSpec
                    {
                        id = "flowers",
                        type = "flower",
                        count = 115,
                        areaMin = new Vector3(-19f, 0f, -12f),
                        areaMax = new Vector3(19f, 0f, 12.5f),
                        scaleRange = new Vector2(0.65f, 1.25f),
                        edgeBias = 0.8f,
                        assetKeywords = new[] { "flower", "daisy", "plant" }
                    },
                    new ScatterGroupSpec
                    {
                        id = "background_mountains",
                        type = "mountain",
                        count = 8,
                        areaMin = new Vector3(-24f, 0f, 18f),
                        areaMax = new Vector3(24f, 0f, 28f),
                        scaleRange = new Vector2(1f, 2.2f),
                        edgeBias = 0f,
                        assetKeywords = new[] { "mountain", "hill", "cliff" }
                    }
                }
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
