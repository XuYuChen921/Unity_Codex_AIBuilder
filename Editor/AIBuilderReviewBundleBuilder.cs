using System;
using System.Collections.Generic;
using System.IO;
using AIBuilder;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;
using Object = UnityEngine.Object;
using PackageManagerInfo = UnityEditor.PackageManager.PackageInfo;

namespace AIBuilder.EditorTools
{
    public sealed class AIBuilderReviewCaptureOptions
    {
        public string OutputRoot = "Library/AIBuilder/Reviews";
        public int MainWidth = 1280;
        public int MainHeight = 720;
        public int SpatialWidth = 1024;
        public int SpatialHeight = 1024;
        public bool RevealInFinder = true;
    }

    public sealed class AIBuilderReviewBundleResult
    {
        public string FolderPath;
        public string ManifestPath;
        public string SceneSpecSnapshotPath;
        public string MainImagePath;
        public string ReferenceImagePath;
        public string OverlayImagePath;
        public string DifferenceImagePath;
        public string SideImagePath;
        public string TopImagePath;
        public string AssetReportPath;
        public string ProjectionReportPath;
        public string ReviewRequestPath;
    }

    [Serializable]
    internal sealed class AIBuilderReviewManifest
    {
        public int schemaVersion = 1;
        public string sceneName;
        public string generatedAtUtc;
        public string sceneSpecAssetPath;
        public string sceneSpecAbsolutePath;
        public string sceneSpecSnapshotPath;
        public string sceneSpecSchemaPath;
        public string referenceImageAssetPath;
        public string referenceImageAbsolutePath;
        public string mainImagePath;
        public string sideImagePath;
        public string topImagePath;
        public string assetReportPath;
        public string reviewRequestPath;
        public AIBuilderExecutionReport executionReport;
    }

    [Serializable]
    internal sealed class AIBlockoutReviewManifest
    {
        public int schemaVersion = 1;
        public string blockoutType;
        public string displayName;
        public string generatedAtUtc;
        public string specAssetPath;
        public string specAbsolutePath;
        public string specSnapshotPath;
        public string specSchemaPath;
        public string[] referenceImageAbsolutePaths = Array.Empty<string>();
        public string mainImagePath;
        public string referenceImagePath;
        public string overlayImagePath;
        public string differenceImagePath;
        public string sideImagePath;
        public string topImagePath;
        public string summaryPath;
        public string projectionReportPath;
        public string reviewRequestPath;
    }

    public static class AIBuilderReviewBundleBuilder
    {
        public static AIBuilderReviewBundleResult Create(
            TextAsset specAsset,
            GameObject generatedRoot,
            AIBuilderExecutionReport executionReport,
            AIBuilderReviewCaptureOptions options = null)
        {
            if (specAsset == null)
            {
                throw new ArgumentNullException(nameof(specAsset));
            }

            SceneSpec spec;
            try
            {
                spec = JsonUtility.FromJson<SceneSpec>(specAsset.text);
            }
            catch (Exception exception)
            {
                throw new ArgumentException("SceneSpec JSON 解析失败。", nameof(specAsset), exception);
            }

            return Create(spec, AssetDatabase.GetAssetPath(specAsset), generatedRoot, executionReport, options);
        }

        public static AIBuilderReviewBundleResult Create(
            SceneSpec spec,
            string specAssetPath,
            GameObject generatedRoot,
            AIBuilderExecutionReport executionReport,
            AIBuilderReviewCaptureOptions options = null)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (generatedRoot == null)
            {
                throw new ArgumentNullException(nameof(generatedRoot));
            }

            options ??= new AIBuilderReviewCaptureOptions();
            ValidateResolution(options.MainWidth, options.MainHeight);
            ValidateResolution(options.SpatialWidth, options.SpatialHeight);

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var outputRoot = ResolveOutputRoot(projectRoot, options.OutputRoot);
            Directory.CreateDirectory(outputRoot);
            var folder = CreateUniqueFolder(outputRoot, spec.sceneName);

