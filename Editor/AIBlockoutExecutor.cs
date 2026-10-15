using System;
using AIBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AIBuilder.EditorTools
{
    public sealed class AIBlockoutPreviewResult
    {
        public GameObject Root;
        public AIBuilderReviewBundleResult ReviewBundle;
        public string ReviewError;
    }

    public sealed class AIBlockoutCommitResult
    {
        public GameObject Root;
        public string AssetPath;
    }

    public static class AIBlockoutExecutor
    {
        private const string SceneFolder = "Assets/AIBuilder/Scenes";
        private const string SceneAssetFolder = "Assets/AIBuilder/Generated/Blockouts";

        public static AIBlockoutPreviewResult Preview(
            AIBlockoutSpecContext context,
            AIBuilderReviewCaptureOptions captureOptions = null)
        {
            RequireContext(context);
            var root = AIProBuilderModelGenerator.GenerateFromTextAsset(
                context.Asset,
                new AIProBuilderModelBuildOptions
                {
                    ReplaceExisting = true,
                    SavePrefab = false,
                    PersistMaterials = false,
                    OverwriteExistingPrefab = false,
                    SelectGeneratedRoot = false,
                    OutputFolder = ResolveOutputFolder(context.Spec)
                });

            if (root == null)
            {
                return null;
            }

            var result = new AIBlockoutPreviewResult { Root = root };
            try
            {
                result.ReviewBundle = AIBuilderReviewBundleBuilder.CreateBlockout(
                    context.Asset,
                    context.Spec,
                    root,
                    captureOptions);
            }
            catch (Exception exception)
            {
                result.ReviewError = exception.Message;
                Debug.LogException(exception);
            }

            return result;
        }

        public static AIBlockoutCommitResult CommitScene(AIBlockoutSpecContext context)
        {
            RequireKind(context, AIBlockoutKind.Scene);
            var assetFolder = SceneAssetFolder + "/" + AIBlockoutSpecRepository.SanitizeFileName(context.DisplayName);
            var root = AIProBuilderModelGenerator.GenerateFromTextAsset(
                context.Asset,
                new AIProBuilderModelBuildOptions
                {
                    ReplaceExisting = true,
                    SavePrefab = false,
                    PersistMaterials = true,
                    OverwriteExistingPrefab = false,
                    SelectGeneratedRoot = true,
                    OutputFolder = assetFolder
                });

            if (root == null)
            {
                return null;
            }

            AIBuilderAssetPathUtility.EnsureAssetFolder(SceneFolder);
            AssetDatabase.SaveAssets();
            var scenePath = AssetDatabase.GenerateUniqueAssetPath(
                SceneFolder + "/" + AIBlockoutSpecRepository.SanitizeFileName(context.DisplayName) + ".unity");
            EditorSceneManager.MarkSceneDirty(root.scene);
            if (!EditorSceneManager.SaveScene(root.scene, scenePath, true))
            {
                throw new InvalidOperationException("白盒场景副本保存失败：" + scenePath);
            }

            AssetDatabase.Refresh();
            return new AIBlockoutCommitResult { Root = root, AssetPath = scenePath };
        }

        public static AIBlockoutCommitResult CommitObject(AIBlockoutSpecContext context)
        {
            RequireKind(context, AIBlockoutKind.Object);
            var root = AIProBuilderModelGenerator.GenerateFromTextAsset(
                context.Asset,
                new AIProBuilderModelBuildOptions
                {
                    ReplaceExisting = true,
                    SavePrefab = true,
                    PersistMaterials = true,
                    OverwriteExistingPrefab = false,
                    SelectGeneratedRoot = true,
                    OutputFolder = ResolveOutputFolder(context.Spec)
                });

            return root == null
                ? null
                : new AIBlockoutCommitResult
                {
                    Root = root,
                    AssetPath = AIProBuilderModelGenerator.LastSavedPrefabPath
                };
        }

        private static string ResolveOutputFolder(ProBuilderModelSpec spec)
        {
            return string.IsNullOrWhiteSpace(spec.outputFolder)
                ? "Assets/AIBuilder/GeneratedModels"
                : spec.outputFolder;
        }

        private static void RequireContext(AIBlockoutSpecContext context)
        {
            if (context?.Asset == null || context.Spec == null)
            {
                throw new ArgumentNullException(nameof(context), "白盒任务为空。");
            }
        }

        private static void RequireKind(AIBlockoutSpecContext context, AIBlockoutKind expected)
        {
            RequireContext(context);
            if (context.Kind != expected)
            {
                throw new ArgumentException("白盒任务类型不匹配。", nameof(context));
            }
        }
    }
}
