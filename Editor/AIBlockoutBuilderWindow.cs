using System;
using System.IO;
using AIBuilder;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace AIBuilder.EditorTools
{
    public sealed class AIBlockoutBuilderWindow : EditorWindow
    {
        private static readonly string[] ModeLabels = { "场景白盒", "物体白盒" };

        private AIBlockoutKind mode;
        private AIBlockoutSpecContext context;
        private AIBlockoutPreviewResult preview;
        private string previewSpecPath;
        private DateTime previewSpecWriteUtc;
        private string lastCommittedPath;
        private Texture2D overlayPreviewTexture;
        private Vector2 scrollPosition;

        [MenuItem("AI构建器/打开白盒搭建器", priority = 0)]
        public static void Open()
        {
            var window = GetWindow<AIBlockoutBuilderWindow>("AI 白盒搭建器");
            window.minSize = new Vector2(620f, 580f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.projectChanged += HandleProjectChanged;
            var latest = AIBlockoutSpecRepository.FindLatestAny();
            if (latest != null)
            {
                mode = latest.Kind;
                context = latest;
            }
            else
            {
                RefreshContext();
            }
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= HandleProjectChanged;
            ClearOverlayPreview();
        }

        private void OnFocus()
        {
            RefreshContext();
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            DrawHeader();
            EditorGUILayout.Space(10f);

            var selectedMode = (AIBlockoutKind)GUILayout.Toolbar((int)mode, ModeLabels, GUILayout.Height(32f));
            if (selectedMode != mode)
            {
                mode = selectedMode;
                RefreshContext();
            }

            EditorGUILayout.Space(10f);
            DrawTaskSummary();
            EditorGUILayout.Space(10f);
            DrawPreviewStatus();
            EditorGUILayout.Space(10f);
            DrawActions();
            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "Codex 是任务入口；Unity 只负责 ProBuilder 预览、多视角复查和安全写入。",
                MessageType.Info);
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("Unity AI 白盒搭建器", EditorStyles.largeLabel);
            EditorGUILayout.LabelField(
                "只搭建可编辑体块、布局与相机；不匹配 Prefab，不生成最终美术。",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.Label("仅两个核心功能", EditorStyles.helpBox);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTaskSummary()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (context == null)
            {
                EditorGUILayout.LabelField("当前没有可执行任务", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    mode == AIBlockoutKind.Scene
                        ? "在 Codex 中提交一张或多张同场景参考图。Codex 生成场景白盒配置后，本窗口会自动识别。"
                        : "在 Codex 中提交一张物体参考图。Codex 生成物体白盒配置后，本窗口会自动识别。",
                    MessageType.None);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("当前任务：" + context.DisplayName, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label("Codex 已准备", EditorStyles.miniButton);
            EditorGUILayout.EndHorizontal();
            DrawInfoRow("类型", context.Kind == AIBlockoutKind.Scene ? "场景白盒" : "物体白盒");
            DrawInfoRow("参考图", BuildReferenceSummary(context.Spec));
            DrawInfoRow("尺度提示", string.IsNullOrWhiteSpace(context.Spec.scaleHint) ? "未提供，按参考图保守推断" : context.Spec.scaleHint);
            DrawInfoRow("输出根节点", context.Spec.generatedRootName);

            if (context.Spec.visualNotes != null && context.Spec.visualNotes.Length > 0)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.HelpBox(context.Spec.visualNotes[0], MessageType.None);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPreviewStatus()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Scene 视图预览", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(IsPreviewReady() ? "预览已生成" : "等待预览", EditorStyles.miniButton);
            EditorGUILayout.EndHorizontal();

            if (!IsPreviewReady())
            {
                EditorGUILayout.HelpBox("点击“生成 / 更新预览”后，Unity 会生成 ProBuilder 并自动输出主、侧、俯视角复查图。", MessageType.None);
                EditorGUILayout.EndVertical();
                return;
            }

            var partCount = preview.Root.GetComponentsInChildren<ProBuilderMesh>(true).Length;
            EditorGUILayout.LabelField($"✓  已生成 {partCount} 个可编辑 ProBuilder 体块");
            if (preview.ReviewBundle != null)
            {
                EditorGUILayout.LabelField("✓  已自动截取主视角、侧视角和俯视角");
                EditorGUILayout.LabelField("✓  Codex 复查包已准备");
                if (!string.IsNullOrWhiteSpace(preview.ReviewBundle.OverlayImagePath))
                {
                    EditorGUILayout.LabelField("✓  已生成参考图叠图和像素差异图");
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("查看主视角"))
                {
                    EditorUtility.RevealInFinder(preview.ReviewBundle.MainImagePath);
                }
                if (!string.IsNullOrWhiteSpace(preview.ReviewBundle.OverlayImagePath) && GUILayout.Button("查看50%叠图"))
                {
                    EditorUtility.RevealInFinder(preview.ReviewBundle.OverlayImagePath);
                }
                if (!string.IsNullOrWhiteSpace(preview.ReviewBundle.ProjectionReportPath) && GUILayout.Button("查看投影误差"))
                {
                    EditorUtility.RevealInFinder(preview.ReviewBundle.ProjectionReportPath);
                }
                EditorGUILayout.EndHorizontal();

                if (overlayPreviewTexture != null)
                {
                    EditorGUILayout.Space(6f);
                    EditorGUILayout.LabelField("参考图 / 白盒 50% 叠图", EditorStyles.boldLabel);
                    var previewWidth = Mathf.Max(320f, position.width - 52f);
                    GUILayout.Label(
                        overlayPreviewTexture,
                        GUILayout.Width(previewWidth),
                        GUILayout.Height(previewWidth * 9f / 16f));
                }
            }
            else
            {
                EditorGUILayout.HelpBox("预览已生成，但复查截图失败：" + preview.ReviewError, MessageType.Warning);
            }

            if (context.Spec.codexSuggestions != null && context.Spec.codexSuggestions.Length > 0)
            {
                EditorGUILayout.HelpBox("Codex 建议：" + string.Join("；", context.Spec.codexSuggestions), MessageType.Warning);
            }

            if (!string.IsNullOrWhiteSpace(lastCommittedPath))
            {
                EditorGUILayout.HelpBox("已写入：" + lastCommittedPath, MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawActions()
        {
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(context == null))
            {
                if (GUILayout.Button("生成 / 更新预览", GUILayout.Height(38f)))
                {
                    GeneratePreview();
                }
            }

            using (new EditorGUI.DisabledScope(!IsPreviewReady()))
            {
                var commitLabel = mode == AIBlockoutKind.Scene ? "确认写入场景" : "确认保存 Prefab";
                if (GUILayout.Button(commitLabel, GUILayout.Height(38f)))
                {
                    Commit();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void GeneratePreview()
        {
            try
            {
                lastCommittedPath = null;
                preview = AIBlockoutExecutor.Preview(
                    context,
                    new AIBuilderReviewCaptureOptions { RevealInFinder = false });
                previewSpecPath = preview == null ? null : context.AssetPath;
                previewSpecWriteUtc = preview == null ? DateTime.MinValue : context.LastWriteUtc;
                LoadOverlayPreview(preview?.ReviewBundle?.OverlayImagePath);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("AI 白盒搭建器", "生成预览失败：\n" + exception.Message, "确定");
            }
        }

        private void Commit()
        {
            var isScene = mode == AIBlockoutKind.Scene;
            var message = isScene
                ? "将持久化白盒材质，并在 Assets/AIBuilder/Scenes 下保存新的场景副本。不会覆盖原场景。是否继续？"
                : "将保存新的 Prefab 和材质资产。不会覆盖现有同名资产。是否继续？";
            if (!EditorUtility.DisplayDialog("AI 白盒搭建器", message, "确认写入", "取消"))
            {
                return;
            }

            try
            {
                var result = isScene
                    ? AIBlockoutExecutor.CommitScene(context)
                    : AIBlockoutExecutor.CommitObject(context);
                if (result == null)
                {
                    return;
                }

                preview.Root = result.Root;
                lastCommittedPath = result.AssetPath;
                var asset = AssetDatabase.LoadMainAssetAtPath(result.AssetPath);
                if (asset != null)
                {
                    EditorGUIUtility.PingObject(asset);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("AI 白盒搭建器", "写入失败：\n" + exception.Message, "确定");
            }
        }

        private void HandleProjectChanged()
        {
            RefreshContext();
            Repaint();
        }

        private void RefreshContext()
        {
            var next = AIBlockoutSpecRepository.FindLatest(mode);
            var nextPath = next?.AssetPath;
            var changed = !string.Equals(nextPath, context?.AssetPath, StringComparison.Ordinal) ||
                          next?.LastWriteUtc != context?.LastWriteUtc;
            if (changed)
            {
                context = next;
                preview = null;
                previewSpecPath = null;
                previewSpecWriteUtc = DateTime.MinValue;
                lastCommittedPath = null;
                ClearOverlayPreview();
            }
            else
            {
                context = next;
            }
        }

        private bool IsPreviewReady()
        {
            return context != null &&
                   preview?.Root != null &&
                   string.Equals(previewSpecPath, context.AssetPath, StringComparison.Ordinal) &&
                   previewSpecWriteUtc == context.LastWriteUtc;
        }

        private static void DrawInfoRow(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(82f));
            EditorGUILayout.SelectableLabel(value ?? string.Empty, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.EndHorizontal();
        }

        private void LoadOverlayPreview(string path)
        {
            ClearOverlayPreview();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            if (ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false))
            {
                overlayPreviewTexture = texture;
            }
            else
            {
                DestroyImmediate(texture);
            }
        }

        private void ClearOverlayPreview()
        {
            if (overlayPreviewTexture != null)
            {
                DestroyImmediate(overlayPreviewTexture);
                overlayPreviewTexture = null;
            }
        }

        private static string BuildReferenceSummary(ProBuilderModelSpec spec)
        {
            if (spec.referenceImages == null || spec.referenceImages.Count == 0)
            {
                return "由当前 Codex 任务提供，未导入 Unity";
            }

            var names = new string[spec.referenceImages.Count];
            for (var i = 0; i < spec.referenceImages.Count; i++)
            {
                var path = spec.referenceImages[i]?.assetPath;
                names[i] = string.IsNullOrWhiteSpace(path) ? "参考图 " + (i + 1) : Path.GetFileName(path);
            }

            return string.Join(" · ", names);
        }
    }
}