            var mainCamera = generatedRoot.GetComponentInChildren<Camera>(true);
            if (mainCamera == null)
            {
                throw new InvalidOperationException("生成根节点下没有可用于评审截图的 Camera。");
            }

            var bounds = CalculateBounds(spec, generatedRoot);
            var result = new AIBuilderReviewBundleResult
            {
                FolderPath = Normalize(folder),
                ManifestPath = Normalize(Path.Combine(folder, "ReviewManifest.json")),
                SceneSpecSnapshotPath = Normalize(Path.Combine(folder, "SceneSpec.snapshot.json")),
                MainImagePath = Normalize(Path.Combine(folder, "Main.png")),
                SideImagePath = Normalize(Path.Combine(folder, "Side.png")),
                TopImagePath = Normalize(Path.Combine(folder, "Top.png")),
                AssetReportPath = Normalize(Path.Combine(folder, "AssetDecisions.md")),
                ReviewRequestPath = Normalize(Path.Combine(folder, "CodexReviewRequest.md"))
            };

            CaptureCamera(mainCamera, result.MainImagePath, options.MainWidth, options.MainHeight);
            CaptureSpatialView(mainCamera, bounds, result.SideImagePath, options.SpatialWidth, options.SpatialHeight, false);
            CaptureSpatialView(mainCamera, bounds, result.TopImagePath, options.SpatialWidth, options.SpatialHeight, true);

            executionReport ??= new AIBuilderExecutionReport(spec.sceneName);
            File.WriteAllText(result.SceneSpecSnapshotPath, JsonUtility.ToJson(spec, true));
            File.WriteAllText(result.AssetReportPath, executionReport.ToMarkdown());

            var referenceAssetPath = spec.referenceImage?.assetPath ?? string.Empty;
            var manifest = new AIBuilderReviewManifest
            {
                sceneName = spec.sceneName,
                generatedAtUtc = DateTime.UtcNow.ToString("O"),
                sceneSpecAssetPath = specAssetPath ?? string.Empty,
                sceneSpecAbsolutePath = ResolveAssetAbsolutePath(projectRoot, specAssetPath),
                sceneSpecSnapshotPath = result.SceneSpecSnapshotPath,
                sceneSpecSchemaPath = ResolveSceneSchemaPath(),
                referenceImageAssetPath = referenceAssetPath,
                referenceImageAbsolutePath = ResolveAssetAbsolutePath(projectRoot, referenceAssetPath),
                mainImagePath = result.MainImagePath,
                sideImagePath = result.SideImagePath,
                topImagePath = result.TopImagePath,
                assetReportPath = result.AssetReportPath,
                reviewRequestPath = result.ReviewRequestPath,
                executionReport = executionReport
            };

            File.WriteAllText(result.ManifestPath, JsonUtility.ToJson(manifest, true));
            File.WriteAllText(result.ReviewRequestPath, BuildReviewRequest(manifest));

            Debug.Log($"AIBuilder Codex 评审包已生成：{result.FolderPath}");
            if (options.RevealInFinder && !Application.isBatchMode)
            {
                EditorUtility.RevealInFinder(result.FolderPath);
            }

            return result;
        }

