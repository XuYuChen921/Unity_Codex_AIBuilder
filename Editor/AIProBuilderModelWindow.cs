using UnityEditor;
using UnityEngine;

namespace AIBuilder.EditorTools
{
    public sealed class AIProBuilderModelWindow : EditorWindow
    {
        private TextAsset specAsset;
        private bool replaceExisting;
        private bool overwriteExistingPrefab;
        private bool selectGeneratedRoot = true;
        private string outputFolder = "Assets/AIBuilder/GeneratedModels";

        public static void Open()
        {
            GetWindow<AIProBuilderModelWindow>("AI ProBuilder Model");
        }

        private void OnEnable()
        {
            specAsset = AIProBuilderModelGenerator.LoadDefaultModelSpec();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("参考图模型转 ProBuilder", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            specAsset = (TextAsset)EditorGUILayout.ObjectField("模型配置 JSON", specAsset, typeof(TextAsset), false);
            replaceExisting = EditorGUILayout.Toggle("替换同名场景根节点", replaceExisting);
            overwriteExistingPrefab = EditorGUILayout.Toggle("允许覆盖同名 Prefab", overwriteExistingPrefab);
            selectGeneratedRoot = EditorGUILayout.Toggle("选中生成根节点", selectGeneratedRoot);
            outputFolder = EditorGUILayout.TextField("输出目录", outputFolder);

            EditorGUILayout.Space(8f);

            using (new EditorGUI.DisabledScope(specAsset == null))
            {
                if (GUILayout.Button("预览 ProBuilder 模型（不写入资产）", GUILayout.Height(32f)))
                {
                    AIProBuilderModelGenerator.GenerateFromTextAsset(specAsset, CreateBuildOptions(false));
                }

                if (GUILayout.Button("确认生成并保存 Prefab", GUILayout.Height(28f)) &&
                    EditorUtility.DisplayDialog(
                        "AI ProBuilder Model",
                        "将创建 Prefab 和材质资产。是否继续？",
                        "生成并保存",
                        "取消"))
                {
                    AIProBuilderModelGenerator.GenerateFromTextAsset(specAsset, CreateBuildOptions(true));
                }
            }

            if (GUILayout.Button("预览默认风格化木箱"))
            {
                AIProBuilderModelGenerator.GenerateDefaultModelFromMenu();
            }

            if (GUILayout.Button("从选中的参考图创建模板"))
            {
                AIProBuilderModelGenerator.CreateTemplateFromSelectedImages();
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "默认只在当前场景中预览，不保存 Prefab 或材质。ProBuilderModelSpec 可由本地模型、云端模型或人工生成；单视图需要保守推断深度，多视图更利于还原比例。",
                MessageType.Info);
        }

        private AIProBuilderModelBuildOptions CreateBuildOptions(bool savePrefab)
        {
            return new AIProBuilderModelBuildOptions
            {
                ReplaceExisting = replaceExisting,
                SavePrefab = savePrefab,
                OverwriteExistingPrefab = savePrefab && overwriteExistingPrefab,
                SelectGeneratedRoot = selectGeneratedRoot,
                OutputFolder = outputFolder
            };
        }
    }
}
