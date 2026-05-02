using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AIBuilder;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AIBuilder.EditorTools
{
    public sealed class AssetCatalogWindow : EditorWindow
    {
        private bool useSelectionOnly = true;
        private bool includeChildObjects;
        private bool sortByName = true;
        private float spacing = 1.2f;
        private float maxRowWidth = 28f;
        private float groundY;
        private float labelVerticalPadding = 0.25f;
        private int captureWidth = 1920;
        private int captureHeight = 1080;
        private string outputFolder = "Assets/AIBuilder/Generated/AssetCatalogShots";

        [MenuItem("AIBuilder/Asset Catalog Capture")]
        public static void Open()
        {
            GetWindow<AssetCatalogWindow>("Asset Catalog");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Asset Catalog Layout", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Drop candidate assets into the scene, select them if needed, then build a catalog. The tool lays them out by world bounds, adds name labels, creates front/left/right/top cameras, and captures PNG views.",
                MessageType.Info);

            useSelectionOnly = EditorGUILayout.Toggle("Use Selection If Any", useSelectionOnly);
            includeChildObjects = EditorGUILayout.Toggle("Treat Children As Items", includeChildObjects);
            sortByName = EditorGUILayout.Toggle("Sort By Name", sortByName);
            spacing = Mathf.Max(0.05f, EditorGUILayout.FloatField("Spacing", spacing));
            maxRowWidth = Mathf.Max(1f, EditorGUILayout.FloatField("Max Row Width", maxRowWidth));
            groundY = EditorGUILayout.FloatField("Ground Y", groundY);
            labelVerticalPadding = Mathf.Max(0f, EditorGUILayout.FloatField("Label Padding", labelVerticalPadding));

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Capture", EditorStyles.boldLabel);
            captureWidth = Mathf.Max(128, EditorGUILayout.IntField("Capture Width", captureWidth));
            captureHeight = Mathf.Max(128, EditorGUILayout.IntField("Capture Height", captureHeight));
            outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);

            EditorGUILayout.Space(10f);
            if (GUILayout.Button("Build / Reset Catalog Layout", GUILayout.Height(30f)))
            {
                AssetCatalogBuilder.BuildCatalogFromCurrentScene(CreateSettings(), resetCameras: true);
            }

            if (GUILayout.Button("Create / Reset Cameras From Current Layout"))
            {
                AssetCatalogBuilder.CreateOrResetCatalogCameras(CreateSettings());
            }

            if (GUILayout.Button("Capture All Catalog Cameras", GUILayout.Height(30f)))
            {
                AssetCatalogBuilder.CaptureAllCatalogCameras(CreateSettings());
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "After building the layout, you can manually adjust the generated cameras under AssetCatalog_Rig/Cameras. Capturing does not reset camera transforms.",
                MessageType.None);
        }

        private AssetCatalogSettings CreateSettings()
        {
            return new AssetCatalogSettings
            {
                UseSelectionIfAny = useSelectionOnly,
                IncludeChildObjects = includeChildObjects,
                SortByName = sortByName,
                Spacing = spacing,
                MaxRowWidth = maxRowWidth,
                GroundY = groundY,
                LabelVerticalPadding = labelVerticalPadding,
                CaptureWidth = captureWidth,
                CaptureHeight = captureHeight,
                OutputFolder = outputFolder
            };
        }
    }

    public sealed class AssetCatalogSettings
    {
        public bool UseSelectionIfAny = true;
        public bool IncludeChildObjects;
        public bool SortByName = true;
        public float Spacing = 1.2f;
        public float MaxRowWidth = 28f;
        public float GroundY;
        public float LabelVerticalPadding = 0.25f;
        public int CaptureWidth = 1920;
        public int CaptureHeight = 1080;
        public string OutputFolder = "Assets/AIBuilder/Generated/AssetCatalogShots";
    }

    public sealed class AssetCatalogBuildResult
    {
        public GameObject Rig;
        public List<GameObject> Items = new List<GameObject>();
        public Bounds OverallBounds;
    }

    public static class AssetCatalogBuilder
    {
        public const string RigName = "AssetCatalog_Rig";
        public const string LabelsRootName = "Labels";
        public const string CamerasRootName = "Cameras";
        public const string LightingRootName = "Lighting";
        public const string CameraPrefix = "AssetCatalog_Camera_";

        private const string FrontCameraName = CameraPrefix + "Front";
        private const string LeftCameraName = CameraPrefix + "Left";
        private const string RightCameraName = CameraPrefix + "Right";
        private const string TopCameraName = CameraPrefix + "Top";

        [MenuItem("AIBuilder/Asset Catalog/Build Layout From Scene")]
        public static void BuildLayoutFromSceneMenu()
        {
            BuildCatalogFromCurrentScene(new AssetCatalogSettings(), resetCameras: true);
        }

        [MenuItem("AIBuilder/Asset Catalog/Capture All Views")]
        public static void CaptureAllViewsMenu()
        {
            CaptureAllCatalogCameras(new AssetCatalogSettings());
        }

        public static AssetCatalogBuildResult BuildCatalogFromCurrentScene(AssetCatalogSettings settings, bool resetCameras)
        {
            settings ??= new AssetCatalogSettings();

            var items = CollectCatalogObjects(settings);
            if (items.Count == 0)
            {
                Debug.LogWarning("Asset Catalog: no valid scene objects found. Select placed assets or add root objects with Renderer/Collider/MeshFilter components.");
                return null;
            }

            EnsureAssetFolder(settings.OutputFolder);

            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build Asset Catalog Layout");

            var rig = GetOrCreateRig();
            var labelsRoot = GetOrCreateChild(rig.transform, LabelsRootName);
            var camerasRoot = GetOrCreateChild(rig.transform, CamerasRootName);
            var lightingRoot = GetOrCreateChild(rig.transform, LightingRootName);

            ClearChildren(labelsRoot);
            LayoutItems(items, settings);
            CreateLabels(items, labelsRoot, settings);
            EnsureCatalogLight(lightingRoot);

            var result = new AssetCatalogBuildResult
            {
                Rig = rig,
                Items = items
            };
            result.OverallBounds = CalculateOverallBounds(items);

            CreateOrUpdateCatalogCameras(camerasRoot, result.OverallBounds, settings, resetCameras);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log($"Asset Catalog: arranged {items.Count} objects and generated labels/cameras under {RigName}.");
            return result;
        }

        public static void CreateOrResetCatalogCameras(AssetCatalogSettings settings)
        {
            settings ??= new AssetCatalogSettings();
            var items = CollectCatalogObjects(settings);
            if (items.Count == 0)
            {
                Debug.LogWarning("Asset Catalog: no valid scene objects found for camera framing.");
                return;
            }

            var rig = GetOrCreateRig();
            var camerasRoot = GetOrCreateChild(rig.transform, CamerasRootName);
            var bounds = CalculateOverallBounds(items);
            CreateOrUpdateCatalogCameras(camerasRoot, bounds, settings, resetCameras: true);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("Asset Catalog: reset front/left/right/top cameras from current object bounds.");
        }

        public static List<string> CaptureAllCatalogCameras(AssetCatalogSettings settings)
        {
            settings ??= new AssetCatalogSettings();
            EnsureAssetFolder(settings.OutputFolder);

            var cameras = FindCatalogCameras();
            if (cameras.Count == 0)
            {
                Debug.LogWarning("Asset Catalog: no catalog cameras found. Build the catalog layout first.");
                return new List<string>();
            }

            var labels = Object.FindObjectsOfType<AssetCatalogLabel>(true);
            var capturedPaths = new List<string>();
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            foreach (var camera in cameras)
            {
                foreach (var label in labels)
                {
                    if (label == null)
                    {
                        continue;
                    }

                    label.forcedCamera = camera;
                    label.Refresh(camera);
                    EditorUtility.SetDirty(label);
                }

                var path = CaptureCamera(camera, settings, timestamp);
                if (!string.IsNullOrEmpty(path))
                {
                    capturedPaths.Add(path);
                }
            }

            foreach (var label in labels)
            {
                if (label != null)
                {
                    label.forcedCamera = null;
                    EditorUtility.SetDirty(label);
                }
            }

            AssetDatabase.Refresh();
            WriteCaptureIndex(settings, capturedPaths, labels);
            Debug.Log($"Asset Catalog: captured {capturedPaths.Count} view(s) to {settings.OutputFolder}.");
            return capturedPaths;
        }

        public static void CreatePrimitiveDemoAndCapture()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "AssetCatalog_Demo";

            CreateDemoPrimitive("Demo_Tree_Cylinder", PrimitiveType.Cylinder, new Vector3(-3f, 1.4f, 0f), new Vector3(0.8f, 2.8f, 0.8f), new Color(0.42f, 0.28f, 0.14f));
            CreateDemoPrimitive("Demo_Rock_Wide", PrimitiveType.Cube, new Vector3(0f, 0.45f, 0f), new Vector3(2.1f, 0.9f, 1.4f), new Color(0.56f, 0.58f, 0.52f));
            CreateDemoPrimitive("Demo_Shrub_Round", PrimitiveType.Sphere, new Vector3(2.5f, 0.75f, 0f), new Vector3(1.5f, 1.5f, 1.5f), new Color(0.2f, 0.48f, 0.12f));
            CreateDemoPrimitive("Demo_Flower_Small", PrimitiveType.Sphere, new Vector3(4.5f, 0.25f, 0f), new Vector3(0.45f, 0.45f, 0.45f), new Color(1f, 0.68f, 0.25f));

            var settings = new AssetCatalogSettings
            {
                UseSelectionIfAny = false,
                Spacing = 1.1f,
                MaxRowWidth = 10f,
                OutputFolder = "Assets/AIBuilder/Generated/AssetCatalogShots"
            };
            BuildCatalogFromCurrentScene(settings, resetCameras: true);
            CaptureAllCatalogCameras(settings);
            EnsureAssetFolder("Assets/AIBuilder/Scenes");
            EditorSceneManager.SaveScene(scene, "Assets/AIBuilder/Scenes/AssetCatalog_Demo.unity");
        }

        private static List<GameObject> CollectCatalogObjects(AssetCatalogSettings settings)
        {
            var scene = SceneManager.GetActiveScene();
            var results = new List<GameObject>();

            if (settings.UseSelectionIfAny && Selection.gameObjects.Length > 0)
            {
                var selected = Selection.gameObjects
                    .Where(go => go != null && go.scene == scene)
                    .Select(go => go.transform)
                    .ToList();

                if (settings.IncludeChildObjects)
                {
                    foreach (var transform in selected)
                    {
                        AddRenderableChildren(transform, results);
                    }
                }
                else
                {
                    foreach (var transform in selected)
                    {
                        if (!HasAncestorInSet(transform, selected) && IsValidCatalogObject(transform.gameObject) && HasRenderableBounds(transform.gameObject))
                        {
                            results.Add(transform.gameObject);
                        }
                    }
                }
            }
            else
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (settings.IncludeChildObjects)
                    {
                        AddRenderableChildren(root.transform, results);
                    }
                    else if (IsValidCatalogObject(root) && HasRenderableBounds(root))
                    {
                        results.Add(root);
                    }
                }
            }

            var unique = results
                .Where(go => go != null)
                .GroupBy(go => go.GetInstanceID())
                .Select(group => group.First());

            if (settings.SortByName)
            {
                unique = unique.OrderBy(go => go.name, StringComparer.OrdinalIgnoreCase);
            }

            return unique.ToList();
        }

        private static void LayoutItems(IReadOnlyList<GameObject> items, AssetCatalogSettings settings)
        {
            var placements = new Dictionary<GameObject, Vector3>();
            var x = 0f;
            var z = 0f;
            var rowDepth = 0f;
            var totalWidth = 0f;
            var totalDepth = 0f;

            foreach (var item in items)
            {
                AssetCatalogLabel.TryCalculateWorldBounds(item.transform, out var bounds);
                var width = Mathf.Max(0.1f, bounds.size.x);
                var depth = Mathf.Max(0.1f, bounds.size.z);

                if (x > 0f && x + width > settings.MaxRowWidth)
                {
                    totalWidth = Mathf.Max(totalWidth, x - settings.Spacing);
                    x = 0f;
                    z += rowDepth + settings.Spacing;
                    rowDepth = 0f;
                }

                placements[item] = new Vector3(x + width * 0.5f, 0f, z + depth * 0.5f);
                x += width + settings.Spacing;
                rowDepth = Mathf.Max(rowDepth, depth);
                totalWidth = Mathf.Max(totalWidth, x - settings.Spacing);
                totalDepth = Mathf.Max(totalDepth, z + rowDepth);
            }

            var layoutCenter = new Vector3(totalWidth * 0.5f, 0f, totalDepth * 0.5f);
            foreach (var item in items)
            {
                if (!placements.TryGetValue(item, out var plannedCenter))
                {
                    continue;
                }

                AssetCatalogLabel.TryCalculateWorldBounds(item.transform, out var bounds);
                var targetCenter = plannedCenter - layoutCenter;
                var delta = new Vector3(
                    targetCenter.x - bounds.center.x,
                    settings.GroundY - bounds.min.y,
                    targetCenter.z - bounds.center.z);

                Undo.RecordObject(item.transform, "Layout Catalog Item");
                item.transform.position += delta;
                EditorUtility.SetDirty(item.transform);
            }
        }

        private static void CreateLabels(IReadOnlyList<GameObject> items, Transform labelsRoot, AssetCatalogSettings settings)
        {
            var viewCamera = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.camera : Camera.main;
            foreach (var item in items)
            {
                var labelObject = new GameObject("AssetCatalog_Label_" + SanitizeName(item.name));
                Undo.RegisterCreatedObjectUndo(labelObject, "Create Catalog Label");
                labelObject.transform.SetParent(labelsRoot, false);

                var fontAsset = ResolveDefaultFontAsset();
                if (fontAsset != null)
                {
                    var text = labelObject.AddComponent<TextMeshPro>();
                    text.font = fontAsset;
                    text.text = item.name;
                    text.alignment = TextAlignmentOptions.Center;
                    text.enableAutoSizing = true;
                    text.fontSizeMin = 1f;
                    text.fontSizeMax = 3.5f;
                    text.fontStyle = FontStyles.Bold;
                    text.color = Color.white;
                    text.outlineColor = new Color(0f, 0f, 0f, 0.9f);
                    text.outlineWidth = 0.18f;
                    text.enableWordWrapping = false;
                    text.overflowMode = TextOverflowModes.Ellipsis;
                    text.rectTransform.sizeDelta = new Vector2(4f, 0.8f);
                }
                else
                {
                    var text = labelObject.AddComponent<TextMesh>();
                    text.text = item.name;
                    text.anchor = TextAnchor.MiddleCenter;
                    text.alignment = TextAlignment.Center;
                    text.fontSize = 56;
                    text.characterSize = 0.1f;
                    text.color = Color.white;
                }

                var label = labelObject.AddComponent<AssetCatalogLabel>();
                label.target = item.transform;
                label.verticalPadding = settings.LabelVerticalPadding;
                label.Refresh(viewCamera);
            }
        }

        private static TMP_FontAsset ResolveDefaultFontAsset()
        {
            try
            {
                if (TMP_Settings.defaultFontAsset != null)
                {
                    return TMP_Settings.defaultFontAsset;
                }
            }
            catch (NullReferenceException)
            {
                // TMP Essentials are optional for this tool. Fall back to legacy TextMesh below.
            }

            var guids = AssetDatabase.FindAssets("LiberationSans SDF t:TMP_FontAsset");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (fontAsset != null)
                {
                    return fontAsset;
                }
            }

            return null;
        }

        private static void CreateOrUpdateCatalogCameras(Transform camerasRoot, Bounds bounds, AssetCatalogSettings settings, bool resetCameras)
        {
            var aspect = Mathf.Max(0.1f, settings.CaptureWidth / (float)Mathf.Max(1, settings.CaptureHeight));
            var center = bounds.center;
            var maxSize = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            maxSize = Mathf.Max(1f, maxSize);
            var distance = maxSize * 2.5f + 6f;
            var verticalPadding = Mathf.Max(2f, maxSize * 0.55f);
            var horizontalPadding = Mathf.Max(1f, maxSize * 0.12f);

            CreateOrUpdateCatalogCamera(
                camerasRoot,
                FrontCameraName,
                center + new Vector3(0f, bounds.extents.y * 0.25f, -distance),
                center,
                Vector3.up,
                CalculateOrthoSize(bounds.size.x, bounds.size.y + verticalPadding, aspect, horizontalPadding),
                distance,
                resetCameras);

            CreateOrUpdateCatalogCamera(
                camerasRoot,
                LeftCameraName,
                center + new Vector3(-distance, bounds.extents.y * 0.25f, 0f),
                center,
                Vector3.up,
                CalculateOrthoSize(bounds.size.z, bounds.size.y + verticalPadding, aspect, horizontalPadding),
                distance,
                resetCameras);

            CreateOrUpdateCatalogCamera(
                camerasRoot,
                RightCameraName,
                center + new Vector3(distance, bounds.extents.y * 0.25f, 0f),
                center,
                Vector3.up,
                CalculateOrthoSize(bounds.size.z, bounds.size.y + verticalPadding, aspect, horizontalPadding),
                distance,
                resetCameras);

            CreateOrUpdateCatalogCamera(
                camerasRoot,
                TopCameraName,
                center + new Vector3(0f, distance, 0f),
                center,
                Vector3.forward,
                CalculateOrthoSize(bounds.size.x, bounds.size.z + verticalPadding, aspect, horizontalPadding),
                distance,
                resetCameras);
        }

        private static void CreateOrUpdateCatalogCamera(
            Transform camerasRoot,
            string cameraName,
            Vector3 position,
            Vector3 lookAt,
            Vector3 up,
            float orthographicSize,
            float distance,
            bool resetCamera)
        {
            var existing = camerasRoot.Find(cameraName);
            var cameraObject = existing != null ? existing.gameObject : new GameObject(cameraName);
            if (existing == null)
            {
                Undo.RegisterCreatedObjectUndo(cameraObject, "Create Catalog Camera");
                cameraObject.transform.SetParent(camerasRoot, false);
            }

            var camera = cameraObject.GetComponent<Camera>();
            if (camera == null)
            {
                camera = cameraObject.AddComponent<Camera>();
            }

            if (resetCamera || existing == null)
            {
                Undo.RecordObject(cameraObject.transform, "Reset Catalog Camera");
                cameraObject.transform.position = position;
                var direction = lookAt - position;
                cameraObject.transform.rotation = direction.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(direction.normalized, up)
                    : Quaternion.identity;

                Undo.RecordObject(camera, "Configure Catalog Camera");
                camera.orthographic = true;
                camera.orthographicSize = orthographicSize;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = distance * 4f + 20f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.78f, 0.84f, 0.82f, 1f);
                camera.allowHDR = true;
                camera.allowMSAA = true;
            }
        }

        private static string CaptureCamera(Camera camera, AssetCatalogSettings settings, string timestamp)
        {
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var renderTexture = new RenderTexture(settings.CaptureWidth, settings.CaptureHeight, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 8,
                name = camera.name + "_CaptureRT"
            };

            try
            {
                camera.targetTexture = renderTexture;
                RenderTexture.active = renderTexture;
                camera.Render();

                var texture = new Texture2D(settings.CaptureWidth, settings.CaptureHeight, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, settings.CaptureWidth, settings.CaptureHeight), 0, 0);
                texture.Apply();

                var png = texture.EncodeToPNG();
                Object.DestroyImmediate(texture);

                var fullFolder = AssetPathToFullPath(settings.OutputFolder);
                Directory.CreateDirectory(fullFolder);
                var fileName = $"{SanitizeName(camera.name)}_{timestamp}.png";
                var fullPath = Path.Combine(fullFolder, fileName);
                File.WriteAllBytes(fullPath, png);

                var assetPath = FullPathToAssetPath(fullPath);
                AssetDatabase.ImportAsset(assetPath);
                return assetPath;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
            }
        }

        private static void WriteCaptureIndex(AssetCatalogSettings settings, IReadOnlyList<string> capturedPaths, IReadOnlyList<AssetCatalogLabel> labels)
        {
            var fullFolder = AssetPathToFullPath(settings.OutputFolder);
            Directory.CreateDirectory(fullFolder);

            var lines = new List<string>
            {
                "# Asset Catalog Capture",
                string.Empty,
                $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                string.Empty,
                "## Screenshots"
            };

            foreach (var path in capturedPaths)
            {
                lines.Add($"- `{path}`");
            }

            lines.Add(string.Empty);
            lines.Add("## Objects");

            var targets = labels
                .Where(label => label != null && label.target != null)
                .Select(label => label.target)
                .GroupBy(target => target.GetInstanceID())
                .Select(group => group.First())
                .OrderBy(target => target.name, StringComparer.OrdinalIgnoreCase);

            foreach (var target in targets)
            {
                if (AssetCatalogLabel.TryCalculateWorldBounds(target, out var bounds))
                {
                    lines.Add($"- `{target.name}` size=({bounds.size.x:0.###}, {bounds.size.y:0.###}, {bounds.size.z:0.###})");
                }
                else
                {
                    lines.Add($"- `{target.name}`");
                }
            }

            var indexPath = Path.Combine(fullFolder, "AssetCatalog_Index.md");
            File.WriteAllLines(indexPath, lines, Encoding.UTF8);
            AssetDatabase.ImportAsset(FullPathToAssetPath(indexPath));
        }

        private static void EnsureCatalogLight(Transform lightingRoot)
        {
            const string lightName = "AssetCatalog_KeyLight";
            var existing = lightingRoot.Find(lightName);
            var lightObject = existing != null ? existing.gameObject : new GameObject(lightName);
            if (existing == null)
            {
                Undo.RegisterCreatedObjectUndo(lightObject, "Create Catalog Light");
                lightObject.transform.SetParent(lightingRoot, false);
            }

            var light = lightObject.GetComponent<Light>();
            if (light == null)
            {
                light = lightObject.AddComponent<Light>();
            }

            Undo.RecordObject(lightObject.transform, "Configure Catalog Light");
            lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            Undo.RecordObject(light, "Configure Catalog Light");
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            light.color = new Color(1f, 0.94f, 0.82f, 1f);
        }

        private static List<Camera> FindCatalogCameras()
        {
            var rig = GameObject.Find(RigName);
            if (rig == null)
            {
                return new List<Camera>();
            }

            var camerasRoot = rig.transform.Find(CamerasRootName);
            if (camerasRoot == null)
            {
                return new List<Camera>();
            }

            return camerasRoot.GetComponentsInChildren<Camera>(true)
                .Where(camera => camera != null && camera.name.StartsWith(CameraPrefix, StringComparison.Ordinal))
                .OrderBy(camera => CameraSortOrder(camera.name))
                .ToList();
        }

        private static int CameraSortOrder(string name)
        {
            return name switch
            {
                FrontCameraName => 0,
                LeftCameraName => 1,
                RightCameraName => 2,
                TopCameraName => 3,
                _ => 100
            };
        }

        private static float CalculateOrthoSize(float viewWidth, float viewHeight, float aspect, float horizontalPadding)
        {
            var paddedWidth = viewWidth + horizontalPadding * 2f;
            var paddedHeight = viewHeight + horizontalPadding * 2f;
            return Mathf.Max(paddedHeight * 0.5f, paddedWidth / aspect * 0.5f);
        }

        private static Bounds CalculateOverallBounds(IReadOnlyList<GameObject> items)
        {
            var hasBounds = false;
            var overall = new Bounds(Vector3.zero, Vector3.one);
            foreach (var item in items)
            {
                if (item == null || !AssetCatalogLabel.TryCalculateWorldBounds(item.transform, out var bounds))
                {
                    continue;
                }

                if (!hasBounds)
                {
                    overall = bounds;
                    hasBounds = true;
                }
                else
                {
                    overall.Encapsulate(bounds);
                }
            }

            return hasBounds ? overall : new Bounds(Vector3.zero, Vector3.one);
        }

        private static GameObject GetOrCreateRig()
        {
            var rig = GameObject.Find(RigName);
            if (rig != null)
            {
                return rig;
            }

            rig = new GameObject(RigName);
            Undo.RegisterCreatedObjectUndo(rig, "Create Asset Catalog Rig");
            return rig;
        }

        private static Transform GetOrCreateChild(Transform parent, string childName)
        {
            var child = parent.Find(childName);
            if (child != null)
            {
                return child;
            }

            var childObject = new GameObject(childName);
            Undo.RegisterCreatedObjectUndo(childObject, "Create Asset Catalog Child");
            childObject.transform.SetParent(parent, false);
            return childObject.transform;
        }

        private static void ClearChildren(Transform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                Undo.DestroyObjectImmediate(parent.GetChild(i).gameObject);
            }
        }

        private static void AddRenderableChildren(Transform root, List<GameObject> results)
        {
            if (root == null)
            {
                return;
            }

            if (IsValidCatalogObject(root.gameObject) && HasDirectRenderableBounds(root.gameObject))
            {
                results.Add(root.gameObject);
            }

            for (var i = 0; i < root.childCount; i++)
            {
                AddRenderableChildren(root.GetChild(i), results);
            }
        }

        private static bool HasAncestorInSet(Transform transform, IReadOnlyCollection<Transform> set)
        {
            var parent = transform.parent;
            while (parent != null)
            {
                if (set.Contains(parent))
                {
                    return true;
                }

                parent = parent.parent;
            }

            return false;
        }

        private static bool IsValidCatalogObject(GameObject gameObject)
        {
            if (gameObject == null || EditorUtility.IsPersistent(gameObject))
            {
                return false;
            }

            if (gameObject.name.StartsWith("AssetCatalog_", StringComparison.Ordinal))
            {
                return false;
            }

            if (gameObject.GetComponent<Camera>() != null ||
                gameObject.GetComponent<Light>() != null ||
                gameObject.GetComponent<Canvas>() != null ||
                gameObject.GetComponent<AssetCatalogLabel>() != null)
            {
                return false;
            }

            var current = gameObject.transform;
            while (current != null)
            {
                if (current.name == RigName || current.GetComponent<AssetCatalogLabel>() != null)
                {
                    return false;
                }

                current = current.parent;
            }

            return true;
        }

        private static bool HasRenderableBounds(GameObject gameObject)
        {
            return gameObject.GetComponentsInChildren<Renderer>(false).Any(renderer => renderer != null && renderer is not ParticleSystemRenderer) ||
                   gameObject.GetComponentsInChildren<Collider>(false).Any(collider => collider != null) ||
                   gameObject.GetComponentsInChildren<MeshFilter>(false).Any(filter => filter != null && filter.sharedMesh != null);
        }

        private static bool HasDirectRenderableBounds(GameObject gameObject)
        {
            return gameObject.GetComponents<Renderer>().Any(renderer => renderer != null && renderer is not ParticleSystemRenderer) ||
                   gameObject.GetComponents<Collider>().Any(collider => collider != null) ||
                   gameObject.GetComponents<MeshFilter>().Any(filter => filter != null && filter.sharedMesh != null);
        }

        private static string SanitizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Unnamed";
            }

            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return value.Replace(' ', '_').Trim();
        }

        private static void EnsureAssetFolder(string folder)
        {
            folder = NormalizeAssetPath(folder);
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parts = folder.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static string NormalizeAssetPath(string path)
        {
            path = string.IsNullOrWhiteSpace(path) ? "Assets/AIBuilder/Generated/AssetCatalogShots" : path;
            path = path.Replace('\\', '/').TrimEnd('/');
            if (!path.StartsWith("Assets", StringComparison.Ordinal))
            {
                path = "Assets/" + path.TrimStart('/');
            }

            return path;
        }

        private static string AssetPathToFullPath(string assetPath)
        {
            assetPath = NormalizeAssetPath(assetPath);
            var relative = assetPath.Substring("Assets".Length).TrimStart('/', '\\');
            return Path.GetFullPath(Path.Combine(Application.dataPath, relative));
        }

        private static string FullPathToAssetPath(string fullPath)
        {
            var projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/').TrimEnd('/');
            var normalizedFullPath = Path.GetFullPath(fullPath).Replace('\\', '/');
            if (!normalizedFullPath.StartsWith(projectPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Path is outside project: {fullPath}");
            }

            return normalizedFullPath.Substring(projectPath.Length + 1);
        }

        private static void CreateDemoPrimitive(string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color)
        {
            var gameObject = GameObject.CreatePrimitive(type);
            gameObject.name = name;
            gameObject.transform.position = position;
            gameObject.transform.localScale = scale;

            var renderer = gameObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if (material.shader == null)
                {
                    material = new Material(Shader.Find("Standard"));
                }

                material.name = name + "_Material";
                material.color = color;
                renderer.sharedMaterial = material;
            }
        }
    }
}