        public static AIBuilderReviewBundleResult CreateBlockout(
            TextAsset specAsset,
            ProBuilderModelSpec spec,
            GameObject generatedRoot,
            AIBuilderReviewCaptureOptions options = null)
        {
            if (specAsset == null)
            {
                throw new ArgumentNullException(nameof(specAsset));
            }

            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (generatedRoot == null)
            {
                throw new ArgumentNullException(nameof(generatedRoot));
            }

            options ??= new AIBuilderReviewCaptureOptions();
            ResolveMainResolution(options, spec.referenceFrame, out var mainWidth, out var mainHeight);
            ValidateResolution(mainWidth, mainHeight);
            ValidateResolution(options.SpatialWidth, options.SpatialHeight);

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var outputRoot = ResolveOutputRoot(projectRoot, options.OutputRoot);
            Directory.CreateDirectory(outputRoot);
            var displayName = string.IsNullOrWhiteSpace(spec.displayName) ? spec.modelName : spec.displayName;
            var folder = CreateUniqueFolder(outputRoot, displayName);
            var bounds = CalculateBounds(generatedRoot, new Vector3(10f, 5f, 10f));
            var result = new AIBuilderReviewBundleResult
            {
                FolderPath = Normalize(folder),
                ManifestPath = Normalize(Path.Combine(folder, "ReviewManifest.json")),
                SceneSpecSnapshotPath = Normalize(Path.Combine(folder, "ProBuilderModelSpec.snapshot.json")),
                MainImagePath = Normalize(Path.Combine(folder, "Main.png")),
                SideImagePath = Normalize(Path.Combine(folder, "Side.png")),
                TopImagePath = Normalize(Path.Combine(folder, "Top.png")),
                AssetReportPath = Normalize(Path.Combine(folder, "BlockoutSummary.md")),
                ProjectionReportPath = Normalize(Path.Combine(folder, "ProjectionReport.md")),
                ReviewRequestPath = Normalize(Path.Combine(folder, "CodexReviewRequest.md"))
            };

            var cameraObject = new GameObject("AIBuilder_BlockoutReviewCamera")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            var lightObject = new GameObject("AIBuilder_BlockoutReviewLight")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            var previousSelection = Selection.objects;
            try
            {
                Selection.objects = Array.Empty<UnityEngine.Object>();
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = Color.white;
                light.intensity = 1.35f;
                light.shadows = LightShadows.None;
                lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

                var camera = cameraObject.AddComponent<Camera>();
                ConfigureBlockoutCamera(camera, bounds, spec.camera, spec.referenceFrame, generatedRoot.transform);
                WarmUpCamera(camera, mainWidth, mainHeight);
                CaptureCamera(camera, result.MainImagePath, mainWidth, mainHeight);
                CaptureSpatialView(camera, bounds, result.SideImagePath, options.SpatialWidth, options.SpatialHeight, false);
                CaptureSpatialView(camera, bounds, result.TopImagePath, options.SpatialWidth, options.SpatialHeight, true);
                File.WriteAllText(result.ProjectionReportPath, BuildProjectionReport(spec, generatedRoot, camera));
            }
            finally
            {
                Selection.objects = previousSelection;
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(lightObject);
            }

            File.WriteAllText(result.SceneSpecSnapshotPath, specAsset.text);
            File.WriteAllText(
                result.AssetReportPath,
                $"# 白盒生成摘要\n\n- 类型：{spec.blockoutType}\n- 名称：{displayName}\n- ProBuilder 部件：{spec.parts?.Count ?? 0}\n- 场景分析元素：{spec.sceneAnalysis?.elements?.Count ?? 0}\n- 主视角：{mainWidth}×{mainHeight}\n");

            var referencePaths = new List<string>();
            if (spec.referenceImages != null)
            {
                foreach (var reference in spec.referenceImages)
                {
                    if (reference != null && !string.IsNullOrWhiteSpace(reference.assetPath))
                    {
                        referencePaths.Add(ResolveAssetAbsolutePath(projectRoot, reference.assetPath));
                    }
                }
            }

            if (referencePaths.Count > 0 &&
                TryCreateReferenceComparisons(
                    referencePaths[0],
                    result.MainImagePath,
                    folder,
                    mainWidth,
                    mainHeight,
                    out var referenceImagePath,
                    out var overlayImagePath,
                    out var differenceImagePath))
            {
                result.ReferenceImagePath = referenceImagePath;
                result.OverlayImagePath = overlayImagePath;
                result.DifferenceImagePath = differenceImagePath;
            }

            var specAssetPath = AssetDatabase.GetAssetPath(specAsset);
            var manifest = new AIBlockoutReviewManifest
            {
                blockoutType = spec.blockoutType,
                displayName = displayName,
                generatedAtUtc = DateTime.UtcNow.ToString("O"),
                specAssetPath = specAssetPath,
                specAbsolutePath = ResolveAssetAbsolutePath(projectRoot, specAssetPath),
                specSnapshotPath = result.SceneSpecSnapshotPath,
                specSchemaPath = ResolveModelSchemaPath(),
                referenceImageAbsolutePaths = referencePaths.ToArray(),
                mainImagePath = result.MainImagePath,
                referenceImagePath = result.ReferenceImagePath,
                overlayImagePath = result.OverlayImagePath,
                differenceImagePath = result.DifferenceImagePath,
                sideImagePath = result.SideImagePath,
                topImagePath = result.TopImagePath,
                summaryPath = result.AssetReportPath,
                projectionReportPath = result.ProjectionReportPath,
                reviewRequestPath = result.ReviewRequestPath
            };

            File.WriteAllText(result.ManifestPath, JsonUtility.ToJson(manifest, true));
            File.WriteAllText(result.ReviewRequestPath, BuildBlockoutReviewRequest(manifest));
            Debug.Log($"AIBuilder 白盒评审包已生成：{result.FolderPath}");
            if (options.RevealInFinder && !Application.isBatchMode)
            {
                EditorUtility.RevealInFinder(result.FolderPath);
            }

            return result;
        }

