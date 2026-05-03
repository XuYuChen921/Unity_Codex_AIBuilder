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
        private string generatedSpecName = "SceneSpec_FromReference";

        [MenuItem("AI构建器/场景生成/参考图生成场景")]
        public static void Open()
        {
            GetWindow<AISceneBuilderWindow>("AI Scene Builder");
        }

        private void OnEnable()
        {
            specAsset = null;
            referenceImage = null;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("参考图生成场景", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            specAsset = (TextAsset)EditorGUILayout.ObjectField("场景配置 JSON", specAsset, typeof(TextAsset), false);
            useProjectAssets = EditorGUILayout.Toggle("使用项目资产", useProjectAssets);
            saveScene = EditorGUILayout.Toggle("保存生成场景", saveScene);
            createAssetReport = EditorGUILayout.Toggle("生成资产匹配报告", createAssetReport);
            seedOffset = EditorGUILayout.IntField("随机种子偏移", seedOffset);

            EditorGUILayout.Space(8f);

            using (new EditorGUI.DisabledScope(specAsset == null))
            {
                if (GUILayout.Button("根据配置生成场景", GUILayout.Height(32f)))
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

            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("从参考图创建空配置", EditorStyles.boldLabel);
            referenceImage = (Texture2D)EditorGUILayout.ObjectField("参考图", referenceImage, typeof(Texture2D), false);
            generatedSpecName = EditorGUILayout.TextField("输出配置名称", generatedSpecName);

            using (new EditorGUI.DisabledScope(referenceImage == null))
            {
                if (GUILayout.Button("创建干净场景配置"))
                {
                    var outputPath = $"Assets/AIBuilder/Data/{SanitizeFileName(generatedSpecName)}.json";
                    specAsset = AIReferenceSpecCreator.CreateSceneSpecTemplate(referenceImage, outputPath);
                }

                if (GUILayout.Button("创建配置并生成场景"))
                {
                    var outputPath = $"Assets/AIBuilder/Data/{SanitizeFileName(generatedSpecName)}.json";
                    specAsset = AIReferenceSpecCreator.CreateSceneSpecTemplate(referenceImage, outputPath);
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
                "这是干净入口：不会自动套用森林空地、爱心祭坛或默认参考图。先选参考图创建空 SceneSpec，再由 Codex 根据你的参考图补全世界分析、资产选择和摆放规则。",
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
