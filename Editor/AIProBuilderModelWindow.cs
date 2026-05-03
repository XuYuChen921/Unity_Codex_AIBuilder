using UnityEditor;
using UnityEngine;

namespace AIBuilder.EditorTools
{
    public sealed class AIProBuilderModelWindow : EditorWindow
    {
        private TextAsset specAsset;
        private bool replaceExisting = true;
        private bool savePrefab = true;
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
            EditorGUILayout.LabelField("Reference Model To ProBuilder", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            specAsset = (TextAsset)EditorGUILayout.ObjectField("Model Spec JSON", specAsset, typeof(TextAsset), false);
            replaceExisting = EditorGUILayout.Toggle("Replace Existing Root", replaceExisting);
            savePrefab = EditorGUILayout.Toggle("Save Prefab", savePrefab);
            selectGeneratedRoot = EditorGUILayout.Toggle("Select Generated Root", selectGeneratedRoot);
            outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);

            EditorGUILayout.Space(8f);

            using (new EditorGUI.DisabledScope(specAsset == null))
            {
                if (GUILayout.Button("Generate ProBuilder Model", GUILayout.Height(32f)))
                {
                    AIProBuilderModelGenerator.GenerateFromTextAsset(
                        specAsset,
                        new AIProBuilderModelBuildOptions
                        {
                            ReplaceExisting = replaceExisting,
                            SavePrefab = savePrefab,
                            SelectGeneratedRoot = selectGeneratedRoot,
                            OutputFolder = outputFolder
                        });
                }
            }

            if (GUILayout.Button("Generate Default Stylized Crate"))
            {
                AIProBuilderModelGenerator.GenerateDefaultModelFromMenu();
            }

            if (GUILayout.Button("Create Template From Selected Reference Images"))
            {
                AIProBuilderModelGenerator.CreateTemplateFromSelectedImages();
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "Workflow: import one or more model reference images, send them to Codex, let Codex write a ProBuilderModelSpec JSON, then generate editable ProBuilder parts from that spec. Single-view images require conservative depth inference; multi-view references produce stronger proportions.",
                MessageType.Info);
        }
    }
}