        private static void WarmUpCamera(Camera camera, int width, int height)
        {
            var previousTarget = camera.targetTexture;
            var renderTexture = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.ReleaseTemporary(renderTexture);
            }
        }

        private static void ConfigureBlockoutCamera(
            Camera camera,
            Bounds bounds,
            ProBuilderModelCameraSpec cameraSpec,
            ProBuilderReferenceFrameSpec referenceFrame,
            Transform generatedRoot)
        {
            var radius = Mathf.Max(1f, bounds.extents.magnitude);
            var distance = Mathf.Max(5f, radius / Mathf.Tan(22.5f * Mathf.Deg2Rad) * 1.15f);
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.56f, 0.66f, 0.67f, 1f);
            camera.fieldOfView = 45f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = Mathf.Max(1000f, distance * 4f);
            if (referenceFrame != null && referenceFrame.enabled && referenceFrame.width > 0 && referenceFrame.height > 0)
            {
                camera.aspect = referenceFrame.width / (float)referenceFrame.height;
            }

            if (cameraSpec != null && cameraSpec.enabled && generatedRoot != null)
            {
                var position = generatedRoot.TransformPoint(cameraSpec.position);
                var lookAt = generatedRoot.TransformPoint(cameraSpec.lookAt);
                var direction = lookAt - position;
                camera.fieldOfView = Mathf.Clamp(cameraSpec.fieldOfView, 10f, 120f);
                camera.transform.position = position;
                camera.transform.rotation = Quaternion.LookRotation(
                    direction.sqrMagnitude < 0.0001f ? Vector3.forward : direction,
                    Vector3.up);
                return;
            }

            camera.transform.position = bounds.center + new Vector3(0f, radius * 0.3f, -distance);
            camera.transform.rotation = Quaternion.LookRotation(bounds.center - camera.transform.position, Vector3.up);
        }

        private static void CaptureSpatialView(
            Camera source,
            Bounds bounds,
            string path,
            int width,
            int height,
            bool top)
        {
            var cameraObject = new GameObject(top ? "AIBuilder_TopReviewCamera" : "AIBuilder_SideReviewCamera")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.CopyFrom(source);
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = Mathf.Max(2f, top
                    ? Mathf.Max(bounds.extents.x, bounds.extents.z) * 1.2f
                    : Mathf.Max(bounds.extents.y, bounds.extents.z) * 1.2f);

                var distance = Mathf.Max(10f, bounds.size.magnitude * 1.5f);
                if (top)
                {
                    camera.transform.position = bounds.center + Vector3.up * distance;
                    camera.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                }
                else
                {
                    camera.transform.position = bounds.center + Vector3.right * distance;
                    camera.transform.rotation = Quaternion.LookRotation(bounds.center - camera.transform.position, Vector3.up);
                }

                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = distance * 3f;
                CaptureCamera(camera, path, width, height);
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }

        private static void CaptureCamera(Camera camera, string path, int width, int height)
        {
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var renderTexture = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Texture2D image = null;
            try
            {
                camera.targetTexture = renderTexture;
                RenderTexture.active = renderTexture;
                camera.Render();

                image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                image.Apply(false, false);
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                if (image != null)
                {
                    Object.DestroyImmediate(image);
                }

                RenderTexture.ReleaseTemporary(renderTexture);
            }
        }

        private static bool TryCreateReferenceComparisons(
            string referencePath,
            string mainImagePath,
            string folder,
            int width,
            int height,
            out string resizedReferencePath,
            out string overlayPath,
            out string differencePath)
        {
            resizedReferencePath = string.Empty;
            overlayPath = string.Empty;
            differencePath = string.Empty;
            if (string.IsNullOrWhiteSpace(referencePath) || !File.Exists(referencePath) || !File.Exists(mainImagePath))
            {
                return false;
            }

            Texture2D reference = null;
            Texture2D main = null;
            Texture2D resized = null;
            Texture2D overlay = null;
            Texture2D difference = null;
            try
            {
                reference = LoadImage(referencePath);
                main = LoadImage(mainImagePath);
                resized = ResizeImage(reference, width, height);
                var mainColors = main.GetPixels();
                var referenceColors = resized.GetPixels();
                var overlayColors = new Color[mainColors.Length];
                var differenceColors = new Color[mainColors.Length];
                for (var i = 0; i < mainColors.Length; i++)
                {
                    overlayColors[i] = Color.Lerp(mainColors[i], referenceColors[i], 0.5f);
                    differenceColors[i] = new Color(
                        Mathf.Clamp01(Mathf.Abs(mainColors[i].r - referenceColors[i].r) * 2f),
                        Mathf.Clamp01(Mathf.Abs(mainColors[i].g - referenceColors[i].g) * 2f),
                        Mathf.Clamp01(Mathf.Abs(mainColors[i].b - referenceColors[i].b) * 2f),
                        1f);
                }

                overlay = new Texture2D(width, height, TextureFormat.RGB24, false);
                overlay.SetPixels(overlayColors);
                overlay.Apply(false, false);
                difference = new Texture2D(width, height, TextureFormat.RGB24, false);
                difference.SetPixels(differenceColors);
                difference.Apply(false, false);

                resizedReferencePath = Normalize(Path.Combine(folder, "Reference.png"));
                overlayPath = Normalize(Path.Combine(folder, "Overlay.png"));
                differencePath = Normalize(Path.Combine(folder, "Difference.png"));
                File.WriteAllBytes(resizedReferencePath, resized.EncodeToPNG());
                File.WriteAllBytes(overlayPath, overlay.EncodeToPNG());
                File.WriteAllBytes(differencePath, difference.EncodeToPNG());
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("AIBuilder 无法生成参考叠图：" + exception.Message);
                return false;
            }
            finally
            {
                Object.DestroyImmediate(reference);
                Object.DestroyImmediate(main);
                Object.DestroyImmediate(resized);
                Object.DestroyImmediate(overlay);
                Object.DestroyImmediate(difference);
            }
        }

        private static Texture2D LoadImage(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false))
            {
                Object.DestroyImmediate(texture);
                throw new InvalidDataException("无法读取图片：" + path);
            }

            return texture;
        }

        private static Texture2D ResizeImage(Texture2D source, int width, int height)
        {
            var result = new Texture2D(width, height, TextureFormat.RGB24, false);
            var colors = new Color[width * height];
            for (var y = 0; y < height; y++)
            {
                var v = (y + 0.5f) / height;
                for (var x = 0; x < width; x++)
                {
                    colors[y * width + x] = source.GetPixelBilinear((x + 0.5f) / width, v);
                }
            }

            result.SetPixels(colors);
            result.Apply(false, false);
            return result;
        }

        private static string BuildProjectionReport(ProBuilderModelSpec spec, GameObject root, Camera camera)
        {
            var lines = new List<string>
            {
                "# 屏幕投影误差报告",
                string.Empty,
                "坐标采用参考图左上角为原点的归一化坐标。IoU 越接近 1，主要画面区域越吻合。",
                string.Empty,
                "| 元素 | 角色 | 目标区域 | 生成区域 | IoU | 中心偏差 |",
                "|---|---|---:|---:|---:|---:|"
            };

            var measured = 0;
            var totalIou = 0f;
            var elements = spec.sceneAnalysis?.enabled == true ? spec.sceneAnalysis.elements : null;
            if (elements != null)
            {
                foreach (var element in elements)
                {
                    if (element == null || element.screenPolygon == null || element.screenPolygon.Count < 3)
                    {
                        continue;
                    }

                    var target = CalculateScreenBounds(element.screenPolygon);
                    if (string.Equals(element.role, "negative_space", StringComparison.OrdinalIgnoreCase))
                    {
                        lines.Add($"| {element.label} | 负空间 | {FormatRect(target)} | 人工检查遮挡 | - | - |");
                        continue;
                    }

                    if (!TryProjectPartBounds(root, element.partName, camera, out var projected))
                    {
                        lines.Add($"| {element.label} | {element.role} | {FormatRect(target)} | 未找到部件 | 0.000 | - |");
                        continue;
                    }

                    var iou = CalculateRectIou(target, projected);
                    var centerError = Vector2.Distance(target.center, projected.center);
                    lines.Add($"| {element.label} | {element.role} | {FormatRect(target)} | {FormatRect(projected)} | {iou:F3} | {centerError:F3} |");
                    measured++;
                    totalIou += iou;
                }
            }

            lines.Add(string.Empty);
            lines.Add(measured > 0
                ? $"- 已测量元素：{measured}，平均矩形 IoU：{totalIou / measured:F3}。"
                : "- 当前任务没有可测量的屏幕元素。");
            lines.Add($"- 负空间、复杂遮挡和轮廓细节仍需结合 Overlay.png 人工复查。\n");
            return string.Join("\n", lines);
        }

        private static Rect CalculateScreenBounds(IReadOnlyList<Vector2> polygon)
        {
            var minX = 1f;
            var maxX = 0f;
            var minY = 1f;
            var maxY = 0f;
            foreach (var point in polygon)
            {
                minX = Mathf.Min(minX, point.x);
                maxX = Mathf.Max(maxX, point.x);
                minY = Mathf.Min(minY, point.y);
                maxY = Mathf.Max(maxY, point.y);
            }

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        private static bool TryProjectPartBounds(GameObject root, string partName, Camera camera, out Rect rect)
        {
            rect = default;
            if (string.IsNullOrWhiteSpace(partName))
            {
                return false;
            }

            Transform target = null;
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (string.Equals(transform.name, partName, StringComparison.OrdinalIgnoreCase))
                {
                    target = transform;
                    break;
                }
            }

            if (target == null)
            {
                return false;
            }

            var hasPoint = false;
            var minX = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var minY = float.PositiveInfinity;
            var maxY = float.NegativeInfinity;
            foreach (var mesh in target.GetComponentsInChildren<ProBuilderMesh>(true))
            {
                foreach (var position in mesh.positions)
                {
                    var viewport = camera.WorldToViewportPoint(mesh.transform.TransformPoint(position));
                    if (viewport.z <= 0f)
                    {
                        continue;
                    }

                    hasPoint = true;
                    minX = Mathf.Min(minX, viewport.x);
                    maxX = Mathf.Max(maxX, viewport.x);
                    minY = Mathf.Min(minY, viewport.y);
                    maxY = Mathf.Max(maxY, viewport.y);
                }
            }

            if (!hasPoint)
            {
                return false;
            }

            rect = Rect.MinMaxRect(minX, 1f - maxY, maxX, 1f - minY);
            return true;
        }

        private static float CalculateRectIou(Rect a, Rect b)
        {
            var minX = Mathf.Max(a.xMin, b.xMin);
            var maxX = Mathf.Min(a.xMax, b.xMax);
            var minY = Mathf.Max(a.yMin, b.yMin);
            var maxY = Mathf.Min(a.yMax, b.yMax);
            var intersection = Mathf.Max(0f, maxX - minX) * Mathf.Max(0f, maxY - minY);
            var union = a.width * a.height + b.width * b.height - intersection;
            return union <= 0f ? 0f : intersection / union;
        }

        private static string FormatRect(Rect rect)
        {
            return $"({rect.x:F3}, {rect.y:F3}, {rect.width:F3}, {rect.height:F3})";
        }

        private static Bounds CalculateBounds(SceneSpec spec, GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (var i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }

                if (bounds.size.sqrMagnitude > 0.0001f)
                {
                    return bounds;
                }
            }

            var terrainSize = spec.terrain != null && spec.terrain.enabled
                ? new Vector3(spec.terrain.size.x, 5f, spec.terrain.size.y)
                : new Vector3(10f, 5f, 10f);
            return new Bounds(root.transform.position + Vector3.up * terrainSize.y * 0.5f, terrainSize);
        }

        private static Bounds CalculateBounds(GameObject root, Vector3 fallbackSize)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (var i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }

                if (bounds.size.sqrMagnitude > 0.0001f)
                {
                    return bounds;
                }
            }

            return new Bounds(root.transform.position + Vector3.up * fallbackSize.y * 0.5f, fallbackSize);
        }

        private static string ResolveOutputRoot(string projectRoot, string configured)
        {
            var allowedRoot = Path.GetFullPath(Path.Combine(projectRoot, "Library", "AIBuilder"));
            var requested = string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(allowedRoot, "Reviews")
                : Path.IsPathRooted(configured)
                    ? configured
                    : Path.Combine(projectRoot, configured);
            requested = Path.GetFullPath(requested);

            var allowedPrefix = allowedRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!requested.StartsWith(allowedPrefix, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(requested, allowedRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("评审包输出目录必须位于项目 Library/AIBuilder 下。", nameof(configured));
            }

            return requested;
        }

        private static string CreateUniqueFolder(string outputRoot, string sceneName)
        {
            var baseName = Sanitize(sceneName) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            var folder = Path.Combine(outputRoot, baseName);
            var suffix = 1;
            while (Directory.Exists(folder))
            {
                folder = Path.Combine(outputRoot, baseName + "_" + suffix++);
            }

            Directory.CreateDirectory(folder);
            return folder;
        }

        private static string BuildReviewRequest(AIBuilderReviewManifest manifest)
        {
            var lines = new List<string>
            {
                "# Codex 场景评审请求",
                string.Empty,
                "请把参考图视为三维世界，对照生成结果进行下一轮 SceneSpec 修正。",
                string.Empty,
                $"- 待修改 SceneSpec：`{manifest.sceneSpecAbsolutePath}`",
                $"- 本次生成快照：`{manifest.sceneSpecSnapshotPath}`",
                $"- JSON Schema：`{manifest.sceneSpecSchemaPath}`",
                $"- 参考图：`{manifest.referenceImageAbsolutePath}`",
                $"- 主视角：`{manifest.mainImagePath}`",
                $"- 侧视角：`{manifest.sideImagePath}`",
                $"- 俯视角：`{manifest.topImagePath}`",
                $"- 资产决策报告：`{manifest.assetReportPath}`",
                string.Empty,
                "## 检查顺序",
                string.Empty,
                "1. 主视角构图、主体比例和画面层次。",
                "2. 侧视角深度、遮挡和前中后景关系。",
                "3. 俯视角密度、边界、空区和散布节奏。",
                "4. 关键词或程序化回退是否应替换为精确 Prefab。",
                "5. 只修改 SceneSpec；先重新预览，不直接保存场景。",
                string.Empty,
                "输出：问题清单、修改理由、更新后的 SceneSpec JSON。"
            };
            return string.Join("\n", lines) + "\n";
        }

        private static string BuildBlockoutReviewRequest(AIBlockoutReviewManifest manifest)
        {
            var lines = new List<string>
            {
                "# Codex 白盒复查请求",
                string.Empty,
                "请把参考图视为三维空间，对照 ProBuilder 白盒结果修正体块、比例和布局。",
                string.Empty,
                $"- 待修改配置：`{manifest.specAbsolutePath}`",
                $"- 本次生成快照：`{manifest.specSnapshotPath}`",
                $"- JSON Schema：`{manifest.specSchemaPath}`",
                $"- 主视角：`{manifest.mainImagePath}`",
                $"- 参考图标准帧：`{manifest.referenceImagePath}`",
                $"- 50% 叠图：`{manifest.overlayImagePath}`",
                $"- 像素差异图：`{manifest.differenceImagePath}`",
                $"- 侧视角：`{manifest.sideImagePath}`",
                $"- 俯视角：`{manifest.topImagePath}`",
                $"- 屏幕投影误差：`{manifest.projectionReportPath}`",
                string.Empty,
                "## 参考图"
            };

            if (manifest.referenceImageAbsolutePaths == null || manifest.referenceImageAbsolutePaths.Length == 0)
            {
                lines.Add("- 参考图由当前 Codex 任务提供，未写入 Unity 资产路径。");
            }
            else
            {
                foreach (var referencePath in manifest.referenceImageAbsolutePaths)
                {
                    lines.Add($"- `{referencePath}`");
                }
            }

            lines.AddRange(new[]
            {
                string.Empty,
                "## 检查顺序",
                string.Empty,
                "1. 先检查 Overlay.png 的固定相机构图、负空间和主要轮廓。",
                "2. 再检查 ProjectionReport.md 的元素投影位置与尺寸。",
                "3. 使用侧视角和俯视角复查深度、遮挡、通道和隐藏面推断。",
                "4. 只修改 ProBuilderModelSpec；先重新预览，不直接写入场景或 Prefab。",
                string.Empty,
                "输出：问题清单、修改理由、更新后的 ProBuilderModelSpec JSON。"
            });
            return string.Join("\n", lines) + "\n";
        }

        private static string ResolveAssetAbsolutePath(string projectRoot, string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return string.Empty;
            }

            if (Path.IsPathRooted(assetPath))
            {
                return Normalize(Path.GetFullPath(assetPath));
            }

            if (assetPath.StartsWith("Packages/", StringComparison.Ordinal))
            {
                var packageInfo = PackageManagerInfo.FindForAssetPath(assetPath);
                if (packageInfo != null)
                {
                    var packagePrefix = "Packages/" + packageInfo.name + "/";
                    var relative = assetPath.StartsWith(packagePrefix, StringComparison.Ordinal)
                        ? assetPath.Substring(packagePrefix.Length)
                        : string.Empty;
                    return Normalize(Path.Combine(packageInfo.resolvedPath, relative));
                }
            }

            return Normalize(Path.GetFullPath(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar))));
        }

        private static string ResolveSceneSchemaPath()
        {
            var packageInfo = PackageManagerInfo.FindForAssetPath("Packages/com.xuyuchen.ai-builder/package.json");
            return packageInfo == null
                ? string.Empty
                : Normalize(Path.Combine(packageInfo.resolvedPath, "Schemas~", "SceneSpec.v2.schema.json"));
        }

        private static string ResolveModelSchemaPath()
        {
            var packageInfo = PackageManagerInfo.FindForAssetPath("Packages/com.xuyuchen.ai-builder/package.json");
            return packageInfo == null
                ? string.Empty
                : Normalize(Path.Combine(packageInfo.resolvedPath, "Schemas~", "ProBuilderModelSpec.v2.schema.json"));
        }

        private static void ResolveMainResolution(
            AIBuilderReviewCaptureOptions options,
            ProBuilderReferenceFrameSpec referenceFrame,
            out int width,
            out int height)
        {
            width = options.MainWidth;
            height = options.MainHeight;
            if (referenceFrame == null || !referenceFrame.enabled || !referenceFrame.lockAspect ||
                referenceFrame.width <= 0 || referenceFrame.height <= 0)
            {
                return;
            }

            height = Mathf.RoundToInt(width * referenceFrame.height / (float)referenceFrame.width);
        }

        private static void ValidateResolution(int width, int height)
        {
            if (width < 64 || height < 64 || width > 4096 || height > 4096)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "截图分辨率必须在 64 到 4096 之间。");
            }
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "SceneReview";
            }

            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return value.Trim().Replace(' ', '_');
        }

        private static string Normalize(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetFullPath(path).Replace('\\', '/');
        }
    }
}
