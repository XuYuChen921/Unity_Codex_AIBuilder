using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AIBuilder;
using UnityEditor;
using UnityEngine;

namespace AIBuilder.EditorTools
{
    public sealed class AIBuilderValidationResult
    {
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();

        public bool IsValid => Errors.Count == 0;

        public string ToDisplayString()
        {
            var lines = new List<string>();
            if (Errors.Count > 0)
            {
                lines.Add("错误：");
                lines.AddRange(Errors.Select(error => "- " + error));
            }

            if (Warnings.Count > 0)
            {
                if (lines.Count > 0)
                {
                    lines.Add(string.Empty);
                }

                lines.Add("警告：");
                lines.AddRange(Warnings.Select(warning => "- " + warning));
            }

            return string.Join("\n", lines);
        }
    }

    public static class AIBuilderSpecValidator
    {
        private const int MaxGeneratedObjects = 20000;
        private static readonly HashSet<string> SupportedProceduralTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "pine_tree",
            "broadleaf_tree",
            "rock",
            "bush",
            "flower",
            "mountain"
        };

        private static readonly HashSet<string> SupportedModelShapes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "empty",
            "group",
            "box",
            "cube",
            "cylinder",
            "cone",
            "sphere",
            "icosphere",
            "plane",
            "quad",
            "prism",
            "wedge",
            "roof",
            "pipe",
            "ring",
            "torus",
            "arch",
            "stair",
            "stairs",
            "screen_polygon"
        };

        public static AIBuilderValidationResult ValidateSceneSpec(SceneSpec spec)
        {
            var result = new AIBuilderValidationResult();
            if (spec == null)
            {
                result.Errors.Add("SceneSpec 为空。");
                return result;
            }

            if (spec.schemaVersion > 2)
            {
                result.Errors.Add($"不支持 SceneSpec schemaVersion={spec.schemaVersion}，当前最高支持 2。");
            }
            else if (spec.schemaVersion <= 0)
            {
                result.Warnings.Add("未声明 schemaVersion，将按旧版 SceneSpec 兼容处理。");
            }

            RequireName(spec.sceneName, "sceneName", result);
            RequireName(spec.generatedRootName, "generatedRootName", result);

            if (spec.camera == null)
            {
                result.Errors.Add("camera 不能为空。");
            }
            else
            {
                if (spec.camera.fieldOfView <= 1f || spec.camera.fieldOfView >= 179f)
                {
                    result.Errors.Add("camera.fieldOfView 必须在 1 到 179 之间。");
                }

                if (spec.camera.nearClipPlane <= 0f || spec.camera.farClipPlane <= spec.camera.nearClipPlane)
                {
                    result.Errors.Add("相机裁剪面无效：nearClipPlane 必须大于 0，farClipPlane 必须大于 nearClipPlane。");
                }
            }

            if (spec.lighting == null)
            {
                result.Errors.Add("lighting 不能为空。");
            }

            if (spec.features == null)
            {
                result.Errors.Add("features 不能为空。");
            }

            ValidateTerrain(spec.terrain, result);
            ValidateCentralFeature(spec, result);
            ValidateHeroTrees(spec, result);
            ValidateSceneObjects(spec.objects, result);
            ValidateScatterGroups(spec.scatterGroups, result);

            var explicitCount = spec.objects?.Count ?? 0;
            var scatterCount = spec.scatterGroups?.Sum(group => group != null ? Mathf.Max(0, group.count) : 0) ?? 0;
            var patchCount = spec.terrain != null && spec.terrain.enabled && spec.terrain.createColorPatches
                ? Mathf.Max(0, spec.terrain.grassPatchCount)
                : 0;
            if ((long)explicitCount + scatterCount + patchCount > MaxGeneratedObjects)
            {
                result.Errors.Add($"计划生成的对象数量超过安全上限 {MaxGeneratedObjects}。");
            }

            return result;
        }

        public static AIBuilderValidationResult ValidateModelSpec(ProBuilderModelSpec spec, string outputFolder)
        {
            var result = new AIBuilderValidationResult();
            if (spec == null)
            {
                result.Errors.Add("ProBuilderModelSpec 为空。");
                return result;
            }

            RequireName(spec.modelName, "modelName", result);
            RequireName(spec.generatedRootName, "generatedRootName", result);

            if (spec.schemaVersion > 2)
            {
                result.Errors.Add($"不支持 ProBuilderModelSpec schemaVersion={spec.schemaVersion}，当前最高支持 2。");
            }
            else if (spec.schemaVersion <= 0)
            {
                result.Warnings.Add("未声明 schemaVersion，将按旧版 ProBuilderModelSpec 兼容处理。");
            }

            if (!AIBuilderAssetPathUtility.TryNormalizeAssetsFolder(outputFolder, out _, out var pathError))
            {
                result.Errors.Add(pathError);
            }

            if (spec.parts == null || spec.parts.Count == 0)
            {
                result.Errors.Add("parts 至少需要一个部件。");
                return result;
            }

            if (spec.parts.Count > 5000)
            {
                result.Errors.Add("parts 数量超过安全上限 5000。");
            }

            var analysisElements = ValidateSceneAnalysis(spec, result);

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var parents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < spec.parts.Count; i++)
            {
                var part = spec.parts[i];
                if (part == null)
                {
                    result.Errors.Add($"parts[{i}] 为空。");
                    continue;
                }

                var name = part.name?.Trim();
                if (string.IsNullOrEmpty(name))
                {
                    result.Errors.Add($"parts[{i}].name 不能为空。");
                    continue;
                }

                if (!names.Add(name))
                {
                    result.Errors.Add($"部件名称重复：{name}。");
                }

                parents[name] = part.parentName?.Trim();
                var shape = string.IsNullOrWhiteSpace(part.shape) ? "box" : part.shape.Trim().Replace('-', '_').Replace(' ', '_');
                if (!SupportedModelShapes.Contains(shape))
                {
                    result.Errors.Add($"部件 {name} 使用了不支持的 shape：{part.shape}。");
                }

                if (!IsPositiveScale(part.size))
                {
                    result.Errors.Add($"部件 {name} 的 size 必须为有限正数。");
                }

                if (string.Equals(shape, "screen_polygon", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(part.analysisElementId) ||
                        !analysisElements.Contains(part.analysisElementId.Trim()))
                    {
                        result.Errors.Add($"部件 {name} 的 screen_polygon 必须引用有效的 analysisElementId。");
                    }
                }
            }

            ValidateParentGraph(names, parents, "部件", result);
            return result;
        }

        private static HashSet<string> ValidateSceneAnalysis(
            ProBuilderModelSpec spec,
            AIBuilderValidationResult result)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (spec.sceneAnalysis?.enabled != true)
            {
                return ids;
            }

            if (spec.referenceFrame == null || !spec.referenceFrame.enabled ||
                spec.referenceFrame.width <= 0 || spec.referenceFrame.height <= 0)
            {
                result.Errors.Add("启用 sceneAnalysis 时必须提供有效的 referenceFrame 宽高。");
            }
            else if (!string.Equals(spec.referenceFrame.coordinateSystem, "normalized_top_left", StringComparison.Ordinal))
            {
                result.Errors.Add("referenceFrame.coordinateSystem 当前只支持 normalized_top_left。");
            }

            if (!IsFinite(spec.sceneAnalysis.humanHeightMeters) || spec.sceneAnalysis.humanHeightMeters <= 0f)
            {
                result.Errors.Add("sceneAnalysis.humanHeightMeters 必须为有限正数。");
            }

            if (spec.sceneAnalysis.elements == null || spec.sceneAnalysis.elements.Count == 0)
            {
                result.Errors.Add("启用 sceneAnalysis 时至少需要一个元素。");
                return ids;
            }

            foreach (var element in spec.sceneAnalysis.elements)
            {
                if (element == null || string.IsNullOrWhiteSpace(element.id))
                {
                    result.Errors.Add("sceneAnalysis.elements 中存在空元素或空 id。");
                    continue;
                }

                var id = element.id.Trim();
                if (!ids.Add(id))
                {
                    result.Errors.Add($"sceneAnalysis 元素 id 重复：{id}。");
                }

                if (!element.generateFromScreen)
                {
                    continue;
                }

                if (element.screenPolygon == null || element.screenPolygon.Count < 3)
                {
                    result.Errors.Add($"sceneAnalysis 元素 {id} 至少需要 3 个屏幕多边形点。");
                    continue;
                }

                foreach (var point in element.screenPolygon)
                {
                    if (!IsFinite(point.x) || !IsFinite(point.y) ||
                        point.x < 0f || point.x > 1f || point.y < 0f || point.y > 1f)
                    {
                        result.Errors.Add($"sceneAnalysis 元素 {id} 的屏幕坐标必须位于 0 到 1。");
                        break;
                    }
                }

                if (!IsFinite(element.depthMeters) || element.depthMeters <= 0f ||
                    !IsFinite(element.thicknessMeters) || element.thicknessMeters <= 0f)
                {
                    result.Errors.Add($"sceneAnalysis 元素 {id} 的 depthMeters 和 thicknessMeters 必须为有限正数。");
                }
            }

            return ids;
        }

        private static void ValidateTerrain(TerrainSpec terrain, AIBuilderValidationResult result)
        {
            if (terrain == null)
            {
                result.Errors.Add("terrain 不能为空。");
                return;
            }

            if (!terrain.enabled)
            {
                return;
            }

            if (!IsFinite(terrain.size.x) || !IsFinite(terrain.size.y) || terrain.size.x <= 0f || terrain.size.y <= 0f)
            {
                result.Errors.Add("terrain.size 必须为有限正数。");
            }

            if (terrain.size.x > 10000f || terrain.size.y > 10000f)
            {
                result.Errors.Add("terrain.size 超过安全上限 10000。");
            }

            if (terrain.grassPatchCount < 0 || terrain.grassPatchCount > 10000)
            {
                result.Errors.Add("terrain.grassPatchCount 必须在 0 到 10000 之间。");
            }
        }

        private static void ValidateCentralFeature(SceneSpec spec, AIBuilderValidationResult result)
        {
            if (spec.centralFeature == null)
            {
                result.Errors.Add("centralFeature 不能为空。");
                return;
            }

            if (spec.features == null || !spec.features.centralFeature)
            {
                return;
            }

            if (spec.centralFeature.altarRadius <= 0f || spec.centralFeature.altarHeight <= 0f)
            {
                result.Errors.Add("centralFeature 的 altarRadius 和 altarHeight 必须大于 0。");
            }

            if (spec.centralFeature.usePrefabAltar && HasValue(spec.centralFeature.altarPrefabGuid, spec.centralFeature.altarPrefabPath) &&
                !AIBuilderPrefabResolver.TryResolvePrefab(spec.centralFeature.altarPrefabGuid, spec.centralFeature.altarPrefabPath, out _, out _))
            {
                result.Errors.Add("centralFeature 指定的祭坛 prefab 无法解析。");
            }

            ValidatePrefabSet(
                "centralFeature.ringStone",
                spec.centralFeature.ringStonePrefabGuids,
                spec.centralFeature.ringStonePrefabPaths,
                requireExact: false,
                result);
        }

        private static void ValidateHeroTrees(SceneSpec spec, AIBuilderValidationResult result)
        {
            if (spec.features == null || !spec.features.heroForegroundTrees)
            {
                return;
            }

            ValidatePrefabSet(
                "features.heroTree",
                spec.features.heroTreePrefabGuids,
                spec.features.heroTreePrefabPaths,
                requireExact: false,
                result);
        }

        private static void ValidateSceneObjects(IReadOnlyList<SceneObjectSpec> objects, AIBuilderValidationResult result)
        {
            if (objects == null)
            {
                result.Errors.Add("objects 不能为空；没有对象时请使用空数组。");
                return;
            }

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var parents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < objects.Count; i++)
            {
                var item = objects[i];
                if (item == null)
                {
                    result.Errors.Add($"objects[{i}] 为空。");
                    continue;
                }

                var id = item.id?.Trim();
                if (string.IsNullOrEmpty(id))
                {
                    result.Errors.Add($"objects[{i}].id 不能为空。");
                    continue;
                }

                if (!ids.Add(id))
                {
                    result.Errors.Add($"对象 ID 重复：{id}。");
                }

                parents[id] = item.parentId?.Trim();
                if (!AIBuilderPrefabResolver.TryResolvePrefab(item.prefabGuid, item.prefabPath, out _, out _))
                {
                    result.Errors.Add($"对象 {id} 的 prefabGuid/prefabPath 无法解析。");
                }

                if (!IsPositiveScale(item.scale))
                {
                    result.Errors.Add($"对象 {id} 的 scale 必须为有限正数。");
                }

                if (item.targetHeight < 0f || !IsFinite(item.targetHeight))
                {
                    result.Errors.Add($"对象 {id} 的 targetHeight 必须为有限非负数。");
                }
            }

            ValidateParentGraph(ids, parents, "对象", result);
        }

        private static void ValidateScatterGroups(IReadOnlyList<ScatterGroupSpec> groups, AIBuilderValidationResult result)
        {
            if (groups == null)
            {
                result.Errors.Add("scatterGroups 不能为空；没有散布组时请使用空数组。");
                return;
            }

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                if (group == null)
                {
                    result.Errors.Add($"scatterGroups[{i}] 为空。");
                    continue;
                }

                var id = group.id?.Trim();
                if (string.IsNullOrEmpty(id))
                {
                    result.Errors.Add($"scatterGroups[{i}].id 不能为空。");
                }
                else if (!ids.Add(id))
                {
                    result.Errors.Add($"散布组 ID 重复：{id}。");
                }

                if (group.count < 0 || group.count > 10000)
                {
                    result.Errors.Add($"散布组 {id ?? i.ToString()} 的 count 必须在 0 到 10000 之间。");
                }

                if (group.areaMin.x > group.areaMax.x || group.areaMin.y > group.areaMax.y || group.areaMin.z > group.areaMax.z)
                {
                    result.Errors.Add($"散布组 {id ?? i.ToString()} 的 areaMin 不能大于 areaMax。");
                }

                if (!IsFinite(group.scaleRange.x) || !IsFinite(group.scaleRange.y) ||
                    group.scaleRange.x <= 0f || group.scaleRange.y < group.scaleRange.x)
                {
                    result.Errors.Add($"散布组 {id ?? i.ToString()} 的 scaleRange 无效。");
                }

                if (!IsFinite(group.edgeBias) || group.edgeBias < 0f || group.edgeBias > 1f)
                {
                    result.Errors.Add($"散布组 {id ?? i.ToString()} 的 edgeBias 必须在 0 到 1 之间。");
                }

                var hasExact = HasAny(group.prefabGuids) || HasAny(group.prefabPaths);
                ValidatePrefabSet($"scatterGroups[{id ?? i.ToString()}]", group.prefabGuids, group.prefabPaths, hasExact && group.requireExactPrefabMatch, result);
                if (!hasExact && !group.allowKeywordFallback && !group.allowProceduralFallback)
                {
                    result.Errors.Add($"散布组 {id ?? i.ToString()} 没有可用的 prefab 或回退方式。");
                }

                if (group.allowProceduralFallback && !SupportedProceduralTypes.Contains(group.type ?? string.Empty))
                {
                    result.Warnings.Add($"散布组 {id ?? i.ToString()} 的 type={group.type} 没有程序化回退实现；找不到 prefab 时会跳过。");
                }
            }
        }

        private static void ValidatePrefabSet(
            string label,
            IReadOnlyList<string> guids,
            IReadOnlyList<string> paths,
            bool requireExact,
            AIBuilderValidationResult result)
        {
            var requested = (guids?.Count(value => !string.IsNullOrWhiteSpace(value)) ?? 0) +
                            (paths?.Count(value => !string.IsNullOrWhiteSpace(value)) ?? 0);
            if (requested == 0)
            {
                return;
            }

            var resolved = AIBuilderPrefabResolver.ResolvePrefabs(guids, paths, out var failures);
            foreach (var failure in failures)
            {
                result.Warnings.Add($"{label}: {failure}");
            }

            if (resolved.Count == 0 && requireExact)
            {
                result.Errors.Add($"{label} 要求精确匹配，但所有 prefab GUID/路径都无法解析。");
            }
        }

        private static void ValidateParentGraph(
            HashSet<string> names,
            IReadOnlyDictionary<string, string> parents,
            string label,
            AIBuilderValidationResult result)
        {
            foreach (var pair in parents)
            {
                if (!string.IsNullOrWhiteSpace(pair.Value) && !names.Contains(pair.Value))
                {
                    result.Errors.Add($"{label} {pair.Key} 的父级不存在：{pair.Value}。");
                }
            }

            var states = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in names)
            {
                Visit(name, parents, states, label, result);
            }
        }

        private static void Visit(
            string name,
            IReadOnlyDictionary<string, string> parents,
            IDictionary<string, int> states,
            string label,
            AIBuilderValidationResult result)
        {
            if (states.TryGetValue(name, out var state))
            {
                if (state == 1)
                {
                    result.Errors.Add($"{label}父子关系存在循环：{name}。");
                }

                return;
            }

            states[name] = 1;
            if (parents.TryGetValue(name, out var parent) && !string.IsNullOrWhiteSpace(parent) && parents.ContainsKey(parent))
            {
                Visit(parent, parents, states, label, result);
            }

            states[name] = 2;
        }

        private static void RequireName(string value, string field, AIBuilderValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                result.Errors.Add($"{field} 不能为空。");
            }
        }

        private static bool IsPositiveScale(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) &&
                   value.x > 0f && value.y > 0f && value.z > 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool HasAny(IReadOnlyList<string> values)
        {
            return values != null && values.Any(value => !string.IsNullOrWhiteSpace(value));
        }

        private static bool HasValue(string guid, string path)
        {
            return !string.IsNullOrWhiteSpace(guid) || !string.IsNullOrWhiteSpace(path);
        }
    }

    internal static class AIBuilderPrefabResolver
    {
        public static bool TryResolvePrefab(string guid, string path, out GameObject prefab, out string resolvedPath)
        {
            prefab = null;
            resolvedPath = string.Empty;

            if (!string.IsNullOrWhiteSpace(guid))
            {
                var guidPath = AssetDatabase.GUIDToAssetPath(guid.Trim());
                if (TryLoadPrefab(guidPath, out prefab))
                {
                    resolvedPath = guidPath;
                    return true;
                }
            }

            var normalizedPath = NormalizePrefabPath(path);
            if (TryLoadPrefab(normalizedPath, out prefab))
            {
                resolvedPath = normalizedPath;
                return true;
            }

            return false;
        }

        public static List<GameObject> ResolvePrefabs(
            IReadOnlyList<string> guids,
            IReadOnlyList<string> paths,
            out List<string> failures)
        {
            var results = new List<GameObject>();
            var resultPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            failures = new List<string>();

            if (guids != null)
            {
                foreach (var guid in guids.Where(value => !string.IsNullOrWhiteSpace(value)))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid.Trim());
                    if (!TryLoadPrefab(path, out var prefab))
                    {
                        failures.Add($"GUID 无法解析为 prefab：{guid}");
                        continue;
                    }

                    if (resultPaths.Add(path))
                    {
                        results.Add(prefab);
                    }
                }
            }

            if (paths != null)
            {
                foreach (var requestedPath in paths.Where(value => !string.IsNullOrWhiteSpace(value)))
                {
                    var path = NormalizePrefabPath(requestedPath);
                    if (!TryLoadPrefab(path, out var prefab))
                    {
                        failures.Add($"路径无法解析为 prefab：{requestedPath}");
                        continue;
                    }

                    if (resultPaths.Add(path))
                    {
                        results.Add(prefab);
                    }
                }
            }

            return results;
        }

        private static bool TryLoadPrefab(string path, out GameObject prefab)
        {
            prefab = null;
            if (string.IsNullOrWhiteSpace(path) ||
                (!path.StartsWith("Assets/", StringComparison.Ordinal) && !path.StartsWith("Packages/", StringComparison.Ordinal)))
            {
                return false;
            }

            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return prefab != null;
        }

        private static string NormalizePrefabPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Trim().Replace('\\', '/');
        }
    }

    internal static class AIBuilderAssetPathUtility
    {
        public static bool TryNormalizeAssetsFolder(string value, out string normalized, out string error)
        {
            normalized = string.IsNullOrWhiteSpace(value) ? "Assets/AIBuilder/Generated" : value.Trim().Replace('\\', '/').TrimEnd('/');
            error = string.Empty;

            if (normalized != "Assets" && !normalized.StartsWith("Assets/", StringComparison.Ordinal))
            {
                error = $"输出目录必须位于 Assets 下：{value}";
                return false;
            }

            var parts = normalized.Split('/');
            if (parts.Any(part => string.IsNullOrWhiteSpace(part) || part == "." || part == ".."))
            {
                error = $"输出目录包含非法路径片段：{value}";
                return false;
            }

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var fullPath = Path.GetFullPath(Path.Combine(projectRoot, normalized.Replace('/', Path.DirectorySeparatorChar)));
            var assetsRoot = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(fullPath.TrimEnd(Path.DirectorySeparatorChar), Application.dataPath.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            {
                error = $"输出目录越出 Assets：{value}";
                return false;
            }

            return true;
        }

        public static void EnsureAssetFolder(string folder)
        {
            if (!TryNormalizeAssetsFolder(folder, out var normalized, out var error))
            {
                throw new ArgumentException(error, nameof(folder));
            }

            if (AssetDatabase.IsValidFolder(normalized))
            {
                return;
            }

            var parts = normalized.Split('/');
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
    }
}
