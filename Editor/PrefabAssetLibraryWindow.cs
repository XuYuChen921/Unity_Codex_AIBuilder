using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AIBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AIBuilder.EditorTools
{
    public sealed class PrefabAssetLibraryWindow : EditorWindow
    {
        private string sourceFolder = "Assets";
        private string outputFolder = PrefabAssetLibraryBuilder.DefaultOutputFolder;
        private string fastMetadataOutputFolder = PrefabAssetLibraryBuilder.DefaultFastMetadataOutputFolder;
        private int captureWidth = 1024;
        private int captureHeight = 1024;
        private bool onlyVisiblePrefabs = true;
        private bool includeTopView = true;
        private bool includeSideView = true;
        private bool showAdvanced;
        private int maxItemsPerRun;

        [MenuItem("AI构建器/Prefab资产库/打开资产库面板")]
        public static void Open()
        {
            GetWindow<PrefabAssetLibraryWindow>("Prefab Library");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Prefab Asset Library", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Scans project prefabs, renders clean front/side/top screenshots, and writes per-prefab metadata for later reference-image scene generation.",
                MessageType.Info);

            sourceFolder = EditorGUILayout.TextField("Source Folder", sourceFolder);
            outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
            fastMetadataOutputFolder = EditorGUILayout.TextField("Fast Metadata Output", fastMetadataOutputFolder);
            captureWidth = Mathf.Max(256, EditorGUILayout.IntField("Capture Width", captureWidth));
            captureHeight = Mathf.Max(256, EditorGUILayout.IntField("Capture Height", captureHeight));
            onlyVisiblePrefabs = EditorGUILayout.Toggle("Only Visible Prefabs", onlyVisiblePrefabs);
            includeSideView = EditorGUILayout.Toggle("Capture Side", includeSideView);
            includeTopView = EditorGUILayout.Toggle("Capture Top", includeTopView);

            showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Advanced");
            if (showAdvanced)
            {
                maxItemsPerRun = Mathf.Max(0, EditorGUILayout.IntField("Max Items Per Run", maxItemsPerRun));
                EditorGUILayout.HelpBox("0 means no limit. Use a small limit for quick smoke tests on very large projects.", MessageType.None);
            }

            EditorGUILayout.Space(10f);

            if (GUILayout.Button("Scan Missing / Changed Prefabs", GUILayout.Height(28f)))
            {
                var report = PrefabAssetLibraryBuilder.Scan(CreateSettings());
                EditorUtility.DisplayDialog(
                    "Prefab Asset Library",
                    $"Found {report.TotalPrefabs} prefab(s).\nNew: {report.NewCount}\nChanged: {report.ChangedCount}\nUp to date: {report.UpToDateCount}\nSkipped: {report.SkippedCount}",
                    "OK");
            }

            if (GUILayout.Button("Build Missing / Changed Library", GUILayout.Height(32f)))
            {
                PrefabAssetLibraryBuilder.BuildLibrary(CreateSettings(), rebuildAll: false);
            }

            if (GUILayout.Button("Rebuild All Library"))
            {
                if (EditorUtility.DisplayDialog("Prefab Asset Library", "Rebuild all prefab screenshots and metadata?", "Rebuild", "Cancel"))
                {
                    PrefabAssetLibraryBuilder.BuildLibrary(CreateSettings(), rebuildAll: true);
                }
            }

            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("Fast Metadata Only", EditorStyles.boldLabel);
            if (GUILayout.Button("Build Fast Metadata Missing / Changed", GUILayout.Height(30f)))
            {
                PrefabAssetLibraryBuilder.BuildFastMetadataIndex(CreateSettings(), rebuildAll: false);
            }

            if (GUILayout.Button("Rebuild Fast Metadata Index"))
            {
                if (EditorUtility.DisplayDialog("Prefab Asset Library", "Rebuild fast metadata for all prefabs without screenshots?", "Rebuild", "Cancel"))
                {
                    PrefabAssetLibraryBuilder.BuildFastMetadataIndex(CreateSettings(), rebuildAll: true);
                }
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "Output contains one folder per prefab plus a manifest and index. Incremental builds compare source GUID and dependency hash, so newly imported or changed prefabs are detected.",
                MessageType.None);
        }

        private PrefabAssetLibrarySettings CreateSettings()
        {
            return new PrefabAssetLibrarySettings
            {
                SourceFolder = sourceFolder,
                OutputFolder = outputFolder,
                FastMetadataOutputFolder = fastMetadataOutputFolder,
                CaptureWidth = captureWidth,
                CaptureHeight = captureHeight,
                OnlyVisiblePrefabs = onlyVisiblePrefabs,
                IncludeSideView = includeSideView,
                IncludeTopView = includeTopView,
                MaxItemsPerRun = maxItemsPerRun
            };
        }
    }

    public sealed class PrefabAssetLibrarySettings
    {
        public string SourceFolder = "Assets";
        public string OutputFolder = PrefabAssetLibraryBuilder.DefaultOutputFolder;
        public string FastMetadataOutputFolder = PrefabAssetLibraryBuilder.DefaultFastMetadataOutputFolder;
        public int CaptureWidth = 1024;
        public int CaptureHeight = 1024;
        public bool OnlyVisiblePrefabs = true;
        public bool IncludeSideView = true;
        public bool IncludeTopView = true;
        public int MaxItemsPerRun;
    }

    [Serializable]
    public sealed class PrefabAssetLibraryManifest
    {
        public string generatedAt;
        public string sourceFolder;
        public string outputFolder;
        public int captureWidth;
        public int captureHeight;
        public List<PrefabAssetMetadata> items = new List<PrefabAssetMetadata>();
    }

    [Serializable]
    public sealed class PrefabAssetMetadata
    {
        public string name;
        public string prefabPath;
        public string sourceGuid;
        public string sourceHash;
        public string entryFolder;
        public string frontImage;
        public string sideImage;
        public string topImage;
        public Vector3 boundsSize;
        public Vector3 boundsCenter;
        public int rendererCount;
        public int meshCount;
        public int materialCount;
        public int vertexCount;
        public string assetType;
        public string style;
        public string shape;
        public string description;
        public List<string> dominantColors = new List<string>();
        public List<string> colorFamilies = new List<string>();
        public List<string> materialNames = new List<string>();
        public List<string> meshNames = new List<string>();
        public List<string> tags = new List<string>();
        public string generatedAt;
    }

    public sealed class PrefabAssetLibraryScanReport
    {
        public int TotalPrefabs;
        public int NewCount;
        public int ChangedCount;
        public int UpToDateCount;
        public int SkippedCount;
        public readonly List<PrefabAssetLibraryWorkItem> Items = new List<PrefabAssetLibraryWorkItem>();
    }

    public sealed class PrefabAssetLibraryWorkItem
    {
        public string PrefabPath;
        public string Guid;
        public string Hash;
        public PrefabAssetMetadata ExistingMetadata;
        public PrefabAssetLibraryItemStatus Status;
        public string SkipReason;
    }

    public enum PrefabAssetLibraryItemStatus
    {
        New,
        Changed,
        UpToDate,
        Skipped
    }

    public static class PrefabAssetLibraryBuilder
    {
        public const string DefaultOutputFolder = "Assets/AIBuilder/Generated/PrefabAssetLibrary";
        public const string DefaultFastMetadataOutputFolder = "Assets/AIBuilder/Generated/PrefabAssetMetadataLibrary";

        private const string ManifestFileName = "PrefabAssetLibrary_Manifest.json";
        private const string IndexFileName = "PrefabAssetLibrary_Index.md";
        private const string MetadataFileName = "metadata.json";
        private const string SummaryFileName = "summary.md";
        private const string FrontImageName = "front.png";
        private const string SideImageName = "side.png";
        private const string TopImageName = "top.png";

        [MenuItem("AI构建器/Prefab资产库/生成缺失或变化的截图")]
        public static void BuildMissingOrChangedMenu()
        {
            BuildLibrary(new PrefabAssetLibrarySettings(), rebuildAll: false);
        }

        [MenuItem("AI构建器/Prefab资产库/全部重新生成截图")]
        public static void RebuildAllMenu()
        {
            BuildLibrary(new PrefabAssetLibrarySettings(), rebuildAll: true);
        }

        [MenuItem("AI构建器/Prefab资产库/快速扫描缺失或变化资产")]
        public static void BuildFastMetadataMenu()
        {
            BuildFastMetadataIndex(new PrefabAssetLibrarySettings(), rebuildAll: false);
        }

        public static void SmokeTestFirstThree()
        {
            BuildLibrary(
                new PrefabAssetLibrarySettings
                {
                    SourceFolder = "Assets/Imported/GhibliNature/Art/Prefabs",
                    OutputFolder = "Assets/AIBuilder/Generated/PrefabAssetLibrary_SmokeTest",
                    CaptureWidth = 512,
                    CaptureHeight = 512,
                    MaxItemsPerRun = 3
                },
                rebuildAll: true);
        }

        public static void SmokeTestFastMetadataFirstFive()
        {
            BuildFastMetadataIndex(
                new PrefabAssetLibrarySettings
                {
                    SourceFolder = "Assets/Imported/GhibliNature/Art/Prefabs",
                    FastMetadataOutputFolder = "Assets/AIBuilder/Generated/PrefabAssetMetadataLibrary_SmokeTest",
                    MaxItemsPerRun = 5
                },
                rebuildAll: true);
        }

        public static PrefabAssetLibraryScanReport Scan(PrefabAssetLibrarySettings settings)
        {
            settings = NormalizeSettings(settings);
            EnsureAssetFolder(settings.OutputFolder);

            var manifest = LoadManifest(settings.OutputFolder);
            var existingByGuid = manifest.items
                .Where(item => !string.IsNullOrEmpty(item.sourceGuid))
                .GroupBy(item => item.sourceGuid)
                .ToDictionary(group => group.Key, group => group.First());

            var prefabPaths = AssetDatabase.FindAssets("t:Prefab", new[] { settings.SourceFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Where(path => path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                .Where(path => !IsInsideFolder(path, settings.OutputFolder))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (settings.MaxItemsPerRun > 0)
            {
                prefabPaths = prefabPaths.Take(settings.MaxItemsPerRun).ToList();
            }

            var report = new PrefabAssetLibraryScanReport { TotalPrefabs = prefabPaths.Count };
            foreach (var prefabPath in prefabPaths)
            {
                var guid = AssetDatabase.AssetPathToGUID(prefabPath);
                var workItem = new PrefabAssetLibraryWorkItem
                {
                    PrefabPath = prefabPath,
                    Guid = guid,
                    Hash = ComputeDependencyHash(prefabPath)
                };

                if (settings.OnlyVisiblePrefabs && !PrefabHasVisibleRenderer(prefabPath))
                {
                    workItem.Status = PrefabAssetLibraryItemStatus.Skipped;
                    workItem.SkipReason = "No visible renderer";
                    report.SkippedCount++;
                    report.Items.Add(workItem);
                    continue;
                }

                existingByGuid.TryGetValue(guid, out var existing);
                workItem.ExistingMetadata = existing;
                if (existing == null || string.IsNullOrEmpty(existing.entryFolder))
                {
                    workItem.Status = PrefabAssetLibraryItemStatus.New;
                    report.NewCount++;
                }
                else if (existing.sourceHash != workItem.Hash || !EntryHasRequiredFiles(existing, settings))
                {
                    workItem.Status = PrefabAssetLibraryItemStatus.Changed;
                    report.ChangedCount++;
                }
                else
                {
                    workItem.Status = PrefabAssetLibraryItemStatus.UpToDate;
                    report.UpToDateCount++;
                }

                report.Items.Add(workItem);
            }

            Debug.Log($"Prefab Asset Library scan: total={report.TotalPrefabs}, new={report.NewCount}, changed={report.ChangedCount}, upToDate={report.UpToDateCount}, skipped={report.SkippedCount}.");
            return report;
        }

        public static PrefabAssetLibraryManifest BuildLibrary(PrefabAssetLibrarySettings settings, bool rebuildAll)
        {
            settings = NormalizeSettings(settings);
            EnsureAssetFolder(settings.OutputFolder);

            var scanReport = Scan(settings);
            var manifest = LoadManifest(settings.OutputFolder);
            var outputItems = manifest.items
                .Where(item => !string.IsNullOrEmpty(item.sourceGuid))
                .GroupBy(item => item.sourceGuid)
                .ToDictionary(group => group.Key, group => group.First());

            var buildItems = scanReport.Items
                .Where(item => item.Status != PrefabAssetLibraryItemStatus.Skipped)
                .Where(item => rebuildAll || item.Status != PrefabAssetLibraryItemStatus.UpToDate)
                .ToList();

            if (buildItems.Count == 0)
            {
                DisplayFinishedDialog("Prefab Asset Library", "No new or changed prefab needs building.");
                return manifest;
            }

            var previewScene = EditorSceneManager.NewPreviewScene();
            var renderRig = CreateRenderRig(previewScene, settings);
            var failures = new List<string>();

            try
            {
                for (var i = 0; i < buildItems.Count; i++)
                {
                    var item = buildItems[i];
                    if (!Application.isBatchMode)
                    {
                        var canceled = EditorUtility.DisplayCancelableProgressBar(
                            "Prefab Asset Library",
                            $"Rendering {i + 1}/{buildItems.Count}: {Path.GetFileNameWithoutExtension(item.PrefabPath)}",
                            (i + 1) / (float)buildItems.Count);

                        if (canceled)
                        {
                            break;
                        }
                    }

                    try
                    {
                        var metadata = BuildSinglePrefab(item, settings, previewScene, renderRig);
                        outputItems[item.Guid] = metadata;
                    }
                    catch (Exception exception)
                    {
                        failures.Add($"{item.PrefabPath}: {exception.Message}");
                        Debug.LogException(exception);
                    }
                }
            }
            finally
            {
                if (!Application.isBatchMode)
                {
                    EditorUtility.ClearProgressBar();
                }

                EditorSceneManager.ClosePreviewScene(previewScene);
            }

            var finalManifest = new PrefabAssetLibraryManifest
            {
                generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                sourceFolder = settings.SourceFolder,
                outputFolder = settings.OutputFolder,
                captureWidth = settings.CaptureWidth,
                captureHeight = settings.CaptureHeight,
                items = outputItems.Values
                    .OrderBy(item => item.assetType, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.name, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };

            WriteManifest(settings, finalManifest);
            WriteIndex(settings, finalManifest, failures);
            AssetDatabase.Refresh();

            var message = $"Built {buildItems.Count - failures.Count}/{buildItems.Count} prefab asset(s).\nOutput: {settings.OutputFolder}";
            if (failures.Count > 0)
            {
                message += $"\nFailures: {failures.Count}. See console and index file.";
            }

            DisplayFinishedDialog("Prefab Asset Library", message);
            Debug.Log($"Prefab Asset Library: {message}");
            return finalManifest;
        }

        public static PrefabAssetLibraryManifest BuildFastMetadataIndex(PrefabAssetLibrarySettings settings, bool rebuildAll)
        {
            settings = NormalizeSettings(settings);
            EnsureAssetFolder(settings.FastMetadataOutputFolder);

            var fastSettings = CloneForFastMetadata(settings);
            var scanReport = ScanFastMetadata(fastSettings);
            var manifest = LoadManifest(fastSettings.OutputFolder);
            var outputItems = manifest.items
                .Where(item => !string.IsNullOrEmpty(item.sourceGuid))
                .GroupBy(item => item.sourceGuid)
                .ToDictionary(group => group.Key, group => group.First());

            var buildItems = scanReport.Items
                .Where(item => item.Status != PrefabAssetLibraryItemStatus.Skipped)
                .Where(item => rebuildAll || item.Status != PrefabAssetLibraryItemStatus.UpToDate)
                .ToList();

            if (buildItems.Count == 0)
            {
                DisplayFinishedDialog("Prefab Asset Metadata", "No new or changed prefab metadata needs building.");
                return manifest;
            }

            var failures = new List<string>();
            for (var i = 0; i < buildItems.Count; i++)
            {
                var item = buildItems[i];
                if (!Application.isBatchMode)
                {
                    var canceled = EditorUtility.DisplayCancelableProgressBar(
                        "Prefab Asset Metadata",
                        $"Scanning {i + 1}/{buildItems.Count}: {Path.GetFileNameWithoutExtension(item.PrefabPath)}",
                        (i + 1) / (float)buildItems.Count);

                    if (canceled)
                    {
                        break;
                    }
                }

                try
                {
                    var metadata = BuildSinglePrefabMetadataOnly(item, fastSettings);
                    outputItems[item.Guid] = metadata;
                }
                catch (Exception exception)
                {
                    failures.Add($"{item.PrefabPath}: {exception.Message}");
                    Debug.LogException(exception);
                }
            }

            if (!Application.isBatchMode)
            {
                EditorUtility.ClearProgressBar();
            }

            var finalManifest = new PrefabAssetLibraryManifest
            {
                generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                sourceFolder = fastSettings.SourceFolder,
                outputFolder = fastSettings.OutputFolder,
                captureWidth = 0,
                captureHeight = 0,
                items = outputItems.Values
                    .OrderBy(item => item.assetType, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.name, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };

            WriteManifest(fastSettings, finalManifest);
            WriteIndex(fastSettings, finalManifest, failures);
            AssetDatabase.Refresh();

            var message = $"Built fast metadata for {buildItems.Count - failures.Count}/{buildItems.Count} prefab asset(s).\nOutput: {fastSettings.OutputFolder}";
            if (failures.Count > 0)
            {
                message += $"\nFailures: {failures.Count}. See console and index file.";
            }

            DisplayFinishedDialog("Prefab Asset Metadata", message);
            Debug.Log($"Prefab Asset Metadata: {message}");
            return finalManifest;
        }

        private static PrefabAssetLibraryScanReport ScanFastMetadata(PrefabAssetLibrarySettings settings)
        {
            EnsureAssetFolder(settings.OutputFolder);

            var manifest = LoadManifest(settings.OutputFolder);
            var existingByGuid = manifest.items
                .Where(item => !string.IsNullOrEmpty(item.sourceGuid))
                .GroupBy(item => item.sourceGuid)
                .ToDictionary(group => group.Key, group => group.First());

            var prefabPaths = AssetDatabase.FindAssets("t:Prefab", new[] { settings.SourceFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Where(path => path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                .Where(path => !IsInsideFolder(path, settings.OutputFolder))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (settings.MaxItemsPerRun > 0)
            {
                prefabPaths = prefabPaths.Take(settings.MaxItemsPerRun).ToList();
            }

            var report = new PrefabAssetLibraryScanReport { TotalPrefabs = prefabPaths.Count };
            foreach (var prefabPath in prefabPaths)
            {
                var guid = AssetDatabase.AssetPathToGUID(prefabPath);
                var workItem = new PrefabAssetLibraryWorkItem
                {
                    PrefabPath = prefabPath,
                    Guid = guid,
                    Hash = ComputeDependencyHash(prefabPath)
                };

                if (settings.OnlyVisiblePrefabs && !PrefabHasVisibleRenderer(prefabPath))
                {
                    workItem.Status = PrefabAssetLibraryItemStatus.Skipped;
                    workItem.SkipReason = "No visible renderer";
                    report.SkippedCount++;
                    report.Items.Add(workItem);
                    continue;
                }

                existingByGuid.TryGetValue(guid, out var existing);
                workItem.ExistingMetadata = existing;
                if (existing == null || string.IsNullOrEmpty(existing.entryFolder))
                {
                    workItem.Status = PrefabAssetLibraryItemStatus.New;
                    report.NewCount++;
                }
                else if (existing.sourceHash != workItem.Hash || !EntryHasMetadataFile(existing))
                {
                    workItem.Status = PrefabAssetLibraryItemStatus.Changed;
                    report.ChangedCount++;
                }
                else
                {
                    workItem.Status = PrefabAssetLibraryItemStatus.UpToDate;
                    report.UpToDateCount++;
                }

                report.Items.Add(workItem);
            }

            Debug.Log($"Prefab Asset Metadata scan: total={report.TotalPrefabs}, new={report.NewCount}, changed={report.ChangedCount}, upToDate={report.UpToDateCount}, skipped={report.SkippedCount}.");
            return report;
        }

        private static PrefabAssetMetadata BuildSinglePrefabMetadataOnly(PrefabAssetLibraryWorkItem workItem, PrefabAssetLibrarySettings settings)
        {
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(workItem.PrefabPath);
            if (prefabAsset == null)
            {
                throw new InvalidOperationException($"Could not load prefab: {workItem.PrefabPath}");
            }

            var bounds = CalculatePrefabAssetBounds(prefabAsset);
            var analysis = AnalyzePrefabInstance(prefabAsset.name, workItem.PrefabPath, prefabAsset, bounds);
            var entryFolder = ResolveEntryFolder(settings, workItem, prefabAsset.name);
            Directory.CreateDirectory(AssetPathToFullPath(entryFolder));

            var metadata = new PrefabAssetMetadata
            {
                name = prefabAsset.name,
                prefabPath = workItem.PrefabPath,
                sourceGuid = workItem.Guid,
                sourceHash = workItem.Hash,
                entryFolder = entryFolder,
                frontImage = string.Empty,
                sideImage = string.Empty,
                topImage = string.Empty,
                boundsSize = bounds.size,
                boundsCenter = bounds.center,
                rendererCount = analysis.RendererCount,
                meshCount = analysis.MeshCount,
                materialCount = analysis.MaterialNames.Count,
                vertexCount = analysis.VertexCount,
                assetType = analysis.AssetType,
                style = analysis.Style,
                shape = analysis.Shape,
                dominantColors = analysis.DominantColors,
                colorFamilies = analysis.ColorFamilies,
                materialNames = analysis.MaterialNames,
                meshNames = analysis.MeshNames,
                tags = analysis.Tags,
                generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            metadata.description = BuildDescription(metadata);

            WriteMetadataFiles(entryFolder, metadata);
            return metadata;
        }

        private static PrefabAssetMetadata BuildSinglePrefab(
            PrefabAssetLibraryWorkItem workItem,
            PrefabAssetLibrarySettings settings,
            Scene previewScene,
            PrefabRenderRig renderRig)
        {
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(workItem.PrefabPath);
            if (prefabAsset == null)
            {
                throw new InvalidOperationException($"Could not load prefab: {workItem.PrefabPath}");
            }

            var instance = PrefabUtility.InstantiatePrefab(prefabAsset, previewScene) as GameObject;
            if (instance == null)
            {
                instance = Object.Instantiate(prefabAsset);
                SceneManager.MoveGameObjectToScene(instance, previewScene);
            }

            try
            {
                instance.name = prefabAsset.name;
                ResetInstanceTransform(instance);
                var bounds = NormalizeInstanceToOrigin(instance);
                var analysis = AnalyzePrefabInstance(prefabAsset.name, workItem.PrefabPath, instance, bounds);

                var entryFolder = ResolveEntryFolder(settings, workItem, prefabAsset.name);
                Directory.CreateDirectory(AssetPathToFullPath(entryFolder));

                var frontPath = CombineAssetPath(entryFolder, FrontImageName);
                var sampledColors = CapturePrefabView(renderRig.Camera, settings, bounds, PrefabView.Front, frontPath);

                var sidePath = string.Empty;
                if (settings.IncludeSideView)
                {
                    sidePath = CombineAssetPath(entryFolder, SideImageName);
                    CapturePrefabView(renderRig.Camera, settings, bounds, PrefabView.Side, sidePath);
                }

                var topPath = string.Empty;
                if (settings.IncludeTopView)
                {
                    topPath = CombineAssetPath(entryFolder, TopImageName);
                    CapturePrefabView(renderRig.Camera, settings, bounds, PrefabView.Top, topPath);
                }

                if (sampledColors.Count > 0)
                {
                    analysis.DominantColors = ToDominantColorHexes(sampledColors, 6);
                    analysis.ColorFamilies = ToColorFamilies(sampledColors, 5);
                    analysis.Tags = BuildTags(analysis.AssetType, analysis.Style, analysis.Shape, analysis.ColorFamilies, prefabAsset.name, workItem.PrefabPath);
                }

                var metadata = new PrefabAssetMetadata
                {
                    name = prefabAsset.name,
                    prefabPath = workItem.PrefabPath,
                    sourceGuid = workItem.Guid,
                    sourceHash = workItem.Hash,
                    entryFolder = entryFolder,
                    frontImage = frontPath,
                    sideImage = sidePath,
                    topImage = topPath,
                    boundsSize = bounds.size,
                    boundsCenter = bounds.center,
                    rendererCount = analysis.RendererCount,
                    meshCount = analysis.MeshCount,
                    materialCount = analysis.MaterialNames.Count,
                    vertexCount = analysis.VertexCount,
                    assetType = analysis.AssetType,
                    style = analysis.Style,
                    shape = analysis.Shape,
                    dominantColors = analysis.DominantColors,
                    colorFamilies = analysis.ColorFamilies,
                    materialNames = analysis.MaterialNames,
                    meshNames = analysis.MeshNames,
                    tags = analysis.Tags,
                    generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };
                metadata.description = BuildDescription(metadata);

                WriteMetadataFiles(entryFolder, metadata);
                return metadata;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static PrefabAnalysis AnalyzePrefabInstance(string prefabName, string prefabPath, GameObject instance, Bounds bounds)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(false)
                .Where(renderer => renderer != null && renderer is not ParticleSystemRenderer)
                .ToList();
            var meshFilters = instance.GetComponentsInChildren<MeshFilter>(false)
                .Where(filter => filter != null && filter.sharedMesh != null)
                .ToList();
            var skinnedMeshes = instance.GetComponentsInChildren<SkinnedMeshRenderer>(false)
                .Where(renderer => renderer != null && renderer.sharedMesh != null)
                .ToList();

            var materials = renderers
                .SelectMany(renderer => renderer.sharedMaterials ?? Array.Empty<Material>())
                .Where(material => material != null)
                .ToList();

            var materialNames = materials
                .Select(material => material.name.Replace(" (Instance)", string.Empty))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var meshNames = meshFilters.Select(filter => filter.sharedMesh.name)
                .Concat(skinnedMeshes.Select(renderer => renderer.sharedMesh.name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var colors = ExtractMaterialAndTextureColors(materials);
            var colorHexes = colors.Select(ColorToHex).Distinct(StringComparer.OrdinalIgnoreCase).Take(6).ToList();
            var colorFamilies = colors
                .Select(GetColorFamily)
                .Where(family => !string.IsNullOrEmpty(family))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToList();

            var vertexCount = meshFilters.Sum(filter => filter.sharedMesh.vertexCount) +
                              skinnedMeshes.Sum(renderer => renderer.sharedMesh.vertexCount);
            var assetType = InferAssetType(prefabName, prefabPath, materialNames, meshNames);
            var shape = InferShape(bounds);
            var style = InferStyle(prefabName, prefabPath, vertexCount, renderers.Count, colors);
            ApplySemanticColorHints(assetType, prefabName, prefabPath, materialNames, meshNames, colorFamilies);
            var tags = BuildTags(assetType, style, shape, colorFamilies, prefabName, prefabPath);

            return new PrefabAnalysis
            {
                RendererCount = renderers.Count,
                MeshCount = meshFilters.Count + skinnedMeshes.Count,
                VertexCount = vertexCount,
                MaterialNames = materialNames,
                MeshNames = meshNames,
                DominantColors = colorHexes,
                ColorFamilies = colorFamilies,
                AssetType = assetType,
                Shape = shape,
                Style = style,
                Tags = tags
            };
        }

        private static List<Color> CapturePrefabView(Camera camera, PrefabAssetLibrarySettings settings, Bounds bounds, PrefabView view, string assetPath)
        {
            ConfigureCameraForView(camera, bounds, view, settings);

            var fullPath = AssetPathToFullPath(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? Application.dataPath);

            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var renderTexture = new RenderTexture(settings.CaptureWidth, settings.CaptureHeight, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 8,
                name = "PrefabAssetLibrary_RT"
            };

            try
            {
                camera.targetTexture = renderTexture;
                RenderTexture.active = renderTexture;
                camera.Render();

                var texture = new Texture2D(settings.CaptureWidth, settings.CaptureHeight, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, settings.CaptureWidth, settings.CaptureHeight), 0, 0);
                texture.Apply();
                var sampledColors = ExtractRenderedObjectColors(texture, camera.backgroundColor);
                File.WriteAllBytes(fullPath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                return sampledColors;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
            }
        }

        private static void ConfigureCameraForView(Camera camera, Bounds bounds, PrefabView view, PrefabAssetLibrarySettings settings)
        {
            var center = bounds.center;
            var maxSize = Mathf.Max(0.1f, Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)));
            var distance = maxSize * 2.4f + 3f;
            var aspect = settings.CaptureWidth / (float)Mathf.Max(1, settings.CaptureHeight);
            var padding = Mathf.Max(0.25f, maxSize * 0.2f);

            Vector3 position;
            Vector3 up;
            float width;
            float height;

            switch (view)
            {
                case PrefabView.Side:
                    position = center + Vector3.right * distance;
                    up = Vector3.up;
                    width = bounds.size.z;
                    height = bounds.size.y;
                    break;
                case PrefabView.Top:
                    position = center + Vector3.up * distance;
                    up = Vector3.forward;
                    width = bounds.size.x;
                    height = bounds.size.z;
                    break;
                default:
                    position = center + Vector3.back * distance;
                    up = Vector3.up;
                    width = bounds.size.x;
                    height = bounds.size.y;
                    break;
            }

            var direction = center - position;
            camera.transform.position = position;
            camera.transform.rotation = Quaternion.LookRotation(direction.normalized, up);
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max((height + padding * 2f) * 0.5f, (width + padding * 2f) / aspect * 0.5f);
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = distance * 4f + maxSize * 4f + 20f;
        }

        private static PrefabRenderRig CreateRenderRig(Scene previewScene, PrefabAssetLibrarySettings settings)
        {
            var cameraObject = new GameObject("PrefabAssetLibrary_Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, previewScene);
            var camera = cameraObject.AddComponent<Camera>();
            camera.scene = previewScene;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.73f, 0.78f, 0.76f, 1f);
            camera.allowHDR = true;
            camera.allowMSAA = true;

            var keyLightObject = new GameObject("PrefabAssetLibrary_KeyLight");
            SceneManager.MoveGameObjectToScene(keyLightObject, previewScene);
            keyLightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            var keyLight = keyLightObject.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.intensity = 1.8f;
            keyLight.color = new Color(1f, 0.94f, 0.84f, 1f);

            var fillLightObject = new GameObject("PrefabAssetLibrary_FillLight");
            SceneManager.MoveGameObjectToScene(fillLightObject, previewScene);
            fillLightObject.transform.rotation = Quaternion.Euler(30f, 145f, 0f);
            var fillLight = fillLightObject.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.intensity = 0.45f;
            fillLight.color = new Color(0.68f, 0.82f, 1f, 1f);

            return new PrefabRenderRig { Camera = camera };
        }

        private static void ResetInstanceTransform(GameObject instance)
        {
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
        }

        private static Bounds NormalizeInstanceToOrigin(GameObject instance)
        {
            if (!AssetCatalogLabel.TryCalculateWorldBounds(instance.transform, out var bounds))
            {
                bounds = new Bounds(Vector3.zero, Vector3.one);
            }

            var delta = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            instance.transform.position += delta;
            AssetCatalogLabel.TryCalculateWorldBounds(instance.transform, out bounds);
            return bounds;
        }

        private static Bounds CalculatePrefabAssetBounds(GameObject prefabAsset)
        {
            var hasBounds = false;
            var bounds = new Bounds(Vector3.zero, Vector3.one);
            var root = prefabAsset.transform;
            var rootToLocal = root.worldToLocalMatrix;

            foreach (var filter in prefabAsset.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }

                var worldToRoot = rootToLocal * filter.transform.localToWorldMatrix;
                var meshBounds = TransformBounds(filter.sharedMesh.bounds, worldToRoot);
                if (!hasBounds)
                {
                    bounds = meshBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(meshBounds);
                }
            }

            foreach (var renderer in prefabAsset.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer == null || renderer.sharedMesh == null)
                {
                    continue;
                }

                var worldToRoot = rootToLocal * renderer.transform.localToWorldMatrix;
                var meshBounds = TransformBounds(renderer.sharedMesh.bounds, worldToRoot);
                if (!hasBounds)
                {
                    bounds = meshBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(meshBounds);
                }
            }

            return hasBounds ? bounds : new Bounds(Vector3.zero, Vector3.one);
        }

        private static Bounds TransformBounds(Bounds localBounds, Matrix4x4 matrix)
        {
            var center = matrix.MultiplyPoint3x4(localBounds.center);
            var extents = localBounds.extents;
            var axisX = matrix.MultiplyVector(new Vector3(extents.x, 0f, 0f));
            var axisY = matrix.MultiplyVector(new Vector3(0f, extents.y, 0f));
            var axisZ = matrix.MultiplyVector(new Vector3(0f, 0f, extents.z));
            extents.x = Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x);
            extents.y = Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y);
            extents.z = Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z);
            return new Bounds(center, extents * 2f);
        }

        private static string BuildDescription(PrefabAssetMetadata metadata)
        {
            var colors = metadata.colorFamilies != null && metadata.colorFamilies.Count > 0
                ? string.Join("/", metadata.colorFamilies)
                : "unknown color";
            return $"{metadata.style} {metadata.assetType}, {metadata.shape}, dominant colors: {colors}, size: {metadata.boundsSize.x:0.###} x {metadata.boundsSize.y:0.###} x {metadata.boundsSize.z:0.###}.";
        }

        private static string InferAssetType(
            string prefabName,
            string prefabPath,
            IReadOnlyList<string> materialNames,
            IReadOnlyList<string> meshNames)
        {
            var text = BuildSearchText(prefabName, prefabPath, materialNames, meshNames);

            if (ContainsAny(text, "tree", "trunk", "pine", "oak")) return "tree";
            if (ContainsAny(text, "rock", "stone", "boulder", "cliff")) return "rock";
            if (ContainsAny(text, "grass", "moss", "lawn")) return "grass";
            if (ContainsAny(text, "flower", "blossom", "plant")) return "flower";
            if (ContainsAny(text, "shrub", "bush", "leaf", "leaves")) return "shrub";
            if (ContainsAny(text, "altar", "pedestal", "shrine")) return "altar";
            if (ContainsAny(text, "arch", "gate", "portal")) return "arch";
            if (ContainsAny(text, "fence", "rail")) return "fence";
            if (ContainsAny(text, "box", "crate", "barrel")) return "prop";
            if (ContainsAny(text, "lamp", "light", "lantern")) return "lighting prop";
            if (ContainsAny(text, "water", "pond", "river")) return "water";
            if (ContainsAny(text, "mountain", "hill")) return "terrain";

            return "prop";
        }

        private static string InferShape(Bounds bounds)
        {
            var size = bounds.size;
            var maxHorizontal = Mathf.Max(size.x, size.z);
            var minHorizontal = Mathf.Max(0.001f, Mathf.Min(size.x, size.z));
            var height = Mathf.Max(0.001f, size.y);

            if (height > maxHorizontal * 2.2f) return "tall vertical silhouette";
            if (height < maxHorizontal * 0.25f) return "flat low silhouette";
            if (maxHorizontal / minHorizontal > 3f) return "long narrow footprint";
            if (Mathf.Abs(size.x - size.z) < maxHorizontal * 0.25f && Mathf.Abs(height - maxHorizontal) < maxHorizontal * 0.35f) return "rounded compact silhouette";
            if (maxHorizontal > height * 1.5f) return "wide footprint";
            return "balanced medium silhouette";
        }

        private static string InferStyle(string prefabName, string prefabPath, int vertexCount, int rendererCount, IReadOnlyList<Color> colors)
        {
            var text = BuildSearchText(prefabName, prefabPath, Array.Empty<string>(), Array.Empty<string>());
            if (ContainsAny(text, "ghibli", "stylized", "stylised", "lowpoly", "low_poly", "low-poly"))
            {
                return "stylized low-poly";
            }

            var averageVertices = rendererCount > 0 ? vertexCount / Mathf.Max(1, rendererCount) : vertexCount;
            var hasSaturatedColor = colors.Any(color =>
            {
                Color.RGBToHSV(color, out _, out var saturation, out _);
                return saturation > 0.35f;
            });
            if (averageVertices < 900 && hasSaturatedColor)
            {
                return "stylized low-poly";
            }

            if (averageVertices < 1500)
            {
                return "simple low-poly";
            }

            return "generic 3D";
        }

        private static List<string> BuildTags(
            string assetType,
            string style,
            string shape,
            IReadOnlyList<string> colorFamilies,
            string prefabName,
            string prefabPath)
        {
            var tags = new List<string> { assetType, style, shape };
            tags.AddRange(colorFamilies);

            var text = BuildSearchText(prefabName, prefabPath, Array.Empty<string>(), Array.Empty<string>());
            if (ContainsAny(text, "forest", "nature", "natural")) tags.Add("nature");
            if (ContainsAny(text, "magic", "glow", "fantasy")) tags.Add("fantasy");
            if (ContainsAny(text, "modular", "tile")) tags.Add("modular");

            return tags
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void ApplySemanticColorHints(
            string assetType,
            string prefabName,
            string prefabPath,
            IReadOnlyList<string> materialNames,
            IReadOnlyList<string> meshNames,
            List<string> colorFamilies)
        {
            var text = BuildSearchText(prefabName, prefabPath, materialNames, meshNames);
            if ((assetType == "tree" || assetType == "shrub" || assetType == "grass" || ContainsAny(text, "leaf", "leaves", "foliage")) &&
                !colorFamilies.Contains("green", StringComparer.OrdinalIgnoreCase))
            {
                colorFamilies.Insert(0, "green");
            }

            if ((assetType == "rock" || ContainsAny(text, "stone", "rock", "boulder")) &&
                !colorFamilies.Contains("gray", StringComparer.OrdinalIgnoreCase))
            {
                colorFamilies.Add("gray");
            }

            if ((assetType == "fence" || assetType == "prop" || ContainsAny(text, "wood", "bark", "trunk", "plank")) &&
                !colorFamilies.Contains("brown", StringComparer.OrdinalIgnoreCase))
            {
                colorFamilies.Add("brown");
            }
        }

        private static List<Color> ExtractMaterialAndTextureColors(IReadOnlyList<Material> materials)
        {
            var colors = new List<Color>();
            colors.AddRange(ExtractTextureColors(materials));
            colors.AddRange(ExtractMaterialColors(materials, includeFallback: false));

            if (colors.Count == 0)
            {
                colors.Add(Color.gray);
            }

            return colors
                .GroupBy(ColorToHex)
                .OrderByDescending(group => group.Count())
                .Select(group => group.First())
                .ToList();
        }

        private static List<Color> ExtractMaterialColors(IReadOnlyList<Material> materials, bool includeFallback = true)
        {
            var colors = new List<Color>();
            foreach (var material in materials)
            {
                if (material == null)
                {
                    continue;
                }

                if (material.HasProperty("_BaseColor"))
                {
                    colors.Add(material.GetColor("_BaseColor"));
                }
                else if (material.HasProperty("_Color"))
                {
                    colors.Add(material.GetColor("_Color"));
                }
                else if (material.HasProperty("_TintColor"))
                {
                    colors.Add(material.GetColor("_TintColor"));
                }
            }

            if (includeFallback && colors.Count == 0)
            {
                colors.Add(Color.gray);
            }

            return colors
                .GroupBy(ColorToHex)
                .OrderByDescending(group => group.Count())
                .Select(group => group.First())
                .ToList();
        }

        private static List<Color> ExtractTextureColors(IReadOnlyList<Material> materials)
        {
            var colors = new List<Color>();
            foreach (var material in materials)
            {
                foreach (var texture in EnumerateMaterialTextures(material))
                {
                    colors.AddRange(SampleTextureColors(texture));
                }
            }

            return colors;
        }

        private static IEnumerable<Texture2D> EnumerateMaterialTextures(Material material)
        {
            if (material == null)
            {
                yield break;
            }

            var yielded = new HashSet<int>();
            var shader = material.shader;
            if (shader != null)
            {
                var propertyCount = ShaderUtil.GetPropertyCount(shader);
                for (var i = 0; i < propertyCount; i++)
                {
                    if (ShaderUtil.GetPropertyType(shader, i) != ShaderUtil.ShaderPropertyType.TexEnv)
                    {
                        continue;
                    }

                    var propertyName = ShaderUtil.GetPropertyName(shader, i);
                    if (material.GetTexture(propertyName) is Texture2D texture && yielded.Add(texture.GetInstanceID()))
                    {
                        yield return texture;
                    }
                }
            }

            foreach (var propertyName in new[] { "_BaseMap", "_MainTex", "_BaseColorMap", "_BumpMap", "_EmissionMap", "_MetallicGlossMap" })
            {
                if (!material.HasProperty(propertyName))
                {
                    continue;
                }

                if (material.GetTexture(propertyName) is Texture2D texture && yielded.Add(texture.GetInstanceID()))
                {
                    yield return texture;
                }
            }
        }

        private static List<Color> SampleTextureColors(Texture2D sourceTexture)
        {
            var readableTexture = GetReadableTexture(sourceTexture);
            if (readableTexture == null)
            {
                return new List<Color>();
            }

            var colors = new List<Color>();
            var step = Mathf.Max(1, Mathf.Min(readableTexture.width, readableTexture.height) / 64);
            for (var y = 0; y < readableTexture.height; y += step)
            {
                for (var x = 0; x < readableTexture.width; x += step)
                {
                    var color = readableTexture.GetPixel(x, y);
                    if (color.a < 0.1f)
                    {
                        continue;
                    }

                    Color.RGBToHSV(color, out _, out var saturation, out var value);
                    if (value < 0.04f || saturation < 0.03f)
                    {
                        continue;
                    }

                    colors.Add(color);
                }
            }

            if (readableTexture != sourceTexture)
            {
                Object.DestroyImmediate(readableTexture);
            }

            return colors
                .GroupBy(QuantizeColorKey)
                .OrderByDescending(group => group.Count())
                .Select(group => AverageColor(group))
                .Take(12)
                .ToList();
        }

        private static Texture2D GetReadableTexture(Texture2D texture)
        {
            if (texture == null)
            {
                return null;
            }

            try
            {
                texture.GetPixel(0, 0);
                return texture;
            }
            catch (UnityException)
            {
                var path = AssetDatabase.GetAssetPath(texture);
                if (string.IsNullOrEmpty(path))
                {
                    return null;
                }

                var fullPath = AssetPathToFullPath(path);
                if (!File.Exists(fullPath))
                {
                    return null;
                }

                var extension = Path.GetExtension(fullPath).ToLowerInvariant();
                if (extension != ".png" && extension != ".jpg" && extension != ".jpeg")
                {
                    return null;
                }

                var readable = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                return readable.LoadImage(File.ReadAllBytes(fullPath)) ? readable : null;
            }
        }

        private static Color AverageColor(IEnumerable<Color> colors)
        {
            var total = Color.black;
            var count = 0;
            foreach (var color in colors)
            {
                total += color;
                count++;
            }

            return total / Mathf.Max(1, count);
        }

        private static List<Color> ExtractRenderedObjectColors(Texture2D texture, Color backgroundColor)
        {
            var buckets = new Dictionary<int, (Color Color, int Count)>();
            var step = Mathf.Max(1, Mathf.Min(texture.width, texture.height) / 160);

            for (var y = 0; y < texture.height; y += step)
            {
                for (var x = 0; x < texture.width; x += step)
                {
                    var color = texture.GetPixel(x, y);
                    if (IsBackgroundLike(color, backgroundColor))
                    {
                        continue;
                    }

                    Color.RGBToHSV(color, out _, out var saturation, out var value);
                    if (value < 0.04f || saturation < 0.04f)
                    {
                        continue;
                    }

                    var key = QuantizeColorKey(color);
                    if (buckets.TryGetValue(key, out var bucket))
                    {
                        buckets[key] = (bucket.Color + color, bucket.Count + 1);
                    }
                    else
                    {
                        buckets.Add(key, (color, 1));
                    }
                }
            }

            return buckets.Values
                .OrderByDescending(bucket => bucket.Count)
                .Select(bucket => bucket.Color / Mathf.Max(1, bucket.Count))
                .Take(12)
                .ToList();
        }

        private static List<string> ToDominantColorHexes(IReadOnlyList<Color> colors, int maxCount)
        {
            return colors
                .Select(ColorToHex)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(maxCount)
                .ToList();
        }

        private static List<string> ToColorFamilies(IReadOnlyList<Color> colors, int maxCount)
        {
            var families = colors
                .Select(GetColorFamily)
                .Where(family => !string.IsNullOrEmpty(family))
                .GroupBy(family => family, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .Select(group => group.Key)
                .ToList();

            var nonNeutralFamilies = families
                .Where(family => family != "black" && family != "gray" && family != "white")
                .ToList();

            return (nonNeutralFamilies.Count > 0 ? nonNeutralFamilies : families)
                .Take(maxCount)
                .ToList();
        }

        private static string GetColorFamily(Color color)
        {
            Color.RGBToHSV(color, out var hue, out var saturation, out var value);
            if (value < 0.08f) return "black";
            if (value > 0.86f && saturation < 0.16f) return "white";
            if (saturation < 0.18f) return value < 0.22f ? "black" : "gray";

            var degrees = hue * 360f;
            if (degrees < 18f || degrees >= 345f) return "red";
            if (degrees < 55f && value < 0.58f && saturation > 0.22f) return "brown";
            if (degrees < 42f) return "orange";
            if (degrees < 68f) return "yellow";
            if (degrees < 165f) return "green";
            if (degrees < 195f) return "cyan";
            if (degrees < 255f) return "blue";
            if (degrees < 290f) return "purple";
            if (degrees < 345f) return "pink";
            return "unknown";
        }

        private static bool IsBackgroundLike(Color color, Color backgroundColor)
        {
            var difference = Mathf.Abs(color.r - backgroundColor.r) +
                             Mathf.Abs(color.g - backgroundColor.g) +
                             Mathf.Abs(color.b - backgroundColor.b);
            return difference < 0.08f;
        }

        private static int QuantizeColorKey(Color color)
        {
            var r = Mathf.Clamp(Mathf.RoundToInt(color.r * 7f), 0, 7);
            var g = Mathf.Clamp(Mathf.RoundToInt(color.g * 7f), 0, 7);
            var b = Mathf.Clamp(Mathf.RoundToInt(color.b * 7f), 0, 7);
            return (r << 6) | (g << 3) | b;
        }

        private static string ResolveEntryFolder(PrefabAssetLibrarySettings settings, PrefabAssetLibraryWorkItem workItem, string prefabName)
        {
            if (workItem.ExistingMetadata != null &&
                !string.IsNullOrEmpty(workItem.ExistingMetadata.entryFolder) &&
                AssetDatabase.IsValidFolder(workItem.ExistingMetadata.entryFolder))
            {
                return workItem.ExistingMetadata.entryFolder;
            }

            var folderName = $"{workItem.Guid.Substring(0, Math.Min(8, workItem.Guid.Length))}_{SanitizeFileName(prefabName)}";
            var folder = CombineAssetPath(settings.OutputFolder, folderName);
            EnsureAssetFolder(folder);
            return folder;
        }

        private static bool EntryHasRequiredFiles(PrefabAssetMetadata metadata, PrefabAssetLibrarySettings settings)
        {
            if (metadata == null || string.IsNullOrEmpty(metadata.entryFolder))
            {
                return false;
            }

            if (!File.Exists(AssetPathToFullPath(metadata.frontImage)) ||
                !File.Exists(AssetPathToFullPath(CombineAssetPath(metadata.entryFolder, MetadataFileName))))
            {
                return false;
            }

            if (settings.IncludeSideView && !File.Exists(AssetPathToFullPath(metadata.sideImage)))
            {
                return false;
            }

            if (settings.IncludeTopView && !File.Exists(AssetPathToFullPath(metadata.topImage)))
            {
                return false;
            }

            return true;
        }

        private static bool EntryHasMetadataFile(PrefabAssetMetadata metadata)
        {
            return metadata != null &&
                   !string.IsNullOrEmpty(metadata.entryFolder) &&
                   File.Exists(AssetPathToFullPath(CombineAssetPath(metadata.entryFolder, MetadataFileName)));
        }

        private static bool PrefabHasVisibleRenderer(string prefabPath)
        {
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset == null)
            {
                return false;
            }

            return prefabAsset.GetComponentsInChildren<Renderer>(true)
                .Any(renderer => renderer != null && renderer is not ParticleSystemRenderer);
        }

        private static void WriteMetadataFiles(string entryFolder, PrefabAssetMetadata metadata)
        {
            var metadataPath = AssetPathToFullPath(CombineAssetPath(entryFolder, MetadataFileName));
            File.WriteAllText(metadataPath, JsonUtility.ToJson(metadata, true), Encoding.UTF8);

            var lines = new List<string>
            {
                "# " + metadata.name,
                string.Empty,
                "- Prefab: `" + metadata.prefabPath + "`",
                "- Type: `" + metadata.assetType + "`",
                "- Style: `" + metadata.style + "`",
                "- Shape: `" + metadata.shape + "`",
                "- Size: `" + $"{metadata.boundsSize.x:0.###} x {metadata.boundsSize.y:0.###} x {metadata.boundsSize.z:0.###}" + "`",
                "- Colors: `" + string.Join(", ", metadata.colorFamilies) + "`",
                "- Description: " + metadata.description,
                string.Empty
            };

            if (!string.IsNullOrEmpty(metadata.sideImage))
            {
                if (!lines.Contains("## Images"))
                {
                    lines.Add("## Images");
                }

                if (!string.IsNullOrEmpty(metadata.frontImage))
                {
                    lines.Add("- Front: `" + metadata.frontImage + "`");
                }

                lines.Add("- Side: `" + metadata.sideImage + "`");
            }

            if (!string.IsNullOrEmpty(metadata.topImage))
            {
                if (!lines.Contains("## Images"))
                {
                    lines.Add("## Images");
                }

                if (!string.IsNullOrEmpty(metadata.frontImage) && !lines.Any(line => line.StartsWith("- Front:", StringComparison.Ordinal)))
                {
                    lines.Add("- Front: `" + metadata.frontImage + "`");
                }

                lines.Add("- Top: `" + metadata.topImage + "`");
            }

            if (!string.IsNullOrEmpty(metadata.frontImage) && !lines.Any(line => line.StartsWith("- Front:", StringComparison.Ordinal)))
            {
                lines.Add("## Images");
                lines.Add("- Front: `" + metadata.frontImage + "`");
            }

            File.WriteAllLines(AssetPathToFullPath(CombineAssetPath(entryFolder, SummaryFileName)), lines, Encoding.UTF8);
        }

        private static void WriteManifest(PrefabAssetLibrarySettings settings, PrefabAssetLibraryManifest manifest)
        {
            var manifestPath = AssetPathToFullPath(CombineAssetPath(settings.OutputFolder, ManifestFileName));
            File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true), Encoding.UTF8);
        }

        private static PrefabAssetLibraryManifest LoadManifest(string outputFolder)
        {
            var manifestPath = AssetPathToFullPath(CombineAssetPath(outputFolder, ManifestFileName));
            if (!File.Exists(manifestPath))
            {
                return new PrefabAssetLibraryManifest();
            }

            try
            {
                return JsonUtility.FromJson<PrefabAssetLibraryManifest>(File.ReadAllText(manifestPath, Encoding.UTF8)) ??
                       new PrefabAssetLibraryManifest();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Prefab Asset Library: could not read manifest. A new manifest will be created. {exception.Message}");
                return new PrefabAssetLibraryManifest();
            }
        }

        private static void WriteIndex(PrefabAssetLibrarySettings settings, PrefabAssetLibraryManifest manifest, IReadOnlyList<string> failures)
        {
            var lines = new List<string>
            {
                "# Prefab Asset Library",
                string.Empty,
                $"Generated: {manifest.generatedAt}",
                $"Source: `{settings.SourceFolder}`",
                $"Items: {manifest.items.Count}",
                string.Empty,
                "## Usage",
                "- Use metadata for fast filtering by type/style/color/shape.",
                "- Use screenshots when Codex needs visual confirmation against a new reference image.",
                string.Empty
            };

            foreach (var group in manifest.items.GroupBy(item => item.assetType).OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
            {
                lines.Add("## " + group.Key);
                foreach (var item in group.OrderBy(item => item.name, StringComparer.OrdinalIgnoreCase))
                {
                    var colors = item.colorFamilies != null ? string.Join("/", item.colorFamilies) : string.Empty;
                    var front = string.IsNullOrEmpty(item.frontImage) ? string.Empty : $" | front=`{item.frontImage}`";
                    lines.Add($"- `{item.name}` | style=`{item.style}` | colors=`{colors}` | size=`{item.boundsSize.x:0.##},{item.boundsSize.y:0.##},{item.boundsSize.z:0.##}` | prefab=`{item.prefabPath}`{front}");
                }

                lines.Add(string.Empty);
            }

            if (failures.Count > 0)
            {
                lines.Add("## Failures");
                foreach (var failure in failures)
                {
                    lines.Add("- " + failure);
                }
            }

            File.WriteAllLines(AssetPathToFullPath(CombineAssetPath(settings.OutputFolder, IndexFileName)), lines, Encoding.UTF8);
        }

        private static string ComputeDependencyHash(string prefabPath)
        {
            using var sha = SHA256.Create();
            var dependencies = AssetDatabase.GetDependencies(prefabPath, recursive: true)
                .Where(path => path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

            foreach (var dependency in dependencies)
            {
                var fullPath = AssetPathToFullPath(dependency);
                var info = File.Exists(fullPath) ? new FileInfo(fullPath) : null;
                var line = info != null
                    ? $"{dependency}|{AssetDatabase.AssetPathToGUID(dependency)}|{info.Length}|{info.LastWriteTimeUtc.Ticks}\n"
                    : $"{dependency}|missing\n";
                var bytes = Encoding.UTF8.GetBytes(line);
                sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
            }

            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return BitConverter.ToString(sha.Hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static PrefabAssetLibrarySettings NormalizeSettings(PrefabAssetLibrarySettings settings)
        {
            settings ??= new PrefabAssetLibrarySettings();
            settings.SourceFolder = NormalizeAssetPath(string.IsNullOrWhiteSpace(settings.SourceFolder) ? "Assets" : settings.SourceFolder);
            settings.OutputFolder = NormalizeAssetPath(string.IsNullOrWhiteSpace(settings.OutputFolder) ? DefaultOutputFolder : settings.OutputFolder);
            settings.FastMetadataOutputFolder = NormalizeAssetPath(string.IsNullOrWhiteSpace(settings.FastMetadataOutputFolder) ? DefaultFastMetadataOutputFolder : settings.FastMetadataOutputFolder);
            settings.CaptureWidth = Mathf.Max(256, settings.CaptureWidth);
            settings.CaptureHeight = Mathf.Max(256, settings.CaptureHeight);
            settings.MaxItemsPerRun = Mathf.Max(0, settings.MaxItemsPerRun);
            return settings;
        }

        private static PrefabAssetLibrarySettings CloneForFastMetadata(PrefabAssetLibrarySettings settings)
        {
            return new PrefabAssetLibrarySettings
            {
                SourceFolder = settings.SourceFolder,
                OutputFolder = settings.FastMetadataOutputFolder,
                FastMetadataOutputFolder = settings.FastMetadataOutputFolder,
                CaptureWidth = 0,
                CaptureHeight = 0,
                OnlyVisiblePrefabs = settings.OnlyVisiblePrefabs,
                IncludeSideView = false,
                IncludeTopView = false,
                MaxItemsPerRun = settings.MaxItemsPerRun
            };
        }

        private static bool ContainsAny(string text, params string[] tokens)
        {
            return tokens.Any(token => text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string BuildSearchText(
            string prefabName,
            string prefabPath,
            IReadOnlyList<string> materialNames,
            IReadOnlyList<string> meshNames)
        {
            return string.Join(" ", new[]
            {
                prefabName,
                prefabPath,
                string.Join(" ", materialNames ?? Array.Empty<string>()),
                string.Join(" ", meshNames ?? Array.Empty<string>())
            }).ToLowerInvariant();
        }

        private static string ColorToHex(Color color)
        {
            return "#" + ColorUtility.ToHtmlStringRGB(color);
        }

        private static bool IsInsideFolder(string assetPath, string folder)
        {
            assetPath = NormalizeAssetPath(assetPath);
            folder = NormalizeAssetPath(folder).TrimEnd('/') + "/";
            return assetPath.StartsWith(folder, StringComparison.OrdinalIgnoreCase);
        }

        private static string CombineAssetPath(string folder, string fileOrChild)
        {
            return NormalizeAssetPath(folder).TrimEnd('/') + "/" + fileOrChild.TrimStart('/', '\\');
        }

        private static string NormalizeAssetPath(string path)
        {
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

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Prefab";
            }

            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return value.Replace(' ', '_').Trim();
        }

        private static void DisplayFinishedDialog(string title, string message)
        {
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(title, message, "OK");
            }
        }

        private enum PrefabView
        {
            Front,
            Side,
            Top
        }

        private sealed class PrefabRenderRig
        {
            public Camera Camera;
        }

        private sealed class PrefabAnalysis
        {
            public int RendererCount;
            public int MeshCount;
            public int VertexCount;
            public string AssetType;
            public string Style;
            public string Shape;
            public List<string> DominantColors = new List<string>();
            public List<string> ColorFamilies = new List<string>();
            public List<string> MaterialNames = new List<string>();
            public List<string> MeshNames = new List<string>();
            public List<string> Tags = new List<string>();
        }
    }
}
