using UnityEditor;
using UnityEngine;

namespace AIBuilder.EditorTools
{
    public sealed class AISceneBuilderWindow : EditorWindow
    {
        private TextAsset specAsset;
        private Texture2D referenceImage;
        private bool useProjectAssets = true;
        private bool saveScene = true;
        private bool createAssetReport = true;
        private int seedOffset;
        private string generatedSpecName = "ForestHeartClearing_FromReference";

        [MenuItem("AIBuilder/Scene From Reference")]
        public static void Open()
        {
            GetWindow<AISceneBuilderWindow>("AI Scene Builder");
        }

        private void OnEnable()
        {
            specAsset = AIReferenceSceneGenerator.LoadDefaultSceneSpec();
            referenceImage = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AIBuilder/References/00c0b5c0-1d08-4a11-94b2-db92ab1ed23a.png");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Reference Scene Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            specAsset = (TextAsset)EditorGUILayout.ObjectField("Scene Spec", specAsset, typeof(TextAsset), false);
            useProjectAssets = EditorGUILayout.Toggle("Use Project Assets", useProjectAssets);
            saveScene = EditorGUILayout.Toggle("Save Generated Scene", saveScene);
            createAssetReport = EditorGUILayout.Toggle("Create Asset Report", createAssetReport);
            seedOffset = EditorGUILayout.IntField("Seed Offset", seedOffset);

            EditorGUILayout.Space(8f);

            using (new EditorGUI.DisabledScope(specAsset == null))
            {
                if (GUILayout.Button("Generate Scene", GUILayout.Height(32f)))
                {
                    AIReferenceSceneGenerator.GenerateFromTextAsset(
                        specAsset,
                        new AIReferenceSceneBuildOptions
                        {
                            UseProjectAssets = useProjectAssets,
                            SaveScene = saveScene,
                            CreateAssetReport = createAssetReport,
                            SeedOffset = seedOffset
                        });
                }
            }

            if (GUILayout.Button("Generate Forest Heart Clearing Preset"))
            {
                AIReferenceSceneGenerator.GenerateForestHeartClearingFromMenu();
            }

            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("V2 Reference Image Spec", EditorStyles.boldLabel);
            referenceImage = (Texture2D)EditorGUILayout.ObjectField("Reference Image", referenceImage, typeof(Texture2D), false);
            generatedSpecName = EditorGUILayout.TextField("Output Spec Name", generatedSpecName);

            using (new EditorGUI.DisabledScope(referenceImage == null))
            {
                if (GUILayout.Button("Create SceneSpec From Reference Image"))
                {
                    var outputPath = $"Assets/AIBuilder/Data/{SanitizeFileName(generatedSpecName)}.json";
                    specAsset = AIReferenceSpecCreator.CreateForestHeartClearingSpec(referenceImage, outputPath);
                }

                if (GUILayout.Button("Create Spec And Generate Scene"))
                {
                    var outputPath = $"Assets/AIBuilder/Data/{SanitizeFileName(generatedSpecName)}.json";
                    specAsset = AIReferenceSpecCreator.CreateForestHeartClearingSpec(referenceImage, outputPath);
                    if (specAsset != null)
                    {
                        AIReferenceSceneGenerator.GenerateFromTextAsset(
                            specAsset,
                            new AIReferenceSceneBuildOptions
                            {
                                UseProjectAssets = useProjectAssets,
                                SaveScene = saveScene,
                                CreateAssetReport = createAssetReport,
                                SeedOffset = seedOffset
                            });
                    }
                }
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "V1 scans project prefabs by semantic type and replaces procedural fallback objects when matching assets exist. V2 creates editable SceneSpec JSON from a reference image asset.",
                MessageType.Info);
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "GeneratedSceneSpec";
            }

            foreach (var invalid in System.IO.Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return value.Trim();
        }
    }
}
