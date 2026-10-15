using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AIBuilder.EditorTools
{
    public sealed class UVOptimizationWindow : EditorWindow
    {
        private readonly int[] textureSizes = { 2048, 4096, 8192 };
        private readonly string[] textureSizeLabels = { "2048", "4096（推荐）", "8192" };

        private UVOptimizationSettings settings;
        private bool showAdvanced;
        private Vector2 scroll;
        private string validationMessage = string.Empty;

        public static void Open()
        {
            GetWindow<UVOptimizationWindow>("UV优化");
        }

        private void OnEnable()
        {
            settings = settings ?? new UVOptimizationSettings();
            if (string.IsNullOrEmpty(settings.BlenderPath))
            {
                settings.BlenderPath = UVOptimizationRunner.FindDefaultBlenderPath();
            }
            TryUseCurrentSelection();
            UVOptimizationRunner.Completed += OnTaskCompleted;
        }

        private void OnDisable()
        {
            UVOptimizationRunner.Completed -= OnTaskCompleted;
        }

        private void OnInspectorUpdate()
        {
            if (UVOptimizationRunner.IsRunning)
            {
                Repaint();
            }
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("UV优化", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "针对单个已绑定骨骼的人形 FBX：减少碎 UV、重烘焙 BaseColor、填满空白区，并验证拓扑、骨骼、权重和动画。原 FBX、贴图、材质和场景都不会被修改。",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            settings.SourceModel = (GameObject)EditorGUILayout.ObjectField(
                "角色 FBX", settings.SourceModel, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck())
            {
                AutoDetectInputs();
            }
            settings.SourceTexture = (Texture2D)EditorGUILayout.ObjectField(
                "原 BaseColor", settings.SourceTexture, typeof(Texture2D), false);
            settings.SourceMaterial = (Material)EditorGUILayout.ObjectField(
                "原材质（可空）", settings.SourceMaterial, typeof(Material), false);

            DrawBlenderPath();
            DrawOutputFolder();

            int selectedSize = Math.Max(0, Array.IndexOf(textureSizes, settings.TextureSize));
            selectedSize = EditorGUILayout.Popup("输出贴图尺寸", selectedSize, textureSizeLabels);
            settings.TextureSize = textureSizes[selectedSize];
            settings.BakeMargin = EditorGUILayout.IntSlider("烘焙扩边像素", settings.BakeMargin, 8, 256);
            settings.OverwriteExisting = EditorGUILayout.Toggle("覆盖已有优化结果", settings.OverwriteExisting);

            showAdvanced = EditorGUILayout.Foldout(showAdvanced, "高级设置", true);
            if (showAdvanced)
            {
                EditorGUI.indentLevel++;
                settings.MeshObjectName = EditorGUILayout.TextField("指定 Blender 网格名", settings.MeshObjectName);
                settings.SaveBlend = EditorGUILayout.Toggle("保留可复查 Blend", settings.SaveBlend);
                EditorGUI.indentLevel--;
                EditorGUILayout.HelpBox(
                    "网格名留空时自动选择面数最多的蒙皮网格。若检测到多个主要蒙皮网格，工具会停止，避免只处理半个角色。",
                    MessageType.None);
            }

            EditorGUILayout.Space(8f);
            DrawRunControls();
            DrawResult();
            EditorGUILayout.EndScrollView();
        }

        private void DrawBlenderPath()
        {
            EditorGUILayout.BeginHorizontal();
            settings.BlenderPath = EditorGUILayout.TextField("Blender", settings.BlenderPath);
            if (GUILayout.Button("选择", GUILayout.Width(52f)))
            {
                string selected = EditorUtility.OpenFilePanel("选择 Blender", Path.GetDirectoryName(settings.BlenderPath), "exe");
                if (!string.IsNullOrEmpty(selected))
                {
                    settings.BlenderPath = selected;
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawOutputFolder()
        {
            EditorGUILayout.BeginHorizontal();
            settings.OutputFolder = EditorGUILayout.TextField("输出目录", settings.OutputFolder);
            if (GUILayout.Button("选择", GUILayout.Width(52f)))
            {
                string projectAssets = Application.dataPath.Replace('\\', '/');
                string selected = EditorUtility.OpenFolderPanel("选择 Assets 下的输出目录", projectAssets, string.Empty);
                selected = selected.Replace('\\', '/');
                if (selected.StartsWith(projectAssets, StringComparison.OrdinalIgnoreCase))
                {
                    settings.OutputFolder = "Assets" + selected.Substring(projectAssets.Length);
                }
                else if (!string.IsNullOrEmpty(selected))
                {
                    validationMessage = "输出目录必须位于当前项目 Assets 下。";
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawRunControls()
        {
            using (new EditorGUI.DisabledScope(UVOptimizationRunner.IsRunning))
            {
                if (GUILayout.Button("开始 UV 优化", GUILayout.Height(36f)))
                {
                    Material detectedMaterial = settings.SourceMaterial
                        ?? UVOptimizationRunner.FindSourceMaterial(settings.SourceModel, settings.SourceTexture);
                    settings.SourceMaterial = detectedMaterial;
                    string confirmation =
                        "将生成独立的 UVOptimized FBX、PNG、材质和报告。\n\n"
                        + "不会修改原资源、骨骼、动画或当前场景。是否开始？";
                    if (settings.OverwriteExisting)
                    {
                        confirmation += "\n\n已勾选覆盖，同名的旧优化结果会被替换。";
                    }
                    if (EditorUtility.DisplayDialog("UV优化", confirmation, "开始", "取消"))
                    {
                        if (!UVOptimizationRunner.TryStart(settings, out validationMessage))
                        {
                            Repaint();
                        }
                        else
                        {
                            validationMessage = string.Empty;
                        }
                    }
                }
            }

            if (UVOptimizationRunner.IsRunning)
            {
                EditorGUILayout.HelpBox("处理中：" + UVOptimizationRunner.Status + "。Blender 烘焙可能需要几分钟。", MessageType.Info);
            }
            if (!string.IsNullOrEmpty(validationMessage))
            {
                EditorGUILayout.HelpBox(validationMessage, MessageType.Error);
            }
        }

        private void DrawResult()
        {
            if (string.IsNullOrEmpty(UVOptimizationRunner.LastResult))
            {
                return;
            }
            MessageType type = UVOptimizationRunner.Status == "完成"
                ? MessageType.Info
                : MessageType.Warning;
            EditorGUILayout.HelpBox(UVOptimizationRunner.LastResult, type);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("定位输出"))
            {
                UVOptimizationRunner.RevealLastOutput();
            }
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(UVOptimizationRunner.LastReportAssetPath)))
            {
                if (GUILayout.Button("查看验证报告"))
                {
                    UVOptimizationRunner.SelectLastReport();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void TryUseCurrentSelection()
        {
            GameObject selected = Selection.activeObject as GameObject;
            if (selected == null)
            {
                return;
            }
            string path = AssetDatabase.GetAssetPath(selected);
            if (string.Equals(Path.GetExtension(path), ".fbx", StringComparison.OrdinalIgnoreCase))
            {
                settings.SourceModel = selected;
                AutoDetectInputs();
            }
        }

        private void AutoDetectInputs()
        {
            if (settings.SourceModel == null)
            {
                return;
            }
            settings.SourceTexture = UVOptimizationRunner.FindSourceTexture(settings.SourceModel);
            settings.SourceMaterial = UVOptimizationRunner.FindSourceMaterial(
                settings.SourceModel, settings.SourceTexture);
        }

        private void OnTaskCompleted()
        {
            Repaint();
        }
    }
}
