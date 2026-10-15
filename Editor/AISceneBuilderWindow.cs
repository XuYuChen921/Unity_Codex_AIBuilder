using UnityEditor;
using UnityEngine;

namespace AIBuilder.EditorTools
{
    public sealed class AISceneBuilderWindow : EditorWindow
    {
        private TextAsset specAsset;
        private Texture2D referenceImage;
        private bool useProjectAssets = true;
        private bool createAssetReport;
        private bool overwriteGeneratedAssets;
        private int seedOffset;
        private string generatedSpecName = "SceneSpec_FromReference";
        private string lastReviewFolder;

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
            createAssetReport = EditorGUILayout.Toggle("保存时生成资产匹配报告", createAssetReport);
            overwriteGeneratedAssets = EditorGUILayout.Toggle("允许覆盖同名生成资产", overwriteGeneratedAssets);
            seedOffset = EditorGUILayout.IntField("随机种子偏移", seedOffset);

            EditorGUILayout.Space(8f);

            using (new EditorGUI.DisabledScope(specAsset == null))
            {
                if (GUILayout.Button("预览场景（不写入资产）", GUILayout.Height(32f)))
                {
                    AIReferenceSceneGenerator.GenerateFromTextAsset(specAsset, CreateBuildOptions(false));
                }

                if (GUILayout.Button("预览并生成 Codex 评审包", GUILayout.Height(28f)))
                {
                    var root = AIReferenceSceneGenerator.GenerateFromTextAsset(specAsset, CreateBuildOptions(false));
                    if (root != null)
                    {
                        try
                        {
                            var review = AIBuilderReviewBundleBuilder.Create(
                                specAsset,
                                root,
                                AIReferenceSceneGenerator.LastExecutionReport);
                            lastReviewFolder = review.FolderPath;
                        }
                        catch (System.Exception exception)
                        {
                            Debug.LogException(exception);
                            EditorUtility.DisplayDialog("AI Scene Builder", "生成 Codex 评审包失败：\n" + exception.Message, "确定");
                        }
                    }
                }

                if (GUILayout.Button("确认生成并保存场景", GUILayout.Height(28f)) &&
                    EditorUtility.DisplayDialog(
                        "AI Scene Builder",
                        "将创建场景和相关生成资产。是否继续？",
                        "生成并保存",
                        "取消"))
                {
                    AIReferenceSceneGenerator.GenerateFromTextAsset(specAsset, CreateBuildOptions(true));
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

                if (GUILayout.Button("创建配置并预览场景"))
                {
                    var outputPath = $"Assets/AIBuilder/Data/{SanitizeFileName(generatedSpecName)}.json";
                    specAsset = AIReferenceSpecCreator.CreateSceneSpecTemplate(referenceImage, outputPath);
                    if (specAsset != null)
                    {
                        AIReferenceSceneGenerator.GenerateFromTextAsset(specAsset, CreateBuildOptions(false));
                    }
                }
            }

            EditorGUILayout.Space(8f);
            if (!string.IsNullOrWhiteSpace(lastReviewFolder))
            {
                EditorGUILayout.HelpBox("最近评审包：\n" + lastReviewFolder, MessageType.None);
            }

            EditorGUILayout.HelpBox(
                "默认只预览，不写入场景或材质资产。Codex 评审包会以唯一目录写入 Library/AIBuilder/Reviews，包含主视角、侧视角、俯视角、Schema 和资产回退报告。",
                MessageType.Info);
        }

        private AIReferenceSceneBuildOptions CreateBuildOptions(bool saveScene)
        {
            return new AIReferenceSceneBuildOptions
            {
                UseProjectAssets = useProjectAssets,
                SaveScene = saveScene,
                CreateAssetReport = saveScene && createAssetReport,
                OverwriteGeneratedAssets = saveScene && overwriteGeneratedAssets,
                SeedOffset = seedOffset
            };
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
