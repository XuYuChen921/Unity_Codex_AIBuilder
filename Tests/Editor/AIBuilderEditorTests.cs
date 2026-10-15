using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AIBuilder;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace AIBuilder.EditorTools.Tests
{
    public sealed class AIBuilderEditorTests
    {
        private const string TempFolder = "Assets/AIBuilderTestsTemp";

        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.CreateFolder("Assets", "AIBuilderTestsTemp");
            }
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(TempFolder);
            AssetDatabase.Refresh();

            var reviewTestFolder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/AIBuilder/ReviewTests"));
            if (Directory.Exists(reviewTestFolder))
            {
                Directory.Delete(reviewTestFolder, true);
            }
        }

        [Test]
        public void SceneValidator_RejectsMissingExactPrefabBeforeGeneration()
        {
            var spec = CreateEmptySceneSpec();
            spec.objects.Add(new SceneObjectSpec
            {
                id = "missing",
                prefabPath = TempFolder + "/Missing.prefab"
            });

            var result = AIBuilderSpecValidator.ValidateSceneSpec(spec);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("无法解析"));
        }

        [Test]
        public void ScenePreview_UsesExactPrefabWithoutWritingSceneAsset()
        {
            var source = new GameObject("ExactSource");
            var prefabPath = TempFolder + "/Exact.prefab";
            PrefabUtility.SaveAsPrefabAsset(source, prefabPath);
            UnityEngine.Object.DestroyImmediate(source);

            var spec = CreateEmptySceneSpec();
            spec.objects.Add(new SceneObjectSpec
            {
                id = "exact",
                name = "ExactInstance",
                prefabPath = prefabPath,
                position = new Vector3(1f, 2f, 3f)
            });

            var root = AIReferenceSceneGenerator.Generate(spec, new AIReferenceSceneBuildOptions
            {
                UseProjectAssets = false,
                SaveScene = false,
                PromptToSaveModifiedScenes = false
            });

            Assert.That(root, Is.Not.Null);
            Assert.That(root.transform.Find("ExactInstance"), Is.Not.Null);
            Assert.That(EditorSceneManager.GetActiveScene().path, Is.Empty);
        }

        [Test]
        public void ModelValidator_RejectsOutputOutsideAssets()
        {
            var spec = new ProBuilderModelSpec();
            spec.parts.Add(new ProBuilderModelPartSpec());

            var result = AIBuilderSpecValidator.ValidateModelSpec(spec, "../Outside");

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("Assets"));
        }

        [Test]
        public void ModelPreview_DoesNotWritePrefabOrMaterialAssets()
        {
            var spec = new ProBuilderModelSpec
            {
                modelName = "PreviewBox",
                generatedRootName = "AI_PreviewBox",
                savePrefab = true,
                parts = new List<ProBuilderModelPartSpec>
                {
                    new ProBuilderModelPartSpec
                    {
                        name = "body",
                        shape = "box",
                        size = Vector3.one
                    }
                }
            };

            var root = AIProBuilderModelGenerator.Generate(spec, new AIProBuilderModelBuildOptions
            {
                SavePrefab = false,
                ReplaceExisting = false,
                OutputFolder = TempFolder
            });

            Assert.That(root, Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(TempFolder + "/PreviewBox.prefab"), Is.Null);
            Assert.That(AssetDatabase.FindAssets("t:Material", new[] { TempFolder }), Is.Empty);
        }

        [Test]
        public void ModelSave_DoesNotOverwriteExistingPrefabByDefault()
        {
            var spec = new ProBuilderModelSpec
            {
                modelName = "SafeSaveBox",
                generatedRootName = "AI_SafeSaveBox",
                savePrefab = true,
                parts = new List<ProBuilderModelPartSpec>
                {
                    new ProBuilderModelPartSpec
                    {
                        name = "body",
                        shape = "box",
                        size = Vector3.one
                    }
                }
            };

            var options = new AIProBuilderModelBuildOptions
            {
                SavePrefab = true,
                ReplaceExisting = false,
                OverwriteExistingPrefab = false,
                SelectGeneratedRoot = false,
                OutputFolder = TempFolder
            };

            Assert.That(AIProBuilderModelGenerator.Generate(spec, options), Is.Not.Null);
            Assert.That(AIProBuilderModelGenerator.Generate(spec, options), Is.Not.Null);

            Assert.That(AssetDatabase.FindAssets("t:Prefab", new[] { TempFolder }), Has.Length.EqualTo(2));
        }

        [Test]
        public void ExecutionReport_AggregatesSameAssetDecision()
        {
            var report = new AIBuilderExecutionReport("ReportScene");

            report.RecordDecision("tree", AIBuilderExecutionReport.ExactPrefab, "Path:A.prefab", "Assets/A.prefab");
            report.RecordDecision("tree", AIBuilderExecutionReport.ExactPrefab, "Path:A.prefab", "Assets/A.prefab");
            report.RecordDecision("rock", AIBuilderExecutionReport.ProceduralFallback, string.Empty, string.Empty);

            Assert.That(report.decisions, Has.Count.EqualTo(2));
            Assert.That(report.decisions[0].count, Is.EqualTo(2));
            Assert.That(report.ToMarkdown(), Does.Contain("程序化回退：1"));
        }

        [Test]
        public void ReviewBundle_WritesThreeViewsManifestAndAssetReport()
        {
            var spec = CreateEmptySceneSpec();
            var root = new GameObject(spec.generatedRootName);
            var cameraObject = new GameObject("ReviewCamera");
            cameraObject.transform.SetParent(root.transform);
            cameraObject.transform.position = new Vector3(0f, 2f, -6f);
            cameraObject.transform.LookAt(Vector3.up * 0.5f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.gray;

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(root.transform);
            cube.transform.position = Vector3.up * 0.5f;

            var report = new AIBuilderExecutionReport(spec.sceneName);
            report.RecordDecision("cube", AIBuilderExecutionReport.ProceduralFallback, string.Empty, string.Empty);
            var result = AIBuilderReviewBundleBuilder.Create(
                spec,
                TempFolder + "/ReviewSpec.json",
                root,
                report,
                new AIBuilderReviewCaptureOptions
                {
                    OutputRoot = "Library/AIBuilder/ReviewTests",
                    MainWidth = 128,
                    MainHeight = 96,
                    SpatialWidth = 128,
                    SpatialHeight = 128,
                    RevealInFinder = false
                });

            Assert.That(new FileInfo(result.MainImagePath).Length, Is.GreaterThan(100));
            Assert.That(new FileInfo(result.SideImagePath).Length, Is.GreaterThan(100));
            Assert.That(new FileInfo(result.TopImagePath).Length, Is.GreaterThan(100));
            Assert.That(File.Exists(result.ManifestPath), Is.True);
            Assert.That(File.ReadAllText(result.SceneSpecSnapshotPath), Does.Contain("\"schemaVersion\": 2"));
            Assert.That(File.ReadAllText(result.AssetReportPath), Does.Contain("procedural_fallback"));
            Assert.That(File.ReadAllText(result.ReviewRequestPath), Does.Contain("只修改 SceneSpec"));
        }

        [Test]
        public void BlockoutRepository_RecognizesSceneTask()
        {
            var asset = WriteBlockoutSpec(CreateBlockoutSpec(AIBlockoutSpecRepository.SceneBlockout, "测试场景白盒"));

            Assert.That(AIBlockoutSpecRepository.TryLoad(asset, out var context, out var error), Is.True, error);
            Assert.That(context.Kind, Is.EqualTo(AIBlockoutKind.Scene));
            Assert.That(context.DisplayName, Is.EqualTo("测试场景白盒"));
        }

        [Test]
        public void ScreenPolygon_ProjectsToDeclaredReferenceBounds()
        {
            var spec = CreateBlockoutSpec(AIBlockoutSpecRepository.SceneBlockout, "屏幕投影测试");
            spec.camera.enabled = true;
            spec.camera.position = Vector3.zero;
            spec.camera.lookAt = Vector3.forward;
            spec.camera.fieldOfView = 50f;
            spec.referenceFrame = new ProBuilderReferenceFrameSpec
            {
                enabled = true,
                width = 1600,
                height = 900,
                lockAspect = true
            };
            spec.sceneAnalysis = new ProBuilderSceneAnalysisSpec
            {
                enabled = true,
                elements = new List<ProBuilderSceneElementSpec>
                {
                    new ProBuilderSceneElementSpec
                    {
                        id = "mass",
                        label = "测试轮廓",
                        partName = "mass",
                        generateFromScreen = true,
                        depthMeters = 10f,
                        thicknessMeters = 1f,
                        screenPolygon = new List<Vector2>
                        {
                            new Vector2(0.2f, 0.25f),
                            new Vector2(0.6f, 0.25f),
                            new Vector2(0.6f, 0.65f),
                            new Vector2(0.2f, 0.65f)
                        }
                    }
                }
            };
            spec.parts = new List<ProBuilderModelPartSpec>
            {
                new ProBuilderModelPartSpec
                {
                    name = "mass",
                    shape = "screen_polygon",
                    analysisElementId = "mass"
                }
            };

            var root = AIProBuilderModelGenerator.Generate(spec, new AIProBuilderModelBuildOptions
            {
                SavePrefab = false,
                ReplaceExisting = false,
                SelectGeneratedRoot = false,
                OutputFolder = TempFolder
            });
            var camera = root.GetComponentInChildren<Camera>(true);
            var mesh = root.GetComponentInChildren<ProBuilderMesh>(true);
            var points = mesh.positions
                .Select(position => camera.WorldToViewportPoint(mesh.transform.TransformPoint(position)))
                .Where(point => point.z > 0f)
                .ToArray();

            Assert.That(points.Min(point => point.x), Is.EqualTo(0.2f).Within(0.01f));
            Assert.That(points.Max(point => point.x), Is.EqualTo(0.6f).Within(0.01f));
            Assert.That(1f - points.Max(point => point.y), Is.EqualTo(0.25f).Within(0.01f));
            Assert.That(1f - points.Min(point => point.y), Is.EqualTo(0.65f).Within(0.01f));
        }

        [Test]
        public void BlockoutReviewBundle_CapturesGenericProBuilderRoot()
        {
            var spec = CreateBlockoutSpec(AIBlockoutSpecRepository.ObjectBlockout, "测试物体白盒");
            spec.camera.enabled = true;
            spec.camera.position = new Vector3(0f, 2f, -8f);
            spec.camera.lookAt = Vector3.up;
            spec.referenceFrame = new ProBuilderReferenceFrameSpec
            {
                enabled = true,
                width = 160,
                height = 90,
                lockAspect = true
            };
            var referencePath = TempFolder + "/Reference.png";
            WriteReferenceImage(referencePath, 160, 90);
            spec.referenceImages.Add(new ProBuilderModelReferenceImageSpec
            {
                assetPath = referencePath,
                view = "main",
                width = 160,
                height = 90,
                notes = "测试参考图"
            });
            var asset = WriteBlockoutSpec(spec);
            var root = AIProBuilderModelGenerator.Generate(spec, new AIProBuilderModelBuildOptions
            {
                SavePrefab = false,
                ReplaceExisting = false,
                SelectGeneratedRoot = false,
                OutputFolder = TempFolder
            });

            var result = AIBuilderReviewBundleBuilder.CreateBlockout(
                asset,
                spec,
                root,
                new AIBuilderReviewCaptureOptions
                {
                    OutputRoot = "Library/AIBuilder/ReviewTests",
                    MainWidth = 128,
                    MainHeight = 96,
                    SpatialWidth = 128,
                    SpatialHeight = 128,
                    RevealInFinder = false
                });

            Assert.That(root.GetComponentInChildren<Camera>(), Is.Not.Null);
            Assert.That(new FileInfo(result.MainImagePath).Length, Is.GreaterThan(100));
            Assert.That(new FileInfo(result.SideImagePath).Length, Is.GreaterThan(100));
            Assert.That(new FileInfo(result.TopImagePath).Length, Is.GreaterThan(100));
            Assert.That(new FileInfo(result.ReferenceImagePath).Length, Is.GreaterThan(100));
            Assert.That(new FileInfo(result.OverlayImagePath).Length, Is.GreaterThan(100));
            Assert.That(new FileInfo(result.DifferenceImagePath).Length, Is.GreaterThan(100));
            Assert.That(File.ReadAllText(result.ProjectionReportPath), Does.Contain("屏幕投影误差报告"));
            Assert.That(File.ReadAllText(result.SceneSpecSnapshotPath), Does.Contain("object_blockout"));
            Assert.That(File.ReadAllText(result.ReviewRequestPath), Does.Contain("只修改 ProBuilderModelSpec"));
        }

        [Test]
        public void Menus_ExposeOnlyUnifiedBlockoutBuilder()
        {
            var menuPaths = typeof(AIBlockoutBuilderWindow).Assembly.GetTypes()
                .SelectMany(type => type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                .SelectMany(method => method.GetCustomAttributesData())
                .Where(attribute => attribute.AttributeType == typeof(MenuItem))
                .Select(attribute => attribute.ConstructorArguments.FirstOrDefault().Value as string)
                .Where(path => path != null && path.StartsWith("AI构建器/", StringComparison.Ordinal))
                .ToArray();

            Assert.That(menuPaths, Is.EqualTo(new[] { "AI构建器/打开白盒搭建器" }));
        }

        [Test]
        public void Menus_PutModelToolsUnderUnifiedAIModelProcessingRoot()
        {
            var menuPaths = typeof(AIBlockoutBuilderWindow).Assembly.GetTypes()
                .SelectMany(type => type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                .SelectMany(method => method.GetCustomAttributesData())
                .Where(attribute => attribute.AttributeType == typeof(MenuItem))
                .Select(attribute => attribute.ConstructorArguments.FirstOrDefault().Value as string)
                .Where(path => path != null)
                .ToArray();

            Assert.That(menuPaths.Any(path => path.StartsWith("Tools/AIBuilder/", StringComparison.Ordinal)), Is.False);
            Assert.That(menuPaths.Count(path => path == "Tools/AI模型处理/贴图绘制"), Is.EqualTo(1));
        }

        private static TextAsset WriteBlockoutSpec(ProBuilderModelSpec spec)
        {
            var path = TempFolder + "/BlockoutTask.json";
            File.WriteAllText(path, JsonUtility.ToJson(spec, true));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<TextAsset>(path);
        }

        private static void WriteReferenceImage(string path, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var colors = Enumerable.Repeat(new Color(0.2f, 0.5f, 0.7f), width * height).ToArray();
            texture.SetPixels(colors);
            texture.Apply(false, false);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        private static ProBuilderModelSpec CreateBlockoutSpec(string blockoutType, string displayName)
        {
            return new ProBuilderModelSpec
            {
                blockoutType = blockoutType,
                displayName = displayName,
                modelName = "BlockoutTest",
                generatedRootName = "AI_BlockoutTest",
                savePrefab = blockoutType == AIBlockoutSpecRepository.ObjectBlockout,
                outputFolder = TempFolder,
                parts = new List<ProBuilderModelPartSpec>
                {
                    new ProBuilderModelPartSpec
                    {
                        name = "body",
                        shape = "box",
                        position = Vector3.up * 0.5f,
                        size = Vector3.one
                    }
                }
            };
        }

        private static SceneSpec CreateEmptySceneSpec()
        {
            return new SceneSpec
            {
                sceneName = "AIBuilderTestScene",
                generatedRootName = "AIBuilderTestRoot",
                terrain = new TerrainSpec { enabled = false, createColorPatches = false, grassPatchCount = 0 },
                features = new SceneFeatureSpec
                {
                    centralFeature = false,
                    magicPools = false,
                    heroForegroundTrees = false,
                    clouds = false,
                    fog = false,
                    postProcessing = false
                },
                objects = new List<SceneObjectSpec>(),
                scatterGroups = new List<ScatterGroupSpec>()
            };
        }
    }
}
